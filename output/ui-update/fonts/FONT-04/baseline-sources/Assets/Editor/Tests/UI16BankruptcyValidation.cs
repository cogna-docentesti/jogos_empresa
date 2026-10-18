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

public static class UI16BankruptcyValidation
{
    [Serializable] private class Check { public string name, detail; public bool passed; public int frame; }
    [Serializable] private class Report { public bool completed; public int failures; public List<Check> checks = new(); }
    private static readonly Queue<(string, Action)> steps = new();
    private static readonly List<object> captures = new();
    private static Report report; private static double next; private static int lastFrame, retries;
    private static string sessionId, snapshot; private static SQLite4Unity3d.SQLiteConnection isolatedDb;
    private static Transform Root => GameObject.Find("Canvas").transform;
    private static BankruptcyView View => Root.Find("Panel_Bankruptcy").GetComponent<BankruptcyView>();
    private static BankruptcyController Controller => View.GetComponent<BankruptcyController>();
    private static MonthlyResultView Monthly => Root.Find("Panel_MonthlyResult").GetComponent<MonthlyResultView>();
    private static QuarterlyResultView Quarterly => Root.Find("Panel_QuarterlyResult").GetComponent<QuarterlyResultView>();
    private static SQLite4Unity3d.SQLiteConnection Db => DatabaseInitializer.DatabaseService.Connection;
    private static void Expect(bool b, string message) { if (!b) throw new InvalidOperationException(message); }
    private static void Add(string name, Action action) => steps.Enqueue((name, action));
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Prepare() => typeof(GameManager).GetMethod("PrepareSession", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(GameManager.Instance, new object[] { GameSessionState.Current.userId, "test-professor" });
    private static GameSessionEntity Fixture()
    {
        MenuNavigator.Instance.CloseAll(); Monthly.gameObject.SetActive(false); Quarterly.gameObject.SetActive(false); View.gameObject.SetActive(false);
        GameSessionState.Clear(); var s = new GameSessionService("ui16-" + Guid.NewGuid(), "test-professor").CreateNewSession();
        var restaurant = Resources.LoadAll<RestaurantData>("Restaurants").Single(r => r.type == RestaurantType.PODRAO);
        s.companyName = "Restaurante da campanha de validação"; s.studentName = "Participante de validação"; s.studentRA = "123456";
        s.restaurantType = restaurant.type; s.locationZone = LocationZone.Comercio; s.targetSegment = Segment.LOW; s.currentCash = s.initialCapital = 65000; s.reputationScore = 50;
        s.menuPricingJson = MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(restaurant.products));
        s.teamJson = TeamSelectionHelper.ToJson(new TeamSelectionData { members = restaurant.requiredRoles.Select(r => new TeamSelectionItem { roleId = r.roleId, quantity = r.quantity }).ToList() });
        s.equipmentJson = EquipmentSelectionHelper.ToJson(new EquipmentSelectionData { equipmentIds = restaurant.requiredEquipmentIds.ToList() });
        Expect(GameSessionState.Persist(), "Persist fixture"); sessionId = s.sessionId; return s;
    }
    private static Rect Bounds(RectTransform r) { var c = new Vector3[4]; r.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
    private static PointerEventData Pointer(UnityEngine.UI.Button b) => new(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left, position = Bounds((RectTransform)b.transform).center };
    private static void Click(UnityEngine.UI.Button b)
    {
        Canvas.ForceUpdateCanvases(); Expect(b.IsActive() && b.IsInteractable(), "Unavailable " + b.name); var p = Pointer(b); var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(p, hits);
        Expect(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == b, "Blocked " + b.name + " by " + (hits.Count > 0 ? hits[0].gameObject.name : "nothing"));
        ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerClickHandler);
    }
    private static void Complete(bool negativeCashOnly = false)
    {
        var s = Fixture(); s.teamJson = "{}"; s.currentCash = negativeCashOnly ? -1000000 : 1000; GameSessionState.Persist();
        for (int month = 1; month <= 3; month++)
        {
            if (negativeCashOnly && month == 2)
            {
                var forecast = MonthlySimulationEngine.Calculate(MonthlySimulationCatalog.Load(GameSessionState.Current));
                new SessionEventHistoryRepository(Db).RecordFromOption(sessionId, month, 6, Resources.LoadAll<EventData>("Events").First(), new EventOption { id = "ui16-fixture", title = "Escolha exclusiva de validação isolada", effects = new EventEffectData { cashDelta = -forecast.netResult } });
            }
            Expect(new RoundService().ProcessRound(month) != null, "Actual settlement");
            if (month < 3) Expect(s.status == GameSessionStatus.IN_PROGRESS, "No early bankruptcy despite negative cash");
        }
        Expect(s.status == (negativeCashOnly ? GameSessionStatus.COMPLETED : GameSessionStatus.BANKRUPT), "Existing ending rule");
        if (negativeCashOnly) Expect(s.currentCash < 0 && s.consecutiveNegativeRounds == 1, "Negative cash alone completes");
        GameManager.Instance.StateMachine.ForceState(GameState.Round_Summary);
    }
    private static void Snapshot() => snapshot = Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current);
    private static void Parity(int count = 3)
    {
        var rows = new RoundResultRepository(Db).GetBySessionId(sessionId);
        var expected = BankruptcyPresentation.Build(new GameSessionRepository(Db).GetById(sessionId), rows);
        Expect(Controller.Report != null && Controller.Report.Quarter.Count == count, "Saved campaign only");
        Expect(Field<TMP_Text>(View, "net").text == expected.Net && Field<TMP_Text>(View, "cash").text == expected.Cash, "KPIs from SQLite");
        Expect(Field<TMP_Text>(View, "reason").text == expected.Reason && Field<TMP_Text>(View, "counter").text == expected.Counter, "Recorded reason and sequence");
        Expect(Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current) == snapshot, "Read-only session"); Expect(rows.Count == count, "No repeated settlement");
    }
    private static void Fits()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var t in View.GetComponentsInChildren<TMP_Text>()) { t.ForceMeshUpdate(); Expect(t.font.name == "InterTight-Variable" && !t.enableAutoSizing, "Typography " + t.name); Expect(!t.isTextOverflowing, "Overflow " + t.name + ": " + t.text); }
        Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One EventSystem");
        Expect(!Root.Find("MenuAccess/Button").gameObject.activeInHierarchy, "Menu hidden outside hub");
        foreach (var n in new[] { "BusinessDifficulty", "ConcernedCharacter" })
        { var i = View.transform.Find(n).GetComponent<UnityEngine.UI.Image>(); Expect(i.sprite != null && i.preserveAspect && i.color == Color.white && !i.raycastTarget, "Independent complete art " + n); Expect(!Bounds(i.rectTransform).Overlaps(Bounds(View.Scroll.viewport)), "Art does not cover data"); }
        var controls = View.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b => b.IsInteractable()).ToList(); controls.Add(Root.Find("ExitUI/ExitButton").GetComponent<UnityEngine.UI.Button>());
        foreach (var b in controls)
        {
            var r = Bounds((RectTransform)b.transform); var scroll = b.GetComponentInParent<UnityEngine.UI.ScrollRect>(); if (scroll != null && !Bounds(scroll.viewport).Contains(r.center)) continue;
            Expect(r.xMin >= -.5 && r.yMin >= -.5 && r.xMax <= Screen.width + .5 && r.yMax <= Screen.height + .5, "Offscreen " + b.name);
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(Pointer(b), hits); Expect(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == b, "Control covered " + b.name);
        }
    }
    private static void Capture(string name, bool final = true)
    {
        if (final) Fits(); string path = UI01Validation.Capture("../UI-16/" + name); using var hash = System.Security.Cryptography.SHA256.Create();
        captures.Add(new { name, path, frame = Time.frameCount, width = Screen.width, height = Screen.height, sha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() });
        File.WriteAllText("output/ui-update/UI-16/capture-frames.json", Newtonsoft.Json.JsonConvert.SerializeObject(captures, Newtonsoft.Json.Formatting.Indented));
    }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage(); Expect(EditorApplication.isPlaying, "Use Play Mode"); Directory.CreateDirectory("output/ui-update/UI-16");
        report = new Report(); steps.Clear(); captures.Clear(); lastFrame = -1; retries = 0; isolatedDb = Db;
        foreach (var resolution in new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768), new Vector2Int(1280, 720) })
        {
            var size = resolution; string res = size.x + "x" + size.y;
            Add("Set viewport " + res, () => UI01Validation.SetGameView(size.x, size.y)); Add("Settle bankrupt campaign " + res, () => Complete());
            Add("Reload before acknowledgement " + res, () => Prepare());
            Add("Summary waits " + res, () => { Expect(GameManager.Instance.StateMachine.CurrentState == GameState.Round_Summary && Monthly.gameObject.activeInHierarchy && !View.gameObject.activeInHierarchy, "Summary remains readable"); Capture("awaiting-summary-" + res, false); Click(Monthly.ContinueButton); Snapshot(); });
            Add("Recorded bankruptcy " + res, () => { Expect(Screen.width == size.x && Screen.height == size.y, "Rendered resolution"); Expect(GameManager.Instance.StateMachine.CurrentState == GameState.GameOver_Bankruptcy && View.gameObject.activeInHierarchy, "Bankruptcy linked"); Parity(); View.Scroll.verticalNormalizedPosition = 1; Capture("bankruptcy-" + res); });
            Add("Scroll to saved months " + res, () => View.Scroll.verticalNormalizedPosition = 0); Add("Saved month rows " + res, () => Capture("bankruptcy-bottom-" + res));
            for (int m = 1; m <= 3; m++)
            {
                int month = m; Add("Open month " + m + " " + res, () => Click(View.MonthButtons[month - 1]));
                Add("Monthly consultation " + m + " " + res, () => { Expect(Monthly.GetComponent<MonthlyResultController>().Result.round == month, "Exact month"); Expect(!Monthly.GetComponent<MonthlyResultController>().IsAwaitingContinue, "Consultation only"); Expect(Monthly.ContinueButton.GetComponentInChildren<TMP_Text>().text == "Voltar ao encerramento", "Return label"); Capture("month-" + month + "-" + res, false); });
                if (m == 2)
                {
                    Add("Nested quarterly " + res, () => Click(Monthly.QuarterlyButton));
                    Add("Nested return " + res, () => { Capture("nested-quarterly-" + res, false); Click(Quarterly.BackButton); });
                    Add("Retained requested month " + res, () => Expect(Monthly.GetComponent<MonthlyResultController>().Result.round == month, "Monthly origin retained"));
                }
                Add("Return from month " + m + " " + res, () => Click(Monthly.ContinueButton));
                Add("Restore source focus " + m + " " + res, () => { Parity(); Expect(EventSystem.current.currentSelectedGameObject == View.MonthButtons[month - 1].gameObject, "Month focus"); });
            }
            Add("Open direct quarterly " + res, () => Click(View.QuarterlyButton)); Add("Quarterly return " + res, () => { Capture("quarterly-" + res, false); Click(Quarterly.BackButton); });
            Add("Direct quarterly focus " + res, () => { Parity(); Expect(EventSystem.current.currentSelectedGameObject == View.QuarterlyButton.gameObject, "Quarter focus"); Click(View.ReviewButton); });
            Add("Last month action " + res, () => { Expect(Monthly.GetComponent<MonthlyResultController>().Result.round == 3, "Last saved month"); Click(Monthly.ContinueButton); });
            Add("Last month return focus " + res, () => { Parity(); Expect(EventSystem.current.currentSelectedGameObject == View.ReviewButton.gameObject, "Last focus"); });
            Add("Wheel and keyboard " + res, () => { View.Scroll.verticalNormalizedPosition = 1; ExecuteEvents.Execute(View.Scroll.gameObject, new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -5) }, ExecuteEvents.scrollHandler); Expect(View.Scroll.verticalNormalizedPosition < 1, "Wheel"); View.NewGameButton.Select(); ExecuteEvents.Execute(View.NewGameButton.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler); });
            Add("Cancel modal focus " + res, () => { Capture("new-game-confirmation-" + res, false); Click(View.CancelNewGame); Parity(); Expect(EventSystem.current.currentSelectedGameObject == View.NewGameButton.gameObject, "Modal focus restored"); });
            Add("Reload after acknowledgement " + res, () => Prepare()); Add("Recovered bankruptcy " + res, () => { Parity(); Expect(GameManager.Instance.StateMachine.CurrentState == GameState.GameOver_Bankruptcy, "Resume bankruptcy"); Capture("reload-" + res); });
            Add("Open exit " + res, () => Click(Root.Find("ExitUI/ExitButton").GetComponent<UnityEngine.UI.Button>()));
            Add("Cancel exit " + res, () => { var exit = Root.Find("ExitUI").GetComponent<ExitDialogController>(); Expect(exit.IsOpen, "Exit dialog opened"); Capture("exit-" + res, false); Click(Field<UnityEngine.UI.Button>(exit, "cancelButton")); });
            Add("Prepare normal " + res, () => { View.Scroll.verticalNormalizedPosition = 1; EventSystem.current.SetSelectedGameObject(null); ExecuteEvents.Execute(View.NewGameButton.gameObject, Pointer(View.NewGameButton), ExecuteEvents.pointerExitHandler); });
            Add("Normal " + res, () => { Capture("normal-" + res); ExecuteEvents.Execute(View.NewGameButton.gameObject, Pointer(View.NewGameButton), ExecuteEvents.pointerEnterHandler); });
            Add("Hover " + res, () => { Expect(View.NewGameButton.GetComponent<SharedControlAppearance>().CurrentState == "hover", "Hover"); Capture("hover-" + res); ExecuteEvents.Execute(View.NewGameButton.gameObject, Pointer(View.NewGameButton), ExecuteEvents.pointerDownHandler); });
            Add("Pressed " + res, () => { Expect(View.NewGameButton.GetComponent<SharedControlAppearance>().CurrentState == "pressed", "Pressed"); Capture("pressed-" + res); ExecuteEvents.Execute(View.NewGameButton.gameObject, Pointer(View.NewGameButton), ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(View.NewGameButton.gameObject, Pointer(View.NewGameButton), ExecuteEvents.pointerExitHandler); View.NewGameButton.Select(); });
            Add("Focus " + res, () => { Capture("focus-" + res); View.NewGameButton.GetComponent<SharedControlAppearance>().SetSelected(true); });
            Add("Selected " + res, () => { Capture("selected-" + res); View.NewGameButton.GetComponent<SharedControlAppearance>().SetSelected(false); View.NewGameButton.interactable = false; });
            Add("Disabled " + res, () => { Capture("disabled-" + res); View.NewGameButton.interactable = true; });
            Add("Missing middle month " + res, () => { Db.Delete(new RoundResultRepository(Db).GetBySessionAndRound(sessionId, 2)); Controller.Refresh(); });
            Add("Missing explicit " + res, () => { Parity(2); Expect(!View.MonthButtons[1].IsInteractable(), "Absent month disabled"); Capture("partial-" + res); View.Scroll.verticalNormalizedPosition = 0; });
            Add("Partial bottom " + res, () => Capture("partial-bottom-" + res));
            Add("No results " + res, () => { new RoundResultRepository(Db).DeleteBySessionId(sessionId); Controller.Refresh(); });
            Add("Absence not zero " + res, () => { Parity(0); Expect(!View.ReviewButton.IsInteractable() && View.MonthButtons.All(b => !b.IsInteractable()), "No invented rows"); View.Scroll.verticalNormalizedPosition = 1; Capture("no-months-" + res); });
            Add("Database unavailable " + res, () => { typeof(DatabaseService).GetProperty("Connection").SetValue(DatabaseInitializer.DatabaseService, null); Controller.Refresh(); });
            Add("Query error " + res, () => { Expect(View.DisplayState == "error", "Error state"); typeof(DatabaseService).GetProperty("Connection").SetValue(DatabaseInitializer.DatabaseService, isolatedDb); Capture("error-" + res); Click(View.RetryButton); Expect(View.DisplayState == "loaded", "Retry"); });
            Add("No current ending " + res, () => { GameSessionState.Clear(); Controller.Refresh(); }); Add("Empty explicit " + res, () => { Expect(View.DisplayState == "empty", "Empty state"); Capture("empty-" + res); });
            Add("Long stored name " + res, () => { Complete(); Monthly.ContinueButton.onClick.Invoke(); GameSessionState.Current.companyName = string.Join(" ", Enumerable.Repeat("Restaurante de nome extenso para revisão da leitura", 20)); GameSessionState.Save(); Controller.Refresh(); View.Scroll.verticalNormalizedPosition = 1; Snapshot(); });
            Add("Long text top " + res, () => { Parity(); Capture("long-top-" + res); View.Scroll.verticalNormalizedPosition = .5f; });
            Add("Long text middle " + res, () => { Capture("long-middle-" + res); View.Scroll.verticalNormalizedPosition = 0; }); Add("Long text bottom " + res, () => Capture("long-bottom-" + res));
            Add("Zero and divergence " + res, () => { var row = new RoundResultRepository(Db).GetBySessionAndRound(sessionId, 3); row.netResult = row.closingCash = 0; Db.Update(row); Controller.Refresh(); View.Scroll.verticalNormalizedPosition = 0; });
            Add("Zero source " + res, () => { Expect(Field<TMP_Text>(View, "cash").text == MonthlyResultPresentation.Money(0), "Stored zero"); Capture("zero-and-divergence-" + res); });
            Add("Restore campaign " + res, () => { Complete(); Monthly.ContinueButton.onClick.Invoke(); Snapshot(); Click(View.NewGameButton); Db.Execute("CREATE TRIGGER reject_ui16_archive BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'test'); END"); });
            Add("New game failure " + res, () => Click(View.ConfirmNewGame));
            Add("Failure retained results " + res, () => { Parity(); Capture("new-game-error-" + res, false); Db.Execute("DROP TRIGGER reject_ui16_archive"); Click(View.CancelNewGame); Click(View.NewGameButton); });
            Add("Confirm new game " + res, () => { Click(View.ConfirmNewGame); Expect(GameManager.Instance.StateMachine.CurrentState == GameState.Config_Identification && !GameSessionState.IsPersisted, "Unpersisted identification"); Expect(new RoundResultRepository(Db).GetBySessionId(sessionId).Count == 3 && new GameSessionRepository(Db).GetById(sessionId).finalReportArchived, "Ending preserved"); });
            Add("New identification " + res, () => Capture("new-identification-" + res, false));
            Add("Negative cash without three losses " + res, () => Complete(true));
            Add("Negative cash summary " + res, () => { Expect(!View.gameObject.activeInHierarchy, "No bankruptcy from balance"); Capture("negative-cash-summary-" + res, false); Click(Monthly.ContinueButton); });
            Add("Negative cash completion " + res, () => { Expect(GameManager.Instance.StateMachine.CurrentState == GameState.FinalReport && !View.gameObject.activeInHierarchy, "Completion with negative cash"); Capture("negative-cash-completed-" + res, false); });
        }
        Add("Runtime evidence", () => File.WriteAllText("output/ui-update/UI-16/runtime.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { project = UnityEngine.Application.dataPath, unity = UnityEngine.Application.unityVersion, database = isolatedDb.DatabasePath, frame = Time.frameCount }, Newtonsoft.Json.Formatting.Indented)));
        next = EditorApplication.timeSinceStartup + .5; EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        if (!EditorApplication.isPlaying || steps.Count == 0) { report.completed = steps.Count == 0 && report.failures == 0; EditorApplication.update -= Tick; File.WriteAllText("output/ui-update/UI-16/playmode.json", JsonUtility.ToJson(report, true)); return; }
        if (Time.frameCount <= lastFrame) { EditorApplication.QueuePlayerLoopUpdate(); EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint(); return; }
        var step = steps.Dequeue(); var check = new Check { name = step.Item1, frame = Time.frameCount };
        try { step.Item2(); check.passed = true; retries = 0; }
        catch (InvalidOperationException e) when (e.Message == "The Game View must render at the evidence resolution." && retries++ < 10) { var rest = steps.ToArray(); steps.Clear(); steps.Enqueue(step); foreach (var r in rest) steps.Enqueue(r); next = EditorApplication.timeSinceStartup + 1; return; }
        catch (Exception e) { check.detail = e.ToString(); report.failures++; steps.Clear(); Debug.LogError("[UI-16 QA] " + check.name + ": " + e.Message); }
        report.checks.Add(check); lastFrame = Time.frameCount; EditorApplication.QueuePlayerLoopUpdate(); EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint(); next = EditorApplication.timeSinceStartup + .35;
    }
}
