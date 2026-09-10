using SQLite4Unity3d;

public class SessionEventHistoryEntity
{
    [PrimaryKey]
    public string historyId { get; set; }

    [Indexed]
    public string sessionId { get; set; }

    public string eventId { get; set; }

    public int round { get; set; }

    public int day { get; set; }

    public string eventName { get; set; }

    public string polarity { get; set; }

    public string selectedChoiceId { get; set; }

    public string selectedChoiceTitle { get; set; }

    public string selectedChoiceText { get; set; }

    public long occurredAt { get; set; }
}
