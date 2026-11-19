using SQLite4Unity3d;

/// <summary>
/// Estado do simulador sistemico que passa de um mes para o outro. Uma linha por sessao.
///
/// O GameSessionEntity continua sendo a fonte de caixa, reputacao, emprestimo e mes atual.
/// Aqui fica o que so o simulador usa: unidades de equipamento (idade, falhas, quebradas),
/// efeitos de eventos ainda ativos, contadores de recorrencia, contas a pagar, estoque,
/// moral da equipe, consciencia de marca e as politicas de gestao escolhidas no hub.
///
/// Tabela nova, criada sob demanda pelo SqliteSimulationGateway (CreateTable e idempotente).
/// Bancos antigos nao precisam de migracao: sem linha, o simulador comeca do estado inicial.
/// </summary>
public class SimulationStateEntity
{
    [PrimaryKey]
    public string sessionId { get; set; }

    /// <summary>JSON de Game.Domain.Simulation.SimulationState (JsonUtility).</summary>
    public string stateJson { get; set; }

    /// <summary>JSON de PolicyChoiceList: categoria e opcao escolhida no hub.</summary>
    public string policiesJson { get; set; }

    /// <summary>Ultimo mes fechado pelo simulador (0 = nenhum).</summary>
    public int lastClosedMonth { get; set; }

    /// <summary>SimulationConfig.versionLabel usado no ultimo fechamento.</summary>
    public string configVersion { get; set; }

    public long updatedAt { get; set; }
}
