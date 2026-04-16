using System.Linq;
using SQLite4Unity3d;

public class GameSessionRepository : BaseRepository<GameSessionEntity>
{
    public GameSessionRepository(SQLiteConnection connection) : base(connection) { }

    public GameSessionEntity GetById(string sessionId)
    {
        return db.Table<GameSessionEntity>()
            .FirstOrDefault(x => x.sessionId == sessionId);
    }

    public GameSessionEntity GetActiveSessionByUserId(string userId)
    {
        return db.Table<GameSessionEntity>()
            .FirstOrDefault(x => x.userId == userId && x.status == "IN_PROGRESS");
    }
}