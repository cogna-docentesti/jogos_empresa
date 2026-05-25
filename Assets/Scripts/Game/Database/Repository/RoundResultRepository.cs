using System.Collections.Generic;
using System.Linq;
using SQLite4Unity3d;

public class RoundResultRepository : BaseRepository<RoundResultEntity>
{
    public RoundResultRepository(SQLiteConnection connection) : base(connection) { }

    public RoundResultEntity GetById(string roundResultId)
    {
        return db.Table<RoundResultEntity>()
            .FirstOrDefault(x => x.roundResultId == roundResultId);
    }

    public List<RoundResultEntity> GetBySessionId(string sessionId)
    {
        return db.Table<RoundResultEntity>()
            .Where(x => x.sessionId == sessionId)
            .OrderBy(x => x.round)
            .ToList();
    }

    public RoundResultEntity GetBySessionAndRound(string sessionId, int round)
    {
        return db.Table<RoundResultEntity>()
            .FirstOrDefault(x =>
                x.sessionId == sessionId &&
                x.round == round);
    }

    public RoundResultEntity GetLastRoundBySessionId(string sessionId)
    {
        return db.Table<RoundResultEntity>()
            .Where(x => x.sessionId == sessionId)
            .OrderByDescending(x => x.round)
            .FirstOrDefault();
    }

    public void DeleteBySessionId(string sessionId)
    {
        var rounds = GetBySessionId(sessionId);

        foreach (var round in rounds)
            db.Delete(round);
    }
}
