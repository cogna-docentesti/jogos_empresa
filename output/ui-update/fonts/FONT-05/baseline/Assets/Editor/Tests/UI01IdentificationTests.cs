using System;
using System.IO;
using System.Reflection;
using Game.Infrastructure.Session;
using NUnit.Framework;
using SQLite4Unity3d;
using UnityEngine;

public sealed class UI01IdentificationTests
{
    private const string TestKey = "UI01.NUnit.Identification";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private string previousKey;
    private DatabaseService previousDatabase;
    private GameManager previousManager;
    private GameObject databaseRoot, managerRoot;
    private SQLiteConnection db;
    private GameManager manager;

    [SetUp]
    public void SetUp()
    {
        previousKey = Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY");
        Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY", TestKey);
        PlayerPrefs.DeleteKey(TestKey);
        previousDatabase = DatabaseInitializer.DatabaseService;
        previousManager = GameManager.Instance;
        databaseRoot = new GameObject("UI01TestDatabase");
        var service = databaseRoot.AddComponent<DatabaseService>();
        db = new SQLiteConnection(":memory:");
        db.CreateTable<GameSessionEntity>(); db.CreateTable<RoundResultEntity>(); db.CreateTable<SessionEventHistoryEntity>();
        typeof(DatabaseService).GetProperty("Connection").SetValue(service, db);
        typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, service);
        managerRoot = new GameObject("UI01TestManager");
        manager = managerRoot.AddComponent<GameManager>();
        typeof(GameManager).GetProperty("Instance").SetValue(null, manager);
        typeof(GameManager).GetProperty("StateMachine").SetValue(manager, new GameStateMachine());
        PlayerSession.Clear(); GameSessionState.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        GameSessionState.Clear(); PlayerSession.Clear();
        db?.Close();
        UnityEngine.Object.DestroyImmediate(managerRoot); UnityEngine.Object.DestroyImmediate(databaseRoot);
        typeof(GameManager).GetProperty("Instance").SetValue(null, previousManager);
        typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, previousDatabase);
        PlayerPrefs.DeleteKey(TestKey); PlayerPrefs.Save();
        Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY", previousKey);
    }

    [Test]
    public void SerializedEnumValuesAndSevenStepsStayStable()
    {
        Assert.AreEqual(0, (int)GameState.Bootstrap);
        Assert.AreEqual(16, (int)GameState.GameOver_Bankruptcy);
        Assert.AreEqual(17, (int)GameState.Config_Identification);
        var legacy = new[] {GameState.Bootstrap,GameState.MainMenu,GameState.Config_Location,GameState.Config_Restaurant,
            GameState.Config_TargetSegment,GameState.Config_Review,GameState.Initial_Equipment,GameState.Initial_Team,
            GameState.Initial_Capital,GameState.Management_Hub,GameState.Round_Start,GameState.Round_Sales,
            GameState.Round_Costs,GameState.Round_Event,GameState.Round_Summary,GameState.FinalReport,GameState.GameOver_Bankruptcy};
        for (int i=0;i<legacy.Length;i++) Assert.AreEqual(i,(int)legacy[i]);
        Assert.AreEqual(7, InitialDecisionFlow.StepCount);
        Assert.AreEqual(1, InitialDecisionFlow.StepFor(InitialDecisionFlow.Identification));
        Assert.AreEqual(2, InitialDecisionFlow.StepFor(InitialDecisionFlow.Location));
        Assert.AreEqual(7, InitialDecisionFlow.StepFor(GameState.Initial_Team));
    }

    [Test]
    public void NewGameWithPrefilledRegistrationWaitsForConfirmation()
    {
        SaveIdentity(); Prepare();
        Assert.AreEqual(InitialDecisionFlow.Identification, manager.StateMachine.CurrentState);
        Assert.IsFalse(GameSessionState.IsPersisted);
        Assert.AreEqual(0, db.Table<GameSessionEntity>().Count());
        Assert.IsTrue(manager.ConfirmIdentification());
        Assert.AreEqual(InitialDecisionFlow.Location, manager.StateMachine.CurrentState);
    }

    [Test]
    public void BackAndIdentificationConfirmationPreserveEntireDraft()
    {
        SaveIdentity(); Prepare(); manager.ConfirmIdentification();
        PlayerSession.SaveLocation("bank","Área Financeira",LocationZone.Financas);
        PlayerSession.SaveRestaurant(RestaurantType.JAPONES,Segment.MEDIUM);
        var menu = new MenuPricingData(); menu.items.Add(new MenuPricingItem {productId="TEST",selectedPrice=55});
        PlayerSession.SetMenu(RestaurantType.JAPONES,menu);
        Assert.IsTrue(manager.StateMachine.TryChangeState(InitialDecisionFlow.Identification));
        Assert.IsTrue(manager.ConfirmIdentification());
        Assert.IsTrue(PlayerSession.IsComplete);
        Assert.AreEqual(55,PlayerSession.GetMenu().items[0].selectedPrice);
        Assert.AreEqual(0,db.Table<GameSessionEntity>().Count());
    }

    [Test]
    public void CompleteSaveResumesHubAndMissingIdentityRequiresRepair()
    {
        SaveIdentity(); SeedSave(); Prepare();
        Assert.AreEqual(GameState.Management_Hub,manager.StateMachine.CurrentState);
        PlayerSession.ClearSavedIdentification(); Prepare();
        Assert.AreEqual(InitialDecisionFlow.Identification,manager.StateMachine.CurrentState);
        Assert.IsFalse(manager.ConfirmIdentification());
        SaveIdentity(); Assert.IsTrue(manager.ConfirmIdentification());
        Assert.AreEqual(GameState.Management_Hub,manager.StateMachine.CurrentState);
        Assert.AreEqual(1,db.Table<GameSessionEntity>().Count());
    }

    [TestCase("abcd", "12345", false)]
    [TestCase("abcde", "1234", false)]
    [TestCase("abcde", "12345", true)]
    [TestCase("abcde", "12345678901", false)]
    public void EntryChecksOriginalFormLimits(string name,string ra,bool valid)
    {
        PlayerSession.SaveIdentification(name,ra,ra+" "+name);
        Assert.AreEqual(valid,PlayerSession.HasValidIdentification);
        SeedSave(); Prepare();
        Assert.AreEqual(valid ? GameState.Management_Hub : InitialDecisionFlow.Identification,manager.StateMachine.CurrentState);
    }

    [Test]
    public void CorruptPrefsCannotReuseStaleRuntimeIdentity()
    {
        SaveIdentity(); PlayerPrefs.SetString(TestKey,"{broken");
        Assert.IsFalse(PlayerSession.TryLoadIdentification());
        Assert.IsFalse(PlayerSession.HasIdentification);
    }

    [Test]
    public void IncompatibleSaveWaitsForExplicitResetThenReturnsToStepOne()
    {
        SaveIdentity(); SeedSave(4); Prepare();
        Assert.IsTrue(GameSessionState.HasIncompatibleSave);
        Assert.AreEqual(GameState.MainMenu,manager.StateMachine.CurrentState);
        Assert.AreEqual(1,db.Table<GameSessionEntity>().Count());
        var root=new GameObject("UI01ResetController");
        try { root.AddComponent<Game.Adapter.In.Controllers.EstablishmentSummaryController>().ResetGame(); }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Assert.IsFalse(GameSessionState.HasIncompatibleSave);
        Assert.AreEqual(InitialDecisionFlow.Identification,manager.StateMachine.CurrentState);
        Assert.IsFalse(PlayerSession.HasIdentification);
        Assert.AreEqual(0,db.Table<GameSessionEntity>().Count());
    }

    private void Prepare() => typeof(GameManager).GetMethod("PrepareSession",Flags).Invoke(manager,new object[]{"ui01","qa"});
    private static void SaveIdentity() => PlayerSession.SaveIdentification("Aluna Teste","123456","123456 Aluna Teste");
    private void SeedSave(int round=1)
    {
        db.InsertOrReplace(new GameSessionEntity {sessionId="ui01-save",userId="ui01",professorId="qa",status=GameSessionStatus.IN_PROGRESS,
            currentRound=round,cityId="",coherenceRating="",teamJson="{}",equipmentJson="{}",menuPricingJson="{\"items\":[{\"productId\":\"TEST\",\"selectedPrice\":55}]}"});
    }
}
