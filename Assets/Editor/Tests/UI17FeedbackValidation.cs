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

public static class UI17FeedbackValidation
{
    [Serializable] private class Check { public string name, detail; public bool passed; public int frame; }
    [Serializable] private class Report { public bool completed; public int failures; public List<Check> checks = new(); }
    private static readonly Queue<(string, Action)> steps = new();
    private static readonly List<object> captures = new();
    private static Report report;
    private static double next;
    private static int lastFrame, waitFrames, lastCaptureFrame;
    private static string res, sessionId;
    private static string[] paths;
    private static float cash;
    private static bool loadingCaptured;
    private static Transform Root => GameObject.Find("Canvas").transform;
    private static Transform Bank => Root.Find("Panel_Financial");
    private static EquipmentScreenView Equipment => Root.Find("Panel_Equipment").GetComponent<EquipmentScreenView>();
    private static EquipmentScreenController EquipmentController => Equipment.GetComponent<EquipmentScreenController>();
    private static SharedLoading Loader => Equipment.GetComponent<SharedLoading>();
    private static TeamScreenView Team => Root.Find("Panel_Team").GetComponent<TeamScreenView>();
    private static SQLite4Unity3d.SQLiteConnection Db => DatabaseInitializer.DatabaseService.Connection;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Expect(bool b, string text) { if (!b) throw new InvalidOperationException(text); }
    private static void Add(string name, Action action) => steps.Enqueue((name, action));
    private static Rect Bounds(RectTransform r) { var c = new Vector3[4]; r.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
    private static PointerEventData Pointer(UnityEngine.UI.Button b, int id = -1) => new(EventSystem.current) { pointerId = id, button = PointerEventData.InputButton.Left, position = Bounds((RectTransform)b.transform).center };
    private static void Click(UnityEngine.UI.Button b, bool touch = false)
    {
        Canvas.ForceUpdateCanvases(); Expect(b.IsActive() && b.IsInteractable(), "Unavailable " + b.name);
        var p = Pointer(b, touch ? 4 : -1); var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(p, hits);
        Expect(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == b, "Covered " + b.name + " by " + (hits.Count > 0 ? hits[0].gameObject.name : "nothing"));
        ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerClickHandler);
    }
    private static void Submit(UnityEngine.UI.Button b) { b.Select(); ExecuteEvents.Execute(b.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler); }
    private static ContextualHelp Help(Component c) => c.GetComponent<ContextualHelp>();
    private static UnityEngine.UI.Button Dismiss(ContextualHelp h) => Field<UnityEngine.UI.Button>(h, "dismiss");
    private static void Fixture()
    {
        FeedbackToast.Current.Hide(); MenuNavigator.Instance.CloseAll();
        GameSessionState.Clear();
        var s = new GameSessionService("ui17-" + Guid.NewGuid(), "test-professor").CreateNewSession();
        var r = Resources.LoadAll<RestaurantData>("Restaurants").Single(x => x.type == RestaurantType.PODRAO);
        s.companyName = "Restaurante da validação UI"; s.restaurantType = r.type; s.targetSegment = Segment.LOW; s.locationZone = LocationZone.Comercio; s.currentCash = s.initialCapital = 65000; s.reputationScore = 50;
        s.menuPricingJson = MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(r.products)); s.teamJson = "{}"; s.equipmentJson = "{}";
        Expect(GameSessionState.Persist(), "Persist fixture after menu"); sessionId = s.sessionId;
        GameManager.Instance.StateMachine.ForceState(GameState.Initial_Capital); EventSystem.current.SetSelectedGameObject(null);
    }
    private static void Fits()
    {
        Canvas.ForceUpdateCanvases(); Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One EventSystem");
        foreach (var help in Root.GetComponentsInChildren<ContextualHelp>())
        {
            if (!help.IsVisible) continue;
            foreach (var t in Field<GameObject>(help, "panel").GetComponentsInChildren<TMP_Text>()) { t.ForceMeshUpdate(); Expect(!t.isTextOverflowing && t.font.name == "InterTight-Variable" && !t.enableAutoSizing, "Guide typography/overflow " + t.name); }
        }
        if (FeedbackToast.Current.IsVisible)
        { var t = Field<TMP_Text>(FeedbackToast.Current, "message"); t.ForceMeshUpdate(); Expect(!t.isTextOverflowing, "Toast overflow"); }
        foreach (var b in Root.GetComponentsInChildren<UnityEngine.UI.Button>().Where(x => x.IsInteractable()))
        {
            var scroll = b.GetComponentInParent<UnityEngine.UI.ScrollRect>(); var r = Bounds((RectTransform)b.transform);
            if (scroll != null && !Bounds(scroll.viewport).Contains(r.center)) continue;
            if (b.GetComponentInParent<SharedComponentsShowcase>() != null) continue;
            Expect(r.xMin >= -1 && r.yMin >= -1 && r.xMax <= Screen.width + 1 && r.yMax <= Screen.height + 1, "Offscreen " + b.name);
            // Modal ownership is checked by the real click paths below.
        }
    }
    private static void Capture(string name)
    {
        Fits(); string p = UI01Validation.Capture("../UI-17/" + name + "-" + res);
        using var hash = System.Security.Cryptography.SHA256.Create();
        captures.Add(new { name, path = p, frame = Time.frameCount, width = Screen.width, height = Screen.height, sha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant() });
        lastCaptureFrame = Time.frameCount;
        File.WriteAllText("output/ui-update/UI-17/capture-frames.json", Newtonsoft.Json.JsonConvert.SerializeObject(captures, Newtonsoft.Json.Formatting.Indented));
    }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage(); Expect(EditorApplication.isPlaying, "Use Play Mode"); Directory.CreateDirectory("output/ui-update/UI-17");
        steps.Clear(); captures.Clear(); report = new Report(); lastFrame = -1; paths = Resources.Load<EquipmentCatalogIndex>("EquipmentCatalogIndex").resourcePaths.ToArray();
        foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1366,768), new Vector2Int(1280,720) })
        {
            var resolution = size;
            Add("Set resolution " + size, () => { res = resolution.x + "x" + resolution.y; UI01Validation.SetGameView(resolution.x,resolution.y); });
            Add("First bank visit", Fixture);
            Add("Bank tap guide", () => { Expect(Help(Bank).IsVisible, "Initial tap guide"); Capture("bank-first"); });
            Add("Guide normal", () => { EventSystem.current.SetSelectedGameObject(null); Capture("guide-normal"); var b=Dismiss(Help(Bank)); ExecuteEvents.Execute(b.gameObject, Pointer(b), ExecuteEvents.pointerEnterHandler); });
            Add("Guide hover", () => { var b=Dismiss(Help(Bank)); Expect(b.GetComponent<SharedControlAppearance>().CurrentState=="hover", "Hover"); Capture("guide-hover"); ExecuteEvents.Execute(b.gameObject,Pointer(b),ExecuteEvents.pointerDownHandler); });
            Add("Guide pressed", () => { var b=Dismiss(Help(Bank)); Expect(b.GetComponent<SharedControlAppearance>().CurrentState=="pressed", "Pressed"); Capture("guide-pressed"); ExecuteEvents.Execute(b.gameObject,Pointer(b),ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(b.gameObject,Pointer(b),ExecuteEvents.pointerExitHandler); b.Select(); });
            Add("Guide focus", () => { Capture("guide-focus"); Dismiss(Help(Bank)).GetComponent<SharedControlAppearance>().SetSelected(true); });
            Add("Guide selected", () => { Capture("guide-selected"); Dismiss(Help(Bank)).GetComponent<SharedControlAppearance>().SetSelected(false); Dismiss(Help(Bank)).interactable=false; });
            Add("Guide disabled", () => { Capture("guide-disabled"); Dismiss(Help(Bank)).interactable=true; });
            Add("Dismiss by keyboard", () => { Submit(Dismiss(Help(Bank))); Expect(!Help(Bank).IsVisible,"Guide dismissed"); });
            Add("Tooltip keyboard", () => { Bank.Find("CreditHelp").GetComponent<UnityEngine.UI.Button>().Select(); });
            Add("Tooltip visible", () => { Expect(Bank.Find("CreditTooltip").gameObject.activeSelf,"Keyboard tooltip"); Capture("tooltip-keyboard"); ExecuteEvents.Execute(Bank.Find("CreditHelp").gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.cancelHandler); EventSystem.current.SetSelectedGameObject(null); });
            Add("Tooltip touch", () => Click(Bank.Find("CreditHelp").GetComponent<UnityEngine.UI.Button>(),true));
            Add("Tooltip touch visible", () => { Expect(Bank.Find("CreditTooltip").gameObject.activeSelf,"Touch tooltip"); Capture("tooltip-touch"); Click(Bank.Find("CreditHelp").GetComponent<UnityEngine.UI.Button>(),true); Expect(!Bank.Find("CreditTooltip").gameObject.activeSelf,"Touch toggle closed"); });
            Add("Choose no loan", () => Click(Field<UnityEngine.UI.Button>(UnityEngine.Object.FindObjectsByType<BankCardView>(FindObjectsSortMode.None).Single(x=>x.CreditLine.id=="sem_credito"),"selectButton"),true));
            Add("Continue to equipment", () => { loadingCaptured=false; Click(Field<UnityEngine.UI.Button>(Bank.GetComponent<FinancialScreenView>(),"confirmButton")); });
            Add("Loaded input released", () => { Expect(!Loader.IsBusy && Equipment.GetComponent<CanvasGroup>().interactable,"Input released on success"); Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Initial_Equipment,"Real step six"); Expect(Help(Equipment).IsVisible,"Drag guide"); Capture("equipment-first"); });
            Add("Empty cart", () => Click(Field<UnityEngine.UI.Button>(Equipment,"cartButton")));
            Add("Empty cart action", () => { Expect(Field<GameObject>(Equipment,"cartEmpty").activeSelf && !Field<UnityEngine.UI.Button>(Equipment,"checkoutButton").IsInteractable(),"Empty cart disabled checkout"); Capture("cart-empty"); Click(Field<UnityEngine.UI.Button>(Equipment,"closeCartButton")); });
            Add("Real touch scroll", () => { var s=Field<UnityEngine.UI.ScrollRect>(Equipment,"catalogScroll"); ExecuteEvents.Execute(s.gameObject,new PointerEventData(EventSystem.current){pointerId=4,position=Bounds(s.viewport).center},ExecuteEvents.beginDragHandler); ExecuteEvents.Execute(s.gameObject,new PointerEventData(EventSystem.current){scrollDelta=new Vector2(0,-4)},ExecuteEvents.scrollHandler); Expect(!Help(Equipment).IsVisible,"User gesture hides help"); s.verticalNormalizedPosition=1; });
            Add("Bank revisit", () => Click(Field<UnityEngine.UI.Button>(Equipment,"bankBackButton")));
            Add("Bank guidance stays dismissed", () => { Expect(!Help(Bank).IsVisible,"Revisit remembers comprehension"); Capture("bank-revisit"); Click(Field<UnityEngine.UI.Button>(Bank.GetComponent<FinancialScreenView>(),"confirmButton")); });
            Add("Equipment revisit", () => { Expect(!Help(Equipment).IsVisible,"Equipment help remains dismissed"); Capture("equipment-revisit"); var c=Equipment.GetComponentsInChildren<EquipmentCardView>().First(x=>x.Equipment.category==EquipmentCategory.BASIC); cash=GameSessionState.Current.currentCash-c.Equipment.cost; Click(Field<UnityEngine.UI.Button>(c,"buyButton"),true); Click(Field<UnityEngine.UI.Button>(Equipment,"cartButton")); });
            Add("Purchase real item", () => { Capture("cart-item"); Click(Field<UnityEngine.UI.Button>(Equipment,"checkoutButton")); Expect(GameSessionState.Current.currentCash==cash,"Original price charged once"); Expect(new GameSessionRepository(Db).GetById(sessionId).currentCash==cash,"Purchase persisted"); });
            Add("Team initial focus cue", () => { Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Initial_Team && Help(Team).IsVisible,"Team guide"); Expect(Field<GameObject>(Team,"emptyTeam").activeSelf,"Empty team"); Capture("team-first-empty"); Click(Field<UnityEngine.UI.Button>(Team.GetComponentsInChildren<TeamRoleCardView>().Single(x=>x.Role.id=="attendant"),"addButton"),true); });
            Add("Team saved action", () => { Expect(!Help(Team).IsVisible && TeamSelectionHelper.FromJson(new GameSessionRepository(Db).GetById(sessionId).teamJson).members.Sum(x=>x.quantity)==1,"Hire real role"); Capture("team-hired"); var c=UnityEngine.Object.FindObjectsByType<TeamRoleCardView>(FindObjectsSortMode.None).Single(x=>x.Quantity==1); Submit(Field<UnityEngine.UI.Button>(c,"removeButton")); });
            Add("Empty team remains optional", () => { Expect(Field<GameObject>(Team,"emptyTeam").activeSelf,"Empty team restored"); Capture("team-empty-understood"); Submit(Field<UnityEngine.UI.Button>(Team,"confirmButton")); });
            Add("Hub with warning", () => { Expect(GameManager.Instance.StateMachine.CurrentState==GameState.Management_Hub && FeedbackToast.Current.Kind==FeedbackKind.Warning,"Original empty-team rule"); Capture("toast-warning"); });
            Add("Open equipment from hub", () => { FeedbackToast.Current.Hide(); MenuNavigator.Instance.Open(PanelId.EquipmentStore); });
            Add("Cancel actual load", () => { EquipmentController.BeginCatalogLoad(); Expect(Loader.IsBusy,"Real request pending"); Loader.Cancel(); Expect(!Loader.IsBusy && Equipment.GetComponent<CanvasGroup>().interactable,"Cancel releases input"); });
            Add("Cancelled feedback", () => { Capture("catalog-cancelled"); Click(Field<UnityEngine.UI.Button>(Equipment,"retryCatalogButton")); });
            Add("Retry success", () => { Expect(!Loader.IsBusy && Field<UnityEngine.UI.Button>(Equipment,"confirmButton").IsInteractable(),"Retry succeeds"); Expect(!FeedbackToast.Current.IsVisible,"Retry clears obsolete feedback"); Capture("catalog-retry"); Resources.Load<EquipmentCatalogIndex>("EquipmentCatalogIndex").resourcePaths=new[]{"Equipments/__ui17_missing"}; EquipmentController.BeginCatalogLoad(); });
            Add("Actual resource failure", () => { Expect(!Loader.IsBusy && Equipment.GetComponent<CanvasGroup>().interactable && Field<UnityEngine.UI.Button>(Equipment,"retryCatalogButton").gameObject.activeSelf,"Failure releases input/retry"); Expect(!Field<TMP_Text>(Equipment,"resultText").text.Contains("0"),"Missing is not zero"); Capture("catalog-failed"); Resources.Load<EquipmentCatalogIndex>("EquipmentCatalogIndex").resourcePaths=paths; Click(Field<UnityEngine.UI.Button>(Equipment,"retryCatalogButton"),true); });
            Add("Recovered catalog", () => { Expect(!Loader.IsBusy && Field<UnityEngine.UI.Button>(Equipment,"confirmButton").IsInteractable(),"Recovery"); Expect(!FeedbackToast.Current.IsVisible,"Recovery clears obsolete error"); Capture("catalog-recovered"); EquipmentController.BeginCatalogLoad(); GameManager.Instance.StateMachine.ForceState(GameState.Initial_Team); });
            Add("Disabled loading cannot continue", () => { Expect(!Loader.IsBusy && Equipment.GetComponent<CanvasGroup>().interactable,"Disable releases input"); Expect(!Help(Team).IsVisible,"Saved/revisited team has no cue"); Capture("team-revisit"); GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub); MenuNavigator.Instance.Open(PanelId.EquipmentStore); });
            Add("Global Menu raycast", () => Click(Root.Find("MenuAccess/Button").GetComponent<UnityEngine.UI.Button>(),true));
            Add("Hub return", () => { Expect(MenuNavigator.Instance.Current==PanelId.Menu,"Menu opens hub"); Capture("hub-return"); MenuNavigator.Instance.Open(PanelId.EquipmentStore); });
            Add("Global Back raycast", () => Click(Root.Find("HubHistory/Back").GetComponent<UnityEngine.UI.Button>()));
            Add("Open legacy exit", () => { Expect(MenuNavigator.Instance.Current==PanelId.Menu,"Back restores hub"); Click(Root.Find("ExitUI/ExitButton").GetComponent<UnityEngine.UI.Button>()); });
            Add("Cancel legacy exit", () => { var exit=Root.Find("ExitUI").GetComponent<ExitDialogController>(); Expect(exit.IsOpen,"Exit opened"); Capture("exit-legacy"); Click(Field<UnityEngine.UI.Button>(exit,"cancelButton")); Expect(!exit.IsOpen,"Exit cancelled"); MenuNavigator.Instance.Open(PanelId.MonthlyResults); });
            Add("Monthly result empty", () => { Expect(Root.Find("Panel_MonthlyResult").GetComponent<MonthlyResultView>().DisplayState=="empty","No invented monthly data"); Capture("results-monthly-empty"); MenuNavigator.Instance.Back(); MenuNavigator.Instance.Open(PanelId.QuarterlyResults); });
            Add("Quarterly result empty", () => { Expect(Root.Find("Panel_QuarterlyResult").GetComponent<QuarterlyResultView>().DisplayState=="empty","No invented quarter"); Capture("results-quarterly-empty"); MenuNavigator.Instance.CloseAll(); GameSessionState.Clear(); GameManager.Instance.StateMachine.ForceState(GameState.FinalReport); });
            Add("Final report empty", () => { Expect(Root.Find("Panel_FinalReport").GetComponent<FinalReportView>().DisplayState=="empty","No invented report"); Capture("results-final-empty"); GameManager.Instance.StateMachine.ForceState(GameState.Initial_Team); });
            Add("Unavailable team has no tutorial", () => { Expect(!Help(Team).IsVisible && !Field<GameObject>(Team,"emptyTeam").activeSelf,"Unavailable differs from empty"); Expect(Team.GetComponentsInChildren<TeamRoleCardView>().All(c=>Field<TMP_Text>(c,"quantityText").text=="—"),"Unknown quantities are not zero"); Capture("team-unavailable"); });
        }
        Add("Runtime evidence", () => File.WriteAllText("output/ui-update/UI-17/runtime.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{project=UnityEngine.Application.dataPath,unity=UnityEngine.Application.unityVersion,database=Db.DatabasePath,frame=Time.frameCount,lastCaptureFrame},Newtonsoft.Json.Formatting.Indented)));
        next=EditorApplication.timeSinceStartup+.3; EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }
    private static void Tick()
    {
        if(!EditorApplication.isPlaying || report==null) return;
        if(Time.frameCount<=lastFrame) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        if(Loader.IsBusy)
        {
            if(!loadingCaptured && Time.frameCount>lastFrame+1 && Screen.width.ToString()+"x"+Screen.height==res)
            { try { Capture("catalog-loading"); loadingCaptured=true; } catch(InvalidOperationException) { } }
            if(++waitFrames<600) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        }
        waitFrames=0;
        if(EditorApplication.timeSinceStartup<next) return;
        if(steps.Count==0) { Finish(); return; }
        var step=steps.Dequeue(); var check=new Check{name=step.Item1,frame=Time.frameCount};
        try { step.Item2(); check.passed=true; }
        catch(Exception e) { check.detail=e.ToString(); report.failures++; steps.Clear(); Debug.LogError("[UI-17 QA] "+step.Item1+": "+e.Message); }
        report.checks.Add(check); lastFrame=Time.frameCount; EditorApplication.QueuePlayerLoopUpdate(); EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint(); next=EditorApplication.timeSinceStartup+.3;
    }
    private static void Finish()
    {
        Resources.Load<EquipmentCatalogIndex>("EquipmentCatalogIndex").resourcePaths=paths;
        report.completed=report.failures==0; EditorApplication.update-=Tick; File.WriteAllText("output/ui-update/UI-17/playmode.json",JsonUtility.ToJson(report,true));
    }
}
