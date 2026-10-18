using System.Collections.Generic;
using System.Linq;
using SQLite4Unity3d;

public class SessionEventHistoryRepository : BaseRepository<SessionEventHistoryEntity>
{
    public SessionEventHistoryRepository(SQLiteConnection connection) : base(connection) { }

    public List<SessionEventHistoryEntity> GetBySession(string sessionId)
    {
        return db.Table<SessionEventHistoryEntity>()
            .Where(x => x.sessionId == sessionId)
            .OrderBy(x => x.round)
            .ThenBy(x => x.day)
            .ToList();
    }

    public List<SessionEventHistoryEntity> GetBySessionAndRound(string sessionId, int round)
    {
        return db.Table<SessionEventHistoryEntity>()
            .Where(x => x.sessionId == sessionId && x.round == round)
            .OrderBy(x => x.day)
            .ToList();
    }

    public bool HasEventOccurred(string sessionId, string eventId)
    {
        return db.Table<SessionEventHistoryEntity>()
            .FirstOrDefault(x => x.sessionId == sessionId && x.eventId == eventId) != null;
    }

    /// <summary>
    /// E-04: registra que um evento aconteceu e o que a escolha do jogador
    /// causou. E o ponto unico de gravacao do historico de eventos: o sistema
    /// de eventos chama este metodo quando o jogador escolhe uma opcao, e o
    /// resumo mensal / DRE (Renan) leem com GetBySessionAndRound.
    /// </summary>
    /// <param name="cashChange">Variacao do caixa (negativo = saiu dinheiro).</param>
    /// <param name="reputationChange">Pontos de reputacao ganhos ou perdidos.</param>
    /// <param name="clientsChange">Clientes a mais ou a menos no mes.</param>
    public SessionEventHistoryEntity Record(string sessionId, int round, int day,
        EventData eventData, EventOption option,
        float cashChange, int reputationChange, int clientsChange)
    {
        // Settled statements must keep their original event inputs.
        if (new RoundResultRepository(db).GetBySessionAndRound(sessionId, round) != null)
            throw new System.InvalidOperationException("Events cannot be recorded for a settled month.");
        var session = new GameSessionRepository(db).GetById(sessionId);
        if (session != null && (session.status != GameSessionStatus.IN_PROGRESS || session.currentRound != round))
            throw new System.InvalidOperationException("Events must belong to the active session month.");

        var item = new SessionEventHistoryEntity
        {
            historyId = System.Guid.NewGuid().ToString(),
            sessionId = sessionId,
            eventId = eventData != null ? eventData.id : null,
            eventName = eventData != null ? eventData.title : null,
            polarity = eventData != null ? eventData.polarity.ToString() : null,
            round = round,
            day = day,
            selectedChoiceId = option?.id,
            selectedChoiceTitle = option?.title,
            selectedChoiceText = option?.description,
            cashChange = cashChange,
            reputationChange = reputationChange,
            clientsChange = clientsChange,
            effectsJson = option?.effects != null ? UnityEngine.JsonUtility.ToJson(option.effects) : null,
            occurredAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        db.Insert(item);
        return item;
    }

    /// <summary>
    /// Mesmo que Record, tirando caixa e reputacao direto dos efeitos da opcao
    /// (EventEffectData.cashDelta e reputationDelta). Clientes nao estao nos
    /// efeitos (eles mexem na demanda), entao quem chama informa.
    /// </summary>
    public SessionEventHistoryEntity RecordFromOption(string sessionId, int round, int day,
        EventData eventData, EventOption option, int clientsChange = 0)
    {
        var effects = option?.effects;
        return Record(sessionId, round, day, eventData, option,
            effects != null ? effects.cashDelta : 0f,
            effects != null ? effects.reputationDelta : 0,
            clientsChange);
    }

    /// <summary>Apaga os eventos de uma sessao. Usado pelo botao Resetar.</summary>
    public void DeleteBySessionId(string sessionId)
    {
        foreach (var item in GetBySession(sessionId))
            db.Delete(item);
    }
}
