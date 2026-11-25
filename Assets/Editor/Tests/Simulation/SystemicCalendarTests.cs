using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application.Ports.Out;
using Game.Domain.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;
using SQLite4Unity3d;

public sealed class SystemicCalendarTests
{
    private SQLiteConnection db;
    private CalendarGateway gateway;
    private SystemicCalendarService calendar;
    private const string Id = "calendar-motor-merge";

    private sealed class CalendarGateway : ISimulationGateway
    {
        private readonly SQLiteConnection db;
        public readonly SimulationCatalog Catalog = SampleCatalog.Build();
        public readonly ActiveMonthData Opening;
        public int Commits;
        public bool FailCommit;
        public MonthCommit LastCommit;
        public CalendarGateway(SQLiteConnection db)
        {
            this.db = db;
            var setup = new SessionSetup
            {
                SessionId = Id, Seed = 123456, RegionId = "university", Restaurant = RestaurantType.PODRAO,
                TargetSegment = Segment.LOW, Team = new List<RoleType> { RoleType.ATENDENTE, RoleType.ATENDENTE, RoleType.CHAPEIRO }
            };
            setup.Menu = Catalog.Restaurants[setup.Restaurant].Products.Select(p => new MenuPrice(p.Id, p.ReferencePrice)).ToList();
            setup.EquipmentIds = Catalog.Equipment.Keys.Take(3).ToList();
            Opening = new ActiveMonthData { Setup = setup, State = InitialStateFactory.Create(Catalog, setup), Month = 1, Policies = new MonthPolicies() };
        }
        public SimulationCatalog LoadCatalog() => Catalog;
        public ActiveMonthData LoadActiveMonth(string id) => Opening;
        public MonthPolicies LoadPolicies(string id, int month) => Opening.Policies;
        public void SavePolicies(string id, int month, MonthPolicies policies) => Opening.Policies = policies;
        public IReadOnlyList<MonthReport> LoadReports(string id) => LastCommit == null ? Array.Empty<MonthReport>() : new[] { LastCommit.Report };
        public CommitResult Commit(MonthCommit commit)
        {
            if (FailCommit) return new CommitResult { Status = CommitStatus.StorageError, Message = "storage unavailable" };
            db.RunInTransaction(() =>
            {
                db.Insert(new SimulationMonthEntity { id = Id + ":1", sessionId = Id, month = 1 });
                db.Insert(new RoundResultEntity { roundResultId = Id + ":1", sessionId = Id, round = 1, closingCash = (float)commit.CashAfterClose });
                var session = db.Find<GameSessionEntity>(Id);
                session.currentCash = (float)commit.CashAfterClose;
                session.currentRound = 2; session.calendarMonthLocked = 0; session.pendingSummaryResultId = Id + ":1";
                db.Update(session);
            });
            LastCommit = commit; Commits++;
            return new CommitResult { Status = CommitStatus.Ok };
        }
    }

    [SetUp]
    public void SetUp()
    {
        GameSessionState.Clear();
        db = new SQLiteConnection(":memory:");
        db.CreateTable<GameSessionEntity>(); db.CreateTable<RoundResultEntity>(); db.CreateTable<SimulationMonthEntity>();
        gateway = new CalendarGateway(db);
        db.Insert(new GameSessionEntity { sessionId = Id, status = GameSessionStatus.IN_PROGRESS, currentRound = 1,
            userId = "test", professorId = "test", cityId = "test", coherenceRating = "test",
            teamJson = "{\"members\":[]}", equipmentJson = "{\"equipmentIds\":[]}",
            currentCash = (float)gateway.Opening.State.Cash, loanBalance = (float)gateway.Opening.State.LoanBalance });
        calendar = new SystemicCalendarService(db, gateway);
    }

    [TearDown] public void TearDown() { db.Close(); GameSessionState.Clear(); }

    private void Resolve()
    {
        while (calendar.HasPending)
        {
            var choice = calendar.Options.FirstOrDefault(o => o.Enabled && o.Cost == 0) ?? calendar.Options.First(o => o.Enabled);
            calendar.Choose(calendar.Revision, calendar.Pending.OccurrenceId, choice.Reaction.Id);
        }
    }

    private void Complete()
    {
        int guard = 200;
        while (!calendar.Current.IsFinished && guard-- > 0) { Resolve(); calendar.Step(calendar.Revision); }
        Assert.Greater(guard, 0);
    }

