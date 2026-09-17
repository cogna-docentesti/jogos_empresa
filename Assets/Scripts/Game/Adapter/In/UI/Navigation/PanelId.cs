namespace Game.Adapter.In.UI.Navigation
{
    /// <summary>
    /// Identificador estavel de cada painel-tela do jogo.
    /// Usado pelo PanelRegistry e pelo MenuNavigator para trocar de tela
    /// sem depender de nome de GameObject (que quebra ao renomear).
    /// </summary>
    public enum PanelId
    {
        None = 0,

        /// <summary>Mapa do Menu do Jogo (hub central).</summary>
        Menu = 1,

        /// <summary>Meu Estabelecimento: resumo + indicadores.</summary>
        Establishment = 2,

        /// <summary>D4 Financeiro (Banco).</summary>
        Financial = 3,

        /// <summary>D3 Cardapio e estrategia de preco.</summary>
        MenuPricing = 4,

        /// <summary>D6 Equipe (RH).</summary>
        Team = 5,

        /// <summary>D5 Loja de Equipamentos.</summary>
        EquipmentStore = 6,

        /// <summary>D1 Localizacao.</summary>
        Location = 7,

        /// <summary>D2 Tipo de restaurante.</summary>
        Restaurant = 8,

        /// <summary>Revisao das escolhas.</summary>
        Review = 9
    }
}
