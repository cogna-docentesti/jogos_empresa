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
        Service().ProcessRound(1);
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
        var first = service.ProcessRound(1);
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
        _db.Execute("CREATE TRIGGER reject_month BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'Injected settlement failure'); END");
        Assert.Throws<SQLiteException>(() => Service().ProcessRound(1));
        Assert.AreEqual(0, _db.Table<RoundResultEntity>().Count());
        Assert.AreEqual(1, _context.Session.currentRound);
        Assert.AreEqual(10000f, _context.Session.currentCash);
        Assert.AreEqual(50, _context.Session.reputationScore);
        Assert.AreEqual(10000f, _db.Table<GameSessionEntity>().Single().currentCash);
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
        var first = service.ProcessRound(1);
        Assert.AreEqual(353.33f, first.loanPayment);
        service.ProcessRound(2);
        var last = service.ProcessRound(3);
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
        service.ProcessRound(1);
        var line = Asset<CreditLineData>();
        line.id = "late_credit"; line.monthlyInterestRate = 0.02f;
        _context.CreditLine = line;
        _context.Session.loanBalance = 900f;
        _context.Session.creditLineId = line.id;
        GameSessionState.Save();
        Assert.AreEqual(468f, service.ProcessRound(2).loanPayment);
        Assert.AreEqual(459f, service.ProcessRound(3).loanPayment);
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
                Assert.IsNotNull(service.ProcessCurrentRound());
                Assert.AreEqual(month < 3 ? GameState.Management_Hub : GameState.FinalReport, states.CurrentState);
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
        service.ProcessRound(1); service.ProcessRound(2); service.ProcessRound(3);
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
        var first = Service().ProcessRound(1);
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
        Assert.Throws<InvalidOperationException>(() => Service().ProcessRound());
        Assert.AreEqual(0, _db.Table<RoundResultEntity>().Count());
    }

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
    private RoundService Service() => new RoundService(_db, (session, events) => new MonthlySimulationContext
    {
        Session = session, Restaurant = _context.Restaurant, Location = _context.Location, Roles = _context.Roles,
        Equipment = _context.Equipment, Settings = _context.Settings, CreditLine = _context.CreditLine, Events = events
    });
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
