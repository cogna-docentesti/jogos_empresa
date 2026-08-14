#if UNITY_EDITOR
using Game.Adapter.In.UI.Effects;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Gera a CENA NOVA "LocationScreenMockup" com a tela "Abertura do
    /// Restaurante" reconstruida a partir das medidas do mockup (new.png).
    ///
    /// Isto NAO toca em nada que ja existe: nem no Panel_Location_V2 da
    /// GameScene, nem na LocationSelectionScene. Roda o menu, abre a cena, olha,
    /// ajusta. Se estragar, roda de novo - a cena e descartavel.
    ///
    /// ------------------------------------------------------------------
    ///  DUAS TECNICAS QUE ESTA TELA INTRODUZ
    /// ------------------------------------------------------------------
    ///
    /// 1. BORDA FINA DE VERDADE (Frame + Fill).
    ///
    ///    Em botao.png a espessura de um Image Sliced com fillCenter = false e
    ///    "48 / pixelsPerUnitMultiplier", e o raio e "36 / pixelsPerUnitMultiplier".
    ///    Sao a MESMA variavel. Pedir raio 13 entrega borda de 15px - uma laje,
    ///    nao uma hairline. Nao existe multiplier que resolva.
    ///
    ///    A saida sao dois Images concentricos, os dois com fillCenter = true:
    ///      Frame -> raio externo R, cor da borda
    ///      Fill  -> recuado T px, raio R-T, cor do fundo
    ///    Assim a espessura e exatamente T em toda a volta, inclusive nos cantos,
    ///    que e onde qualquer outra tecnica falha. Custa 2 draw calls, mas como
    ///    todos usam a mesma sprite e material o Canvas junta tudo num batch.
    ///
    /// 2. GRADIENTE DE VERDADE (VerticalGradient).
    ///
    ///    Empilhar uma faixa clara por cima da base cria uma aresta dura na
    ///    emenda. O VerticalGradient pinta a malha e nao tem emenda nenhuma.
    ///    Ver o comentario do proprio componente.
    ///
    /// ------------------------------------------------------------------
    ///  O QUE E ARTE E NAO FOI RECONSTRUIDO
    /// ------------------------------------------------------------------
    ///
    /// O contorno neon que traca a silhueta dos predios do quarteirao
    /// selecionado. Aquilo e um tracado poligonal sobre a ilustracao; nao sai de
    /// primitiva. Aqui o estado selecionado usa o glow_&lt;zona&gt;.png que ja existe
    /// (mancha suave, alpha alto). Para chegar no mockup seria preciso uma
    /// sprite RGBA de 1345x948 por zona, alinhada ao background-map.
    /// </summary>
    public static class LocationScreenMockupBuilder
    {
        private const string ScenePath = "Assets/Scenes/LocationScreenMockup.unity";
        private const string MapSprite = "Assets/Art/Sprites/LocationScene/background-map.png";
        private const string GlowFolder = "Assets/Art/Sprites/LocationScene/";
        private const string CircleSprite = "Assets/Art/Sprites/UI/Circle.png";
        private const string ButtonSprite = "Assets/Art/Sprites/UI/botao.png";
        private const string FontPath = "Assets/TextMesh Pro/Fonts/InterTight-Variable.asset";
        private const string IconFolder = "Assets/Layer Lab/2D Icons-PictoIconPack01/Icons/PictoIcon_128/";

        // Os acentos vao como escape unicode para o arquivo continuar 100% ASCII,
        // que e a convencao do resto do projeto - assim ele nao depende de o
        // editor salvar em UTF-8.
        private const string Area = "\u00C1rea";

        // =====================================================
        //  ZONAS
        //  u,v = posicao normalizada do CENTRO DO BADGE sobre o mapa.
        //  Medidos no mockup, nao chutados.
        // =====================================================

        private sealed class Zone
        {
            public string Id;
            public string Name;
            public string Subtitle;
            public string IconName;
            public string GlowAsset;
            public string AccentHex;
            public float  U;
            public float  V;
            public float  CardWidth;
            public bool   Selected;
        }

        private static readonly Zone[] Zones =
        {
            new Zone { Id = "bank",        Name = Area + " Financeira",  Subtitle = "Institui\u00E7\u00E3o financeira",
                       IconName = "Dollar",     GlowAsset = "glow_bank",
                       AccentHex = LocationMockupTokens.HexZoneFinance,
                       U = 0.5013f, V = 0.8509f, CardWidth = 320f, Selected = true },

            new Zone { Id = "university",  Name = Area + " Educacional", Subtitle = "Universidade",
                       IconName = "Graduation", GlowAsset = "glow_university",
                       AccentHex = LocationMockupTokens.HexZoneEducation,
                       U = 0.1943f, V = 0.5901f, CardWidth = 294f },

            new Zone { Id = "store",       Name = Area + " Comercial",   Subtitle = "Lojas e Servi\u00E7os",
                       IconName = "Cart",       GlowAsset = "glow_store",
                       AccentHex = LocationMockupTokens.HexZoneCommerce,
                       U = 0.7906f, V = 0.5382f, CardWidth = 278f },

            new Zone { Id = "condominium", Name = Area + " Residencial", Subtitle = "Bairros e Moradias",
                       IconName = "Home",       GlowAsset = "glow_condominium",
                       AccentHex = LocationMockupTokens.HexZoneResidential,
                       U = 0.2578f, V = 0.1938f, CardWidth = 280f },

            // "Bank" e o unico predio institucional do pacote. "Firm", apesar do
            // nome, e um rolo de filme.
            new Zone { Id = "marketing",   Name = Area + " Corporativa", Subtitle = "Empresas e Escrit\u00F3rios",
                       IconName = "Bank",       GlowAsset = "glow_marketing",
                       AccentHex = LocationMockupTokens.HexZoneCorporate,
                       U = 0.7294f, V = 0.1946f, CardWidth = 290f }
        };

        // =====================================================
        //  MENU
        // =====================================================

        [MenuItem("Tools/Jogo/Localizacao Mockup/Gerar cena nova", false, 200)]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Saia do Play Mode",
                    "Cenas criadas durante o Play sao descartadas ao parar o jogo.", "Entendi");
                return;
            }

            if (!EditorUtility.DisplayDialog("Gerar a cena de mockup?",
                    "Cria (ou sobrescreve) " + ScenePath + ".\n\n"
                    + "Nenhuma outra cena e tocada: o Panel_Location_V2 da GameScene e a "
                    + "LocationSelectionScene continuam exatamente como estao.",
                    "Gerar", "Cancelar"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureButtonSpriteBorder();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildEventSystem();

            var canvas = BuildCanvas();

            try
            {
                BuildScreen(canvas);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[LocationScreenMockupBuilder] Falhou: " + e.Message + "\n" + e.StackTrace);
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[LocationScreenMockupBuilder] Cena gerada em " + ScenePath
                    + "\nAbra a Game view em 16:9 (1920x1080) para ver no enquadramento correto.");
        }

        // =====================================================
        //  ESQUELETO DA CENA
        // =====================================================

        private static void BuildCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";

            var cam = go.GetComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex(LocationMockupTokens.HexBg);
            cam.orthographic    = true;

            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void BuildEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));

            // O projeto usa com.unity.inputsystem. Resolver o modulo por nome
            // evita amarrar este arquivo ao assembly do Input System - se um dia
            // o pacote sair, o builder continua compilando.
            var moduleType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (moduleType != null)
                go.AddComponent(moduleType);
            else
                go.AddComponent<StandaloneInputModule>();
        }

        private static RectTransform BuildCanvas()
        {
            var go = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode             = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution     = new Vector2(1920f, 1080f);
            scaler.screenMatchMode         = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referencePixelsPerUnit  = 100f;

            // 0 = casar pela LARGURA. O layout foi desenhado assim de proposito:
            // a barra fica presa no topo, o CONTINUAR na base, e a sobra vertical
            // (o mockup e 2:1, o canvas e 16:9) cai num unico ponto declarado -
            // a lacuna entre a descricao e o primeiro indicador.
            scaler.matchWidthOrHeight = 0f;

            return (RectTransform)go.transform;
        }

        // =====================================================
        //  A TELA
        // =====================================================

        private static void BuildScreen(RectTransform canvas)
        {
            var panel = Node(canvas, "Panel_Location");
            Stretch(panel);
            Solid(panel, Hex(LocationMockupTokens.HexBg), raycast: true);

            BuildTopBar(panel);

            var body = Node(panel, "Body");
            Stretch(body, 0f, LocationMockupTokens.TopBarHeight, 0f, 0f);

            BuildMap(body);
            BuildSidePanel(body);
        }

        // =====================================================
        //  BARRA SUPERIOR
        // =====================================================

        private static void BuildTopBar(RectTransform panel)
        {
            var bar = Node(panel, "TopBar");
            TopBand(bar, LocationMockupTokens.TopBarHeight);
            Solid(bar, Hex(LocationMockupTokens.HexChrome), raycast: true);

            BuildCrest(bar);
            BuildStepLabel(bar);

            var title = Node(bar, "Title");
            At(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), 1200f, 90f, 0f, -60f);
            Label(title, "Abertura do Restaurante",
                  LocationMockupTokens.FontTitle, FontStyles.Bold,
                  Hex(LocationMockupTokens.HexInk), TextAlignmentOptions.Center);

            var subtitle = Node(bar, "Subtitle");
            At(subtitle, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), 900f, 40f, 0f, -116f);
            Label(subtitle, "Escolha onde abrir seu primeiro neg\u00F3cio",
                  LocationMockupTokens.FontBody, FontStyles.Normal,
                  Hex(LocationMockupTokens.HexMuted), TextAlignmentOptions.Center);

            BuildOrnament(bar, "OrnamentLeft",  -1);
            BuildOrnament(bar, "OrnamentRight", +1);

            BuildMenuButton(bar);
        }

        private static void BuildCrest(RectTransform bar)
        {
            float d = LocationMockupTokens.CrestSize;

            var crest = Node(bar, "Crest");
            At(crest, new Vector2(0f, 1f), new Vector2(0f, 1f), d, d, 52f, -24f);

            Ringed(crest,
                   LocationMockupTokens.RingCrest,
                   Hex(LocationMockupTokens.HexGoldCrest),
                   Hex(LocationMockupTokens.HexCrestFill));

            // O brasao original e um escudo COM um pino dentro. Duas camadas
            // chegam perto: o escudo fosco atras, o pino dourado na frente.
            var shield = Node(crest, "Shield");
            Stretch(shield, 18f, 18f, 18f, 18f);
            Pic(shield, Icon("Defense"), Rgba(LocationMockupTokens.HexGoldCrest, 0.45f));

            var pin = Node(crest, "Icon");
            Stretch(pin, 27f, 27f, 27f, 27f);
            Pic(pin, Icon("Location"), Hex(LocationMockupTokens.HexGoldStep));
        }

        private static void BuildStepLabel(RectTransform bar)
        {
            var step = Node(bar, "StepLabel");
            At(step, new Vector2(0f, 1f), new Vector2(0f, 0.5f), 700f, 40f, 180f, -65f);

            // No mockup so "ETAPA 1 DE 4" e dourado; o separador e a palavra
            // LOCALIZACAO sao brancos. Duas cores numa linha so = rich text.
            string text = "<color=" + LocationMockupTokens.HexGoldStep + ">ETAPA 1 DE 4</color>"
                        + "  <color=" + LocationMockupTokens.HexInk + ">\u00B7  LOCALIZA\u00C7\u00C3O</color>";

            var label = Label(step, text, LocationMockupTokens.FontStep, FontStyles.Bold,
                              Hex(LocationMockupTokens.HexInk), TextAlignmentOptions.MidlineLeft);
            label.richText         = true;
            label.characterSpacing = 6f;
        }

        private static void BuildOrnament(RectTransform bar, string name, int side)
        {
            var root = Node(bar, name);
            At(root,
               new Vector2(0.5f, 1f),
               side < 0 ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f),
               110f, 20f, side * 281f, -116f);

            var color = Rgba(LocationMockupTokens.HexGoldOrnament, 0.85f);

            // A ponta EXTERNA leva o traco; a ponta INTERNA (lado do texto) leva
            // o losango. Assim os dois ornamentos apontam para o subtitulo.
            var line = Node(root, "Line");
            At(line,
               side < 0 ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f),
               side < 0 ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f),
               86f, 2f, 0f, 0f);
            Solid(line, color);

            // ATENCAO: nem LiberationSans nem InterTight tem o glifo U+25C6.
            // Escrever o losango como texto sairia como quadrado vazio. Por isso
            // ele e um Image quadrado girado 45 graus.
            var diamond = Node(root, "Diamond");
            At(diamond,
               side < 0 ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f),
               new Vector2(0.5f, 0.5f),
               10f, 10f, 0f, 0f);
            Solid(diamond, Hex(LocationMockupTokens.HexGoldOrnament));
            diamond.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static void BuildMenuButton(RectTransform bar)
        {
            var menu = Node(bar, "MenuButton");
            At(menu, new Vector2(1f, 1f), new Vector2(1f, 1f), 184f, 70f, -60f, -24f);

            HitArea(menu);

            var menuFill = Framed(menu,
                                  LocationMockupTokens.RadiusPill,
                                  LocationMockupTokens.BorderHairline,
                                  Hex(LocationMockupTokens.HexGoldBorder),
                                  Hex(LocationMockupTokens.HexMenuFill));

            // O icone "Menu" do pacote e uma GRADE 2x2, nao um hamburguer.
            // Nenhum icone do pacote tem 3 linhas iguais, entao ele vai como
            // tres retangulos - que e literalmente o desenho.
            var icon = Node(menu, "Icon");
            At(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 30f, 24f, 27f, 0f);

            for (int i = 0; i < 3; i++)
            {
                var bar3 = Node(icon, "Bar" + (i + 1));
                At(bar3, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 30f, 4f, 0f, 10f - i * 10f);
                Solid(bar3, Hex(LocationMockupTokens.HexGoldText));
            }

            var label = Node(menu, "Label");
            At(label, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 100f, 40f, 78f, 0f);
            var text = Label(label, "MENU", LocationMockupTokens.FontMenu, FontStyles.Bold,
                             Hex(LocationMockupTokens.HexGoldText), TextAlignmentOptions.MidlineLeft);
            text.characterSpacing = 4f;

            // Sem targetGraphic o ColorTint nao tem o que pintar e o botao fica
            // sem retorno visual nenhum - falha silenciosa classica.
            MakeButton(menu, 1.18f, 0.88f).targetGraphic = menuFill;
        }

        // =====================================================
        //  MAPA
        // =====================================================

        private static void BuildMap(RectTransform body)
        {
            var map = Node(body, "MapArea");
            map.anchorMin = new Vector2(0f, 0f);
            map.anchorMax = new Vector2(LocationMockupTokens.MapWidthFraction, 1f);
            map.offsetMin = Vector2.zero;
            map.offsetMax = Vector2.zero;

            var image = Node(map, "MapImage");
            Stretch(image);
            Pic(image, Load<Sprite>(MapSprite), Color.white, preserveAspect: false);

            var scrim = Node(map, "Scrim");
            Stretch(scrim);
            Solid(scrim, Rgba(LocationMockupTokens.HexMapScrim, LocationMockupTokens.MapScrimAlpha));

            var markers = Node(map, "Markers");
            Stretch(markers);

            foreach (var zone in Zones)
                BuildMarker(markers, zone);
        }

        private static void BuildMarker(RectTransform layer, Zone zone)
        {
            var accent = Hex(zone.AccentHex);

            var root = Node(layer, "Marker_" + zone.Id);
            root.anchorMin        = new Vector2(zone.U, zone.V);
            root.anchorMax        = new Vector2(zone.U, zone.V);
            // O pivot 0.775 faz o (u,v) marcar exatamente o CENTRO DO BADGE, que
            // e o ponto que precisa cair sobre o predio. Ancorar pelo centro do
            // conjunto deslocaria o badge conforme o card mudasse de altura.
            root.pivot            = new Vector2(0.5f, 0.775f);
            root.sizeDelta        = new Vector2(zone.CardWidth, LocationMockupTokens.MarkerHeight);
            root.anchoredPosition = Vector2.zero;

            var glow = Node(root, "Glow");
            Stretch(glow, -150f, -140f, -150f, -140f);
            Pic(glow, Load<Sprite>(GlowFolder + zone.GlowAsset + ".png"),
                new Color(accent.r, accent.g, accent.b, zone.Selected ? 0.85f : 0.18f),
                preserveAspect: false);

            HitArea(root);

            // ---- card ----
            var card = Node(root, "Card");
            At(card, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
               zone.CardWidth, LocationMockupTokens.MarkerCardHeight, 0f, 0f);

            var cardFill = Framed(card,
                                  LocationMockupTokens.RadiusCard,
                                  zone.Selected ? LocationMockupTokens.BorderSelected
                                                : LocationMockupTokens.BorderHairline,
                                  zone.Selected ? accent
                                                : Rgba(LocationMockupTokens.HexCardBorder, 0.9f),
                                  Rgba(LocationMockupTokens.HexCardMap, LocationMockupTokens.CardMapAlpha));

            var name = Node(card, "Name");
            At(name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
               zone.CardWidth - 56f, 40f, 0f, -10f);
            Label(name, zone.Name, LocationMockupTokens.FontMarkerName, FontStyles.Bold,
                  Hex(LocationMockupTokens.HexInk), TextAlignmentOptions.Center);

            var sub = Node(card, "Subtitle");
            At(sub, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
               zone.CardWidth - 56f, 32f, 0f, -48f);
            Label(sub, zone.Subtitle, LocationMockupTokens.FontMarkerSub, FontStyles.Normal,
                  Hex(LocationMockupTokens.HexInkSubtle), TextAlignmentOptions.Center);

            // ---- badge ----
            // Vem DEPOIS do card na hierarquia: a base do circulo tangencia o
            // topo do card, e se o card ficasse na frente comeria o anel.
            float d = LocationMockupTokens.BadgeMarker;

            var badge = Node(root, "Badge");
            At(badge, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), d, d, 0f, 0f);

            Ringed(badge, LocationMockupTokens.RingBadgeMarker, accent,
                   LocationMockupTokens.BadgeCore(accent));

            var icon = Node(badge, "Icon");
            Stretch(icon, d * 0.25f, d * 0.25f, d * 0.25f, d * 0.25f);
            Pic(icon, Icon(zone.IconName), Hex(LocationMockupTokens.HexInk));

            // No jogo quem responde ao toque e o glow do LocationMarkerV2, nao um
            // tint. Aqui o card recebe um realce discreto so para o marcador nao
            // ficar mudo dentro da cena de mockup.
            MakeButton(root, 1.10f, 0.94f).targetGraphic = cardFill;
        }

        // =====================================================
        //  PAINEL LATERAL
        // =====================================================

        private static void BuildSidePanel(RectTransform body)
        {
            var side = Node(body, "SidePanel");
            side.anchorMin = new Vector2(LocationMockupTokens.MapWidthFraction, 0f);
            side.anchorMax = new Vector2(1f, 1f);
            side.offsetMin = new Vector2(0f, 12f);
            side.offsetMax = new Vector2(-6f, 0f);

            // O painel tem o MESMO fundo da tela. Quem o define e a borda roxa,
            // nao um fill mais claro - foi assim que o mockup foi medido.
            Framed(side,
                   LocationMockupTokens.RadiusPanel,
                   LocationMockupTokens.BorderHairline,
                   Hex(LocationMockupTokens.HexPanelBorder),
                   Hex(LocationMockupTokens.HexBg));

            BuildEmptyState(side);
            BuildDetails(side);
        }

        private static void BuildEmptyState(RectTransform side)
        {
            var empty = Node(side, "EmptyState");
            Stretch(empty,
                    LocationMockupTokens.PanelPadLeft, LocationMockupTokens.PanelPadTop,
                    LocationMockupTokens.PanelPadRight, LocationMockupTokens.PanelPadBottom);

            var icon = Node(empty, "Icon");
            At(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 96f, 96f, 0f, 90f);
            Pic(icon, Icon("Map"), Hex(LocationMockupTokens.HexDisabled));

            var text = Node(empty, "Text");
            At(text, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 460f, 130f, 0f, -20f);
            Label(text, "Selecione uma \u00E1rea no mapa para ver os detalhes.",
                  LocationMockupTokens.FontBody, FontStyles.Normal,
                  Hex(LocationMockupTokens.HexMutedPanel), TextAlignmentOptions.Top, wrap: true);

            // O mockup mostra a tela COM uma area escolhida, entao o estado vazio
            // nasce desligado. Todos os numeros dele sao estimativa.
            empty.gameObject.SetActive(false);
        }

        private static void BuildDetails(RectTransform side)
        {
            var accent = Hex(LocationMockupTokens.HexZoneFinance);

            var details = Node(side, "Details");
            Stretch(details,
                    LocationMockupTokens.PanelPadLeft, LocationMockupTokens.PanelPadTop,
                    LocationMockupTokens.PanelPadRight, LocationMockupTokens.PanelPadBottom);

            // ---- cabecalho de identidade ----
            float bd = LocationMockupTokens.BadgePanel;

            var badge = Node(details, "IconBadge");
            At(badge, new Vector2(0f, 1f), new Vector2(0f, 1f), bd, bd, 0f, 0f);
            Ringed(badge, LocationMockupTokens.RingBadgePanel, accent,
                   LocationMockupTokens.BadgeCore(accent));

            var badgeIcon = Node(badge, "Icon");
            Stretch(badgeIcon, bd * 0.25f, bd * 0.25f, bd * 0.25f, bd * 0.25f);
            // No cabecalho do painel o icone e BRANCO e quem carrega a cor e o
            // anel. Nos indicadores e o contrario.
            Pic(badgeIcon, Icon("Dollar"), Hex(LocationMockupTokens.HexInk));

            var title = Node(details, "ZoneTitle");
            TopBand(title, 50f, 124f, 0f, -4f);
            Label(title, Area.ToUpperInvariant() + " FINANCEIRA",
                  LocationMockupTokens.FontZoneTitle, FontStyles.Bold,
                  LocationMockupTokens.ZoneTitleTint(accent), TextAlignmentOptions.MidlineLeft);

            var zoneSub = Node(details, "ZoneSubtitle");
            TopBand(zoneSub, 36f, 124f, 0f, -53f);
            Label(zoneSub, "Institui\u00E7\u00E3o financeira",
                  LocationMockupTokens.FontZoneSubtitle, FontStyles.Normal,
                  Hex(LocationMockupTokens.HexMutedZone), TextAlignmentOptions.MidlineLeft);

            var desc = Node(details, "Description");
            TopBand(desc, 110f, 0f, 0f, -106f);
            Label(desc, "Regi\u00E3o nobre com grande fluxo de profissionais e empresas.",
                  LocationMockupTokens.FontBody, FontStyles.Normal,
                  Hex(LocationMockupTokens.HexMutedPanel), TextAlignmentOptions.TopLeft, wrap: true);

            BuildStats(details);
            BuildHighlight(details);
            BuildContinue(details);
        }

        private static void BuildStats(RectTransform details)
        {
            float h   = LocationMockupTokens.StatRowHeight;
            float gap = LocationMockupTokens.StatRowGap;

            var group = Node(details, "StatGroup");
            BottomBand(group, h * 3f + gap * 2f, 0f, 0f, 284f);

            BuildStatRow(group, "Stat_Investment", "Investimento", "Alto", "Graph",
                         LocationMockupTokens.HexZoneFinance, LocationMockupTokens.HexGoldValue, 0f);

            BuildStatRow(group, "Stat_Traffic", "Movimento", "M\u00E9dio", "Add_Friend",
                         LocationMockupTokens.HexZoneEducation, LocationMockupTokens.HexGoldValue, h + gap);

            // "Defense" e um escudo. O "Safe" que a tela antiga usa e um cofre.
            BuildStatRow(group, "Stat_Competition", "Concorr\u00EAncia", "Baixa", "Defense",
                         LocationMockupTokens.HexGood, LocationMockupTokens.HexGood, (h + gap) * 2f);
        }

        private static void BuildStatRow(RectTransform group, string name, string caption,
                                         string value, string iconName,
                                         string accentHex, string valueHex, float y)
        {
            var accent = Hex(accentHex);

            var row = Node(group, name);
            TopBand(row, LocationMockupTokens.StatRowHeight, 0f, 0f, -y);

            Framed(row,
                   LocationMockupTokens.RadiusCard,
                   LocationMockupTokens.BorderHairline,
                   Hex(LocationMockupTokens.HexRowBorder),
                   Hex(LocationMockupTokens.HexBg));

            float d = LocationMockupTokens.BadgeStat;

            var badge = Node(row, "IconBadge");
            At(badge, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), d, d, 16f, 0f);
            Ringed(badge, LocationMockupTokens.RingBadgeStat, accent,
                   LocationMockupTokens.BadgeCore(accent));

            var icon = Node(badge, "Icon");
            Stretch(icon, d * 0.25f, d * 0.25f, d * 0.25f, d * 0.25f);
            Pic(icon, Icon(iconName), accent);

            var captionRt = Node(row, "Caption");
            At(captionRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 300f, 44f, 94f, 0f);
            Label(captionRt, caption, LocationMockupTokens.FontStatRow, FontStyles.Normal,
                  Hex(LocationMockupTokens.HexInk), TextAlignmentOptions.MidlineLeft);

            var valueRt = Node(row, "Value");
            At(valueRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), 220f, 46f, -32f, 0f);
            Label(valueRt, value, LocationMockupTokens.FontStatRow, FontStyles.Bold,
                  Hex(valueHex), TextAlignmentOptions.MidlineRight);
        }

        private static void BuildHighlight(RectTransform details)
        {
            var box = Node(details, "HighlightBox");
            BottomBand(box, LocationMockupTokens.HighlightHeight, 0f, 0f, 166f);

            // O fill e MAIS ESCURO que o painel, nao uma lavagem azul clara.
            Framed(box,
                   LocationMockupTokens.RadiusCard,
                   LocationMockupTokens.BorderHairline,
                   Hex(LocationMockupTokens.HexAccentBlue),
                   Hex(LocationMockupTokens.HexHighlightFill));

            var icon = Node(box, "Icon");
            At(icon, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), 44f, 44f, 24f, 0f);
            Pic(icon, Icon("Star"), Hex(LocationMockupTokens.HexAccentBlueIcon));

            var text = Node(box, "Text");
            Stretch(text, 85f, 12f, 20f, 12f);
            Label(text, "Maior circula\u00E7\u00E3o em dias \u00FAteis",
                  LocationMockupTokens.FontMenu, FontStyles.Normal,
                  Hex(LocationMockupTokens.HexAccentBlueText), TextAlignmentOptions.MidlineLeft,
                  wrap: true);
        }

        private static void BuildContinue(RectTransform details)
        {
            var button = Node(details, "ContinueButton");
            BottomBand(button, LocationMockupTokens.ContinueHeight, 0f, 0f, 0f);

            HitArea(button);

            var frame = Node(button, "Frame");
            Stretch(frame);
            Sliced(frame, LocationMockupTokens.RadiusCard, Hex(LocationMockupTokens.HexPlateRim));

            var plate = Node(button, "Plate");
            Stretch(plate, 2f, 2f, 2f, 2f);
            var plateImage = Sliced(plate,
                                    LocationMockupTokens.RadiusCard - LocationMockupTokens.BorderHairline,
                                    Color.white);

            // BRANCO de proposito: o VerticalGradient MULTIPLICA a cor do
            // vertice. Se a chapa ja fosse dourada, o dourado entraria duas
            // vezes e o botao ficaria marrom.
            plateImage.color = Color.white;

            var gradient = plate.gameObject.AddComponent<VerticalGradient>();
            gradient.SetColors(Hex(LocationMockupTokens.HexPlateTop),
                               Hex(LocationMockupTokens.HexPlateBottom));

            // Os dois glints sao o que faz a chapa ler como metal, e custam
            // um retangulo cada.
            var topGlint = Node(button, "TopGlint");
            TopBand(topGlint, 2f, 6f, 6f, -2f);
            Solid(topGlint, Rgba(LocationMockupTokens.HexPlateGlintTop, 0.85f));

            var bottomGlint = Node(button, "BottomGlint");
            BottomBand(bottomGlint, 2f, 6f, 6f, 2f);
            Solid(bottomGlint, Rgba(LocationMockupTokens.HexPlateGlintBottom, 0.9f));

            var label = Node(button, "Label");
            Stretch(label);
            var text = Label(label, "CONTINUAR", LocationMockupTokens.FontContinue, FontStyles.Bold,
                             Hex(LocationMockupTokens.HexOnGold), TextAlignmentOptions.Center);
            text.characterSpacing = 8f;

            // O targetGraphic e a chapa: o ColorTint multiplica a malha inteira
            // de forma uniforme, entao o gradiente sobrevive ao hover.
            var component = MakeButton(button, 1.12f, 0.88f);
            component.targetGraphic = plateImage;
        }

        // =====================================================
        //  HELPERS DE HIERARQUIA E LAYOUT
        // =====================================================

        private static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Stretch(RectTransform rt, float left = 0f, float top = 0f,
                                             float right = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        private static RectTransform TopBand(RectTransform rt, float height, float left = 0f,
                                             float right = 0f, float y = 0f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -height + y);
            rt.offsetMax = new Vector2(-right, y);
            return rt;
        }

        private static RectTransform BottomBand(RectTransform rt, float height, float left = 0f,
                                                float right = 0f, float y = 0f)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, y);
            rt.offsetMax = new Vector2(-right, height + y);
            return rt;
        }

        private static RectTransform At(RectTransform rt, Vector2 anchor, Vector2 pivot,
                                        float width, float height, float x, float y)
        {
            rt.anchorMin        = anchor;
            rt.anchorMax        = anchor;
            rt.pivot            = pivot;
            rt.sizeDelta        = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }

        // =====================================================
        //  HELPERS DE PINTURA
        // =====================================================

        /// <summary>Retangulo chapado, sem sprite. Veu, traco, barra de icone.</summary>
        private static Image Solid(RectTransform rt, Color color, bool raycast = false)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite        = null;
            image.type          = Image.Type.Simple;
            image.color         = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>Retangulo arredondado 9-slice. O raio vem do ppuMultiplier.</summary>
        private static Image Sliced(RectTransform rt, float radius, Color color, bool raycast = false)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite                  = Load<Sprite>(ButtonSprite);
            image.type                    = Image.Type.Sliced;
            image.fillCenter              = true;
            image.pixelsPerUnitMultiplier = LocationMockupTokens.PpuForRadius(radius);
            image.color                   = color;
            image.raycastTarget           = raycast;
            return image;
        }

        private static Image Pic(RectTransform rt, Sprite sprite, Color color,
                                 bool preserveAspect = true)
        {
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite         = sprite;
            image.type           = Image.Type.Simple;
            image.color          = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget  = false;
            return image;
        }

        /// <summary>
        /// Moldura + miolo concentricos. E a unica forma de ter raio 13 com
        /// borda 2 - ver o cabecalho da classe.
        /// </summary>
        /// <returns>O Image do miolo, para quem precisa dele como targetGraphic.</returns>
        private static Image Framed(RectTransform box, float radius, float thickness,
                                    Color borderColor, Color fillColor)
        {
            var frame = Node(box, "Frame");
            Stretch(frame);
            Sliced(frame, radius, borderColor);

            var fill = Node(box, "Fill");
            Stretch(fill, thickness, thickness, thickness, thickness);
            return Sliced(fill, radius - thickness, fillColor);
        }

        /// <summary>
        /// Anel + miolo circulares. Circle.png nao tem borda 9-slice, entao
        /// fillCenter nao funciona nele; dois circulos concentricos e o caminho.
        /// </summary>
        private static void Ringed(RectTransform box, float thickness,
                                   Color ringColor, Color coreColor)
        {
            var ring = Node(box, "Ring");
            Stretch(ring);
            Pic(ring, Load<Sprite>(CircleSprite), ringColor, preserveAspect: false);

            var core = Node(box, "Core");
            Stretch(core, thickness, thickness, thickness, thickness);
            Pic(core, Load<Sprite>(CircleSprite), coreColor, preserveAspect: false);
        }

        private static TextMeshProUGUI Label(RectTransform rt, string content, float size,
                                             FontStyles style, Color color,
                                             TextAlignmentOptions alignment, bool wrap = false)
        {
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();

            var font = Load<TMP_FontAsset>(FontPath);

            if (font != null)
                label.font = font;

            label.text               = content;
            label.fontSize           = size;
            label.fontStyle          = style;
            label.color              = color;
            label.alignment          = alignment;
            label.textWrappingMode   = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode       = TextOverflowModes.Overflow;
            label.raycastTarget      = false;
            label.characterSpacing   = 0f;

            return label;
        }

        /// <summary>
        /// Alvo de raycast invisivel cobrindo o objeto inteiro. Sem isto, trocar
        /// o visual de fundo de um botao o deixa mudo - sem erro nenhum no
        /// Console.
        /// </summary>
        private static Image HitArea(RectTransform target)
        {
            var hit = Node(target, "HitArea");
            Stretch(hit);

            var image = hit.gameObject.AddComponent<Image>();
            image.sprite        = null;
            image.color         = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;

            return image;
        }

        /// <summary>
        /// Botao com ColorTint. Em tema escuro o hover precisa CLAREAR, e o
        /// ColorTint so multiplica - nunca passaria de 1. A saida e usar o
        /// colorMultiplier como teto e guardar as cores ja divididas por ele.
        /// </summary>
        private static Button MakeButton(RectTransform rt, float highlighted, float pressed)
        {
            const float ceiling = 1.5f;

            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.ColorTint;

            var colors = button.colors;
            colors.colorMultiplier  = ceiling;
            colors.normalColor      = Gray(1f / ceiling);
            colors.highlightedColor = Gray(Mathf.Clamp(highlighted, 0f, ceiling) / ceiling);
            colors.pressedColor     = Gray(Mathf.Clamp(pressed, 0f, ceiling) / ceiling);
            colors.selectedColor    = Gray(1f / ceiling);
            colors.disabledColor    = new Color(1f / ceiling, 1f / ceiling, 1f / ceiling, 0.4f);
            colors.fadeDuration     = 0.12f;
            button.colors           = colors;

            return button;
        }

        private static Color Gray(float v)
        {
            return new Color(v, v, v, 1f);
        }

        // =====================================================
        //  ASSETS
        // =====================================================

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
                Debug.LogWarning("[LocationScreenMockupBuilder] Asset nao encontrado: " + path);

            return asset;
        }

        private static Sprite Icon(string shortName)
        {
            return Load<Sprite>(IconFolder + "Icon_PictoIcon_" + shortName + ".Png");
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        private static Color Rgba(string hex, float alpha)
        {
            var c = Hex(hex);
            c.a = alpha;
            return c;
        }

        /// <summary>
        /// botao.png vem sem borda 9-slice. Sem borda, Image.Type.Sliced nao
        /// funciona e os cantos esticam. Configura uma unica vez.
        /// </summary>
        private static void EnsureButtonSpriteBorder()
        {
            var importer = AssetImporter.GetAtPath(ButtonSprite) as TextureImporter;

            if (importer == null)
                return;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            var border = new Vector4(48f, 48f, 48f, 48f);
            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (settings.spriteBorder != border)
            {
                settings.spriteBorder = border;
                importer.SetTextureSettings(settings);
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }
    }
}
#endif
