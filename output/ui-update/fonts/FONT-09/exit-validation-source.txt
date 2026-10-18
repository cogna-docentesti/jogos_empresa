using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI.Navigation;
using Game.Infrastructure.Session;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Exercises the production quit callbacks. Every run actually stops Play Mode using isolated storage.</summary>
public static class Font09ProductionExitProbe
{
    public static void Run(string mode)
    {
        UI01Validation.RequireIsolatedStorage();
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Play Mode required.");
        var exit=UnityEngine.Object.FindFirstObjectByType<ExitDialogController>();
        var db=DatabaseInitializer.DatabaseService.Connection;var path=db.DatabasePath;
        var fields=BindingFlags.Instance|BindingFlags.NonPublic;
        UnityEngine.UI.Button Button(object owner,string n)=>(UnityEngine.UI.Button)owner.GetType().GetField(n,fields).GetValue(owner);
        void Click(UnityEngine.UI.Button b)
        {
            Canvas.ForceUpdateCanvases();var rt=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pointerId=8,position=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
            if(!b.IsActive()||!b.IsInteractable()||hits.Count==0||hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()!=b)throw new InvalidOperationException("Covered action "+b.name);
            ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);
        }
        GameSessionState.Clear();PlayerSession.Clear();PlayerSession.SaveIdentification("Participante Teste","123456","Empresa de teste");
        var s=new GameSessionService("ui18-exit-"+Guid.NewGuid(),"qa").CreateNewSession();s.menuPricingJson="{\"items\":[{\"productId\":\"TEST\",\"selectedPrice\":25}]}";
        bool draft=mode=="draft";if(!draft) {if(!GameSessionState.Persist())throw new InvalidOperationException("Fixture persistence failed");}
        db.Execute("CREATE TABLE IF NOT EXISTS UI18ExitAudit (id INTEGER PRIMARY KEY AUTOINCREMENT, sessionId TEXT)");
        db.Execute("CREATE TRIGGER IF NOT EXISTS ui18_exit_audit AFTER INSERT ON GameSessionEntity BEGIN INSERT INTO UI18ExitAudit(sessionId) VALUES (NEW.sessionId); END");
        var oldCash=s.currentCash;
        if(mode=="completed"||mode=="bankrupt") {s.status=mode=="completed"?GameSessionStatus.COMPLETED:GameSessionStatus.BANKRUPT;s.finalSummaryAcknowledged=true;GameSessionState.Save();}
        if(mode=="incompatible") {s.currentRound=4;GameSessionState.Save();typeof(GameManager).GetMethod("PrepareSession",fields).Invoke(GameManager.Instance,new object[]{s.userId,"qa"});}
        else GameManager.Instance.StateMachine.ForceState(draft?InitialDecisionFlow.Identification:GameState.Management_Hub);
        if(mode=="save"||mode=="discard"||mode=="failure-retry"||mode=="loading-discard")GameSessionState.AddCash(77,false);
        if(mode=="loading-discard")MenuNavigator.Instance.Open(PanelId.EquipmentStore);
        int prior=db.ExecuteScalar<int>("SELECT COUNT(*) FROM UI18ExitAudit WHERE sessionId = ?",s.sessionId);
        if(mode!="incompatible") Click(Button(exit,"openButton"));
        int openedFrame=Time.frameCount;
        EditorApplication.CallbackFunction complete=null;
        complete=()=>
        {
        if(Time.frameCount<openedFrame+3){EditorApplication.QueuePlayerLoopUpdate();return;}
        EditorApplication.update-=complete;
        try
        {
        bool failedSaveStayedOpen=false;
        bool pendingLoadAtQuit=false;
        if(mode=="incompatible")
        {
            var view=UnityEngine.Object.FindObjectsByType<EstablishmentSummaryView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single();
            if(!GameSessionState.HasIncompatibleSave||!view.IsResetOpen)throw new InvalidOperationException("Incompatible dialog missing");
            Click(Button(view,"cancelResetButton"));
        }
        else
        {
            if(mode=="failure-retry")
            {
                db.Execute("CREATE TRIGGER ui18_exit_fail BEFORE INSERT ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'isolated failure'); END");
                Click(Button(exit,"saveExitButton"));failedSaveStayedOpen=exit.IsOpen&&!GameManager.Instance.IsShuttingDown&&DatabaseInitializer.DatabaseService.Connection==db&&GameSessionState.HasUnsavedChanges;
                if(!failedSaveStayedOpen)throw new InvalidOperationException("Save failure quit/lost data");db.Execute("DROP TRIGGER ui18_exit_fail");
            }
            if(mode=="loading-discard")
            {
                var equipment=UnityEngine.Object.FindFirstObjectByType<EquipmentScreenController>();equipment.BeginCatalogLoad();
                pendingLoadAtQuit=equipment.GetComponent<SharedLoading>().IsBusy;
            }
            Click(Button(exit,mode=="save"||mode=="failure-retry"?"saveExitButton":"discardButton"));
        }
        bool closed=GameManager.Instance.IsShuttingDown&&DatabaseInitializer.DatabaseService.Connection==null;
        // The same callback and OnApplicationQuit must be harmless after the first successful shutdown.
        exit.Quit(true);GameManager.Instance.Shutdown(true);
        using var check=new SQLite4Unity3d.SQLiteConnection(path);var row=new GameSessionRepository(check).GetById(s.sessionId);
        int writes=check.ExecuteScalar<int>("SELECT COUNT(*) FROM UI18ExitAudit WHERE sessionId = ?",s.sessionId)-prior;
        bool saved=mode=="save"||mode=="failure-retry";
        bool passed=closed && (draft?row==null:row!=null&&row.currentCash==oldCash+(saved?77:0)) && writes==(saved?1:0) && PlayerPrefs.HasKey(Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY"))
            && (mode!="incompatible"||row.currentRound==4) && (mode!="completed"||row.status==GameSessionStatus.COMPLETED)
            && (mode!="bankrupt"||row.status==GameSessionStatus.BANKRUPT) && (mode!="loading-discard"||pendingLoadAtQuit);
        Directory.CreateDirectory("output/ui-update/fonts/FONT-09/exits");File.WriteAllText("output/ui-update/fonts/FONT-09/exits/exit-"+mode+".json",Newtonsoft.Json.JsonConvert.SerializeObject(new{mode,passed,closed,draft,saveRequested=saved,writes,failedSaveStayedOpen,pendingLoadAtQuit,sessionId=s.sessionId,database=path,storedCash=row?.currentCash,storedStatus=row?.status.ToString(),frame=Time.frameCount},Newtonsoft.Json.Formatting.Indented));
        if(!passed)throw new InvalidOperationException("Actual quit validation failed for "+mode);
        }
        catch(Exception e)
        {
            File.WriteAllText("output/ui-update/fonts/FONT-09/exits/exit-"+mode+".json",Newtonsoft.Json.JsonConvert.SerializeObject(new{mode,passed=false,error=e.ToString()},Newtonsoft.Json.Formatting.Indented));
            Debug.LogError("[UI-18 exit QA] "+e.Message);
        }
        };
        EditorApplication.update+=complete;EditorApplication.QueuePlayerLoopUpdate();
    }
}

