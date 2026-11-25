// Assets/Editor/Simulation/SimulationPanelsBuilder.cs
// Menu Game > Simulação > Montar painéis na GameScene.
//
// Monta na GameScene (layout novo) as duas telas do mês simulado e liga o motor ao fluxo do redesign:
//
//   SimulationFlow (raiz da cena)        SystemicRoundFlow + MonthSimulationController
//   Canvas
//   ├── Panel_Planning        [off]      ManagementPoliciesView   (PanelId.Planning, registrado no PanelRegistry)
//   └── Panel_MonthSimulation [off]      MonthSimulationView + EventDecisionPopupView
//       ├── CommitError       [off]      erro ao gravar + Tentar de novo
//       └── Popup_EventDecision [off]    popup de evento com até 4 reações
//   Panel_Menu/EventActions/CloseMonth   botão do hub vira "Planejar mês" (SystemicRoundFlow.OpenPlanning)
//   UiNavigation/MonthlySettlementController  desligado (era o fechamento do motor antigo)
//
// O resultado do mês, o trimestral, o relatório final e a falência são as telas do layout novo
// (Panel_MonthlyResult, Panel_QuarterlyResult, Panel_FinalReport, Panel_Bankruptcy): este script não mexe nelas.
// Cores e peças visuais vêm do tema novo (GamePalette, SharedUiSkin e SharedUiStyles).
// Rodar de novo apaga e recria só o que este script criou. Ctrl+Z desfaz.
using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SimulationPanelsBuilder
{
    private const string FontGuid = "89cea402e3db5bf4d9bc9d4f698b7692";      // InterTight-Variable (TMP), o mesmo do E-05
    private const string ConfigPath = "Assets/Resources/Simulation/SimulationConfig.asset";

    private static readonly string[] PanelNames = { "Panel_Planning", "Panel_MonthSimulation", "Panel_EventCalendar" };
    // telas da versão anterior deste script; só são apagadas se ainda tiverem os componentes antigos
    private static readonly string[] LegacyPanelNames = { "Panel_MonthSummary", "Panel_FinalReport", "Panel_Bankruptcy" };
    private const string HubButtonPath = "EventActions/CloseMonth";

    private static readonly (string id, string caption)[] Policies =
    {
        ("marketing", "Marketing"), ("loyalty", "Fidelização"), ("supplier", "Fornecedor"),
        ("supplierPayment", "Pagamento ao fornecedor"), ("maintenance", "Manutenção"), ("stock", "Estoque"),
        ("pay", "Remuneração da equipe"), ("training", "Treinamento")
    };

    private static Color Backdrop => GamePalette.WithAlpha(GamePalette.Plum, 0.72f);

    private static Sprite _panel, _primary, _secondary, _topbar, _white;
    private static TMP_FontAsset _font;

    [MenuItem("Game/Simulação/Montar painéis na GameScene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { Debug.LogError("[Simulação] Saia do Play Mode antes de montar os painéis."); return; }

        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var canvas = roots.FirstOrDefault(g => g.name == "Canvas");
        var listener = roots.Select(g => g.GetComponentInChildren<UIStateListener>(true)).FirstOrDefault(c => c != null);
        var registry = canvas != null ? canvas.GetComponentInChildren<PanelRegistry>(true) : null;
        var menuPanel = canvas != null ? canvas.transform.Find("Panel_Menu") : null;
        if (canvas == null || listener == null || registry == null || menuPanel == null)
        {
            Debug.LogError("[Simulação] Abra a GameScene antes (Canvas, UIStateListener, PanelRegistry e Panel_Menu não encontrados).");
            return;
        }

        if (!LoadAssets()) return;
        var config = AssetDatabase.LoadAssetAtPath<SimulationConfigAsset>(ConfigPath);
        if (config == null)
        {
            Debug.LogError("[Simulação] Crie o SimulationConfig antes: Game > Simulação > Criar ou atualizar SimulationConfig.");
            return;
        }

        // ---------------------------------------------------------------- limpa a execução anterior
        foreach (var name in PanelNames) RemoveIfExists(canvas.transform, name);
        foreach (var name in LegacyPanelNames) RemoveLegacy(canvas.transform, name);
        RemoveIfExists(menuPanel, "StartMonthButton");
        var hubButton = menuPanel.Find(HubButtonPath)?.GetComponent<Button>();
        if (hubButton == null)
        {
            Debug.LogError("[Simulação] Botão do hub " + HubButtonPath + " não encontrado no Panel_Menu.");
            return;
        }
        foreach (var old in roots.Where(g => g.name == "SimulationFlow")) Undo.DestroyObjectImmediate(old);

        var anchor = canvas.transform.Find("UiNavigation");
        int siblingIndex = anchor != null ? anchor.GetSiblingIndex() : canvas.transform.childCount;

        // ---------------------------------------------------------------- telas
        var planning = BuildPlanning(canvas.transform);
        var month = BuildMonth(canvas.transform, out var monthView, out var popupView, out var speedButtons, out var skipButton, out var abandonButton);
        var events = BuildEventCalendar(canvas.transform);

        foreach (var panel in new[] { month.gameObject, planning.gameObject, events.gameObject })
        {
            panel.transform.SetSiblingIndex(siblingIndex);
            panel.SetActive(false);
            Undo.RegisterCreatedObjectUndo(panel, "Montar painéis do simulador");
        }

        // ---------------------------------------------------------------- fluxo (composição)
        var flowGo = new GameObject("SimulationFlow");
        Undo.RegisterCreatedObjectUndo(flowGo, "Montar painéis do simulador");
        SceneManager.MoveGameObjectToScene(flowGo, scene);
        var controller = flowGo.AddComponent<MonthSimulationController>();
        var flow = flowGo.AddComponent<SystemicRoundFlow>();

        Set(controller, "monthView", monthView);
        Set(controller, "popupView", popupView);
        Set(flow, "simulationConfig", config);
        Set(flow, "monthController", controller);
        Set(flow, "planningView", planning.GetComponent<ManagementPoliciesView>());
        Set(flow, "monthPanel", month.gameObject);

        float[] speeds = { 0f, 1f, 2f, 4f };
        for (int i = 0; i < speedButtons.Count; i++)
            UnityEventTools.AddFloatPersistentListener(speedButtons[i].onClick, controller.SetSpeed, speeds[i]);
        UnityEventTools.AddVoidPersistentListener(skipButton.onClick, controller.SkipToEnd);
        UnityEventTools.AddVoidPersistentListener(abandonButton.onClick, flow.RequestAbandonMonth);      // pede confirmação

        // ---------------------------------------------------------------- hub: o botão de fechar o mês abre o planejamento
        Undo.RecordObject(hubButton, "Montar painéis do simulador");
        for (int i = hubButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(hubButton.onClick, i);
        UnityEventTools.AddVoidPersistentListener(hubButton.onClick, flow.OpenPlanning);
        var hubLabel = hubButton.GetComponentInChildren<TMP_Text>(true);
        if (hubLabel != null) { Undo.RecordObject(hubLabel, "Montar painéis do simulador"); hubLabel.text = "Planejar mês"; }

        // ---------------------------------------------------------------- motor antigo desligado
        foreach (var settlement in canvas.GetComponentsInChildren<MonthlySettlementController>(true))
        {
            Undo.RecordObject(settlement, "Montar painéis do simulador");
            settlement.enabled = false;                         // sem Start/Update: não fecha o mês pelo motor antigo
        }

        Register(registry, PanelId.Planning, planning.gameObject);
        Register(registry, PanelId.EventHistory, events.gameObject);           // hub > Eventos: calendário do motor novo

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Simulação] Painéis montados e ligados: Panel_Planning (hub > Planejar mês), Panel_MonthSimulation, Panel_EventCalendar (hub > Eventos), " +
                  "Popup_EventDecision e SimulationFlow. Resultado do mês, relatório final e falência usam as telas do layout novo. Cena salva.");
    }

    // =================================================================== Panel_Planning
    private static RectTransform BuildPlanning(Transform canvas)
    {
        var root = MakeScreen(canvas, "Panel_Planning");
        var view = root.gameObject.AddComponent<ManagementPoliciesView>();

        var title = Label(root, "TitleText", "Planejamento do mês", 46, GamePalette.OnDark, FontStyles.Bold);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -36), new Vector2(1200, 60));
        var hint = Label(root, "HintText", "Escolha as políticas do mês. Elas valem do dia 1 ao 30 e ficam travadas enquanto o mês roda.", 24, GamePalette.OnDark);
        Place(hint.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -100), new Vector2(1500, 40));
        var cash = Label(root, "CashText", "Caixa: R$ 0", 34, GamePalette.OnDark, FontStyles.Bold, TextAlignmentOptions.TopRight);
        Place(cash.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-190, -44), new Vector2(600, 50));
        var notice = Label(root, "NoticeText", "", 24, GamePalette.OnDark);
        notice.textWrappingMode = TextWrappingModes.Normal;
        notice.overflowMode = TextOverflowModes.Truncate;
        notice.rectTransform.anchorMin = new Vector2(0, 1); notice.rectTransform.anchorMax = new Vector2(1, 1);
        notice.rectTransform.pivot = new Vector2(0, 1);
        notice.rectTransform.offsetMin = new Vector2(80, -196); notice.rectTransform.offsetMax = new Vector2(-80, -140);

        var card = Rect("Card", root);
        Stretch(card, 60, 200, 60, 200);
        Img(card, _panel, Color.white, true, true);

        var list = Rect("Rows", card);
        Stretch(list, 36, 28, 36, 28);
        var vertical = list.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 10;
        vertical.childControlWidth = vertical.childControlHeight = true;
        vertical.childForceExpandWidth = vertical.childForceExpandHeight = true;

        var rowsProp = new List<(string id, RectTransform options)>();
        foreach (var (id, caption) in Policies)
        {
            var row = Rect("Row_" + id, list);
            var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 16;
            horizontal.childAlignment = TextAnchor.MiddleLeft;
            horizontal.childControlWidth = horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = true;

            var captionText = Label(row, "Caption", caption, 24, GamePalette.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            captionText.textWrappingMode = TextWrappingModes.Normal;
            var captionLayout = captionText.gameObject.AddComponent<LayoutElement>();
            captionLayout.preferredWidth = captionLayout.minWidth = 330;

            var options = Rect("Options", row);
            var optionsLayout = options.gameObject.AddComponent<LayoutElement>();
            optionsLayout.flexibleWidth = 1;
            var chips = options.gameObject.AddComponent<HorizontalLayoutGroup>();
            chips.spacing = 12;
            chips.childControlWidth = chips.childControlHeight = true;
            chips.childForceExpandWidth = chips.childForceExpandHeight = true;
            rowsProp.Add((id, options));
        }

        // chip clonado pela view (fica desativado)
        var chipRt = Rect("ChipTemplate", root);
        Place(chipRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 76));
        var chip = MakeButton(chipRt, _white, "Opção\n<size=80%>custo</size>", GamePalette.Ink, 21, out var chipLabel);
        chip.image.sprite = _secondary;                         // marfim com contorno; a view pinta selecionado/livre
        chip.image.color = Color.white;
        chipLabel.textWrappingMode = TextWrappingModes.Normal;
        chipLabel.margin = new Vector4(10, 4, 10, 4);
        chipRt.gameObject.SetActive(false);

        var cost = Label(root, "CostText", "Custo fixo das políticas: R$ 0 por mês", 28, GamePalette.OnDark);
        Place(cost.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(80, 128), new Vector2(1000, 44));
        var feedback = Label(root, "FeedbackText", "", 26, GamePalette.Warn, FontStyles.Normal, TextAlignmentOptions.BottomRight);
        Place(feedback.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 128), new Vector2(1000, 44));

        // Voltar: o HubBackButton do layout novo já aparece no canto inferior esquerdo de toda tela do hub
        var save = MakeButton(Footer(root, "SaveButton", false, 560, 240), _secondary, "SALVAR", GamePalette.Gold, 26, out _);
        var confirm = MakeButton(Footer(root, "ConfirmButton", false, 60, 460), _primary, "CONFIRMAR E INICIAR MÊS", GamePalette.OnPrimary, 26, out _);

        Set(view, "titleText", title);
        Set(view, "cashText", cash);
        Set(view, "costText", cost);
        Set(view, "feedbackText", feedback);
        Set(view, "noticeText", notice);
        Set(view, "chipTemplate", chip);
        Set(view, "saveButton", save);
        Set(view, "confirmButton", confirm);
        var so = new SerializedObject(view);
        var rows = so.FindProperty("rows");
        rows.arraySize = rowsProp.Count;
        for (int i = 0; i < rowsProp.Count; i++)
        {
            var element = rows.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("policyId").stringValue = rowsProp[i].id;
            element.FindPropertyRelative("options").objectReferenceValue = rowsProp[i].options;
        }
        so.FindProperty("chipSelected").colorValue = GamePalette.Primary;
        so.FindProperty("chipSelectedText").colorValue = GamePalette.OnPrimary;
        so.FindProperty("chipIdle").colorValue = Color.white;
        so.FindProperty("chipIdleText").colorValue = GamePalette.Ink;
        so.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    // =================================================================== Panel_MonthSimulation
    private static RectTransform BuildMonth(Transform canvas, out MonthSimulationView view, out EventDecisionPopupView popup,
                                            out List<Button> speedButtons, out Button skip, out Button abandon)
    {
        var root = MakeScreen(canvas, "Panel_MonthSimulation");
        view = root.gameObject.AddComponent<MonthSimulationView>();
        popup = root.gameObject.AddComponent<EventDecisionPopupView>();

        var day = Label(root, "DayText", "Mês 1 · dia 0 de 30", 46, GamePalette.OnDark, FontStyles.Bold);
        Place(day.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -36), new Vector2(1000, 60));
        var hint = Label(root, "HintText", "Cada dia o motor calcula demanda, capacidade, vendas e custos. Eventos pausam o tempo.", 24, GamePalette.OnDark);
        Place(hint.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -100), new Vector2(1400, 40));
        var cashCaption = Label(root, "CashCaption", "Caixa", 24, GamePalette.OnDark, FontStyles.Normal, TextAlignmentOptions.TopRight);
        Place(cashCaption.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-190, -30), new Vector2(500, 34));
        var cash = Label(root, "CashText", "R$ 0", 46, GamePalette.OnDark, FontStyles.Bold, TextAlignmentOptions.TopRight);
        Place(cash.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-190, -64), new Vector2(600, 60));

        // calendário (dia 1 = segunda): cor das vendas e etiqueta de cada evento com a reação escolhida
        var calendarCard = Rect("CalendarCard", root);
        calendarCard.anchorMin = new Vector2(0, 0); calendarCard.anchorMax = new Vector2(0.62f, 1);
        calendarCard.offsetMin = new Vector2(60, 200); calendarCard.offsetMax = new Vector2(-20, -150);
        Img(calendarCard, _panel, Color.white, true, true);
        var calendar = BuildCalendar(calendarCard);

        // números do mês
        var stats = Rect("StatsCard", root);
        stats.anchorMin = new Vector2(0.62f, 0); stats.anchorMax = new Vector2(1, 1);
        stats.offsetMin = new Vector2(20, 200); stats.offsetMax = new Vector2(-60, -150);
        Img(stats, _panel, Color.white, true, true);
        var column = Rect("Column", stats);
        Stretch(column, 34, 28, 34, 28);
        var vertical = column.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.spacing = 4;
        vertical.childControlWidth = vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        var inflow = Stat(column, "Inflow", "Entradas do mês (vendas)", GamePalette.Parse(GamePalette.HexMoney));
        var outflow = Stat(column, "Outflow", "Saídas do mês", GamePalette.Parse(GamePalette.HexDanger));
        var result = Stat(column, "Result", "Resultado de caixa", GamePalette.Ink);
        var speed = Stat(column, "Speed", "Velocidade", GamePalette.Primary);
        var eventsCaption = Label(column, "EventsCaption", "Eventos do mês", 24, GamePalette.Ink, FontStyles.Bold);
        eventsCaption.gameObject.AddComponent<LayoutElement>().preferredHeight = 40;
        var events = Label(column, "EventsText", "", 22, GamePalette.Ink);
        events.textWrappingMode = TextWrappingModes.Normal;
        events.overflowMode = TextOverflowModes.Truncate;
        var eventsLayout = events.gameObject.AddComponent<LayoutElement>();
        eventsLayout.flexibleHeight = 1;

        // tempo
        speedButtons = new List<Button>();
        float x = 60;
        foreach (var (name, label) in new[] { ("PauseButton", "II"), ("Speed1Button", "1x"), ("Speed2Button", "2x"), ("Speed4Button", "4x") })
        {
            speedButtons.Add(MakeButton(Footer(root, name, true, x, 120), _secondary, label, GamePalette.Gold, 28, out _));
            x += 134;
        }
        skip = MakeButton(Footer(root, "SkipButton", true, x, 200), _secondary, "PULAR", GamePalette.Gold, 26, out _);
        abandon = MakeButton(Footer(root, "AbandonButton", false, 60, 340), _secondary, "ABANDONAR MÊS", GamePalette.Gold, 26, out _);

        // confirmação do abandono (o tempo fica parado enquanto aparece)
        var abandonConfirm = Overlay(root, "AbandonConfirm", new Vector2(960, 440), out var abandonCard);
        var abandonTitle = Label(abandonCard, "Title", "Abandonar o mês?", 38, GamePalette.Ink, FontStyles.Bold, TextAlignmentOptions.Top);
        Place(abandonTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(860, 56));
        var abandonText = Label(abandonCard, "Message",
            "Nada deste mês será gravado. O caixa volta ao do dia 1 e você volta ao hub. Quando jogar de novo, o mês recomeça do dia 1 com os mesmos eventos.",
            26, GamePalette.Ink, FontStyles.Normal, TextAlignmentOptions.Top);
        abandonText.textWrappingMode = TextWrappingModes.Normal;
        Place(abandonText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(860, 170));
        var keepRt = Rect("CancelButton", abandonCard);
        Place(keepRt, new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-12, 40), new Vector2(400, 80));
        var keepPlaying = MakeButton(keepRt, _secondary, "CONTINUAR JOGANDO", GamePalette.Ink, 26, out _);
        var leaveRt = Rect("ConfirmButton", abandonCard);
        Place(leaveRt, new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(12, 40), new Vector2(400, 80));
        var leave = MakeButton(leaveRt, _primary, "ABANDONAR MÊS", GamePalette.OnPrimary, 26, out _);
        abandonConfirm.gameObject.SetActive(false);

        // erro ao gravar
        var error = Overlay(root, "CommitError", new Vector2(960, 420), out var errorCard);
        var errorTitle = Label(errorCard, "Title", "Fechamento não gravado", 38, GamePalette.Primary, FontStyles.Bold, TextAlignmentOptions.Top);
        Place(errorTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(860, 56));
        var errorText = Label(errorCard, "Message", "", 26, GamePalette.Ink, FontStyles.Normal, TextAlignmentOptions.Top);
        errorText.textWrappingMode = TextWrappingModes.Normal;
        Place(errorText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(860, 170));
        var retryRt = Rect("RetryButton", errorCard);
        Place(retryRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(380, 80));
        var retry = MakeButton(retryRt, _primary, "TENTAR DE NOVO", GamePalette.OnPrimary, 26, out _);
        error.gameObject.SetActive(false);

        // popup de evento
        var popupRoot = Overlay(root, "Popup_EventDecision", new Vector2(1160, 820), out var popupCard);
        var popupTitle = Label(popupCard, "TitleText", "Evento", 40, GamePalette.Primary, FontStyles.Bold, TextAlignmentOptions.Top);
        popupTitle.textWrappingMode = TextWrappingModes.Normal;
        Place(popupTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(1060, 100));
        var chance = Label(popupCard, "ChanceText", "Chance hoje: 0%", 28, GamePalette.Ink, FontStyles.Bold, TextAlignmentOptions.Top);
        Place(chance.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(1060, 40));
        var factors = Label(popupCard, "FactorsText", "", 24, GamePalette.Ink, FontStyles.Normal, TextAlignmentOptions.Top);
        factors.textWrappingMode = TextWrappingModes.Normal;
        factors.overflowMode = TextOverflowModes.Truncate;
        Place(factors.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -186), new Vector2(1060, 190));
        var reactions = Rect("Reactions", popupCard);
        reactions.anchorMin = new Vector2(0, 0); reactions.anchorMax = new Vector2(1, 0); reactions.pivot = new Vector2(0.5f, 0);
        reactions.anchoredPosition = new Vector2(0, 40); reactions.sizeDelta = new Vector2(-100, 380);
        var reactionsLayout = reactions.gameObject.AddComponent<VerticalLayoutGroup>();
        reactionsLayout.spacing = 12;
        reactionsLayout.childAlignment = TextAnchor.LowerCenter;
        reactionsLayout.childControlWidth = reactionsLayout.childControlHeight = true;
        reactionsLayout.childForceExpandWidth = true;
        reactionsLayout.childForceExpandHeight = false;
        var reactionButtons = new Button[4];
        var reactionLabels = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            var rt = Rect("Reaction_" + (i + 1), reactions);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = 84;
            reactionButtons[i] = MakeButton(rt, _primary, "Reação", GamePalette.OnPrimary, 24, out reactionLabels[i]);
            reactionLabels[i].textWrappingMode = TextWrappingModes.Normal;
            reactionLabels[i].margin = new Vector4(24, 4, 24, 4);
        }
        popupRoot.gameObject.SetActive(false);

        Set(view, "dayText", day);
        Set(view, "cashText", cash);
        Set(view, "inflowText", inflow);
        Set(view, "outflowText", outflow);
        Set(view, "resultText", result);
        Set(view, "speedText", speed);
        Set(view, "lastEventsText", events);
        Set(view, "commitErrorPanel", error.gameObject);
        Set(view, "commitErrorText", errorText);
        Set(view, "retryButton", retry);
        Set(view, "calendar", calendar);
        Set(view, "abandonConfirmPanel", abandonConfirm.gameObject);
        Set(view, "abandonConfirmButton", leave);
        Set(view, "abandonCancelButton", keepPlaying);

        Set(popup, "root", popupRoot.gameObject);
        Set(popup, "titleText", popupTitle);
        Set(popup, "chanceText", chance);
        Set(popup, "factorsText", factors);
        SetArray(popup, "reactionButtons", reactionButtons);
        SetArray(popup, "reactionLabels", reactionLabels);
        return root;
    }

    // =================================================================== Panel_EventCalendar (hub > Eventos)
    private static RectTransform BuildEventCalendar(Transform canvas)
    {
        var root = MakeScreen(canvas, "Panel_EventCalendar");
        var screen = root.gameObject.AddComponent<EventCalendarScreen>();

        var title = Label(root, "TitleText", "Eventos do mês", 46, GamePalette.OnDark, FontStyles.Bold);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -36), new Vector2(1000, 60));
        var hint = Label(root, "HintText", "Cada dia mostra a cor das vendas e os eventos com a escolha que você fez.", 24, GamePalette.OnDark);
        Place(hint.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -100), new Vector2(1400, 40));

        // abas Mês 1, 2 e 3 na faixa do topo, à direita
        var tabs = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            var tabRt = Rect("MonthTab_" + (i + 1), root);
            Place(tabRt, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-190 - (2 - i) * 196, -40), new Vector2(180, 70));
            tabs[i] = MakeButton(tabRt, _secondary, "Mês " + (i + 1), GamePalette.Ink, 26, out _);
        }

        var calendarCard = Rect("CalendarCard", root);
        calendarCard.anchorMin = new Vector2(0, 0); calendarCard.anchorMax = new Vector2(0.62f, 1);
        calendarCard.offsetMin = new Vector2(60, 140); calendarCard.offsetMax = new Vector2(-20, -180);
        Img(calendarCard, _panel, Color.white, true, true);
        var calendar = BuildCalendar(calendarCard);

        var side = Rect("ListCard", root);
        side.anchorMin = new Vector2(0.62f, 0); side.anchorMax = new Vector2(1, 1);
        side.offsetMin = new Vector2(20, 140); side.offsetMax = new Vector2(-60, -180);
        Img(side, _panel, Color.white, true, true);
        var monthTitle = Label(side, "MonthTitle", "Mês 1", 32, GamePalette.Ink, FontStyles.Bold);
        Place(monthTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -26), new Vector2(560, 46));
        var list = Label(side, "ListText", "", 22, GamePalette.Ink);
        list.textWrappingMode = TextWrappingModes.Normal;
        list.overflowMode = TextOverflowModes.Truncate;
        list.lineSpacing = 18;
        list.rectTransform.anchorMin = Vector2.zero; list.rectTransform.anchorMax = Vector2.one;
        list.rectTransform.offsetMin = new Vector2(34, 28); list.rectTransform.offsetMax = new Vector2(-34, -90);

        // sem mês jogado: o cartão do calendário some e fica esta frase no lugar
        var empty = Label(root, "EmptyText", "Jogue o primeiro mês para ver o calendário de eventos.", 30, GamePalette.OnDark, FontStyles.Normal, TextAlignmentOptions.Center);
        empty.textWrappingMode = TextWrappingModes.Normal;
        empty.rectTransform.anchorMin = new Vector2(0, 0); empty.rectTransform.anchorMax = new Vector2(0.62f, 1);
        empty.rectTransform.offsetMin = new Vector2(100, 140); empty.rectTransform.offsetMax = new Vector2(-60, -180);
        empty.gameObject.SetActive(false);

        Set(screen, "calendar", calendar);
        Set(screen, "monthTitle", monthTitle);
        Set(screen, "listText", list);
        Set(screen, "emptyText", empty);
        Set(screen, "calendarCard", calendarCard.gameObject);
        SetArray(screen, "monthTabs", tabs);
        return root;
    }

    /// Calendário de 30 dias em 7 colunas (Seg a Dom; dia 1 = segunda) dentro de um cartão.
    private static EventCalendarView BuildCalendar(RectTransform card)
    {
        var view = card.gameObject.AddComponent<EventCalendarView>();
        string[] weekdays = { "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb", "Dom" };
        var header = Rect("Weekdays", card);
        header.anchorMin = new Vector2(0, 1); header.anchorMax = new Vector2(1, 1); header.pivot = new Vector2(0.5f, 1);
        header.offsetMin = new Vector2(24, -58); header.offsetMax = new Vector2(-24, -20);
        var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>();
        headerLayout.spacing = 8;
        headerLayout.childControlWidth = headerLayout.childControlHeight = true;
        headerLayout.childForceExpandWidth = headerLayout.childForceExpandHeight = true;
        foreach (var w in weekdays) Label(header, "Weekday_" + w, w, 22, GamePalette.Ink, FontStyles.Bold, TextAlignmentOptions.Center);

        var grid = Rect("Grid", card);
        Stretch(grid, 24, 66, 24, 70);

        var cells = new EventCalendarView.DayCell[30];
        for (int i = 0; i < 30; i++)
        {
            var cellRt = Rect("Day_" + (i + 1), grid);
            int col = i % 7, row = i / 7;                          // âncoras por fração: acompanha qualquer resolução
            cellRt.anchorMin = new Vector2(col / 7f, 1 - (row + 1) / 5f);
            cellRt.anchorMax = new Vector2((col + 1) / 7f, 1 - row / 5f);
            cellRt.offsetMin = new Vector2(4, 4); cellRt.offsetMax = new Vector2(-4, -4);
            var cell = new EventCalendarView.DayCell { background = Img(cellRt, _white, Color.white, true, false) };
            cell.number = Label(cellRt, "Number", (i + 1).ToString(), 24, GamePalette.Ink, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(cell.number.rectTransform, 10, 6, 6, 6);

            var marker = Rect("Event", cellRt);
            marker.anchorMin = new Vector2(0, 0); marker.anchorMax = new Vector2(1, 0); marker.pivot = new Vector2(0.5f, 0);
            marker.offsetMin = new Vector2(6, 30); marker.offsetMax = new Vector2(-6, 60);
            cell.marker = Img(marker, _white, GamePalette.ErrorText, true, false);
            cell.markerText = Label(marker, "Title", "", 18, GamePalette.OnDark, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            Stretch(cell.markerText.rectTransform, 8, 0, 6, 0);
            cell.markerText.overflowMode = TextOverflowModes.Ellipsis;

            cell.reactionText = Label(cellRt, "Reaction", "", 18, GamePalette.Ink, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            cell.reactionText.overflowMode = TextOverflowModes.Ellipsis;
            var reaction = cell.reactionText.rectTransform;
            reaction.anchorMin = new Vector2(0, 0); reaction.anchorMax = new Vector2(1, 0); reaction.pivot = new Vector2(0.5f, 0);
            reaction.offsetMin = new Vector2(10, 4); reaction.offsetMax = new Vector2(-6, 28);
            marker.gameObject.SetActive(false);
            cells[i] = cell;
        }

        var legend = Label(card, "Legend",
            $"Cor do dia (vendas contra a média do mês): <b><color={GamePalette.HexDanger}>fraco</color></b>, <b><color=#B07D1E>normal</color></b>, <b><color={GamePalette.HexMoney}>forte</color></b>.   " +
            $"Etiqueta do evento: <b><color={GamePalette.HexErrorText}>negativo</color></b> ou <b><color=#145146>positivo</color></b>, com a reação embaixo.",
            20, GamePalette.Ink, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
        legend.textWrappingMode = TextWrappingModes.Normal;
        legend.rectTransform.anchorMin = new Vector2(0, 0); legend.rectTransform.anchorMax = new Vector2(1, 0); legend.rectTransform.pivot = new Vector2(0, 0);
        legend.rectTransform.offsetMin = new Vector2(26, 12); legend.rectTransform.offsetMax = new Vector2(-26, 64);

        var so = new SerializedObject(view);
        var array = so.FindProperty("days");
        array.arraySize = cells.Length;
        for (int i = 0; i < cells.Length; i++)
        {
            var e = array.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("background").objectReferenceValue = cells[i].background;
            e.FindPropertyRelative("number").objectReferenceValue = cells[i].number;
            e.FindPropertyRelative("marker").objectReferenceValue = cells[i].marker;
            e.FindPropertyRelative("markerText").objectReferenceValue = cells[i].markerText;
            e.FindPropertyRelative("reactionText").objectReferenceValue = cells[i].reactionText;
        }
        so.FindProperty("emptyDay").colorValue = GamePalette.SurfaceAlt;
        so.FindProperty("weakDay").colorValue = Color.Lerp(Color.white, GamePalette.Danger, 0.45f);
        so.FindProperty("normalDay").colorValue = Color.Lerp(Color.white, GamePalette.Warn, 0.55f);
        so.FindProperty("strongDay").colorValue = Color.Lerp(Color.white, GamePalette.Money, 0.45f);
        so.FindProperty("negativeEvent").colorValue = GamePalette.ErrorText;
        so.FindProperty("positiveEvent").colorValue = GamePalette.PositiveReportText;
        so.FindProperty("pendingEvent").colorValue = GamePalette.Primary;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // =================================================================== registro
    private static void Register(PanelRegistry registry, PanelId id, GameObject panel)
    {
        var so = new SerializedObject(registry);
        var bindings = so.FindProperty("bindings");
        int index = -1;
        for (int i = 0; i < bindings.arraySize; i++)
            if (bindings.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue == (int)id) index = i;
        if (index < 0)
        {
            index = bindings.arraySize;
            bindings.arraySize++;
        }
        var element = bindings.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("id").intValue = (int)id;
        element.FindPropertyRelative("root").objectReferenceValue = panel;
        so.ApplyModifiedProperties();
    }

    // =================================================================== helpers
    private static bool LoadAssets()
    {
        var skin = SharedUiSkin.Current;
        _panel = skin?.Get("UI_PANEL_IVORY");
        _primary = skin?.Get("UI_PRIMARY_VIOLET_NORMAL");
        _secondary = skin?.Get("UI_SECONDARY_IVORY_NORMAL");
        _topbar = skin?.Get("UI_TOPBAR_VIOLET");
        _white = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        _font = GameTypography.Resolve(TypographyRole.Body).Font
                ?? AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        bool ok = skin && _panel && _primary && _secondary && _topbar && _white && _font;
        if (!ok)
            Debug.LogError($"[Simulação] Asset faltando: SharedUiSkin={skin != null} painel={_panel != null} primário={_primary != null} " +
                           $"secundário={_secondary != null} topo={_topbar != null} branco={_white != null} fonte={_font != null}");
        return ok;
    }

    /// Tela cheia: fundo opaco que bloqueia cliques e faixa do topo.
    private static RectTransform MakeScreen(Transform canvas, string name)
    {
        var root = Rect(name, canvas);
        Stretch(root);
        Img(root, null, GamePalette.Background, false, true);
        var top = Rect("TopBar", root);
        top.anchorMin = new Vector2(0, 1); top.anchorMax = new Vector2(1, 1); top.pivot = new Vector2(0.5f, 1);
        top.sizeDelta = new Vector2(0, 150);
        Img(top, _topbar, Color.white, true, false);
        return root;
    }

    /// Camada por cima da tela (escurece o fundo) com um cartão centralizado.
    private static RectTransform Overlay(RectTransform parent, string name, Vector2 size, out RectTransform card)
    {
        var root = Rect(name, parent);
        Stretch(root);
        var backdrop = Rect("Backdrop", root);
        Stretch(backdrop);
        backdrop.gameObject.AddComponent<Image>().color = Backdrop;
        card = Rect("Card", root);
        Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        Img(card, _panel, Color.white, true, true);
        return root;
    }

    private static TextMeshProUGUI Stat(RectTransform column, string name, string caption, Color valueColor)
    {
        var captionText = Label(column, name + "Caption", caption, 24, GamePalette.Muted);
        captionText.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
        var value = Label(column, name + "Text", "R$ 0", 40, valueColor, FontStyles.Bold);
        value.gameObject.AddComponent<LayoutElement>().preferredHeight = 64;
        return value;
    }

    private static RectTransform Footer(RectTransform root, string name, bool left, float offset, float width)
    {
        var rt = Rect(name, root);
        var anchor = left ? new Vector2(0, 0) : new Vector2(1, 0);
        Place(rt, anchor, anchor, new Vector2(left ? offset : -offset, 40), new Vector2(width, 80));
        return rt;
    }

    /// Botão do tema novo: _primary = violeta (ação principal), _secondary = marfim com contorno.
    /// SharedUiStyles.Bind liga o SharedControlAppearance (hover, pressionado, foco, desabilitado).
    private static Button MakeButton(RectTransform rt, Sprite sprite, string label, Color labelColor, float size, out TextMeshProUGUI text)
    {
        bool themed = sprite == _primary || sprite == _secondary;
        if (themed) labelColor = sprite == _primary ? GamePalette.OnPrimary : GamePalette.Ink;
        var image = Img(rt, sprite, Color.white, true, true);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        text = Label(rt, "Label", label, size, labelColor, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 8, 4, 8, 4);
        text.raycastTarget = false;
        if (themed) SharedUiStyles.Bind(button, sprite == _primary ? SharedControlKind.Primary : SharedControlKind.Secondary);
        else
        {
            var colors = button.colors;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.96f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.88f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
            button.colors = colors;
        }
        return button;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string content, float size, Color color,
                                         FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        var rt = Rect(name, parent);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        // papel do tema: a face real carrega o peso (Roboto Bold ou Regular), sem negrito sintético
        GameTypography.Apply(text, (style & FontStyles.Bold) != 0 ? TypographyRole.TableTotal : TypographyRole.Body, size);
        text.text = content;
        text.color = color;
        text.alignment = align;
        text.richText = true;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static Image Img(RectTransform rt, Sprite sprite, Color color, bool sliced, bool raycast)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static void RemoveLegacy(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null && (existing.GetComponent<MonthSummaryView>() != null || existing.GetComponent<MatchEndView>() != null))
            Undo.DestroyObjectImmediate(existing.gameObject);
    }

    private static void RemoveIfExists(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
    }

    private static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var property = so.FindProperty(field);
        if (property == null) { Debug.LogError($"[Simulação] Campo {field} não existe em {target.GetType().Name}."); return; }
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        var property = so.FindProperty(field);
        if (property == null) { Debug.LogError($"[Simulação] Campo {field} não existe em {target.GetType().Name}."); return; }
        property.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var property = so.FindProperty(field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Ref(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property == null) { Debug.LogError($"[Simulação] Campo {field} não existe no UIStateListener."); return; }
        property.objectReferenceValue = value;
    }
}
