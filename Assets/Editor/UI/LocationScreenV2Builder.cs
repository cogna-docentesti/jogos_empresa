#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Layout;
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
    /// Monta o Panel_Location_V2: a tela "Abertura do Restaurante" do mockup.
    ///
    /// Constroi um painel NOVO, ao lado do Panel_Location atual, que continua
    /// intacto e desativado. Da para alternar entre os dois e comparar antes
    /// de decidir qual fica.
    ///
    /// O QUE ESTE SCRIPT ALCANCA: toda a estrutura, o layout, a tipografia,
    /// as cores, os cinco marcadores e a fiacao com o LocationData.
    ///
    /// O QUE ELE NAO ALCANCA: o contorno luminoso que traca o formato exato
    /// de cada quarteirao no mockup. Aquilo e arte, nao codigo. Aqui os
    /// marcadores usam os glow_my-*.png que ja existem no projeto - manchas
    /// suaves, nao contornos tracados. Trocar por contornos de verdade e so
    /// substituir a sprite do filho "Glow" de cada marcador.
    /// </summary>
    public static class LocationScreenV2Builder
    {
        private const string ScenePath  = "Assets/Scenes/GameScene.unity";
        private const string PanelName  = "Panel_Location_V2";
        private const string MapSprite  = "Assets/Art/Sprites/LocationScene/background-map.png";

        private const float TopBarHeight   = 150f;
        private const float SidePanelWidth = 0.30f; // fracao da largura
        private const float MarkerWidth    = 340f;
        private const float MarkerHeight   = 210f;
        private const float BadgeSize      = 76f;
        private const float CardHeight     = 116f;

        // =====================================================
        //  AREAS DO MAPA
        //  u,v = posicao normalizada sobre a arte (v=0 embaixo).
        // =====================================================

        private sealed class AreaSpec
        {
            public string Id;
            public string FallbackName;
            public string FallbackSubtitle;
            public string AccentHex;
            public string IconName;
            public string GlowAsset;
            public float  U;
            public float  V;
        }

        private static readonly AreaSpec[] Areas =
        {
            new AreaSpec { Id = "bank", FallbackName = "Area Financeira", FallbackSubtitle = "Instituicao financeira",
                           AccentHex = GamePalette.HexZoneFinance, IconName = "Dollar",
                           GlowAsset = "glow_my-bank", U = 0.46f, V = 0.72f },

            new AreaSpec { Id = "university", FallbackName = "Area Educacional", FallbackSubtitle = "Universidade",
                           AccentHex = GamePalette.HexZoneEducation, IconName = "Graduation",
                           GlowAsset = "glow_my-RH", U = 0.15f, V = 0.55f },

            new AreaSpec { Id = "store", FallbackName = "Area Comercial", FallbackSubtitle = "Lojas e Servicos",
                           AccentHex = GamePalette.HexZoneCommerce, IconName = "Cart",
                           GlowAsset = "glow_my-store", U = 0.77f, V = 0.52f },

            new AreaSpec { Id = "condominium", FallbackName = "Area Residencial", FallbackSubtitle = "Bairros e Moradias",
                           AccentHex = GamePalette.HexZoneResidential, IconName = "Home",
                           GlowAsset = "glow_my-tools", U = 0.27f, V = 0.17f },

            new AreaSpec { Id = "marketing", FallbackName = "Area Corporativa", FallbackSubtitle = "Empresas e Escritorios",
                           AccentHex = GamePalette.HexZoneCorporate, IconName = "Bank",
                           GlowAsset = "glow_my-marketing", U = 0.72f, V = 0.16f }
        };

        // =====================================================
        //  MENUS
        // =====================================================

        [MenuItem("Tools/Jogo/Localizacao V2/1 - Construir tela (preserva ajustes)", false, 100)]
        public static void Build()
        {
            Run(overwrite: false);
        }

        [MenuItem("Tools/Jogo/Localizacao V2/2 - Reconstruir com o estilo padrao", false, 101)]
        public static void Rebuild()
        {
            if (!EditorUtility.DisplayDialog(
                    "Reaplicar o estilo?",
                    "Isto reescreve cor, fonte e posicao de TODOS os objetos do Panel_Location_V2, "
                    + "inclusive o que voce tiver ajustado na mao.\n\nCtrl+Z desfaz.",
                    "Reaplicar", "Cancelar"))
                return;

            Run(overwrite: true);
        }

        [MenuItem("Tools/Jogo/Localizacao V2/3 - Ver apenas esta tela", false, 102)]
        public static void Isolate()
        {
            var target = FindInScene(PanelName);

            if (target == null)
            {
                Debug.LogWarning($"[LocationScreenV2Builder] {PanelName} ainda nao existe. Rode o item 1.");
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
            Debug.Log($"[LocationScreenV2Builder] Agora so o {PanelName} esta ativo na Scene.");
        }

        // =====================================================
        //  EXECUCAO
        // =====================================================

        private static void Run(bool overwrite)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Saia do Play Mode",
                    "Alteracoes de cena feitas no Play sao descartadas ao parar o jogo.", "Entendi");
                return;
            }

            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorUtility.DisplayDialog("Abrir a GameScene?",
                        "Este builder trabalha na Assets/Scenes/GameScene.unity.", "Abrir", "Cancelar"))
                    return;

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                EditorSceneManager.OpenScene(ScenePath);
            }

            var canvas = FindCanvas();

            if (canvas == null)
            {
                Debug.LogError("[LocationScreenV2Builder] Nao encontrei o Canvas raiz da GameScene.");
                return;
            }

            UiFactory.BeginRun(overwrite);
            UiFactory.EnsureButtonSpriteBorder();

            try
            {
                BuildPanel(canvas);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LocationScreenV2Builder] Falhou: {e.Message}\n{e.StackTrace}");
                return;
            }

            int dead = UiFactory.WarnAboutDeadButtons(canvas);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            Debug.Log($"[LocationScreenV2Builder] {PanelName} pronto."
                    + (dead > 0 ? $" ATENCAO: {dead} botao(oes) sem alvo de raycast." : "")
                    + "\nUse Tools > Jogo > Localizacao V2 > 3 para ver so esta tela. Salve com Ctrl+S.");
        }

        private static void BuildPanel(RectTransform canvas)
        {
            var panel = UiFactory.EnsureChild(canvas, PanelName);
            UiFactory.Stretch(panel);
            UiFactory.Solid(panel, GamePalette.Background, raycast: true);

            var view = UiFactory.Ensure<LocationScreenV2View>(panel.gameObject);

            BuildTopBar(panel, view);
            BuildBody(panel, view);

            var controller = UiFactory.Ensure<LocationScreenV2Controller>(panel.gameObject);
            UiFactory.SetPrivate(controller, "view", view);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(view);

            panel.gameObject.SetActive(false);
        }

        // =====================================================
        //  BARRA SUPERIOR
        // =====================================================

        private static void BuildTopBar(RectTransform panel, LocationScreenV2View view)
        {
            var bar = UiFactory.EnsureChild(panel, "TopBar");
            bar.SetAsFirstSibling();
            UiFactory.TopBand(bar, TopBarHeight);
            UiFactory.Solid(bar, GamePalette.Chrome, raycast: true);

            var hairline = UiFactory.EnsureChild(bar, "Hairline");
            UiFactory.BottomBand(hairline, 2f);
            UiFactory.Solid(hairline, GamePalette.Hairline);

            var safe = UiFactory.EnsureChild(bar, "Safe");
            UiFactory.Stretch(safe);
            var fitter = UiFactory.Ensure<SafeAreaFitter>(safe.gameObject);
            UiFactory.SetPrivateBool(fitter, "applyTop", false);
            UiFactory.SetPrivateBool(fitter, "applyBottom", false);
            EditorUtility.SetDirty(fitter);

            // ---- brasao ----
            var crest = UiFactory.EnsureChild(safe, "Crest");
            UiFactory.AnchorCorner(crest, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 84, 84, 34, 0);
            UiFactory.Panel(crest, GamePalette.SurfaceAlt, GamePalette.PpuPill, raycast: false);

            var crestRing = UiFactory.EnsureChild(crest, "Ring");
            UiFactory.Stretch(crestRing);
            UiFactory.Panel(crestRing, GamePalette.GoldBorder, GamePalette.PpuPill, raycast: false).fillCenter = false;

            var crestIcon = UiFactory.EnsureChild(crest, "Icon");
            UiFactory.Stretch(crestIcon, 24, 24, 24, 24);
            UiFactory.Picture(crestIcon, UiFactory.Icon("Shop"), GamePalette.Gold);

            // ---- etapa ----
            var step = UiFactory.EnsureChild(safe, "StepLabel");
            UiFactory.AnchorCorner(step, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 640, 34, 138, 0);
            var stepLabel = UiFactory.Text(step, "ETAPA 1 DE 4  \u00B7  LOCALIZACAO",
                                           GameTypography.Caption, FontStyles.Bold,
                                           GamePalette.Muted, TextAlignmentOptions.MidlineLeft);
            stepLabel.characterSpacing = 6f;

            // ---- titulo central ----
            var title = UiFactory.EnsureChild(safe, "Title");
            UiFactory.AnchorCorner(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 1100, 84, 0, 20);
            var titleLabel = UiFactory.Text(title, "Abertura do Restaurante",
                                            GameTypography.ScreenTitle, FontStyles.Bold,
                                            GamePalette.Ink, TextAlignmentOptions.Center);

            // Ornamentos losango dos dois lados, como no mockup.
            var subtitle = UiFactory.EnsureChild(safe, "Subtitle");
            UiFactory.AnchorCorner(subtitle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 1100, 44, 0, -34);
            var subtitleLabel = UiFactory.Text(subtitle, "Escolha onde abrir seu primeiro negocio",
                                               GameTypography.ScreenHint, FontStyles.Normal,
                                               GamePalette.Muted, TextAlignmentOptions.Center);

            BuildOrnament(safe, "OrnamentLeft",  -1);
            BuildOrnament(safe, "OrnamentRight", +1);

            // ---- botao MENU ----
            var menu = UiFactory.EnsureChild(safe, "MenuButton");
            UiFactory.AnchorCorner(menu, new Vector2(1, 0.5f), new Vector2(1, 0.5f), 210, 78, -34, 0);
            UiFactory.Panel(menu, new Color(0, 0, 0, 0), GamePalette.PpuButton, raycast: true);
            UiFactory.EnsureHitArea(menu);

            var menuBorder = UiFactory.EnsureChild(menu, "Border");
            UiFactory.Stretch(menuBorder);
            UiFactory.Panel(menuBorder, GamePalette.GoldBorder, GamePalette.PpuButton, raycast: false).fillCenter = false;

            var menuIcon = UiFactory.EnsureChild(menu, "Icon");
            UiFactory.AnchorCorner(menuIcon, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 32, 32, 26, 0);
            UiFactory.Picture(menuIcon, UiFactory.Icon("Menu"), GamePalette.Gold);

            var menuLabel = UiFactory.EnsureChild(menu, "Label");
            UiFactory.AnchorCorner(menuLabel, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 130, 40, 70, 0);
            var menuText = UiFactory.Text(menuLabel, "MENU", GameTypography.Body, FontStyles.Bold,
                                          GamePalette.Gold, TextAlignmentOptions.MidlineLeft);
            menuText.characterSpacing = 4f;

            var menuButton = UiFactory.MakeButton(menu, highlightedTint: 1.18f, pressedTint: 0.88f);

            UiFactory.SetPrivate(view, "stepLabel", stepLabel);
            UiFactory.SetPrivate(view, "titleLabel", titleLabel);
            UiFactory.SetPrivate(view, "subtitleLabel", subtitleLabel);
            UiFactory.SetPrivate(view, "menuButton", menuButton);
        }

        private static void BuildOrnament(RectTransform parent, string name, int side)
        {
            var rt = UiFactory.EnsureChild(parent, name);
            UiFactory.AnchorCorner(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                   200, 30, side * 300f, -34);

            // O losango cheio e o travessao vao como escape unicode para o arquivo
            // continuar 100% ASCII e nao depender de encoding.
            string glyph = side < 0 ? "\u25C6 \u2014\u2014\u2014" : "\u2014\u2014\u2014 \u25C6";

            UiFactory.Text(rt, glyph, GameTypography.Caption, FontStyles.Normal,
                           GamePalette.GoldBorder,
                           side < 0 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft);
        }

        // =====================================================
        //  CORPO: MAPA + PAINEL LATERAL
        // =====================================================

        private static void BuildBody(RectTransform panel, LocationScreenV2View view)
        {
            var body = UiFactory.EnsureChild(panel, "Body");
            UiFactory.Stretch(body, 0, TopBarHeight, 0, 0);

            BuildMap(body, view);
            BuildSidePanel(body, view);
        }

        private static void BuildMap(RectTransform body, LocationScreenV2View view)
        {
            var map = UiFactory.EnsureChild(body, "MapArea");
            map.anchorMin = new Vector2(0f, 0f);
            map.anchorMax = new Vector2(1f - SidePanelWidth, 1f);
            map.offsetMin = Vector2.zero;
            map.offsetMax = Vector2.zero;

            var image = UiFactory.EnsureChild(map, "MapImage");
            UiFactory.Stretch(image);
            UiFactory.Picture(image, UiFactory.Load(MapSprite), Color.white, preserveAspect: false);

            var scrim = UiFactory.EnsureChild(map, "Scrim");
            UiFactory.Stretch(scrim);
            UiFactory.Solid(scrim, GamePalette.MapScrim);

            var layer = UiFactory.EnsureChild(map, "Markers");
            UiFactory.Stretch(layer);

            var markers = new List<LocationMarkerV2>();

            foreach (var area in Areas)
                markers.Add(BuildMarker(layer, area));

            view.EditorSetMarkers(markers);
        }

        private static LocationMarkerV2 BuildMarker(RectTransform layer, AreaSpec area)
        {
            var accent = GamePalette.Parse(area.AccentHex);

            var root = UiFactory.EnsureChild(layer, $"Marker_{area.Id}");
            UiFactory.AnchorNormalized(root, area.U, area.V, MarkerWidth, MarkerHeight);

            // Halo. Fica atras de tudo por ser o primeiro filho.
            var glow = UiFactory.EnsureChild(root, "Glow");
            glow.SetAsFirstSibling();
            UiFactory.Stretch(glow, -150, -130, -150, -130);
            var glowImage = UiFactory.Picture(glow,
                UiFactory.Load($"Assets/Art/Sprites/UI/{area.GlowAsset}.png"),
                GamePalette.WithAlpha(accent, 0.18f), preserveAspect: false);

            // Area de clique cobrindo card e badge, mas nao o halo.
            var hit = UiFactory.EnsureChild(root, "HitArea");
            UiFactory.Stretch(hit);
            var hitImage = UiFactory.Ensure<Image>(hit.gameObject);
            hitImage.sprite = null;
            hitImage.color = new Color(1, 1, 1, 0);
            hitImage.raycastTarget = true;

            // Card com nome e subtitulo.
            var card = UiFactory.EnsureChild(root, "Card");
            UiFactory.AnchorCorner(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                   MarkerWidth, CardHeight, 0, 0);
            var cardImage = UiFactory.Panel(card, GamePalette.MapLabel, GamePalette.PpuCard, raycast: false);

            var cardBorder = UiFactory.EnsureChild(card, "Border");
            UiFactory.Stretch(cardBorder);
            var borderImage = UiFactory.Panel(cardBorder, GamePalette.Hairline, GamePalette.PpuCard, raycast: false);
            borderImage.fillCenter = false;

            var nameRt = UiFactory.EnsureChild(card, "Name");
            UiFactory.AnchorCorner(nameRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                   MarkerWidth - 28, 44, 0, -16);
            var nameLabel = UiFactory.Text(nameRt, area.FallbackName, GameTypography.SectionTitle,
                                           FontStyles.Bold, GamePalette.Ink, TextAlignmentOptions.Center);

            var subRt = UiFactory.EnsureChild(card, "Subtitle");
            UiFactory.AnchorCorner(subRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                   MarkerWidth - 28, 36, 0, -62);
            var subLabel = UiFactory.Text(subRt, area.FallbackSubtitle, GameTypography.Caption,
                                          FontStyles.Normal, GamePalette.Muted, TextAlignmentOptions.Center);

            // Badge circular por cima do card.
            var badge = UiFactory.EnsureChild(root, "Badge");
            UiFactory.AnchorCorner(badge, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                   BadgeSize, BadgeSize, 0, 0);
            var badgeImage = UiFactory.Panel(badge, accent, GamePalette.PpuPill, raycast: false);

            var ring = UiFactory.EnsureChild(badge, "Ring");
            UiFactory.Stretch(ring, -8, -8, -8, -8);
            var ringImage = UiFactory.Panel(ring, GamePalette.WithAlpha(accent, 0.35f),
                                            GamePalette.PpuPill, raycast: false);
            ringImage.fillCenter = false;

            var badgeIcon = UiFactory.EnsureChild(badge, "Icon");
            UiFactory.Stretch(badgeIcon, 20, 20, 20, 20);
            var iconImage = UiFactory.Picture(badgeIcon, UiFactory.Icon(area.IconName), GamePalette.Ink);

            UiFactory.MakeButton(root, highlightedTint: 1.0f, pressedTint: 1.0f);

            var marker = UiFactory.Ensure<LocationMarkerV2>(root.gameObject);
            UiFactory.SetPrivateString(marker, "locationId", area.Id);
            UiFactory.SetPrivate(marker, "glow", glowImage);
            UiFactory.SetPrivate(marker, "card", cardImage);
            UiFactory.SetPrivate(marker, "cardBorder", borderImage);
            UiFactory.SetPrivate(marker, "badge", badgeImage);
            UiFactory.SetPrivate(marker, "badgeIcon", iconImage);
            UiFactory.SetPrivate(marker, "badgeRing", ringImage);
            UiFactory.SetPrivate(marker, "nameLabel", nameLabel);
            UiFactory.SetPrivate(marker, "subtitleLabel", subLabel);
            EditorUtility.SetDirty(marker);

            return marker;
        }

        // =====================================================
        //  PAINEL LATERAL
        // =====================================================

        private static void BuildSidePanel(RectTransform body, LocationScreenV2View view)
        {
            var side = UiFactory.EnsureChild(body, "SidePanel");
            side.anchorMin = new Vector2(1f - SidePanelWidth, 0f);
            side.anchorMax = new Vector2(1f, 1f);
            side.offsetMin = new Vector2(16, 20);
            side.offsetMax = new Vector2(-24, -20);

            var safe = UiFactory.EnsureChild(side, "Safe");
            UiFactory.Stretch(safe);
            var fitter = UiFactory.Ensure<SafeAreaFitter>(safe.gameObject);
            UiFactory.SetPrivateBool(fitter, "applyTop", false);
            EditorUtility.SetDirty(fitter);

            var card = UiFactory.EnsureChild(safe, "Card");
            UiFactory.Stretch(card);
            UiFactory.Panel(card, GamePalette.Surface, GamePalette.PpuCard, raycast: true);

            var border = UiFactory.EnsureChild(card, "Border");
            UiFactory.Stretch(border);
            UiFactory.Panel(border, GamePalette.Hairline, GamePalette.PpuCard, raycast: false).fillCenter = false;

            // ---------- estado vazio ----------
            var empty = UiFactory.EnsureChild(card, "EmptyState");
            UiFactory.Stretch(empty, 30, 30, 30, 30);

            var emptyIcon = UiFactory.EnsureChild(empty, "Icon");
            UiFactory.AnchorCorner(emptyIcon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 96, 96, 0, 90);
            UiFactory.Picture(emptyIcon, UiFactory.Icon("Map"), GamePalette.Gray);

            var emptyText = UiFactory.EnsureChild(empty, "Text");
            UiFactory.AnchorCorner(emptyText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 460, 130, 0, -20);
            UiFactory.Text(emptyText, "Selecione uma area no mapa para ver os detalhes.",
                           GameTypography.Body, FontStyles.Normal, GamePalette.Muted,
                           TextAlignmentOptions.Top, wrap: true);

            // ---------- detalhes ----------
            var details = UiFactory.EnsureChild(card, "Details");
            UiFactory.Stretch(details, 30, 30, 30, 30);

            var iconBadge = UiFactory.EnsureChild(details, "IconBadge");
            UiFactory.AnchorCorner(iconBadge, new Vector2(0, 1), new Vector2(0, 1), 84, 84, 0, 0);
            var iconBadgeImage = UiFactory.Panel(iconBadge, GamePalette.WithAlpha(GamePalette.AccentBlue, 0.18f),
                                                 GamePalette.PpuPill, raycast: false);

            var panelIconRt = UiFactory.EnsureChild(iconBadge, "Icon");
            UiFactory.Stretch(panelIconRt, 22, 22, 22, 22);
            var panelIconImage = UiFactory.Picture(panelIconRt, UiFactory.Icon("Dollar"), GamePalette.AccentBlue);

            var zoneTitle = UiFactory.EnsureChild(details, "ZoneTitle");
            UiFactory.AnchorCorner(zoneTitle, new Vector2(0, 1), new Vector2(0, 1), 460, 52, 100, -4);
            var zoneTitleLabel = UiFactory.Text(zoneTitle, "AREA FINANCEIRA", GameTypography.SectionTitle,
                                                FontStyles.Bold, GamePalette.AccentBlue,
                                                TextAlignmentOptions.MidlineLeft);

            var zoneSub = UiFactory.EnsureChild(details, "ZoneSubtitle");
            UiFactory.AnchorCorner(zoneSub, new Vector2(0, 1), new Vector2(0, 1), 460, 40, 100, -58);
            var zoneSubLabel = UiFactory.Text(zoneSub, "Instituicao financeira", GameTypography.Caption,
                                              FontStyles.Normal, GamePalette.Muted,
                                              TextAlignmentOptions.MidlineLeft);

            var desc = UiFactory.EnsureChild(details, "Description");
            UiFactory.TopBand(desc, 110f, 0, 0, -108);
            var descLabel = UiFactory.Text(desc, "Regiao nobre com grande fluxo de profissionais e empresas.",
                                           GameTypography.Body, FontStyles.Normal, GamePalette.Muted,
                                           TextAlignmentOptions.TopLeft, wrap: true);

            // ---------- indicadores ----------
            const float rowHeight = 104f;
            const float rowGap = 16f;
            float firstRowY = 232f;

            var investment  = BuildIndicator(details, "Stat_Investment",  "Investimento", "Alto",
                                             "Graph", GamePalette.HexZoneFinance, firstRowY);
            var traffic     = BuildIndicator(details, "Stat_Traffic",     "Movimento", "Medio",
                                             "Add_Friend", GamePalette.HexZoneEducation, firstRowY + rowHeight + rowGap);
            var competition = BuildIndicator(details, "Stat_Competition", "Concorrencia", "Baixa",
                                             "Safe", GamePalette.HexZoneResidential, firstRowY + (rowHeight + rowGap) * 2);

            // ---------- destaque ----------
            var highlight = UiFactory.EnsureChild(details, "HighlightBox");
            UiFactory.TopBand(highlight, 104f, 0, 0, -(firstRowY + (rowHeight + rowGap) * 3));
            UiFactory.Panel(highlight, GamePalette.WithAlpha(GamePalette.AccentBlue, 0.10f),
                            GamePalette.PpuCard, raycast: false);

            var highlightBorder = UiFactory.EnsureChild(highlight, "Border");
            UiFactory.Stretch(highlightBorder);
            UiFactory.Panel(highlightBorder, GamePalette.AccentBlue, GamePalette.PpuCard, raycast: false)
                     .fillCenter = false;

            var highlightIconRt = UiFactory.EnsureChild(highlight, "Icon");
            UiFactory.AnchorCorner(highlightIconRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 38, 38, 24, 0);
            var highlightIconImage = UiFactory.Picture(highlightIconRt, UiFactory.Icon("Star"), GamePalette.AccentBlue);

            var highlightTextRt = UiFactory.EnsureChild(highlight, "Text");
            UiFactory.Stretch(highlightTextRt, 74, 14, 20, 14);
            var highlightLabel = UiFactory.Text(highlightTextRt, "Maior circulacao em dias uteis",
                                                GameTypography.Caption, FontStyles.Normal,
                                                GamePalette.AccentBlue, TextAlignmentOptions.Left, wrap: true);

            // ---------- CONTINUAR ----------
            var continueRt = UiFactory.EnsureChild(details, "ContinueButton");
            UiFactory.BottomBand(continueRt, 108f, 0, 0, 0);
            UiFactory.Panel(continueRt, GamePalette.GoldDeep, GamePalette.PpuButton, raycast: true);
            UiFactory.EnsureHitArea(continueRt);

            // Duas camadas aproximam o gradiente do mockup sem precisar de
            // sprite nova: a base escura, e uma faixa mais clara na metade de cima.
            var sheen = UiFactory.EnsureChild(continueRt, "Sheen");
            sheen.anchorMin = new Vector2(0f, 0.45f);
            sheen.anchorMax = new Vector2(1f, 1f);
            sheen.offsetMin = Vector2.zero;
            sheen.offsetMax = Vector2.zero;
            UiFactory.Panel(sheen, GamePalette.WithAlpha(GamePalette.Gold, 0.85f),
                            GamePalette.PpuButton, raycast: false);

            var continueLabelRt = UiFactory.EnsureChild(continueRt, "Label");
            UiFactory.Stretch(continueLabelRt);
            var continueText = UiFactory.Text(continueLabelRt, "CONTINUAR", GameTypography.SectionTitle,
                                              FontStyles.Bold, GamePalette.OnGold, TextAlignmentOptions.Center);
            continueText.characterSpacing = 8f;

            var continueButton = UiFactory.MakeButton(continueRt, highlightedTint: 1.12f, pressedTint: 0.88f);

            // ---------- fiacao ----------
            UiFactory.SetPrivate(view, "panelIconBadge",   iconBadgeImage);
            UiFactory.SetPrivate(view, "panelIcon",        panelIconImage);
            UiFactory.SetPrivate(view, "panelTitle",       zoneTitleLabel);
            UiFactory.SetPrivate(view, "panelSubtitle",    zoneSubLabel);
            UiFactory.SetPrivate(view, "panelDescription", descLabel);
            UiFactory.SetPrivate(view, "investmentValue",  investment);
            UiFactory.SetPrivate(view, "trafficValue",     traffic);
            UiFactory.SetPrivate(view, "competitionValue", competition);
            UiFactory.SetPrivate(view, "highlightBox",     highlight.gameObject);
            UiFactory.SetPrivate(view, "highlightText",    highlightLabel);
            UiFactory.SetPrivate(view, "highlightIcon",    highlightIconImage);
            UiFactory.SetPrivate(view, "emptyState",       empty.gameObject);
            UiFactory.SetPrivate(view, "detailsState",     details.gameObject);
            UiFactory.SetPrivate(view, "continueButton",   continueButton);
        }

        private static TextMeshProUGUI BuildIndicator(RectTransform parent, string name, string caption,
                                                      string value, string iconName, string accentHex, float y)
        {
            var row = UiFactory.EnsureChild(parent, name);
            UiFactory.TopBand(row, 104f, 0, 0, -y);
            UiFactory.Panel(row, GamePalette.SurfaceAlt, GamePalette.PpuCard, raycast: false);

            var accent = GamePalette.Parse(accentHex);

            var badge = UiFactory.EnsureChild(row, "IconBadge");
            UiFactory.AnchorCorner(badge, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 62, 62, 20, 0);
            UiFactory.Panel(badge, GamePalette.WithAlpha(accent, 0.18f), GamePalette.PpuButton, raycast: false);

            var icon = UiFactory.EnsureChild(badge, "Icon");
            UiFactory.Stretch(icon, 15, 15, 15, 15);
            UiFactory.Picture(icon, UiFactory.Icon(iconName), accent);

            var captionRt = UiFactory.EnsureChild(row, "Caption");
            UiFactory.AnchorCorner(captionRt, new Vector2(0, 0.5f), new Vector2(0, 0.5f), 300, 44, 98, 0);
            UiFactory.Text(captionRt, caption, GameTypography.Body, FontStyles.Normal,
                           GamePalette.Ink, TextAlignmentOptions.MidlineLeft);

            var valueRt = UiFactory.EnsureChild(row, "Value");
            UiFactory.AnchorCorner(valueRt, new Vector2(1, 0.5f), new Vector2(1, 0.5f), 220, 46, -22, 0);
            return UiFactory.Text(valueRt, value, GameTypography.Body, FontStyles.Bold,
                                  GamePalette.Gold, TextAlignmentOptions.MidlineRight);
        }

        // =====================================================
        //  APOIO
        // =====================================================

        private static RectTransform FindCanvas()
        {
            var existing = FindInScene("Panel_Menu");

            if (existing != null && existing.transform.parent != null)
                return existing.transform.parent as RectTransform;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            return canvas != null ? canvas.transform as RectTransform : null;
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
    }
}
#endif
