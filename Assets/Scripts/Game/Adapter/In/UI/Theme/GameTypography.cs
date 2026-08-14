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

        /// <summary>Titulo da tela. "Financeiro", "Cardapio". Era 70.</summary>
        public const float ScreenTitle = 88f;

        /// <summary>Linha de apoio do cabecalho. Bold na cena original. Era 32.</summary>
        public const float ScreenHint = 40f;

        /// <summary>Titulo de secao ou de card. Era 42.</summary>
        public const float SectionTitle = 52f;

        /// <summary>Numero de destaque. "R$ 150.000". Era 50.</summary>
        public const float DisplayValue = 62f;

        /// <summary>Corpo de texto e rotulos comuns. Tambem o texto dos botoes. Era 30.</summary>
        public const float Body = 38f;

        /// <summary>Texto secundario e botao Voltar. Era 28.</summary>
        public const float Caption = 35f;

        // =====================================================
        //  ESCALA DO MAPA
        //  Referencia: Panel_Menu depois do ajuste manual
        // =====================================================

        /// <summary>Titulo da barra do menu. Era 32.</summary>
        public const float MapBarTitle = 40f;

        /// <summary>Subtitulo da barra do menu. Era 28.</summary>
        public const float MapBarSubtitle = 35f;

        /// <summary>Rotulo dentro de uma pill da barra. Era 23.</summary>
        public const float PillCaption = 29f;

        /// <summary>Valor dentro de uma pill da barra. Era 27.</summary>
        public const float PillValue = 34f;

        /// <summary>Titulo de um node do mapa. Era 28.</summary>
        public const float NodeTitle = 35f;

        /// <summary>Subtitulo de um node do mapa. Era 21.</summary>
        public const float NodeSubtitle = 26f;

        // =====================================================
        //  SOMBRA DE TEXTO
        //  Padrao das telas antigas: uma copia preta atras, deslocada.
        // =====================================================

        public const float ShadowOffsetX = 3f;
        public const float ShadowOffsetY = -3f;
        public const float ShadowAlpha   = 0.39f;
    }
}
