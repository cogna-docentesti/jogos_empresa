using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Infrastructure.Session;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class UI18DialogValidation
{
    [Serializable] private class Check { public string name,detail; public bool passed; public int frame; }
    [Serializable] private class Report { public bool completed; public int failures; public List<Check> checks=new(); }
    private static readonly Queue<(string,Action)> steps=new();
    private static readonly List<object> captures=new();
    private static Report report;
    private static int lastFrame, wait;
    private static double next;
    private static string res, id, snapshot, prefs;
    private static GameSessionEntity beforeReset;
    private static GameObject focus;
    private static Keyboard testKeyboard;
    private static Transform Root => GameObject.Find("Canvas").transform;
    private static ExitDialogController Exit => Root.Find("ExitUI").GetComponent<ExitDialogController>();
    private static EstablishmentSummaryView Reset => Root.Find("Panel_Establishment").GetComponent<EstablishmentSummaryView>();
    private static EquipmentScreenView Equipment => Root.Find("Panel_Equipment").GetComponent<EquipmentScreenView>();
    private static EquipmentScreenController EquipmentController => Equipment.GetComponent<EquipmentScreenController>();
    private static SQLite4Unity3d.SQLiteConnection Db => DatabaseInitializer.DatabaseService.Connection;
    private static T Field<T>(object target,string n) => (T)target.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    private static UnityEngine.UI.Button B(object target,string n) => Field<UnityEngine.UI.Button>(target,n);
    private static void Expect(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
    private static void Add(string name,Action action) => steps.Enqueue((name,action));
    private static Rect Bounds(RectTransform t) {var c=new Vector3[4];t.GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
    private static PointerEventData Pointer(UnityEngine.UI.Button b,int id=-1) => new(EventSystem.current){pointerId=id,button=PointerEventData.InputButton.Left,position=Bounds((RectTransform)b.transform).center};
    private static void Click(UnityEngine.UI.Button b,bool touch=false)
    {
        Canvas.ForceUpdateCanvases(); Expect(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name); var p=Pointer(b,touch?5:-1); var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(p,hits);
        Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b,"Covered "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"nothing"));
        ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    private static void Submit(UnityEngine.UI.Button b) {b.Select();ExecuteEvents.Execute(b.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);}
    private static void Press(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys));
    private static void SaveSnapshot() {snapshot=Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current);prefs=PlayerPrefs.GetString(Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY"));}
    private static void Unchanged()
    {Expect(snapshot==Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current),"Cancel changed memory");Expect(prefs==PlayerPrefs.GetString(Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY")),"Cancel changed identification");}
    private static void Draft()
    {
        Exit.Close(); Reset.HideResetWarning(); MenuNavigator.Instance.CloseAll(); GameSessionState.Clear(); PlayerSession.Clear();
        var s=new GameSessionService("ui18-"+Guid.NewGuid(),"qa").CreateNewSession();id=s.sessionId;
        PlayerSession.SaveIdentification("Participante de validação","123456","Empresa da validação UI");
        PlayerSession.SaveLocation("commerce","Área Comercial",LocationZone.Comercio); PlayerSession.SaveRestaurant(RestaurantType.PODRAO,Segment.LOW);
        GameManager.Instance.BeginNewIdentification(); EventSystem.current.SetSelectedGameObject(null);
    }
    private static void Active()
    {
        Draft(); var s=GameSessionState.Current;var r=Resources.LoadAll<RestaurantData>("Restaurants").Single(x=>x.type==RestaurantType.PODRAO);
        s.companyName=PlayerSession.RestaurantName;s.studentName=PlayerSession.StudentName;s.studentRA=PlayerSession.StudentRA;s.restaurantType=r.type;s.targetSegment=Segment.LOW;s.locationZone=LocationZone.Comercio;s.initialCapital=s.currentCash=65000;s.reputationScore=50;
        s.menuPricingJson=MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(r.products));s.teamJson="{}";s.equipmentJson="{}";Expect(GameSessionState.Persist(),"Persist fixture");
        GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub);EventSystem.current.SetSelectedGameObject(null);
    }
    private static void Prepare() => typeof(GameManager).GetMethod("PrepareSession",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(GameManager.Instance,new object[]{GameSessionState.Current.userId,"qa"});
    private static void Complete(bool bankrupt)
    {
        var type=bankrupt?typeof(UI16BankruptcyValidation):typeof(UI15FinalReportValidation);
        type.GetMethod("Complete",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{false});id=GameSessionState.Current.sessionId;
        GameSessionState.Current.finalSummaryAcknowledged=true;GameSessionState.Save();GameManager.Instance.StateMachine.ForceState(bankrupt?GameState.GameOver_Bankruptcy:GameState.FinalReport);
    }
    private static void Fits()
    {
        Canvas.ForceUpdateCanvases();Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"One EventSystem");
        var modal=SharedModalFocus.Active;if(modal==null)return;
        foreach(var t in modal.GetComponentsInChildren<TMP_Text>()) {t.ForceMeshUpdate();Expect(t.font.name=="InterTight-Variable"&&!t.enableAutoSizing,"Typography "+t.name);Expect(!t.isTextOverflowing,"Text overflow "+t.name+": "+t.text);}
        foreach(var b in modal.GetComponentsInChildren<UnityEngine.UI.Button>())
        {var r=Bounds((RectTransform)b.transform);Expect(r.xMin>=0&&r.yMin>=0&&r.xMax<=Screen.width+.5f&&r.yMax<=Screen.height+.5f,"Offscreen "+b.name);Expect(r.height>=44-.1f,"Touch target "+b.name);}
        Expect(modal.GetComponent<Canvas>().overrideSorting&&modal.GetComponent<Canvas>().sortingOrder>=1000,"Modal layer ownership");
        var selected=EventSystem.current.currentSelectedGameObject;Expect(selected!=null&&selected.transform.IsChildOf(modal.transform),"Focus escaped modal");
        var point=Pointer(Root.Find("ExitUI/ExitButton").GetComponent<UnityEngine.UI.Button>());var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(point,hits);
        Expect(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(modal.transform),"Background raycast leaked");
        foreach(var b in modal.GetComponentsInChildren<UnityEngine.UI.Button>().Where(x=>x.IsInteractable()))
        {hits.Clear();EventSystem.current.RaycastAll(Pointer(b),hits);Expect(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b,"Modal action covered "+b.name);}
    }
    private static void Capture(string name)
    {
        Fits();string p=UI01Validation.Capture("../UI-18/"+name+"-"+res);using var h=System.Security.Cryptography.SHA256.Create();captures.Add(new{name,path=p,width=Screen.width,height=Screen.height,frame=Time.frameCount,sha256=BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant()});
        File.WriteAllText("output/ui-update/UI-18/capture-frames.json",Newtonsoft.Json.JsonConvert.SerializeObject(captures,Newtonsoft.Json.Formatting.Indented));
    }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage();Expect(EditorApplication.isPlaying,"Use Play Mode");Directory.CreateDirectory("output/ui-update/UI-18");steps.Clear();captures.Clear();report=new();lastFrame=-1;
        testKeyboard=InputSystem.AddDevice<Keyboard>("UI18ValidationKeyboard");testKeyboard.MakeCurrent();
        Db.Execute("DROP TRIGGER IF EXISTS ui18_fail");Db.Execute("DROP TRIGGER IF EXISTS ui18_reset_fail");
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)})
        {
            var s=size;
            Add("Viewport "+s,()=>{res=s.x+"x"+s.y;UI01Validation.SetGameView(s.x,s.y);Draft();});
            Add("Draft exit",()=>{SaveSnapshot();Click(B(Exit,"openButton"),true);});
            Add("Draft modal focus and no save option",()=>{Expect(Exit.CurrentMode==ExitDialogController.ExitMode.BeforeFirstSave&&!B(Exit,"saveExitButton").gameObject.activeSelf,"Draft actions");Expect(EventSystem.current.currentSelectedGameObject==B(Exit,"cancelButton").gameObject,"Safe initial focus");Capture("exit-draft");Press(Key.Tab);});
            Add("Draft Tab skips hidden save",()=>{Press();Expect(EventSystem.current.currentSelectedGameObject==B(Exit,"discardButton").gameObject,"Draft traversal");Press(Key.LeftShift,Key.Tab);});
            Add("Reverse Tab returns cancel",()=>{Press();Expect(EventSystem.current.currentSelectedGameObject==B(Exit,"cancelButton").gameObject,"Reverse Tab");Press(Key.Escape);});
            Add("Escape restores draft",()=>{Press();Expect(!Exit.IsOpen&&SharedModalFocus.Active==null,"Escape reopened Exit");Expect(EventSystem.current.currentSelectedGameObject==B(Exit,"openButton").gameObject,"Restore opener");Unchanged();Expect(new GameSessionRepository(Db).GetById(id)==null,"No INSERT before D3");Active();GameSessionState.AddCash(123,false);SaveSnapshot();Click(B(Exit,"openButton"));});
            Add("Unsaved modal",()=>{Expect(B(Exit,"saveExitButton").gameObject.activeSelf,"Save available");Expect(EventSystem.current.currentSelectedGameObject==B(Exit,"cancelButton").gameObject,"Unsaved safe initial focus");Capture("exit-unsaved");Press(Key.Tab);});
            Add("Tab discard",()=>{Press();Expect(EventSystem.current.currentSelectedGameObject==B(Exit,"discardButton").gameObject,"Discard traversal");Press(Key.Tab);});
            Add("Tab save",()=>{Press();Expect(EventSystem.current.currentSelectedGameObject==B(Exit,"saveExitButton").gameObject,"Save traversal");Capture("exit-keyboard-save");Press(Key.Tab);});
            Add("Tab wraps inside modal",()=>{Press();Expect(EventSystem.current.currentSelectedGameObject==B(Exit,"cancelButton").gameObject,"Wrap");Press(Key.RightArrow);});
            Add("Arrows stay in modal",()=>{Press();Expect(EventSystem.current.currentSelectedGameObject==B(Exit,"discardButton").gameObject,"Arrow traversal");EventSystem.current.SetSelectedGameObject(null);});
            Add("Normal state setup",()=>{B(Exit,"saveExitButton").GetComponent<SharedControlAppearance>().SetSelected(false);EventSystem.current.SetSelectedGameObject(null);});
            Add("Hover setup",()=>{ExecuteEvents.Execute(B(Exit,"saveExitButton").gameObject,Pointer(B(Exit,"saveExitButton")),ExecuteEvents.pointerEnterHandler);});
            Add("Hover capture",()=>{Expect(B(Exit,"saveExitButton").GetComponent<SharedControlAppearance>().CurrentState=="hover","Hover");Capture("exit-hover");ExecuteEvents.Execute(B(Exit,"saveExitButton").gameObject,Pointer(B(Exit,"saveExitButton")),ExecuteEvents.pointerDownHandler);});
            Add("Pressed capture",()=>{Capture("exit-pressed");ExecuteEvents.Execute(B(Exit,"saveExitButton").gameObject,Pointer(B(Exit,"saveExitButton")),ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(B(Exit,"saveExitButton").gameObject,Pointer(B(Exit,"saveExitButton")),ExecuteEvents.pointerExitHandler);B(Exit,"saveExitButton").GetComponent<SharedControlAppearance>().SetSelected(true);});
            Add("Selected capture",()=>{Capture("exit-selected");B(Exit,"saveExitButton").GetComponent<SharedControlAppearance>().SetSelected(false);B(Exit,"saveExitButton").interactable=false;});
            Add("Disabled capture",()=>{Capture("exit-disabled");B(Exit,"saveExitButton").interactable=true;Field<TMP_Text>(Exit,"bodyText").text="Você tem alterações que ainda não foram salvas. Confira suas escolhas de localização, restaurante, cardápio, crédito, equipamentos e equipe antes de sair.\n\nSalvar e sair grava seu progresso e fecha o jogo. Sair sem salvar mantém o último progresso gravado e descarta as alterações pendentes. Cancelar devolve você à mesma tela, com todas as escolhas preservadas.";});
            Add("Long native body",()=>{Capture("exit-long-text");Submit(B(Exit,"cancelButton"));Expect(!Exit.IsOpen,"Submit cancel");Unchanged();Click(B(Exit,"openButton"));});
            Add("Save failure through real button",()=>{Db.Execute("CREATE TRIGGER ui18_fail BEFORE INSERT ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'isolated QA failure'); END");Click(B(Exit,"saveExitButton"));Expect(Exit.IsOpen&&!GameManager.Instance.IsShuttingDown&&GameSessionState.HasUnsavedChanges,"Failed save must remain open");Expect(new GameSessionRepository(Db).GetById(id).currentCash==65000,"Failed save changed row");});
            Add("Failure feedback and cancel",()=>{Capture("exit-save-error");Click(B(Exit,"cancelButton"),true);Unchanged();Db.Execute("DROP TRIGGER ui18_fail");Click(B(Exit,"openButton"));});
            Add("Reopen clears obsolete failure",()=>{Expect(Field<TMP_Text>(Exit,"titleText").text=="Sair do jogo","Stale failure");Capture("exit-retry-ready");Click(B(Exit,"cancelButton"));Expect(GameSessionState.TrySave(out _),"Recovery write");Click(B(Exit,"openButton"));});
            Add("All saved state",()=>{Expect(Exit.CurrentMode==ExitDialogController.ExitMode.AllSaved&&!B(Exit,"saveExitButton").gameObject.activeSelf,"All-saved actions");Capture("exit-saved");Click(B(Exit,"cancelButton"));MenuNavigator.Instance.Open(PanelId.Establishment);});
            Add("Open reset",()=>{SaveSnapshot();focus=B(Reset,"resetButton").gameObject;Click(B(Reset,"resetButton"));});
            Add("Reset visual and cancellation",()=>{Capture("reset-warning");Click(B(Reset,"cancelResetButton"),true);Expect(!Reset.IsResetOpen&&EventSystem.current.currentSelectedGameObject==focus,"Reset cancel focus");Unchanged();Click(B(Reset,"resetButton"));});
            Add("Reset Escape",()=>{Press(Key.Escape);});
            Add("Reset escaped without Exit",()=>{Press();Expect(!Reset.IsResetOpen&&!Exit.IsOpen&&EventSystem.current.currentSelectedGameObject==focus,"Reset Escape cleanup");Unchanged();Db.Execute("CREATE TRIGGER ui18_reset_fail BEFORE DELETE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'isolated reset failure'); END");Click(B(Reset,"resetButton"));});
            Add("Confirm reset with isolated failure",()=>Click(B(Reset,"confirmResetButton")));
            Add("Reset failure keeps warning and data",()=>{Expect(Reset.IsResetOpen&&new GameSessionRepository(Db).GetById(id)!=null,"Reset failure lost data");Capture("reset-error");Click(B(Reset,"cancelResetButton"));Db.Execute("DROP TRIGGER ui18_reset_fail");Click(B(Reset,"resetButton"));});
            Add("Explicit reset to identification",()=>{var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;Click(B(Reset,"confirmResetButton"));var draft=GameSessionState.Current;B(Reset,"confirmResetButton").onClick.Invoke();Expect(GameSessionState.Current==draft,"Duplicate reset callback");Expect(GameManager.Instance.StateMachine.CurrentState==InitialDecisionFlow.Identification&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle==scene,"Reset scene/step");Expect(!PlayerSession.HasIdentification&&!GameSessionState.IsPersisted&&new GameSessionRepository(Db).GetById(id)==null,"Reset storage");});
            Add("Reset result",()=>{Capture("reset-identification");Active();GameSessionState.Current.currentRound=4;GameSessionState.Save();beforeReset=GameSessionState.Current;Prepare();});
            Add("Incompatible warning preserves save",()=>{Expect(GameSessionState.HasIncompatibleSave&&Reset.IsResetOpen&&new GameSessionRepository(Db).GetById(beforeReset.sessionId)!=null,"Opening warning deleted data");Capture("incompatible-warning");Press(Key.Escape);});
            Add("Incompatible requires explicit action",()=>{Press();Expect(Reset.IsResetOpen&&!Exit.IsOpen,"Incompatible Escape leaked");Expect(new GameSessionRepository(Db).GetById(beforeReset.sessionId)!=null,"Escape deleted save");Click(B(Reset,"confirmResetButton"),true);Expect(!GameSessionState.HasIncompatibleSave&&!GameSessionState.IsPersisted&&GameManager.Instance.StateMachine.CurrentState==InitialDecisionFlow.Identification,"Explicit incompatible reset");});
            Add("Incompatible reset result",()=>{Capture("incompatible-identification");Active();MenuNavigator.Instance.Open(PanelId.EquipmentStore);});
            Add("Open exit during actual loading",()=>{EquipmentController.BeginCatalogLoad();Expect(Equipment.GetComponent<SharedLoading>().IsBusy,"Actual load in flight");Click(B(Exit,"openButton"));});
            Add("Exit above loading and background feedback",()=>{Expect(Exit.IsOpen&&SharedModalFocus.Active.gameObject==Field<GameObject>(Exit,"dialogRoot"),"Modal lost ownership");FeedbackToast.Show("Feedback de validação isolada",FeedbackKind.Warning);Expect(!FeedbackToast.Current.IsVisible,"Feedback appeared behind modal");Capture("exit-over-store");Click(B(Exit,"cancelButton"));});
            Add("Cancelled exit releases store input",()=>{Expect(!Equipment.GetComponent<SharedLoading>().IsBusy&&Equipment.GetComponent<CanvasGroup>().interactable,"Store stuck after cancel");Expect(EventSystem.current.currentSelectedGameObject==B(Exit,"openButton").gameObject,"Store exit focus restore");Click(B(Equipment,"cartButton"));});
            Add("Kit cart modal",()=>{Capture("cart-empty");Press(Key.Escape);});
            Add("Cart Escape restores invoker",()=>{Press();Expect(!Equipment.CartIsOpen&&!Exit.IsOpen&&EventSystem.current.currentSelectedGameObject==B(Equipment,"cartButton").gameObject,"Cart cancel lifecycle");MenuNavigator.Instance.CloseAll();Complete(false);});
            Add("Completed exit",()=>{Click(B(Exit,"openButton"));});
            Add("Completed saved exit",()=>{Capture("exit-completed");Click(B(Exit,"cancelButton"));Click(B(Root.Find("Panel_FinalReport").GetComponent<FinalReportView>(),"newGameButton"));});
            Add("Completed new game confirmation",()=>{Capture("newgame-completed");Press(Key.Escape);});
            Add("New game cancel preserves report",()=>{Press();Expect(SharedModalFocus.Active==null&&!Exit.IsOpen&&new GameSessionRepository(Db).GetById(id)!=null,"New-game cancel");var v=Root.Find("Panel_FinalReport").GetComponent<FinalReportView>();Click(B(v,"newGameButton"));});
            Add("Explicit new game preserves report",()=>{var v=Root.Find("Panel_FinalReport").GetComponent<FinalReportView>();Click(B(v,"confirmNewGame"));Expect(new GameSessionRepository(Db).GetById(id).finalReportArchived&&new RoundResultRepository(Db).GetBySessionId(id).Count==3,"New game lost history");Expect(!GameSessionState.IsPersisted&&GameManager.Instance.StateMachine.CurrentState==InitialDecisionFlow.Identification,"New game skipped D3");});
            Add("Bankrupt fixture",()=>{Complete(true);});
            Add("Bankrupt exit",()=>{Click(B(Exit,"openButton"));});
            Add("Bankrupt saved exit",()=>{Capture("exit-bankrupt");Click(B(Exit,"cancelButton"));Click(B(Root.Find("Panel_Bankruptcy").GetComponent<BankruptcyView>(),"newGameButton"));});
            Add("Bankrupt new game confirmation",()=>{Capture("newgame-bankrupt");Press(Key.Escape);});
            Add("Bankrupt cancel preserves results",()=>{Press();var v=Root.Find("Panel_Bankruptcy").GetComponent<BankruptcyView>();Expect(SharedModalFocus.Active==null&&!Exit.IsOpen&&EventSystem.current.currentSelectedGameObject==B(v,"newGameButton").gameObject,"Bankrupt modal cancel focus");Click(B(v,"newGameButton"));});
            Add("Explicit bankrupt new game preserves results",()=>{var v=Root.Find("Panel_Bankruptcy").GetComponent<BankruptcyView>();Click(B(v,"confirmNewGame"));Expect(new GameSessionRepository(Db).GetById(id).finalReportArchived&&new RoundResultRepository(Db).GetBySessionId(id).Count==3,"Bankrupt new game lost history");Expect(!GameSessionState.IsPersisted,"Bankrupt draft inserted");});
        }
        Add("Runtime evidence",()=>File.WriteAllText("output/ui-update/UI-18/runtime.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{project=UnityEngine.Application.dataPath,unity=UnityEngine.Application.unityVersion,database=Db.DatabasePath,frame=Time.frameCount},Newtonsoft.Json.Formatting.Indented)));
        next=EditorApplication.timeSinceStartup+.3;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        if(!EditorApplication.isPlaying||report==null)return;
        if(Time.frameCount<=lastFrame||EditorApplication.timeSinceStartup<next){EditorApplication.QueuePlayerLoopUpdate();return;}
        // Wait for the real operation only when no modal is inspecting it.
        if(Equipment.GetComponent<SharedLoading>().IsBusy&&SharedModalFocus.Active==null&&++wait<600){EditorApplication.QueuePlayerLoopUpdate();return;}wait=0;
        if(steps.Count==0){Finish();return;}var step=steps.Dequeue();var check=new Check{name=step.Item1,frame=Time.frameCount};
        try{step.Item2();check.passed=true;}catch(Exception e){check.detail=e.ToString();report.failures++;steps.Clear();Debug.LogError("[UI-18 QA] "+step.Item1+": "+e.Message);}
        report.checks.Add(check);lastFrame=Time.frameCount;next=EditorApplication.timeSinceStartup+.3;EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
    }
    private static void Finish(){Press();if(testKeyboard!=null){InputSystem.RemoveDevice(testKeyboard);testKeyboard=null;}report.completed=report.failures==0;EditorApplication.update-=Tick;File.WriteAllText("output/ui-update/UI-18/playmode.json",JsonUtility.ToJson(report,true));}
}
