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

public static class UI15FinalReportValidation
{
    [Serializable] private class Check { public string name, detail; public bool passed; public int frame; }
    [Serializable] private class Report { public bool completed; public int failures; public List<Check> checks = new(); }
    private static readonly Queue<(string, Action)> steps = new();
    private static readonly List<object> captures = new();
    private static Report report; private static double next; private static int lastFrame, retries;
    private static string sessionId, snapshot; private static SQLite4Unity3d.SQLiteConnection isolatedDb;
    private static Transform Root => GameObject.Find("Canvas").transform;
    private static FinalReportView View => Root.Find("Panel_FinalReport").GetComponent<FinalReportView>();
    private static FinalReportController Controller => View.GetComponent<FinalReportController>();
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
        GameSessionState.Clear(); var s = new GameSessionService("ui15-" + Guid.NewGuid(), "test-professor").CreateNewSession();
        var restaurant = Resources.LoadAll<RestaurantData>("Restaurants").Single(r => r.type == RestaurantType.PODRAO);
        s.companyName = "Restaurante da campanha de validação"; s.studentName = "Participante de validação"; s.studentRA = "123456";
        s.restaurantType = restaurant.type; s.locationZone = LocationZone.Comercio; s.targetSegment = Segment.LOW; s.currentCash = s.initialCapital = 65000; s.reputationScore = 50;
        s.menuPricingJson = MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(restaurant.products));
        s.teamJson = TeamSelectionHelper.ToJson(new TeamSelectionData { members = restaurant.requiredRoles.Select(r => new TeamSelectionItem { roleId = r.roleId, quantity = r.quantity }).ToList() });
        s.equipmentJson = EquipmentSelectionHelper.ToJson(new EquipmentSelectionData { equipmentIds = restaurant.requiredEquipmentIds.ToList() });
        Expect(GameSessionState.Persist(), "Persist fixture"); sessionId = s.sessionId; return s;
    }
    private static void Complete(bool negative = false)
    {
        var s = Fixture();
        if (negative) { s.teamJson = "{}"; s.currentCash = 1000; GameSessionState.Persist(); }
        for (int month = 1; month <= 3; month++)
        {
            if (!negative)
            {
                var forecast = MonthlySimulationEngine.Calculate(MonthlySimulationCatalog.Load(GameSessionState.Current));
                var item = Resources.LoadAll<EventData>("Events").First();
                new SessionEventHistoryRepository(Db).RecordFromOption(sessionId, month, 6, item, new EventOption { id = "ui15-fixture", title = "Escolha da fixture isolada", description = "Snapshot exclusivo de validação, sem origem em previews.", effects = new EventEffectData { cashDelta = Mathf.Abs(forecast.netResult) + 5000 * month } });
            }
            if (GameSessionState.Current.status == GameSessionStatus.IN_PROGRESS) Expect(new RoundService().ProcessRound(month) != null, "Actual settlement");
        }
        Expect(GameSessionState.Current.status == (negative ? GameSessionStatus.BANKRUPT : GameSessionStatus.COMPLETED), "Final status");
        GameManager.Instance.StateMachine.ForceState(GameState.Round_Summary);
    }
    private static void Snapshot() => snapshot = Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current);
    private static void Parity(int count = 3)
    {
        var saved = new GameSessionRepository(Db).GetById(sessionId); var rows = new RoundResultRepository(Db).GetBySessionId(sessionId);
        var expected = FinalReportPresentation.Build(saved, rows, new SessionEventHistoryRepository(Db).GetBySession(sessionId));
        Expect(Controller.Report != null && Controller.Report.Quarter.Count == count, "Saved campaign only");
        Expect(Field<TMP_Text>(View, "net").text == expected.Net && Field<TMP_Text>(View, "cash").text == expected.Cash, "KPIs match saved results");
        for (int i = 0; i < 9; i++) Expect(View.Bmc[i].text.text == expected.Bmc[i].Text && View.Bmc[i].source.text == expected.Bmc[i].Source, "BMC mapping " + i);
        for (int i = 0; i < 4; i++) Expect(View.Reflection[i].text.text == expected.Reflection[i].Text, "Reflection mapping " + i);
        Expect(Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current) == snapshot, "Read-only session"); Expect(rows.Count == count, "No repeated settlement");
    }
    private static Rect Bounds(RectTransform r) { var c = new Vector3[4]; r.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
    private static PointerEventData Pointer(UnityEngine.UI.Button b) => new(EventSystem.current) { pointerId = -1, button = PointerEventData.InputButton.Left, position = Bounds((RectTransform)b.transform).center };
    private static void Click(UnityEngine.UI.Button b)
    {
        Canvas.ForceUpdateCanvases(); Expect(b.IsActive() && b.IsInteractable(), "Unavailable " + b.name); var p = Pointer(b); var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(p, hits);
        Expect(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == b, "Blocked " + b.name + " by " + (hits.Count > 0 ? hits[0].gameObject.name : "nothing"));
        ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerClickHandler);
    }
    private static void Fits()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var t in View.GetComponentsInChildren<TMP_Text>()) { t.ForceMeshUpdate(); Expect(t.font.name == "InterTight-Variable" && !t.enableAutoSizing, "Typography " + t.name); Expect(!t.isTextOverflowing, "Overflow " + t.name + ": " + t.text); }
        Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One EventSystem"); Expect(!Root.Find("MenuAccess/Button").gameObject.activeInHierarchy, "Menu hidden outside hub");
        var controls = View.GetComponentsInChildren<UnityEngine.UI.Button>().Where(b => b.IsInteractable()).ToList(); controls.Add(Root.Find("ExitUI/ExitButton").GetComponent<UnityEngine.UI.Button>());
        foreach (var b in controls)
        {
            var r = Bounds((RectTransform)b.transform); var scroll = b.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if (scroll != null && !Bounds(scroll.viewport).Contains(r.center)) continue;
            Expect(r.xMin >= -.5 && r.yMin >= -.5 && r.xMax <= Screen.width + .5 && r.yMax <= Screen.height + .5, "Offscreen " + b.name);
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(Pointer(b), hits); Expect(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == b, "Control covered " + b.name);
        }
        if (View.Tab == 2 && View.DisplayState == "loaded")
        { var character = View.transform.Find("LoadedBody/Reflection/GuidingCharacter").GetComponent<UnityEngine.UI.Image>(); Expect(character.preserveAspect && character.color == Color.white && !character.raycastTarget, "Guiding layer intact"); Expect(!Bounds(character.rectTransform).Overlaps(Bounds(View.CurrentScroll.viewport)), "Guiding art overlaps text"); }
    }
    private static void Capture(string name, bool final = true)
    {
        if (final) Fits(); string path = UI01Validation.Capture("../UI-15/" + name); using var hash = System.Security.Cryptography.SHA256.Create();
        captures.Add(new { name, path, frame = Time.frameCount, width = Screen.width, height = Screen.height, sha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant() });
        File.WriteAllText("output/ui-update/UI-15/capture-frames.json", Newtonsoft.Json.JsonConvert.SerializeObject(captures, Newtonsoft.Json.Formatting.Indented));
    }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage(); Expect(EditorApplication.isPlaying, "Use Play Mode"); Directory.CreateDirectory("output/ui-update/UI-15");
        report = new Report(); steps.Clear(); captures.Clear(); lastFrame = -1; retries = 0; isolatedDb = Db;
        foreach (var resolution in new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768), new Vector2Int(1280, 720) })
        {
            var size = resolution; string res = size.x + "x" + size.y;
            Add("Set viewport " + res, () => UI01Validation.SetGameView(size.x, size.y)); Add("Settle completed campaign " + res, () => Complete());
            Add("Summary waits before final " + res, () => { Expect(GameManager.Instance.StateMachine.CurrentState == GameState.Round_Summary && Monthly.gameObject.activeInHierarchy, "Summary remains readable"); Capture("awaiting-summary-" + res, false); Click(Monthly.ContinueButton); Snapshot(); });
            Add("Conclusion saved KPIs " + res, () => { Expect(Screen.width == size.x && Screen.height == size.y, "Rendered resolution"); Expect(GameManager.Instance.StateMachine.CurrentState == GameState.FinalReport && View.gameObject.activeInHierarchy, "Final linked"); View.ShowTab(0); View.CurrentScroll.verticalNormalizedPosition = 1; Parity(); Capture("conclusion-" + res); });
            Add("Conclusion bottom " + res, () => View.CurrentScroll.verticalNormalizedPosition = 0); Add("Conclusion links " + res, () => Capture("conclusion-bottom-" + res));
            for (int m = 1; m <= 3; m++)
            {
                int month = m; Add("Open saved month " + m + " " + res, () => Click(View.MonthButtons[month - 1]));
                Add("Month parity " + m + " " + res, () => { Expect(Monthly.GetComponent<MonthlyResultController>().Result.round == month, "Exact saved month"); Expect(Monthly.ContinueButton.GetComponentInChildren<TMP_Text>().text == "Voltar ao relatório", "Action label"); Capture("month-" + month + "-" + res, false); });
                if (m == 2) { Add("Nested quarterly " + res, () => Click(Monthly.QuarterlyButton)); Add("Nested return " + res, () => { Capture("nested-quarterly-" + res, false); Click(Quarterly.BackButton); }); Add("Nested month retained " + res, () => Expect(Monthly.GetComponent<MonthlyResultController>().Result.round == month, "Requested month retained")); }
                Add("Return from month " + m + " " + res, () => Click(Monthly.ContinueButton));
                Add("Restore source focus " + m + " " + res, () => { Parity(); Expect(EventSystem.current.currentSelectedGameObject == View.MonthButtons[month - 1].gameObject, "Month source focus"); });
            }
            Add("Open direct quarter " + res, () => Click(View.QuarterlyButton)); Add("Quarter returns final " + res, () => { Capture("quarterly-" + res, false); Click(Quarterly.BackButton); });
            Add("Quarter focus " + res, () => { Expect(EventSystem.current.currentSelectedGameObject == View.QuarterlyButton.gameObject, "Quarter source focus"); Click(View.Tabs[1]); });
            foreach (float value in new[] { 1f, .5f, 0f }) { float scroll = value; Add("BMC scroll " + value + " " + res, () => View.CurrentScroll.verticalNormalizedPosition = scroll); Add("BMC native " + value + " " + res, () => { Parity(); Capture("bmc-" + scroll.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "-" + res); }); }
            Add("Reflection tab " + res, () => Click(View.Tabs[2]));
            foreach (float value in new[] { 1f, .66f, .33f, 0f }) { float scroll = value; Add("Reflection scroll " + value + " " + res, () => View.CurrentScroll.verticalNormalizedPosition = scroll); Add("Reflection native " + value + " " + res, () => { Parity(); Capture("reflection-" + scroll.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "-" + res); }); }
            Add("Wheel and keyboard " + res, () => { View.CurrentScroll.verticalNormalizedPosition = 1; ExecuteEvents.Execute(View.CurrentScroll.gameObject, new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -5) }, ExecuteEvents.scrollHandler); Expect(View.CurrentScroll.verticalNormalizedPosition < 1, "Wheel"); View.BackButton.Select(); ExecuteEvents.Execute(View.BackButton.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler); Expect(View.Tab == 0, "Keyboard conclusion return"); });
            Add("Reload completed campaign " + res, () => Prepare()); Add("Recovered report " + res, () => { Parity(); Expect(GameManager.Instance.StateMachine.CurrentState == GameState.FinalReport, "Resume final"); Capture("reload-" + res); });
            Add("Exit consultation " + res, () => Click(Root.Find("ExitUI/ExitButton").GetComponent<UnityEngine.UI.Button>()));
            Add("Cancel exit " + res, () => { var exit = Root.Find("ExitUI").GetComponent<ExitDialogController>(); Expect(exit.IsOpen, "Exit dialog"); Capture("exit-" + res, false); Click(Field<UnityEngine.UI.Button>(exit, "cancelButton")); });
            Add("Open new game confirmation " + res, () => Click(View.NewGameButton));
            Add("Cancel preserves report " + res, () => { Capture("new-game-confirmation-" + res, false); Click(View.CancelNewGame); Parity(); });
            Add("Prepare normal state " + res, () => { EventSystem.current.SetSelectedGameObject(null); ExecuteEvents.Execute(View.NewGameButton.gameObject, Pointer(View.NewGameButton), ExecuteEvents.pointerExitHandler); });
            Add("Normal " + res, () => { Expect(View.NewGameButton.GetComponent<SharedControlAppearance>().CurrentState == "normal", "Normal"); Capture("normal-" + res); ExecuteEvents.Execute(View.NewGameButton.gameObject, Pointer(View.NewGameButton), ExecuteEvents.pointerEnterHandler); });
            Add("Hover " + res, () => { Expect(View.NewGameButton.GetComponent<SharedControlAppearance>().CurrentState == "hover", "Hover"); Capture("hover-" + res); ExecuteEvents.Execute(View.NewGameButton.gameObject, Pointer(View.NewGameButton), ExecuteEvents.pointerDownHandler); });
            Add("Pressed " + res, () => { Expect(View.NewGameButton.GetComponent<SharedControlAppearance>().CurrentState == "pressed", "Pressed"); Capture("pressed-" + res); ExecuteEvents.Execute(View.NewGameButton.gameObject, Pointer(View.NewGameButton), ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(View.NewGameButton.gameObject, Pointer(View.NewGameButton), ExecuteEvents.pointerExitHandler); View.NewGameButton.Select(); });
            Add("Focus " + res, () => { Capture("focus-" + res); View.NewGameButton.GetComponent<SharedControlAppearance>().SetSelected(true); });
            Add("Selected " + res, () => { Capture("selected-" + res); View.NewGameButton.GetComponent<SharedControlAppearance>().SetSelected(false); View.NewGameButton.interactable = false; });
            Add("Disabled " + res, () => { Capture("disabled-" + res); View.NewGameButton.interactable = true; });
            Add("Missing middle month " + res, () => { Db.Delete(new RoundResultRepository(Db).GetBySessionAndRound(sessionId, 2)); Controller.Refresh(); });
            Add("Missing is explicit " + res, () => { Parity(2); Expect(!View.MonthButtons[1].IsInteractable(), "Missing month disabled"); Capture("partial-" + res); });
            Add("No saved months " + res, () => { new RoundResultRepository(Db).DeleteBySessionId(sessionId); Controller.Refresh(); });
            Add("No fictional results " + res, () => { Parity(0); Expect(View.MonthButtons.All(b => !b.IsInteractable()), "Absent months disabled"); Capture("no-months-" + res); });
            Add("Database unavailable " + res, () => { typeof(DatabaseService).GetProperty("Connection").SetValue(DatabaseInitializer.DatabaseService, null); Controller.Refresh(); });
            Add("Error and retry " + res, () => { Expect(View.DisplayState == "error", "Error state"); typeof(DatabaseService).GetProperty("Connection").SetValue(DatabaseInitializer.DatabaseService, isolatedDb); Capture("error-" + res); Click(View.RetryButton); Expect(View.DisplayState == "loaded", "Retry"); });
            Add("Absent completed campaign " + res, () => { GameSessionState.Clear(); Controller.Refresh(); }); Add("Empty is explicit " + res, () => { Expect(View.DisplayState == "empty", "Empty state"); Capture("empty-" + res); });
            Add("Long stored content " + res, () => { Complete(); Monthly.ContinueButton.onClick.Invoke(); var s = GameSessionState.Current; s.companyName = string.Join(" ", Enumerable.Repeat("Estabelecimento de nome longo", 12)); s.studentName = string.Join(" ", Enumerable.Repeat("Participante", 18)); GameSessionState.Save(); var e = new SessionEventHistoryRepository(Db).GetBySession(sessionId).First(); e.selectedChoiceTitle = string.Join(" ", Enumerable.Repeat("Escolha histórica extensa", 20)); e.selectedChoiceText = string.Join(" ", Enumerable.Repeat("Descrição histórica longa para revisão de quebra de linha e rolagem.", 60)); Db.Update(e); Controller.Refresh(); Snapshot(); });
            Add("Long overview " + res, () => { View.ShowTab(0); Capture("long-conclusion-" + res); Click(View.Tabs[2]); View.CurrentScroll.verticalNormalizedPosition = 1; });
            Add("Long reflection top " + res, () => { Parity(); Capture("long-reflection-top-" + res); View.CurrentScroll.verticalNormalizedPosition = .5f; });
            Add("Long reflection middle " + res, () => { Capture("long-reflection-middle-" + res); View.CurrentScroll.verticalNormalizedPosition = 0; });
            Add("Long reflection bottom " + res, () => Capture("long-reflection-bottom-" + res));
            Add("Restore sourced content " + res, () => { Controller.Refresh(); Click(View.BackButton); });
            Add("Open new game failure case " + res, () => { Click(View.NewGameButton); Db.Execute("CREATE TRIGGER reject_ui15_archive BEFORE UPDATE ON GameSessionEntity BEGIN SELECT RAISE(ABORT, 'test'); END"); });
            Add("New game failure " + res, () => Click(View.ConfirmNewGame));
            Add("Preserved after failure " + res, () => { Expect(GameSessionState.Current.sessionId == sessionId, "Failure preserves campaign"); Capture("new-game-error-" + res, false); Db.Execute("DROP TRIGGER reject_ui15_archive"); Click(View.CancelNewGame); Click(View.NewGameButton); });
            Add("Confirm new game " + res, () => { Click(View.ConfirmNewGame); Expect(GameManager.Instance.StateMachine.CurrentState == GameState.Config_Identification, "New identification"); Expect(!GameSessionState.IsPersisted, "No early INSERT"); Expect(new RoundResultRepository(Db).GetBySessionId(sessionId).Count == 3 && new GameSessionRepository(Db).GetById(sessionId).finalReportArchived, "Archived report preserved"); });
            Add("New campaign capture " + res, () => Capture("new-identification-" + res, false));
            Add("Bankruptcy precedence " + res, () => Complete(true)); Add("Bankruptcy final route " + res, () => { Expect(GameManager.Instance.StateMachine.CurrentState == GameState.Round_Summary, "Bankruptcy summary waits"); Click(Monthly.ContinueButton); Expect(GameManager.Instance.StateMachine.CurrentState == GameState.GameOver_Bankruptcy && !View.gameObject.activeInHierarchy, "Bankruptcy wins"); });
        }
        Add("Runtime evidence", () => File.WriteAllText("output/ui-update/UI-15/runtime.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { project = UnityEngine.Application.dataPath, unity = UnityEngine.Application.unityVersion, database = isolatedDb.DatabasePath, frame = Time.frameCount }, Newtonsoft.Json.Formatting.Indented)));
        next = EditorApplication.timeSinceStartup + .5; EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        if (!EditorApplication.isPlaying || steps.Count == 0) { report.completed = steps.Count == 0 && report.failures == 0; EditorApplication.update -= Tick; File.WriteAllText("output/ui-update/UI-15/playmode.json", JsonUtility.ToJson(report, true)); return; }
        if (Time.frameCount <= lastFrame) { EditorApplication.QueuePlayerLoopUpdate(); EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint(); return; }
        var step = steps.Dequeue(); var check = new Check { name = step.Item1, frame = Time.frameCount };
        try { step.Item2(); check.passed = true; retries = 0; }
        catch (InvalidOperationException e) when (e.Message == "The Game View must render at the evidence resolution." && retries++ < 10) { var rest = steps.ToArray(); steps.Clear(); steps.Enqueue(step); foreach (var r in rest) steps.Enqueue(r); next = EditorApplication.timeSinceStartup + 1; return; }
        catch (Exception e) { check.detail = e.ToString(); report.failures++; steps.Clear(); Debug.LogError("[UI-15 QA] " + check.name + ": " + e.Message); }
        report.checks.Add(check); lastFrame = Time.frameCount; EditorApplication.QueuePlayerLoopUpdate(); EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint(); next = EditorApplication.timeSinceStartup + .35;
    }
}
