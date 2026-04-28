using UnityEngine;

[CreateAssetMenu(fileName = "LocationData", menuName = "Game Data/LocationData")]
public class LocationData : ScriptableObject
{
    [Header("Identificação")]
    public string id;
    public string displayName;

    [Header("Classificação")]
    public LocationZone zone;

    [Header("Custos")]
    public int rent;

    [Header("Perfil de Público")]
    public Segment primarySegment;

    [Header("Canais de Venda")]
    public string[] channels;

    [Header("Coerência")]
    [Range(1, 3)]
    public int coherenceLevel;

    [Header("Demanda")]
    [Range(0f, 2f)]
    public float demandModifier = 1f;

    [Header("Compatibilidade de Ticket")]
    public int ticketCompatibleMin;
    public int ticketCompatibleMax;

}
