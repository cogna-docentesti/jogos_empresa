using SQLite4Unity3d;

/// <summary>
/// Resultado de um mes (rodada) da partida. Uma linha por mes por sessao.
///
/// E esta tabela que o resumo mensal, a DRE e o export do Renan leem.
/// O roteiro chamava ela de "MonthlyResultEntity"; como ela ja existia com
/// quase todos os campos, foi expandida em vez de duplicada. Equivalencias:
///
///   roteiro (MonthlyResultEntity)   aqui (RoundResultEntity)
///   month                           round           (alias Month)
///   customers                       customers       (NOVO na E-04)
///   revenue                         grossRevenue
///   variableCosts                   supplyCost      (alias VariableCosts)
///   fixedCosts                      rent+utilities  (alias FixedCosts)
///   payroll                         salaries+13o    (alias Payroll)
///   netResult                       netResult
///   cashAtEnd                       closingCash
///   reputationAtEnd                 reputationAtEnd (NOVO na E-04)
///   closedAt                        createdAt
///
/// Os campos novos nao tem [NotNull]: o SQLite consegue adicionar a coluna em
/// bancos que ja existem, e as linhas antigas ficam com 0.
/// </summary>
public class RoundResultEntity
{
    [PrimaryKey]
    public string roundResultId { get; set; }

    [Indexed]
    public string sessionId { get; set; }

    public int round { get; set; }

    public float grossRevenue { get; set; }
    public float supplyCost { get; set; }
    public float rent { get; set; }
    public float salaries { get; set; }
    public float utilities { get; set; }
    public float loanPayment { get; set; }
    public float thirteenthSalary { get; set; }
    public float eventCashImpact { get; set; }

    public float netResult { get; set; }

    public float openingCash { get; set; }
    public float closingCash { get; set; }

    public string eventId { get; set; }
    public int eventChoice { get; set; }

    public int reputationDelta { get; set; }

    public float coherenceFactor { get; set; }

    public long createdAt { get; set; }

    // ── E-04: campos que o resumo mensal precisa e ainda nao existiam ──

    /// <summary>Clientes atendidos no mes.</summary>
    public int customers { get; set; }

    /// <summary>Reputacao (0 a 100) ao fechar o mes.</summary>
    public int reputationAtEnd { get; set; }

    // Monthly simulation diagnostics. Existing databases add these nullable columns.
    public int baseDemand { get; set; }
    public int potentialDemand { get; set; }
    public int serviceCapacity { get; set; }
    public float averageTicket { get; set; }
    public float loanPrincipalPayment { get; set; }
    public int reputationAtStart { get; set; }
    public int alignmentScore { get; set; }
    public string alignmentClassification { get; set; }

    [Ignore] public float TotalCosts => supplyCost + rent + salaries + utilities + loanPayment + thirteenthSalary;

    // ── Atalhos so de leitura para a DRE. [Ignore]: nao viram coluna. ──

    [Ignore] public int   Month         => round;
    [Ignore] public float VariableCosts => supplyCost;
    [Ignore] public float FixedCosts    => rent + utilities;
    [Ignore] public float Payroll       => salaries + thirteenthSalary;
}
