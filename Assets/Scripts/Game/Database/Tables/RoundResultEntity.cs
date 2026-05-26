using SQLite4Unity3d;

public class RoundResultEntity
{
    [PrimaryKey]
    public string roundResultId { get; set; }

    [Indexed]    
    public string sessionId { get; set; }

    public int round { get; set; }

    public float grossRevenue { get; set; }
    public float supplyCost { get; set; }
    public float rent { get; set; }
    public float salaries { get; set; }
    public float utilities { get; set; }
    public float loanPayment { get; set; }
    public float thirteenthSalary { get; set; }
    public float eventCashImpact { get; set; }

    public float netResult { get; set; }

    public float openingCash { get; set; }
    public float closingCash { get; set; }

    public string eventId { get; set; }
    public int eventChoice { get; set; }

    public int reputationDelta { get; set; }

    public float coherenceFactor { get; set; }

    public long createdAt { get; set; }
}