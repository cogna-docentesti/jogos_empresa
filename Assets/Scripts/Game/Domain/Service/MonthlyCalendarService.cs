using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SQLite4Unity3d;
using UnityEngine;

/// <summary>Owns durable calendar commands. Only Project exposes upcoming commitments.</summary>
public sealed class MonthlyCalendarService
{
    public const int Days = 30;

    [Serializable]
    public sealed class Bill
    {
        public string type;
        public int day;
        public long amountCents, principalCents;
    }

    [Serializable]
    public sealed class Receipt
    {
        public string occurrenceId, type;
        public int day;
        public long amountCents, principalCents, interestCents, cashBeforeCents, cashAfterCents;
    }

    public sealed class VisibleEvent
    {
        public string EventId { get; }
        public int Day { get; }
        public bool Announced { get; }
        public bool Decided { get; }

        internal VisibleEvent(string eventId, int day, bool announced, bool decided)
        {
            EventId = eventId;
            Day = day;
            Announced = announced;
            Decided = decided;
        }
    }

    public sealed class View
    {
        public int Month { get; internal set; }
        public int Day { get; internal set; }
        public int DestinationDay { get; internal set; }
        public int Revision { get; internal set; }
        public bool Moving { get; internal set; }
        public bool ReadyToClose { get; internal set; }
        public string ResultId { get; internal set; }
        public VisibleEvent Pending { get; internal set; }
        public IReadOnlyList<VisibleEvent> Events { get; internal set; }
        public IReadOnlyList<Bill> Bills { get; internal set; }
        public IReadOnlyList<Receipt> Receipts { get; internal set; }
        public long CashCents { get; internal set; }
        public long DebtCents { get; internal set; }
    }

    private sealed class Occurrence
    {
        public string id;
        public int day;
        public bool announced, revealed, decided;
        public string[] optionIds;
    }

    private sealed class Plan
    {
        public int version, revision, day, publicDestination, privateStop;
        public string pendingId, resultId;
        public GameSessionEntity opening;
        public MonthlySimulationEngine.Opening economics;
        public List<Bill> bills;
        public List<Occurrence> events;
        // Each occurrence is keyed by session/month/type/1. Its containing row is
        // updated in the same transaction as the cash and debt it reconciles.
        public Dictionary<string, Receipt> ledger = new();
    }

    private readonly SQLiteConnection db;
    private readonly MonthlyEventDecisionService decisions;
    private readonly Func<GameSessionEntity, IReadOnlyList<SessionEventHistoryEntity>, MonthlySimulationContext> loadContext;

    public MonthlyCalendarService(SQLiteConnection connection, MonthlyEventSelector selector = null,
        Func<GameSessionEntity, IReadOnlyList<SessionEventHistoryEntity>, MonthlySimulationContext> contextFactory = null,
        Func<int, int, int> nextEventDay = null)
    {
        db = connection ?? throw new ArgumentNullException(nameof(connection));
        decisions = new MonthlyEventDecisionService(db, selector, nextEventDay);
        loadContext = contextFactory ?? MonthlySimulationCatalog.Load;
    }

    public View Read(string sessionId, int month)
    {
        View result = null;
        db.RunInDatabaseLock(() =>
        {
            var row = Find(db, sessionId, month);
            if (HasCalendar(row)) result = Project(Load(row));
        });
        return result;
    }

