using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Infrastructure.Session;
using NUnit.Framework;
using SQLite4Unity3d;
using UnityEngine;

public sealed class UI18DialogTests
{
    private string key, previousKey, path;
    private DatabaseService previousDatabase, database;
    private GameManager previousManager, manager;
    private GameObject dbRoot, managerRoot;
    private SQLiteConnection Db => database.Connection;
    [SetUp] public void SetUp()
    {
        previousKey=Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY"); key="Cogna.UI18.Tests."+Guid.NewGuid(); Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY",key);
        previousDatabase=DatabaseInitializer.DatabaseService; previousManager=GameManager.Instance;
        path=Path.GetFullPath("Temp/ui18-tests-"+Guid.NewGuid()+".db"); dbRoot=new GameObject("UI18 database"); database=dbRoot.AddComponent<DatabaseService>();
        var db=new SQLiteConnection(path); db.CreateTable<GameSessionEntity>(); db.CreateTable<RoundResultEntity>(); db.CreateTable<SessionEventHistoryEntity>(); db.CreateTable<MonthlyEventPlanEntity>();
        typeof(DatabaseService).GetProperty("Connection").SetValue(database,db); typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null,database);
        managerRoot=new GameObject("UI18 manager"); manager=managerRoot.AddComponent<GameManager>(); typeof(GameManager).GetProperty("Instance").SetValue(null,manager); typeof(GameManager).GetProperty("StateMachine").SetValue(manager,new GameStateMachine());
        GameSessionState.Clear(); PlayerSession.Clear();
    }
    [TearDown] public void TearDown()
    {
        database.Close(); GameSessionState.Clear(); PlayerSession.Clear(); UnityEngine.Object.DestroyImmediate(managerRoot); UnityEngine.Object.DestroyImmediate(dbRoot);
        typeof(GameManager).GetProperty("Instance").SetValue(null,previousManager); typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null,previousDatabase);
        PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY",previousKey); if(File.Exists(path)) File.Delete(path);
    }
    private GameSessionEntity Seed(bool persisted=true)
    {
        var s=new GameSessionService("ui18","qa").CreateNewSession(); s.menuPricingJson="{\"items\":[{\"productId\":\"TEST\",\"selectedPrice\":25}]}";
        if(persisted) Assert.IsTrue(GameSessionState.Persist()); return s;
    }
    private void FailWrites() => Db.Execute("CREATE TRIGGER ui18_fail BEFORE INSERT ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'isolated write failure'); END");
    [TestCase(false)][TestCase(true)] public void DraftExitNeverInsertsBeforeD3(bool save)
    {
        Seed(false); PlayerSession.SaveIdentification("Aluna Teste","123456","Empresa Teste"); Assert.IsTrue(manager.TryShutdown(save,out _)); Assert.IsTrue(manager.IsShuttingDown);
        using var check=new SQLiteConnection(path); Assert.AreEqual(0,check.Table<GameSessionEntity>().Count()); Assert.IsTrue(PlayerPrefs.HasKey(key));
    }
    [Test] public void SaveFailurePreservesRowMemoryDirtyFlagAndConnection()
    {
        var s=Seed(); float old=s.currentCash; GameSessionState.AddCash(129,false); FailWrites();
        Assert.IsFalse(manager.TryShutdown(true,out var error)); Assert.IsNotEmpty(error); Assert.IsFalse(manager.IsShuttingDown); Assert.IsNotNull(Db); Assert.AreSame(s,GameSessionState.Current);
        Assert.IsTrue(GameSessionState.HasUnsavedChanges); Assert.AreEqual(old,new GameSessionRepository(Db).GetById(s.sessionId).currentCash); Assert.AreEqual(old+129,s.currentCash);
    }
    [Test] public void RetryAcknowledgesWriteBeforeClosingAndRepeatedShutdownCannotSaveAgain()
    {
        var s=Seed(); GameSessionState.AddCash(129,false); FailWrites(); Assert.IsFalse(manager.TryShutdown(true,out _)); Db.Execute("DROP TRIGGER ui18_fail");
        Assert.IsTrue(manager.TryShutdown(true,out _)); Assert.IsNull(Db); Assert.IsFalse(GameSessionState.HasUnsavedChanges);
        GameSessionState.AddCash(999,false); Assert.IsTrue(manager.TryShutdown(true,out _));
        using var check=new SQLiteConnection(path); Assert.AreEqual(129,new GameSessionRepository(check).GetById(s.sessionId).currentCash);
    }
    [Test] public void DiscardAfterFailedSaveKeepsLastSavedRow()
    {
        var s=Seed(); GameSessionState.AddCash(129,false); FailWrites(); Assert.IsFalse(manager.TryShutdown(true,out _)); Assert.IsTrue(manager.TryShutdown(false,out _));
        using var check=new SQLiteConnection(path); Assert.AreEqual(0,new GameSessionRepository(check).GetById(s.sessionId).currentCash);
    }
    [Test] public void MissingConnectionCannotClaimSuccessfulSave()
    {
        Seed(); GameSessionState.AddCash(129,false); database.Close(); Assert.IsFalse(manager.TryShutdown(true,out var error)); Assert.IsNotEmpty(error); Assert.IsFalse(manager.IsShuttingDown); Assert.IsTrue(GameSessionState.HasUnsavedChanges);
    }
    [TestCase(GameSessionStatus.COMPLETED)][TestCase(GameSessionStatus.BANKRUPT)] public void FinishedCampaignSaveExitPreservesEndingMetadata(GameSessionStatus status)
    {
        var s=Seed(); s.status=status; s.finalSummaryAcknowledged=true; s.finalReportArchived=false; GameSessionState.AddCash(129,false); Assert.IsTrue(manager.TryShutdown(true,out _));
        using var check=new SQLiteConnection(path); var stored=new GameSessionRepository(check).GetById(s.sessionId); Assert.AreEqual(status,stored.status); Assert.IsTrue(stored.finalSummaryAcknowledged); Assert.IsFalse(stored.finalReportArchived); Assert.AreEqual(129,stored.currentCash);
    }
    [Test] public void IncompatibleSaveIsPreservedUntilExplicitResetAndExitDoesNotRewriteIt()
    {
        var s=Seed(); s.currentRound=4; GameSessionState.Save(); GameSessionState.LoadActiveSession("ui18"); Assert.IsTrue(GameSessionState.HasIncompatibleSave); Assert.IsTrue(manager.TryShutdown(true,out _));
        using var check=new SQLiteConnection(path); Assert.AreEqual(4,new GameSessionRepository(check).GetById(s.sessionId).currentRound);
    }
    [Test] public void ResetDeletesOnlyTheConfirmedCampaignAndIgnoresDuplicateCallback()
    {
        var s=Seed(); PlayerSession.SaveIdentification("Aluna Teste","123456","Empresa Teste");
        Db.Insert(new RoundResultEntity{roundResultId=Guid.NewGuid().ToString(),sessionId=s.sessionId,round=1}); var old=new GameSessionEntity{sessionId="other-report",userId="other",professorId="qa",cityId="",coherenceRating="",teamJson="{}",equipmentJson="{}",status=GameSessionStatus.COMPLETED}; Db.Insert(old);
        var root=new GameObject("UI18 reset"); try
        {
            var controller=root.AddComponent<EstablishmentSummaryController>(); controller.ResetGame(); var draft=GameSessionState.Current;
            controller.ResetGame(); Assert.AreSame(draft,GameSessionState.Current); Assert.AreEqual(InitialDecisionFlow.Identification,manager.StateMachine.CurrentState); Assert.IsFalse(GameSessionState.IsPersisted); Assert.IsFalse(PlayerSession.HasIdentification);
            Assert.IsNull(new GameSessionRepository(Db).GetById(s.sessionId)); Assert.IsNotNull(new GameSessionRepository(Db).GetById("other-report")); Assert.AreEqual(0,new RoundResultRepository(Db).GetBySessionId(s.sessionId).Count);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [Test] public void ProductionDialogsHaveModalFocusCanvasAndCurrentArtwork()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity"); bool opened=!scene.isLoaded;
        if(opened)scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
            var root=scene.GetRootGameObjects().Single(g=>g.name=="Canvas").transform;
            foreach(var path in new[]{"ExitUI/ExitDialog","Panel_Establishment/PanelReset","Panel_FinalReport/NewGameDialog","Panel_Bankruptcy/NewGameDialog","Panel_Equipment/CartModal"})
            {
                var modal=root.Find(path); Assert.IsNotNull(modal.GetComponent<SharedModalFocus>(),path); Assert.IsNotNull(modal.GetComponent<Canvas>(),path); Assert.IsNotNull(modal.GetComponent<UnityEngine.UI.GraphicRaycaster>(),path);
                foreach(var i in modal.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(i=>i.sprite!=null)) Assert.IsTrue(UnityEditor.AssetDatabase.GetAssetPath(i.sprite).StartsWith("Assets/Art/new/"),path+"/"+i.name);
                foreach(var text in modal.GetComponentsInChildren<TMPro.TMP_Text>(true)) { Assert.AreEqual("InterTight-Variable",text.font.name,path); Assert.IsFalse(text.enableAutoSizing); }
            }
        }
        finally { if(opened)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true); }
    }
}
