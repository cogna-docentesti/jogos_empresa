using System;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Infrastructure.Session;
using NUnit.Framework;
using SQLite4Unity3d;
using UnityEngine;

public sealed class UI15FinalReportTests
{
    private SQLiteConnection db;
    private DatabaseService previousDb;
    private GameManager previousManager, manager;
    private GameObject databaseRoot, managerRoot;
    private string previousKey;
    [SetUp] public void Setup()
    {
        previousKey = Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY"); Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY", "Cogna.UI15.NUnit");
        previousDb = DatabaseInitializer.DatabaseService; previousManager = GameManager.Instance;
        databaseRoot = new GameObject("UI15Database"); var service = databaseRoot.AddComponent<DatabaseService>();
        db = new SQLiteConnection(":memory:"); db.CreateTable<GameSessionEntity>(); db.CreateTable<RoundResultEntity>(); db.CreateTable<SessionEventHistoryEntity>(); db.CreateTable<MonthlyEventPlanEntity>();
        typeof(DatabaseService).GetProperty("Connection").SetValue(service, db); typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, service);
        managerRoot = new GameObject("UI15Manager"); manager = managerRoot.AddComponent<GameManager>(); typeof(GameManager).GetProperty("Instance").SetValue(null, manager); typeof(GameManager).GetProperty("StateMachine").SetValue(manager, new GameStateMachine());
        PlayerSession.Clear(); GameSessionState.Clear();
    }
    [TearDown] public void Teardown()
    {
        GameSessionState.Clear(); PlayerSession.Clear(); db.Close(); UnityEngine.Object.DestroyImmediate(managerRoot); UnityEngine.Object.DestroyImmediate(databaseRoot);
        typeof(GameManager).GetProperty("Instance").SetValue(null, previousManager); typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, previousDb);
        PlayerPrefs.DeleteKey("Cogna.UI15.NUnit"); PlayerPrefs.Save(); Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY", previousKey);
    }
    private GameSessionEntity Seed(string id = "completed", bool acknowledged = false, GameSessionStatus status = GameSessionStatus.COMPLETED, long finished = 100)
    {
        var s = new GameSessionEntity { sessionId = id, userId = "ui15-user", professorId = "qa", companyName = "Restaurante salvo", studentName = "Participante salvo", studentRA = "123456", status = status, currentRound = 3, cityId = "", coherenceRating = "HIGH", restaurantType = RestaurantType.PODRAO, locationZone = LocationZone.Comercio, targetSegment = Segment.LOW, teamJson = "{}", equipmentJson = "{}", menuPricingJson = "{}", completedAt = finished, finalSummaryAcknowledged = acknowledged };
        db.Insert(s); for (int i = 1; i <= 3; i++) db.Insert(new RoundResultEntity { roundResultId = id + i, sessionId = id, round = i, grossRevenue = 10000 * i, netResult = -50 * i, closingCash = 50000 - 100 * i, reputationAtEnd = 50 + i, alignmentScore = 60, alignmentClassification = "ADEQUATE" }); return s;
    }
    private void Prepare() => typeof(GameManager).GetMethod("PrepareSession", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { "ui15-user", "qa" });
    [TestCase(false, GameState.Round_Summary)] [TestCase(true, GameState.FinalReport)]
    public void CompletedCampaignReloadRespectsSummaryAcknowledgement(bool acknowledged, GameState expected)
    { Seed(acknowledged: acknowledged); Prepare(); Assert.AreEqual(expected, manager.StateMachine.CurrentState); Assert.IsTrue(GameSessionState.IsPersisted); Assert.AreEqual(3, db.Table<RoundResultEntity>().Count()); }
    [Test] public void ExplicitContinueAcknowledgesOnlyTheSavedFinalSummary()
    {
        Seed(); Prepare(); var service = new GameSessionService("ui15-user", "qa"); Assert.IsFalse(service.ContinueAfterSummary("foreign"));
        Assert.IsFalse(db.Find<GameSessionEntity>("completed").finalSummaryAcknowledged);
        Assert.IsTrue(service.ContinueAfterSummary("completed3")); Assert.IsTrue(db.Find<GameSessionEntity>("completed").finalSummaryAcknowledged);
        Assert.AreEqual(GameState.FinalReport, manager.StateMachine.CurrentState); Assert.IsFalse(service.ContinueAfterSummary("completed3"));
        GameSessionState.Clear(); Prepare(); Assert.AreEqual(GameState.FinalReport, manager.StateMachine.CurrentState); Assert.AreEqual(3, db.Table<RoundResultEntity>().Count());
    }
    [Test] public void FailureToAcknowledgeKeepsTheMonthlySummaryReadable()
    {
        Seed(); Prepare(); db.Execute("CREATE TRIGGER reject_ack BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'test'); END");
        Assert.Throws<SQLiteException>(() => new GameSessionService("ui15-user", "qa").ContinueAfterSummary("completed3"));
        Assert.AreEqual(GameState.Round_Summary, manager.StateMachine.CurrentState); Assert.IsFalse(GameSessionState.Current.finalSummaryAcknowledged);
    }
    [Test] public void LatestCompletionAndActiveSessionAreSelectedForTheirOwnUser()
    {
        Seed("older", true, finished: 10); Seed("latest", true, finished: 20); Seed("foreign", true, finished: 30); var foreign = db.Find<GameSessionEntity>("foreign"); foreign.userId = "other-user"; db.Update(foreign);
        Prepare(); Assert.AreEqual("latest", GameSessionState.Current.sessionId);
        GameSessionState.Clear(); Seed("active", status: GameSessionStatus.IN_PROGRESS); Prepare(); Assert.AreEqual("active", GameSessionState.Current.sessionId);
    }
    [Test] public void LaterBankruptcyNeverResurrectsAnOlderCompletion()
    { Seed("older", true); Seed("bankrupt", status: GameSessionStatus.BANKRUPT, finished: 200); Prepare(); Assert.AreEqual(GameState.Round_Summary, manager.StateMachine.CurrentState); Assert.AreEqual("bankrupt", GameSessionState.Current.sessionId); Assert.IsTrue(GameSessionState.IsPersisted); }
    [Test] public void BankruptcyStillHasPrecedenceOnContinue()
    {
        Seed(status: GameSessionStatus.BANKRUPT); GameSessionState.Set(db.Find<GameSessionEntity>("completed")); manager.StateMachine.ForceState(GameState.Round_Summary);
        Assert.IsTrue(new GameSessionService("ui15-user", "qa").ContinueAfterSummary("completed3")); Assert.AreEqual(GameState.GameOver_Bankruptcy, manager.StateMachine.CurrentState); Assert.IsTrue(db.Find<GameSessionEntity>("completed").finalSummaryAcknowledged);
    }
    [Test] public void NewGamePreservesAllResultsAndStartsAnUnpersistedIdentificationDraft()
    {
        Seed(true.ToString(), true); Prepare(); PlayerSession.SaveIdentification("Aluna Teste", "123456", "123456 Aluna Teste");
        var before = db.Table<RoundResultEntity>().ToList().Select(x => x.netResult).ToArray();
        var s = new GameSessionService("ui15-user", "qa").StartNewCampaignPreservingReport(); manager.BeginNewIdentification();
        Assert.AreEqual(GameState.Config_Identification, manager.StateMachine.CurrentState); Assert.AreNotEqual("True", s.sessionId); Assert.IsFalse(GameSessionState.IsPersisted); Assert.IsFalse(PlayerSession.HasIdentification);
        Assert.IsTrue(db.Find<GameSessionEntity>("True").finalReportArchived); Assert.AreEqual(1, db.Table<GameSessionEntity>().Count()); CollectionAssert.AreEqual(before, db.Table<RoundResultEntity>().ToList().Select(x => x.netResult));
        GameSessionState.Clear(); Prepare(); Assert.AreEqual(GameState.Config_Identification, manager.StateMachine.CurrentState); Assert.IsFalse(GameSessionState.IsPersisted);
    }
    [Test] public void NewGameStorageFailurePreservesCurrentReportAndIdentification()
    {
        Seed(acknowledged: true); Prepare(); PlayerSession.SaveIdentification("Aluna Teste", "123456", "123456 Aluna Teste");
        db.Execute("CREATE TRIGGER reject_archive BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'test'); END");
        Assert.Throws<SQLiteException>(() => new GameSessionService("ui15-user", "qa").StartNewCampaignPreservingReport()); Assert.AreEqual("completed", GameSessionState.Current.sessionId); Assert.IsTrue(PlayerSession.HasIdentification); Assert.IsFalse(db.Find<GameSessionEntity>("completed").finalReportArchived);
    }
    [Test] public void OldTwelveMonthCompletionStaysIncompatible()
    { var s = Seed(); s.currentRound = 12; db.Update(s); Prepare(); Assert.IsTrue(GameSessionState.HasIncompatibleSave); Assert.AreEqual(GameState.MainMenu, manager.StateMachine.CurrentState); Assert.AreEqual(3, db.Table<RoundResultEntity>().Count()); }
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void DocumentaryProjectionUsesOnlySavedMonthsAndExplicitAbsences(int months)
    {
        var s = Seed(); for (int i = 3; i > months; i--) db.Delete<RoundResultEntity>("completed" + i);
        var before = Newtonsoft.Json.JsonConvert.SerializeObject(s);
        var r = FinalReportPresentation.Build(s, new RoundResultRepository(db).GetBySessionId(s.sessionId), Array.Empty<SessionEventHistoryEntity>());
        Assert.AreEqual(9, r.Bmc.Length); Assert.AreEqual(4, r.Reflection.Length); Assert.AreEqual(months, r.Quarter.Count); Assert.AreEqual(before, Newtonsoft.Json.JsonConvert.SerializeObject(s));
        Assert.AreEqual("Restaurante salvo", r.Company); Assert.IsTrue(r.Bmc[0].Text.Contains("não registradas")); Assert.IsTrue(r.Bmc[5].Text.Contains("não registrados")); Assert.IsTrue(r.Reflection[2].Text.Contains("não estão disponíveis")); Assert.IsTrue(r.Reflection[3].Text.Contains("não estão disponíveis nesta versão"));
        if (months == 0) Assert.AreEqual("Não disponível", r.Net); else Assert.AreEqual(QuarterlyResultPresentation.SignedMoney(-50 * months * (months + 1) / 2), r.Net);
    }
    [Test] public void PersistedEventChoicesAreNeverInventedAsReflectionAnswers()
    {
        var s = Seed(); var e = new SessionEventHistoryEntity { sessionId = s.sessionId, round = 2, eventName = "Evento salvo", selectedChoiceTitle = "Escolha salva", selectedChoiceText = "Descrição salva" };
        var report = FinalReportPresentation.Build(s, new RoundResultRepository(db).GetBySessionId(s.sessionId), new[] { e });
        StringAssert.Contains("Escolha salva", report.Reflection[1].Text); StringAssert.DoesNotContain("Escolha salva", report.Reflection[2].Text);
        e.sessionId = "foreign"; Assert.Throws<InvalidOperationException>(() => FinalReportPresentation.Build(s, Array.Empty<RoundResultEntity>(), new[] { e }));
    }
    [Test] public void InvalidCompletedResultsAreRejectedAndUnrecordedIdentityIsExplicit()
    {
        var s = Seed(); s.companyName = s.studentName = s.studentRA = null; var rows = new RoundResultRepository(db).GetBySessionId(s.sessionId);
        var report = FinalReportPresentation.Build(s, rows, Array.Empty<SessionEventHistoryEntity>()); StringAssert.Contains("não registrado", report.Company); StringAssert.Contains("Não registrado", report.Identity);
        rows[0].netResult = float.NaN; Assert.Throws<InvalidOperationException>(() => FinalReportPresentation.Build(s, rows, Array.Empty<SessionEventHistoryEntity>()));
    }
    [Test] public void LegacySessionMigrationDefaultsResumeMetadataWithoutChangingCash()
    {
        using var legacy = new SQLiteConnection(":memory:");
        legacy.Execute("CREATE TABLE GameSessionEntity (sessionId TEXT PRIMARY KEY, userId TEXT, currentCash REAL)");
        legacy.Execute("INSERT INTO GameSessionEntity VALUES ('old', 'owner', 12345.67)"); legacy.CreateTable<GameSessionEntity>();
        var saved = legacy.Find<GameSessionEntity>("old"); Assert.IsFalse(saved.finalSummaryAcknowledged); Assert.IsFalse(saved.finalReportArchived); Assert.That(saved.currentCash, Is.EqualTo(12345.67f).Within(.001));
    }
    [Test] public void MissingChoiceRecordsAreDistinctFromEmptySelectionsAndCorruption()
    {
        var s = Seed(); var report = FinalReportPresentation.Build(s, Array.Empty<RoundResultEntity>(), Array.Empty<SessionEventHistoryEntity>());
        StringAssert.Contains("Equipe não registrada", report.Bmc[2].Text); StringAssert.Contains("Equipamentos não registrados", report.Bmc[2].Text);
        s.teamJson = "{broken"; Assert.Throws<Newtonsoft.Json.JsonReaderException>(() => FinalReportPresentation.Build(s, Array.Empty<RoundResultEntity>(), Array.Empty<SessionEventHistoryEntity>()));
    }
}
