// Assets/Editor/Simulation/SimulationPanelsBuilder.cs
// Menu Game > Simulação > Montar painéis na GameScene.
//
// Monta na GameScene tudo o que o mês simulado precisa e liga nas peças que já existem:
//
//   SimulationFlow (raiz da cena)        SystemicRoundFlow + MonthSimulationController
//   Canvas
//   ├── Panel_Planning        [off]      ManagementPoliciesView   (PanelId.Planning, aberto pelo hub)
//   ├── Panel_MonthSimulation [off]      MonthSimulationView + EventDecisionPopupView
//   │   ├── CommitError       [off]      erro ao gravar + Tentar de novo
//   │   └── Popup_EventDecision [off]    popup de evento com até 4 reações
//   ├── Panel_MonthSummary    [off]      MonthSummaryView         (estado Round_Summary)
//   ├── Panel_FinalReport     [off]      MatchEndView             (estado FinalReport)
//   └── Panel_Bankruptcy      [off]      MatchEndView             (estado GameOver_Bankruptcy)
//   Panel_Menu/StartMonthButton          botão PLANEJAR MÊS no hub (PanelNavButton -> Planning)
//
// Também preenche no UIStateListener os campos de rodada/fim e registra o Panel_Planning no
// PanelRegistry. Rodar de novo apaga e recria só o que este script criou. Ctrl+Z desfaz.
using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.Controllers;
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
    private const string Art = "Assets/Art/Sprites/REDESIGN-UI/";
    private const string FontGuid = "89cea402e3db5bf4d9bc9d4f698b7692";      // InterTight-Variable (TMP), o mesmo do E-05
    private const string ConfigPath = "Assets/Resources/Simulation/SimulationConfig.asset";

    private static readonly string[] PanelNames =
        { "Panel_Planning", "Panel_MonthSimulation", "Panel_MonthSummary", "Panel_FinalReport", "Panel_Bankruptcy" };

    private static readonly (string id, string caption)[] Policies =
    {
        ("marketing", "Marketing"), ("loyalty", "Fidelização"), ("supplier", "Fornecedor"),
        ("supplierPayment", "Pagamento ao fornecedor"), ("maintenance", "Manutenção"), ("stock", "Estoque"),
        ("pay", "Remuneração da equipe"), ("training", "Treinamento")
    };

    // cores sobre o dourado e texto escuro das células coloridas
    private static readonly Color DarkBrown = new Color32(61, 38, 4, 255);
    private static readonly Color Backdrop = new Color(0.04f, 0.04f, 0.06f, 0.70f);

    private static Sprite _screen, _panel, _primary, _secondary, _topbar, _white;
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
        RemoveIfExists(menuPanel, "StartMonthButton");
        foreach (var old in roots.Where(g => g.name == "SimulationFlow")) Undo.DestroyObjectImmediate(old);

        var anchor = canvas.transform.Find("UiNavigation");
        int siblingIndex = anchor != null ? anchor.GetSiblingIndex() : canvas.transform.childCount;

        // ---------------------------------------------------------------- telas
        var planning = BuildPlanning(canvas.transform);
        var month = BuildMonth(canvas.transform, out var monthView, out var popupView, out var speedButtons, out var skipButton, out var abandonButton);
        var summary = BuildSummary(canvas.transform);
        var final = BuildMatchEnd(canvas.transform, "Panel_FinalReport", false);
        var bankrupt = BuildMatchEnd(canvas.transform, "Panel_Bankruptcy", true);

        foreach (var panel in new[] { bankrupt, final, summary, month.gameObject, planning.gameObject })
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
        Set(summary.GetComponent<MonthSummaryView>(), "flow", flow);
        Set(final.GetComponent<MatchEndView>(), "flow", flow);
        Set(bankrupt.GetComponent<MatchEndView>(), "flow", flow);

        float[] speeds = { 0f, 1f, 2f, 4f };
        for (int i = 0; i < speedButtons.Count; i++)
            UnityEventTools.AddFloatPersistentListener(speedButtons[i].onClick, controller.SetSpeed, speeds[i]);
        UnityEventTools.AddVoidPersistentListener(skipButton.onClick, controller.SkipToEnd);
        UnityEventTools.AddVoidPersistentListener(abandonButton.onClick, flow.AbandonMonth);

        // ---------------------------------------------------------------- hub: botão PLANEJAR MÊS
        var startRt = Rect("StartMonthButton", menuPanel);
        Place(startRt, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 24), new Vector2(380, 80));
        var startButton = MakeButton(startRt, _primary, "PLANEJAR MÊS", DarkBrown, 28, out _);
        var nav = startButton.gameObject.AddComponent<PanelNavButton>();
        SetInt(nav, "action", (int)PanelNavButton.NavAction.Open);
        SetInt(nav, "target", (int)PanelId.Planning);
        Undo.RegisterCreatedObjectUndo(startRt.gameObject, "Montar painéis do simulador");

        // ---------------------------------------------------------------- ligações com o que já existe
        var listenerSo = new SerializedObject(listener);
        Ref(listenerSo, "roundStartPanel", month.gameObject);
        Ref(listenerSo, "roundSalesPanel", month.gameObject);
        Ref(listenerSo, "roundCostsPanel", month.gameObject);
        Ref(listenerSo, "roundEventPanel", month.gameObject);
        Ref(listenerSo, "roundSummaryPanel", summary);
        Ref(listenerSo, "finalReportPanel", final);
        Ref(listenerSo, "bankruptcyPanel", bankrupt);
        listenerSo.ApplyModifiedProperties();

        RegisterPlanning(registry, planning.gameObject);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Simulação] Painéis montados e ligados: Panel_Planning (hub > PLANEJAR MÊS), Panel_MonthSimulation, " +
                  "Popup_EventDecision, Panel_MonthSummary, Panel_FinalReport, Panel_Bankruptcy e SimulationFlow. Cena salva.");
    }

    // =================================================================== Panel_Planning
    private static RectTransform BuildPlanning(Transform canvas)
    {
        var root = MakeScreen(canvas, "Panel_Planning");
        var view = root.gameObject.AddComponent<ManagementPoliciesView>();

        var title = Label(root, "TitleText", "Planejamento do mês", 46, GamePalette.Gold, FontStyles.Bold);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -36), new Vector2(1200, 60));
        var hint = Label(root, "HintText", "Escolha as políticas do mês. Elas valem do dia 1 ao 30 e ficam travadas enquanto o mês roda.", 24, GamePalette.Muted);
        Place(hint.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -100), new Vector2(1500, 40));
        var cash = Label(root, "CashText", "Caixa: R$ 0", 34, GamePalette.Ink, FontStyles.Bold, TextAlignmentOptions.TopRight);
        Place(cash.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-80, -44), new Vector2(600, 50));
        var notice = Label(root, "NoticeText", "", 22, GamePalette.Muted);
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
        chip.image.color = GamePalette.SurfaceAlt;
        chipLabel.textWrappingMode = TextWrappingModes.Normal;
        chipLabel.margin = new Vector4(10, 4, 10, 4);
        chipRt.gameObject.SetActive(false);

        var cost = Label(root, "CostText", "Custo fixo das políticas: R$ 0 por mês", 28, GamePalette.Ink);
        Place(cost.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(80, 128), new Vector2(1000, 44));
        var feedback = Label(root, "FeedbackText", "", 26, GamePalette.Gold, FontStyles.Normal, TextAlignmentOptions.BottomRight);
        Place(feedback.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 128), new Vector2(1000, 44));

        var back = MakeButton(Footer(root, "BackButton", true, 60, 240), _secondary, "VOLTAR", GamePalette.Gold, 26, out _);
        var backNav = back.gameObject.AddComponent<PanelNavButton>();
        SetInt(backNav, "action", (int)PanelNavButton.NavAction.Back);
        var save = MakeButton(Footer(root, "SaveButton", false, 560, 240), _secondary, "SALVAR", GamePalette.Gold, 26, out _);
        var confirm = MakeButton(Footer(root, "ConfirmButton", false, 60, 460), _primary, "CONFIRMAR E INICIAR MÊS", DarkBrown, 26, out _);

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
        so.FindProperty("chipSelected").colorValue = GamePalette.Gold;
        so.FindProperty("chipSelectedText").colorValue = GamePalette.OnGold;
        so.FindProperty("chipIdle").colorValue = GamePalette.SurfaceAlt;
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

        var day = Label(root, "DayText", "Mês 1 · dia 0 de 30", 46, GamePalette.Gold, FontStyles.Bold);
        Place(day.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -36), new Vector2(1000, 60));
        var hint = Label(root, "HintText", "Cada dia o motor calcula demanda, capacidade, vendas e custos. Eventos pausam o tempo.", 24, GamePalette.Muted);
        Place(hint.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -100), new Vector2(1400, 40));
        var cashCaption = Label(root, "CashCaption", "Caixa", 24, GamePalette.Muted, FontStyles.Normal, TextAlignmentOptions.TopRight);
        Place(cashCaption.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-80, -30), new Vector2(500, 34));
        var cash = Label(root, "CashText", "R$ 0", 46, GamePalette.Ink, FontStyles.Bold, TextAlignmentOptions.TopRight);
        Place(cash.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-80, -64), new Vector2(600, 60));

        // calendário
        var calendar = Rect("CalendarCard", root);
        calendar.anchorMin = new Vector2(0, 0); calendar.anchorMax = new Vector2(0.62f, 1);
        calendar.offsetMin = new Vector2(60, 200); calendar.offsetMax = new Vector2(-20, -150);
        Img(calendar, _panel, Color.white, true, true);
        var grid = Rect("Grid", calendar);
        Stretch(grid, 30, 30, 30, 80);
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(164, 110);
        layout.spacing = new Vector2(12, 12);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 6;
        layout.childAlignment = TextAnchor.UpperCenter;
        var cells = new Image[30];
        for (int i = 0; i < 30; i++)
        {
            var cell = Rect("Day_" + (i + 1), grid);
            cells[i] = Img(cell, _white, Color.white, true, false);
            var number = Label(cell, "Number", (i + 1).ToString(), 30, GamePalette.OnGold, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Stretch(number.rectTransform, 14, 8, 8, 8);
        }
        var legend = Label(calendar, "Legend",
            "Cor do dia: <color=#D95A4D>vermelho</color> fraco, <color=#F2BF4D>amarelo</color> normal, <color=#4DA666>verde</color> forte (receita do dia contra a média do mês)",
            22, GamePalette.Muted, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
        Place(legend.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(34, 26), new Vector2(1100, 36));

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
        var speed = Stat(column, "Speed", "Velocidade", GamePalette.Gold);
        var eventsCaption = Label(column, "EventsCaption", "Eventos do mês", 24, GamePalette.Muted);
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

        // erro ao gravar
        var error = Overlay(root, "CommitError", new Vector2(960, 420), out var errorCard);
        var errorTitle = Label(errorCard, "Title", "Fechamento não gravado", 38, GamePalette.Gold, FontStyles.Bold, TextAlignmentOptions.Top);
        Place(errorTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(860, 56));
        var errorText = Label(errorCard, "Message", "", 26, GamePalette.Ink, FontStyles.Normal, TextAlignmentOptions.Top);
        errorText.textWrappingMode = TextWrappingModes.Normal;
        Place(errorText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(860, 170));
        var retryRt = Rect("RetryButton", errorCard);
        Place(retryRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(380, 80));
        var retry = MakeButton(retryRt, _primary, "TENTAR DE NOVO", DarkBrown, 26, out _);
        error.gameObject.SetActive(false);

        // popup de evento
        var popupRoot = Overlay(root, "Popup_EventDecision", new Vector2(1160, 820), out var popupCard);
        var popupTitle = Label(popupCard, "TitleText", "Evento", 40, GamePalette.Gold, FontStyles.Bold, TextAlignmentOptions.Top);
        popupTitle.textWrappingMode = TextWrappingModes.Normal;
        Place(popupTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(1060, 100));
        var chance = Label(popupCard, "ChanceText", "Chance hoje: 0%", 28, GamePalette.Ink, FontStyles.Normal, TextAlignmentOptions.Top);
        Place(chance.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(1060, 40));
        var factors = Label(popupCard, "FactorsText", "", 22, GamePalette.Muted, FontStyles.Normal, TextAlignmentOptions.Top);
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
            reactionButtons[i] = MakeButton(rt, _secondary, "Reação", GamePalette.Gold, 24, out reactionLabels[i]);
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
        SetArray(view, "calendarCells", cells);

        Set(popup, "root", popupRoot.gameObject);
        Set(popup, "titleText", popupTitle);
        Set(popup, "chanceText", chance);
        Set(popup, "factorsText", factors);
        SetArray(popup, "reactionButtons", reactionButtons);
        SetArray(popup, "reactionLabels", reactionLabels);
        return root;
    }

    // =================================================================== Panel_MonthSummary
    private static GameObject BuildSummary(Transform canvas)
    {
        var root = MakeScreen(canvas, "Panel_MonthSummary");
        var view = root.gameObject.AddComponent<MonthSummaryView>();

        var title = Label(root, "TitleText", "Resumo do mês", 46, GamePalette.Gold, FontStyles.Bold);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -30), new Vector2(1200, 60));
        var headline = Label(root, "HeadlineText", "", 28, GamePalette.Ink);
        Place(headline.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -96), new Vector2(1760, 44));

        var dre = Card(root, "DreCard", new Vector2(0, 0.40f), new Vector2(0.34f, 1), 60, 10, out var dreText);
        var cash = Card(root, "CashCard", new Vector2(0.34f, 0.40f), new Vector2(0.67f, 1), 10, 10, out var cashText);
        var balance = Card(root, "BalanceCard", new Vector2(0.67f, 0.40f), new Vector2(1, 1), 10, 60, out var balanceText);
        var indicators = Card(root, "IndicatorsCard", new Vector2(0, 0), new Vector2(0.34f, 0.40f), 60, 10, out var indicatorsText);
        var score = Card(root, "ScoreCard", new Vector2(0.34f, 0), new Vector2(0.67f, 0.40f), 10, 10, out var scoreText);

        // feedback: texto longo, com rolagem
        var feedbackCard = Rect("FeedbackCard", root);
        feedbackCard.anchorMin = new Vector2(0.67f, 0); feedbackCard.anchorMax = new Vector2(1, 0.40f);
        feedbackCard.offsetMin = new Vector2(10, 130); feedbackCard.offsetMax = new Vector2(-60, -10);
        Img(feedbackCard, _panel, Color.white, true, true);
        var viewport = Rect("Viewport", feedbackCard);
        Stretch(viewport, 24, 20, 24, 20);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = Vector2.zero;
        var feedbackText = Label(content, "FeedbackText", "", 20, GamePalette.Ink);
        feedbackText.textWrappingMode = TextWrappingModes.Normal;
        Stretch(feedbackText.rectTransform);
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.childControlWidth = contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        var scroll = feedbackCard.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30;

        var continueButton = MakeButton(Footer(root, "ContinueButton", false, 60, 460), _primary, "CONTINUAR", DarkBrown, 26, out var continueLabel);

        Set(view, "titleText", title);
        Set(view, "headlineText", headline);
        Set(view, "dreText", dreText);
        Set(view, "cashText", cashText);
        Set(view, "balanceText", balanceText);
        Set(view, "indicatorsText", indicatorsText);
        Set(view, "scoreText", scoreText);
        Set(view, "feedbackText", feedbackText);
        Set(view, "continueButton", continueButton);
        Set(view, "continueLabel", continueLabel);
        return root.gameObject;
    }

    private static RectTransform Card(RectTransform root, string name, Vector2 min, Vector2 max, float left, float right, out TextMeshProUGUI text)
    {
        bool top = max.y >= 1f;
        var card = Rect(name, root);
        card.anchorMin = min; card.anchorMax = max;
        card.offsetMin = new Vector2(left, top ? 10 : 130);
        card.offsetMax = new Vector2(-right, top ? -150 : -10);
        Img(card, _panel, Color.white, true, true);
        text = Label(card, "Text", "", 20, GamePalette.Ink);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        Stretch(text.rectTransform, 26, 20, 26, 16);
        return card;
    }

    // =================================================================== fim de partida
    private static GameObject BuildMatchEnd(Transform canvas, string name, bool bankruptcy)
    {
        var root = MakeScreen(canvas, name);
        var view = root.gameObject.AddComponent<MatchEndView>();
        var title = Label(root, "TitleText", bankruptcy ? "Falência" : "Relatório final da partida", 50, GamePalette.Gold, FontStyles.Bold);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -40), new Vector2(1400, 66));
        var card = Rect("Card", root);
        Stretch(card, 60, 150, 60, 140);
        Img(card, _panel, Color.white, true, true);
        var body = Label(card, "BodyText", "", 28, GamePalette.Ink);
        body.textWrappingMode = TextWrappingModes.Normal;
        Stretch(body.rectTransform, 50, 40, 50, 40);
        var hint = Label(root, "HintText", "A partida terminou. Use SAIR para fechar o jogo.", 24, GamePalette.Muted);
        Place(hint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(80, 60), new Vector2(800, 40));
        Set(view, "titleText", title);
        Set(view, "bodyText", body);
        var so = new SerializedObject(view);
        so.FindProperty("bankruptcy").boolValue = bankruptcy;
        so.ApplyModifiedPropertiesWithoutUndo();
        return root.gameObject;
    }

    // =================================================================== registro
    private static void RegisterPlanning(PanelRegistry registry, GameObject planning)
    {
        var so = new SerializedObject(registry);
        var bindings = so.FindProperty("bindings");
        int index = -1;
        for (int i = 0; i < bindings.arraySize; i++)
            if (bindings.GetArrayElementAtIndex(i).FindPropertyRelative("id").intValue == (int)PanelId.Planning) index = i;
        if (index < 0)
        {
            index = bindings.arraySize;
            bindings.arraySize++;
        }
        var element = bindings.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("id").intValue = (int)PanelId.Planning;
        element.FindPropertyRelative("root").objectReferenceValue = planning;
        so.ApplyModifiedProperties();
    }

    // =================================================================== helpers
    private static bool LoadAssets()
    {
        _screen = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "screen-bg.png");
        _panel = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "panel-bg.png");
        _primary = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "btn-continuar-base.png");
        _secondary = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "btn-menu-solid.png");
        _topbar = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "topbar-bg.png");
        _white = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(FontGuid));
        bool ok = _screen && _panel && _primary && _secondary && _topbar && _white && _font;
        if (!ok)
            Debug.LogError($"[Simulação] Asset faltando: fundo={_screen != null} painel={_panel != null} primário={_primary != null} " +
                           $"secundário={_secondary != null} topo={_topbar != null} branco={_white != null} fonte={_font != null}");
        return ok;
    }

    /// Tela cheia: fundo opaco que bloqueia cliques e faixa do topo.
    private static RectTransform MakeScreen(Transform canvas, string name)
    {
        var root = Rect(name, canvas);
        Stretch(root);
        Img(root, _screen, Color.white, false, true);
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

    private static Button MakeButton(RectTransform rt, Sprite sprite, string label, Color labelColor, float size, out TextMeshProUGUI text)
    {
        var image = Img(rt, sprite, Color.white, true, true);
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
        button.colors = colors;
        text = Label(rt, "Label", label, size, labelColor, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 8, 4, 8, 4);
        text.raycastTarget = false;
        return button;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string content, float size, Color color,
                                         FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        var rt = Rect(name, parent);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
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
