using SQLite4Unity3d;

/// <summary>Reservations survive reopening/reloading; choices remain in SessionEventHistoryEntity.</summary>
public sealed class MonthlyEventPlanEntity
{
    [PrimaryKey] public string planId { get; set; }
    [Indexed] public string sessionId { get; set; }
    public int month { get; set; }
    public string entriesJson { get; set; }
}
