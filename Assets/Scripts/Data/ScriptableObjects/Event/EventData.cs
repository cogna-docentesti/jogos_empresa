using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Event")]
public class EventData : ScriptableObject
{
    [Header("Identificação")]
    public string id;
    public string title;

    [TextArea]
    public string description;

    [Header("Educacional")]
    public string educationalConcept;

    [Header("Probabilidade")]
    public int baseWeight;

    [Header("Compatibilidade")]
    public RestaurantType[] applicableRestaurantTypes;

    [Header("Rodadas")]
    public int minRound = 1;
    public int maxRound = 12;

    [Header("Opções")]
    public EventOption[] options;
}