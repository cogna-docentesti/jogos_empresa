// Assets/Scripts/Game/Infrastructure/Database/Tables/SimulationStateEntity.cs
// Uma linha por sessão: o estado no início do mês corrente e as políticas desse mês.
// Criada no DatabaseInitializer. O estado vai como JSON (JsonUtility), como teamJson e equipmentJson.
using SQLite4Unity3d;

public class SimulationStateEntity
{
    [PrimaryKey] public string sessionId { get; set; }
    public int month { get; set; }                  // mês a que o stateJson se refere (início do mês)
    public string stateJson { get; set; }           // SimulationState
    public int policiesMonth { get; set; }          // mês a que o policiesJson se refere
    public string policiesJson { get; set; }        // MonthPolicies
    public string configVersion { get; set; }
    public long updatedAt { get; set; }
}
