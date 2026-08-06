using UnityEngine;

namespace Game.Adapter.In.UI.Theme
{
    /// <summary>
    /// Fonte unica de verdade das cores do jogo.
    /// Trocar a paleta = trocar apenas as constantes HEX aqui e rodar
    /// Tools > Jogo > 3 - Construir Tudo de novo.
    /// Nenhum outro script deve conter cor "hard coded".
    ///
    /// PALETA ATUAL: o mesmo dark navy das telas que ja existem, so que
    /// alguns pontos mais claro em cada degrau. A referencia antiga esta
    /// anotada em cada linha para voce medir o quanto subiu.
    /// </summary>
    public static class GamePalette
    {
        // ---------- BASE ----------
        public const string HexBackground   = "#122540"; // antes #0D1B2D - fundo geral
        public const string HexChrome       = "#0F1E33"; // antes #0A1220 - topbar e header
        public const string HexSurface      = "#1E3352"; // antes #1A2A3E - card e node
        public const string HexSurfaceAlt   = "#182C49"; // trilho, chip, barra vazia
        public const string HexHairline     = "#2B4C79"; // antes #1E3A5F - borda de 1px
        public const string HexBorderStrong = "#3A6099"; // borda de enfase

        // ---------- TEXTO ----------
        // Branco puro em tudo, seguindo o padrao das telas ja prontas e o
        // ajuste manual do Panel_Menu. Texto apagado sobre fundo escuro estava
        // custando legibilidade sem ganhar nada em hierarquia - quem separa os
        // niveis aqui e o TAMANHO, nao o tom.
        public const string HexInk        = "#FFFFFF"; // titulo
        public const string HexInkBody    = "#FFFFFF"; // corpo
        public const string HexInkCaption = "#FFFFFF"; // legenda

        /// <summary>Unico texto realmente apagado: botao Voltar e notas de rodape.</summary>
        public const string HexMuted = "#7A9CC0";

        // ---------- ACAO ----------
        public const string HexPrimary     = "#3D82F7"; // antes #2563EB
        public const string HexPrimaryDark = "#2563EB";
        public const string HexPrimarySoft = "#1E3A6B"; // fundo de selecao
        public const string HexOnPrimary   = "#FFFFFF";

        // ---------- SEMANTICO ----------
        public const string HexMoney  = "#2ECC71"; // antes #16A34A - caixa / positivo
        public const string HexWarn   = "#F0A02E"; // antes #D97706
        public const string HexDanger = "#EF5350"; // antes #DC2626
        public const string HexXp     = "#A78BFA"; // antes #7C3AED - score

        /// <summary>Texto por cima do ambar. Escuro, porque o ambar e claro demais para texto branco.</summary>
        public const string HexOnWarn = "#1B1305";

        // ---------- ACCENT POR NODE DO MAPA ----------
        public const string HexNodeBank  = "#3D82F7";
        public const string HexNodeStore = "#F0A02E";
        public const string HexNodeHome  = "#2ECC71";
        public const string HexNodeRh    = "#A78BFA";
        public const string HexNodeMenu  = "#2DD4E0";

        // ---------- FUNDO DOS BADGES DE ICONE ----------
        // Accent com alpha baixo (os 2 ultimos digitos), para o badge acender
        // por cima do card escuro sem virar um bloco pastel.
        public const string HexSoftBank  = "#3D82F733";
        public const string HexSoftStore = "#F0A02E33";
        public const string HexSoftHome  = "#2ECC7133";
        public const string HexSoftRh    = "#A78BFA33";
        public const string HexSoftMenu  = "#2DD4E033";

        // ---------- VEU SOBRE O MAPA ISOMETRICO ----------
        /// <summary>
        /// Veu ESCURO por cima de mapa-menu.png. Ele nao existe para esconder o
        /// mapa e sim para rebaixar a saturacao da arte, para os nodes lerem por
        /// cima. Se o mapa ainda estiver competindo com os cards, suba o alpha
        /// em passos de 0.05. Se estiver apagado demais, desca.
        /// </summary>
        public const string HexMapScrim   = "#0B1A2E";
        public const float  MapScrimAlpha = 0.42f;

        // ---------- SOMBRA ----------
        public const string HexShadow = "#03080F";
        public const float  ShadowAlpha = 0.35f;

