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

/// <summary>Opt-in QA with isolated storage, real controls and captures after rendered frames.</summary>
public static class UI09FinancialValidation
{
    [Serializable] private class Check { public string name, detail; public bool passed; public int frame; }
    [Serializable] private class Report { public bool completed; public int failures; public List<Check> checks = new(); }
    private static readonly Queue<(string, Action)> steps = new(); private static Report report; private static double next;
    private static int managerId, databaseId, sceneHandle; private static string sessionId, snapshot, draft;
    private static GameObject origin; private static CreditLineData longLine; private static string oldName, oldDescription;
    private static BankCardView prefab; private static GameSessionEntity savedSession;
    private static int captureRetries, lastFrame;
    private static T Field<T>(object o, string f) => (T)o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private static void Set(object o, string f, object v) => o.GetType().GetField(f, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v);
    private static void Call(object o, string m) => o.GetType().GetMethod(m, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, null);
    private static Transform CanvasRoot => GameObject.Find("Canvas").transform;
    private static FinancialScreenView View => CanvasRoot.Find("Panel_Financial").GetComponent<FinancialScreenView>();
    private static BankCardView[] Cards => View.GetComponentsInChildren<BankCardView>();
    private static UnityEngine.UI.Button Confirm => Field<UnityEngine.UI.Button>(View, "confirmButton");
    private static UnityEngine.UI.ScrollRect Scroll => Field<UnityEngine.UI.ScrollRect>(View, "cardsScrollRect");
    private static UnityEngine.UI.Button Back => CanvasRoot.Find("HubHistory/Back").GetComponent<UnityEngine.UI.Button>();
    private static UnityEngine.UI.Button EquipmentBack => Field<UnityEngine.UI.Button>(CanvasRoot.Find("Panel_Equipment").GetComponent<EquipmentScreenView>(), "bankBackButton");
    private static BankCardView Card(string id) => Cards.Single(c => c.CreditLine.id == id);
    private static UnityEngine.UI.Button Button(BankCardView c) => Field<UnityEngine.UI.Button>(c, "selectButton");
    private static void Expect(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Add(string name, Action action) => steps.Enqueue((name, action));
    private static void Capture(string name) => UI01Validation.Capture("../UI-09/" + name);
    private static PointerEventData Pointer(UnityEngine.UI.Button b)
    { var r = (RectTransform)(b.transform.Find("MarkerTouchTarget") ?? b.transform); return new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center)), pointerId = -1, button = PointerEventData.InputButton.Left }; }
    private static void Click(UnityEngine.UI.Button b)
    {
        Expect(b.IsActive() && b.IsInteractable(), "Unavailable control " + b.name);
        var p = Pointer(b); var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(p, hits);
        Expect(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() == b, "Blocked raycast " + b.name);
        ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerDownHandler); ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(b.gameObject, p, ExecuteEvents.pointerClickHandler);
    }
    private static void Fits()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var t in View.GetComponentsInChildren<TMP_Text>())
        { t.ForceMeshUpdate(); Expect(t.font.name == "InterTight-Variable" && !t.enableAutoSizing, "Typography " + t.name); Expect(!t.isTextOverflowing, "Overflow " + t.name + ": " + t.text); }
        Expect(Scroll.viewport.GetComponent<UnityEngine.UI.Mask>() != null && Scroll.content.parent == Scroll.viewport && Scroll.vertical && !Scroll.horizontal, "Scroll hierarchy");
        Expect(Scroll.verticalScrollbar.handleRect.sizeDelta == Vector2.zero, "Scrollbar handle dimensions");
        Expect(Cards.Length == 4, "Missing or duplicate options");
        var guidance = Field<TMP_Text>(View, "footerHintText").text;
        Expect(guidance.Contains("Selecione") == (GameSessionState.HasActiveSession && !LoanService.HasLoan(GameSessionState.Current) && GameSessionState.Current.currentRound < 3), "Footer instruction does not match availability");
        foreach (var c in Cards)
        { var i = Field<UnityEngine.UI.Image>(c, "creditLineIcon"); Expect(i.sprite == c.CreditLine.icon && i.color == Color.white && i.preserveAspect && i.type == UnityEngine.UI.Image.Type.Simple, "Artwork binding/tint/contain " + c.CreditLine.id); }
        foreach (var name in new[] { "BankEnvironment", "BankAdvisor" })
        { var i = View.transform.Find(name).GetComponent<UnityEngine.UI.Image>(); Expect(i.sprite != null && i.color == Color.white && i.preserveAspect && i.type == UnityEngine.UI.Image.Type.Simple, "Whole neutral layer " + name); Expect(!i.raycastTarget, "Decorative raycast"); }
        Expect(View.transform.Find("BankEnvironment").GetSiblingIndex() < View.transform.Find("BankAdvisor").GetSiblingIndex(), "Character behind scenario");
        var controls = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Where(b => b.IsInteractable() && b.GetComponentInParent<UnityEngine.UI.ScrollRect>() == null).ToArray();
        for (int i = 0; i < controls.Length; i++) for (int j = i + 1; j < controls.Length; j++)
        {
            var a = RectTransformUtility.CalculateRelativeRectTransformBounds(CanvasRoot, controls[i].transform); var b = RectTransformUtility.CalculateRelativeRectTransformBounds(CanvasRoot, controls[j].transform);
            Expect(Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x) < 1 || Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y) < 1, "Overlapping fixed controls " + controls[i].name + "/" + controls[j].name);
        }
        Expect(UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "Duplicate EventSystem");
    }
    private static void SameSession()
    {
        Expect(GameSessionState.Current.sessionId == sessionId && GameSessionState.IsPersisted && DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count() == 1, "Session/persistence changed");
        Expect(GameManager.Instance.GetInstanceID() == managerId && DatabaseInitializer.DatabaseService.GetInstanceID() == databaseId && UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle == sceneHandle, "Scene/services replaced");
    }
    private static void Fixture(int month, string id = "", float balance = 0)
    { var s = GameSessionState.Current; s.currentRound = month; s.creditLineId = id; s.loanBalance = balance; s.currentCash = 65000; GameSessionState.Save(); }
    private static void ReopenSetup() => GameManager.Instance.StateMachine.ForceState(GameState.Initial_Capital);
    private static void OpenBank()
    {
        var node = UnityEngine.Object.FindObjectsByType<MapMenuNode>(FindObjectsSortMode.None).Single(n => n.Destination == PanelId.Financial);
        origin = node.gameObject; var button = node.GetComponent<UnityEngine.UI.Button>(); button.Select(); Click(button);
    }
    private static void History()
    { Expect(MenuNavigator.Instance.Current == PanelId.Menu && EventSystem.current.currentSelectedGameObject == origin, "Hub history/focus"); SameSession(); }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage(); Expect(EditorApplication.isPlaying && GameManager.Instance.StateMachine.CurrentState == InitialDecisionFlow.Identification, "Start with clean isolated registration");
        UnityEngine.Application.runInBackground = true;
        report = new Report(); steps.Clear(); captureRetries = 0; lastFrame = Time.frameCount + 3; managerId = GameManager.Instance.GetInstanceID(); databaseId = DatabaseInitializer.DatabaseService.GetInstanceID(); sceneHandle = UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        Add("Register using actual identification form", () => { var f = UnityEngine.Object.FindFirstObjectByType<UIIndentification>(); Field<TMP_InputField>(f, "studentNameInput").text = "Aluna UI09"; Field<TMP_InputField>(f, "raInput").text = "123456"; f.Register(); });
        Add("Choose D1 without persistence", () => { var v = UnityEngine.Object.FindFirstObjectByType<LocationScreenView>(); ExecuteEvents.Execute(v.Zones.Single(z => z.id == "bank").marker.gameObject, new PointerEventData(EventSystem.current) { pointerId = 4 }, ExecuteEvents.pointerClickHandler); Click(Field<UnityEngine.UI.Button>(v, "confirmButton")); Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count() == 0, "Inserted in D1"); });
        Add("Choose D2 without persistence", () => { var v = UnityEngine.Object.FindFirstObjectByType<RestaurantScreenView>(); Click(Field<UnityEngine.UI.Button>(v, "cardPodrao")); Click(Field<UnityEngine.UI.Button>(v, "confirmButton")); Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count() == 0, "Inserted in D2"); });
        Add("D3 makes first INSERT through technical review to Banco", () => { Click(Field<UnityEngine.UI.Button>(UnityEngine.Object.FindFirstObjectByType<MenuPricingScreenView>(), "confirmButton")); Expect(GameManager.Instance.StateMachine.CurrentState == GameState.Initial_Capital, "D3 destination"); sessionId = GameSessionState.Current.sessionId; SameSession(); });
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768), new Vector2Int(1280, 720) })
        {
            var s = size; string suffix = s.x + "x" + s.y;
            Add("Viewport and clean setup " + suffix, () => { UI01Validation.SetGameView(s.x, s.y); Fixture(1); ReopenSetup(); EventSystem.current.SetSelectedGameObject(null); });
            Add("Rendered setup, four options and disabled confirmation " + suffix, () => { Fits(); SameSession(); Expect(Screen.width == s.x && Screen.height == s.y && Field<TMP_Text>(View, "stepText").text.Contains("5 DE 7"), "Resolution/step"); Expect(!Confirm.interactable, "Unselected confirmation"); Capture("setup-normal-" + suffix); });
            foreach (var option in new[] { "sem_credito", "microcredito_empresarial", "credito_empresarial_padrao", "credito_de_investimento" })
            {
                string id = option;
                Add("Focus reveals " + id + " " + suffix, () => Button(Card(id)).Select());
                Add("Select actual option " + id + " " + suffix, () => { Click(Button(Card(id))); Expect(Confirm.interactable, "Selected confirmation"); });
                Add("Rendered selection and real values " + id + " " + suffix, () => { Fits(); Expect(Card(id).GetComponent<SharedControlAppearance>().IsSelected && Field<UnityEngine.UI.Image>(Card(id), "selectionMark").enabled, "Selection mark"); Expect(Field<TMP_Text>(Card(id), "interestRateText").text.Contains(id == "sem_credito" ? "Sem juros" : (Card(id).CreditLine.monthlyInterestRate * 100).ToString("0")), "Interest"); Capture(id + "-selected-" + suffix); SameSession(); });
            }
        }
        Add("Prepare shared card hover", () => { Fixture(1); ReopenSetup(); EventSystem.current.SetSelectedGameObject(null); ExecuteEvents.Execute(Button(Card("sem_credito")).gameObject, Pointer(Button(Card("sem_credito"))), ExecuteEvents.pointerEnterHandler); });
        Add("Rendered shared hover outline", () => { var c = Card("sem_credito"); Expect(c.GetComponent<SharedControlAppearance>().CurrentState == "hover" && Field<UnityEngine.UI.Image>(c, "interactionOutline").enabled, "Hover state"); Capture("card-hover-1280x720"); ExecuteEvents.Execute(Button(c).gameObject, Pointer(Button(c)), ExecuteEvents.pointerDownHandler); });
        Add("Rendered shared pressed outline", () => { var c = Card("sem_credito"); Expect(c.GetComponent<SharedControlAppearance>().CurrentState == "pressed", "Pressed state"); Capture("card-pressed-1280x720"); ExecuteEvents.Execute(Button(c).gameObject, Pointer(Button(c)), ExecuteEvents.pointerUpHandler); ExecuteEvents.Execute(Button(c).gameObject, Pointer(Button(c)), ExecuteEvents.pointerExitHandler); Button(Card("credito_de_investimento")).Select(); });
        Add("Keyboard focus reveals last masked row", () => { Expect(Card("credito_de_investimento").GetComponent<SharedControlAppearance>().CurrentState == "focus", "Focus state"); Fits(); Capture("card-focus-1280x720"); ExecuteEvents.Execute(Button(Card("credito_de_investimento")).gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler); });
        Add("Keyboard submit selects without contracting", () => { Expect(Confirm.interactable && GameSessionState.Current.loanBalance == 0, "Submit contracted before confirmation"); Button(Card("sem_credito")).Select(); });
        Add("Choose Sem crédito", () => { Click(Button(Card("sem_credito"))); Click(Confirm); });
        Add("Sem crédito reaches Equipamentos without adding money", () => { Expect(GameManager.Instance.StateMachine.CurrentState == GameState.Initial_Equipment && GameSessionState.Current.creditLineId == "sem_credito" && GameSessionState.Current.loanBalance == 0 && GameSessionState.Current.currentCash == 65000, "Sem crédito changed economy"); Capture("equipment-return-1280x720"); Click(EquipmentBack); });
        Add("Equipment return restores Sem crédito and step five", () => { Fits(); Expect(GameManager.Instance.StateMachine.CurrentState == GameState.Initial_Capital && Card("sem_credito").GetComponent<SharedControlAppearance>().IsSelected, "Equipment return"); Capture("bank-return-no-credit-1280x720"); SameSession(); });
        foreach (string option in new[] { "microcredito_empresarial", "credito_empresarial_padrao", "credito_de_investimento" })
        {
            string id = option;
            Add("Prepare setup contract " + id, () => { Fixture(1); ReopenSetup(); Button(Card(id)).Select(); });
            Add("Confirm real contract " + id, () => { Click(Button(Card(id))); Click(Confirm); });
            Add("Contract persisted once " + id, () => { var line = LoanService.FindLine(id); Expect(GameManager.Instance.StateMachine.CurrentState == GameState.Initial_Equipment && GameSessionState.Current.currentCash == 65000 + line.maxAmount && GameSessionState.Current.loanBalance == line.maxAmount, "Contract amounts"); var persisted = DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Single(); Expect(persisted.creditLineId == id && persisted.currentCash == GameSessionState.Current.currentCash && persisted.loanBalance == line.maxAmount, "Persisted contract"); Expect(!LoanService.TryContract(line), "Duplicate loan"); Click(EquipmentBack); });
            Add("Returning loan is selected and all changes locked " + id, () => { Fits(); Expect(Cards.All(c => !Button(c).interactable) && Card(id).GetComponent<SharedControlAppearance>().IsSelected, "Locked selection"); Capture(id + "-contracted-1280x720"); SameSession(); Click(Confirm); });
            Add("Continuing existing contract never adds money twice " + id, () => { Expect(GameSessionState.Current.currentCash == 65000 + LoanService.FindLine(id).maxAmount, "Cash duplicated"); Click(EquipmentBack); });
        }
        Add("Reach hub through existing setup states", () => { Click(Confirm); GameManager.Instance.StateMachine.TryChangeState(GameState.Initial_Team); GameManager.Instance.StateMachine.TryChangeState(GameState.Management_Hub); });
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768), new Vector2Int(1280, 720) })
        {
            var s = size; var suffix = s.x + "x" + s.y;
            foreach (int round in new[] { 1, 2, 3 })
            {
                int month = round;
                Add("Hub month fixture " + month + " " + suffix, () => { UI01Validation.SetGameView(s.x, s.y); Fixture(month, "sem_credito"); MenuNavigator.Instance.OpenRoot(); });
                Add("Open bank through actual hub marker " + month + " " + suffix, OpenBank);
                Add("Rendered management month " + month + " " + suffix, () => { Fits(); Expect(!Field<TMP_Text>(View, "stepText").text.Contains("PASSO") && Field<TMP_Text>(View, "stepText").text.Contains("MÊS " + month), "Management header"); Expect(Cards.Where(c => c.CreditLine.maxAmount > 0).All(c => Button(c).interactable == (month < 3)), "Real availability"); Expect(Field<TMP_Text>(View, "hintText").text.Contains(LoanService.LastMonthMessage) == (month == 3), "Last month feedback"); Capture("hub-month" + month + "-" + suffix); Click(Back); });
                Add("History and focus restored month " + month + " " + suffix, History);
            }
        }
        Add("Prepare month two hub loan", () => { Fixture(2, "sem_credito"); OpenBank(); });
        Add("Focus month two investment", () => Button(Card("credito_de_investimento")).Select());
        Add("Month two shows two payments and real first payment", () => { var c = Card("credito_de_investimento"); Expect(Field<TMP_Text>(c, "termText").text.Contains("2 parcela"), "Month two term"); Click(Button(c)); Capture("month2-before-contract-1280x720"); Click(Confirm); });
        Add("Month two hub contract preserves navigation and persistence", () => { History(); Expect(GameSessionState.Current.currentCash == 315000 && GameSessionState.Current.loanBalance == 250000, "Month two loan"); Fixture(3, "credito_de_investimento", 125000); OpenBank(); });
        Add("Month three existing contract shows actual balance and payment", () => { Fits(); Expect(Field<TMP_Text>(View, "hintText").text.Contains("Contrato mantido") && !Field<TMP_Text>(View, "hintText").text.Contains(LoanService.LastMonthMessage), "Existing contract falsely unavailable"); Expect(Field<TMP_Text>(View, "summaryText").text.Contains(FinancialScreenView.Money(LoanService.Installment(125000, LoanService.FindLine("credito_de_investimento"), 3))), "Real month three installment"); Capture("month3-existing-contract-1280x720"); Click(Confirm); });
        Add("Month three consultation returns with focus and no new loan", () => { History(); Expect(GameSessionState.Current.loanBalance == 125000 && GameSessionState.Current.currentCash == 65000, "Consultation mutated loan"); Fixture(3, "sem_credito"); OpenBank(); });
        Add("Month three blocks positive selection even if callback invoked", () => { foreach (var c in Cards.Where(c => c.CreditLine.maxAmount > 0)) Button(c).onClick.Invoke(); Expect(Card("sem_credito").GetComponent<SharedControlAppearance>().IsSelected && GameSessionState.Current.loanBalance == 0, "Disabled callback bypass"); Click(Confirm); });
        Add("Month three Sem crédito remains valid and preserves cash", () => { History(); Expect(GameSessionState.Current.currentCash == 65000 && GameSessionState.Current.creditLineId == "sem_credito", "No credit month three"); Fixture(1); OpenBank(); Button(Card("microcredito_empresarial")).Select(); });
        Add("Prepare stale selection between selection and confirm", () => { Click(Button(Card("microcredito_empresarial"))); GameSessionState.Current.currentRound = 3; Click(Confirm); });
        Add("Failed contract gives actual error and never advances or credits cash", () => { Expect(MenuNavigator.Instance.Current == PanelId.Financial && !Confirm.interactable && GameSessionState.Current.loanBalance == 0 && Field<TMP_Text>(View, "hintText").text == LoanService.LastMonthMessage, "Stale contract guard"); Fits(); Capture("contract-error-1280x720"); Click(Back); });
        Add("History after failed contract", () => { History(); Fixture(1); OpenBank(); longLine = LoanService.FindLine("microcredito_empresarial"); oldName = longLine.displayName; oldDescription = longLine.cardDescription; longLine.displayName = "Microcrédito Empresarial com um nome extenso para conferir a leitura completa"; longLine.cardDescription = string.Join(" ", Enumerable.Repeat("Descrição longa de catálogo para conferir quebra de linha e rolagem sem perder valores ou controles.", 4)); Call(View.GetComponent<FinancialScreenController>(), "PopulateCreditLines"); Button(Card(longLine.id)).Select(); });
        Add("Long names and descriptions expand and remain reachable", () => { Fits(); Capture("long-copy-1280x720"); longLine.displayName = oldName; longLine.cardDescription = oldDescription; longLine = null; Click(Back); });
        Add("Prepare nested history from establishment", () => { Fixture(2, "sem_credito"); MenuNavigator.Instance.Open(PanelId.Establishment); });
        Add("Nested bank shortcut preserves originating focus", () => { var content = UnityEngine.Object.FindFirstObjectByType<EstablishmentReviewContent>(); var b = Field<EstablishmentReviewContent.DecisionVisual[]>(content, "decisions")[3].button; b.Select(); snapshot = Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current); draft = MenuPricingHelper.ToJson(PlayerSession.GetMenu()); origin = b.gameObject; });
        Add("Open nested bank and return through history", () => { Click(origin.GetComponent<UnityEngine.UI.Button>()); Expect(MenuNavigator.Instance.Current == PanelId.Financial, "Nested bank"); });
        Add("Confirm no credit returns to originating establishment", () => Click(Confirm));
        Add("Nested history restores panel focus and leaves session/draft unchanged", () => { Expect(MenuNavigator.Instance.Current == PanelId.Establishment && EventSystem.current.currentSelectedGameObject == origin, "Nested focus"); Expect(snapshot == Newtonsoft.Json.JsonConvert.SerializeObject(GameSessionState.Current) && draft == MenuPricingHelper.ToJson(PlayerSession.GetMenu()), "Nested mutation"); SameSession(); MenuNavigator.Instance.OpenRoot(); });
        Add("Prepare missing-reference error", () => { OpenBank(); prefab = Field<BankCardView>(View, "bankCardPrefab"); Set(View, "bankCardPrefab", null); Call(View.GetComponent<FinancialScreenController>(), "PopulateCreditLines"); });
        Add("Missing catalog presentation is explicit with no confirmation", () => { Expect(!Confirm.interactable && Field<TMP_Text>(View, "hintText").text.Contains("Não foi possível"), "Missing reference feedback"); Capture("missing-reference-error-1280x720"); Set(View, "bankCardPrefab", prefab); Call(View.GetComponent<FinancialScreenController>(), "PopulateCreditLines"); Click(Back); });
        Add("Prepare missing-session state", () => { OpenBank(); savedSession = GameSessionState.Current; GameSessionState.Clear(); Call(View.GetComponent<FinancialScreenController>(), "PopulateCreditLines"); });
        Add("Missing session clears monetary values and disables every action", () => { Fits(); Expect(!Confirm.interactable && Cards.All(c => !Button(c).interactable) && Field<TMP_Text>(View, "capitalValueText").text == "Indisponível" && Field<TMP_Text>(View, "cashValueText").text == "Indisponível", "Missing session invented values"); Capture("unavailable-session-1280x720"); GameSessionState.Set(savedSession); GameSessionState.Persist(); GameManager.Instance.StateMachine.ForceState(GameState.Management_Hub); });
        Add("Final runtime and isolation", () => { SameSession(); File.WriteAllText("output/ui-update/UI-09/runtime.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { project = UnityEngine.Application.dataPath, unity = UnityEngine.Application.unityVersion, scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, frame = Time.frameCount, managerId, databaseId, sceneHandle, sessionId, database = DatabaseInitializer.DatabaseService.Connection.DatabasePath, sessionRows = 1, state = GameManager.Instance.StateMachine.CurrentState.ToString(), panel = MenuNavigator.Instance.Current.ToString() }, Newtonsoft.Json.Formatting.Indented)); });
        next = EditorApplication.timeSinceStartup + .5; EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        if (!EditorApplication.isPlaying || steps.Count == 0)
        { report.completed = steps.Count == 0 && report.failures == 0; EditorApplication.update -= Tick; if (longLine != null) { longLine.displayName = oldName; longLine.cardDescription = oldDescription; longLine = null; } File.WriteAllText("output/ui-update/UI-09/playmode-financial.json", JsonUtility.ToJson(report, true)); return; }
        if (Time.frameCount <= lastFrame) { EditorApplication.QueuePlayerLoopUpdate(); EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint(); return; }
        var step = steps.Dequeue(); var check = new Check { name = step.Item1, frame = Time.frameCount };
        try { step.Item2(); check.passed = true; captureRetries = 0; }
        catch (InvalidOperationException e) when (e.Message == "The Game View must render at the evidence resolution." && captureRetries++ < 10)
        {
            var remaining = steps.ToArray(); steps.Clear(); steps.Enqueue(step); foreach (var pending in remaining) steps.Enqueue(pending);
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint(); EditorApplication.QueuePlayerLoopUpdate(); next = EditorApplication.timeSinceStartup + 1; return;
        }
        catch (Exception e) { check.detail = e.ToString(); report.failures++; steps.Clear(); Debug.LogError("[UI-09 QA] " + check.name + ": " + e.Message); }
        report.checks.Add(check); lastFrame = Time.frameCount; EditorApplication.QueuePlayerLoopUpdate(); EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint(); next = EditorApplication.timeSinceStartup + .5;
    }
}
