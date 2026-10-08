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
    public GameSessionStatus status { get; set; }

    public int currentRound { get; set; }

    [NotNull]
    public string cityId { get; set; }

    // ── Identificacao do aluno (cena 0_Identification, tela do Renan R-02) ──
    // Sem [NotNull] de proposito: bancos que ja existem tem linhas sem esses
    // valores. Uma coluna nova NOT NULL sem default faz o SQLite recusar o
    // ALTER TABLE e o jogo quebraria na abertura para quem ja tinha partida.
    // Preenchidas no confirm da D3 (GameSessionService.CommitInitialDecisions).

    public string studentName { get; set; }

    public string studentRA { get; set; }

    /// <summary>Nome do restaurante digitado na identificacao (PlayerSession.RestaurantName).</summary>
    public string companyName { get; set; }

    // ── Decisoes obrigatorias ──
    // D1 = locationZone, D2 = restaurantType + targetSegment,
    // D3 = menuPricingJson (pratos e preco escolhido de cada um, formato MenuPricingData).

    public RestaurantType restaurantType { get; set; }

    public LocationZone locationZone { get; set; }

    public Segment targetSegment { get; set; }

    public float selectedPrice { get; set; }

    public string menuPricingJson { get; set; }

    [NotNull]
    public string coherenceRating { get; set; }

    public int alignmentScore { get; set; }

    public string alignmentClassification { get; set; }

    public float alignmentFactor { get; set; }

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

    /// <summary>Creates a detached copy for transactional settlement.</summary>
    public GameSessionEntity Copy() => (GameSessionEntity)MemberwiseClone();
}
