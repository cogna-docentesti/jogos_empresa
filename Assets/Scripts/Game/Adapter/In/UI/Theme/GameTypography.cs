namespace Game.Adapter.In.UI.Theme
{
    /// <summary>
    /// Escala tipografica do jogo, extraida das telas que ja existem
    /// (Panel_Financial, Panel_MenuPricing, Panel_Restaurant e o Panel_Menu
    /// depois do ajuste manual). Nenhum numero aqui foi inventado.
    ///
    /// Qualquer texto criado por codigo deve usar uma destas constantes.
    /// Se um tamanho novo for realmente necessario, ele entra AQUI primeiro.
    ///
    /// Duas escalas convivem de proposito:
    ///  - TELA: telas cheias, onde o texto compete com muito espaco.
    ///  - MAPA: marcadores sobre o mapa, que precisam caber em cards pequenos.
    /// </summary>
    public static class GameTypography
    {
        // =====================================================
        //  ESCALA DE TELA
        //  Referencia: Panel_Financial e Panel_MenuPricing
        // =====================================================

        /// <summary>Titulo da tela. "Financeiro", "Cardapio".</summary>
        public const float ScreenTitle = 70f;

        /// <summary>Linha de apoio do cabecalho. Bold na cena original.</summary>
        public const float ScreenHint = 32f;

        /// <summary>Titulo de secao ou de card. "CAPITAL INICIAL DISPONIVEL:".</summary>
        public const float SectionTitle = 42f;

        /// <summary>Numero de destaque. "R$ 150.000".</summary>
        public const float DisplayValue = 50f;

        /// <summary>Corpo de texto e rotulos comuns. Tambem o texto dos botoes.</summary>
        public const float Body = 30f;

        /// <summary>Texto secundario e botao Voltar.</summary>
        public const float Caption = 28f;

        // =====================================================
        //  ESCALA DO MAPA
        //  Referencia: Panel_Menu depois do ajuste manual
        // =====================================================

        /// <summary>Titulo da barra do menu.</summary>
        public const float MapBarTitle = 32f;

        /// <summary>Subtitulo da barra do menu.</summary>
        public const float MapBarSubtitle = 28f;

        /// <summary>Rotulo dentro de uma pill da barra.</summary>
        public const float PillCaption = 23f;

        /// <summary>Valor dentro de uma pill da barra.</summary>
        public const float PillValue = 27f;

        /// <summary>Titulo de um node do mapa.</summary>
        public const float NodeTitle = 28f;

        /// <summary>Subtitulo de um node do mapa.</summary>
        public const float NodeSubtitle = 21f;

        // =====================================================
        //  SOMBRA DE TEXTO
        //  Padrao das telas antigas: uma copia preta atras, deslocada.
        // =====================================================

        public const float ShadowOffsetX = 3f;
        public const float ShadowOffsetY = -3f;
        public const float ShadowAlpha   = 0.39f;
    }
}
