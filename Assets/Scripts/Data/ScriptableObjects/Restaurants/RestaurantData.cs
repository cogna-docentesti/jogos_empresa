using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Restaurant")]
public class RestaurantData : ScriptableObject
{
    [Header("Identificacao")]
    public string id;
    public string displayName;

    [Header("Classificacao")]
    public RestaurantType type;
    public Segment[] allowedSegments;
    public Segment[] blockedSegments;

    [Header("Apresentacao")]
    [TextArea]
    public string description;
    [TextArea]
    public string selectionHint;

    [Header("Cardapio")]
    public ProductData[] products;

    [Header("Financeiro")]
    public int ticketMin;
    public int ticketMax;
    public int estimatedMonthlyCustomers;

    [Header("Requisitos")]
    public RoleRequirement[] requiredRoles;
    public string[] requiredEquipmentIds;

    [Header("Coerencia")]
    [Range(1, 3)]
    public int coherenceLevel;

    [Header("Custos Fixos")]
    public int baseMonthlyCost;

    public bool AllowsSegment(Segment segment)
    {
        if (allowedSegments == null)
            return false;

        foreach (var allowedSegment in allowedSegments)
        {
            if (allowedSegment == segment)
                return true;
        }

        return false;
    }
}
