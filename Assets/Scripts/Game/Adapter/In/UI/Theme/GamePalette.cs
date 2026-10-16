using UnityEngine;

namespace Game.Adapter.In.UI.Theme
{
    /// <summary>Approved art palette. Compatibility aliases never restore the former theme.</summary>
    public static class GamePalette
    {
        public const string HexViolet = "#6D28D9";
        public const string HexPlum = "#24113D";
        public const string HexLavender = "#B99AE8";
        public const string HexIvory = "#F5F0E8";
        public const string HexErrorText = "#A6323D";
        public const string HexPositiveOnDark = "#8DD4C5";
        public const string HexNegativeOnDark = "#F2A4A6";
        public static Color OnDark => Parse(HexIvory);
        public static Color Plum => Parse(HexPlum);
        public static Color Lavender => Parse(HexLavender);
        public static Color ErrorText => Parse(HexErrorText);
        public static Color TextOn(Color surface) => surface.grayscale > .5f ? Ink : OnDark;
        public static Color TextOnSurface(Transform parent)
        {
            for (var t=parent;t!=null;t=t.parent)
            {
                var image=t.GetComponent<UnityEngine.UI.Image>();
                if(image==null || image.color.a<.5f) continue;
                string sprite=image.sprite!=null?image.sprite.name:"";
                if(sprite.Contains("primary_violet") || sprite.Contains("topbar_violet") || sprite.Contains("navbar_plum") || sprite.Contains("panel_plum")) return OnDark;
                if(sprite.Contains("ivory") || sprite.Contains("card_normal") || sprite.Contains("lavender")) return Ink;
                if(image.color!=Color.white) return TextOn(image.color);
            }
            return Ink;
        }
        public static Color SemanticOn(Color surface, float value) => surface.grayscale > .5f
            ? (value >= 0 ? Money : ErrorText)
            : Parse(value >= 0 ? HexPositiveOnDark : HexNegativeOnDark);

        // =====================================================

        // =====================================================

        public const string HexBackground   = HexPlum; // fundo da tela
        public const string HexChrome       = HexViolet; // barra superior e rodape
        public const string HexSurface      = HexIvory; // painel lateral, cards
        public const string HexSurfaceAlt   = "#E8DDF4"; // linha de indicador, chip
        public const string HexHairline     = HexLavender; // borda de 1px
        public const string HexBorderStrong = HexViolet; // borda de enfase

        /// <summary>Fundo dos cards de rotulo sobre o mapa. Escuro e quase opaco.</summary>
        public const string HexMapLabel = HexPlum;
        public const float  MapLabelAlpha = 0.92f;

        // =====================================================
        //  TEXTO
        // =====================================================

        public const string HexInk        = "#30243C"; // titulo e valor
        public const string HexInkBody    = HexInk; // corpo
        public const string HexInkCaption = HexInk; // legenda

        /// <summary>Texto de apoio: descricoes, subtitulos, rotulo de indicador.</summary>
        public const string HexMuted = "#65566F";

        /// <summary>Cinza mais apagado ainda: estados desabilitados.</summary>
        public const string HexGray = "#74677E";

        // =====================================================

        // =====================================================

        public const string HexGold       = HexPrimary;
        public const string HexGoldDeep   = HexPrimaryDark; // base do gradiente do botao
        public const string HexGoldBorder = HexLavender; // contorno do botao de menu
        public const string HexOnGold     = HexOnPrimary;

        // Aliases para o resto do codigo continuar falando "primary".
        public const string HexPrimary      = HexViolet;
        public const string HexPrimaryDark  = "#5420A6";
        public const string HexPrimaryHover = "#7C3DEA";
        public const string HexPrimarySoft  = HexLavender;
        public const string HexOnPrimary    = HexIvory;
        public const string HexOnWarn       = HexPlum;

        // =====================================================
        //  SELECTION
        // =====================================================

        /// <summary>Realce da area escolhida no mapa e do card selecionado.</summary>
        public const string HexAccentBlue     = HexViolet;
        public const string HexAccentBlueSoft = "#6D28D926";

        // =====================================================
        //  SEMANTICO
        // =====================================================

        public const string HexMoney  = "#26776A"; // positivo, concorrencia baixa
        public const string HexWarn   = "#E7B65A"; // atencao
        public const string HexDanger = "#C55457"; // negativo
        public const string HexXp     = HexViolet;   // score

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
        public const string HexSoftHome  = "#B99AE840";
        public const string HexSoftRh    = "#A855F740";
        public const string HexSoftMenu  = "#22D3EE40";

        // =====================================================
        //  VEU E SOMBRA
        // =====================================================

        /// <summary>
        /// Veu sobre o mapa. Leve: aqui o mapa E o assunto da tela, entao ele
        /// so precisa de um rebaixamento suave para os cards lerem por cima.
        /// </summary>
        public const string HexMapScrim   = HexPlum;
        public const float  MapScrimAlpha = 0.32f;

        public const string HexShadow   = HexPlum;
        public const float  ShadowAlpha = 0.55f;

        // =====================================================
        //  TOKENS DAS TELAS ANTIGAS
        //  Usados pelo LegacyScreenRestyler e pelos scripts das telas
        //  montadas a mao.
        // =====================================================

        public const string HexLegacyHeader     = HexChrome;
        public const string HexLegacyBackground = HexBackground;
        public const string HexLegacyCard       = HexSurface;
        public const string HexLegacyFooter     = HexPlum;
        public const string HexLegacyMuted      = HexMuted;


        public const string HexConfirm = HexPrimary;

        // ---- Selecao de card no RestaurantScreenView ----
        public const string HexCardNormal      = HexSurface;
        public const string HexCardSelected    = HexLavender;
        public const float  CardSelectedAlpha  = 1f;
        public const string HexBorderNormal    = HexHairline;
        public const string HexBorderSelected  = HexViolet;
        public const string HexSegmentSelected = HexLavender;
        public const string HexDisabledCard    = "#D8CFDF"; // alpha 0.85
        public const string HexDisabledBank    = HexDisabledCard; // alpha 0.85
        public const float  DisabledAlpha      = 0.85f;

        // ---- Faixas de pontuacao ----
        public const string HexScoreGood    = HexMoney;
        public const string HexScoreAverage = HexWarn;
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
