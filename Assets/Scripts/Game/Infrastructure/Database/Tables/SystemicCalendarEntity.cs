using SQLite4Unity3d;

/// <summary>Frozen month inputs and a replayable command journal for the monthly calendar.</summary>
public sealed class SystemicCalendarEntity
{
    [PrimaryKey] public string id { get; set; }
    [Indexed] public string sessionId { get; set; }
    public int month { get; set; }
    public int revision { get; set; }
    public string journalJson { get; set; }
}