    [Test]
    public void ReloadReplaysTheSameDayCashAndPendingOccurrence()
    {
        calendar.Open(Id, 1);
        for (int i = 0; i < 12 && !calendar.HasPending; i++) calendar.Step(calendar.Revision);
        var before = calendar.Project();
        var reloaded = new SystemicCalendarService(db, gateway);
        Assert.IsTrue(reloaded.Read(Id, 1));
        var after = reloaded.Project();
        Assert.AreEqual(before.Day, after.Day); Assert.AreEqual(before.CashCents, after.CashCents);
        Assert.AreEqual(before.Pending?.EventId, after.Pending?.EventId);
        Assert.AreEqual(before.Revision, after.Revision);
    }

    [Test]
    public void StaleAdvanceDoesNotAdvanceOrDebitTwice()
    {
        calendar.Open(Id, 1);
        int revision = calendar.Revision;
        calendar.Step(revision);
        string saved = db.Find<SystemicCalendarEntity>(Id + ":1").journalJson;
        float cash = db.Find<GameSessionEntity>(Id).currentCash;
        calendar.Step(revision);
        Assert.AreEqual(saved, db.Find<SystemicCalendarEntity>(Id + ":1").journalJson);
        Assert.AreEqual(cash, db.Find<GameSessionEntity>(Id).currentCash);
    }

    [Test]
    public void ConfirmedReactionSurvivesReloadAndStaleConfirmationDoesNotRepeatIt()
    {
        var definition = gateway.Catalog.Events.First(e => e.RequiresDecision && !e.Deterministic);
        definition.BaseRatePerMonth = 1000000; definition.IsNegative = false;
        definition.Conditions.Clear(); definition.OnlyRegions = null; definition.OnlyRestaurants = null;
        calendar.Open(Id, 1);
        while (!calendar.HasPending && !calendar.Current.IsFinished) calendar.Step(calendar.Revision);
        Assert.IsTrue(calendar.HasPending);
        string occurrence = calendar.Pending.OccurrenceId;
        var option = calendar.Options.First(o => o.Enabled);
        int revision = calendar.Revision;
        calendar.Choose(revision, occurrence, option.Reaction.Id);
        string saved = db.Find<SystemicCalendarEntity>(Id + ":1").journalJson;
        float cash = db.Find<GameSessionEntity>(Id).currentCash;
        calendar.Choose(revision, occurrence, option.Reaction.Id);
        Assert.AreEqual(saved, db.Find<SystemicCalendarEntity>(Id + ":1").journalJson);
        Assert.AreEqual(cash, db.Find<GameSessionEntity>(Id).currentCash);
        calendar.Read(Id, 1);
        Assert.AreEqual(option.Reaction.Id, calendar.Current.Occurrences.Single(e => e.OccurrenceId == occurrence).ChosenReactionId);
    }

    [Test]
    public void StorageFailureRollsBackProgressAndCashAndCanRetry()
    {
        calendar.Open(Id, 1);
        string saved = db.Find<SystemicCalendarEntity>(Id + ":1").journalJson;
        float cash = db.Find<GameSessionEntity>(Id).currentCash;
        db.Execute("CREATE TRIGGER fail_calendar BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'calendar failure'); END");
        Assert.Throws<SQLiteException>(() => calendar.Step(calendar.Revision));
        Assert.AreEqual(saved, db.Find<SystemicCalendarEntity>(Id + ":1").journalJson);
        Assert.AreEqual(cash, db.Find<GameSessionEntity>(Id).currentCash);
        Assert.AreEqual(0, calendar.Current.Day);
        db.Execute("DROP TRIGGER fail_calendar"); calendar.Step(calendar.Revision);
        Assert.AreEqual(1, calendar.Current.Day);
    }

    [Test]
    public void FinishedMonthMatchesEngineAndCommitsOnlyOnceAfterRetry()
    {
        calendar.Open(Id, 1); Complete();
        var expected = MonthCloser.Close(calendar.Current, Id).Report;
        gateway.FailCommit = true;
        Assert.Throws<InvalidOperationException>(() => calendar.Finish());
        Assert.AreEqual(0, gateway.Commits); Assert.AreEqual(0, db.Table<RoundResultEntity>().Count());
        gateway.FailCommit = false;
        var result = calendar.Finish();
        Assert.AreEqual(expected.NetIncome, gateway.LastCommit.Report.NetIncome, 0.001);
        Assert.AreEqual(expected.CashResult, gateway.LastCommit.Report.CashResult, 0.001);
        Assert.AreEqual(0, gateway.LastCommit.Report.BalanceDifference, 0.01);
        Assert.AreEqual(result.roundResultId, calendar.Finish().roundResultId);
        Assert.AreEqual(1, gateway.Commits); Assert.AreEqual(1, db.Table<RoundResultEntity>().Count());
    }

