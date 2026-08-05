using UnityEngine;

[CreateAssetMenu(fileName = "EquipmentData", menuName = "Game Data/EquipmentData")]
public class EquipmentData : ScriptableObject
{
    [Header("Identificação")]
    public string id;
    public string displayName;
    public Sprite icon;

    [Header("Financeiro")]
    public int cost;

    [Header("Classificação")]
    public EquipmentCategory category;

    [Header("Compatibilidade")]
    public RestaurantType[] applicableTypes;

    [Header("Benefícios")]
    [Range(0f, 1f)]
    public float qualityBonus;

    public int capacityBonus;
}
