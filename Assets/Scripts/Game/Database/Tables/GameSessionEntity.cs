using UnityEngine;
using SQLite4Unity3d;

public class GameSessionEntity
{
    [PrimaryKey]
    public string sessionId { get; set; }

    [NotNull]
    public string userId { get; set; }

    [NotNull]
    public string professorId { get; set; }

    [NotNull]
    public string status { get; set; }

    public int currentRound { get; set; }

    [NotNull]
    public string cityId { get; set; }

    public RestaurantType restaurantType { get; set; }

    public LocationZone locationZone { get; set; }

    public Segment targetSegment { get; set; }

    [NotNull]
    public string coherenceRating { get; set; }

    public float initialCapital { get; set; }

    public float currentCash { get; set; }

    public float loanBalance { get; set; }

    public string creditLineId { get; set; }

    public int reputationScore { get; set; }

    [NotNull]
    public string teamJson { get; set; }

    [NotNull]
    public string equipmentJson { get; set; }

    public int consecutiveNegativeRounds { get; set; }

    public long startedAt { get; set; }

    public long? completedAt { get; set; }

    public long syncedAt { get; set; }
}
