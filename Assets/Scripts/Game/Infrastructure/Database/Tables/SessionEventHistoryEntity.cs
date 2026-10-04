using SQLite4Unity3d;

/// <summary>
/// Cada evento que aconteceu numa partida e a opcao que o jogador escolheu.
/// Tabela criada pela Thaysla no sistema de eventos.
///
/// O roteiro da E-04 pedia uma "EventOccurrenceEntity" para o Renan. Como
/// esta tabela ja registra o evento, o mes (round) e a escolha, ela foi
/// expandida com os tres impactos que faltavam, em vez de criar uma segunda
/// tabela de eventos. Equivalencias:
///
///   roteiro (EventOccurrenceEntity)   aqui (SessionEventHistoryEntity)
///   month                             round
///   eventId                           eventId
///   chosenOptionId                    selectedChoiceId
///   cashChange                        cashChange        (NOVO na E-04)
///   reputationChange                  reputationChange  (NOVO na E-04)
///   clientsChange                     clientsChange     (NOVO na E-04)
///   occurredAt                        occurredAt
///
/// Quem registra o evento (sistema de eventos) passa a preencher tambem os
/// tres campos novos. Sem [NotNull]: linhas antigas ficam com 0.
/// </summary>
public class SessionEventHistoryEntity
{
    [PrimaryKey]
    public string historyId { get; set; }

    [Indexed]
    public string sessionId { get; set; }

    public string eventId { get; set; }

    public int round { get; set; }

    public int day { get; set; }

    public string eventName { get; set; }

    public string polarity { get; set; }

    public string selectedChoiceId { get; set; }

    public string selectedChoiceTitle { get; set; }

    public string selectedChoiceText { get; set; }

    public long occurredAt { get; set; }

    // ── E-04: impacto da escolha, para o resumo mensal e a DRE ──

    /// <summary>Quanto o caixa mudou por causa desta escolha (negativo = saiu dinheiro).</summary>
    public float cashChange { get; set; }

    /// <summary>Pontos de reputacao ganhos (positivo) ou perdidos (negativo).</summary>
    public int reputationChange { get; set; }

    /// <summary>Clientes a mais (positivo) ou a menos (negativo) no mes.</summary>
    public int clientsChange { get; set; }
}
