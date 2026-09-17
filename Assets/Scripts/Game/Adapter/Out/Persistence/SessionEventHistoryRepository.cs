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
}
