using UnityEngine;

namespace Game.Adapter.In.UI.Theme
{
    /// <summary>
    /// Tokens MEDIDOS no mockup "Abertura do Restaurante" (new.png, 1741x903).
    ///
    /// Por que isto existe em vez de entrar no GamePalette:
    /// varios valores aqui divergem da paleta atual, e a paleta e usada por
    /// TODAS as telas do jogo. Trocar HexMoney, por exemplo, mexeria em
    /// ResultColor, ScoreColor e no restyler das telas legadas. Estes tokens
    /// ficam isolados para a cena de mockup poder ser fiel ao pixel sem
    /// arrastar o resto do jogo junto.
    ///
    /// Quando a tela for aprovada, migre o que fizer sentido para o GamePalette.
    ///
    /// FATOR DE CONVERSAO usado para chegar nos numeros de canvas:
    ///   area util do mockup = (66,49)..(1673,851) = 1608 x 803 px
    ///   S = 1920 / 1608 = 1.19403
    /// O mockup e 2:1; o Canvas e 16:9. A folga vertical de ~121px foi colocada
    /// de proposito num unico lugar: entre a Descricao e o primeiro indicador.
    /// </summary>
    public static class LocationMockupTokens
    {
        // =====================================================
        //  SUPERFICIES
        // =====================================================

        /// <summary>Fundo da tela, do painel lateral E das linhas de indicador.
        /// No mockup os tres sao a MESMA cor - o que separa cada caixa e a
        /// borda, nao um fill mais claro.</summary>
        public const string HexBg = "#090B1D";

        public const string HexChrome        = "#0A0B1A"; // barra superior
        public const string HexMenuFill      = "#101019"; // miolo do botao MENU
        public const string HexCardMap       = "#060E21"; // card do marcador
        public const float  CardMapAlpha     = 0.94f;
        public const string HexHighlightFill = "#040B20"; // caixa de destaque (MAIS escura que o painel)
        public const string HexCrestFill     = "#242123"; // miolo do brasao

        // =====================================================
        //  BORDAS
        // =====================================================

        public const string HexPanelBorder  = "#4B4275"; // painel lateral
        public const string HexRowBorder    = "#26253E"; // linhas de indicador
        public const string HexCardBorder   = "#1E2B4A"; // card do marcador em repouso

        // =====================================================
        //  TEXTO
        // =====================================================

        public const string HexInk        = "#FFFFFF"; // titulo, rotulos, nome do marcador
        public const string HexInkSubtle  = "#E6EAF2"; // subtitulo do marcador
        public const string HexMuted      = "#C2C5C7"; // subtitulo da tela
        public const string HexMutedPanel = "#B4B4BE"; // descricao do painel
        public const string HexMutedZone  = "#BCBEC2"; // subtitulo de zona
        public const string HexDisabled   = "#6E7A90"; // estado vazio

        // =====================================================
        //  DOURADO
        // =====================================================

        public const string HexGoldStep     = "#F0CE6E"; // "ETAPA 1 DE 4"
        public const string HexGoldText     = "#EFC868"; // texto e icone do MENU
        public const string HexGoldValue    = "#F7CE66"; // valores Alto / Medio
        public const string HexGoldBorder   = "#C9A05A"; // contorno do MENU
        public const string HexGoldOrnament = "#C29A52"; // losangos e traços
        public const string HexGoldCrest    = "#B39A5F"; // anel do brasao

        // ---- chapa do botao CONTINUAR ----
        public const string HexPlateTop         = "#F0C05A";
        public const string HexPlateBottom      = "#C5882A";
        public const string HexPlateRim         = "#F6D372";
        public const string HexPlateGlintTop    = "#FFFFFF";
        public const string HexPlateGlintBottom = "#FFE070";
        public const string HexOnGold           = "#100C05";

        // =====================================================
        //  AZUL DE DESTAQUE
        // =====================================================

        public const string HexAccentBlue     = "#0080E0"; // borda da caixa de destaque
        public const string HexAccentBlueText = "#42A9F1"; // texto da caixa
        public const string HexAccentBlueIcon = "#2A88F8"; // estrela

        // =====================================================
        //  ZONAS - medidas no anel de cada badge
        // =====================================================

