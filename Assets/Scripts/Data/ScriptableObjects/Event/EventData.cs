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

    [Header("Classificação")]
    public EventPolarity polarity;
    public EventTriggerType triggerType;
    public EventExclusionGroup exclusionGroup = EventExclusionGroup.NONE;

    [Header("Probabilidade")]
    public float baseWeight = 1f;

    [Header("Repetição")]
    public bool canRepeat = false;

    [Header("Compatibilidade")]
    public RestaurantType[] applicableRestaurantTypes;

    [Header("Rodadas")]
    public int minRound = 1;
    public int maxRound = 3;

    [Header("Condições")]
    public EventConditionData[] conditions;

    [Header("Opções")]
    public EventOption[] options;
}
