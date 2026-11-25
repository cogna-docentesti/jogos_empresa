using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Service;
using NUnit.Framework;
using SQLite4Unity3d;
using UnityEngine;
using Object = UnityEngine.Object;

public class MonthlySimulationTests
{
    private readonly List<ScriptableObject> _assets = new List<ScriptableObject>();
    private SQLiteConnection _db;
    private GameObject _databaseObject;
    private MonthlySimulationContext _context;

    [SetUp]
    public void SetUp()
    {
        GameSessionState.Clear();
        _db = new SQLiteConnection(":memory:");
        _db.CreateTable<GameSessionEntity>();
        _db.CreateTable<RoundResultEntity>();
        _db.CreateTable<MonthlyEventPlanEntity>();
        _db.CreateTable<SessionEventHistoryEntity>();
        _databaseObject = new GameObject("MonthlySimulationTestDatabase");
        var service = _databaseObject.AddComponent<DatabaseService>();
        typeof(DatabaseService).GetProperty("Connection").SetValue(service, _db);
        typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, service);

        var first = Product("first", 20, 10, 50, 0.3f);
        var second = Product("second", 40, 20, 60, 0.5f);
        var restaurant = Asset<RestaurantData>();
        restaurant.id = "restaurant";
        restaurant.type = RestaurantType.PODRAO;
        restaurant.products = new[] { first, second };
        restaurant.requiredRoles = new[] { new RoleRequirement { roleId = "attendant", quantity = 2 }, new RoleRequirement { roleId = "cook", quantity = 1 } };
        restaurant.baseMonthlyCost = 100;
        var location = Asset<LocationData>();
        location.zone = LocationZone.Comercio;
        location.baseDailyDemand = 10;
        location.initialPhysicalCapacity = 100;
        location.rent = 500;
        var attendant = Asset<RoleData>();
        attendant.id = "attendant"; attendant.roleType = RoleType.ATENDENTE;
        attendant.salary = 1000; attendant.maxClientsSupported = 500;
        var cook = Asset<RoleData>();
        cook.id = "cook"; cook.roleType = RoleType.CHAPEIRO;
        cook.salary = 2000; cook.maxClientsSupported = 500;
        var session = new GameSessionService("monthly_test", "test_professor").CreateNewSession();
        session.restaurantType = restaurant.type;
        session.locationZone = location.zone;
        session.targetSegment = Segment.LOW;
        session.currentCash = 10000f;
        session.menuPricingJson = MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(restaurant.products));
        session.teamJson = "{\"members\":[{\"roleId\":\"attendant\",\"quantity\":2},{\"roleId\":\"cook\",\"quantity\":1}]}";
        _context = new MonthlySimulationContext
        {
            Session = session, Restaurant = restaurant, Location = location,
            Roles = new[] { attendant, cook }, Settings = Asset<MonthlySimulationSettings>()
        };
        Assert.IsTrue(GameSessionState.Persist());
    }

    [TearDown]
    public void TearDown()
    {
        GameSessionState.Clear();
        _db.Close();
        typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, null);
        Object.DestroyImmediate(_databaseObject);
        foreach (var asset in _assets) Object.DestroyImmediate(asset);
        _assets.Clear();
    }

    [Test]
    public void KnownMonthlyStatementUsesSelectedPricesAndCatalogCosts()
    {
        var result = Calculate();
        Assert.AreEqual(300, result.baseDemand);
        Assert.AreEqual(360, result.potentialDemand);
        Assert.AreEqual(360, result.customers);
        Assert.AreEqual(30f, result.averageTicket);
        Assert.AreEqual(10800f, result.grossRevenue);
        Assert.AreEqual(4680f, result.supplyCost);
        Assert.AreEqual(4000f, result.salaries);
        Assert.AreEqual(500f, result.rent);
        Assert.AreEqual(100f, result.utilities);
        Assert.AreEqual(1520f, result.netResult);
        Assert.AreEqual(11520f, result.closingCash);
        Assert.AreEqual(52, result.reputationAtEnd);
        Assert.AreEqual(0f, result.thirteenthSalary);
    }

    [Test]
    public void RepeatedCalculationIsDeterministicAndDoesNotChangeInputs()
    {
        var before = _context.Session.Copy();
        var first = Calculate();
        var second = Calculate();
        Assert.AreEqual(first.grossRevenue, second.grossRevenue);
        Assert.AreEqual(first.closingCash, second.closingCash);
        Assert.IsNull(first.roundResultId);
        Assert.AreEqual(0, first.createdAt);
        Assert.AreEqual(before.currentCash, _context.Session.currentCash);
        Assert.AreEqual(before.reputationScore, _context.Session.reputationScore);
        Assert.AreEqual(before.currentRound, _context.Session.currentRound);
    }

    [Test]
    public void HigherPricesReduceDemandAndReputationFeedsTheNextMonth()
    {
        int baseline = Calculate().potentialDemand;
        _context.Session.menuPricingJson = "{\"items\":[{\"productId\":\"first\",\"selectedPrice\":50},{\"productId\":\"second\",\"selectedPrice\":60}]}";
        Assert.Less(Calculate().potentialDemand, baseline);
        _context.Session.reputationScore = 0;
        int poor = Calculate().potentialDemand;
        _context.Session.reputationScore = 100;
        Assert.Greater(Calculate().potentialDemand, poor);
    }

    [Test]
    public void CapacityLimitsCustomersAndEmptyTeamCannotSell()
    {
        _context.Location.initialPhysicalCapacity = 2;
        Assert.AreEqual(60, Calculate().customers);
        Assert.AreEqual(45, Calculate().reputationAtEnd);
        _context.Session.teamJson = "{\"members\":[]}";
        var result = Calculate();
        Assert.AreEqual(0, result.customers);
        Assert.AreEqual(0, result.grossRevenue);
        Assert.AreEqual(0, result.salaries);
        Assert.AreEqual(-600f, result.netResult);
    }

    [Test]
    public void MissingSpecialistPreventsOperationAndQuantitiesAffectAlignment()
    {
        _context.Session.teamJson = "{\"members\":[{\"roleId\":\"attendant\",\"quantity\":1}]}";
        var result = Calculate();
        Assert.AreEqual(0, result.customers);
        Assert.AreEqual(1000f, result.salaries);
        Assert.AreEqual(5, AlignmentEngine.Calculate(_context.Session.restaurantType, _context.Session.targetSegment,
            _context.Session.locationZone, _context.Restaurant, MenuPricingHelper.FromJson(_context.Session.menuPricingJson),
            TeamSelectionHelper.FromJson(_context.Session.teamJson), _context.Roles.ToList()).restaurantTeamScore);
    }

    [Test]
    public void EquipmentCapacityIsCountedOnceAndDoesNotRepeatPurchaseCosts()
    {
        _context.Location.initialPhysicalCapacity = 2;
        var equipment = Asset<EquipmentData>();
        equipment.id = "extra"; equipment.cost = 9999; equipment.capacityBonus = 60;
        _context.Equipment = new[] { equipment };
        _context.Session.equipmentJson = "{\"equipmentIds\":[\"extra\",\"extra\"]}";
        var result = Calculate();
        Assert.AreEqual(120, result.customers);
        Assert.AreEqual(120, result.serviceCapacity);
        Assert.AreEqual(600f, result.FixedCosts);
    }

    [Test]
    public void SignedEventCashChangesAreAddedAndReputationIsClamped()
    {
        _context.Events = new[] { History(1, -200f, -100), History(1, 50f, 0) };
        var result = Calculate();
        Assert.AreEqual(-150f, result.eventCashImpact);
        Assert.AreEqual(1370f, result.netResult);
        Assert.AreEqual(0, result.reputationAtEnd);
        _context.Events = new[] { History(1, 0, 100) };
        Assert.AreEqual(100, Calculate().reputationAtEnd);
    }

    [Test]
    public void DailyAndMonthlyEffectsRespectTheirRecordedStartDay()
    {
        var daily = History(30, 0, 0);
        daily.effectsJson = JsonUtility.ToJson(new EventEffectData { demandMultiplier = 2f, duration = EventEffectDuration.CURRENT_DAY });
        _context.Events = new[] { daily };
        Assert.AreEqual(372, Calculate().customers);
        daily.day = 16;
        daily.effectsJson = JsonUtility.ToJson(new EventEffectData { demandMultiplier = 2f, duration = EventEffectDuration.CURRENT_MONTH });
        Assert.AreEqual(540, Calculate().customers);
        daily.day = 30;
        Assert.AreEqual(372, Calculate().customers);
    }

    [Test]
    public void TicketAndIngredientEffectsChangeRevenueAndSuppliesIndependently()
    {
        var history = History(1, 0, 0);
        history.effectsJson = JsonUtility.ToJson(new EventEffectData
        { averageTicketMultiplier = 2f, ingredientCostMultiplier = 1.5f, duration = EventEffectDuration.CURRENT_MONTH });
        _context.Events = new[] { history };
        var result = Calculate();
        Assert.AreEqual(21600f, result.grossRevenue);
        Assert.AreEqual(7020f, result.supplyCost);
    }

    [Test]
    public void RecordedOptionKeepsAnImmutableEffectSnapshot()
    {
        var option = new EventOption { id = "choice", effects = new EventEffectData { demandMultiplier = 1.5f, cashDelta = -100f } };
        var history = new SessionEventHistoryRepository(_db).RecordFromOption(_context.Session.sessionId, 1, 1, null, option);
        option.effects.demandMultiplier = 9f;
        Assert.AreEqual(1.5f, JsonUtility.FromJson<EventEffectData>(history.effectsJson).demandMultiplier);
        Assert.AreEqual(-100f, history.cashChange);
    }

    [Test]
    public void EventsCannotChangeAnAlreadySettledStatement()
    {
        Settle(Service(), 1);
        Assert.Throws<InvalidOperationException>(() => new SessionEventHistoryRepository(_db)
            .Record(_context.Session.sessionId, 1, 1, null, null, 999f, 10, 0));
        Assert.AreEqual(0, _db.Table<SessionEventHistoryEntity>().Count());
    }

    [Test]
    public void ZeroStockStopsSalesAndFractionalDemandNeverCreatesFractionalCustomers()
    {
        var history = History(1, 0, 0);
        history.effectsJson = JsonUtility.ToJson(new EventEffectData { stockMultiplier = 0f, duration = EventEffectDuration.CURRENT_MONTH });
        _context.Events = new[] { history };
        Assert.AreEqual(0f, Calculate().grossRevenue);
        _context.Events = Array.Empty<SessionEventHistoryEntity>();
        _context.Settings.priceElasticity = 0.5f;
        _context.Session.reputationScore = 51;
        var result = Calculate();
        Assert.AreEqual(363, result.customers);
        Assert.AreEqual(10890f, result.grossRevenue);
    }

    [TestCase(0)]
    [TestCase(4)]
    public void InvalidMonthIsRejected(int month)
    {
        _context.Session.currentRound = month;
        Assert.Throws<InvalidOperationException>(() => Calculate());
    }

    [Test]
    public void InvalidCatalogAndNonFiniteValuesFailBeforeSettlement()
    {
        var role = _context.Roles[0];
        role.salary = -1;
        Assert.Throws<InvalidOperationException>(() => Calculate());
        role.salary = 1000;
        _context.Session.currentCash = float.NaN;
        Assert.Throws<InvalidOperationException>(() => Calculate());
        _context.Session.currentCash = 10000;
        _context.Session.teamJson = "{\"members\":[{\"roleId\":\"missing\",\"quantity\":1}]}";
        Assert.Throws<InvalidOperationException>(() => Calculate());
    }

    [Test]
    public void SettlementPersistsStatementAndSessionAndExplicitRetryIsIdempotent()
    {
        var service = Service();
        var first = Settle(service, 1);
        float cash = _context.Session.currentCash;
        var retry = service.ProcessRound(1);
        Assert.AreEqual(first.roundResultId, retry.roundResultId);
        Assert.AreEqual(1, _db.Table<RoundResultEntity>().Count());
        Assert.AreEqual(2, _context.Session.currentRound);
        Assert.AreEqual(cash, _context.Session.currentCash);
        var stored = new GameSessionRepository(_db).GetById(_context.Session.sessionId);
        Assert.AreEqual(first.closingCash, stored.currentCash);
        Assert.AreEqual(first.reputationAtEnd, stored.reputationScore);
        Assert.IsFalse(GameSessionState.HasUnsavedChanges);
    }

    [Test]
    public void DatabaseFailureRollsBackResultAndPreservesMemoryAndAllowsRetry()
    {
        CompleteCalendar();
        float cash = _context.Session.currentCash;
        _db.Execute("CREATE TRIGGER reject_month BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'Injected settlement failure'); END");
        Assert.Throws<SQLiteException>(() => Service().ProcessRound(1));
        Assert.AreEqual(0, _db.Table<RoundResultEntity>().Count());
        Assert.AreEqual(1, _context.Session.currentRound);
        Assert.AreEqual(cash, _context.Session.currentCash);
        Assert.AreEqual(50, _context.Session.reputationScore);
        Assert.AreEqual(cash, _db.Table<GameSessionEntity>().Single().currentCash);
        _db.Execute("DROP TRIGGER reject_month");
        Assert.IsNotNull(Service().ProcessRound(1));
    }

    [Test]
    public void ThreeMonthsCompleteTheQuarterAndLoanIsFullyRepaid()
    {
        var line = Asset<CreditLineData>();
        line.id = "credit"; line.monthlyInterestRate = 0.02f;
        _context.CreditLine = line;
        _context.Session.loanBalance = 1000f;
        _context.Session.creditLineId = line.id;
        GameSessionState.Save();
        var service = Service();
        var first = Settle(service, 1);
        Assert.AreEqual(353.33f, first.loanPayment);
        Settle(service, 2);
        var last = Settle(service, 3);
        Assert.AreEqual(GameSessionStatus.COMPLETED, _context.Session.status);
        Assert.AreEqual(0f, _context.Session.loanBalance);
        Assert.IsNotNull(_context.Session.completedAt);
        Assert.AreEqual(0f, last.thirteenthSalary);
        Assert.IsNull(service.ProcessRound());
        Assert.IsNull(service.ProcessRound(4));
        Assert.AreEqual(3, _db.Table<RoundResultEntity>().Count());
    }

    [Test]
    public void MonthTwoLoanUsesTwoInstallmentsAndClearsTheRemainingBalance()
    {
        var service = Service();
        Settle(service, 1);
        AcknowledgeTestSummary();
        var line = Asset<CreditLineData>();
        line.id = "late_credit"; line.monthlyInterestRate = 0.02f;
        _context.CreditLine = line;
        _context.Session.loanBalance = 900f;
        _context.Session.creditLineId = line.id;
        GameSessionState.Save();
        Assert.AreEqual(468f, Settle(service, 2).loanPayment);
        Assert.AreEqual(459f, Settle(service, 3).loanPayment);
        Assert.AreEqual(0f, _context.Session.loanBalance);
    }

    [Test]
    public void GameFlowUsesTheEngineAndReachesTheFinalReportAfterMonthThree()
    {
        ConfigureShippedMenuAndTeam();
        GameSessionState.Save();
        var managerObject = new GameObject("MonthlyFlowTestManager");
        var manager = managerObject.AddComponent<GameManager>();
        var states = new GameStateMachine();
        typeof(GameManager).GetProperty("Instance").SetValue(null, manager);
        typeof(GameManager).GetProperty("StateMachine").SetValue(manager, states);
        try
        {
            var service = new GameSessionService(_context.Session.userId, _context.Session.professorId);
            states.ForceState(GameState.Management_Hub);
            for (int month = 1; month <= 3; month++)
            {
                service.StartRound();
                Assert.AreEqual(GameState.Round_Sales, states.CurrentState);
                Assert.IsNull(service.ProcessCurrentRound());
                CalendarTestFlow.Complete(_db);
                var result = service.ProcessCurrentRound();
                Assert.IsNotNull(result);
                Assert.AreEqual(GameState.Round_Summary, states.CurrentState);
                var cash = GameSessionState.Current.currentCash;
                Assert.IsFalse(service.ContinueAfterSummary("wrong-result"));
                Assert.AreEqual(GameState.Round_Summary, states.CurrentState);
                Assert.IsTrue(service.ContinueAfterSummary(result.roundResultId));
                Assert.AreEqual(month < 3 ? GameState.Management_Hub : GameState.FinalReport, states.CurrentState);
                Assert.IsFalse(service.ContinueAfterSummary(result.roundResultId));
                Assert.AreEqual(cash, GameSessionState.Current.currentCash);
            }
        }
        finally
        {
            typeof(GameManager).GetProperty("Instance").SetValue(null, null);
            Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void ThreeLossesRetainExistingBankruptcyRuleAndAProfitResetsTheCounter()
    {
        _context.Location.rent = 20000;
        var service = Service();
        Settle(service, 1); Settle(service, 2); Settle(service, 3);
        Assert.AreEqual(GameSessionStatus.BANKRUPT, _context.Session.status);
        Assert.AreEqual(3, _context.Session.consecutiveNegativeRounds);
        var session = _context.Session.Copy();
        session.status = GameSessionStatus.IN_PROGRESS; session.currentRound = 2;
        var profit = new RoundResultEntity { sessionId = session.sessionId, round = 2, netResult = 1f };
        Assert.AreEqual(0, MonthlySessionSettlement.Apply(session, profit, 123).consecutiveNegativeRounds);
    }

    [Test]
    public void ReloadRestoresTheSettledMonthAndReputationForTheNextCalculation()
    {
        var first = Settle(Service(), 1);
        GameSessionState.Clear();
        GameSessionState.LoadActiveSession("monthly_test");
        Assert.AreEqual(2, GameSessionState.Current.currentRound);
        Assert.AreEqual(first.closingCash, GameSessionState.Current.currentCash);
        Assert.AreEqual(first.reputationAtEnd, GameSessionState.Current.reputationScore);
        _context.Session = GameSessionState.Current;
        Assert.Greater(Calculate().potentialDemand, first.potentialDemand);
    }

    [Test]
    public void UncommittedSetupAndMissingMonthHistoryCannotBeSettled()
    {
        GameSessionState.Set(_context.Session);
        Assert.IsNull(Service().ProcessRound());
        Assert.AreEqual(0, _db.Table<RoundResultEntity>().Count());
        GameSessionState.Persist();
        _context.Session.currentRound = 2;
        GameSessionState.Save();
        CompleteCalendar();
        Assert.Throws<InvalidOperationException>(() => Service().ProcessRound());
        Assert.AreEqual(0, _db.Table<RoundResultEntity>().Count());
    }

    [Test]
    public void CalendarResumesMovementAndPaymentsWithoutExposingThePrivateStop()
    {
        var id = _context.Session.sessionId;
        _context.Settings.rentDueDay = _context.Settings.utilitiesDueDay = 20;
        var catalog = Resources.LoadAll<EventData>("Events");
        var freezer = catalog.Single(e => e.id == "freezer_breakdown");
        var festival = catalog.Single(e => e.id == "local_festival");
        SaveLegacyPlan("{\"entries\":[{\"eventId\":\"freezer_breakdown\",\"day\":16},{\"eventId\":\"local_festival\",\"day\":20}]}");
        var calendar = new MonthlyCalendarService(_db, contextFactory: TestContext);
        Assert.IsNull(calendar.Read(id, 1));
        var view = calendar.Open(id, 1, catalog);
        Assert.AreEqual(0, view.Day);
        Assert.AreEqual(20, view.DestinationDay);
        CollectionAssert.AreEqual(new[] { festival.id }, view.Events.Select(e => e.EventId));

        view = calendar.BeginAdvance(id, 1, view.Revision);
        int movementRevision = view.Revision;
        GameSessionState.Clear();
        GameSessionState.LoadActiveSession("monthly_test");
        calendar = new MonthlyCalendarService(_db);
        Assert.True(calendar.Read(id, 1).Moving);
        Assert.AreEqual(20, calendar.Read(id, 1).DestinationDay);
        Assert.AreEqual(0, calendar.Step(id, 1, movementRevision, 15).Day);
        Assert.Throws<InvalidOperationException>(() => new MonthlyEventDecisionService(_db).Confirm(id, 1, 16, freezer, freezer.options[0]));
        view = calendar.Step(id, 1, movementRevision, 16);
        Assert.AreEqual(16, view.Day);
        Assert.AreEqual(freezer.id, calendar.Read(id, 1).Pending.EventId);
        Assert.False(calendar.BeginAdvance(id, 1, view.Revision).Moving);
        GameSessionState.SetCash(999999);
        Assert.AreEqual(10000f, GameSessionState.Current.currentCash);
        var decisions = new MonthlyEventDecisionService(_db);
        var record = decisions.Confirm(id, 1, 16, freezer, freezer.options[0]);
        Assert.AreEqual(record.historyId, decisions.Confirm(id, 1, 16, freezer, freezer.options[0]).historyId);
        Assert.Throws<InvalidOperationException>(() => decisions.Confirm(id, 1, 16, freezer, freezer.options[1]));
        Assert.IsNull(calendar.Read(id, 1).Pending);

        view = calendar.BeginAdvance(id, 1, calendar.Read(id, 1).Revision);
        _db.Execute("CREATE TRIGGER reject_calendar_cash BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'Calendar failure'); END");
        Assert.Throws<SQLiteException>(() => calendar.Step(id, 1, view.Revision, 20));
        var afterFailure = calendar.Read(id, 1);
        Assert.AreEqual(16, afterFailure.Day);
        Assert.True(afterFailure.Moving);
        Assert.IsEmpty(afterFailure.Receipts);
        Assert.IsNull(afterFailure.Pending);
        Assert.AreEqual(10000f, GameSessionState.Current.currentCash);
        _db.Execute("DROP TRIGGER reject_calendar_cash");

        var arrival = calendar.Step(id, 1, view.Revision, 20);
        Assert.AreEqual(20, arrival.Day);
        Assert.AreEqual(festival.id, arrival.Pending.EventId);
        CollectionAssert.AreEqual(new[] { "rent", "utilities" }, arrival.Receipts.Select(r => r.type));
        CollectionAssert.AreEqual(new long[] { 950000, 940000 }, arrival.Receipts.Select(r => r.cashAfterCents));
        Assert.AreEqual(2, calendar.Step(id, 1, view.Revision, 20).Receipts.Count);
        GameSessionState.Clear();
        GameSessionState.LoadActiveSession("monthly_test");
        Assert.AreEqual(9400f, GameSessionState.Current.currentCash);
        Assert.AreEqual(festival.id, calendar.Read(id, 1).Pending.EventId);
        Assert.IsNull(Service().ProcessRound(1));
    }

    [Test]
    public void CalendarUsesFrozenEconomicsAndSettlesDayThirtyOnlyAfterItsLastDecision()
    {
        string id = _context.Session.sessionId;
        _context.Settings.salariesDueDay = 30;
        _context.Location.rent = 7000;
        var catalog = Resources.LoadAll<EventData>("Events");
        var freezer = catalog.Single(e => e.id == "freezer_breakdown");
        var festival = catalog.Single(e => e.id == "local_festival");
        SaveLegacyPlan("{\"entries\":[{\"eventId\":\"local_festival\",\"day\":30},{\"eventId\":\"freezer_breakdown\",\"day\":30}]}");
        var calendar = new MonthlyCalendarService(_db, contextFactory: TestContext);
        var view = calendar.Open(id, 1, catalog);
        _context.Location.rent = 123;
        _context.Roles[0].salary = 456;
        _context.Settings.salariesDueDay = 4;
        for (int i = 0; i < 3; i++)
        {
            view = calendar.BeginAdvance(id, 1, view.Revision);
            view = calendar.Step(id, 1, view.Revision, view.DestinationDay);
        }
        Assert.AreEqual(30, view.Day);
        Assert.AreEqual(-1100f, GameSessionState.Current.currentCash);
        Assert.AreEqual(freezer.id, view.Pending.EventId);
        var decisions = new MonthlyEventDecisionService(_db);
        _db.Execute("CREATE TRIGGER reject_reveal BEFORE UPDATE ON MonthlyEventPlanEntity BEGIN SELECT RAISE(ABORT, 'Choice failure'); END");
        Assert.Throws<SQLiteException>(() => decisions.Confirm(id, 1, 30, freezer, freezer.options[0]));
        Assert.IsEmpty(new SessionEventHistoryRepository(_db).GetBySessionAndRound(id, 1));
        Assert.AreEqual(freezer.id, calendar.Read(id, 1).Pending.EventId);
        _db.Execute("DROP TRIGGER reject_reveal");
        decisions.Confirm(id, 1, 30, freezer, freezer.options[0]);
        Assert.AreEqual(festival.id, calendar.Read(id, 1).Pending.EventId);
        Assert.IsNull(Service().ProcessRound(1));
        decisions.Confirm(id, 1, 30, festival, festival.options[0]);
        Assert.True(calendar.Read(id, 1).ReadyToClose);
        var row = _db.Find<MonthlyEventPlanEntity>(id + ":1");
        var incorrect = Newtonsoft.Json.Linq.JObject.Parse(row.calendarJson);
        ((Newtonsoft.Json.Linq.JObject)incorrect["ledger"][id + ":1:rent:1"])["type"] = "utilities";
        _db.Execute("UPDATE MonthlyEventPlanEntity SET calendarJson = ? WHERE planId = ?", incorrect.ToString(), row.planId);
        Assert.Throws<InvalidOperationException>(() => Service().ProcessRound(1));
        Assert.IsEmpty(_db.Table<RoundResultEntity>().ToList());
        _db.Update(row);
        var result = Service().ProcessRound(1);
        Assert.AreEqual(7000f, result.rent);
        Assert.AreEqual(4000f, result.salaries);
        Assert.AreEqual(10000f, result.openingCash);
        Assert.AreEqual(result.openingCash + result.netResult, result.closingCash, .005f);
        Assert.AreEqual(result.closingCash, GameSessionState.Current.currentCash);
        Assert.AreEqual(result.roundResultId, calendar.Read(id, 1).ResultId);
        Assert.AreEqual(result.roundResultId, Service().ProcessRound(1).roundResultId);
        Assert.Throws<InvalidOperationException>(() => calendar.Open(id, 2, catalog));
    }

    [Test]
    public void CalendarImportsLegacyChoicesAndDoesNotReplaceAnEmptyReservation()
    {
        var session = _context.Session;
        var data = Resources.LoadAll<EventData>("Events").Single(e => e.id == "freezer_breakdown");
        var before = new MonthlyEventDecisionService(_db).Confirm(session.sessionId, 1, 1, data, data.options[0]);
        const string empty = "{\"entries\":[]}";
        SaveLegacyPlan(empty);
        var calendar = new MonthlyCalendarService(_db, new MonthlyEventSelector(() => throw new Exception("Unexpected draw")), TestContext);
        var view = calendar.Open(session.sessionId, 1, Array.Empty<EventData>());
        Assert.AreEqual(data.id, view.Events.Single().EventId);
        Assert.IsTrue(view.Events.Single().Decided);
        Assert.AreEqual(5, view.DestinationDay);
        Assert.AreEqual(before.historyId, new SessionEventHistoryRepository(_db).GetBySessionAndRound(session.sessionId, 1).Single().historyId);
        Assert.AreEqual(empty, _db.Table<MonthlyEventPlanEntity>().Single().entriesJson);
        Assert.AreEqual(0, new MonthlyCalendarService(_db).Open(session.sessionId, 1, Array.Empty<EventData>()).Day);
    }

    [Test]
    public void CalendarRejectsInvalidLegacyPlanWithoutChangingIt()
    {
        const string invalid = "{\"entries\":[{\"eventId\":\"freezer_breakdown\",\"day\":31}]}";
        SaveLegacyPlan(invalid);
        var calendar = new MonthlyCalendarService(_db, contextFactory: TestContext);
        Assert.Throws<InvalidOperationException>(() => calendar.Open(_context.Session.sessionId, 1, Array.Empty<EventData>()));
        var saved = _db.Table<MonthlyEventPlanEntity>().Single();
        Assert.AreEqual(invalid, saved.entriesJson);
        Assert.IsNull(saved.calendarJson);
        Assert.Zero(_context.Session.calendarMonthLocked);
    }

    [Test]
    public void CalendarReceiptsUseShippedRentPayrollAndMicrocredit()
    {
        ConfigureShippedMenuAndTeam();
        var session = _context.Session;
        session.locationZone = LocationZone.Educacao;
        session.currentRound = 2;
        session.loanBalance = 50000;
        session.creditLineId = "microcredito_empresarial";
        session.currentCash = 12000;
        GameSessionState.Save();
        CalendarTestFlow.Complete(_db);

        var view = new MonthlyCalendarService(_db).Read(session.sessionId, 2);
        CollectionAssert.AreEqual(new[] { 5, 10, 25, 28 }, view.Receipts.Select(r => r.day));
        CollectionAssert.AreEqual(new long[] { 600000, 150000, 690000, 2600000 }, view.Receipts.Select(r => r.amountCents));
        var installment = view.Receipts.Last();
        Assert.AreEqual(2500000, installment.principalCents);
        Assert.AreEqual(100000, installment.interestCents);
        Assert.AreEqual(25000, session.loanBalance);
        Assert.AreEqual(-28400, session.currentCash);
    }

    [Test]
    public void CalendarSchemaUpgradeKeepsPriorReservations()
    {
        using var legacy = new SQLiteConnection(":memory:");
        legacy.Execute("CREATE TABLE MonthlyEventPlanEntity (planId TEXT PRIMARY KEY, sessionId TEXT, month INTEGER, entriesJson TEXT)");
        legacy.Execute("INSERT INTO MonthlyEventPlanEntity VALUES ('legacy:1', 'legacy', 1, '{\"entries\":[]}')");
        legacy.CreateTable<MonthlyEventPlanEntity>();
        var plan = legacy.Find<MonthlyEventPlanEntity>("legacy:1");
        Assert.AreEqual("{\"entries\":[]}", plan.entriesJson);
        Assert.IsNull(plan.calendarJson);
    }

    private void SaveLegacyPlan(string entries) => _db.Insert(new MonthlyEventPlanEntity
    {
        planId = _context.Session.sessionId + ":" + _context.Session.currentRound,
        sessionId = _context.Session.sessionId, month = _context.Session.currentRound, entriesJson = entries
    });

    [Test]
    public void ShippedCatalogAndSummaryUseTheSameMonthlyEngine()
    {
        var session = _context.Session;
        ConfigureShippedMenuAndTeam();
        var result = MonthlySimulationEngine.Calculate(MonthlySimulationCatalog.Load(session));
        var summary = EstablishmentSummaryService.Build();
        Assert.IsTrue(summary.HasMonthlyEstimate, summary.EstimateFailureReason);
        Assert.IsTrue(summary.HasEquipmentCatalog);
        Assert.IsTrue(summary.HasTeamCatalog);
        Assert.Greater(result.customers, 0);
        Assert.AreEqual(result.grossRevenue, summary.EstimatedRevenue);
        Assert.AreEqual(result.netResult, summary.MonthlyResult);
        Assert.AreEqual(result.FixedCosts, summary.FixedCost);
    }

    [Test]
    public void ExistingDatabaseSchemaAddsMonthlyFieldsWithoutRemovingRows()
    {
        using (var legacy = new SQLiteConnection(":memory:"))
        {
            legacy.Execute("CREATE TABLE RoundResultEntity (roundResultId TEXT PRIMARY KEY, sessionId TEXT, round INTEGER, grossRevenue REAL)");
            legacy.Execute("INSERT INTO RoundResultEntity VALUES ('legacy', 'old', 1, 123)");
            legacy.CreateTable<RoundResultEntity>();
            var result = legacy.Table<RoundResultEntity>().Single();
            Assert.AreEqual(123f, result.grossRevenue);
            Assert.AreEqual(0, result.potentialDemand);
            Assert.AreEqual(0f, result.loanPrincipalPayment);
        }
    }

    private RoundResultEntity Calculate() => MonthlySimulationEngine.Calculate(_context);
    private void ConfigureShippedMenuAndTeam()
    {
        var restaurant = Resources.LoadAll<RestaurantData>("Restaurants").Single(item => item.type == _context.Session.restaurantType);
        _context.Session.menuPricingJson = MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(restaurant.products));
        _context.Session.teamJson = "{\"members\":[{\"roleId\":\"attendant\",\"quantity\":2},{\"roleId\":\"grill_cook\",\"quantity\":1}]}";
    }
    private RoundService Service() => new RoundService(_db);

    private RoundResultEntity Settle(RoundService service, int month)
    {
        AcknowledgeTestSummary();
        CompleteCalendar();
        return service.ProcessRound(month);
    }

    private void AcknowledgeTestSummary()
    {
        GameSessionState.Current.pendingSummaryResultId = null;
        GameSessionState.Save();
    }

    private void CompleteCalendar() => CalendarTestFlow.Complete(_db, TestContext);

    private MonthlySimulationContext TestContext(GameSessionEntity session, IReadOnlyList<SessionEventHistoryEntity> events)
        => new MonthlySimulationContext
    {
        Session = session, Restaurant = _context.Restaurant, Location = _context.Location, Roles = _context.Roles,
        Equipment = _context.Equipment, Settings = _context.Settings, CreditLine = _context.CreditLine, Events = events
    };
    private T Asset<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>(); _assets.Add(asset); return asset;
    }
    private ProductData Product(string id, int price, float min, float max, float ratio)
    {
        var product = Asset<ProductData>();
        product.id = id; product.price = price; product.minPrice = min; product.maxPrice = max; product.inputCostRatio = ratio;
        return product;
    }
    private SessionEventHistoryEntity History(int day, float cash, int reputation) => new SessionEventHistoryEntity
    {
        historyId = Guid.NewGuid().ToString(), sessionId = _context.Session.sessionId, round = _context.Session.currentRound,
        day = day, cashChange = cash, reputationChange = reputation
    };
}


internal static class CalendarTestFlow
{
    internal static void Complete(SQLiteConnection db,
        Func<GameSessionEntity, IReadOnlyList<SessionEventHistoryEntity>, MonthlySimulationContext> contextFactory = null)
    {
        var session = GameSessionState.Current;
        var calendar = new MonthlyCalendarService(db, contextFactory: contextFactory);
        var view = calendar.Open(session.sessionId, session.currentRound, Array.Empty<EventData>());
        while (!view.ReadyToClose)
        {
            Assert.IsNull(view.Pending);
            view = calendar.BeginAdvance(session.sessionId, session.currentRound, view.Revision);
            Assert.IsTrue(view.Moving);
            view = calendar.Step(session.sessionId, session.currentRound, view.Revision, view.DestinationDay);
        }
    }
}