    public View Open(string sessionId, int month, IReadOnlyList<EventData> catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        return Write(() =>
        {
            var session = decisions.RequireActiveMonth(sessionId, month);
            var row = Find(db, sessionId, month);
            if (HasCalendar(row)) return (Project(Load(row)), session);
            if (session.calendarMonthLocked != 0)
                throw new InvalidOperationException("The session's open calendar is missing.");
            if (session.initialSetupResumeState == (int)GameState.Initial_Capital
                || session.initialSetupResumeState == (int)GameState.Initial_Equipment
                || session.initialSetupResumeState == (int)GameState.Initial_Team)
                throw new InvalidOperationException("Finish the initial setup before opening the calendar.");

            var history = new SessionEventHistoryRepository(db).GetBySessionAndRound(sessionId, month);
            var context = loadContext(session.Copy(), history);
            if (context.Settings == null || context.Settings.operatingDays != Days
                || !TeamSelectionHelper.HasMinimumStaff(context.Restaurant, session.teamJson))
                throw new InvalidOperationException("A 30-day month and the minimum staffing are required.");
            var economics = MonthlySimulationEngine.Prepare(context);
            var bills = MakeBills(economics, context.Settings);
            var entries = decisions.Reserve(session, catalog, context.Restaurant, context.Equipment, Days);
            var plan = new Plan
            {
                version = 1, revision = 1, opening = session.Copy(), economics = economics,
                bills = bills, events = ImportEvents(entries, history, catalog)
            };
            row ??= Find(db, sessionId, month);
            Save(db, row, plan);
            session.calendarMonthLocked = month;
            UpdateSession(session);
            return (Project(plan), session);
        });
    }

    public View BeginAdvance(string sessionId, int month, int expectedRevision) => Change(sessionId, month, (plan, session) =>
    {
        if (plan.revision != expectedRevision) return false;
        if (plan.privateStop != 0 || plan.pendingId != null || plan.day == Days) return false;
        plan.publicDestination = NextDay(plan, visibleOnly: true);
        plan.privateStop = NextDay(plan, visibleOnly: false);
        return true;
    });

    // Intermediate animation frames are replayable. Only the authorized arrival
    // advances durable time, so an interrupted animation resumes at its origin.
    public View Step(string sessionId, int month, int movementRevision, int day) => Change(sessionId, month, (plan, session) =>
    {
        if (plan.revision != movementRevision || plan.privateStop == 0) return false;
        if (day <= plan.day || day > plan.publicDestination)
            throw new InvalidOperationException("The step is outside the active movement.");
        if (day < plan.privateStop) return false;
        PayAndArrive(plan, session);
        return true;
    });

    private View Change(string sessionId, int month, Func<Plan, GameSessionEntity, bool> change) => Write(() =>
    {
        var row = Find(db, sessionId, month);
        var plan = Load(row);
        if (plan.resultId != null) return (Project(plan), null);
        var session = decisions.RequireActiveMonth(sessionId, month);
        if (session.calendarMonthLocked != month)
            throw new InvalidOperationException("The active calendar and session do not match.");
        long cash = Cents(session.currentCash), debt = Cents(session.loanBalance);
        if (change(plan, session))
        {
            plan.revision++;
            Save(db, row, plan);
            if (cash != Cents(session.currentCash) || debt != Cents(session.loanBalance))
                UpdateSession(session);
        }
        return (Project(plan), session);
    });

    private View Write(Func<(View view, GameSessionEntity session)> action)
    {
        View view = null;
        db.RunInDatabaseLock(() =>
        {
            if (db.IsInTransaction) throw new InvalidOperationException("Calendar commands require their own transaction.");
            GameSessionEntity persisted = null;
            db.RunInTransaction(() => (view, persisted) = action());
            var current = GameSessionState.Current;
            if (persisted != null && GameSessionState.IsPersisted && current?.sessionId == persisted.sessionId
                && current.currentRound == persisted.currentRound)
                GameSessionState.AcceptMonthlySettlement(persisted);
        });
        return view;
    }

    private void UpdateSession(GameSessionEntity session)
    {
        if (db.Update(session) != 1)
            throw new InvalidOperationException("The calendar session update was not persisted.");
    }

