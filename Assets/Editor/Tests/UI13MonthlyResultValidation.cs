using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public static class UI13MonthlyResultValidation
{
    [Serializable] private class Check { public string name, detail; public bool passed; public int frame; }
    [Serializable] private class Report { public bool completed; public int failures; public List<Check> checks=new(); }
    private static readonly Queue<(string,Action)> steps=new();
    private static readonly List<object> captures=new();
    private static Report report; private static double next; private static int lastFrame,retries,continued;
    private static RoundResultEntity expected; private static string sessionId; private static float cash; private static int rep;
    private static Transform Root=>GameObject.Find("Canvas").transform;
    private static MonthlyResultView View=>Root.Find("Panel_MonthlyResult").GetComponent<MonthlyResultView>();
    private static MonthlyResultController Controller=>View.GetComponent<MonthlyResultController>();
    private static SQLite4Unity3d.SQLiteConnection Db=>DatabaseInitializer.DatabaseService.Connection;
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
    private static void Expect(bool b,string message) { if(!b)throw new InvalidOperationException(message); }
    private static void Add(string n,Action a)=>steps.Enqueue((n,a));
    private static void OnContinued(RoundResultEntity r)=>continued++;
    private static GameSessionEntity Fixture()
    {
        GameSessionState.Clear();var s=new GameSessionService("ui13-"+Guid.NewGuid(),"test-professor").CreateNewSession();
        var restaurant=Resources.LoadAll<RestaurantData>("Restaurants").Single(r=>r.type==RestaurantType.PODRAO);
        s.restaurantType=restaurant.type;s.locationZone=LocationZone.Comercio;s.targetSegment=Segment.LOW;s.currentCash=s.initialCapital=65000;s.reputationScore=50;
        s.menuPricingJson=MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(restaurant.products));
        s.teamJson=TeamSelectionHelper.ToJson(new TeamSelectionData { members=restaurant.requiredRoles.Select(r=>new TeamSelectionItem {roleId=r.roleId,quantity=r.quantity}).ToList() });
        s.equipmentJson=EquipmentSelectionHelper.ToJson(new EquipmentSelectionData {equipmentIds=restaurant.requiredEquipmentIds.ToList()});
        Expect(GameSessionState.Persist(),"Persist isolated fixture");sessionId=s.sessionId;return s;
    }
    private static void SavedCase(string kind)
    {
        var s=Fixture();
        if(kind=="negative") { s.teamJson=TeamSelectionHelper.ToJson(new TeamSelectionData());s.currentCash=1000;GameSessionState.Persist(); }
        // Deterministic signed snapshots are isolated QA inputs, processed by the real engine and transaction.
        var forecast=MonthlySimulationEngine.Calculate(MonthlySimulationCatalog.Load(s));
        float delta=kind=="zero"?-forecast.netResult:kind=="positive"?Mathf.Abs(forecast.netResult)+5000:0;
        if(delta!=0) new SessionEventHistoryRepository(Db).RecordFromOption(s.sessionId,1,6,
            Resources.LoadAll<EventData>("Events")[0],new EventOption {id="isolated-ui13",title="Isolated validation",effects=new EventEffectData {cashDelta=delta}});
        expected=new RoundService().ProcessRound(1);Expect(expected!=null,"RoundService result");
        Expect(kind=="zero"?expected.netResult==0:kind=="positive"?expected.netResult>0:expected.netResult<0,"Requested result sign");
        cash=GameSessionState.Current.currentCash;rep=GameSessionState.Current.reputationScore;
        GameManager.Instance.StateMachine.ForceState(GameState.Round_Summary);Controller.Refresh();EventSystem.current.SetSelectedGameObject(null);
    }
    private static Rect Bounds(RectTransform r) { var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y); }
    private static PointerEventData Pointer(UnityEngine.UI.Button b)=>new(EventSystem.current) {pointerId=-1,button=PointerEventData.InputButton.Left,position=Bounds((RectTransform)b.transform).center};
    private static void Click(UnityEngine.UI.Button b)
    {
        Canvas.ForceUpdateCanvases();Expect(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);var p=Pointer(b);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Expect(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"nothing"));
        ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    private static void Fits()
    {
        Canvas.ForceUpdateCanvases();foreach(var t in View.GetComponentsInChildren<TMP_Text>()) {t.ForceMeshUpdate();Expect(t.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>()!=null&&!t.enableAutoSizing,"Typography "+t.name);Expect(!t.isTextOverflowing,"Overflow "+t.name+": "+t.text);}
        Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"One EventSystem");
        var globals=new List<UnityEngine.UI.Button>();foreach(var path in new[]{"MenuAccess/Button","HubHistory/Back","ExitUI/ExitButton"})
        {var b=Root.Find(path).GetComponent<UnityEngine.UI.Button>();if(b.gameObject.activeInHierarchy)globals.Add(b);}
        globals.AddRange(View.GetComponentsInChildren<UnityEngine.UI.Button>());
        foreach(var b in globals) {var r=Bounds((RectTransform)b.transform);Expect(r.xMin>=-.5&&r.yMin>=-.5&&r.xMax<=Screen.width+.5&&r.yMax<=Screen.height+.5,"Offscreen "+b.name);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(Pointer(b),hits);Expect(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b,"Control covered "+b.name);}
        for(int i=0;i<globals.Count;i++)for(int j=i+1;j<globals.Count;j++)Expect(!Bounds((RectTransform)globals[i].transform).Overlaps(Bounds((RectTransform)globals[j].transform)),"Overlapping controls");
        var clip=Bounds((RectTransform)View.transform.Find("MonthlyIllustration"));var chart=Bounds((RectTransform)View.transform.Find("LoadedBody/TableScroll/Viewport/Content/CashChart"));Expect(!clip.Overlaps(chart),"Props overlap chart");
        foreach(var image in View.transform.Find("MonthlyIllustration").GetComponentsInChildren<UnityEngine.UI.Image>())Expect(image.preserveAspect&&image.type==UnityEngine.UI.Image.Type.Simple&&image.color==Color.white&&!image.raycastTarget,"Whole art");
    }
    private static void Parity()
    {
        Expect(Controller.Result.roundResultId==expected.roundResultId&&Controller.IsAwaitingContinue,"Saved result and wait");
        var rows=MonthlyResultPresentation.Build(new RoundResultRepository(Db).GetById(expected.roundResultId));
        for(int i=0;i<rows.Count;i++)Expect(View.Rows[i].value.text==rows[i].Value&&View.Rows[i].label.text==rows[i].Label,"Saved row "+i);
        Expect(GameSessionState.Current.currentCash==cash&&GameSessionState.Current.reputationScore==rep,"Read mutated economy");
        Expect(new RoundResultRepository(Db).GetBySessionId(sessionId).Count==1,"Read settled again");
        float[] values={expected.grossRevenue,-expected.TotalCosts,expected.eventCashImpact,expected.netResult};float max=Mathf.Max(1,values.Max(v=>Mathf.Abs(v)));
        for(int i=0;i<4;i++) {Expect(View.Bars[i].value.text==MonthlyResultPresentation.SignedMoney(values[i]),"Chart saved value");Expect(Mathf.Abs(View.Bars[i].fill.sizeDelta.x-232*Mathf.Abs(values[i])/max)<.01,"Chart scale");}
    }
    private static void Capture(string name,bool screen=true)
    {
        if(screen)Fits();string path=UI01Validation.Capture(UiValidationContext.CaptureName("UI-13",name));UiValidationContext.OnCapture?.Invoke(name,path); using var hash=System.Security.Cryptography.SHA256.Create();
        captures.Add(new {name,path,frame=Time.frameCount,width=Screen.width,height=Screen.height,sha256=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant()});
        File.WriteAllText(UiValidationContext.FileFor("UI-13","capture-frames.json"),Newtonsoft.Json.JsonConvert.SerializeObject(captures,Newtonsoft.Json.Formatting.Indented));
    }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage();Expect(EditorApplication.isPlaying,"Use Play Mode");Directory.CreateDirectory(UiValidationContext.DirectoryFor("UI-13"));
        report=new Report();steps.Clear();captures.Clear();lastFrame=-1;continued=0;retries=0;Controller.Continued-=OnContinued;Controller.Continued+=OnContinued;
        foreach(var resolution in UiValidationContext.Resolutions)
        {
            var size=resolution;string res=size.x+"x"+size.y;Add("Set viewport "+res,()=>UI01Validation.SetGameView(size.x,size.y));
            foreach(string kind in new[]{"positive","negative","zero"})
            {
                string state=kind;Add("Settle isolated "+state+" "+res,()=>SavedCase(state));
                Add("Persisted values, native chart and wait "+state+" "+res,()=>{Expect(Screen.width==size.x&&Screen.height==size.y,"Rendered resolution");Parity();Capture(state+"-top-"+res);Controller.Refresh();View.TableScroll.verticalNormalizedPosition=0;});
                Add("Scroll indicators and repeat read "+state+" "+res,()=>{Parity();Capture(state+"-bottom-"+res);});
                Add("Explicit continue "+state+" "+res,()=>{Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Round_Summary,"Still readable");int before=continued;Click(View.ContinueButton);Controller.Continue();Expect(continued==before+1&&GameManager.Instance.StateMachine.CurrentState==GameState.Management_Hub,"One explicit continuation");Expect(GameSessionState.Current.currentCash==cash,"Continuation debited cash");});
            }
            Add("Real loan and signed event "+res,()=>
            {
                Fixture();var line=Resources.LoadAll<CreditLineData>("CreditLines").Where(l=>l.maxAmount>0).OrderBy(l=>l.maxAmount).First();Expect(LoanService.TryContract(line),"Actual credit contract");
                var pair=Resources.LoadAll<EventData>("Events").SelectMany(e=>e.options.Select(o=>new {e,o})).First(p=>p.o.effects.cashDelta<0);
                new SessionEventHistoryRepository(Db).RecordFromOption(sessionId,1,6,pair.e,pair.o);expected=new RoundService().ProcessRound(1);
                Expect(expected.loanPrincipalPayment>0&&expected.loanPayment>=expected.loanPrincipalPayment&&expected.eventCashImpact<0,"Principal, interest and signed event");
                cash=GameSessionState.Current.currentCash;rep=GameSessionState.Current.reputationScore;GameManager.Instance.StateMachine.ForceState(GameState.Round_Summary);Controller.Refresh();
            });
            Add("Loan included once "+res,()=>{Parity();Capture("loan-top-"+res);View.TableScroll.verticalNormalizedPosition=0;});
            Add("Principal and interest details "+res,()=>{Parity();Capture("loan-bottom-"+res);Click(View.ContinueButton);});
            Add("Empty current session "+res,()=>{Fixture();MenuNavigator.Instance.Open(PanelId.MonthlyResults);});
            Add("Empty has no figures "+res,()=>{Expect(View.DisplayState=="empty"&&!Field<GameObject>(View,"loadedBody").activeSelf,"Empty statement");Capture("empty-"+res);Click(View.ContinueButton);});
            Add("Missing database "+res,()=>{Fixture();MenuNavigator.Instance.Open(PanelId.MonthlyResults);typeof(DatabaseService).GetProperty("Connection").SetValue(DatabaseInitializer.DatabaseService,null);Controller.Refresh();});
            Add("Error and retry "+res,()=>{Expect(View.DisplayState=="error"&&View.RetryButton.IsActive(),"Error state");typeof(DatabaseService).GetProperty("Connection").SetValue(DatabaseInitializer.DatabaseService,isolatedDb);Capture("error-"+res);Click(View.RetryButton);Expect(View.DisplayState=="empty","Retry queries");Click(View.ContinueButton);});
            Add("Load state interactions "+res,()=>{SavedCase("positive");View.TableScroll.verticalNormalizedPosition=1;});
            Add("Normal "+res,()=>{Capture("normal-"+res);ExecuteEvents.Execute(View.ContinueButton.gameObject,Pointer(View.ContinueButton),ExecuteEvents.pointerEnterHandler);});
            Add("Hover "+res,()=>{Expect(View.ContinueButton.GetComponent<SharedControlAppearance>().CurrentState=="hover","Hover");Capture("hover-"+res);ExecuteEvents.Execute(View.ContinueButton.gameObject,Pointer(View.ContinueButton),ExecuteEvents.pointerDownHandler);});
            Add("Pressed "+res,()=>{Expect(View.ContinueButton.GetComponent<SharedControlAppearance>().CurrentState=="pressed","Pressed");Capture("pressed-"+res);ExecuteEvents.Execute(View.ContinueButton.gameObject,Pointer(View.ContinueButton),ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(View.ContinueButton.gameObject,Pointer(View.ContinueButton),ExecuteEvents.pointerExitHandler);View.ContinueButton.Select();});
            Add("Focus "+res,()=>{Expect(View.ContinueButton.GetComponent<SharedControlAppearance>().CurrentState=="focus","Focus");Capture("focus-"+res);View.ContinueButton.GetComponent<SharedControlAppearance>().SetSelected(true);});
            Add("Selected "+res,()=>{Capture("selected-"+res);View.ContinueButton.GetComponent<SharedControlAppearance>().SetSelected(false);View.ContinueButton.interactable=false;});
            Add("Disabled "+res,()=>{Expect(View.ContinueButton.GetComponent<SharedControlAppearance>().CurrentState=="disabled","Disabled");Capture("disabled-"+res);Controller.Refresh();});
            Add("Long persisted classification "+res,()=>{var r=new RoundResultRepository(Db).GetById(expected.roundResultId);r.alignmentClassification=string.Join(" ",Enumerable.Repeat("Classificação histórica extensa preservada na consulta",8));Db.Update(r);Controller.Refresh();View.TableScroll.verticalNormalizedPosition=0;});
            Add("Long wrapping and mask "+res,()=>{Capture("long-"+res);var r=new RoundResultRepository(Db).GetById(expected.roundResultId);r.alignmentClassification="HIGH";Db.Update(r);Controller.Refresh();Click(View.ContinueButton);});
            Add("Revisit from hub shortcut "+res,()=>{Click(Root.Find("Panel_Menu/HubSummary/MonthlyResult").GetComponent<UnityEngine.UI.Button>());});
            Add("Read-only consultation "+res,()=>{Expect(!Controller.IsAwaitingContinue&&Controller.Result.roundResultId==expected.roundResultId,"Consultation");Capture("consultation-"+res);Click(View.ContinueButton);});
            Add("Hub shortcut preserves progress "+res,()=>
            {
                var hub=Root.Find("Panel_Menu/HubSummary");var shortcut=hub.Find("MonthlyResult").GetComponent<UnityEngine.UI.Button>();var button=Bounds((RectTransform)shortcut.transform);
                foreach(string path in new[]{"Progress","ProgressTrack","Establishment","Help"})Expect(!button.Overlaps(Bounds((RectTransform)hub.Find(path))),"Shortcut overlaps "+path);
                foreach(var t in hub.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();Expect(!t.isTextOverflowing,"Hub overflow "+t.name);}
                Capture("hub-shortcut-"+res,false);
            });
        }
        Add("Actual close-month flow",()=>{Fixture();GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub);Root.Find("UiNavigation").GetComponent<MonthlySettlementController>().CloseMonth();});
        Add("Waits for all event choices",()=>{var e=Root.Find("Panel_Event").GetComponent<EventScreenController>();Expect(e.IsAwaitingChoice&&new RoundResultRepository(Db).GetBySessionId(sessionId).Count==0,"UI12 wait");});
        for(int i=0;i<4;i++)Add("Confirm real monthly event "+i,()=>{var e=Root.Find("Panel_Event").GetComponent<EventScreenController>();var v=e.GetComponent<EventScreenView>();Click(v.Cards[0].Button);Click(v.ConfirmButton);});
        Add("Summary remains after all choices",()=>{expected=new RoundResultRepository(Db).GetLastRoundBySessionId(sessionId);cash=GameSessionState.Current.currentCash;rep=GameSessionState.Current.reputationScore;Parity();Capture("actual-flow-waits-"+Screen.width+"x"+Screen.height);});
        Add("Continue actual flow",()=>{Click(View.ContinueButton);Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Management_Hub,"Hub destination");GameSessionState.LoadActiveSession(GameSessionState.Current.userId);MenuNavigator.Instance.Open(PanelId.MonthlyResults);});
        Add("Reloaded persisted consultation",()=>{Expect(Controller.Result.roundResultId==expected.roundResultId,"Reload");Capture("reload-"+Screen.width+"x"+Screen.height);Click(View.ContinueButton);});
        Add("Completed month-three waits",()=>FinalCase(false));Add("Completed still readable",()=>{Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Round_Summary,"Final wait");Capture("month-three-completed-"+Screen.width+"x"+Screen.height);Click(View.ContinueButton);Expect(GameManager.Instance.StateMachine.CurrentState==GameState.FinalReport,"Final report destination");});
        Add("Bankrupt month-three waits",()=>FinalCase(true));Add("Bankrupt still readable",()=>{Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Round_Summary,"Bankrupt wait");Capture("month-three-bankrupt-"+Screen.width+"x"+Screen.height);Click(View.ContinueButton);Expect(GameManager.Instance.StateMachine.CurrentState==GameState.GameOver_Bankruptcy,"Bankruptcy destination");});
        Add("Runtime evidence",()=>File.WriteAllText(UiValidationContext.FileFor("UI-13","runtime.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new {project=UnityEngine.Application.dataPath,unity=UnityEngine.Application.unityVersion,database=isolatedDb.DatabasePath,frame=Time.frameCount,continued},Newtonsoft.Json.Formatting.Indented)));
        isolatedDb=Db;next=EditorApplication.timeSinceStartup+.5;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    private static SQLite4Unity3d.SQLiteConnection isolatedDb;
    private static void FinalCase(bool loss)
    {
        var s=Fixture();if(loss){s.teamJson=TeamSelectionHelper.ToJson(new TeamSelectionData());GameSessionState.Persist();}
        for(int month=1;month<=3;month++)
        {
            if(!loss) {s=GameSessionState.Current;var forecast=MonthlySimulationEngine.Calculate(MonthlySimulationCatalog.Load(s));new SessionEventHistoryRepository(Db).RecordFromOption(s.sessionId,month,6,Resources.LoadAll<EventData>("Events")[0],new EventOption {id="isolated-ui13",title="Isolated validation",effects=new EventEffectData {cashDelta=Mathf.Abs(forecast.netResult)+5000}});}
            expected=new RoundService().ProcessRound(month);
        }
        Expect(GameSessionState.Current.status==(loss?GameSessionStatus.BANKRUPT:GameSessionStatus.COMPLETED),"Final financial status");
        GameManager.Instance.StateMachine.ForceState(GameState.Round_Summary);Controller.Refresh();
    }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        if(!EditorApplication.isPlaying||steps.Count==0) {report.completed=steps.Count==0&&report.failures==0;EditorApplication.update-=Tick;File.WriteAllText(UiValidationContext.FileFor("UI-13","playmode.json"),JsonUtility.ToJson(report,true));return;}
        if(Time.frameCount<=lastFrame){EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();return;}
        var step=steps.Dequeue();var check=new Check {name=step.Item1,frame=Time.frameCount};
        try {step.Item2();check.passed=true;retries=0;}
        catch(InvalidOperationException e)when(e.Message=="The Game View must render at the evidence resolution."&&retries++<10){var rest=steps.ToArray();steps.Clear();steps.Enqueue(step);foreach(var r in rest)steps.Enqueue(r);next=EditorApplication.timeSinceStartup+1;return;}
        catch(Exception e){check.detail=e.ToString();report.failures++;steps.Clear();Debug.LogError("[UI-13 QA] "+check.name+": "+e.Message);}
        report.checks.Add(check);lastFrame=Time.frameCount;EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();next=EditorApplication.timeSinceStartup+.35;
    }
}