        // =====================================================
        //  TOKENS LEGADOS
        //  Valores que ja estavam escritos na mao dentro dos scripts das
        //  telas antigas. Foram trazidos para ca SEM alterar nenhum tom,
        //  para centralizar sem mudar a aparencia de nada que ja funciona.
        //  Quando quiser harmonizar com a paleta nova, e so mexer aqui.
        // =====================================================

        /// <summary>Barra de cabecalho das telas antigas (Financeiro, Cardapio).</summary>
        public const string HexLegacyHeader = "#13557B";

        /// <summary>Fundo das telas antigas.</summary>
        public const string HexLegacyBackground = "#0D1B2D";

        /// <summary>Card das telas antigas.</summary>
        public const string HexLegacyCard = "#0F1E30";

        /// <summary>Rodape das telas antigas.</summary>
        public const string HexLegacyFooter = "#0D1525";

        /// <summary>Texto secundario das telas antigas (botao Voltar).</summary>
        public const string HexLegacyMuted = "#7A9CC0";

        /// <summary>Botao Confirmar das telas antigas.</summary>
        public const string HexConfirm = "#2E7D5B";

        // ---- Selecao de card no RestaurantScreenView ----
        public const string HexCardNormal      = "#141F33";
        public const string HexCardSelected    = "#73FF1A"; // usado com alpha 0.18
        public const float  CardSelectedAlpha  = 0.18f;
        public const string HexBorderNormal    = "#293D5C";
        public const string HexBorderSelected  = "#B3FF00";
        public const string HexSegmentSelected = "#2E9E2E";
        public const string HexDisabledCard    = "#475261"; // alpha 0.85
        public const string HexDisabledBank    = "#595959"; // alpha 0.85
        public const float  DisabledAlpha      = 0.85f;

        // ---- Faixas de pontuacao (RestaurantScreenView e MenuPricingScreenView) ----
        public const string HexScoreGood    = "#16A34A";
        public const string HexScoreAverage = "#D99A0B";
        public const string HexScoreBad     = "#E11D48";

        /// <summary>Cor da barra de pontuacao conforme o valor normalizado 0..1.</summary>
        public static Color ScoreColor(float normalized)
        {
            if (normalized >= 0.7f) return Parse(HexScoreGood);
            if (normalized >= 0.4f) return Parse(HexScoreAverage);
            return Parse(HexScoreBad);
        }

        // ---------- RAIOS (via pixelsPerUnitMultiplier no Image sliced) ----------
        // botao.png tem raio nativo de ~36px. Multiplier maior => raio menor.
        public const float PpuNodeBig = 1.6f;  // ~22px
        public const float PpuCard    = 2.2f;  // ~16px
        public const float PpuButton  = 3.2f;  // ~11px
        public const float PpuPill    = 0.8f;  // ~45px (totalmente arredondado)

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

        public static Color Background   => Parse(HexBackground);
        public static Color Chrome       => Parse(HexChrome);
        public static Color Surface      => Parse(HexSurface);
        public static Color SurfaceAlt   => Parse(HexSurfaceAlt);
        public static Color Hairline     => Parse(HexHairline);
        public static Color BorderStrong => Parse(HexBorderStrong);

        public static Color Ink        => Parse(HexInk);
        public static Color InkBody    => Parse(HexInkBody);
        public static Color InkCaption => Parse(HexInkCaption);
        public static Color Muted      => Parse(HexMuted);

        public static Color Primary     => Parse(HexPrimary);
        public static Color PrimaryDark => Parse(HexPrimaryDark);
        public static Color PrimarySoft => Parse(HexPrimarySoft);
        public static Color OnPrimary   => Parse(HexOnPrimary);

        public static Color Money  => Parse(HexMoney);
        public static Color Warn   => Parse(HexWarn);
        public static Color Danger => Parse(HexDanger);
        public static Color Xp     => Parse(HexXp);

        public static Color MapScrim => Parse(HexMapScrim, MapScrimAlpha);
        public static Color Shadow   => Parse(HexShadow, ShadowAlpha);

        /// <summary>Verde quando positivo, vermelho quando negativo.</summary>
        public static Color ResultColor(float value)
        {
            return value >= 0f ? Money : Danger;
        }
    }
}