    private static List<Bill> MakeBills(MonthlySimulationEngine.Opening economics, MonthlySimulationSettings settings)
    {
        var bills = new[]
        {
            new Bill { type = "rent", day = settings.rentDueDay, amountCents = Cents(economics.rent) },
            new Bill { type = "utilities", day = settings.utilitiesDueDay, amountCents = Cents(economics.utilities) },
            new Bill { type = "salaries", day = settings.salariesDueDay, amountCents = Cents(economics.payroll) },
            new Bill { type = "loanPayment", day = settings.loanDueDay, amountCents = Cents(economics.loanPayment),
                principalCents = Cents(economics.loanPrincipal) }
        };
        if (bills.Any(b => b.day < 1 || b.day > Days))
            throw new InvalidOperationException("Bill days must be between 1 and 30.");
        return bills.Where(b => b.amountCents > 0).ToList();
    }

    private static List<Occurrence> ImportEvents(IReadOnlyList<MonthlyEventDecisionService.Entry> entries,
        IReadOnlyList<SessionEventHistoryEntity> history, IReadOnlyList<EventData> catalog)
    {
        if (history.Any(h => string.IsNullOrWhiteSpace(h.eventId) || h.day < 1 || h.day > Days)
            || history.Select(h => h.eventId).Distinct(StringComparer.Ordinal).Count() != history.Count)
            throw new InvalidOperationException("The active month contains incompatible event history.");
        var occurrences = new List<Occurrence>();
        foreach (var entry in entries)
        {
            var recorded = history.SingleOrDefault(h => h.eventId == entry.eventId);
            if (recorded != null && recorded.day != entry.day)
                throw new InvalidOperationException("A saved choice conflicts with its reserved event day.");
            occurrences.Add(ImportEvent(entry.eventId, entry.day, recorded != null, catalog, entry.visibility));
        }
        foreach (var record in history)
            if (!occurrences.Any(e => e.id == record.eventId))
                occurrences.Add(ImportEvent(record.eventId, record.day, true, catalog, EventVisibility.Unspecified));
        return occurrences.OrderBy(e => e.day).ThenBy(e => e.id, StringComparer.Ordinal).ToList();
    }

    private static Occurrence ImportEvent(string id, int day, bool decided, IReadOnlyList<EventData> catalog,
        EventVisibility visibility)
    {
        var matches = catalog.Where(e => e != null && e.id == id);
        var data = decided ? matches.FirstOrDefault() : matches.SingleOrDefault();
        var options = decided ? null : data?.options;
        if (!decided && (options == null || options.Length == 0
            || options.Any(o => o == null || string.IsNullOrWhiteSpace(o.id) || o.effects == null)
            || options.Select(o => o.id).Distinct(StringComparer.Ordinal).Count() != options.Length))
            throw new InvalidOperationException("A reserved event has no valid options: " + id);
        return new Occurrence
        {
            id = id, day = day, decided = decided, revealed = decided,
            announced = (visibility == EventVisibility.Unspecified ? data?.calendarVisibility : visibility)
                == EventVisibility.Announced,
            optionIds = options?.Select(o => o.id).ToArray()
        };
    }

    private static int NextDay(Plan plan, bool visibleOnly) => plan.bills.Where(b => b.day > plan.day).Select(b => b.day)
        .Concat(plan.events.Where(e => e.day > plan.day && !e.decided && (!visibleOnly || e.announced)).Select(e => e.day))
        .Append(Days).Min();

    private static void PayAndArrive(Plan plan, GameSessionEntity session)
    {
        int day = plan.privateStop;
        var (cash, debt) = Balances(plan);
        RequireBalances(plan, session);
        foreach (var bill in plan.bills.Where(b => b.day == day))
        {
            string key = BillKey(plan, bill.type);
            plan.ledger.Add(key, new Receipt
            {
                occurrenceId = key, type = bill.type, day = day,
                amountCents = bill.amountCents, principalCents = bill.principalCents,
                interestCents = bill.type == "loanPayment" ? bill.amountCents - bill.principalCents : 0,
                cashBeforeCents = cash, cashAfterCents = cash - bill.amountCents
            });
            cash -= bill.amountCents;
            debt -= bill.principalCents;
        }
        session.currentCash = Money(cash);
        session.loanBalance = Money(debt);
        plan.day = day;
        plan.publicDestination = plan.privateStop = 0;
        RevealNext(plan);
    }