        public const string HexZoneFinance     = "#1E8FF5";
        public const string HexZoneEducation   = "#C165D6";
        public const string HexZoneCommerce    = "#F6962D";
        public const string HexZoneResidential = "#A1E741";
        public const string HexZoneCorporate   = "#1EE8E5";

        /// <summary>Verde do valor "Baixa". Deliberadamente NAO e o HexMoney da
        /// paleta - mexer naquele afetaria as telas financeiras.</summary>
        public const string HexGood = "#95DD69";

        public const string HexMapScrim   = "#060A14";
        public const float  MapScrimAlpha = 0.18f;

        // =====================================================
        //  DERIVACOES
        // =====================================================

        /// <summary>
        /// Miolo de qualquer badge. Na arte ele nunca e a cor cheia da zona:
        /// e um escuro levemente tingido por ela. Medido #0C2756 na Financeira,
        /// que e exatamente este Lerp.
        /// </summary>
        public static Color BadgeCore(Color accent)
        {
            return Color.Lerp(GamePalette.Parse("#050A18"), accent, 0.22f);
        }

        /// <summary>
        /// Titulo da zona no painel lateral. No mockup ele e ~2 tons MAIS CLARO
        /// que a cor da zona (medido #7EC4F5 contra o #1E8FF5 do anel), porque
        /// carrega um glow. Sem isto o titulo some no fundo escuro.
        /// </summary>
        public static Color ZoneTitleTint(Color accent)
        {
            return Color.Lerp(accent, Color.white, 0.42f);
        }

        // =====================================================
        //  GEOMETRIA
        // =====================================================

        public const float TopBarHeight     = 132f;
        public const float MapWidthFraction = 0.70f;

        public const float PanelPadLeft   = 40f;
        public const float PanelPadTop    = 55f;
        public const float PanelPadRight  = 40f;
        public const float PanelPadBottom = 24f;

        public const float StatRowHeight   = 88f;
        public const float StatRowGap      = 12f;
        public const float HighlightHeight = 88f;
        public const float ContinueHeight  = 100f;

        public const float BadgePanel  = 80f;
        public const float BadgeStat   = 56f;
        public const float BadgeMarker = 74f;
        public const float CrestSize   = 84f;

        public const float MarkerCardHeight = 86f;
        public const float MarkerHeight     = 160f; // badge 74 + card 86

        // ---- raios de canto, em px de canvas ----
        public const float RadiusPanel = 26f;
        public const float RadiusPill  = 20f;
        public const float RadiusCard  = 13f;

        // ---- espessuras ----
        public const float BorderHairline = 2f;
        public const float BorderSelected = 3f;
        public const float RingCrest      = 3f;
        public const float RingBadgePanel = 3f;
        public const float RingBadgeStat  = 3f;
        public const float RingBadgeMarker = 4f;

        // =====================================================
        //  TIPOGRAFIA - InterTight (razao cap/em = 0.734)
        // =====================================================

        public const float FontTitle        = 60f; // "Abertura do Restaurante"
        public const float FontContinue     = 39f; // "CONTINUAR"
        public const float FontZoneTitle    = 37f; // "AREA FINANCEIRA"
        public const float FontMarkerName   = 29f;
        public const float FontStatRow      = 28f; // rotulo e valor
        public const float FontMenu         = 26f; // MENU e texto de destaque
        public const float FontBody         = 24f; // subtitulo da tela, descricao
        public const float FontZoneSubtitle = 23f;
        public const float FontMarkerSub    = 21f;
        public const float FontStep         = 20f; // "ETAPA 1 DE 4"

        // =====================================================
        //  9-SLICE
        //  botao.png: spriteBorder 48, spritePixelsToUnits 100.
        //  Canvas: referencePixelsPerUnit 100.
        //  Logo:  raio = 36 / ppuMultiplier  e  borda = 48 / ppuMultiplier.
        //
        //  Consequencia importante: raio e espessura sao a MESMA variavel.
        //  E matematicamente impossivel ter raio 13 e borda 2 com um unico
        //  Image sliced usando fillCenter = false - ele desenharia 15px.
        //  Por isso toda caixa desta tela usa Frame + Fill (ver o builder).
        // =====================================================

        public static float PpuForRadius(float radiusPx)
        {
            return 36f / Mathf.Max(radiusPx, 0.5f);
        }
    }
}
