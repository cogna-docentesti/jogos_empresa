using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application.Ports.Out;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using Newtonsoft.Json;
using SQLite4Unity3d;

/// <summary>Runs the systemic engine behind the existing calendar, with durable daily progress.</summary>
public sealed class SystemicCalendarService
{
    public sealed class Command
    {
        public string occurrence, reaction;
    }

    public sealed class Journal
    {
        public SimulationCatalog catalog;
        public ActiveMonthData opening;
        public List<Command> commands = new();
    }

    private sealed class FrozenGateway : ISimulationGateway
    {
        private readonly ISimulationGateway storage;
        private readonly Journal journal;
        public FrozenGateway(ISimulationGateway storage, Journal journal) { this.storage = storage; this.journal = journal; }
        public SimulationCatalog LoadCatalog() => journal.catalog;
        public ActiveMonthData LoadActiveMonth(string id) => Clone(journal.opening);
        public MonthPolicies LoadPolicies(string id, int month) => journal.opening.Policies.Clone();
        public void SavePolicies(string id, int month, MonthPolicies policies) => throw new InvalidOperationException("O mês está aberto.");
        public CommitResult Commit(MonthCommit commit) => storage.Commit(commit);
        public IReadOnlyList<MonthReport> LoadReports(string id) => storage.LoadReports(id);
    }

    private readonly SQLiteConnection db;
    private readonly ISimulationGateway storage;
    private SystemicMonthService service;
    private SystemicCalendarEntity row;
    private Journal journal;
    public MonthSimulation Current => service?.Current;
    public int Revision => row?.revision ?? 0;
    public bool HasPending => Current?.IsAwaitingDecision == true;
    public EventOccurrence Pending => HasPending ? Current.PendingDecisions[0] : null;
    public IReadOnlyList<ReactionOption> Options => service.ReactionsFor(Pending);

    public SystemicCalendarService(SQLiteConnection connection, ISimulationGateway gateway)
    {
        db = connection ?? throw new ArgumentNullException(nameof(connection));
        storage = gateway ?? throw new ArgumentNullException(nameof(gateway));
        db.CreateTable<SystemicCalendarEntity>();
    }

    public bool Read(string sessionId, int month)
    {
        row = db.Find<SystemicCalendarEntity>(Key(sessionId, month));
        if (row == null) { service = null; journal = null; return false; }
        journal = JsonConvert.DeserializeObject<Journal>(row.journalJson);
        Replay();
        return true;
    }

    public void Open(string sessionId, int month)
    {
        db.RunInDatabaseLock(() =>
        {
            if (Read(sessionId, month)) return;
            var session = RequireSession(sessionId, month);
            if (session.calendarMonthLocked != 0 || session.initialSetupResumeState == (int)GameState.Initial_Capital
                || session.initialSetupResumeState == (int)GameState.Initial_Equipment || session.initialSetupResumeState == (int)GameState.Initial_Team)
                throw new InvalidOperationException("Conclua a preparação antes de abrir o mês.");
            var active = storage.LoadActiveMonth(sessionId);
            if (active == null || active.IsFinished || active.SetupErrors.Count > 0)
                throw new InvalidOperationException(active == null ? "Partida indisponível." : string.Join(" ", active.SetupErrors));
            // Hub purchases/loans are already paid by the session. Carry its actual balances
            // into the engine, including campaigns whose previous month used legacy economics.
            active.State.RetainedEarnings += session.currentCash - active.State.Cash
                - (session.loanBalance - active.State.LoanBalance);
            active.State.Cash = session.currentCash;
            active.State.LoanBalance = session.loanBalance;
            active.State.Month = active.Month = month;
            active.Cash = session.currentCash;
            if (month > 1) active.State.Reputation = session.reputationScore;
            journal = Clone(new Journal { catalog = storage.LoadCatalog(), opening = active });
            if (journal.catalog.Constants.DaysPerMonth != MonthlyCalendarService.Days)
                throw new InvalidOperationException("O calendário requer um mês de 30 dias.");
            row = new SystemicCalendarEntity { id = Key(sessionId, month), sessionId = sessionId, month = month, revision = 1 };
            Replay();
            db.RunInTransaction(() => Save(session));
            Sync(session);
        });
    }

    private void Replay()
    {
        service = new SystemicMonthService(new FrozenGateway(storage, journal));
        service.Begin(row.sessionId);
        foreach (var command in journal.commands)
            if (command.occurrence == null) service.Step();
            else service.Choose(command.occurrence, command.reaction);
    }

    /// <summary>One engine step, including the opening of a day that pauses for a decision.</summary>
    public void Step(int revision) => Change(revision, () =>
    {
        if (HasPending || Current.IsFinished) return false;
        service.Step();
        journal.commands.Add(new Command());
        return true;
    });

    public void Choose(int revision, string occurrence, string reaction) => Change(revision, () =>
    {
        if (Pending?.OccurrenceId != occurrence) throw new InvalidOperationException("Esta ocorrência não está pendente.");
        service.Choose(occurrence, reaction);
        journal.commands.Add(new Command { occurrence = occurrence, reaction = reaction });
        return true;
    });

