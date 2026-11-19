// Assets/Scripts/Data/ScriptableObjects/Simulation/SimulationConfigAsset.cs
// Os números do simulador que NÃO estão nos ScriptableObjects do jogo (mix de público, perfil da semana,
// atratividade, perfil de confiabilidade, políticas, eventos...). O que já existe nos assets
// (aluguel, demanda base, salão, fator de preço, preços dos pratos, salários, custos) é lido de lá.
// Para criar ou atualizar com os valores do GDD: menu Game > Simulação > Criar ou atualizar SimulationConfig.
// O asset fica em Assets/Resources/Simulation/SimulationConfig.asset.
using System;
using System.Collections.Generic;
using Game.Domain.Simulation;
using UnityEngine;

[CreateAssetMenu(fileName = "SimulationConfig", menuName = "Game Data/Simulation Config")]
public class SimulationConfigAsset : ScriptableObject
{
    [Header("Versão da calibração (vai para cada relatório como configVersion)")]
    public string configVersion = "gdd-2026-11";

    [Header("Constantes (impostos, encargos, limites, calendário de contas)")]
    public SimulationConstants constants = new SimulationConstants();

    [Header("Pressão de concorrência por nível (LocationData.competitionLevel)")]
    public float competitionPressureLow = 0.80f;
    public float competitionPressureMedium = 1.00f;
    public float competitionPressureHigh = 1.25f;

    [Header("Mercado")]
    public List<SegmentConfig> segments = new List<SegmentConfig>();
    public List<RegionConfig> regions = new List<RegionConfig>();             // um por LocationData (pelo id)
    public List<RestaurantConfig> restaurants = new List<RestaurantConfig>(); // um por RestaurantType
    public List<ProductConfig> products = new List<ProductConfig>();          // opcional: só para sobrescrever o custo

    [Header("Operação")]
    public List<RoleConfig> roles = new List<RoleConfig>();
    public List<EquipmentProfileConfig> equipmentProfiles = new List<EquipmentProfileConfig>();
    public List<LevelConfig> levels = new List<LevelConfig>();
    public List<CategoryConfig> categories = new List<CategoryConfig>();

    [Header("Gestão mensal e eventos")]
    public List<PolicyOptionConfig> policies = new List<PolicyOptionConfig>();
    public List<EventConfig> events = new List<EventConfig>();
}

[Serializable] public class SegmentConfig { public Segment segment; public float referenceSpend; public float sensitivity; public float cityShare; }

[Serializable] public class RegionConfig
{
    public string locationId;                          // igual ao LocationData.id ("bank", "university"...)
    [Range(0, 1)] public float mixLow, mixMedium, mixHigh;
    public float[] weekdayProfile = { 1, 1, 1, 1, 1, 1, 1 };   // segunda..domingo
    public float regionalIndex = 1f;
}

[Serializable] public class RestaurantConfig
{
    public RestaurantType type;
    public float initialCapital;
    public RoleType specialistRole;
    public float attendantCapacityPerDay, baseServiceMinutes, perishabilityPerDay;
    public float attractivenessLow, attractivenessMedium, attractivenessHigh;
    public float cmvMin, cmvMax;
    public bool isPremium;
}

[Serializable] public class ProductConfig
{
    public string productId;                           // igual ao ProductData.id
    [Tooltip("0 = usa price x inputCostRatio do ProductData")]
    public float unitCostOverride;
}

[Serializable] public class RoleConfig { public RoleType role; public float kitchenCapacityPerDay; }

[Serializable] public class EquipmentProfileConfig
{
    public string equipmentId;                         // igual ao EquipmentData.id
    public EquipmentReliabilityCategory category = EquipmentReliabilityCategory.Preparation;
    public EquipmentLevel level = EquipmentLevel.Standard;
    public float kitchenCapacityBonus, diningCapacityBonus, kwhPerMonth;
}

[Serializable] public class LevelConfig
{
    public EquipmentLevel level;
    public float hazardRatio = 1, energyFactor = 1, lifeMonths = 60, repairFraction = 0.2f, qualityDelta, stockLossFactor = 1;
}

[Serializable] public class CategoryConfig
{
    public EquipmentReliabilityCategory category;
    public float failuresPerMonth;
    public float kitchenFactorWhenDown = 1, serviceFactorWhenDown = 1, diningFactorWhenDown = 1, stockLossOnFailure;
    public string failureEventId;
}

[Serializable] public class LeverConfig { public string key; public float value; }
[Serializable] public class EffectConfig { public string key; public float value; public EffectDuration duration; public int days; }

[Serializable] public class PolicyOptionConfig
{
    public string policyId, optionId, label;
    public float fixedMonthlyCost, costPerOrder, assetValueRate;
    public List<LeverConfig> levers = new List<LeverConfig>();
}

[Serializable] public class ReactionConfig
{
    public string id, label;
    public ReactionCostKind costKind;
    [Tooltip("Valor negativo com costKind = Fixed significa ganho de caixa")]
    public float costValue;
    public float costExtra;
    public bool repairsUnit, keepsStaff, usesOverdraft, renegotiates;
    public List<EffectConfig> effects = new List<EffectConfig>();
}

[Serializable] public class CovariateConfig { public string variable; public float reference; public float beta; }
[Serializable] public class ConditionConfig { public string variable; public string op = ">="; public float value; }

[Serializable] public class EventConfig
{
    public string id, title, category, group;
    public EventScope scope;
    public float baseRatePerMonth;
    public List<CovariateConfig> covariates = new List<CovariateConfig>();
    public List<ConditionConfig> conditions = new List<ConditionConfig>();
    public RestaurantType[] onlyRestaurants;           // vazio = todos
    public string[] onlyRegions;                       // vazio = todas
    public int minIntervalDays;
    public int maxPerMatch;                            // 0 = sem limite
    public bool repeats = true;
    public float severity;
    public bool isNegative, requiresDecision, deterministic;
    public int activeDays = 1;
    public List<EffectConfig> automaticEffects = new List<EffectConfig>();
    public List<ReactionConfig> reactions = new List<ReactionConfig>();
}
