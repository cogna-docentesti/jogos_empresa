using SQLite4Unity3d;

/// <summary>
/// E-06: cada localizacao do mapa da D1, guardada no banco.
///
/// Espelha os campos numericos e de texto do LocationData (ScriptableObject).
/// O que nao cabe numa coluna fica no asset: o pinIcon e um Sprite, objeto
/// da Unity, e continua vindo de Resources/Locations/LOC_*.
///
/// Os enums (zone, primarySegment, competitionLevel) sao gravados como numero,
/// igual as outras tabelas do jogo.
/// </summary>
public class LocationEntity
{
    [PrimaryKey]
    public string id { get; set; }

    public string displayName { get; set; }

    public int zone { get; set; }                     // LocationZone

    public int rent { get; set; }

    public int primarySegment { get; set; }           // Segment

    public int competitionLevel { get; set; }         // CompetitionLevel

    /// <summary>Canais separados por " · ". "-" quando nao ha nenhum.</summary>
    public string channels { get; set; }

    public int coherenceLevel { get; set; }

    public float referencePriceFactor { get; set; }

    public int initialPhysicalCapacity { get; set; }

    public int baseDailyDemand { get; set; }

    public string colorHex { get; set; }

    public string description { get; set; }

    /// <summary>Quando a linha foi copiada dos assets (Unix, segundos).</summary>
    public long seededAt { get; set; }
}