    private void Change(int revision, Func<bool> change)
    {
        db.RunInDatabaseLock(() =>
        {
            string id = row.sessionId;
            int month = row.month;
            Read(id, month);
            if (revision != Revision) return; // stale callbacks never advance or charge twice
            var session = RequireSession(id, month);
            if (session.calendarMonthLocked != month) throw new InvalidOperationException("O mês não está aberto.");
            try
            {
                if (!change()) return;
                row.revision++;
                db.RunInTransaction(() => Save(session));
                Sync(session);
            }
            catch { Read(id, month); throw; }
        });
    }

    private void Save(GameSessionEntity session)
    {
        row.journalJson = JsonConvert.SerializeObject(journal);
        db.InsertOrReplace(row);
        session.calendarMonthLocked = row.month;
        session.currentCash = (float)Current.State.Cash;
        session.loanBalance = (float)Current.State.LoanBalance;
        if (db.Update(session) != 1) throw new InvalidOperationException("Não foi possível salvar o calendário.");
    }

    public RoundResultEntity Finish()
    {
        // The gateway has its own result key; identify completed months by their durable report.
        var closed = db.Find<SimulationMonthEntity>(Key(row.sessionId, row.month));
        if (closed != null) return db.Table<RoundResultEntity>().FirstOrDefault(r => r.sessionId == row.sessionId && r.round == row.month);
        Read(row.sessionId, row.month);
        RequireSession(row.sessionId, row.month);
        if (!Current.IsFinished || HasPending) throw new InvalidOperationException("Conclua os compromissos do mês.");
        var (result, _) = service.Finish();
        if (!result.Ok) throw new InvalidOperationException(result.Message);
        return db.Table<RoundResultEntity>().FirstOrDefault(r => r.sessionId == row.sessionId && r.round == row.month);
    }

    public bool IsCheckpoint(int day)
    {
        var k = Current.Catalog.Constants;
        return day == k.MarketingDay || day == k.RentDay || day == k.EnergyDay || day == k.MaintenanceDay
            || day == k.LoanDay || day == k.WaterDay || day == k.SalaryDay || day == k.TaxDay
            || day == k.DaysPerMonth || Current.Occurrences.Any(e => e.Day == day);
    }

    public MonthlyCalendarService.View Project()
    {
        var month = Current;
        var k = month.Catalog.Constants;
        var bills = new List<MonthlyCalendarService.Bill>();
        var receipts = new List<MonthlyCalendarService.Receipt>();
        void Bill(string type, int day, params CashLine[] lines)
        {
            var entries = month.Books.Entries.Where(e => e.IsCash && e.Day == day && lines.Contains((CashLine)e.Line)).ToList();
            long amount = -entries.Sum(e => Cents(e.Amount));
            bills.Add(new MonthlyCalendarService.Bill { type = type, day = day, amountCents = amount, estimated = day > month.Day });
            if (day <= month.Day && entries.Count > 0)
                receipts.Add(new MonthlyCalendarService.Receipt { type = type, day = day, amountCents = amount });
        }
        Bill("marketing", k.MarketingDay, CashLine.Marketing);
        Bill("rent", k.RentDay, CashLine.Rent);
        Bill("utilities", k.EnergyDay, CashLine.Bills);
        Bill("maintenance", k.MaintenanceDay, CashLine.Maintenance);
        Bill("loanPayment", k.LoanDay, CashLine.Interest, CashLine.Amortization);
        Bill("water", k.WaterDay, CashLine.Bills);
        Bill("salaries", k.SalaryDay, CashLine.Salaries);
        Bill("taxes", k.TaxDay, CashLine.Taxes);
        var events = month.Occurrences.Select(e => new MonthlyCalendarService.VisibleEvent(e.OccurrenceId, e.Day, false,
            !e.RequiresDecision || e.ChosenReactionId != null, e.Definition.Title)).ToArray();
        return new MonthlyCalendarService.View
        {
            Month = month.Month, Day = month.Day, Revision = Revision,
            CashCents = Cents(month.State.Cash), DebtCents = Cents(month.State.LoanBalance),
            Bills = bills, Receipts = receipts, Events = events,
            Pending = Pending == null ? null : events.First(e => e.EventId == Pending.OccurrenceId),
            ReadyToClose = month.IsFinished && !HasPending
        };
    }

    private GameSessionEntity RequireSession(string id, int month)
    {
        var session = db.Find<GameSessionEntity>(id);
        if (session == null || session.currentRound != month || session.status != GameSessionStatus.IN_PROGRESS
            || !string.IsNullOrEmpty(session.pendingSummaryResultId))
            throw new InvalidOperationException("A sessão não corresponde ao mês aberto.");
        return session;
    }

    private static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
    private static string Key(string id, int month) => id + ":" + month;
    private static long Cents(double value) => (long)Math.Round(value * 100, MidpointRounding.AwayFromZero);
    private static void Sync(GameSessionEntity session)
    {
        if (GameSessionState.IsPersisted && GameSessionState.Current?.sessionId == session.sessionId)
            GameSessionState.AcceptMonthlySettlement(session);
    }
}
