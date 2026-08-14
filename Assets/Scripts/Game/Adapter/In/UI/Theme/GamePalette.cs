using UnityEngine;

namespace Game.Adapter.In.UI.Theme
{
    /// <summary>
    /// Fonte unica de verdade das cores do jogo.
    /// Nenhum outro script deve conter cor "hard coded".
    ///
    /// PALETA ATUAL: navy profundo com dourado, extraida do mockup da tela
    /// "Abertura do Restaurante".
    ///
    /// A logica dela:
    ///  - O FUNDO e quase preto azulado. Isso deixa o mapa ilustrado ser o
    ///    unico ponto colorido da tela, sem competicao.
    ///  - O DOURADO e a cor de acao. Aparece so onde o jogador deve agir:
    ///    botao principal, botao de menu, valores em destaque. Usado com
    ///    parcimonia, ele vira o guia do olho.
    ///  - Cada ZONA tem sua cor, e ela vem do colorHex do LocationData.
    ///    As constantes aqui sao apenas o padrao de fallback.
    /// </summary>
    public static class GamePalette
    {
        // =====================================================
        //  BASE - navy profundo
        // =====================================================

        public const string HexBackground   = "#080D1A"; // fundo da tela
        public const string HexChrome       = "#0B1224"; // barra superior e rodape
        public const string HexSurface      = "#0E1526"; // painel lateral, cards
        public const string HexSurfaceAlt   = "#131C33"; // linha de indicador, chip
        public const string HexHairline     = "#1E2B4A"; // borda de 1px
        public const string HexBorderStrong = "#2C3E68"; // borda de enfase

        /// <summary>Fundo dos cards de rotulo sobre o mapa. Escuro e quase opaco.</summary>
        public const string HexMapLabel = "#0D1424";
        public const float  MapLabelAlpha = 0.92f;

        // =====================================================
        //  TEXTO
        // =====================================================

        public const string HexInk        = "#FFFFFF"; // titulo e valor
        public const string HexInkBody    = "#FFFFFF"; // corpo
        public const string HexInkCaption = "#FFFFFF"; // legenda

        /// <summary>Texto de apoio: descricoes, subtitulos, rotulo de indicador.</summary>
        public const string HexMuted = "#97A3B8";

        /// <summary>Cinza mais apagado ainda: estados desabilitados.</summary>
        public const string HexGray = "#6E7A90";

        // =====================================================
        //  DOURADO - a cor de acao
        // =====================================================

        public const string HexGold       = "#F2C75C"; // texto e icone dourado
        public const string HexGoldDeep   = "#D9A63C"; // base do gradiente do botao
        public const string HexGoldBorder = "#C9A24A"; // contorno do botao de menu
        public const string HexOnGold     = "#201703"; // texto por cima do dourado

        // Aliases para o resto do codigo continuar falando "primary".
        public const string HexPrimary      = HexGold;
        public const string HexPrimaryDark  = HexGoldDeep;
        public const string HexPrimaryHover = "#FFD97A";
        public const string HexPrimarySoft  = "#2A2412";
        public const string HexOnPrimary    = HexOnGold;
        public const string HexOnWarn       = HexOnGold;

        // =====================================================
        //  AZUL DE SELECAO
        // =====================================================

        /// <summary>Realce da area escolhida no mapa e do card selecionado.</summary>
        public const string HexAccentBlue     = "#2F80F5";
        public const string HexAccentBlueSoft = "#2F80F526";

        // =====================================================
        //  SEMANTICO
        // =====================================================

        public const string HexMoney  = "#4ADE80"; // positivo, concorrencia baixa
        public const string HexWarn   = "#F0912E"; // atencao
        public const string HexDanger = "#EF5350"; // negativo
        public const string HexXp     = HexGold;   // score

        // =====================================================
        //  CORES DE ZONA
        //  Fallback: o valor real vem do colorHex de cada LocationData.
        // =====================================================

        public const string HexZoneFinance     = "#2F80F5"; // Area Financeira
        public const string HexZoneEducation   = "#A855F7"; // Area Educacional
        public const string HexZoneCommerce    = "#F0912E"; // Area Comercial
        public const string HexZoneResidential = "#4ADE80"; // Area Residencial
        public const string HexZoneCorporate   = "#22D3EE"; // Area Corporativa

        // Nodes do mapa do menu, alinhados as mesmas zonas.
        public const string HexNodeBank  = HexZoneFinance;
        public const string HexNodeStore = HexZoneCommerce;
        public const string HexNodeHome  = HexGold;
        public const string HexNodeRh    = HexZoneEducation;
        public const string HexNodeMenu  = HexZoneCorporate;

        // Fundo dos badges de icone: a propria cor com alpha baixo.
        public const string HexSoftBank  = "#2F80F540";
        public const string HexSoftStore = "#F0912E40";
        public const string HexSoftHome  = "#F2C75C40";
        public const string HexSoftRh    = "#A855F740";
        public const string HexSoftMenu  = "#22D3EE40";

        // =====================================================
        //  VEU E SOMBRA
        // =====================================================