    private static void RevealNext(Plan plan)
    {
        var pending = plan.events.FirstOrDefault(e => e.day == plan.day && !e.decided);
        plan.pendingId = pending?.id;
        if (pending != null) pending.revealed = true;
    }

    internal static SessionEventHistoryEntity ConfirmPending(SQLiteConnection db, GameSessionEntity session, int day,
        EventData data, EventOption option, SessionEventHistoryRepository repository)
    {
        var row = Find(db, session.sessionId, session.currentRound);
        var plan = Load(row);
        if (plan.day != day || plan.pendingId != data.id)
            throw new InvalidOperationException("The event is not the calendar's pending decision.");
        var pending = plan.events.Single(e => e.id == data.id);
        if (pending.optionIds?.Contains(option.id, StringComparer.Ordinal) != true)
            throw new InvalidOperationException("The choice does not belong to this calendar occurrence.");
        var record = repository.RecordCalendarChoice(session.sessionId, session.currentRound, day, data, option);
        pending.decided = true;
        plan.revision++;
        RevealNext(plan);
        Save(db, row, plan);
        return record;
    }

    internal static MonthlyEventPlanEntity Find(SQLiteConnection db, string sessionId, int month) =>
        db.Find<MonthlyEventPlanEntity>(MonthlyEventDecisionService.PlanKey(sessionId, month));

    internal static bool HasCalendar(MonthlyEventPlanEntity row) => !string.IsNullOrEmpty(row?.calendarJson);

    internal static bool IsReadyToClose(MonthlyEventPlanEntity row) => Ready(Load(row));

    private static bool Ready(Plan plan) => plan.day == Days && plan.privateStop == 0 && plan.resultId == null
        && plan.pendingId == null && plan.events.All(e => e.decided)
        && plan.ledger.Count == plan.bills.Count;

    public RoundResultEntity Forecast(string sessionId, int month)
    {
        var row = Find(db, sessionId, month);
        if (!HasCalendar(row)) return null;
        var history = new SessionEventHistoryRepository(db).GetBySessionAndRound(sessionId, month);
        return Forecast(row, history);
    }

    internal static RoundResultEntity Forecast(MonthlyEventPlanEntity row, IReadOnlyList<SessionEventHistoryEntity> history)
    {
        var plan = Load(row);
        return MonthlySimulationEngine.Calculate(plan.opening, plan.economics, history);
    }

    internal static RoundResultEntity Reconcile(MonthlyEventPlanEntity row, GameSessionEntity current,
        IReadOnlyList<SessionEventHistoryEntity> history, out GameSessionEntity opening)
    {
        var plan = Load(row);
        if (!Ready(plan) || current.calendarMonthLocked != row.month || history.Count != plan.events.Count
            || history.Any(h => !plan.events.Any(e => e.id == h.eventId && e.day == h.day))
            || history.Select(h => h.eventId).Distinct(StringComparer.Ordinal).Count() != history.Count)
            throw new InvalidOperationException("The calendar has unfinished commitments or inconsistent event history.");
        RequireBalances(plan, current);
        opening = plan.opening;
        var result = MonthlySimulationEngine.Calculate(opening, plan.economics, history);
        var expenses = new Dictionary<string, long>
        {
            ["rent"] = Cents(result.rent), ["utilities"] = Cents(result.utilities),
            ["salaries"] = Cents(result.salaries), ["loanPayment"] = Cents(result.loanPayment)
        };
        long cash = Cents(opening.currentCash);
        foreach (var bill in plan.bills.OrderBy(b => b.day))
        {
            string key = BillKey(plan, bill.type);
            bool isLoan = bill.type == "loanPayment";
            long principal = isLoan ? Cents(result.loanPrincipalPayment) : 0;
            if (!plan.ledger.TryGetValue(key, out var receipt) || receipt.occurrenceId != key || receipt.type != bill.type
                || receipt.day != bill.day || receipt.amountCents != expenses[bill.type] || receipt.amountCents != bill.amountCents
                || receipt.principalCents != principal || receipt.principalCents != bill.principalCents || receipt.cashBeforeCents != cash
                || receipt.cashAfterCents != cash - receipt.amountCents
                || receipt.interestCents != (isLoan ? receipt.amountCents - principal : 0))
                throw new InvalidOperationException("The receipt does not match the monthly statement: " + key);
            cash = receipt.cashAfterCents;
            expenses.Remove(bill.type);
        }
        if (expenses.Values.Any(amount => amount != 0))
            throw new InvalidOperationException("A monthly expense has no receipt.");
        // The full result includes the bills for the statement. Apply only the remaining
        // cash movement to the post-payment balance represented by the receipt chain.
        long paid = Cents(opening.currentCash) - cash;
        result.closingCash = Money(cash + Cents(result.netResult) + paid);
        return result;
    }

