// Assets/Scripts/Game/Infrastructure/Database/Tables/SimulationMonthEntity.cs
// Uma linha por mês fechado: o relatório completo (DRE, caixa, balanço, pontuação, feedback, previsão).
using SQLite4Unity3d;

public class SimulationMonthEntity
{
    [PrimaryKey] public string id { get; set; }     // "<sessionId>:<mês>"
    [Indexed] public string sessionId { get; set; }
    public int month { get; set; }
    public string reportJson { get; set; }          // MonthReport
    public string configVersion { get; set; }
    public string startStateJson { get; set; }      // SimulationState do dia 1 (para reproduzir e conferir o mês); vazio em meses antigos
    public long closedAt { get; set; }
}
