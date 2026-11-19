using SQLite4Unity3d;

/// <summary>
/// Relatorio completo de um mes simulado: DRE, fluxo de caixa, balanco, indicadores,
/// pontuacao, eventos, decisoes, dias do calendario e feedback. Uma linha por mes por sessao.
///
/// O RoundResultEntity continua recebendo o resumo de caixa do mes (para o resumo mensal,
/// a DRE simplificada e o export que ja existem). Esta tabela guarda o detalhe que so as
/// telas novas e o relatorio final leem.
/// </summary>
public class SimulationMonthEntity
{
    [PrimaryKey]
    public string simulationMonthId { get; set; }

    [Indexed]
    public string sessionId { get; set; }

    public int month { get; set; }

    /// <summary>JSON de Game.Domain.Simulation.MonthlyReport (JsonUtility).</summary>
    public string reportJson { get; set; }

    public int scoreTotal { get; set; }

    public string scoreClassification { get; set; }

    public float netIncome { get; set; }

    public string configVersion { get; set; }

    public long createdAt { get; set; }
}
