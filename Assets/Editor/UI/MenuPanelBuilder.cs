#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Layout;
using Game.Adapter.In.UI.Navigation;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Constroi o Panel_Menu (mapa do hub) e os paineis de destino na GameScene.
    ///
    /// Como usar: Tools > Jogo > Construir Tudo.
    /// Pode rodar quantas vezes quiser: nada e duplicado e Ctrl+Z desfaz.
    /// </summary>
    public static class MenuPanelBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameScene.unity";

        // =====================================================
        //  DEFINICAO DOS NODES DO MAPA
        //  u,v = posicao normalizada sobre mapa-menu.png (v=0 embaixo).
        //  Cada node cai em cima do predio que representa.
        // =====================================================

        private sealed class NodeSpec
        {
            public string  ObjectName;
            public string  Title;
            public string  Subtitle;
            public string  AccentHex;
            public string  SoftHex;
            public string  IconName;
            public float   U;
            public float   V;
            public float   Width;
            public float   Height;
            public float   OffsetY;
            public PanelId Target;
            public string  LegacyName;
        }

        private static readonly NodeSpec[] Nodes =
        {
            new NodeSpec {
                ObjectName = "Node_Bank",  Title = "Banco", Subtitle = "credito e reserva",
                AccentHex = GamePalette.HexNodeBank, SoftHex = GamePalette.HexSoftBank, IconName = "Bank",
                U = 0.498f, V = 0.681f, Width = 306, Height = 172, OffsetY = -58,
                Target = PanelId.Financial
            },
            new NodeSpec {
                ObjectName = "Node_Store", Title = "Loja de Equipamentos", Subtitle = "infraestrutura e compras",
                AccentHex = GamePalette.HexNodeStore, SoftHex = GamePalette.HexSoftStore, IconName = "Toolbox",
                U = 0.800f, V = 0.591f, Width = 306, Height = 172, OffsetY = -58,
                Target = PanelId.EquipmentStore
            },
            new NodeSpec {
                ObjectName = "Node_Establishment", Title = "Meu Estabelecimento", Subtitle = "resumo e indicadores",
                AccentHex = GamePalette.HexNodeHome, SoftHex = GamePalette.HexSoftHome, IconName = "Home",
                U = 0.519f, V = 0.453f, Width = 372, Height = 200, OffsetY = -40,
                Target = PanelId.Establishment, LegacyName = "Node"
            },
            new NodeSpec {
                ObjectName = "Node_Rh", Title = "RH", Subtitle = "contratacao da equipe",
                AccentHex = GamePalette.HexNodeRh, SoftHex = GamePalette.HexSoftRh, IconName = "User",
                U = 0.210f, V = 0.603f, Width = 306, Height = 172, OffsetY = -58,
                Target = PanelId.Team
            },
            new NodeSpec {
                ObjectName = "Node_MenuPricing", Title = "Cardapio", Subtitle = "editavel ate confirmar",
                AccentHex = GamePalette.HexNodeMenu, SoftHex = GamePalette.HexSoftMenu, IconName = "List",
                U = 0.761f, V = 0.207f, Width = 306, Height = 172, OffsetY = -58,
                Target = PanelId.MenuPricing
            }
        };

        // =====================================================
        //  MENUS
        // =====================================================

        [MenuItem("Tools/Jogo/1 - Construir Tudo (preserva ajustes manuais)", false, 10)]
        public static void BuildAll()
        {
            RunAll(overwriteStyle: false,
                   "TUDO PRONTO. Nada que ja existia teve cor, fonte ou posicao alterada.");
        }

        [MenuItem("Tools/Jogo/2 - Corrigir areas de clique", false, 11)]
        public static void FixHitAreasOnly()
        {
            if (!PrepareScene(out var canvas)) return;

            UiFactory.BeginRun(overwriteExisting: false);

            int fixedCount = 0;

            foreach (var spec in Nodes)
            {
                var node = FindInScene(spec.ObjectName);

                if (node == null)
                    continue;

                var card = node.transform.Find("Card") as RectTransform;

                if (card == null)
                    continue;

                UiFactory.EnsureHitArea(card);
                fixedCount++;
            }

            int dead = UiFactory.WarnAboutDeadButtons(canvas);

            Finish($"Areas de clique garantidas em {fixedCount} nodes. "
                 + (dead == 0 ? "Nenhum botao morto restante." : $"Ainda ha {dead} botao(oes) sem alvo de raycast."));
        }

        [MenuItem("Tools/Jogo/3 - Reaplicar estilo: Meu Estabelecimento", false, 30)]
        public static void RestyleEstablishment()
        {
            if (!PrepareScene(out var canvas)) return;

            UiFactory.BeginRun(overwriteExisting: true);

            bool ok = Step("Panel_Establishment", () => BuildEstablishmentPanel(canvas));

            Finish(ok
                ? "Meu Estabelecimento reestilizado com a escala tipografica das suas telas."
                : "Falhou - veja a linha vermelha acima.");
        }

        [MenuItem("Tools/Jogo/4 - Reaplicar estilo: TUDO (sobrescreve o menu)", false, 31)]
        public static void RestyleEverything()
        {
            if (!EditorUtility.DisplayDialog(
                    "Sobrescrever os ajustes manuais?",
                    "Isto reaplica o estilo padrao em TODOS os objetos, inclusive nos nodes do "
                    + "Panel_Menu que voce ajustou na mao.\n\nAs cores, fontes e posicoes que voce "
                    + "escolheu serao perdidas.\n\nCtrl+Z desfaz, mas confirme so se for isso mesmo.",
                    "Sobrescrever tudo", "Cancelar"))
                return;

            RunAll(overwriteStyle: true, "Estilo padrao reaplicado em tudo.");
        }

        private static void RunAll(bool overwriteStyle, string successMessage)
        {
            if (!PrepareScene(out var canvas)) return;

            UiFactory.BeginRun(overwriteStyle);

            // Cada etapa e isolada. Se uma falhar, o Console diz EXATAMENTE qual
            // foi e as outras continuam - nada de parar no meio sem explicacao.
            bool ok = true;

            ok &= Step("Panel_Menu",            () => BuildPanelMenu(canvas));
            ok &= Step("Panel_Establishment",   () => BuildEstablishmentPanel(canvas));
            ok &= Step("Panel_Team",            () => BuildPlaceholderPanel(
                        canvas, "Panel_Team", "RH", "Contratacao e gestao da equipe",
                        GamePalette.HexNodeRh, GamePalette.HexSoftRh, "User",
                        "Aqui vao entrar os cards de Atendente, Garcom, Chapeiro, Chef, Sushiman e Gerente, "
                        + "com salario mensal e capacidade de atendimento vindos de RoleData."));
            ok &= Step("Panel_EquipmentStore",  () => BuildPlaceholderPanel(
                        canvas, "Panel_EquipmentStore", "Loja de Equipamentos", "Infraestrutura operacional",
                        GamePalette.HexNodeStore, GamePalette.HexSoftStore, "Toolbox",
                        "Aqui vao entrar os cards de equipamento com custo, bonus de qualidade e "
                        + "bonus de capacidade vindos de EquipmentData."));
            ok &= Step("Navegacao",             () => WireNavigation(canvas));
            ok &= Step("Botao Menu do Jogo",    () => BuildMenuAccess(canvas));
            ok &= Step("Isolar o Panel_Menu",   IsolatePanelMenu);

            int dead = UiFactory.WarnAboutDeadButtons(canvas);

            Finish(!ok
                ? "TERMINOU COM FALHA em pelo menos uma etapa - procure a linha vermelha acima."
                : dead > 0
                    ? successMessage + $" ATENCAO: {dead} botao(oes) sem alvo de raycast - veja os avisos amarelos."
                    : successMessage);
        }

        [MenuItem("Tools/Jogo/Ver apenas o Panel Menu", false, 30)]
        public static void IsolatePanelMenu()
        {
            IsolatePanel("Panel_Menu");
        }

        [MenuItem("Tools/Jogo/Ver apenas o Meu Estabelecimento", false, 31)]
        public static void IsolateEstablishment()
        {
            IsolatePanel("Panel_Establishment");
        }

        /// <summary>
        /// Deixa UM painel ativo e desliga os irmaos.
        /// Existe porque na Scene todos os paineis ocupam o mesmo retangulo:
        /// com varios ativos ao mesmo tempo o que voce ve e uma pilha ilegivel.
        /// </summary>
        private static void IsolatePanel(string panelName)
        {
            var target = FindInScene(panelName);

            if (target == null)
            {
                Debug.LogWarning($"[MenuPanelBuilder] '{panelName}' nao existe na cena.");
                return;
            }

            var parent = target.transform.parent;

            if (parent != null)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    var child = parent.GetChild(i);

                    if (!child.name.StartsWith("Panel_"))
                        continue;

                    Undo.RecordObject(child.gameObject, "Isolar painel");
                    child.gameObject.SetActive(child.gameObject == target);
                }
            }

            Selection.activeGameObject = target;
            SceneView.FrameLastActiveSceneView();

            Debug.Log($"[MenuPanelBuilder] Agora so o '{panelName}' esta ativo na Scene. "
                    + "Os outros paineis foram desligados so para voce enxergar - "
                    + "no Play quem manda e o UIStateListener + MenuNavigator.");
        }

        /// <summary>Roda uma etapa nomeada e transforma qualquer excecao em erro legivel.</summary>
        private static bool Step(string label, System.Action action)
        {
            try
            {
                action();
                Debug.Log($"[MenuPanelBuilder] OK - {label}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MenuPanelBuilder] FALHOU na etapa '{label}': {e.Message}\n{e.StackTrace}");
                return false;
            }
        }

        // =====================================================
        //  PREPARO
        // =====================================================

        private static bool PrepareScene(out RectTransform canvas)
        {
            canvas = null;

            // Rodar o builder em Play Mode nao adianta: tudo que ele constroi
            // some quando voce sai do Play.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Saia do Play Mode",
                    "O builder altera a cena, e qualquer alteracao feita durante o Play e descartada "
                    + "quando voce para o jogo.\n\nPare o Play (Ctrl+P) e rode de novo.",
                    "Entendi");
                return false;
            }

            var scene = SceneManager.GetActiveScene();

            if (scene.path != ScenePath)
            {
                if (!EditorUtility.DisplayDialog(
                        "Abrir a GameScene?",
                        "O builder trabalha na Assets/Scenes/GameScene.unity.\n\n"
                        + "Alteracoes nao salvas da cena atual serao perdidas se voce nao salvar antes.",
                        "Abrir GameScene", "Cancelar"))
                    return false;

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return false;

                EditorSceneManager.OpenScene(ScenePath);
            }

            var panelMenu = FindInScene("Panel_Menu");

            if (panelMenu != null && panelMenu.transform.parent != null)
            {
                canvas = panelMenu.transform.parent as RectTransform;
            }
            else
            {
                var found = Object.FindFirstObjectByType<Canvas>();

                if (found != null)
                    canvas = found.transform as RectTransform;
            }

            if (canvas == null)
            {
                Debug.LogError("[MenuPanelBuilder] Nao encontrei o Canvas raiz da GameScene.");
                return false;
            }

            UiFactory.EnsureButtonSpriteBorder();
            return true;
        }

        private static void Finish(string message)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[MenuPanelBuilder] {message} Lembre de salvar a cena (Ctrl+S).");
        }

        private static GameObject FindInScene(string name)
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name == name)
                    return go;
            }

            return null;
        }

        // =====================================================
        //  PANEL MENU
        // =====================================================

        private static void BuildPanelMenu(RectTransform canvas)
        {
            var panel = UiFactory.EnsureChild(canvas, "Panel_Menu");
            UiFactory.Stretch(panel);

            // --- fundo solido da paleta, atras do mapa ---
            var backdrop = UiFactory.EnsureChild(panel, "Backdrop");
            backdrop.SetSiblingIndex(0);
            UiFactory.Stretch(backdrop);
            UiFactory.Solid(backdrop, GamePalette.Background, raycast: true);

            // --- mapa isometrico esticado na tela inteira ---
            var mapBackground = UiFactory.EnsureChild(panel, "MapBackground");
            mapBackground.SetSiblingIndex(1);
            UiFactory.Stretch(mapBackground);
            UiFactory.Picture(mapBackground, UiFactory.MapSprite(), Color.white, preserveAspect: false);

            // --- veu claro para os nodes lerem bem sobre a arte ---
            var scrim = UiFactory.EnsureChild(panel, "MapScrim");
            scrim.SetSiblingIndex(2);
            UiFactory.Stretch(scrim);
            UiFactory.Solid(scrim, GamePalette.MapScrim);

            // --- camada dos nodes ---
            var mapLayer = UiFactory.EnsureChild(panel, "MapLayer");
            mapLayer.SetSiblingIndex(3);
            UiFactory.Stretch(mapLayer);

            BuildTopBar(mapLayer);

            foreach (var spec in Nodes)
                BuildNode(mapLayer, spec);
        }

        private static void BuildTopBar(RectTransform mapLayer)
        {
            var topBar = UiFactory.EnsureChild(mapLayer, "TopBar");
            topBar.SetAsFirstSibling();
            UiFactory.TopBand(topBar, 136f);
            UiFactory.Solid(topBar, GamePalette.Chrome, raycast: true);

            var hairline = UiFactory.EnsureChild(topBar, "Hairline");
            UiFactory.BottomBand(hairline, 2f);
            UiFactory.Solid(hairline, GamePalette.Hairline);

            // A barra sangra ate a borda; o conteudo dela fica dentro da area
            // segura. As pills encostavam na borda direita, bem onde mora o
            // furo da camera em landscape.
            UiFactory.RetireLegacy(topBar, "CashPill",  "CashP");
            UiFactory.RetireLegacy(topBar, "ScorePill", "ScoreP");
            UiFactory.RetireAlways(topBar, "MenuTitle");

            var safeRow = UiFactory.EnsureChild(topBar, "Safe");
            UiFactory.Stretch(safeRow);
            var safeFitter = UiFactory.Ensure<SafeAreaFitter>(safeRow.gameObject);
            UiFactory.SetPrivateBool(safeFitter, "applyTop", false);
            UiFactory.SetPrivateBool(safeFitter, "applyBottom", false);
            EditorUtility.SetDirty(safeFitter);

            // ---- marca a esquerda ----
            var mark = UiFactory.EnsureChild(safeRow, "Mark");
            UiFactory.AnchorCorner(mark, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 64, 64, 32, 0);
            UiFactory.Panel(mark, GamePalette.Primary, GamePalette.PpuButton, raycast: false);

            var markIcon = UiFactory.EnsureChild(mark, "Icon");
            UiFactory.Stretch(markIcon, 13, 13, 13, 13);
            UiFactory.Picture(markIcon, UiFactory.Icon("Map"), GamePalette.OnPrimary);

            var title = UiFactory.EnsureChild(safeRow, "MenuTitle");
            UiFactory.AnchorCorner(title, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 620, 50, 116, 18);
            UiFactory.Text(title, "Menu do Jogo", GameTypography.MapBarTitle, FontStyles.Bold, GamePalette.Ink, TextAlignmentOptions.MidlineLeft);

            var subtitle = UiFactory.EnsureChild(safeRow, "MenuSubtitle");
            UiFactory.AnchorCorner(subtitle, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 620, 42, 116, -24);
            UiFactory.Text(subtitle, "Areas revisitaveis do seu negocio", GameTypography.MapBarSubtitle, FontStyles.Normal,
                           GamePalette.InkCaption, TextAlignmentOptions.MidlineLeft);

            // ---- pills a direita ----
            BuildPill(safeRow, "CashPill", "Caixa", "R$ 0", GamePalette.HexMoney, "Money",
                      -32f, "CashP", out var cashValue);

            BuildPill(safeRow, "ScorePill", "Score", "0", GamePalette.HexXp, "Star",
                      -398f, "ScoreP", out var scoreValue);

            BuildPill(safeRow, "RoundPill", "", "Rodada 1", GamePalette.HexInkBody, "Calendar",
                      -764f, "RoundP", out var roundValue);

            var hud = UiFactory.Ensure<MenuHudView>(topBar.gameObject);
            UiFactory.SetPrivate(hud, "scoreValue", scoreValue);
            UiFactory.SetPrivate(hud, "cashValue",  cashValue);
            UiFactory.SetPrivate(hud, "roundValue", roundValue);
        }

        private static RectTransform BuildPill(RectTransform parent, string name, string caption, string value,
                                               string accentHex, string iconName, float x, string legacyPrefix,
                                               out TextMeshProUGUI valueLabel)
        {
            var pill = UiFactory.EnsureChildByPrefix(parent, name, legacyPrefix);
            UiFactory.AnchorCorner(pill, new Vector2(1, 0.5f), new Vector2(1, 0.5f), 350, 78, x, 0);
            UiFactory.Panel(pill, GamePalette.Surface, GamePalette.PpuPill, raycast: false);

            var border = UiFactory.EnsureChild(pill, "Border");
            UiFactory.Stretch(border);
            var borderImage = UiFactory.Panel(border, GamePalette.Hairline, GamePalette.PpuPill, raycast: false);
            borderImage.fillCenter = false;

            var icon = UiFactory.EnsureChild(pill, "Icon");
            UiFactory.AnchorCorner(icon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 36, 36, 26, 0);
            UiFactory.Picture(icon, UiFactory.Icon(iconName), GamePalette.Parse(accentHex));

            var captionLabel = UiFactory.EnsureChild(pill, "Caption");
            UiFactory.AnchorCorner(captionLabel, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 150, 40, 72, 0);
            UiFactory.Text(captionLabel, caption, GameTypography.PillCaption, FontStyles.Normal, GamePalette.InkBody,
                           TextAlignmentOptions.MidlineLeft);

            var valueRt = UiFactory.EnsureChild(pill, "Value");
            UiFactory.AnchorCorner(valueRt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), 240, 42, -26, 0);
            valueLabel = UiFactory.Text(valueRt, value, GameTypography.PillValue, FontStyles.Bold, GamePalette.Parse(accentHex),
                                        TextAlignmentOptions.MidlineRight);

            return pill;
        }

        private static void BuildNode(RectTransform mapLayer, NodeSpec spec)
        {
            // O "Node" da cena antiga tinha Image, Button e MapMenuNode na propria
            // raiz. Na estrutura nova a raiz e so um container e esses componentes
            // vivem no filho "Card". Reaproveitar geraria dois botoes empilhados,
            // entao aposentamos o antigo (desativado, nao apagado) e construimos limpo.
            if (!string.IsNullOrEmpty(spec.LegacyName))
                UiFactory.RetireAlways(mapLayer, spec.LegacyName);

            var node = UiFactory.EnsureChild(mapLayer, spec.ObjectName);

            UiFactory.AnchorNormalized(node, spec.U, spec.V, spec.Width, spec.Height, 0, spec.OffsetY);

            var accent = GamePalette.Parse(spec.AccentHex);

            // ---- halo (fica ATRAS do card por ser o primeiro filho) ----
            var glow = UiFactory.EnsureChild(node, "GlowOverlay");
            glow.SetAsFirstSibling();
            UiFactory.Stretch(glow, -22, -22, -22, -22);
            var glowImage = UiFactory.Panel(glow, new Color(accent.r, accent.g, accent.b, 0f),
                                            GamePalette.PpuNodeBig, raycast: false);

            // ---- card ----
            var card = UiFactory.EnsureChild(node, "Card");
            UiFactory.Stretch(card);
            UiFactory.Panel(card, GamePalette.Surface, GamePalette.PpuNodeBig, raycast: true);

            // Alvo de clique dedicado. Sem isto, trocar o visual do card (por
            // exemplo apagando o Image de fundo) mata o botao silenciosamente.
            UiFactory.EnsureHitArea(card);

            var cardBorder = UiFactory.EnsureChild(card, "Border");
            UiFactory.Stretch(cardBorder);
            var cardBorderImage = UiFactory.Panel(cardBorder, GamePalette.Hairline, GamePalette.PpuNodeBig, raycast: false);
            cardBorderImage.fillCenter = false;

            // ---- badge do icone ----
            var badge = UiFactory.EnsureChild(card, "IconBadge");
            UiFactory.AnchorCorner(badge, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 68, 68, 0, -18);
            UiFactory.Panel(badge, GamePalette.Parse(spec.SoftHex), GamePalette.PpuButton, raycast: false);

            var icon = UiFactory.EnsureChild(badge, "Icon");
            UiFactory.Stretch(icon, 17, 17, 17, 17);
            UiFactory.Picture(icon, UiFactory.Icon(spec.IconName), accent);

            // ---- textos ----
            var title = UiFactory.EnsureChildRenamed(card, "Title", "MeTitle");
            UiFactory.AnchorCorner(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), spec.Width - 20, 36, 0, -92);
            UiFactory.Text(title, spec.Title, GameTypography.NodeTitle, FontStyles.Bold, GamePalette.Ink, TextAlignmentOptions.Center);

            var subtitle = UiFactory.EnsureChild(card, "Subtitle");
            UiFactory.AnchorCorner(subtitle, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), spec.Width - 24, 30, 0, -128);
            UiFactory.Text(subtitle, spec.Subtitle, GameTypography.NodeSubtitle, FontStyles.Normal, GamePalette.InkCaption,
                           TextAlignmentOptions.Center);

            // ---- comportamento ----
            UiFactory.MakeButton(card, highlightedTint: 1.16f, pressedTint: 0.92f);

            var mapNode = UiFactory.Ensure<MapMenuNode>(card.gameObject);
            UiFactory.SetPrivate(mapNode, "glowOverlay", glowImage);
            UiFactory.SetPrivateString(mapNode, "colorHex", spec.AccentHex);

            var nav = UiFactory.Ensure<PanelNavButton>(card.gameObject);
            nav.EditorConfigure(PanelNavButton.NavAction.Open, spec.Target);
            EditorUtility.SetDirty(nav);
        }

        // =====================================================
        //  PAINEL MEU ESTABELECIMENTO
        // =====================================================

        // Medidas do layout mobile. Concentradas aqui para ajuste rapido.
        private const float HeaderHeight  = 208f;  // mesma altura do header do Panel_Financial
        private const float FrameMaxWidth = 1800f; // trava a largura no landscape ultrawide
        private const float FrameMargin   = 56f;
        private const float KpiHeight     = 280f;
        private const float GridGap       = 22f;
        private const int   ChipColumns   = 4;

        private static void BuildEstablishmentPanel(RectTransform canvas)
        {
            var panel = UiFactory.EnsureChild(canvas, "Panel_Establishment");
            UiFactory.Stretch(panel);
            UiFactory.Solid(panel, GamePalette.Background, raycast: true);

            var subtitleLabel = BuildHeader(panel, "Meu Estabelecimento", "Rodada 1",
                                            GamePalette.HexNodeHome, GamePalette.HexSoftHome, "Home");

            // Estrutura antiga (duas colunas de linhas full-width) foi aposentada.
            UiFactory.RetireAlways(panel, "Content");

            // SafeArea segura o conteudo longe do furo da camera e da barra de
            // gestos. O fundo do painel continua sangrando ate a borda.
            var safeArea = UiFactory.EnsureChild(panel, "SafeArea");
            UiFactory.Stretch(safeArea);
            var safe = UiFactory.Ensure<SafeAreaFitter>(safeArea.gameObject);
            EditorUtility.SetDirty(safe);

            // ContentFrame trava a largura maxima e centraliza. Sem isso, num
            // celular landscape o canvas tem ~2376 de largura e tudo se afasta.
            var frame = UiFactory.EnsureChild(safeArea, "ContentFrame");
            frame.anchorMin = new Vector2(0f, 0f);
            frame.anchorMax = new Vector2(1f, 1f);
            frame.offsetMin = new Vector2(FrameMargin, 40f);
            frame.offsetMax = new Vector2(-FrameMargin, -(HeaderHeight + 24f));
            var fitter = UiFactory.Ensure<MaxWidthFitter>(frame.gameObject);
            fitter.EditorConfigure(FrameMaxWidth, FrameMargin);
            EditorUtility.SetDirty(fitter);

            // ---------- FAIXA DE INDICADORES ----------
            var kpiRow = UiFactory.EnsureChild(frame, "KpiRow");
            UiFactory.TopBand(kpiRow, KpiHeight);

            var vScore = BuildKpiTile(kpiRow, "Tile_Score", "Score", "0", GamePalette.HexXp, 0, out var barFill);
            var vCash    = BuildKpiTile(kpiRow, "Tile_Cash",    "Caixa disponivel", "R$ 0", GamePalette.HexMoney, 1, out _);
            var vRevenue = BuildKpiTile(kpiRow, "Tile_Revenue", "Receita estimada", "R$ 0", GamePalette.HexInk,   2, out _);
            var vResult  = BuildKpiTile(kpiRow, "Tile_Result",  "Receita mensal",   "R$ 0", GamePalette.HexMoney, 3, out _);

            // ---------- CARD RESUMO ----------
            var summaryCard = UiFactory.EnsureChild(frame, "Card_Summary");
            UiFactory.Stretch(summaryCard, 0, KpiHeight + GridGap, 0, 0);
            UiFactory.Panel(summaryCard, GamePalette.Surface, GamePalette.PpuCard, raycast: false);

            var summaryBorder = UiFactory.EnsureChild(summaryCard, "Border");
            UiFactory.Stretch(summaryBorder);
            UiFactory.Panel(summaryBorder, GamePalette.Hairline, GamePalette.PpuCard, raycast: false).fillCenter = false;

            BuildCardTitle(summaryCard, "Resumo da empresa", "List", GamePalette.HexPrimary, GamePalette.HexPrimarySoft);

            var chips = UiFactory.EnsureChild(summaryCard, "Chips");
            UiFactory.Stretch(chips, 30, 138, 30, 122);

            var vLocation   = BuildChip(chips, "Chip_Location",   "Localizacao",  "Pendente", "Map",        GamePalette.HexPrimary,  0);
            var vRestaurant = BuildChip(chips, "Chip_Restaurant", "Restaurante",  "Pendente", "Shop",       GamePalette.HexWarn,     1);
            var vSegment    = BuildChip(chips, "Chip_Segment",    "Publico alvo", "Pendente", "User",       GamePalette.HexXp,       2);
            var vMenu       = BuildChip(chips, "Chip_Menu",       "Cardapio",     "Pendente", "List",       GamePalette.HexNodeMenu, 3);
            var vPrice      = BuildChip(chips, "Chip_Price",      "Ticket medio", "Pendente", "Money",      GamePalette.HexMoney,    4);
            var vEquipment  = BuildChip(chips, "Chip_Equipment",  "Equipamentos", "Nenhum",   "Toolbox",    GamePalette.HexWarn,     5);
            var vTeam       = BuildChip(chips, "Chip_Team",       "Equipe",       "Ninguem",  "Add_Friend", GamePalette.HexXp,       6);
            var vCoherence  = BuildChip(chips, "Chip_Coherence",  "Coerencia",    "A calcular", "Piechart", GamePalette.HexInkBody,  7);

            var noticeRt = UiFactory.EnsureChild(summaryCard, "Notice");
            UiFactory.BottomBand(noticeRt, 88f, 30, 30, 22);
            var vNotice = UiFactory.Text(noticeRt, "Projecao para o mes corrente com base nas escolhas atuais.",
                                         GameTypography.Caption, FontStyles.Normal, GamePalette.Muted,
                                         TextAlignmentOptions.MidlineLeft, wrap: true);

            // ---------- WIRING ----------
            var view = UiFactory.Ensure<EstablishmentSummaryView>(panel.gameObject);
            UiFactory.SetPrivate(view, "locationValue",      vLocation);
            UiFactory.SetPrivate(view, "restaurantValue",    vRestaurant);
            UiFactory.SetPrivate(view, "segmentValue",       vSegment);
            UiFactory.SetPrivate(view, "menuValue",          vMenu);
            UiFactory.SetPrivate(view, "priceValue",         vPrice);
            UiFactory.SetPrivate(view, "equipmentValue",     vEquipment);
            UiFactory.SetPrivate(view, "teamValue",          vTeam);
            UiFactory.SetPrivate(view, "scoreValue",         vScore);
            UiFactory.SetPrivate(view, "cashValue",          vCash);
            UiFactory.SetPrivate(view, "revenueValue",       vRevenue);
            UiFactory.SetPrivate(view, "monthlyResultValue", vResult);
            UiFactory.SetPrivate(view, "scoreBarFill",       barFill);
            UiFactory.SetPrivate(view, "coherenceValue",     vCoherence);
            UiFactory.SetPrivate(view, "roundValue",         subtitleLabel);
            UiFactory.SetPrivate(view, "noticeText",         vNotice);

            var controller = UiFactory.Ensure<EstablishmentSummaryController>(panel.gameObject);
            UiFactory.SetPrivate(controller, "view", view);

            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// Tile de indicador: faixa de cor na esquerda, rotulo pequeno em caixa
        /// alta e o numero grande logo abaixo. O conteudo e centralizado na
        /// vertical, entao o tile pode crescer sem deixar buraco.
        /// </summary>
        private static TextMeshProUGUI BuildKpiTile(RectTransform row, string name, string caption, string value,
                                                    string accentHex, int column, out Image scoreBar)
        {
            scoreBar = null;

            var tile = UiFactory.EnsureChild(row, name);
            SetColumn(tile, column, 4);
            UiFactory.Panel(tile, GamePalette.Surface, GamePalette.PpuCard, raycast: false);

            var border = UiFactory.EnsureChild(tile, "Border");
            UiFactory.Stretch(border);
            UiFactory.Panel(border, GamePalette.Hairline, GamePalette.PpuCard, raycast: false).fillCenter = false;

            var stripe = UiFactory.EnsureChild(tile, "Stripe");
            stripe.anchorMin = new Vector2(0f, 0f);
            stripe.anchorMax = new Vector2(0f, 1f);
            stripe.pivot     = new Vector2(0f, 0.5f);
            stripe.offsetMin = new Vector2(0f, 22f);
            stripe.offsetMax = new Vector2(9f, -22f);
            UiFactory.Panel(stripe, GamePalette.Parse(accentHex), GamePalette.PpuPill, raycast: false);

            var captionRt = UiFactory.EnsureChild(tile, "Caption");
            UiFactory.AnchorCorner(captionRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 400, 42, 38, 58);
            var captionLabel = UiFactory.Text(captionRt, caption.ToUpperInvariant(), GameTypography.Caption, FontStyles.Bold,
                                              GamePalette.InkCaption, TextAlignmentOptions.MidlineLeft);
            captionLabel.characterSpacing = 3f;

            var valueRt = UiFactory.EnsureChild(tile, "Value");
            UiFactory.AnchorCorner(valueRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 400, 78, 38, -14);
            var label = UiFactory.Text(valueRt, value, GameTypography.DisplayValue, FontStyles.Bold, GamePalette.Parse(accentHex),
                                       TextAlignmentOptions.MidlineLeft);

            // Barra de progresso so no tile de Score.
            if (name == "Tile_Score")
            {
                var track = UiFactory.EnsureChild(tile, "ScoreBarTrack");
                UiFactory.BottomBand(track, 12f, 34, 30, 26);
                UiFactory.Panel(track, GamePalette.SurfaceAlt, GamePalette.PpuPill, raycast: false);

                var fillRt = UiFactory.EnsureChild(track, "Fill");
                UiFactory.Stretch(fillRt);
                scoreBar = UiFactory.Panel(fillRt, GamePalette.Xp, GamePalette.PpuPill, raycast: false);
                scoreBar.type       = Image.Type.Filled;
                scoreBar.fillMethod = Image.FillMethod.Horizontal;
                scoreBar.fillOrigin = (int)Image.OriginHorizontal.Left;
                scoreBar.fillAmount = 0f;
            }

            return label;
        }

        /// <summary>
        /// Chip do resumo: icone a esquerda, rotulo pequeno e valor grande a
        /// 4px um do outro - nao a 800px como na versao anterior.
        /// </summary>
        private static TextMeshProUGUI BuildChip(RectTransform grid, string name, string caption, string value,
                                                 string iconName, string accentHex, int index)
        {
            int column = index % ChipColumns;
            int rowIndex = index / ChipColumns;

            var chip = UiFactory.EnsureChild(grid, name);
            SetCell(chip, column, ChipColumns, rowIndex, 2);
            UiFactory.Panel(chip, GamePalette.SurfaceAlt, GamePalette.PpuCard, raycast: false);

            var accent = GamePalette.Parse(accentHex);

            var badge = UiFactory.EnsureChild(chip, "IconBadge");
            UiFactory.AnchorCorner(badge, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 80, 80, 28, 0);
            UiFactory.Panel(badge, new Color(accent.r, accent.g, accent.b, 0.20f), GamePalette.PpuButton, raycast: false);

            var icon = UiFactory.EnsureChild(badge, "Icon");
            UiFactory.Stretch(icon, 16, 16, 16, 16);
            UiFactory.Picture(icon, UiFactory.Icon(iconName), accent);

            var captionRt = UiFactory.EnsureChild(chip, "Caption");
            UiFactory.AnchorCorner(captionRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 340, 42, 122, 36);
            var captionLabel = UiFactory.Text(captionRt, caption.ToUpperInvariant(), GameTypography.Caption, FontStyles.Bold,
                                              GamePalette.InkCaption, TextAlignmentOptions.MidlineLeft);
            captionLabel.characterSpacing = 3f;

            var valueRt = UiFactory.EnsureChild(chip, "Value");
            UiFactory.AnchorCorner(valueRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 360, 66, 122, -26);
            return UiFactory.Text(valueRt, value, GameTypography.SectionTitle, FontStyles.Bold, GamePalette.Ink,
                                  TextAlignmentOptions.MidlineLeft);
        }

        /// <summary>Coluna de uma grade horizontal, com meia calha de cada lado.</summary>
        private static void SetColumn(RectTransform rt, int column, int columns)
        {
            float unit = 1f / columns;

            rt.anchorMin = new Vector2(column * unit, 0f);
            rt.anchorMax = new Vector2((column + 1) * unit, 1f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(column == 0 ? 0f : GridGap * 0.5f, 0f);
            rt.offsetMax = new Vector2(column == columns - 1 ? 0f : -GridGap * 0.5f, 0f);
        }

        /// <summary>
        /// Celula de uma grade. Usa ancoras fracionarias tambem na vertical,
        /// entao a grade preenche a altura disponivel sozinha - e o que mata
        /// os 40% de espaco morto do layout anterior.
        /// </summary>
        private static void SetCell(RectTransform rt, int column, int columns, int row, int rows)
        {
            float unitX = 1f / columns;
            float unitY = 1f / rows;

            // row 0 = topo, entao invertemos porque y cresce para cima.
            float yMax = 1f - row * unitY;
            float yMin = yMax - unitY;

            rt.anchorMin = new Vector2(column * unitX, yMin);
            rt.anchorMax = new Vector2((column + 1) * unitX, yMax);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(column == 0 ? 0f : GridGap * 0.5f,
                                       row == rows - 1 ? 0f : GridGap * 0.5f);
            rt.offsetMax = new Vector2(column == columns - 1 ? 0f : -GridGap * 0.5f,
                                       row == 0 ? 0f : -GridGap * 0.5f);
        }

        private static void BuildCardTitle(RectTransform card, string text, string iconName,
                                           string accentHex, string softHex)
        {
            var header = UiFactory.EnsureChild(card, "CardTitle");
            UiFactory.TopBand(header, 92f, 30, 30, -26);

            var badge = UiFactory.EnsureChild(header, "Badge");
            UiFactory.AnchorCorner(badge, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 66, 66, 0, 0);
            UiFactory.Panel(badge, GamePalette.Parse(softHex), GamePalette.PpuButton, raycast: false);

            var icon = UiFactory.EnsureChild(badge, "Icon");
            UiFactory.Stretch(icon, 12, 12, 12, 12);
            UiFactory.Picture(icon, UiFactory.Icon(iconName), GamePalette.Parse(accentHex));

            var label = UiFactory.EnsureChild(header, "Label");
            UiFactory.AnchorCorner(label, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 700, 64, 84, 0);
            UiFactory.Text(label, text, GameTypography.SectionTitle, FontStyles.Bold, GamePalette.Ink, TextAlignmentOptions.MidlineLeft);
        }

        // =====================================================
        //  PAINEIS PLACEHOLDER (RH E LOJA)
        // =====================================================

        private static void BuildPlaceholderPanel(RectTransform canvas, string objectName, string title,
                                                  string subtitle, string accentHex, string softHex,
                                                  string iconName, string description)
        {
            var panel = UiFactory.EnsureChild(canvas, objectName);
            UiFactory.Stretch(panel);
            UiFactory.Solid(panel, GamePalette.Background, raycast: true);

            BuildHeader(panel, title, subtitle, accentHex, softHex, iconName);

            var content = UiFactory.EnsureChild(panel, "Content");
            UiFactory.Stretch(content, 56, HeaderHeight + 24f, 56, 40);
            UiFactory.Panel(content, GamePalette.SurfaceAlt, GamePalette.PpuCard, raycast: false);

            var dashed = UiFactory.EnsureChild(content, "Border");
            UiFactory.Stretch(dashed);
            UiFactory.Panel(dashed, GamePalette.BorderStrong, GamePalette.PpuCard, raycast: false).fillCenter = false;

            var badge = UiFactory.EnsureChild(content, "Badge");
            UiFactory.AnchorCorner(badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 92, 92, 0, 96);
            UiFactory.Panel(badge, GamePalette.Parse(softHex), GamePalette.PpuButton, raycast: false);

            var badgeIcon = UiFactory.EnsureChild(badge, "Icon");
            UiFactory.Stretch(badgeIcon, 24, 24, 24, 24);
            UiFactory.Picture(badgeIcon, UiFactory.Icon(iconName), GamePalette.Parse(accentHex));

            var headline = UiFactory.EnsureChild(content, "Headline");
            UiFactory.AnchorCorner(headline, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 900, 40, 0, 12);
            UiFactory.Text(headline, "Tela em construcao", GameTypography.SectionTitle, FontStyles.Bold, GamePalette.Ink,
                           TextAlignmentOptions.Center);

            var body = UiFactory.EnsureChild(content, "Body");
            UiFactory.AnchorCorner(body, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 900, 90, 0, -46);
            UiFactory.Text(body, description, GameTypography.Body, FontStyles.Normal, GamePalette.InkBody,
                           TextAlignmentOptions.Top, wrap: true);

            panel.gameObject.SetActive(false);
        }

        // =====================================================
        //  HEADER PADRAO COM BOTAO VOLTAR
        // =====================================================

        /// <summary>Monta o header e devolve o label do subtitulo, para quem quiser bindar nele.</summary>
        private static TextMeshProUGUI BuildHeader(RectTransform panel, string title, string subtitle,
                                                   string accentHex, string softHex, string iconName)
        {
            var headerBar = UiFactory.EnsureChild(panel, "Header");
            headerBar.SetAsFirstSibling();
            UiFactory.TopBand(headerBar, HeaderHeight);
            UiFactory.Solid(headerBar, GamePalette.Chrome, raycast: true);

            var hairline = UiFactory.EnsureChild(headerBar, "Hairline");
            UiFactory.BottomBand(hairline, 2f);
            UiFactory.Solid(hairline, GamePalette.Hairline);

            // A versao anterior colocava estes filhos direto na barra. Agora eles
            // vivem dentro do HeaderSafe, entao os antigos precisam sair de cena
            // para nao aparecerem duplicados.
            UiFactory.RetireAlways(headerBar, "BackButton");
            UiFactory.RetireAlways(headerBar, "Badge");
            UiFactory.RetireAlways(headerBar, "Title");
            UiFactory.RetireAlways(headerBar, "Subtitle");
            UiFactory.RetireAlways(headerBar, "MapButton");

            // A barra sangra ate a borda, mas o conteudo respeita a area segura
            // nas laterais - em landscape o notch fica justamente ali.
            var header = UiFactory.EnsureChild(headerBar, "HeaderSafe");
            UiFactory.Stretch(header);
            var headerSafe = UiFactory.Ensure<SafeAreaFitter>(header.gameObject);
            UiFactory.SetPrivateBool(headerSafe, "applyTop", false);
            UiFactory.SetPrivateBool(headerSafe, "applyBottom", false);
            EditorUtility.SetDirty(headerSafe);

            // ---- botao voltar ----
            var back = UiFactory.EnsureChild(header, "BackButton");
            UiFactory.AnchorCorner(back, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 240, 86, 32, 0);
            UiFactory.Panel(back, GamePalette.SurfaceAlt, GamePalette.PpuButton, raycast: true);
            UiFactory.EnsureHitArea(back);

            var backIcon = UiFactory.EnsureChild(back, "Icon");
            UiFactory.AnchorCorner(backIcon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 32, 32, 26, 0);
            UiFactory.Picture(backIcon, UiFactory.Icon("Arrow_Prev"), GamePalette.Muted);

            var backLabel = UiFactory.EnsureChild(back, "Label");
            UiFactory.AnchorCorner(backLabel, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 160, 44, 70, 0);
            UiFactory.Text(backLabel, "Voltar", GameTypography.Body, FontStyles.Bold, GamePalette.Muted,
                           TextAlignmentOptions.MidlineLeft);

            UiFactory.MakeButton(back, highlightedTint: 1.18f, pressedTint: 0.90f);

            var backNav = UiFactory.Ensure<PanelNavButton>(back.gameObject);
            backNav.EditorConfigure(PanelNavButton.NavAction.Back, PanelId.None);
            EditorUtility.SetDirty(backNav);

            // ---- identidade da tela ----
            var badge = UiFactory.EnsureChild(header, "Badge");
            UiFactory.AnchorCorner(badge, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 84, 84, 300, 0);
            UiFactory.Panel(badge, GamePalette.Parse(softHex), GamePalette.PpuButton, raycast: false);

            var badgeIcon = UiFactory.EnsureChild(badge, "Icon");
            UiFactory.Stretch(badgeIcon, 14, 14, 14, 14);
            UiFactory.Picture(badgeIcon, UiFactory.Icon(iconName), GamePalette.Parse(accentHex));

            var titleRt = UiFactory.EnsureChild(header, "Title");
            UiFactory.AnchorCorner(titleRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 1000, 96, 402, 26);
            UiFactory.Text(titleRt, title, GameTypography.ScreenTitle, FontStyles.Bold, GamePalette.Ink, TextAlignmentOptions.MidlineLeft);

            var subtitleRt = UiFactory.EnsureChild(header, "Subtitle");
            UiFactory.AnchorCorner(subtitleRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 1000, 48, 402, -42);
            var subtitleLabel = UiFactory.Text(subtitleRt, subtitle, GameTypography.ScreenHint, FontStyles.Normal, GamePalette.InkCaption,
                                               TextAlignmentOptions.MidlineLeft);

            // O "Menu do Jogo" que ficava aqui saiu: agora existe um botao
            // flutuante unico no Canvas, que aparece em TODAS as telas.
            // Dois botoes iguais no mesmo canto so confundiriam.
            return subtitleLabel;
        }

        // =====================================================
        //  BOTAO FLUTUANTE "MENU DO JOGO"
        // =====================================================

        private static void BuildMenuAccess(RectTransform canvas)
        {
            var access = UiFactory.EnsureChild(canvas, "MenuAccess");
            UiFactory.Stretch(access);
            access.SetAsLastSibling(); // sempre por cima de todos os paineis

            var safe = UiFactory.Ensure<SafeAreaFitter>(access.gameObject);
            EditorUtility.SetDirty(safe);

            var button = UiFactory.EnsureChild(access, "Button");
            UiFactory.AnchorCorner(button, new Vector2(1, 1), new Vector2(1, 1), 360, 86, -32, -28);
            UiFactory.Panel(button, GamePalette.Warn, GamePalette.PpuButton, raycast: true);
            UiFactory.EnsureHitArea(button);

            var icon = UiFactory.EnsureChild(button, "Icon");
            UiFactory.AnchorCorner(icon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 34, 34, 28, 0);
            UiFactory.Picture(icon, UiFactory.Icon("Map"), GamePalette.Parse(GamePalette.HexOnWarn));

            var label = UiFactory.EnsureChild(button, "Label");
            UiFactory.AnchorCorner(label, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 280, 46, 76, 0);
            UiFactory.Text(label, "Menu do Jogo", GameTypography.Body, FontStyles.Bold, GamePalette.Parse(GamePalette.HexOnWarn),
                           TextAlignmentOptions.MidlineLeft);

            UiFactory.MakeButton(button, highlightedTint: 1.12f, pressedTint: 0.88f);

            var nav = UiFactory.Ensure<PanelNavButton>(button.gameObject);
            nav.EditorConfigure(PanelNavButton.NavAction.OpenMenu, PanelId.Menu);
            EditorUtility.SetDirty(nav);

            var accessComponent = UiFactory.Ensure<MenuAccessButton>(access.gameObject);
            UiFactory.SetPrivate(accessComponent, "buttonRoot", button.gameObject);
            UiFactory.SetPrivate(accessComponent, "menuPanel", FindInScene("Panel_Menu"));
            UiFactory.SetPrivateBool(accessComponent, "visibleDuringInitialSetup", false);
            EditorUtility.SetDirty(accessComponent);

            Debug.Log("[MenuPanelBuilder] Botao flutuante 'Menu do Jogo' criado em Canvas > MenuAccess (topo-direita). "
                    + "Aparece em cima de qualquer painel, some no proprio mapa e fica oculto durante "
                    + "as decisoes iniciais (D1-D3 e Revisao).");
        }

        // =====================================================
        //  NAVEGACAO
        // =====================================================

        private static void WireNavigation(RectTransform canvas)
        {
            var navRoot = UiFactory.EnsureChild(canvas, "UiNavigation");
            UiFactory.AnchorCorner(navRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 100, 100, 0, 0);

            var registry  = UiFactory.Ensure<PanelRegistry>(navRoot.gameObject);
            var navigator = UiFactory.Ensure<MenuNavigator>(navRoot.gameObject);

            var map = new Dictionary<PanelId, string>
            {
                { PanelId.Menu,           "Panel_Menu" },
                { PanelId.Establishment,  "Panel_Establishment" },
                { PanelId.Financial,      "Panel_Financial" },
                { PanelId.MenuPricing,    "Panel_MenuPricing" },
                { PanelId.Team,           "Panel_Team" },
                { PanelId.EquipmentStore, "Panel_EquipmentStore" }
            };

            foreach (var pair in map)
            {
                var go = FindInScene(pair.Value);

                if (go == null)
                {
                    Debug.LogWarning($"[MenuPanelBuilder] Painel '{pair.Value}' nao existe na cena. "
                                   + $"O PanelId {pair.Key} ficou sem destino.");
                    continue;
                }

                registry.EditorSetBinding(pair.Key, go);
            }

            EditorUtility.SetDirty(registry);

            UiFactory.SetPrivate(navigator, "registry", registry);
            EditorUtility.SetDirty(navigator);

            // O UIStateListener continua dono do fluxo por GameState. A unica
            // ponte que precisamos e: estado Management_Hub => mostrar o mapa.
            // Sem isso o Panel_Menu nunca aparece sozinho, e com isso o
            // MenuNavigator assume dali em diante sem disputar controle.
            var listener = Object.FindFirstObjectByType<UIStateListener>(FindObjectsInactive.Include);
            var menuPanel = FindInScene("Panel_Menu");

            if (listener != null && menuPanel != null)
            {
                UiFactory.SetPrivate(listener, "managementHubPanel", menuPanel);
                EditorUtility.SetDirty(listener);
                Debug.Log("[MenuPanelBuilder] UIStateListener.managementHubPanel apontado para Panel_Menu.");
            }
            else if (listener == null)
            {
                Debug.LogWarning("[MenuPanelBuilder] Nenhum UIStateListener na cena. "
                               + "Ligue 'Open Root On Start' no MenuNavigator para testar o mapa no Play.");
            }

            Debug.Log("[MenuPanelBuilder] PanelRegistry e MenuNavigator configurados em Canvas > UiNavigation.");
        }
    }
}
#endif
