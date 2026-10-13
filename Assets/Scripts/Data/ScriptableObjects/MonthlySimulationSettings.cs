using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Monthly Simulation Settings")]
public sealed class MonthlySimulationSettings : ScriptableObject
{
    [Min(1)] public int operatingDays = 30;
    [Min(0f)] public float priceElasticity = 1f;
    [Min(0f)] public float minimumPriceDemandFactor = 0.5f;
    [Min(0f)] public float maximumPriceDemandFactor = 1.5f;
    [Min(0f)] public float minimumReputationDemandFactor = 0.5f;
    [Min(0f)] public float maximumReputationDemandFactor = 1.5f;
    [Range(0f, 1f)] public float satisfactoryServiceRatio = 0.9f;
    public int satisfactoryServiceReputationGain = 2;
    public int poorServiceReputationPenalty = -5;
    [Min(0f)] public float qualityReputationScale = 5f;
}