    internal static void MarkClosed(SQLiteConnection db, MonthlyEventPlanEntity row, string resultId)
    {
        var plan = Load(row);
        plan.resultId = resultId;
        plan.revision++;
        Save(db, row, plan);
    }

    private static (long cash, long debt) Balances(Plan plan) =>
        (Cents(plan.opening.currentCash) - plan.ledger.Values.Sum(r => r.amountCents),
            Cents(plan.opening.loanBalance) - plan.ledger.Values.Sum(r => r.principalCents));

    private static void RequireBalances(Plan plan, GameSessionEntity session)
    {
        var (cash, debt) = Balances(plan);
        if (Cents(session.currentCash) != Cents(Money(cash)) || Cents(session.loanBalance) != Cents(Money(debt)))
            throw new InvalidOperationException("The saved balances do not match the calendar receipts.");
    }

    private static Plan Load(MonthlyEventPlanEntity row)
    {
        var plan = HasCalendar(row) ? JsonConvert.DeserializeObject<Plan>(row.calendarJson) : null;
        if (plan?.version != 1 || plan.opening?.sessionId != row.sessionId || plan.opening.currentRound != row.month
            || plan.economics?.days != Days || plan.bills == null || plan.events == null || plan.ledger == null
            || plan.day < 0 || plan.day > Days)
            throw new InvalidOperationException("The saved calendar is missing or incompatible.");
        return plan;
    }

    private static void Save(SQLiteConnection db, MonthlyEventPlanEntity row, Plan plan)
    {
        row.calendarJson = JsonConvert.SerializeObject(plan);
        if (db.Update(row) != 1) throw new InvalidOperationException("The calendar update was not persisted.");
    }

    private static View Project(Plan plan)
    {
        var (cash, debt) = Balances(plan);
        var visible = plan.events.Where(e => e.announced || e.revealed)
            .Select(e => new VisibleEvent(e.id, e.day, e.announced, e.decided)).ToArray();
        return new View
        {
            Month = plan.opening.currentRound, Day = plan.day, Revision = plan.revision,
            DestinationDay = plan.publicDestination == 0 ? NextDay(plan, visibleOnly: true) : plan.publicDestination,
            Moving = plan.privateStop != 0, ReadyToClose = Ready(plan), ResultId = plan.resultId,
            Pending = visible.SingleOrDefault(e => e.EventId == plan.pendingId), Events = Array.AsReadOnly(visible),
            Bills = plan.bills.AsReadOnly(),
            Receipts = plan.bills.Select(b => BillKey(plan, b.type)).Where(plan.ledger.ContainsKey)
                .Select(key => plan.ledger[key]).OrderBy(r => r.day).ToList().AsReadOnly(),
            CashCents = cash, DebtCents = debt
        };
    }

    private static string BillKey(Plan plan, string type) =>
        MonthlyEventDecisionService.PlanKey(plan.opening.sessionId, plan.opening.currentRound) + ":" + type + ":1";

    private static long Cents(float value) => checked((long)Math.Round((double)value * 100, MidpointRounding.AwayFromZero));
    private static float Money(long cents) => (float)(cents / 100m);
}