        /// <summary>
        /// Veu sobre o mapa. Leve: aqui o mapa E o assunto da tela, entao ele
        /// so precisa de um rebaixamento suave para os cards lerem por cima.
        /// </summary>
        public const string HexMapScrim   = "#060A14";
        public const float  MapScrimAlpha = 0.32f;

        public const string HexShadow   = "#01030A";
        public const float  ShadowAlpha = 0.55f;

        // =====================================================
        //  TOKENS DAS TELAS ANTIGAS
        //  Usados pelo LegacyScreenRestyler e pelos scripts das telas
        //  montadas a mao.
        // =====================================================

        public const string HexLegacyHeader     = HexChrome;
        public const string HexLegacyBackground = HexBackground;
        public const string HexLegacyCard       = HexSurface;
        public const string HexLegacyFooter     = "#0A101F";
        public const string HexLegacyMuted      = HexMuted;

        /// <summary>Botao Confirmar. Dourado, como o CONTINUAR do mockup.</summary>
        public const string HexConfirm = HexGold;

        // ---- Selecao de card no RestaurantScreenView ----
        public const string HexCardNormal      = HexSurface;
        public const string HexCardSelected    = HexAccentBlue; // com alpha 0.18
        public const float  CardSelectedAlpha  = 0.18f;
        public const string HexBorderNormal    = HexHairline;
        public const string HexBorderSelected  = HexAccentBlue;
        public const string HexSegmentSelected = HexAccentBlue;
        public const string HexDisabledCard    = "#3A4256"; // alpha 0.85
        public const string HexDisabledBank    = "#4A5164"; // alpha 0.85
        public const float  DisabledAlpha      = 0.85f;

        // ---- Faixas de pontuacao ----
        public const string HexScoreGood    = HexMoney;
        public const string HexScoreAverage = HexGold;
        public const string HexScoreBad     = HexDanger;

        /// <summary>Cor da barra de pontuacao conforme o valor normalizado 0..1.</summary>
        public static Color ScoreColor(float normalized)
        {
            if (normalized >= 0.7f) return Parse(HexScoreGood);
            if (normalized >= 0.4f) return Parse(HexScoreAverage);
            return Parse(HexScoreBad);
        }

        // =====================================================
        //  RAIOS (pixelsPerUnitMultiplier no Image sliced)
        //  botao.png tem raio nativo de ~36px. Multiplier maior => raio menor.
        // =====================================================

        public const float PpuNodeBig = 1.6f;  // ~22px
        public const float PpuCard    = 2.2f;  // ~16px
        public const float PpuButton  = 3.2f;  // ~11px
        public const float PpuPill    = 0.8f;  // ~45px

        // =====================================================
        //  Conversao HEX -> Color
        // =====================================================

        public static Color Parse(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
        }

        public static Color Parse(string hex, float alpha)
        {
            var c = Parse(hex);
            c.a = alpha;
            return c;
        }

        /// <summary>Mesma cor com outro alpha. Util para halos e fundos de badge.</summary>
        public static Color WithAlpha(Color c, float alpha)
        {
            c.a = alpha;
            return c;
        }

        public static Color Background   => Parse(HexBackground);
        public static Color Chrome       => Parse(HexChrome);
        public static Color Surface      => Parse(HexSurface);
        public static Color SurfaceAlt   => Parse(HexSurfaceAlt);
        public static Color Hairline     => Parse(HexHairline);
        public static Color BorderStrong => Parse(HexBorderStrong);
        public static Color MapLabel     => Parse(HexMapLabel, MapLabelAlpha);

        public static Color Ink        => Parse(HexInk);
        public static Color InkBody    => Parse(HexInkBody);
        public static Color InkCaption => Parse(HexInkCaption);
        public static Color Muted      => Parse(HexMuted);
        public static Color Gray       => Parse(HexGray);

        public static Color Gold       => Parse(HexGold);
        public static Color GoldDeep   => Parse(HexGoldDeep);
        public static Color GoldBorder => Parse(HexGoldBorder);
        public static Color OnGold     => Parse(HexOnGold);

        public static Color Primary      => Parse(HexPrimary);
        public static Color PrimaryDark  => Parse(HexPrimaryDark);
        public static Color PrimaryHover => Parse(HexPrimaryHover);
        public static Color PrimarySoft  => Parse(HexPrimarySoft);
        public static Color OnPrimary    => Parse(HexOnPrimary);

        public static Color AccentBlue => Parse(HexAccentBlue);
        public static Color Money      => Parse(HexMoney);
        public static Color Warn       => Parse(HexWarn);
        public static Color Danger     => Parse(HexDanger);
        public static Color Xp         => Parse(HexXp);

        public static Color MapScrim => Parse(HexMapScrim, MapScrimAlpha);
        public static Color Shadow   => Parse(HexShadow, ShadowAlpha);

        /// <summary>Verde quando positivo, vermelho quando negativo.</summary>
        public static Color ResultColor(float value)
        {
            return value >= 0f ? Money : Danger;
        }
    }
}
