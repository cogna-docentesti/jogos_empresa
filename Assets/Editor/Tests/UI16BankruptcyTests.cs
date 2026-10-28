using System;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Infrastructure.Session;
using NUnit.Framework;
using SQLite4Unity3d;
using UnityEngine;

public sealed class UI16FinalReportTests
{
    private SQLiteConnection db;
    private DatabaseService previousDb;
    private GameManager previousManager, manager;
    private GameObject databaseRoot, managerRoot;
    private string previousKey;
    [SetUp] public void Setup()
    {
        previousKey = Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY"); Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY", "Cogna.UI16.NUnit");
        previousDb = DatabaseInitializer.DatabaseService; previousManager = GameManager.Instance;
        databaseRoot = new GameObject("UI16Database"); var service = databaseRoot.AddComponent<DatabaseService>();
        db = new SQLiteConnection(":memory:"); db.CreateTable<GameSessionEntity>(); db.CreateTable<RoundResultEntity>(); db.CreateTable<SessionEventHistoryEntity>(); db.CreateTable<MonthlyEventPlanEntity>();
        typeof(DatabaseService).GetProperty("Connection").SetValue(service, db); typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, service);
        managerRoot = new GameObject("UI16Manager"); manager = managerRoot.AddComponent<GameManager>(); typeof(GameManager).GetProperty("Instance").SetValue(null, manager); typeof(GameManager).GetProperty("StateMachine").SetValue(manager, new GameStateMachine());
        PlayerSession.Clear(); GameSessionState.Clear();
    }
    [TearDown] public void Teardown()
    {
        GameSessionState.Clear(); PlayerSession.Clear(); db.Close(); UnityEngine.Object.DestroyImmediate(managerRoot); UnityEngine.Object.DestroyImmediate(databaseRoot);
        typeof(GameManager).GetProperty("Instance").SetValue(null, previousManager); typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, previousDb);
        PlayerPrefs.DeleteKey("Cogna.UI16.NUnit"); PlayerPrefs.Save(); Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY", previousKey);
    }
    private GameSessionEntity Seed(string id = "completed", bool acknowledged = false, GameSessionStatus status = GameSessionStatus.COMPLETED, long finished = 100)
    {
        var s = new GameSessionEntity { sessionId = id, userId = "ui16-user", professorId = "qa", companyName = "Restaurante salvo", studentName = "Participante salvo", studentRA = "123456", status = status, currentRound = 3, cityId = "", coherenceRating = "HIGH", restaurantType = RestaurantType.PODRAO, locationZone = LocationZone.Comercio, targetSegment = Segment.LOW, teamJson = "{}", equipmentJson = "{}", menuPricingJson = "{}", completedAt = finished, finalSummaryAcknowledged = acknowledged };
        db.Insert(s); for (int i = 1; i <= 3; i++) db.Insert(new RoundResultEntity { roundResultId = id + i, sessionId = id, round = i, grossRevenue = 10000 * i, netResult = -50 * i, closingCash = 50000 - 100 * i, reputationAtEnd = 50 + i, alignmentScore = 60, alignmentClassification = "ADEQUATE" }); return s;
    }
    private void Prepare() => typeof(GameManager).GetMethod("PrepareSession", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { "ui16-user", "qa" });
    private GameSessionEntity Bankrupt(bool ack = false)
    { var s = Seed(status: GameSessionStatus.BANKRUPT, acknowledged: ack); s.consecutiveNegativeRounds = 3; db.Update(s); return s; }
    [TestCase(false, GameState.Round_Summary)] [TestCase(true, GameState.GameOver_Bankruptcy)]
    public void ReloadRecoversBankruptcyBeforeAndAfterAcknowledgement(bool ack, GameState expected)
    { Bankrupt(ack); Prepare(); Assert.AreEqual(expected, manager.StateMachine.CurrentState); Assert.IsTrue(GameSessionState.IsPersisted); Assert.AreEqual(3, db.Table<RoundResultEntity>().Count()); }
    [Test] public void AcknowledgementIsIdempotentAndPreservesEveryEconomicField()
    {
        Bankrupt(); Prepare(); var before = GameSessionState.Current.Copy(); var service = new GameSessionService("ui16-user", "qa");
        Assert.IsFalse(service.ContinueAfterSummary("foreign")); Assert.IsFalse(GameSessionState.Current.finalSummaryAcknowledged);
        Assert.IsTrue(service.ContinueAfterSummary("completed3")); Assert.IsFalse(service.ContinueAfterSummary("completed3"));
        before.finalSummaryAcknowledged = true; Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(before), Newtonsoft.Json.JsonConvert.SerializeObject(db.Find<GameSessionEntity>(before.sessionId)));
        GameSessionState.Clear(); Prepare(); Assert.AreEqual(GameState.GameOver_Bankruptcy, manager.StateMachine.CurrentState);
    }
    [Test] public void FailedAcknowledgementKeepsSummaryAndStoredHistory()
    {
        Bankrupt(); Prepare(); db.Execute("CREATE TRIGGER reject_ack BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'test'); END");
        Assert.Throws<SQLiteException>(() => new GameSessionService("ui16-user", "qa").ContinueAfterSummary("completed3")); Assert.AreEqual(GameState.Round_Summary, manager.StateMachine.CurrentState); Assert.IsFalse(GameSessionState.Current.finalSummaryAcknowledged); Assert.AreEqual(3, db.Table<RoundResultEntity>().Count());
    }
    [Test] public void NewGamePreservesBankruptcyResultsEventsPlansAndD3Boundary()
    {
        var s = Bankrupt(true); Prepare(); db.Insert(new SessionEventHistoryEntity { historyId = "event", sessionId = s.sessionId, round = 1 });
        db.Insert(new MonthlyEventPlanEntity { planId = "plan", sessionId = s.sessionId, month = 1, entriesJson = "[]" });
        var results = Newtonsoft.Json.JsonConvert.SerializeObject(db.Table<RoundResultEntity>().ToList());
        var replacement = new GameSessionService("ui16-user", "qa").StartNewCampaignPreservingReport(); manager.BeginNewIdentification();
        Assert.IsFalse(GameSessionState.IsPersisted); Assert.AreEqual(GameState.Config_Identification, manager.StateMachine.CurrentState); Assert.AreNotEqual(s.sessionId, replacement.sessionId);
        Assert.AreEqual(1, db.Table<GameSessionEntity>().Count()); Assert.AreEqual(results, Newtonsoft.Json.JsonConvert.SerializeObject(db.Table<RoundResultEntity>().ToList())); Assert.AreEqual(1, db.Table<SessionEventHistoryEntity>().Count());
        Assert.AreEqual(1, db.Table<MonthlyEventPlanEntity>().Count()); Assert.IsTrue(db.Find<GameSessionEntity>(s.sessionId).finalReportArchived); GameSessionState.Clear(); Prepare(); Assert.IsFalse(GameSessionState.IsPersisted); Assert.AreEqual(GameState.Config_Identification, manager.StateMachine.CurrentState);
    }
    [Test] public void FailureToArchiveDoesNotLoseCurrentCampaign()
    {
        Bankrupt(true); Prepare(); db.Execute("CREATE TRIGGER reject_archive BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'test'); END");
        Assert.Throws<SQLiteException>(() => new GameSessionService("ui16-user", "qa").StartNewCampaignPreservingReport()); Assert.AreEqual("completed", GameSessionState.Current.sessionId); Assert.IsFalse(GameSessionState.Current.finalReportArchived);
    }
    [Test] public void ActiveCampaignHasPriorityOverFinishedBankruptcy()
    { Bankrupt(true); Seed("active", status: GameSessionStatus.IN_PROGRESS); Prepare(); Assert.AreEqual("active", GameSessionState.Current.sessionId); }
    [Test] public void ArchivedBankruptcyNeverResurrectsOlderConclusion()
    { Seed("older", true, finished: 10); var s = Bankrupt(true); s.finalReportArchived = true; db.Update(s); Prepare(); Assert.IsFalse(GameSessionState.IsPersisted); Assert.AreEqual(GameState.Config_Identification, manager.StateMachine.CurrentState); }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void ExplanationAndIndicatorsUseOnlySavedRows(int months)
    {
        var s = Bankrupt(); for (int i = 3; i > months; i--) db.Delete<RoundResultEntity>("completed" + i);
        var r = BankruptcyPresentation.Build(s, new RoundResultRepository(db).GetBySessionId(s.sessionId));
        Assert.AreEqual(months, r.Quarter.Count); Assert.AreEqual(months, r.LastMonth); StringAssert.Contains("três meses consecutivos", r.Reason);
        if (months == 0) Assert.AreEqual("Não disponível", r.Cash); else Assert.AreEqual(MonthlyResultPresentation.Money(db.Find<RoundResultEntity>("completed" + months).closingCash), r.Cash);
        Assert.AreEqual(months == 3, r.History.Contains("confirmam a sequência"));
    }
    [TestCase(0f)] [TestCase(1f)]
    public void NonnegativeResultDoesNotFalselyConfirmThreeLosses(float value)
    { var s = Bankrupt(); var rows = new RoundResultRepository(db).GetBySessionId(s.sessionId); rows[1].netResult = value; StringAssert.Contains("não permitem conferir", BankruptcyPresentation.Build(s, rows).Reason); }
    [Test] public void CorruptForeignOrNonfiniteResultsAreRejected()
    {
        var s = Bankrupt(); var rows = new RoundResultRepository(db).GetBySessionId(s.sessionId); rows[0].netResult = float.NaN;
        Assert.Throws<InvalidOperationException>(() => BankruptcyPresentation.Build(s, rows)); rows[0].netResult = 0; rows[0].sessionId = "other"; Assert.Throws<InvalidOperationException>(() => BankruptcyPresentation.Build(s, rows));
    }
    [Test] public void NegativeCashAloneNeverChangesTheExistingEndingRule()
    {
        var s = new GameSessionEntity { sessionId = "rules", currentRound = 1, status = GameSessionStatus.IN_PROGRESS, currentCash = -100000, consecutiveNegativeRounds = 1 };
        for (int month = 1; month <= 3; month++)
        {
            var result = new RoundResultEntity { sessionId = s.sessionId, round = month, netResult = month == 2 ? 0 : -1, closingCash = -100000 - month };
            s = MonthlySessionSettlement.Apply(s, result, 1); Assert.AreNotEqual(GameSessionStatus.BANKRUPT, s.status);
        }
        Assert.AreEqual(GameSessionStatus.COMPLETED, s.status); Assert.AreEqual(1, s.consecutiveNegativeRounds);
    }
}