    [Test]
    public void JournalFreezesInputsAndDoesNotExposeFutureRandomEvents()
    {
        calendar.Open(Id, 1);
        var opening = calendar.Project();
        Assert.IsEmpty(opening.Events);
        gateway.Catalog.Constants.RentDay = 30;
        gateway.Opening.Policies.Marketing = "high";
        Assert.IsTrue(calendar.Read(Id, 1));
        Assert.AreEqual(5, calendar.Current.Catalog.Constants.RentDay);
        Assert.AreEqual("none", calendar.Current.Policies.Marketing);
        Complete();
        Assert.IsTrue(calendar.Project().Events.All(e => e.Day <= calendar.Current.Day));
    }

    [Test]
    public void FutureSurprisesAreReservedButOnlyAnnouncedEventsAppearInTheProjection()
    {
        foreach (var definition in gateway.Catalog.Events) definition.Announced = true;
        calendar.Open(Id, 1);
        Assert.IsNotNull(calendar.Current.EventPlan);
        Assert.That(calendar.Current.EventPlan.Events.Count, Is.InRange(4, 5));
        var announced = calendar.Project();
        Assert.AreEqual(calendar.Current.EventPlan.Events.Count, announced.Events.Count);
        Assert.IsTrue(announced.Events.All(e => e.Announced && e.Day > 0));
        string frozen = JsonConvert.SerializeObject(calendar.Current.EventPlan);
        calendar.Read(Id, 1);
        Assert.AreEqual(frozen, JsonConvert.SerializeObject(calendar.Current.EventPlan));
    }

    [Test]
    public void OldDailyJournalKeepsItsExactReplayAndIsNotRewrittenDuringUpgrade()
    {
        var raw = new MonthSimulation(gateway.Catalog, gateway.Opening.Setup, gateway.Opening.State, gateway.Opening.Policies);
        var old = new SystemicCalendarService.Journal { catalog = gateway.Catalog, opening = gateway.Opening };
        for (int i = 0; i < 12; i++)
        {
            if (raw.IsAwaitingDecision)
            {
                var occurrence = raw.PendingDecisions[0];
                var choice = raw.ReactionOptions(occurrence).FirstOrDefault(o => o.Enabled && o.Cost == 0)
                    ?? raw.ReactionOptions(occurrence).First(o => o.Enabled);
                raw.Choose(occurrence.OccurrenceId, choice.Reaction.Id);
                old.commands.Add(new SystemicCalendarService.Command { occurrence = occurrence.OccurrenceId, reaction = choice.Reaction.Id });
            }
            else { raw.AdvanceDay(); old.commands.Add(new SystemicCalendarService.Command()); }
        }
        string stored = JsonConvert.SerializeObject(old);
        db.Insert(new SystemicCalendarEntity { id = Id + ":1", sessionId = Id, month = 1, revision = 13, journalJson = stored });
        var session = db.Find<GameSessionEntity>(Id);
        session.calendarMonthLocked = 1; session.currentCash = (float)raw.State.Cash; db.Update(session);
        calendar.Open(Id, 1);
        Assert.IsNull(calendar.Current.EventPlan);
        Assert.AreEqual(raw.Day, calendar.Current.Day);
        Assert.AreEqual(raw.State.Cash, calendar.Current.State.Cash, .00001);
        Assert.AreEqual(stored, db.Find<SystemicCalendarEntity>(Id + ":1").journalJson);
        Assert.AreEqual(session.currentCash, db.Find<GameSessionEntity>(Id).currentCash);
    }

    [Test]
    public void ExistingLegacyCampaignStartsAtItsCurrentMonthAndBalances()
    {
        var session = db.Find<GameSessionEntity>(Id);
        session.currentRound = 2; session.currentCash = 42321.50f; session.loanBalance = 0;
        db.Update(session);
        calendar.Open(Id, 2);
        Assert.AreEqual(2, calendar.Current.Month);
        Assert.AreEqual(42321.50, calendar.Current.State.Cash, 0.001);
        Assert.AreEqual(0, calendar.Current.State.LoanBalance);
    }
}
