using SQLite4Unity3d;

public class SimulationStateRepository : BaseRepository<SimulationStateEntity>
{
    public SimulationStateRepository(SQLiteConnection connection) : base(connection) { }

    public SimulationStateEntity GetBySessionId(string sessionId)
    {
        // Where vira SQL no SQLite4Unity3d; FirstOrDefault(predicado) leria a tabela inteira.
        return db.Table<SimulationStateEntity>()
            .Where(x => x.sessionId == sessionId)
            .FirstOrDefault();
    }

    /// <summary>Usado pelo Resetar: apaga o estado do simulador junto com a sessao.</summary>
    public void DeleteBySessionId(string sessionId)
    {
        var row = GetBySessionId(sessionId);
        if (row != null)
            db.Delete(row);
    }
}