/// <summary>Disposable Play-mode cycles through the existing real quit-button probe.</summary>
public static class Font09ExitValidation
{
    const string Output="output/ui-update/fonts/FONT-09/exits";
    const string Key="Cogna.Font09.Exits.";
    static readonly string[] Modes={"draft","saved","save","discard","failure-retry","loading-discard","incompatible","completed","bankrupt"};
    static double deadline;
    static int lastFrame;
    static string Snapshot()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        return Newtonsoft.Json.JsonConvert.SerializeObject(new{scene.isDirty,scene.rootCount,texts=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TMPro.TMP_Text>(true)).OrderBy(t=>t.GetInstanceID()).Select(t=>new{id=t.GetInstanceID(),json=JsonUtility.ToJson(t)}).ToArray()});
    }
    [InitializeOnLoadMethod] static void Reload()
    {
        EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        deadline=EditorApplication.timeSinceStartup+30;lastFrame=-1;
    }
    static void Start()
    {
        Directory.CreateDirectory(Output);
        SessionState.SetString(Key+"Before",Snapshot());
        SessionState.SetString(Key+"OldDb",Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH")??"");
        SessionState.SetString(Key+"OldKey",Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY")??"");
        SessionState.SetBool(Key+"Active",true);SessionState.SetInt(Key+"Index",0);
        typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("Clear",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        Next();
    }
    static void Next()
    {
        int index=SessionState.GetInt(Key+"Index",0);string mode=Modes[index];
        Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH",Path.GetFullPath(Output+"/qa-"+mode+"-"+Guid.NewGuid()+".db"));
        string identification=Key+mode+"."+Guid.NewGuid();SessionState.SetString(Key+"CurrentKey",identification);
        Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY",identification);
        SessionState.SetInt(Key+"Stage",0);SessionState.SetBool(Key+"Entered",false);
        EditorApplication.isPlaying=true;
    }
    static void Finish(string error=null)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode){SessionState.SetString(Key+"Error",error??"Unexpected Play transition");EditorApplication.isPlaying=false;return;}
        bool preserved=Snapshot()==SessionState.GetString(Key+"Before","");
        Environment.SetEnvironmentVariable("COGNA_TEST_DATABASE_PATH",SessionState.GetString(Key+"OldDb",""));
        Environment.SetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY",SessionState.GetString(Key+"OldKey",""));
        SessionState.SetBool(Key+"Active",false);
        object[] counts={0,0,0};typeof(Editor).Assembly.GetType("UnityEditor.LogEntries").GetMethod("GetCountsByType",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,counts);
        File.WriteAllText(Output+"/execution.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=error==null&&preserved&&(int)counts[0]==0,error,scenePreserved=preserved,modes=Modes,consoleErrors=counts[0],consoleWarnings=counts[1]},Newtonsoft.Json.Formatting.Indented));
    }
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        if(!SessionState.GetBool(Key+"Active",false))
        {
            const string request=Output+"/request.txt";
            if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(request)){File.Delete(request);Start();}
            return;
        }
        try
        {
            if(EditorApplication.isPlaying)
            {
                SessionState.SetBool(Key+"Entered",true);Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
                EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
                if(Time.frameCount<12||Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
                int stage=SessionState.GetInt(Key+"Stage",0);
                if(stage==2)
                {
                    var file=Output+"/exit-"+Modes[SessionState.GetInt(Key+"Index",0)]+".json";
                    if(File.Exists(file) && (bool?)Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file))["passed"]==false)throw new InvalidOperationException("Quit probe reported a failure: "+file);
                    if(EditorApplication.timeSinceStartup>deadline)throw new InvalidOperationException("Quit probe did not leave Play Mode");
                    return;
                }
                if(GameManager.Instance==null || DatabaseInitializer.DatabaseService?.Connection?.DatabasePath!=Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH"))
                {if(EditorApplication.timeSinceStartup>deadline)throw new InvalidOperationException("Isolated exit services did not initialize");return;}
                UI01Validation.RequireIsolatedStorage();
                if(stage==0)
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");SessionState.SetInt(Key+"Stage",1);SessionState.SetInt(Key+"ReadyFrame",Time.frameCount+3);return;
                }
                if(Time.frameCount<SessionState.GetInt(Key+"ReadyFrame",0))return;
                UiValidationContext.OutputRoot=Output;
                SessionState.SetInt(Key+"Stage",2);
                Font09ProductionExitProbe.Run(Modes[SessionState.GetInt(Key+"Index",0)]);
                return;
            }
            if(EditorApplication.isPlayingOrWillChangePlaymode || !SessionState.GetBool(Key+"Entered",false))return;
            var error=SessionState.GetString(Key+"Error","");if(!string.IsNullOrEmpty(error)){SessionState.SetString(Key+"Error","");Finish(error);return;}
            int index=SessionState.GetInt(Key+"Index",0);string path=Output+"/exit-"+Modes[index]+".json";
            var report=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            if((bool?)report["passed"]!=true)throw new InvalidOperationException("Quit probe failed: "+path);
            if(Snapshot()!=SessionState.GetString(Key+"Before",""))throw new InvalidOperationException("Quit changed the Edit-mode session");
            PlayerPrefs.DeleteKey(SessionState.GetString(Key+"CurrentKey",""));UiValidationContext.OutputRoot=null;
            if(++index==Modes.Length){Finish();return;}
            SessionState.SetInt(Key+"Index",index);Next();
        }
        catch(Exception e){Finish(e.ToString());}
    }
}
