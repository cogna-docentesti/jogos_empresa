// Assets/Editor/Simulation/SimulationConfigBuilder.cs
// Menu Game > Simulação > Criar ou atualizar SimulationConfig (valores do GDD).
// Cria Assets/Resources/Simulation/SimulationConfig.asset com os números do GDD que não estão nos
// ScriptableObjects do jogo. Rodar de novo sobrescreve o asset com os valores do GDD.
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Simulation;
using UnityEditor;
using UnityEngine;

public static class SimulationConfigBuilder
{
    public const string AssetFolder = "Assets/Resources/Simulation";
    public const string AssetPath = AssetFolder + "/SimulationConfig.asset";

    [MenuItem("Game/Simulação/Criar ou atualizar SimulationConfig (valores do GDD)")]
    public static void CreateOrUpdate()
    {
        if (!AssetDatabase.IsValidFolder(AssetFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Simulation");
        var asset = AssetDatabase.LoadAssetAtPath<SimulationConfigAsset>(AssetPath);
        bool isNew = asset == null;
        if (isNew) asset = ScriptableObject.CreateInstance<SimulationConfigAsset>();
        Fill(asset);
        if (isNew) AssetDatabase.CreateAsset(asset, AssetPath);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Selection.activeObject = asset;
        Debug.Log("[Simulação] SimulationConfig gravado em " + AssetPath);
    }

    /// Perfil de confiabilidade dos equipamentos do jogo (ids de Resources/Equipments).
    /// Categoria e nível seguem o GDD; kWh/mês são valores de exemplo.
    public static readonly Dictionary<string, (EquipmentReliabilityCategory category, EquipmentLevel level, float kitchen, float dining, float kwh)> EquipmentProfiles =
        new Dictionary<string, (EquipmentReliabilityCategory, EquipmentLevel, float, float, float)>
        {
            ["bancada_inox_preparo"]             = (EquipmentReliabilityCategory.Preparation,   EquipmentLevel.Standard, 0, 0, 0),
            ["conjunto_panelas_industriais"]     = (EquipmentReliabilityCategory.Preparation,   EquipmentLevel.Standard, 0, 0, 0),
            ["fogao_industrial_6_bocas"]         = (EquipmentReliabilityCategory.Cooking,       EquipmentLevel.Standard, 0, 0, 0),
            ["freezer_horizontal"]               = (EquipmentReliabilityCategory.Refrigeration, EquipmentLevel.Standard, 0, 0, 120),
            ["geladeira_comercial_dupla"]        = (EquipmentReliabilityCategory.Refrigeration, EquipmentLevel.Standard, 0, 0, 150),
            ["mobiliario_basico"]                = (EquipmentReliabilityCategory.DiningRoom,    EquipmentLevel.Standard, 0, 0, 0),
            ["pia_industrial_inox"]              = (EquipmentReliabilityCategory.Preparation,   EquipmentLevel.Standard, 0, 0, 0),
            ["sistema_pdv_computador"]           = (EquipmentReliabilityCategory.PointOfSale,   EquipmentLevel.Standard, 0, 0, 15),
            ["chapa_profissional_podrao"]        = (EquipmentReliabilityCategory.Cooking,       EquipmentLevel.Standard, 8, 0, 60),
            ["fritadeira_eletrica_dupla_podrao"] = (EquipmentReliabilityCategory.Cooking,       EquipmentLevel.Standard, 0, 0, 50),
            ["balcao_refrigerado_sushi"]         = (EquipmentReliabilityCategory.Refrigeration, EquipmentLevel.Standard, 0, 0, 180),
            ["facas_especiais_japones"]          = (EquipmentReliabilityCategory.Preparation,   EquipmentLevel.Standard, 0, 0, 0),
            ["panela_eletrica_arroz"]            = (EquipmentReliabilityCategory.Cooking,       EquipmentLevel.Standard, 0, 0, 30),
            ["adega_climatizada"]                = (EquipmentReliabilityCategory.Refrigeration, EquipmentLevel.Standard, 0, 0, 90),
            ["forno_combinado_inteligente"]      = (EquipmentReliabilityCategory.Cooking,       EquipmentLevel.Premium,  8, 0, 200),
            ["mobiliario_luxo"]                  = (EquipmentReliabilityCategory.DiningRoom,    EquipmentLevel.Premium,  0, 5, 0),
        };

    /// Preenche o asset com os valores do GDD (SampleCatalog) e os perfis acima.
    public static void Fill(SimulationConfigAsset a)
    {
        var gdd = SampleCatalog.Build();
        a.configVersion = gdd.ConfigVersion;
        a.constants = gdd.Constants;
        a.competitionPressureLow = 0.80f;
        a.competitionPressureMedium = 1.00f;
        a.competitionPressureHigh = 1.25f;

        a.segments = gdd.Segments.Values.Select(s => new SegmentConfig
        {
            segment = s.Segment, referenceSpend = (float)s.ReferenceSpend, sensitivity = (float)s.Sensitivity, cityShare = (float)s.CityShare
        }).ToList();

        a.regions = gdd.Regions.Values.Select(r => new RegionConfig
        {
            locationId = r.Id,
            mixLow = (float)r.Mix[Segment.LOW], mixMedium = (float)r.Mix[Segment.MEDIUM], mixHigh = (float)r.Mix[Segment.HIGH],
            weekdayProfile = r.WeekdayProfile.Select(v => (float)v).ToArray(),
            regionalIndex = (float)r.RegionalIndex
        }).ToList();

        a.restaurants = gdd.Restaurants.Values.Select(r => new RestaurantConfig
        {
            type = r.Type, initialCapital = (float)r.InitialCapital, specialistRole = r.SpecialistRole,
            attendantCapacityPerDay = (float)r.AttendantCapacityPerDay, baseServiceMinutes = (float)r.BaseServiceMinutes,
            perishabilityPerDay = (float)r.PerishabilityPerDay,
            attractivenessLow = (float)r.Attractiveness[Segment.LOW], attractivenessMedium = (float)r.Attractiveness[Segment.MEDIUM],
            attractivenessHigh = (float)r.Attractiveness[Segment.HIGH],
            cmvMin = (float)r.CmvMin, cmvMax = (float)r.CmvMax, isPremium = r.IsPremium
        }).ToList();

        a.products = new List<ProductConfig>();          // custo = price x inputCostRatio do ProductData

        a.roles = gdd.Roles.Values.Select(r => new RoleConfig { role = r.Role, kitchenCapacityPerDay = (float)r.KitchenCapacityPerDay }).ToList();

        a.equipmentProfiles = EquipmentProfiles.Select(kv => new EquipmentProfileConfig
        {
            equipmentId = kv.Key, category = kv.Value.category, level = kv.Value.level,
            kitchenCapacityBonus = kv.Value.kitchen, diningCapacityBonus = kv.Value.dining, kwhPerMonth = kv.Value.kwh
        }).ToList();

        a.levels = gdd.Levels.Values.Select(l => new LevelConfig
        {
            level = l.Level, hazardRatio = (float)l.HazardRatio, energyFactor = (float)l.EnergyFactor, lifeMonths = (float)l.LifeMonths,
            repairFraction = (float)l.RepairFraction, qualityDelta = (float)l.QualityDelta, stockLossFactor = (float)l.StockLossFactor
        }).ToList();

        a.categories = gdd.Categories.Values.Select(c => new CategoryConfig
        {
            category = c.Category, failuresPerMonth = (float)c.FailuresPerMonth, kitchenFactorWhenDown = (float)c.KitchenFactorWhenDown,
            serviceFactorWhenDown = (float)c.ServiceFactorWhenDown, diningFactorWhenDown = (float)c.DiningFactorWhenDown,
            stockLossOnFailure = (float)c.StockLossOnFailure, failureEventId = c.FailureEventId
        }).ToList();

        a.policies = gdd.Policies.Values.SelectMany(list => list).Select(o => new PolicyOptionConfig
        {
            policyId = o.PolicyId, optionId = o.OptionId, label = o.Label, fixedMonthlyCost = (float)o.FixedMonthlyCost,
            costPerOrder = (float)o.CostPerOrder, assetValueRate = (float)o.AssetValueRate,
            levers = o.Levers.Select(l => new LeverConfig { key = l.Key, value = (float)l.Value }).ToList()
        }).ToList();

        a.events = gdd.Events.Select(ToConfig).ToList();
    }

    private static EventConfig ToConfig(EventDefinition e) => new EventConfig
    {
        id = e.Id, title = e.Title, category = e.Category, group = e.Group, scope = e.Scope,
        baseRatePerMonth = (float)e.BaseRatePerMonth,
        covariates = e.Covariates.Select(c => new CovariateConfig { variable = c.Variable, reference = (float)c.Reference, beta = (float)c.Beta }).ToList(),
        conditions = e.Conditions.Select(c => new ConditionConfig { variable = c.Variable, op = c.Operator, value = (float)c.Value }).ToList(),
        onlyRestaurants = e.OnlyRestaurants, onlyRegions = e.OnlyRegions,
        minIntervalDays = e.MinIntervalDays, maxPerMatch = e.MaxPerMatch == int.MaxValue ? 0 : e.MaxPerMatch, repeats = e.Repeats,
        severity = (float)e.Severity, isNegative = e.IsNegative, requiresDecision = e.RequiresDecision,
        deterministic = e.Deterministic, activeDays = e.ActiveDays,
        automaticEffects = e.AutomaticEffects.Select(Effect).ToList(),
        reactions = e.Reactions.Select(r => new ReactionConfig
        {
            id = r.Id, label = r.Label, costKind = r.CostKind, costValue = (float)r.CostValue, costExtra = (float)r.CostExtra,
            repairsUnit = r.RepairsUnit, keepsStaff = r.KeepsStaff, usesOverdraft = r.UsesOverdraft, renegotiates = r.Renegotiates,
            effects = r.Effects.Select(Effect).ToList()
        }).ToList()
    };

    private static EffectConfig Effect(EffectDefinition e)
        => new EffectConfig { key = e.Lever.Key, value = (float)e.Lever.Value, duration = e.Duration, days = e.Days };
}
