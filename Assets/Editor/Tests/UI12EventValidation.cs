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

public static class UI12EventValidation
{
    [Serializable] private class Check { public string name, detail; public bool passed; public int frame; }
    [Serializable] private class Report { public bool completed; public int failures; public List<Check> checks = new(); }
    private static readonly Queue<(string, Action)> steps = new();
    private static readonly List<object> captures = new();
    private static Report report; private static int lastFrame, retries, callbacks, monthCallbacks; private static double next;
    private static EventData longData; private static EventOptionCardView savedPrefab; private static GameSessionEntity savedSession;
    private static GameObject origin; private static string activeId; private static float cash; private static int reputation;
    private static string reportPath, capturePrefix = "";
    private static Transform Root => GameObject.Find("Canvas").transform;
    private static EventScreenView View => Root.Find("Panel_Event").GetComponent<EventScreenView>();
    private static EventScreenController Controller => View.GetComponent<EventScreenController>();
    private static SQLite4Unity3d.SQLiteConnection Db => DatabaseInitializer.DatabaseService.Connection;
    private static T Field<T>(object o, string n) => (T)o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private static void Set(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v);
    private static void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Add(string name, Action action) => steps.Enqueue((name, action));
    private static void CountMonthlySettlement(RoundResultEntity _) => monthCallbacks++;
    private static void Fixture()
    {
        Expect(!Controller.IsAwaitingChoice, "Prior decision still pending"); GameSessionState.Clear();
        var s = new GameSessionService("ui12-" + Guid.NewGuid(), "test-professor").CreateNewSession();
        var restaurant = Resources.LoadAll<RestaurantData>("Restaurants").Single(r => r.type == RestaurantType.PODRAO);
        s.restaurantType = restaurant.type; s.targetSegment = Segment.LOW; s.locationZone = LocationZone.Comercio;
        s.currentCash = s.initialCapital = 65000; s.reputationScore = 50;
        s.menuPricingJson = MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(restaurant.products));
        s.teamJson = TeamSelectionHelper.ToJson(new TeamSelectionData { members = restaurant.requiredRoles.Select(r => new TeamSelectionItem { roleId = r.roleId, quantity = r.quantity }).ToList() });
        s.equipmentJson = EquipmentSelectionHelper.ToJson(new EquipmentSelectionData { equipmentIds = restaurant.requiredEquipmentIds.ToList() });
        Expect(GameSessionState.Persist(), "Fixture persistence"); activeId = s.sessionId; callbacks = 0;
        GameManager.Instance.StateMachine.ForceState(GameState.Round_Sales);
    }
    private static PointerEventData Pointer(UnityEngine.UI.Button button)
    {
        var r = (RectTransform)button.transform; var corners = new Vector3[4]; r.GetWorldCorners(corners);
        var bounds = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        var scroll = button.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        if (scroll != null) { scroll.viewport.GetWorldCorners(corners); bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin, corners[0].x), Mathf.Max(bounds.yMin, corners[0].y), Mathf.Min(bounds.xMax, corners[2].x), Mathf.Min(bounds.yMax, corners[2].y)); }
        return new PointerEventData(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left, position = bounds.center };
    }
    private static void Click(UnityEngine.UI.Button button)
    {
        Canvas.ForceUpdateCanvases(); Expect(button.IsActive() && button.IsInteractable(), "Unavailable " + button.name);
        var p = Pointer(button); var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(p, hits);
        Expect(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button, "Blocked " + button.name + " by " + (hits.Count > 0 ? hits[0].gameObject.name : "nothing"));
        ExecuteEvents.Execute(button.gameObject, p, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(button.gameObject, p, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(button.gameObject, p, ExecuteEvents.pointerClickHandler);
    }
    private static void Fits()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var t in View.GetComponentsInChildren<TMP_Text>())
        { t.ForceMeshUpdate(); Expect(t.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>() != null && !t.enableAutoSizing, "Typography " + t.name); Expect(!t.isTextOverflowing, "Overflow " + t.name + ": " + t.text); }
        foreach (var s in new[] { View.DecisionScroll, View.HistoryScroll })
            Expect(s.content.parent == s.viewport && s.viewport.GetComponent<UnityEngine.UI.Mask>() != null && s.vertical && !s.horizontal, "Scroll hierarchy");
        Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "EventSystem");
        var art = Field<UnityEngine.UI.Image>(View, "illustration");
        if (Field<GameObject>(View, "decisionBody").activeSelf) Expect(art.sprite != null && art.color == Color.white && art.preserveAspect && art.type == UnityEngine.UI.Image.Type.Simple, "Whole neutral illustration");
        Rect Bounds(RectTransform r) { var c = new Vector3[4]; r.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
        var buttons = View.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b => b.GetComponentInParent<UnityEngine.UI.ScrollRect>() == null).ToArray();
        for (int i = 0; i < buttons.Length; i++) for (int j = i + 1; j < buttons.Length; j++)
            Expect(!Bounds((RectTransform)buttons[i].transform).Overlaps(Bounds((RectTransform)buttons[j].transform)), "Footer overlap " + buttons[i].name + "/" + buttons[j].name);
        var globals = new List<UnityEngine.UI.Button>();
        foreach (var path in new[] { "MenuAccess/Button", "HubHistory/Back", "ExitUI/ExitButton" })
        {
            var global = Root.Find(path); if (global == null || !global.gameObject.activeInHierarchy) continue;
            foreach (var b in buttons) Expect(!Bounds((RectTransform)b.transform).Overlaps(Bounds((RectTransform)global)), "Global overlap " + path + "/" + b.name);
            var button = global.GetComponent<UnityEngine.UI.Button>(); globals.Add(button);
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(Pointer(button), hits);
            Expect(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == button, "Global control covered " + path);
        }
        for (int i = 0; i < globals.Count; i++) for (int j = i + 1; j < globals.Count; j++)
            Expect(!Bounds((RectTransform)globals[i].transform).Overlaps(Bounds((RectTransform)globals[j].transform)), "Global controls overlap");
        foreach (var b in buttons) { var r = Bounds((RectTransform)b.transform); Expect(r.xMin >= -.5 && r.yMin >= -.5 && r.xMax <= Screen.width + .5 && r.yMax <= Screen.height + .5, "Button offscreen " + b.name); }
    }
    private static void Capture(string name, bool screen = true)
    {
        name = capturePrefix + name;
        if (screen) Fits(); string path = UI01Validation.Capture(UiValidationContext.CaptureName("UI-12",name)); UiValidationContext.OnCapture?.Invoke(name,path); using var hash = System.Security.Cryptography.SHA256.Create();
        captures.Add(new { name, path, frame = Time.frameCount, width = Screen.width, height = Screen.height, sha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() });
        File.WriteAllText(UiValidationContext.FileFor("UI-12","capture-frames.json"), Newtonsoft.Json.JsonConvert.SerializeObject(captures, Newtonsoft.Json.Formatting.Indented));
    }
    [MenuItem("Tools/Jogos de Empresa/UI-12/Validate events in isolated Play Mode")]
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage(); if (!EditorApplication.isPlaying) throw new InvalidOperationException("Use isolated Play Mode.");
        Directory.CreateDirectory(UiValidationContext.DirectoryFor("UI-12")); reportPath=UiValidationContext.FileFor("UI-12","playmode-events.json");capturePrefix="";report = new Report(); steps.Clear(); captures.Clear(); lastFrame = -1; retries = 0;
        var catalog = Resources.LoadAll<EventData>("Events").OrderBy(e => e.id).ToArray();
        int sizeIndex = 0;
        foreach (var resolution in UiValidationContext.Resolutions)
        {
            var size = resolution; string res = size.x + "x" + size.y; int viewportIndex = sizeIndex++;
            Add("Prepare viewport " + res, () => UI01Validation.SetGameView(size.x, size.y));
            foreach (var data in catalog)
            {
                var e = data; string tag = e.id + "-" + res;
                int optionIndex = viewportIndex % e.options.Length;
                Add("Open actual catalog " + tag, () => { Fixture(); Controller.OpenDecision(e, 1, 16, () => callbacks++); });
                Add("Read all options and illustration " + tag, () =>
                {
                    Expect(Screen.width == size.x && Screen.height == size.y, "Rendered resolution"); Expect(Controller.IsAwaitingChoice && !View.ConfirmButton.interactable && View.Cards.Count == e.options.Length, "Awaiting three options");
                    Expect(Field<TMP_Text>(View, "titleText").text == e.title && Field<TMP_Text>(View, "descriptionText").text == e.description, "Real text");
                    Expect(Field<UnityEngine.UI.Image>(View, "illustration").sprite == View.Illustration(e.id), "ID illustration");
                    for (int i = 0; i < e.options.Length; i++) Expect(Field<TMP_Text>(View.Cards[i], "effectsText").text == EventEffectText.Format(e.options[i].effects), "Real effects");
                    Expect(!Controller.Close() && callbacks == 0 && Db.Table<SessionEventHistoryEntity>().Where(h => h.sessionId == activeId).Count() == 0, "Cannot skip a choice");
                    Capture("catalog-" + tag); View.Cards[optionIndex].Button.Select();
                });
                Add("Choose by raycast " + tag, () => { Click(View.Cards[optionIndex].Button); Expect(Controller.SelectedOption == optionIndex && View.ConfirmButton.interactable, "Select choice"); View.DecisionScroll.verticalNormalizedPosition = 0; EventSystem.current.SetSelectedGameObject(null); });
                Add("All three options at bottom " + tag, () => { Capture("options-" + tag); Click(View.ConfirmButton); });
                Add("Repeated confirmation and read-only reopen " + tag, () =>
                {
                    Controller.Confirm(); var history = new SessionEventHistoryRepository(Db).GetBySession(activeId);
                    Expect(callbacks == 1 && history.Count == 1 && history[0].selectedChoiceId == e.options[optionIndex].id, "Single callback/record");
                    Expect(GameSessionState.Current.currentCash == 65000 && GameSessionState.Current.reputationScore == 50, "Immediate economic mutation");
                    Controller.OpenDecision(e, 1, 16); Expect(!Controller.IsAwaitingChoice && View.Cards.All(c => !c.Button.interactable) && !View.ConfirmButton.gameObject.activeSelf, "Reopened consultation"); Controller.Confirm(); Expect(history.Count == 1, "Duplicate reopen"); Controller.Close();
                });
            }
            var sample = catalog.Single(e => e.id == "bulk_purchase_opportunity");
            Add("Prepare shared states " + res, () => { Fixture(); Controller.OpenDecision(sample, 1, 6); EventSystem.current.SetSelectedGameObject(null); });
            Add("Normal and disabled " + res, () => { Capture("normal-disabled-" + res); var b = View.Cards[0].Button; ExecuteEvents.Execute(b.gameObject, Pointer(b), ExecuteEvents.pointerEnterHandler); });
            Add("Hover " + res, () => { var b = View.Cards[0].Button; Expect(b.GetComponent<SharedControlAppearance>().CurrentState == "hover", "Hover"); Capture("hover-" + res); ExecuteEvents.Execute(b.gameObject, Pointer(b), ExecuteEvents.pointerDownHandler); });
            Add("Pressed " + res, () => { var b = View.Cards[0].Button; Expect(b.GetComponent<SharedControlAppearance>().CurrentState == "pressed", "Pressed"); Capture("pressed-" + res); ExecuteEvents.Execute(b.gameObject, Pointer(b), ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(b.gameObject, Pointer(b), ExecuteEvents.pointerExitHandler); b.Select(); });
            Add("Keyboard focus " + res, () => { var b = View.Cards[0].Button; Expect(b.GetComponent<SharedControlAppearance>().CurrentState == "focus", "Focus"); Capture("focus-" + res); ExecuteEvents.Execute(b.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler); });
            Add("Keyboard submit selects " + res, () => { Expect(Controller.SelectedOption == 0 && View.Cards[0].GetComponent<SharedControlAppearance>().IsSelected, "Submit selection"); Capture("selected-" + res); Click(Field<UnityEngine.UI.Button>(View, "historyButton")); });
            Add("History while decision pending " + res, () => { Expect(Controller.IsAwaitingChoice && !View.ConfirmButton.gameObject.activeSelf, "History cannot finish pending flow"); Capture("history-empty-pending-" + res); Click(Field<UnityEngine.UI.Button>(View, "returnButton")); });
            Add("Return preserves selection " + res, () => { Expect(Controller.SelectedOption == 0 && View.ConfirmButton.interactable, "Return selection"); Click(View.ConfirmButton); Controller.OpenDecision(sample, 1, 6); });
            Add("Read-only choice copy " + res, () => { Expect(Field<TMP_Text>(View, "footerText").text == "Consulta da escolha já registrada." && !View.ConfirmButton.gameObject.activeSelf, "Consultation copy"); Capture("confirmed-" + res); Controller.Close(); });
            Add("Prepare long native texts " + res, () =>
            {
                Fixture(); longData = UnityEngine.Object.Instantiate(sample); longData.title = string.Join(" ", Enumerable.Repeat(sample.title, 4)); longData.description = string.Join(" ", Enumerable.Repeat(sample.description, 8));
                foreach (var option in longData.options) option.description = string.Join(" ", Enumerable.Repeat(option.description, 12));
                Controller.OpenDecision(longData, 1, 18);
            });
            Add("Long text top wraps and clips to viewport " + res, () => { Capture("long-top-" + res); View.Cards[2].Button.Select(); });
            Add("Long text keyboard reveal and bottom " + res, () => { Click(View.Cards[2].Button); View.DecisionScroll.verticalNormalizedPosition = 0; EventSystem.current.SetSelectedGameObject(null); });
            Add("Long text bottom and confirmation " + res, () => { Capture("long-bottom-" + res); Click(View.ConfirmButton); UnityEngine.Object.Destroy(longData); });
            Add("Prepare missing presentation " + res, () => { Fixture(); savedPrefab = Field<EventOptionCardView>(View, "cardPrefab"); Set(View, "cardPrefab", null); Controller.OpenDecision(sample, 1, 6); });
            Add("Missing presentation has actionable close only " + res, () => { Expect(!Controller.IsAwaitingChoice && !View.ConfirmButton.gameObject.activeSelf && !Field<TMP_Text>(View, "footerText").text.Contains("Confirme"), "Presentation error copy"); Capture("reference-error-" + res); Set(View, "cardPrefab", savedPrefab); Controller.Close(); });
            Add("Prepare unavailable session " + res, () => { Fixture(); savedSession = GameSessionState.Current; GameSessionState.Clear(); Controller.OpenDecision(sample, 1, 6); });
            Add("Unavailable session has no fake figures " + res, () => { Expect(!View.ConfirmButton.gameObject.activeSelf && !Field<GameObject>(View, "decisionBody").activeSelf, "Unavailable session"); Capture("session-error-" + res); Controller.Close(); GameSessionState.Set(savedSession); GameSessionState.Persist(); });
            Add("Open history from actual hub action " + res, () => { GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub); });
            Add("Hub layout and action " + res, () => { Capture("hub-actions-" + res, false); var b = Root.Find("Panel_Menu/HubSummary/History").GetComponent<UnityEngine.UI.Button>(); origin = b.gameObject; b.Select(); Click(b); });
            Add("Empty hub history " + res, () => { Capture("history-empty-hub-" + res); Click(Field<UnityEngine.UI.Button>(View, "returnButton")); });
            Add("History returns focus to hub " + res, () => { Expect(MenuNavigator.Instance.Current == PanelId.Menu && EventSystem.current.currentSelectedGameObject == origin, "History focus restore"); });
        }
        Add("Prepare actual monthly selector flow", () => { Fixture(); GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub); monthCallbacks = 0; var settlement = Root.Find("UiNavigation").GetComponent<MonthlySettlementController>(); settlement.MonthSettled -= CountMonthlySettlement; settlement.MonthSettled += CountMonthlySettlement; });
        Add("Close month waits for player before settlement", () => { Click(Root.Find("Panel_Menu/EventActions/CloseMonth").GetComponent<UnityEngine.UI.Button>()); });
        Add("Selector reservations and no financial mutation", () =>
        {
            Expect(GameManager.Instance.StateMachine.CurrentState == GameState.Round_Event && Controller.IsAwaitingChoice, "Wait before settlement");
            Expect(MonthlySettlementController.SettleCurrentMonth() == null, "A pending choice cannot be bypassed by another settlement request");
            Expect(Db.Table<MonthlyEventPlanEntity>().Where(p => p.sessionId == activeId).Count() == 1 && new RoundResultRepository(Db).GetBySessionId(activeId).Count == 0, "Reserved without settlement");
            cash = GameSessionState.Current.currentCash; reputation = GameSessionState.Current.reputationScore; Capture("monthly-await-1280x720");
            Controller.gameObject.SetActive(false); Controller.OpenMonth(() => throw new Exception("Callback was replaced by reopening"));
        });
        for (int i = 0; i < 4; i++)
        {
            int n = i;
            Add("Select monthly event " + n, () => { Expect(Controller.IsAwaitingChoice, "Pending monthly event"); View.Cards[n % 3].Button.Select(); });
            Add("Confirm monthly event " + n, () => { Click(View.Cards[n % 3].Button); Click(View.ConfirmButton); if (n < 3) { Controller.Confirm(); Expect(new RoundResultRepository(Db).GetBySessionId(activeId).Count == 0 && GameSessionState.Current.currentCash == cash && GameSessionState.Current.reputationScore == reputation, "No early settlement"); } });
        }
        Add("Monthly choices settle exactly once and reload", () =>
        {
            var histories = new SessionEventHistoryRepository(Db).GetBySessionAndRound(activeId, 1); var results = new RoundResultRepository(Db).GetBySessionId(activeId);
            Expect(histories.Count == 4 && results.Count == 1 && monthCallbacks == 1 && GameSessionState.Current.currentRound == 2, "One monthly settlement");
            var first = results[0]; var state = GameSessionState.Current; var opening = state.Copy(); opening.currentRound = 1; opening.currentCash = cash; opening.reputationScore = reputation;
            var expected = MonthlySimulationEngine.Calculate(MonthlySimulationCatalog.Load(opening, histories));
            Expect(first.closingCash == expected.closingCash && first.reputationAtEnd == expected.reputationAtEnd, "Event forecast parity");
            float after = state.currentCash; int repAfter = state.reputationScore; Controller.Confirm(); new RoundService().ProcessRound(1);
            Expect(GameSessionState.Current.currentCash == after && GameSessionState.Current.reputationScore == repAfter, "Repeat financial settlement");
            GameSessionState.LoadActiveSession(state.userId); Expect(GameSessionState.Current.sessionId == activeId && GameSessionState.Current.currentRound == 2, "SQLite reload");
            Expect(GameManager.Instance.StateMachine.CurrentState == GameState.Round_Summary, "Monthly result awaits acknowledgement");
            Root.Find("Panel_MonthlyResult").GetComponent<MonthlyResultController>().Continue();
            MenuNavigator.Instance.Open(PanelId.EventHistory);
        });
        Add("Saved real monthly history", () => { Capture("history-month-one-1280x720"); View.HistoryScroll.verticalNormalizedPosition = 0; });
        Add("History scroll bottom", () => { Capture("history-month-one-bottom-1280x720"); Click(Field<UnityEngine.UI.Button>(View, "returnButton")); });
        Add("Runtime evidence", () => File.WriteAllText(UiValidationContext.FileFor("UI-12","runtime.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { project = UnityEngine.Application.dataPath, unity = UnityEngine.Application.unityVersion, frame = Time.frameCount, database = Db.DatabasePath, sessions = Db.Table<GameSessionEntity>().Count(), session = activeId, month = GameSessionState.Current.currentRound, state = GameManager.Instance.StateMachine.CurrentState.ToString(), panel = MenuNavigator.Instance.Current.ToString() }, Newtonsoft.Json.Formatting.Indented)));
        next = EditorApplication.timeSinceStartup + .5; EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    public static void RunSavedScene()
    {
        var previous = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(UiValidationContext.FileFor("UI-12","capture-frames.json")));
        Run(); reportPath=UiValidationContext.FileFor("UI-12","playmode-saved-scene.json");capturePrefix="saved-scene-";
        foreach(var entry in previous) if(!((string)entry["name"]).StartsWith("saved-scene-")) captures.Add(entry);
        bool monthly=false; var pending=steps.ToArray();steps.Clear();
        foreach(var step in pending)
        {
            if(step.Item1.StartsWith("Prepare actual monthly"))monthly=true;
            if(monthly || step.Item1.StartsWith("Prepare viewport") || step.Item1.Contains("freezer_breakdown")
                || step.Item1.StartsWith("Open history from actual hub") || step.Item1.StartsWith("Hub layout and action")
                || step.Item1.StartsWith("Empty hub history") || step.Item1.StartsWith("History returns focus"))steps.Enqueue(step);
        }
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        if (!EditorApplication.isPlaying || steps.Count == 0)
        { report.completed = steps.Count == 0 && report.failures == 0; EditorApplication.update -= Tick; File.WriteAllText(reportPath, JsonUtility.ToJson(report, true)); return; }
        if (Time.frameCount <= lastFrame) { EditorApplication.QueuePlayerLoopUpdate(); EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint(); return; }
        var step = steps.Dequeue(); var check = new Check { name = step.Item1, frame = Time.frameCount };
        try { step.Item2(); check.passed = true; retries = 0; }
        catch (InvalidOperationException e) when (e.Message == "The Game View must render at the evidence resolution." && retries++ < 10)
        { var pending = steps.ToArray(); steps.Clear(); steps.Enqueue(step); foreach (var rest in pending) steps.Enqueue(rest); next = EditorApplication.timeSinceStartup + 1; return; }
        catch (Exception e) { check.detail = e.ToString(); report.failures++; steps.Clear(); Debug.LogError("[UI-12 QA] " + check.name + ": " + e.Message); }
        report.checks.Add(check); lastFrame = Time.frameCount; EditorApplication.QueuePlayerLoopUpdate(); EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint(); next = EditorApplication.timeSinceStartup + .3;
    }
}
