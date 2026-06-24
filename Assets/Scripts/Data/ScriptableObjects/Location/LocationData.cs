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

    [Header("Canais de Venda")]
    public string[] channels;

    [Header("Coerencia")]
    [Range(1, 3)]
    public int coherenceLevel;

    [Header("Demanda")]
    [Range(0f, 2f)]
    public float demandModifier = 1f;

    [Header("Compatibilidade de Ticket")]
    public int ticketCompatibleMin;
    public int ticketCompatibleMax;

    [Header("Visual (UI)")]
    public string colorHex = "#FFFFFF";   
    public Sprite pinIcon;  
                 
    [TextArea(2, 4)]
    public string description;           

}
