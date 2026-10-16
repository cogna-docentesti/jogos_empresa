using UnityEngine;

namespace Game.Adapter.In.UI.Theme
{
    /// <summary>Compatibility geometry for the location builder; styles share the global theme.</summary>
    public static class LocationMockupTokens
    {
        // =====================================================
        //  SUPERFICIES
        // =====================================================

        /// <summary>Fundo da tela, do painel lateral E das linhas de indicador.
        /// No mockup os tres sao a MESMA cor - o que separa cada caixa e a
        /// borda, nao um fill mais claro.</summary>
        public const string HexBg = GamePalette.HexBackground;

        public const string HexChrome        = GamePalette.HexChrome; // barra superior
        public const string HexMenuFill      = GamePalette.HexPlum; // miolo do botao MENU
        public const string HexCardMap       = GamePalette.HexPlum; // card do marcador
        public const float  CardMapAlpha     = 0.94f;
        public const string HexHighlightFill = GamePalette.HexLavender; // caixa de destaque (MAIS escura que o painel)
        public const string HexCrestFill     = GamePalette.HexPlum; // miolo do brasao

        // =====================================================
        //  BORDAS
        // =====================================================

        public const string HexPanelBorder  = GamePalette.HexLavender; // painel lateral
        public const string HexRowBorder    = GamePalette.HexHairline; // linhas de indicador
        public const string HexCardBorder   = GamePalette.HexLavender; // card do marcador em repouso

        // =====================================================
        //  TEXTO
        // =====================================================

        public const string HexInk        = GamePalette.HexInk; // titulo, rotulos, nome do marcador
        public const string HexInkSubtle  = GamePalette.HexOnPrimary; // subtitulo do marcador
        public const string HexMuted      = GamePalette.HexOnPrimary; // subtitulo da tela
        public const string HexMutedPanel = GamePalette.HexMuted; // descricao do painel
        public const string HexMutedZone  = GamePalette.HexMuted; // subtitulo de zona
        public const string HexDisabled   = GamePalette.HexGray; // estado vazio

        // =====================================================
        //  DOURADO
        // =====================================================

        public const string HexGoldStep     = GamePalette.HexOnPrimary; // "ETAPA 1 DE 4"
        public const string HexGoldText     = GamePalette.HexOnPrimary; // texto e icone do MENU
        public const string HexGoldValue    = GamePalette.HexOnWarn; // valores Alto / Medio
        public const string HexGoldBorder   = GamePalette.HexLavender; // contorno do MENU
        public const string HexGoldOrnament = GamePalette.HexLavender; // losangos e traços
        public const string HexGoldCrest    = GamePalette.HexLavender; // anel do brasao

        // ---- chapa do botao CONTINUAR ----
        public const string HexPlateTop         = GamePalette.HexPrimary;
        public const string HexPlateBottom      = GamePalette.HexPrimary;
        public const string HexPlateRim         = GamePalette.HexPrimary;
        public const string HexPlateGlintTop    = GamePalette.HexPrimary;
        public const string HexPlateGlintBottom = GamePalette.HexPrimary;
        public const string HexOnGold           = GamePalette.HexOnPrimary;

        // =====================================================
        //  AZUL DE DESTAQUE
        // =====================================================

        public const string HexAccentBlue     = GamePalette.HexPrimary; // borda da caixa de destaque
        public const string HexAccentBlueText = GamePalette.HexPlum; // texto da caixa
        public const string HexAccentBlueIcon = GamePalette.HexPrimary; // estrela

        // =====================================================
        //  ZONAS - medidas no anel de cada badge
        // =====================================================

        public const string HexZoneFinance     = GamePalette.HexZoneFinance;
        public const string HexZoneEducation   = GamePalette.HexZoneEducation;
        public const string HexZoneCommerce    = GamePalette.HexZoneCommerce;
        public const string HexZoneResidential = GamePalette.HexZoneResidential;
        public const string HexZoneCorporate   = GamePalette.HexZoneCorporate;

        /// <summary>Verde do valor "Baixa". Deliberadamente NAO e o HexMoney da
        /// paleta - mexer naquele afetaria as telas financeiras.</summary>
        public const string HexGood = GamePalette.HexMoney;

        public const string HexMapScrim   = GamePalette.HexPlum;
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
            return Color.Lerp(GamePalette.Plum, accent, 0.22f);
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

        public const float FontTitle        = GameTypography.ScreenTitle; // "Abertura do Restaurante"
        public const float FontContinue     = GameTypography.Button; // "CONTINUAR"
        public const float FontZoneTitle    = GameTypography.SectionTitle; // "AREA FINANCEIRA"
        public const float FontMarkerName   = GameTypography.NodeTitle;
        public const float FontStatRow      = GameTypography.Body; // rotulo e valor
        public const float FontMenu         = GameTypography.Button; // MENU e texto de destaque
        public const float FontBody         = GameTypography.Body; // subtitulo da tela, descricao
        public const float FontZoneSubtitle = GameTypography.Label;
        public const float FontMarkerSub    = GameTypography.Caption;
        public const float FontStep         = GameTypography.Caption; // "ETAPA 1 DE 4"

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
