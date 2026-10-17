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
using Button = UnityEngine.UI.Button;

/// <summary>Real GameScene campaign/ending routes. Every destructive write requires isolated storage.</summary>
public static class UI19NavigationValidation
{
    [Serializable] private class Check { public string name, detail; public bool passed; public int frame; }
    [Serializable] private class Report { public bool completed; public int failures; public List<Check> checks = new(); }
    private static readonly Queue<(string, Action)> steps = new();
    private static readonly List<object> captures = new(), transitions = new(), inventories = new();
    private static Report report;
    private static int lastFrame = -1, wait, settledCallbacks, continuedCallbacks;
    private static double next;
    private static string res, mode, sessionId, snapshot, planSnapshot;
    private static string evidenceSuffix = "";
    private static SQLite4Unity3d.SQLiteConnection preservedDb;
    private static GameObject focus, hubFocus;
    private static GameStateMachine States => GameManager.Instance.StateMachine;
    private static Transform Root => GameObject.Find("Canvas").transform;
    private static SQLite4Unity3d.SQLiteConnection Db => DatabaseInitializer.DatabaseService.Connection;
    private static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private static T View<T>(string n) => Root.Find(n).GetComponent<T>();
    private static Button B(object o, string n) => Field<Button>(o, n);
    private static MonthlySettlementController Settlement => Root.Find("UiNavigation").GetComponent<MonthlySettlementController>();
    private static EventScreenController Events => View<EventScreenController>("Panel_Event");
    private static MonthlyResultController Monthly => View<MonthlyResultController>("Panel_MonthlyResult");
    private static MonthlyResultView MonthlyView => Monthly.GetComponent<MonthlyResultView>();
    private static QuarterlyResultView QuarterlyView => View<QuarterlyResultView>("Panel_QuarterlyResult");
    private static void Expect(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
    private static void Add(string n, Action action) => steps.Enqueue((n, action));
    private static string EconomicSnapshot() => Newtonsoft.Json.JsonConvert.SerializeObject(new {
        session = GameSessionState.Current,
        results = new RoundResultRepository(Db).GetBySessionId(sessionId),
        events = new SessionEventHistoryRepository(Db).GetBySession(sessionId),
        plans = Db.Table<MonthlyEventPlanEntity>().Where(p => p.sessionId == sessionId).ToList()
    });
    private static void Prepare()
    {
        var s = GameSessionState.Current;
        typeof(GameManager).GetMethod("PrepareSession", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(GameManager.Instance, new object[] { s.userId, s.professorId });
    }
    private static Rect Bounds(RectTransform r)
    { var c = new Vector3[4]; r.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
    private static PointerEventData Pointer(Button b)
    {
        var r = (RectTransform)(b.transform.Find("MarkerTouchTarget") ?? b.transform);
        return new PointerEventData(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center)) };
    }
    private static void Click(Button b)
    {
        Canvas.ForceUpdateCanvases(); Expect(b.IsActive() && b.IsInteractable(), "Unavailable " + b.name);
        var p = Pointer(b); var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(p, hits);
        Expect(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == b,
            "Blocked " + b.name + " by " + (hits.Count > 0 ? hits[0].gameObject.name : "nothing"));
        ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerClickHandler);
    }
    private static GameObject[] ScreenRoots()
    {
        var registry = Root.Find("UiNavigation").GetComponent<PanelRegistry>();
        var listener = UnityEngine.Object.FindFirstObjectByType<UIStateListener>();
        return registry.Bindings.Select(b => b.root).Concat(typeof(UIStateListener).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(GameObject) && f.IsDefined(typeof(SerializeField), true)).Select(f => (GameObject)f.GetValue(listener))).Where(p => p != null).Distinct().ToArray();
    }
    private static void Screen(GameState state, string name)
    {
        Expect(States.CurrentState == state, "State " + States.CurrentState + " expected " + state);
        var visible = ScreenRoots().Where(p => p.activeInHierarchy).ToArray();
        Expect(visible.Length == 1 && visible[0].name == name, "Screen ownership: " + string.Join(",", visible.Select(p => p.name)));
        Expect(UIStateListener.Instance.CurrentPanel == visible[0], "Visibility owner disagrees");
        Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "Exactly one EventSystem");
        Expect(UnityEngine.Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None).Length == 1, "Exactly one GameManager");
        if (state != GameState.Management_Hub) Expect(MenuNavigator.Instance.Current == PanelId.None, "Hub owns a flow screen");
    }
    private static void Inventory()
    {
        var buttons = Root.GetComponentsInChildren<Button>(true).Where(b => !b.transform.IsChildOf(Root.Find("SharedComponentsPreview"))).ToArray();
        var rows = new List<object>();
        foreach (var b in buttons)
        {
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                Expect(b.onClick.GetPersistentTarget(i) != null && !string.IsNullOrEmpty(b.onClick.GetPersistentMethodName(i)), "Broken serialized callback " + b.name);
            var invocations = (System.Collections.IEnumerable)typeof(UnityEngine.Events.UnityEventBase).GetMethod("PrepareInvoke", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(b.onClick, null);
            var callbacks = new List<string>();
            foreach (var invocation in invocations)
            {
                for (var t = invocation.GetType(); t != null; t = t.BaseType)
                {
                    var f = t.GetField("Delegate", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (f?.GetValue(invocation) is not Delegate d) continue;
                    foreach (var call in d.GetInvocationList()) callbacks.Add((call.Target == null ? "static" : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(call.Target).ToString()) + ":" + call.Method);
                }
            }
            Expect(callbacks.Count == callbacks.Distinct().Count(), "Duplicate callback " + b.name);
            rows.Add(new { path = PathOf(b.transform), active = b.gameObject.activeInHierarchy, b.interactable, persistent = b.onClick.GetPersistentEventCount(), callbacks });
        }
        inventories.Add(new { state = States.CurrentState.ToString(), panel = UIStateListener.Instance.CurrentPanel?.name, frame = Time.frameCount, buttons = rows });
    }
    private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    private static void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); Inventory();
        if (name.StartsWith("supp-continue-"))
            Expect(MonthlyView.ContinueButton.GetComponent<SharedControlAppearance>().CurrentState == name.Substring("supp-continue-".Length), "Control state " + name);
        string path = UI01Validation.Capture(UiValidationContext.CaptureName("UI-19", name + "-" + res)); UiValidationContext.OnCapture?.Invoke(name, path);
        using var h = System.Security.Cryptography.SHA256.Create();
        captures.Add(new { name, path, width = UnityEngine.Screen.width, height = UnityEngine.Screen.height, frame = Time.frameCount,
            sha256 = BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() });
        File.WriteAllText(UiValidationContext.FileFor("UI-19", "capture-frames") + evidenceSuffix + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(captures, Newtonsoft.Json.Formatting.Indented));
    }
    private static void NewDraft()
    {
        UI01Validation.RequireIsolatedStorage(); MenuNavigator.Instance.CloseAll(); GameSessionState.Clear(); PlayerSession.Clear();
        new GameSessionService("ui19-" + Guid.NewGuid(), "qa").CreateNewSession(); GameManager.Instance.BeginNewIdentification();
        sessionId = GameSessionState.Current.sessionId; settledCallbacks = continuedCallbacks = 0;
        Settlement.MonthSettled -= OnSettled; Settlement.MonthSettled += OnSettled;
        Monthly.Continued -= OnContinued; Monthly.Continued += OnContinued;
    }
    private static Button HubShortcut(PanelId id)
    {
        var buttons = View<HubMapView>("Panel_Menu").GetComponentsInChildren<PanelNavButton>();
        return buttons.First(b => Field<PanelId>(b, "target") == id && Field<PanelNavButton.NavAction>(b, "action") == PanelNavButton.NavAction.Open).GetComponent<Button>();
    }
    private static void Opening(string campaign)
    {
        Add(campaign + " fresh identification", () => { mode = campaign; NewDraft(); });
        Add(campaign + " valid long identification", () => {
            Screen(GameState.Config_Identification, "Panel_Identification");
            var f = UnityEngine.Object.FindFirstObjectByType<UIIndentification>(); Field<TMP_InputField>(f, "studentNameInput").text = "Participante da campanha integrada de navegação";
            Field<TMP_InputField>(f, "raInput").text = "191919"; Capture(campaign + "-identification"); Click(B(f, "buttonRegister"));
        });
        Add(campaign + " location draft and back", () => { Screen(GameState.Config_Location, "Panel_Location"); Click(B(View<LocationScreenView>("Panel_Location"), "backButton")); });
        Add(campaign + " identification retained", () => { Screen(GameState.Config_Identification, "Panel_Identification"); Click(B(UnityEngine.Object.FindFirstObjectByType<UIIndentification>(), "buttonRegister")); });
        Add(campaign + " select location", () => {
            var v = View<LocationScreenView>("Panel_Location"); ExecuteEvents.Execute(v.Zones.Single(z => z.id == "bank").marker.gameObject, new PointerEventData(EventSystem.current) { pointerId = 4 }, ExecuteEvents.pointerClickHandler);
            Capture(campaign + "-location"); Click(B(v, "confirmButton")); Expect(new GameSessionRepository(Db).GetById(sessionId) == null, "D1 inserted");
        });
        Add(campaign + " restaurant draft", () => { Screen(GameState.Config_Restaurant, "Panel_Restaurant"); var v = View<RestaurantScreenView>("Panel_Restaurant"); Click(B(v, "cardPodrao")); });
        Add(campaign + " restaurant confirm", () => { Capture(campaign + "-restaurant"); Click(B(View<RestaurantScreenView>("Panel_Restaurant"), "confirmButton")); });
        Add(campaign + " menu first commit", () => { Screen(GameState.Config_TargetSegment, "Panel_MenuPricing"); Capture(campaign + "-menu"); Click(B(View<MenuPricingScreenView>("Panel_MenuPricing"), "confirmButton")); Expect(new GameSessionRepository(Db).GetById(sessionId) != null, "D3 did not insert"); });
        Add(campaign + " bank resumes", () => { Screen(GameState.Initial_Capital, "Panel_Financial"); snapshot = EconomicSnapshot(); Prepare(); });
        Add(campaign + " bank reload parity", () => { Screen(GameState.Initial_Capital, "Panel_Financial"); Expect(snapshot == EconomicSnapshot(), "Bank reload wrote economic data"); Capture(campaign + "-bank"); Click(B(UnityEngine.Object.FindObjectsByType<BankCardView>(FindObjectsSortMode.None).Single(c => c.CreditLine.id == "sem_credito"), "selectButton")); Click(B(View<FinancialScreenView>("Panel_Financial"), "confirmButton")); });
        Add(campaign + " equipment resumes", () => { Screen(GameState.Initial_Equipment, "Panel_Equipment"); snapshot = EconomicSnapshot(); Prepare(); });
        Add(campaign + " equipment reload and bank back", () => { Screen(GameState.Initial_Equipment, "Panel_Equipment"); Expect(snapshot == EconomicSnapshot(), "Equipment reload mutated"); Click(B(View<EquipmentScreenView>("Panel_Equipment"), "bankBackButton")); });
        Add(campaign + " bank back persists its origin", () => { Screen(GameState.Initial_Capital, "Panel_Financial"); Prepare(); });
        Add(campaign + " bank choice retained", () => { Screen(GameState.Initial_Capital, "Panel_Financial"); Click(B(View<FinancialScreenView>("Panel_Financial"), "confirmButton")); });
        Add(campaign + " equipment ready", () => { Screen(GameState.Initial_Equipment, "Panel_Equipment"); Capture(campaign + "-equipment"); Click(B(View<EquipmentScreenView>("Panel_Equipment"), "confirmButton")); });
        Add(campaign + " team resumes", () => { Screen(GameState.Initial_Team, "Panel_Team"); snapshot = EconomicSnapshot(); Prepare(); });
        Add(campaign + " team reload and real quantities", () => {
            Screen(GameState.Initial_Team, "Panel_Team"); Expect(snapshot == EconomicSnapshot(), "Team reload mutated");
            if (campaign == "completion")
            {
                var r = Resources.LoadAll<RestaurantData>("Restaurants").Single(x => x.type == RestaurantType.PODRAO);
                var scroll = Field<UnityEngine.UI.ScrollRect>(View<TeamScreenView>("Panel_Team"), "catalogScroll");
                foreach (var requirement in r.requiredRoles)
                {
                    var card = Root.Find("Panel_Team").GetComponentsInChildren<TeamRoleCardView>().Single(c => c.Role.id == requirement.roleId);
                    var plus = B(card, "addButton");
                    // Scroll the actual content to the native control before the raycast.
                    Canvas.ForceUpdateCanvases(); var c = scroll.content; var center = scroll.viewport.InverseTransformPoint(plus.transform.position);
                    c.anchoredPosition += new Vector2(0, -center.y - scroll.viewport.rect.height * .5f); Canvas.ForceUpdateCanvases();
                    for (int i = 0; i < requirement.quantity; i++) Click(plus);
                }
                scroll.verticalNormalizedPosition = 1;
            }
        });
        Add(campaign + " team confirmation", () => { Capture(campaign + "-team"); Click(B(View<TeamScreenView>("Panel_Team"), "confirmButton")); });
        Add(campaign + " hub and persistent setup completion", () => { Screen(GameState.Management_Hub, "Panel_Menu"); Expect(GameSessionState.Current.initialSetupResumeState == (int)GameState.Management_Hub, "Setup completion marker"); snapshot = EconomicSnapshot(); Prepare(); });
        Add(campaign + " hub reload parity", () => { Screen(GameState.Management_Hub, "Panel_Menu"); Expect(snapshot == EconomicSnapshot(), "Hub reload mutated"); Capture(campaign + "-hub"); });
    }
    private static void HubVisits()
    {
        foreach (PanelId id in Enum.GetValues(typeof(PanelId)))
        {
            if (id == PanelId.None || id == PanelId.Menu) continue; var destination = id;
            bool nested = id == PanelId.Location || id == PanelId.Restaurant || id == PanelId.Review;
            Add("Hub opens " + id, () => { snapshot = EconomicSnapshot(); focus = hubFocus = HubShortcut(nested ? PanelId.Establishment : destination).gameObject; Click(focus.GetComponent<Button>()); });
            if (nested)
            {
                Add("Focus establishment shortcut " + id, () => {
                    var content = View<EstablishmentReviewContent>("Panel_Establishment");
                    focus = destination == PanelId.Review ? B(content, "reviewButton").gameObject
                        : Field<EstablishmentReviewContent.DecisionVisual[]>(content, "decisions")[destination == PanelId.Location ? 0 : 1].button.gameObject;
                    focus.GetComponent<Button>().Select();
                });
                Add("Follow establishment shortcut " + id, () => Click(focus.GetComponent<Button>()));
            }
            Add("Hub visit visible " + id, () => {
                var panel = Root.Find("UiNavigation").GetComponent<PanelRegistry>().Resolve(destination); Screen(GameState.Management_Hub, panel.name);
                Expect(MenuNavigator.Instance.Current == destination, "Navigator target"); Capture("hub-" + destination.ToString().ToLowerInvariant());
                if (destination == PanelId.EquipmentStore)
                { Click(B(View<EquipmentScreenView>("Panel_Equipment"), "cartButton")); }
                else Click(Root.Find("HubHistory/Back").GetComponent<Button>());
            });
            if (destination == PanelId.EquipmentStore)
            {
                Add("Modal blocks alternate hub requests", () => { var current = MenuNavigator.Instance.Current; MenuNavigator.Instance.Open(PanelId.Team); MenuNavigator.Instance.Back(); MenuNavigator.Instance.OpenRoot(); Expect(MenuNavigator.Instance.Current == current && SharedModalFocus.Active != null, "Modal lost route"); Capture("hub-cart-modal"); Click(B(View<EquipmentScreenView>("Panel_Equipment"), "closeCartButton")); });
                Add("Modal close restores history", () => { Expect(SharedModalFocus.Active == null, "Stuck modal"); Click(Root.Find("HubHistory/Back").GetComponent<Button>()); });
            }
            if (nested)
                Add("Nested back preserves establishment focus " + id, () => { Screen(GameState.Management_Hub, "Panel_Establishment"); Expect(EventSystem.current.currentSelectedGameObject == focus, "Lost nested origin"); Click(Root.Find("HubHistory/Back").GetComponent<Button>()); });
            Add("Hub back focus and no calculation " + id, () => { Screen(GameState.Management_Hub, "Panel_Menu"); Expect(EventSystem.current.currentSelectedGameObject == hubFocus, "Lost hub focus " + destination); Expect(snapshot == EconomicSnapshot(), "Read changed session " + destination); });
        }
    }
    private static void Month(int month)
    {
        string tag = mode + "-month-" + month;
        Add(tag + " close using actual hub control", () => {
            tag = mode + "-month-" + month;
            Screen(GameState.Management_Hub, "Panel_Menu"); Click(Root.Find("Panel_Menu/EventActions/CloseMonth").GetComponent<Button>());
        });
        Add(tag + " waits before settlement", () => {
            Screen(GameState.Round_Event, "Panel_Event"); Expect(Events.IsAwaitingChoice && GameSessionState.Current.currentRound == month, "Did not wait for choice");
            Expect(new RoundResultRepository(Db).GetBySessionId(sessionId).Count == month - 1, "Early settlement"); Capture(mode + "-month-" + month + "-event");
            planSnapshot = Db.Find<MonthlyEventPlanEntity>(sessionId + ":" + month).entriesJson;
            var v = Events.GetComponent<EventScreenView>(); Click(v.Cards[0].Button); Click(v.ConfirmButton);
        });
        Add(tag + " reload partial event decision", () => {
            Expect(Events.IsAwaitingChoice, "Campaign expects multiple catalog events"); snapshot = EconomicSnapshot(); Prepare();
        });
        Add(tag + " event reservation and choices survive reload", () => {
            Screen(GameState.Round_Event, "Panel_Event"); Expect(Events.IsAwaitingChoice && snapshot == EconomicSnapshot(), "Event resume mutated");
            Expect(Db.Find<MonthlyEventPlanEntity>(sessionId + ":" + month).entriesJson == planSnapshot, "Event redrawn");
        });
        for (int i = 0; i < 3; i++) Add(tag + " confirm pending event " + i, () => {
            Expect(Events.IsAwaitingChoice, "Missing expected remaining event"); var v = Events.GetComponent<EventScreenView>(); Click(v.Cards[0].Button); Click(v.ConfirmButton);
        });
        Add(tag + " summary waits after one settlement", () => {
            Screen(GameState.Round_Summary, "Panel_MonthlyResult"); Expect(!Events.IsAwaitingChoice, "Choice never closed");
            var results = new RoundResultRepository(Db).GetBySessionId(sessionId); Expect(results.Count == month, "Duplicate settlement");
            Expect(GameSessionState.Current.pendingSummaryResultId == Monthly.Result.roundResultId, "Unread marker");
            Expect(settledCallbacks == month, "Settlement callback repeated"); snapshot = EconomicSnapshot();
            Settlement.CloseMonth(); Expect(snapshot == EconomicSnapshot(), "CloseMonth repeated settlement");
            Capture(mode + "-month-" + month + "-summary"); Prepare();
        });
        Add(tag + " summary reload parity and quarterly consultation", () => {
            Screen(GameState.Round_Summary, "Panel_MonthlyResult"); Expect(snapshot == EconomicSnapshot(), "Unread summary recalculated");
            Capture(mode + "-month-" + month + "-resumed"); Click(MonthlyView.QuarterlyButton);
        });
        Add(tag + " quarterly retains wait", () => { Screen(GameState.Round_Summary, "Panel_QuarterlyResult"); Capture(mode + "-month-" + month + "-quarter"); Click(QuarterlyView.BackButton); });
        Add(tag + " return focus and acknowledged continue", () => {
            Screen(GameState.Round_Summary, "Panel_MonthlyResult"); Expect(EventSystem.current.currentSelectedGameObject == MonthlyView.QuarterlyButton.gameObject, "Quarterly return focus");
            Expect(snapshot == EconomicSnapshot(), "Quarterly read recalculated"); Click(MonthlyView.ContinueButton); var saved = EconomicSnapshot();
            MonthlyView.ContinueButton.onClick.Invoke(); Expect(saved == EconomicSnapshot(), "Duplicate Continue mutated");
            Expect(continuedCallbacks == month, "Continue callback count");
        });
        Add(tag + " destination persists", () => {
            var target = month < 3 ? GameState.Management_Hub : mode == "bankruptcy" ? GameState.GameOver_Bankruptcy : GameState.FinalReport;
            Screen(target, month < 3 ? "Panel_Menu" : mode == "bankruptcy" ? "Panel_Bankruptcy" : "Panel_FinalReport");
            Expect(string.IsNullOrEmpty(GameSessionState.Current.pendingSummaryResultId), "Unread marker not cleared"); snapshot = EconomicSnapshot(); Prepare();
        });
        Add(tag + " destination reload parity", () => { Expect(snapshot == EconomicSnapshot(), "Destination reload mutated"); });
    }
    private static void Ending(bool bankrupt)
    {
        Add("Ending report and persistent status " + bankrupt, () => {
            var expected = bankrupt ? GameSessionStatus.BANKRUPT : GameSessionStatus.COMPLETED;
            Expect(GameSessionState.Current.status == expected, "Unexpected campaign outcome: " + GameSessionState.Current.status);
            Screen(bankrupt ? GameState.GameOver_Bankruptcy : GameState.FinalReport, bankrupt ? "Panel_Bankruptcy" : "Panel_FinalReport");
            Expect(GameSessionState.Current.finalSummaryAcknowledged, "Final ack not saved"); Capture(bankrupt ? "ending-bankruptcy" : "ending-completion"); snapshot = EconomicSnapshot();
        });
        Add("Reload ending GameScene " + bankrupt, () => UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene"));
        Add("Ending survives scene reload " + bankrupt, () => {
            Screen(bankrupt ? GameState.GameOver_Bankruptcy : GameState.FinalReport, bankrupt ? "Panel_Bankruptcy" : "Panel_FinalReport");
            Expect(snapshot == EconomicSnapshot(), "Scene reload mutated ending");
            if (bankrupt) View<BankruptcyView>("Panel_Bankruptcy").Scroll.verticalNormalizedPosition = 0;
        });
        for (int i = 1; i <= 3; i++)
        {
            int month = i;
            Add("Ending opens saved month " + bankrupt + " " + i, () => { var v = bankrupt ? (object)View<BankruptcyView>("Panel_Bankruptcy") : View<FinalReportView>("Panel_FinalReport"); Click(bankrupt ? ((BankruptcyView)v).MonthButtons[month - 1] : ((FinalReportView)v).MonthButtons[month - 1]); });
            Add("Ending monthly query " + bankrupt + " " + i, () => { Expect(Monthly.Result.round == month && !Monthly.IsAwaitingContinue, "Wrong final query month"); Capture((bankrupt ? "bankruptcy" : "completion") + "-query-" + month); Click(MonthlyView.QuarterlyButton); });
            Add("Ending nested comparison " + bankrupt + " " + i, () => { Click(QuarterlyView.BackButton); });
            Add("Ending nested return preserves month " + bankrupt + " " + i, () => { Expect(Monthly.Result.round == month, "Nested return lost requested month"); Click(MonthlyView.ContinueButton); });
            Add("Ending restores report and focus " + bankrupt + " " + i, () => { var panel = bankrupt ? "Panel_Bankruptcy" : "Panel_FinalReport"; Screen(bankrupt ? GameState.GameOver_Bankruptcy : GameState.FinalReport, panel); Expect(snapshot == EconomicSnapshot(), "Ending read mutated"); });
        }
        Add("Ending open quarterly " + bankrupt, () => { Click(bankrupt ? View<BankruptcyView>("Panel_Bankruptcy").QuarterlyButton : View<FinalReportView>("Panel_FinalReport").QuarterlyButton); });
        Add("Ending quarterly back " + bankrupt, () => { Capture(bankrupt ? "bankruptcy-quarter" : "completion-quarter"); Click(QuarterlyView.BackButton); });
        Add("Ending new game dialog cancel " + bankrupt, () => { Click(B(bankrupt ? (object)View<BankruptcyView>("Panel_Bankruptcy") : View<FinalReportView>("Panel_FinalReport"), "newGameButton")); });
        Add("Ending modal stays above report " + bankrupt, () => { Expect(SharedModalFocus.Active != null, "No new-game modal"); Capture(bankrupt ? "bankruptcy-new-game" : "completion-new-game"); SharedModalFocus.Active.Cancel(); Expect(snapshot == EconomicSnapshot(), "Cancel mutated"); });
        Add("Ending explicit new game " + bankrupt, () => { Click(B(bankrupt ? (object)View<BankruptcyView>("Panel_Bankruptcy") : View<FinalReportView>("Panel_FinalReport"), "newGameButton")); });
        Add("Ending history preservation " + bankrupt, () => {
            Click(B(bankrupt ? (object)View<BankruptcyView>("Panel_Bankruptcy") : View<FinalReportView>("Panel_FinalReport"), "confirmNewGame"));
            Expect(new GameSessionRepository(Db).GetById(sessionId).finalReportArchived && new RoundResultRepository(Db).GetBySessionId(sessionId).Count == 3, "New game lost history");
            Screen(GameState.Config_Identification, "Panel_Identification"); Expect(!GameSessionState.IsPersisted, "New game inserted before D3");
        });
    }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage(); Expect(EditorApplication.isPlaying, "Use isolated Play Mode");
        evidenceSuffix = ""; report = new(); steps.Clear(); captures.Clear(); transitions.Clear(); inventories.Clear(); lastFrame = -1;
        Directory.CreateDirectory(UiValidationContext.DirectoryFor("UI-19"));
        Settlement.MonthSettled -= OnSettled; Settlement.MonthSettled += OnSettled; Monthly.Continued -= OnContinued; Monthly.Continued += OnContinued;
        States.OnStateChanged -= StateChanged; States.OnStateChanged += StateChanged;
        foreach (var size in UiValidationContext.Resolutions)
        {
            var viewport = size;
            Add("Viewport " + size, () => { res = viewport.x + "x" + viewport.y; UI01Validation.SetGameView(viewport.x, viewport.y); });
            Opening("completion"); HubVisits(); for (int month = 1; month <= 3; month++) Month(month); Ending(false);
            Opening("bankruptcy"); for (int month = 1; month <= 3; month++) Month(month); Ending(true);
        }
        next = EditorApplication.timeSinceStartup + .3; EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void OnSettled(RoundResultEntity r) => settledCallbacks++;
    private static void OnContinued(RoundResultEntity r) => continuedCallbacks++;
    private static void StateChanged(GameState s) => transitions.Add(new { state = s.ToString(), id = (int)s, technical = UIStateListener.IsTechnicalState(s), frame = Time.frameCount, sessionId = GameSessionState.Current?.sessionId });
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || report == null) return;
        if (Time.frameCount <= lastFrame || EditorApplication.timeSinceStartup < next) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        if (UnityEngine.Object.FindObjectsByType<SharedLoading>(FindObjectsSortMode.None).Any(l => l.IsBusy) && ++wait < 900) { EditorApplication.QueuePlayerLoopUpdate(); return; } wait = 0;
        if (steps.Count == 0) { Finish(); return; }
        var step = steps.Dequeue(); var check = new Check { name = res + " " + step.Item1, frame = Time.frameCount };
        try { step.Item2(); check.passed = true; }
        catch (Exception e) { check.detail = e.ToString(); report.failures++; steps.Clear(); Debug.LogError("[UI-19 QA] " + check.name + ": " + e.Message); }
        report.checks.Add(check); lastFrame = Time.frameCount; next = EditorApplication.timeSinceStartup + .22;
        EditorApplication.QueuePlayerLoopUpdate(); EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();
    }
    private static void Finish()
    {
        report.completed = report.failures == 0; EditorApplication.update -= Tick;
        States.OnStateChanged -= StateChanged; Settlement.MonthSettled -= OnSettled; Monthly.Continued -= OnContinued;
        File.WriteAllText(UiValidationContext.FileFor("UI-19", "playmode") + evidenceSuffix + ".json", JsonUtility.ToJson(report, true));
        File.WriteAllText(UiValidationContext.FileFor("UI-19", "state-transitions") + evidenceSuffix + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(transitions, Newtonsoft.Json.Formatting.Indented));
        File.WriteAllText(UiValidationContext.FileFor("UI-19", "runtime-bindings") + evidenceSuffix + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(inventories, Newtonsoft.Json.Formatting.Indented));
        File.WriteAllText(UiValidationContext.FileFor("UI-19", "runtime") + evidenceSuffix + ".json", Newtonsoft.Json.JsonConvert.SerializeObject(new { project = UnityEngine.Application.dataPath, unity = UnityEngine.Application.unityVersion, database = Db?.DatabasePath, key = Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY"), frame = Time.frameCount }, Newtonsoft.Json.Formatting.Indented));
    }

    public static void RunSupplement()
    {
        UI01Validation.RequireIsolatedStorage(); Expect(EditorApplication.isPlaying, "Use Play Mode");
        evidenceSuffix = "-supplemental"; report = new(); steps.Clear(); captures.Clear(); transitions.Clear(); inventories.Clear(); lastFrame = -1;
        foreach (var size in UiValidationContext.Resolutions)
        {
            var viewport = size;
            Add("Supplement viewport", () => { res = viewport.x + "x" + viewport.y; UI01Validation.SetGameView(viewport.x, viewport.y); NewDraft(); });
            Add("Draft exit opens", () => { Click(B(View<ExitDialogController>("ExitUI"), "openButton")); });
            Add("Draft exit cancels without saving", () => { Capture("supp-draft-exit"); Click(B(View<ExitDialogController>("ExitUI"), "cancelButton")); Expect(!GameSessionState.IsPersisted, "Draft saved"); });
            Add("Isolated active fixture", () => {
                var s = GameSessionState.Current; var r = Resources.LoadAll<RestaurantData>("Restaurants").Single(x => x.type == RestaurantType.PODRAO);
                PlayerSession.SaveIdentification("Participante suplementar", "191919", "Empresa suplementar");
                s.restaurantType = r.type; s.locationZone = LocationZone.Comercio; s.targetSegment = Segment.LOW; s.initialCapital = s.currentCash = 65000;
                s.menuPricingJson = MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(r.products));
                s.teamJson = TeamSelectionHelper.ToJson(new TeamSelectionData { members = r.requiredRoles.Select(x => new TeamSelectionItem { roleId = x.roleId, quantity = x.quantity }).ToList() });
                Expect(GameSessionState.Persist(), "Supplement fixture"); States.ForceState(GameState.Management_Hub);
            });
            Add("Empty monthly query", () => { Click(HubShortcut(PanelId.MonthlyResults)); });
            Add("Empty query remains honest", () => { Screen(GameState.Management_Hub, "Panel_MonthlyResult"); Expect(MonthlyView.DisplayState == "empty", "Empty query"); Capture("supp-empty-monthly"); preservedDb = Db; typeof(DatabaseService).GetProperty("Connection").SetValue(DatabaseInitializer.DatabaseService, null); Monthly.Refresh(); });
            Add("Unavailable query and retry", () => { Expect(MonthlyView.DisplayState == "error", "Query error"); typeof(DatabaseService).GetProperty("Connection").SetValue(DatabaseInitializer.DatabaseService, preservedDb); Capture("supp-query-error"); Click(MonthlyView.RetryButton); Expect(MonthlyView.DisplayState == "empty", "Query retry"); Click(MonthlyView.ContinueButton); });
            Add("Open real catalog loading", () => { Click(HubShortcut(PanelId.EquipmentStore)); });
            Add("Loading operation and exit", () => { View<EquipmentScreenController>("Panel_Equipment").BeginCatalogLoad(); Click(B(View<ExitDialogController>("ExitUI"), "openButton")); });
            Add("Modal cancellation restores input", () => { Capture("supp-loading-exit"); Click(B(View<ExitDialogController>("ExitUI"), "cancelButton")); });
            Add("Loading input released", () => { Expect(!View<SharedLoading>("Panel_Equipment").IsBusy && View<CanvasGroup>("Panel_Equipment").interactable, "Loading retained input"); Click(Root.Find("HubHistory/Back").GetComponent<Button>()); });
            Add("Modal takeover opens store", () => Click(HubShortcut(PanelId.EquipmentStore)));
            Add("Modal takeover opens cart", () => Click(B(View<EquipmentScreenView>("Panel_Equipment"), "cartButton")));
            Add("Flow takeover closes hub and modal", () => { Expect(SharedModalFocus.Active != null, "Cart modal missing"); snapshot = EconomicSnapshot(); States.ForceState(GameState.Config_Identification); });
            Add("Flow takeover is exclusive", () => { Screen(GameState.Config_Identification, "Panel_Identification"); Expect(SharedModalFocus.Active == null && snapshot == EconomicSnapshot(), "Takeover changed data or retained modal"); States.ForceState(GameState.Management_Hub); });
            Add("Close real month", () => { Click(Root.Find("Panel_Menu/EventActions/CloseMonth").GetComponent<Button>()); });
            Add("Event exit preserves decisions", () => { Screen(GameState.Round_Event, "Panel_Event"); snapshot = EconomicSnapshot(); Click(B(View<ExitDialogController>("ExitUI"), "openButton")); });
            Add("Exit above pending event", () => { Capture("supp-event-exit"); Click(B(View<ExitDialogController>("ExitUI"), "cancelButton")); Expect(snapshot == EconomicSnapshot() && Events.IsAwaitingChoice, "Event cancel mutated"); Settlement.CloseMonth(); });
            Add("Event history does not release month", () => { Expect(snapshot == EconomicSnapshot(), "Duplicate CloseMonth"); Click(B(Events.GetComponent<EventScreenView>(), "historyButton")); });
            Add("Pending history returns to decision", () => { Capture("supp-pending-history"); Events.Return(); Expect(Events.IsAwaitingChoice && snapshot == EconomicSnapshot(), "History settled"); });
            Add("Reload pending event GameScene", () => UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene"));
            Add("Pending event resumes in new scene", () => { Screen(GameState.Round_Event, "Panel_Event"); Expect(Events.IsAwaitingChoice && snapshot == EconomicSnapshot(), "Scene event resume changed data"); });
            for (int i = 0; i < 4; i++) Add("Supplement event " + i, () => { var v = Events.GetComponent<EventScreenView>(); Click(v.Cards[0].Button); Click(v.ConfirmButton); });
            Add("Reload unread summary GameScene", () => { Screen(GameState.Round_Summary, "Panel_MonthlyResult"); snapshot = EconomicSnapshot(); UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene"); });
            Add("Summary exit opens", () => { Screen(GameState.Round_Summary, "Panel_MonthlyResult"); Expect(snapshot == EconomicSnapshot(), "Scene summary resume recalculated"); Click(B(View<ExitDialogController>("ExitUI"), "openButton")); });
            Add("Exit cancellation preserves unread result", () => { Capture("supp-summary-exit"); Click(B(View<ExitDialogController>("ExitUI"), "cancelButton")); Expect(snapshot == EconomicSnapshot(), "Summary cancel mutated"); });
            Add("Inject acknowledgement failure", () => { Db.Execute("CREATE TRIGGER ui19_ack_fail BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'isolated acknowledgement failure'); END"); Click(MonthlyView.ContinueButton); });
            Add("Failed acknowledgement remains readable", () => { Screen(GameState.Round_Summary, "Panel_MonthlyResult"); Expect(snapshot == EconomicSnapshot() && FeedbackToast.Current.IsVisible, "Acknowledgement failure changed result"); Capture("supp-ack-error"); Db.Execute("DROP TRIGGER ui19_ack_fail"); EventSystem.current.SetSelectedGameObject(null); });
            Add("Continue normal", () => { MonthlyView.ContinueButton.GetComponent<SharedControlAppearance>().SetSelected(false); EventSystem.current.SetSelectedGameObject(null); });
            Add("Continue hover", () => { Capture("supp-continue-normal"); ExecuteEvents.Execute(MonthlyView.ContinueButton.gameObject, Pointer(MonthlyView.ContinueButton), ExecuteEvents.pointerEnterHandler); });
            Add("Continue pressed", () => { Capture("supp-continue-hover"); ExecuteEvents.Execute(MonthlyView.ContinueButton.gameObject, Pointer(MonthlyView.ContinueButton), ExecuteEvents.pointerDownHandler); });
            Add("Continue selected", () => { Capture("supp-continue-pressed"); ExecuteEvents.Execute(MonthlyView.ContinueButton.gameObject, Pointer(MonthlyView.ContinueButton), ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(MonthlyView.ContinueButton.gameObject, Pointer(MonthlyView.ContinueButton), ExecuteEvents.pointerExitHandler); MonthlyView.ContinueButton.GetComponent<SharedControlAppearance>().SetSelected(true); EventSystem.current.SetSelectedGameObject(null); });
            Add("Continue disabled", () => { Capture("supp-continue-selected"); MonthlyView.ContinueButton.GetComponent<SharedControlAppearance>().SetSelected(false); MonthlyView.ContinueButton.interactable = false; });
            Add("Continue focus", () => { Capture("supp-continue-disabled"); MonthlyView.ContinueButton.interactable = true; MonthlyView.ContinueButton.Select(); });
            Add("Retry acknowledges without recalculating", () => { Capture("supp-continue-focus"); var count = new RoundResultRepository(Db).GetBySessionId(sessionId).Count; Click(MonthlyView.ContinueButton); Screen(GameState.Management_Hub, "Panel_Menu"); Expect(count == new RoundResultRepository(Db).GetBySessionId(sessionId).Count, "Retry settled again"); });
            Add("Open establishment after acknowledged summary", () => Click(HubShortcut(PanelId.Establishment)));
            Add("Reset cancel retains history", () => { snapshot = EconomicSnapshot(); Click(B(View<EstablishmentSummaryView>("Panel_Establishment"), "resetButton")); });
            Add("Reset warning cancellation", () => { Capture("supp-reset-cancel"); Click(B(View<EstablishmentSummaryView>("Panel_Establishment"), "cancelResetButton")); Expect(snapshot == EconomicSnapshot(), "Reset cancel mutated"); });
            Add("Explicit reset opens", () => { Click(B(View<EstablishmentSummaryView>("Panel_Establishment"), "resetButton")); });
            Add("Explicit reset deletes only fixture", () => { Click(B(View<EstablishmentSummaryView>("Panel_Establishment"), "confirmResetButton")); Screen(GameState.Config_Identification, "Panel_Identification"); Expect(new GameSessionRepository(Db).GetById(sessionId) == null && new RoundResultRepository(Db).GetBySessionId(sessionId).Count == 0, "Fixture reset incomplete"); Expect(SharedModalFocus.Active == null && MenuNavigator.Instance.Current == PanelId.None, "Reset retained navigation/modal"); });
        }
        next = EditorApplication.timeSinceStartup + .3; EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    /// <summary>Run with -executeMethod after removing the temporary Pipeline package.</summary>
    public static void AuditAndExit()
    {
        EditorApplication.delayCall += () => {
            try
            {
                Expect(UnityEngine.Application.dataPath.Replace('\\', '/') == "D:/Cogna/jogos_empresa/Assets", "Wrong Editor project");
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
                var registry = UnityEngine.Object.FindFirstObjectByType<PanelRegistry>();
                Expect(registry.Bindings.Count == 12 && registry.Bindings.Select(b => b.id).Distinct().Count() == 12, "Registry count/duplicates");
                foreach (var b in registry.Bindings) Expect(b.root != null && registry.Resolve(b.id) == b.root, "Unresolved " + b.id);
                var listener = UnityEngine.Object.FindFirstObjectByType<UIStateListener>();
                var fields = typeof(UIStateListener).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Where(f => f.FieldType == typeof(GameObject) && f.IsDefined(typeof(SerializeField), true)).ToArray();
                foreach (var f in fields.Where(f => !new[] { "roundStartPanel", "roundSalesPanel", "roundCostsPanel" }.Contains(f.Name)))
                    Expect((GameObject)f.GetValue(listener) != null, "Required state panel " + f.Name);
                var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
                Expect(all.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "Missing scene script");
                var prefabs = AssetDatabase.GetDependencies(scene.path, true).Where(p => p.EndsWith(".prefab")).OrderBy(p => p).ToArray();
                foreach (var path in prefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Expect(prefab != null && prefab.GetComponentsInChildren<Transform>(true)
                        .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0, "Missing prefab script " + path);
                    foreach (var button in prefab.GetComponentsInChildren<Button>(true))
                        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                            Expect(button.onClick.GetPersistentTarget(i) != null && !string.IsNullOrEmpty(button.onClick.GetPersistentMethodName(i)), "Broken prefab callback " + path + "/" + button.name);
                }
                int pngs = 0;
                foreach (var path in Directory.GetFiles("Assets/Art/new", "*.png", SearchOption.AllDirectories))
                {
                    var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                    Expect(importer != null && importer.spriteImportMode == SpriteImportMode.Single, "Sprite mode " + path); pngs++;
                }
                Expect(pngs == 277, "Art count");
                var manifest = File.ReadAllText("Packages/manifest.json"); Expect(!manifest.Contains("com.unity.pipeline"), "Temporary tool dependency remains");
                Expect(!scene.isDirty, "Incidental scene edits");
                File.WriteAllText(UiValidationContext.FileFor("UI-19", "final-editor-audit.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new {
                    passed = true, project = UnityEngine.Application.dataPath, unity = UnityEngine.Application.unityVersion,
                    scene = scene.path, dirty = scene.isDirty, missingScripts = 0, registryBindings = 12, requiredStatePanels = 12,
                    technicalNullPanels = 3, pngs, temporaryPipelineRemoved = true,
                    referencedPrefabs = prefabs,
                    fields = fields.Select(f => new { field = f.Name, root = (GameObject)f.GetValue(listener) != null ? ((GameObject)f.GetValue(listener)).name : null })
                }, Newtonsoft.Json.Formatting.Indented));
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                File.WriteAllText(UiValidationContext.FileFor("UI-19", "final-editor-audit.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { passed = false, error = e.ToString() }, Newtonsoft.Json.Formatting.Indented));
                EditorApplication.Exit(1);
            }
        };
    }

    // Standalone regression: a dialog opened before deferred resume must not consume that resume.
    private const string ResumeModalAudit = "Cogna.UI19.ResumeModalAudit";
    private static int modalStage, modalFrame;
    private static float modalCash;
    public static void RunResumeModalAndExit()
    {
        Expect(UnityEngine.Application.dataPath.Replace('\\', '/') == "D:/Cogna/jogos_empresa/Assets", "Wrong project");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        SessionState.SetBool(ResumeModalAudit, true);
        EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod] private static void ResumeModalAfterReload()
    {
        if (!SessionState.GetBool(ResumeModalAudit, false)) return;
        modalStage = 0; modalFrame = -1;
        EditorApplication.update += ResumeModalTick;
    }
    private static void ResumeModalTick()
    {
        if (!EditorApplication.isPlaying) return;
        UnityEngine.Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
        if (Time.frameCount < 4 || Time.frameCount < modalFrame + 3) return;
        try
        {
            UI01Validation.RequireIsolatedStorage();
            if (modalStage == 0)
            {
                // Unity recreates Temp across Editor processes; create this fixture in the isolated live database.
                GameSessionState.Clear(); PlayerSession.Clear();
                var s = new GameSessionService("ui19-modal", "qa").CreateNewSession();
                var restaurant = Resources.LoadAll<RestaurantData>("Restaurants").Single(r => r.type == RestaurantType.PODRAO);
                s.menuPricingJson = MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(restaurant.products));
                s.restaurantType = restaurant.type; s.initialCapital = s.currentCash = 65000;
                PlayerSession.SaveIdentification("Participante do teste modal", "191919", "Empresa do teste modal");
                Expect(GameSessionState.Persist(), "Isolated modal fixture");
                States.ForceState(GameState.Management_Hub);
                modalCash = GameSessionState.Current.currentCash;
                States.ForceState(GameState.Round_Event);
                View<ExitDialogController>("ExitUI").Open();
                modalStage = 1; modalFrame = Time.frameCount; return;
            }
            if (modalStage == 1)
            {
                Expect(SharedModalFocus.Active != null && Field<bool>(Settlement, "resumePending") && !Events.IsAwaitingChoice,
                    "Modal consumed deferred event resume");
                SharedModalFocus.Active.Cancel(); modalStage = 2; modalFrame = Time.frameCount; return;
            }
            Expect(SharedModalFocus.Active == null && Events.IsAwaitingChoice && Events.CurrentEvent != null && Events.gameObject.activeInHierarchy
                && States.CurrentState == GameState.Round_Event && GameSessionState.Current.currentCash == modalCash
                && new RoundResultRepository(Db).GetBySessionId(GameSessionState.Current.sessionId).Count == 0,
                "Cancel did not release the pending event without settlement");
            File.WriteAllText(UiValidationContext.FileFor("UI-19", "resume-modal.json"), JsonUtility.ToJson(new ResumeModalEvidence {
                passed = true, frame = Time.frameCount, month = GameSessionState.Current.currentRound, cash = modalCash,
                pendingSurvivesModal = true, eventOpensAfterCancel = true, results = 0 }, true));
            SessionState.SetBool(ResumeModalAudit, false); EditorApplication.update -= ResumeModalTick; EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            File.WriteAllText(UiValidationContext.FileFor("UI-19", "resume-modal.json"), JsonUtility.ToJson(new ResumeModalEvidence { error = e.ToString() }, true));
            SessionState.SetBool(ResumeModalAudit, false); EditorApplication.update -= ResumeModalTick; EditorApplication.Exit(1);
        }
    }
    [Serializable] private class ResumeModalEvidence
    { public bool passed, pendingSurvivesModal, eventOpensAfterCancel; public int frame, month, results; public float cash; public string error; }
}
