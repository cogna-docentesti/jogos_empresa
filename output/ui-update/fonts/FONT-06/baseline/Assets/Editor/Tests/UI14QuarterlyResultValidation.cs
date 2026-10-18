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

public static class UI14QuarterlyResultValidation
{
    [Serializable] private class Check { public string name, detail; public bool passed; public int frame; }
    [Serializable] private class Report { public bool completed; public int failures; public List<Check> checks=new(); }
    private static readonly Queue<(string,Action)> steps=new();
    private static readonly List<object> captures=new();
    private static Report report; private static double next; private static int lastFrame,retries;
    private static string sessionId, snapshot; private static int count; private static SQLite4Unity3d.SQLiteConnection isolatedDb;
    private static Transform Root=>GameObject.Find("Canvas").transform;
    private static QuarterlyResultView View=>Root.Find("Panel_QuarterlyResult").GetComponent<QuarterlyResultView>();
    private static QuarterlyResultController Controller=>View.GetComponent<QuarterlyResultController>();
    private static MonthlyResultView Monthly=>Root.Find("Panel_MonthlyResult").GetComponent<MonthlyResultView>();
    private static SQLite4Unity3d.SQLiteConnection Db=>DatabaseInitializer.DatabaseService.Connection;
    private static void Expect(bool b,string message) { if(!b)throw new InvalidOperationException(message); }
    private static void Add(string n,Action a)=>steps.Enqueue((n,a));
    private static GameSessionEntity Fixture()
    {
        MenuNavigator.Instance.CloseAll();Root.Find("Panel_MonthlyResult").gameObject.SetActive(false);
        GameSessionState.Clear();var s=new GameSessionService("ui14-"+Guid.NewGuid(),"test-professor").CreateNewSession();
        var restaurant=Resources.LoadAll<RestaurantData>("Restaurants").Single(r=>r.type==RestaurantType.PODRAO);
        s.restaurantType=restaurant.type;s.locationZone=LocationZone.Comercio;s.targetSegment=Segment.LOW;s.currentCash=s.initialCapital=65000;s.reputationScore=50;
        s.menuPricingJson=MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(restaurant.products));
        s.teamJson=TeamSelectionHelper.ToJson(new TeamSelectionData {members=restaurant.requiredRoles.Select(r=>new TeamSelectionItem {roleId=r.roleId,quantity=r.quantity}).ToList()});
        s.equipmentJson=EquipmentSelectionHelper.ToJson(new EquipmentSelectionData {equipmentIds=restaurant.requiredEquipmentIds.ToList()});
        Expect(GameSessionState.Persist(),"Persist isolated fixture");sessionId=s.sessionId;return s;
    }
    private static void SavedCase(int months,string kind="positive")
    {
        var s=Fixture();
        if(kind=="negative") {s.teamJson=TeamSelectionHelper.ToJson(new TeamSelectionData());s.currentCash=1000;GameSessionState.Persist();}
        if(kind=="positive") {var line=Resources.LoadAll<CreditLineData>("CreditLines").Where(l=>l.maxAmount>0).OrderBy(l=>l.maxAmount).First();Expect(LoanService.TryContract(line),"Real loan");}
        for(int month=1;month<=months;month++)
        {
            s=GameSessionState.Current;var forecast=MonthlySimulationEngine.Calculate(MonthlySimulationCatalog.Load(s));
            float delta=kind=="zero"?-forecast.netResult:kind=="positive"?Mathf.Abs(forecast.netResult)+5000*month:0;
            if(delta!=0)new SessionEventHistoryRepository(Db).RecordFromOption(sessionId,month,6,Resources.LoadAll<EventData>("Events")[0],new EventOption {id="isolated-ui14",title="Isolated validation",effects=new EventEffectData {cashDelta=delta}});
            Expect(new RoundService().ProcessRound(month)!=null,"Actual settlement "+month);
        }
        count=months;snapshot=Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current);
        GameManager.Instance.StateMachine.ForceState(GameState.Round_Summary);Monthly.QuarterlyButton.onClick.Invoke();EventSystem.current.SetSelectedGameObject(null);
    }
    private static Rect Bounds(RectTransform r) {var c=new Vector3[4];r.GetWorldCorners(c);return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);}
    private static PointerEventData Pointer(UnityEngine.UI.Button b)=>new(EventSystem.current) {pointerId=-1,button=PointerEventData.InputButton.Left,position=Bounds((RectTransform)b.transform).center};
    private static void Click(UnityEngine.UI.Button b)
    {
        Canvas.ForceUpdateCanvases();Expect(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);var p=Pointer(b);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Expect(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"nothing"));
        ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    private static void Fits()
    {
        Canvas.ForceUpdateCanvases();foreach(var t in View.GetComponentsInChildren<TMP_Text>()) {t.ForceMeshUpdate();Expect(t.font.name=="InterTight-Variable"&&!t.enableAutoSizing,"Typography "+t.name);Expect(!t.isTextOverflowing,"Overflow "+t.name+": "+t.text);}
        Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length==1,"One EventSystem");
        var globals=new List<UnityEngine.UI.Button>();foreach(var path in new[]{"MenuAccess/Button","HubHistory/Back","ExitUI/ExitButton"}) {var b=Root.Find(path).GetComponent<UnityEngine.UI.Button>();if(b.gameObject.activeInHierarchy)globals.Add(b);}
        globals.AddRange(View.GetComponentsInChildren<UnityEngine.UI.Button>());
        foreach(var b in globals) {var r=Bounds((RectTransform)b.transform);Expect(r.xMin>=-.5&&r.yMin>=-.5&&r.xMax<=Screen.width+.5&&r.yMax<=Screen.height+.5,"Offscreen "+b.name);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(Pointer(b),hits);Expect(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b,"Control covered "+b.name);}
        for(int i=0;i<globals.Count;i++)for(int j=i+1;j<globals.Count;j++)Expect(!Bounds((RectTransform)globals[i].transform).Overlaps(Bounds((RectTransform)globals[j].transform)),"Overlapping controls");
        var clip=Bounds((RectTransform)View.transform.Find("QuarterlyIllustration"));Expect(!clip.Overlaps(Bounds((RectTransform)View.transform.Find("LoadedBody/ResultChart"))),"Props overlap chart");Expect(!clip.Overlaps(Bounds((RectTransform)View.transform.Find("LoadedBody/TableScroll"))),"People overlap table");
        foreach(var image in View.transform.Find("QuarterlyIllustration").GetComponentsInChildren<UnityEngine.UI.Image>())Expect(image.preserveAspect&&image.type==UnityEngine.UI.Image.Type.Simple&&image.color==Color.white&&!image.raycastTarget,"Whole art");
    }
    private static void Parity()
    {
        var saved=new RoundResultRepository(Db).GetBySessionId(sessionId);var expected=QuarterlyResultPresentation.Build(sessionId,saved);
        Expect(Controller.Report.Count==expected.Count&&Controller.Report.Net==expected.Net,"Campaign query");
        for(int i=0;i<expected.Rows.Count;i++) {
            var cells=expected.Rows[i].Months.Concat(new[]{expected.Rows[i].Aggregate,expected.Rows[i].Change}).ToArray();
            Expect(View.Rows[i].label.text==expected.Rows[i].Label&&View.Rows[i].rule.text==expected.Rows[i].Rule,"Aggregation rule "+i);
            for(int c=0;c<5;c++)Expect(View.Rows[i].cells[c].text==cells[c].Text,"Saved cell "+i+"/"+c);
        }
        double max=Math.Max(1,saved.Select(r=>Math.Abs((double)r.netResult)).Max());
        for(int i=0;i<3;i++) {var r=expected.Months[i];Expect(View.Bars[i].value.text==(r==null?"Não salvo":QuarterlyResultPresentation.SignedMoney(r.netResult)),"Chart value");Expect(Math.Abs(View.Bars[i].fill.sizeDelta.x-(r==null?0:232*Math.Abs(r.netResult)/max))<.01,"Chart scale");}
        Expect(Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current)==snapshot,"Consultation mutated session");Expect(saved.Count==count,"Consultation settled again");
    }
    private static void Capture(string name,bool screen=true)
    {
        if(screen)Fits();string path=UI01Validation.Capture("../UI-14/"+name);using var hash=System.Security.Cryptography.SHA256.Create();
        captures.Add(new {name,path,frame=Time.frameCount,width=Screen.width,height=Screen.height,sha256=BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant()});
        File.WriteAllText("output/ui-update/UI-14/capture-frames.json",Newtonsoft.Json.JsonConvert.SerializeObject(captures,Newtonsoft.Json.Formatting.Indented));
    }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage();Expect(EditorApplication.isPlaying,"Use Play Mode");Directory.CreateDirectory("output/ui-update/UI-14");
        report=new Report();steps.Clear();captures.Clear();lastFrame=-1;retries=0;isolatedDb=Db;
        foreach(var resolution in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)})
        {
            var size=resolution;string res=size.x+"x"+size.y;Add("Set viewport "+res,()=>UI01Validation.SetGameView(size.x,size.y));
            foreach(int n in new[]{1,2,3})
            {
                int months=n;Add("Settle "+months+" months "+res,()=>SavedCase(months));
                foreach(float pos in new[]{1f,.75f,.5f,.25f,0f}){
                    float scroll=pos;Add("Scroll "+months+"/3 "+scroll+" "+res,()=>View.TableScroll.verticalNormalizedPosition=scroll);
                    Add("Saved cells and capture "+months+"/3 "+scroll+" "+res,()=>{Expect(Screen.width==size.x&&Screen.height==size.y,"Rendered resolution");Parity();Capture("months-"+months+"-scroll-"+scroll.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+"-"+res);});
                }
                Add("Return from quarterly "+months+" "+res,()=>Click(View.BackButton));
                Add("Summary still waits "+months+" "+res,()=>{Expect(Monthly.gameObject.activeInHierarchy&&GameManager.Instance.StateMachine.CurrentState==GameState.Round_Summary,"Awaiting summary");Expect(EventSystem.current.currentSelectedGameObject==Monthly.QuarterlyButton.gameObject,"Focus restored");Capture("monthly-return-"+months+"-"+res,false);});
            }
            foreach(string kind in new[]{"negative","zero"}){
                string k=kind;Add("Saved "+k+" "+res,()=>SavedCase(3,k));Add("Chart "+k+" "+res,()=>{Parity();Capture(k+"-"+res);});
            }
            Add("Empty campaign "+res,()=>{Fixture();GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub);MenuNavigator.Instance.Open(PanelId.QuarterlyResults);});
            Add("Empty has no figures "+res,()=>{Expect(View.DisplayState=="empty","Empty state");Capture("empty-"+res);});
            Add("Unavailable database "+res,()=>{typeof(DatabaseService).GetProperty("Connection").SetValue(DatabaseInitializer.DatabaseService,null);Controller.Refresh();});
            Add("Error and retry "+res,()=>{Expect(View.DisplayState=="error"&&View.RetryButton.IsActive(),"Error state");typeof(DatabaseService).GetProperty("Connection").SetValue(DatabaseInitializer.DatabaseService,isolatedDb);Capture("error-"+res);Click(View.RetryButton);Expect(View.DisplayState=="empty","Retry query");Click(View.BackButton);});
            Add("Missing middle month "+res,()=>{SavedCase(3);Db.Delete(new RoundResultRepository(Db).GetBySessionAndRound(sessionId,2));count=2;Controller.Refresh();});
            Add("Gap is explicit "+res,()=>{Parity();Expect(!Controller.Report.Complete&&View.Rows[0].cells[1].text=="Não salvo","Gap");Capture("gap-"+res);});
            Add("Load states "+res,()=>SavedCase(2));
            Add("Normal "+res,()=>{Capture("normal-"+res);ExecuteEvents.Execute(View.BackButton.gameObject,Pointer(View.BackButton),ExecuteEvents.pointerEnterHandler);});
            Add("Hover "+res,()=>{Expect(View.BackButton.GetComponent<SharedControlAppearance>().CurrentState=="hover","Hover");Capture("hover-"+res);ExecuteEvents.Execute(View.BackButton.gameObject,Pointer(View.BackButton),ExecuteEvents.pointerDownHandler);});
            Add("Pressed "+res,()=>{Expect(View.BackButton.GetComponent<SharedControlAppearance>().CurrentState=="pressed","Pressed");Capture("pressed-"+res);ExecuteEvents.Execute(View.BackButton.gameObject,Pointer(View.BackButton),ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(View.BackButton.gameObject,Pointer(View.BackButton),ExecuteEvents.pointerExitHandler);View.BackButton.Select();});
            Add("Focus "+res,()=>{Expect(View.BackButton.GetComponent<SharedControlAppearance>().CurrentState=="focus","Focus");Capture("focus-"+res);View.BackButton.GetComponent<SharedControlAppearance>().SetSelected(true);});
            Add("Selected "+res,()=>{Capture("selected-"+res);View.BackButton.GetComponent<SharedControlAppearance>().SetSelected(false);View.BackButton.interactable=false;});
            Add("Disabled "+res,()=>{Expect(View.BackButton.GetComponent<SharedControlAppearance>().CurrentState=="disabled","Disabled");Capture("disabled-"+res);View.BackButton.interactable=true;});
            Add("Long historical label "+res,()=>{var r=new RoundResultRepository(Db).GetBySessionAndRound(sessionId,2);r.alignmentClassification=string.Join(" ",Enumerable.Repeat("Classificação histórica extensa",10));Db.Update(r);Controller.Refresh();View.TableScroll.verticalNormalizedPosition=.15f;});
            Add("Long wraps within row "+res,()=>{Parity();Capture("long-"+res);View.TableScroll.verticalNormalizedPosition=1;ExecuteEvents.Execute(View.TableScroll.gameObject,new PointerEventData(EventSystem.current) {scrollDelta=new Vector2(0,-5)},ExecuteEvents.scrollHandler);});
            Add("Wheel and keyboard submit "+res,()=>{Expect(View.TableScroll.verticalNormalizedPosition<1,"Wheel scroll");View.BackButton.Select();ExecuteEvents.Execute(View.BackButton.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);Expect(Monthly.gameObject.activeInHierarchy,"Keyboard return");});
            Add("Return management hub "+res,()=>GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub));
            Add("Hub shortcut "+res,()=>Click(Root.Find("Panel_Menu/HubSummary/QuarterlyResult").GetComponent<UnityEngine.UI.Button>()));
            Add("Hub navigation history "+res,()=>{Expect(MenuNavigator.Instance.Current==PanelId.QuarterlyResults,"Quarter destination");Capture("consultation-"+res);Click(View.BackButton);});
            Add("Hub focus and controls "+res,()=>{Expect(MenuNavigator.Instance.Current==PanelId.Menu,"Hub return");var hub=Root.Find("Panel_Menu/HubSummary");Expect(EventSystem.current.currentSelectedGameObject==hub.Find("QuarterlyResult").gameObject,"Hub focus");var paths=new[]{"Establishment","MonthlyResult","QuarterlyResult","Help","Progress","ProgressTrack"};for(int i=0;i<3;i++)for(int j=i+1;j<paths.Length;j++)Expect(!Bounds((RectTransform)hub.Find(paths[i])).Overlaps(Bounds((RectTransform)hub.Find(paths[j]))),"Hub overlap "+paths[i]+"/"+paths[j]);foreach(var t in hub.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();Expect(!t.isTextOverflowing,"Hub text "+t.name);}Capture("hub-"+res,false);});
            Add("Reload saved campaign "+res,()=>{GameSessionState.LoadActiveSession(GameSessionState.Current.userId);MenuNavigator.Instance.Open(PanelId.MonthlyResults);});
            Add("Open comparison after monthly frame "+res,()=>Click(Monthly.QuarterlyButton));
            Add("Reload refreshes quarterly "+res,()=>{Parity();Capture("reload-"+res);Click(View.BackButton);});
            Add("Return from monthly consultation "+res,()=>Click(Monthly.ContinueButton));
        }
        Add("Runtime evidence",()=>File.WriteAllText("output/ui-update/UI-14/runtime.json",Newtonsoft.Json.JsonConvert.SerializeObject(new {project=UnityEngine.Application.dataPath,unity=UnityEngine.Application.unityVersion,database=isolatedDb.DatabasePath,frame=Time.frameCount},Newtonsoft.Json.Formatting.Indented)));
        next=EditorApplication.timeSinceStartup+.5;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        if(!EditorApplication.isPlaying||steps.Count==0) {report.completed=steps.Count==0&&report.failures==0;EditorApplication.update-=Tick;File.WriteAllText("output/ui-update/UI-14/playmode.json",JsonUtility.ToJson(report,true));return;}
        if(Time.frameCount<=lastFrame){EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();return;}
        var step=steps.Dequeue();var check=new Check {name=step.Item1,frame=Time.frameCount};
        try {step.Item2();check.passed=true;retries=0;}
        catch(InvalidOperationException e)when(e.Message=="The Game View must render at the evidence resolution."&&retries++<10){var rest=steps.ToArray();steps.Clear();steps.Enqueue(step);foreach(var r in rest)steps.Enqueue(r);next=EditorApplication.timeSinceStartup+1;return;}
        catch(Exception e){check.detail=e.ToString();report.failures++;steps.Clear();Debug.LogError("[UI-14 QA] "+check.name+": "+e.Message);}
        report.checks.Add(check);lastFrame=Time.frameCount;EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();next=EditorApplication.timeSinceStartup+.35;
    }
}
