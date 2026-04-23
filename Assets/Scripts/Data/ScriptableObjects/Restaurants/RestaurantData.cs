using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Restaurant")]
public class RestaurantData : ScriptableObject
{
    [Header("Identificação")]
    public string id;
    public string displayName;

    [Header("Classificação")]
    public RestaurantType type;
    public Segment targetSegment;

    [Header("Cardápio")]
    public ProductData[] products;

    [Header("Financeiro")]
    public int ticketMin;
    public int ticketMax;
    public int estimatedMonthlyCustomers;

    [Header("Requisitos")]
    public RoleRequirement[] requiredRoles;
    public string[] requiredEquipmentIds;

    [Header("Coerência")]
    [Range(1, 3)]
    public int coherenceLevel;

    [Header("Custos Fixos")]
    public int baseMonthlyCost;
}