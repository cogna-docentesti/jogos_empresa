using System;
using System.Linq;
using System.Reflection;
using Game.Infrastructure.Session;
using NUnit.Framework;
using SQLite4Unity3d;
using UnityEngine;

public sealed class UI19NavigationTests
{
    private SQLiteConnection db;
    private DatabaseService priorDb;
    private GameManager priorManager, manager;
    private GameObject databaseRoot, managerRoot;
    private string key, priorKey;
    private GameStateMachine States => manager.StateMachine;
    private GameSessionService Service => new("ui19-user", "qa");
    [SetUp] public void Setup()
    {
        priorKey = Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY");
        key = "Cogna.UI19.Tests." + Guid.NewGuid(); Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY", key);
        priorDb = DatabaseInitializer.DatabaseService; priorManager = GameManager.Instance;
        databaseRoot = new GameObject("UI19 database"); var database = databaseRoot.AddComponent<DatabaseService>();
        db = new SQLiteConnection(":memory:"); db.CreateTable<GameSessionEntity>(); db.CreateTable<RoundResultEntity>();
        db.CreateTable<SessionEventHistoryEntity>(); db.CreateTable<MonthlyEventPlanEntity>();
        typeof(DatabaseService).GetProperty("Connection").SetValue(database, db);
        typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, database);
        managerRoot = new GameObject("UI19 manager"); manager = managerRoot.AddComponent<GameManager>();
        typeof(GameManager).GetProperty("Instance").SetValue(null, manager);
        typeof(GameManager).GetProperty("StateMachine").SetValue(manager, new GameStateMachine());
        GameSessionState.Clear(); PlayerSession.Clear();
    }
    [TearDown] public void Teardown()
    {
        GameSessionState.Clear(); PlayerSession.Clear(); db.Close(); UnityEngine.Object.DestroyImmediate(managerRoot); UnityEngine.Object.DestroyImmediate(databaseRoot);
        typeof(GameManager).GetProperty("Instance").SetValue(null, priorManager);
        typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, priorDb);
        PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY", priorKey);
    }
    private GameSessionEntity Seed(bool staffed = false)
    {
        var s = Service.CreateNewSession(); var r = Resources.LoadAll<RestaurantData>("Restaurants").First(x => x.type == RestaurantType.PODRAO);
        s.menuPricingJson = MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(r.products)); s.currentCash = 41234.56f; s.reputationScore = 63;
        if (staffed)
        {
            var team = new TeamSelectionData();
            foreach (var requirement in r.requiredRoles)
                TeamSelectionHelper.AddMember(team, requirement.roleId, requirement.quantity);
            s.teamJson = TeamSelectionHelper.ToJson(team);
        }
        Assert.IsTrue(GameSessionState.Persist()); PlayerSession.SaveIdentification("Participante salvo", "123456", "Empresa salva"); return s;
    }
    private void Prepare() => typeof(GameManager).GetMethod("PrepareSession", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, new object[] { "ui19-user", "qa" });
    [TestCase(GameState.Initial_Capital)] [TestCase(GameState.Initial_Equipment)] [TestCase(GameState.Initial_Team)]
    public void ReloadResumesEachSavedOperationalStep(GameState step)
    {
        var s = Seed(); s.initialSetupResumeState = (int)step; db.Update(s); var before = Newtonsoft.Json.JsonConvert.SerializeObject(s);
        Prepare(); Assert.AreEqual(step, States.CurrentState); Assert.AreEqual(before, Newtonsoft.Json.JsonConvert.SerializeObject(db.Find<GameSessionEntity>(s.sessionId)));
    }
    [TestCase(GameState.Initial_Capital)] [TestCase(GameState.Initial_Equipment)] [TestCase(GameState.Initial_Team)]
    public void IdentificationCorrectionReturnsToThePendingOperationalStep(GameState step)
    {
        var s = Seed(); s.initialSetupResumeState = (int)step; db.Update(s); PlayerSession.Clear(); PlayerPrefs.DeleteKey(key);
        Prepare(); Assert.AreEqual(GameState.Config_Identification, States.CurrentState);
        PlayerSession.SaveIdentification("Participante corrigido", "123456", "Empresa salva"); Assert.IsTrue(manager.ConfirmIdentification()); Assert.AreEqual(step, States.CurrentState);
    }
    [Test] public void LegacyConfiguredSessionWithNullableMetadataStillResumesTheHub()
    { var s = Seed(); Assert.IsNull(s.initialSetupResumeState); Assert.IsNull(s.pendingSummaryResultId); Prepare(); Assert.AreEqual(GameState.Management_Hub, States.CurrentState); }
    [TestCase(GameState.Initial_Capital, GameState.Initial_Equipment)]
    [TestCase(GameState.Initial_Equipment, GameState.Initial_Team)]
    [TestCase(GameState.Initial_Team, GameState.Management_Hub)]
    public void SetupHandoffPersistsOnlyItsScreenAndRejectsADoubleCallback(GameState from, GameState to)
    {
        var s = Seed(staffed: to == GameState.Management_Hub); States.ForceState(from); var cash = s.currentCash;
        Assert.IsTrue(Service.ContinueInitialSetup(to)); Assert.IsFalse(Service.ContinueInitialSetup(to));
        Assert.AreEqual((int)to, db.Find<GameSessionEntity>(s.sessionId).initialSetupResumeState); Assert.AreEqual(cash, s.currentCash); Assert.AreEqual(1, db.Table<GameSessionEntity>().Count());
    }
    [Test] public void FailedSetupHandoffKeepsTheCurrentScreenAndMarker()
    {
        var s = Seed(); s.initialSetupResumeState = (int)GameState.Initial_Equipment; db.Update(s); States.ForceState(GameState.Initial_Equipment);
        db.Execute("CREATE TRIGGER reject_marker BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'isolated marker failure'); END");
        Assert.Throws<SQLiteException>(() => Service.ContinueInitialSetup(GameState.Initial_Team)); Assert.AreEqual(GameState.Initial_Equipment, States.CurrentState);
        Assert.AreEqual((int)GameState.Initial_Equipment, s.initialSetupResumeState);
    }
    [TestCase(1, false)] [TestCase(2, false)] [TestCase(1, true)] [TestCase(2, true)]
    public void ReloadWaitsAtUnreadSummaryAndContinueOnlyAcknowledgesItsSavedResult(int month, bool legacy)
    {
        var s = Seed(); s.currentRound = legacy ? month + 1 : month; s.pendingSummaryResultId = "result"; db.Update(s);
        db.Insert(new RoundResultEntity { roundResultId = "result", sessionId = s.sessionId, round = month, closingCash = s.currentCash });
        Prepare(); Assert.AreEqual(GameState.Round_Summary, States.CurrentState); Assert.IsFalse(Service.ContinueAfterSummary("stale"));
        Assert.IsTrue(Service.ContinueAfterSummary("result")); Assert.IsFalse(Service.ContinueAfterSummary("result"));
        Assert.AreEqual(1, db.Table<RoundResultEntity>().Count()); Assert.AreEqual(s.currentCash, GameSessionState.Current.currentCash);
        var stored = db.Find<GameSessionEntity>(s.sessionId);
        Assert.AreEqual(month + 1, stored.currentRound); Assert.IsNull(stored.pendingSummaryResultId);
        Prepare(); Assert.AreEqual(GameState.Management_Hub, States.CurrentState);
    }
    [Test] public void FailedSummaryAcknowledgementCannotEscapeOrRecalculate()
    {
        var s = Seed(); s.pendingSummaryResultId = "result"; db.Update(s);
        db.Insert(new RoundResultEntity { roundResultId = "result", sessionId = s.sessionId, round = 1 }); Prepare();
        db.Execute("CREATE TRIGGER reject_ack BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'isolated ack failure'); END");
        Assert.Throws<SQLiteException>(() => Service.ContinueAfterSummary("result")); Assert.AreEqual(GameState.Round_Summary, States.CurrentState); Assert.AreEqual("result", GameSessionState.Current.pendingSummaryResultId);
        Assert.AreEqual(1, GameSessionState.Current.currentRound);
        Assert.AreEqual(1, db.Find<GameSessionEntity>(s.sessionId).currentRound);
    }
    [Test] public void LegacyEventPlanReturnsToPreparationWithoutRedrawingOrSettling()
    {
        var s = Seed(staffed: true); string entries = "{\"entries\":[{\"eventId\":\"saved\",\"day\":6}]}";
        db.Insert(new MonthlyEventPlanEntity { planId = s.sessionId + ":1", sessionId = s.sessionId, month = 1, entriesJson = entries });
        Prepare(); Assert.AreEqual(GameState.Management_Hub, States.CurrentState); Assert.IsNull(new MonthlyCalendarService(db).Read(s.sessionId, 1));
        Assert.IsNull(Service.ProcessCurrentRound()); Assert.AreEqual(0, db.Table<RoundResultEntity>().Count()); Assert.AreEqual(entries, db.Table<MonthlyEventPlanEntity>().First().entriesJson);
    }
    [Test] public void PendingSummaryPreventsStartingTheNextMonth()
    { var s = Seed(staffed: true); s.pendingSummaryResultId = "unread"; States.ForceState(GameState.Management_Hub); Service.StartRound(); Assert.AreEqual(GameState.Management_Hub, States.CurrentState); Assert.AreEqual(0, db.Table<RoundResultEntity>().Count()); }
    [Test] public void IncompleteTeamBlocksSetupHandoffButAllowsReturningToEquipment()
    {
        var session = Seed();
        session.initialSetupResumeState = (int)GameState.Initial_Team;
        GameSessionState.Save();
        States.ForceState(GameState.Initial_Team);
        Assert.IsFalse(Service.ContinueInitialSetup(GameState.Management_Hub));
        Assert.IsFalse(InitialDecisionFlow.GoTo(GameState.Management_Hub));
        Assert.AreEqual((int)GameState.Initial_Team, db.Find<GameSessionEntity>(session.sessionId).initialSetupResumeState);
        Assert.IsTrue(Service.ContinueInitialSetup(GameState.Initial_Equipment));
        Prepare();
        Assert.AreEqual(GameState.Initial_Equipment, States.CurrentState);
    }
    [Test] public void TechnicalStatesAndSerializedEnumsRemainExplicitAndStable()
    {
        var technical = new[] { GameState.Bootstrap, GameState.MainMenu, GameState.Config_Review, GameState.Round_Start, GameState.Round_Sales, GameState.Round_Costs };
        foreach (GameState state in Enum.GetValues(typeof(GameState))) Assert.AreEqual(technical.Contains(state), UIStateListener.IsTechnicalState(state), state.ToString());
        Assert.AreEqual(17, (int)GameState.Config_Identification); Assert.AreEqual(13, (int)GameState.Round_Event); Assert.AreEqual(16, (int)GameState.GameOver_Bankruptcy);
    }
}
