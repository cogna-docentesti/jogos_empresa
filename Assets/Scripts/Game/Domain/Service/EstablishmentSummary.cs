namespace Game.Domain.Service
{
    /// <summary>
    /// Retrato somente-leitura do estabelecimento no instante da consulta.
    /// E o que a tela "Meu Estabelecimento" desenha. Nao guarda estado nem
    /// escreve nada: quem produz e o EstablishmentSummaryService.
    /// </summary>
    public sealed class EstablishmentSummary
    {
        public bool HasSession;

        // ---------- RESUMO DA EMPRESA ----------
        public string LocationLabel      = "Pendente";
        public string RestaurantLabel    = "Pendente";
        public string SegmentLabel       = "Pendente";
        public string MenuLabel          = "Pendente";
        public string PriceLabel         = "Pendente";
        public string EquipmentLabel     = "Nenhum";
        public string TeamLabel          = "Ninguem";

        // ---------- INDICADORES ----------
        public int   Score;             // alignmentScore (0-80)
        public int   ScoreMax = 80;     // teto do AlignmentEngine (4 criterios x 20)
        public float Cash;              // caixa disponivel
        public float EstimatedRevenue;  // receita bruta estimada no mes
        public float MonthlyResult;     // receita mensal (resultado liquido)

        // ---------- APOIO ----------
        public string CoherenceLabel = "A calcular";
        public int    Round = 1;
        public float  LoanBalance;
        public int    Reputation;

        // ---------- DETALHAMENTO DO RESULTADO ----------
        public float SupplyCost;      // insumos
        public float SalariesCost;    // folha
        public float FixedCost;       // custo fixo mensal do restaurante
        public float LoanInstallment; // parcela do emprestimo

        /// <summary>
        /// Falso quando os ScriptableObjects de RoleData nao estao em Resources/Roles.
        /// Nesse caso a folha salarial entra como zero e a tela avisa o jogador,
        /// em vez de mostrar um numero inventado.
        /// </summary>
        public bool HasTeamCatalog;

        /// <summary>Idem para EquipmentData em Resources/Equipment.</summary>
        public bool HasEquipmentCatalog;

        public int EquipmentCount;
        public int TeamCount;

        /// <summary>Progresso do score em 0..1, para barras.</summary>
        public float ScoreProgress => ScoreMax <= 0 ? 0f : (float)Score / ScoreMax;
    }
}
