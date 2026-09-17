using UnityEngine;

[CreateAssetMenu(fileName = "LocationData", menuName = "Game Data/LocationData")]
public class LocationData : ScriptableObject
{
    [Header("Identificacao")]
    public string id;
    public string displayName;

    [Header("Classificacao")]
    public LocationZone zone;

    [Header("Custos")]
    public int rent;

    [Header("Perfil de Publico")]
    public Segment primarySegment;

    [Header("Concorrencia")]
    public CompetitionLevel competitionLevel;

    [Header("Canais de Venda")]
    public string[] channels;

    [Header("Coerencia")]
    [Range(1, 3)]
    public int coherenceLevel;

    [Header("Preco")]
    [Range(0f, 2f)]
    public float referencePriceFactor = 1f;

    [Header("Operacao")]
    public int initialPhysicalCapacity;
    public int baseDailyDemand;

    [Header("Visual (UI)")]
    public string colorHex = "#FFFFFF";   
    public Sprite pinIcon;  
                 
    [TextArea(2, 4)]
    public string description;           

    private void OnValidate()
    {
        coherenceLevel = primarySegment switch
        {
            Segment.LOW => 1,
            Segment.MEDIUM => 2,
            Segment.HIGH => 3,
            _ => coherenceLevel
        };
    }
}
