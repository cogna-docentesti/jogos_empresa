using System.Collections.Generic;
using System.Linq;
using SQLite4Unity3d;

public class SimulationMonthRepository : BaseRepository<SimulationMonthEntity>
{
    public SimulationMonthRepository(SQLiteConnection connection) : base(connection) { }

    public SimulationMonthEntity GetBySessionAndMonth(string sessionId, int month)
    {
        return db.Table<SimulationMonthEntity>()
            .Where(x => x.sessionId == sessionId && x.month == month)
            .FirstOrDefault();
    }

    public List<SimulationMonthEntity> GetBySessionId(string sessionId)
    {
        return db.Table<SimulationMonthEntity>()
            .Where(x => x.sessionId == sessionId)
            .OrderBy(x => x.month)
            .ToList();
    }

    public void DeleteBySessionId(string sessionId)
    {
        foreach (var row in GetBySessionId(sessionId))
            db.Delete(row);
    }
}
