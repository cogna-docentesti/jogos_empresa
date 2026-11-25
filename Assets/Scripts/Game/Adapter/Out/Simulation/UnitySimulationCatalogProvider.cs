// Assets/Scripts/Game/Adapter/Out/Simulation/UnitySimulationCatalogProvider.cs
// Monta o SimulationCatalog (domínio) a partir dos ScriptableObjects do jogo e do SimulationConfig.asset.
// Mesmo papel do StaticLocationRepository na D1: o domínio nunca vê um ScriptableObject.
//
// De onde vem cada número:
//   LocationData    -> aluguel, demanda base, capacidade do salão, fator de preço, nível de concorrência
//   ProductData     -> preço de referência, faixa (min/max) e custo (price x inputCostRatio)
//   RoleData        -> salário
//   EquipmentData   -> preço
//   CreditLineData  -> limite e juros
//   EventData       -> eventos que não estão no SimulationConfig (conversor legado do GDD)
//   SimulationConfigAsset -> todo o resto
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Simulation;
using UnityEngine;

namespace Game.Adapter.Out.Simulation
{
    public sealed class UnitySimulationCatalogProvider
    {
        public const string ConfigResourcePath = "Simulation/SimulationConfig";
        private const string LocationsPath = "Locations", RestaurantsPath = "Restaurants", RolesPath = "Roles",
                             EquipmentPath = "Equipments", CreditPath = "CreditLines", EventsPath = "Events";
        private const string NoCreditId = "sem_credito";

        private readonly SimulationConfigAsset _config;

        /// teamJson guarda o id do RoleData ("attendant", "grill_cook"...)
        public Dictionary<string, RoleType> RoleTypeById { get; } = new Dictionary<string, RoleType>();
        /// GameSessionEntity guarda a zona; o motor usa o id da região
        public Dictionary<LocationZone, string> RegionIdByZone { get; } = new Dictionary<LocationZone, string>();

        public UnitySimulationCatalogProvider(SimulationConfigAsset config)
        {
            _config = config;
        }

        public static UnitySimulationCatalogProvider FromResources()
            => new UnitySimulationCatalogProvider(Resources.Load<SimulationConfigAsset>(ConfigResourcePath));

        public SimulationCatalog Build()
        {
            var cfg = _config;
            if (cfg == null)
            {
                Debug.LogError("[Simulação] SimulationConfig.asset não encontrado em Resources/" + ConfigResourcePath
                             + ". Use o menu Game > Simulação > Criar ou atualizar SimulationConfig.");
                return new SimulationCatalog();
            }
            var cat = new SimulationCatalog { ConfigVersion = cfg.configVersion, Constants = cfg.constants ?? new SimulationConstants() };

            foreach (var s in cfg.segments)
                cat.Segments[s.segment] = new SegmentProfile { Segment = s.segment, ReferenceSpend = s.referenceSpend, Sensitivity = s.sensitivity, CityShare = s.cityShare };

            // ---- D1: LocationData + RegionConfig
            foreach (var loc in Resources.LoadAll<LocationData>(LocationsPath))
            {
                RegionIdByZone[loc.zone] = loc.id;
                var rc = cfg.regions.Find(r => r.locationId == loc.id);
                if (rc == null) { Debug.LogWarning($"[Simulação] Região sem RegionConfig: {loc.id}"); continue; }
                cat.Regions[loc.id] = new RegionProfile
                {
                    Id = loc.id, Name = loc.displayName, Rent = loc.rent,
                    BaseDemandPerDay = loc.baseDailyDemand, PhysicalCapacityPerDay = loc.initialPhysicalCapacity,
                    PriceFactor = loc.referencePriceFactor,
                    CompetitionPressure = loc.competitionLevel == CompetitionLevel.LOW ? cfg.competitionPressureLow
                                        : loc.competitionLevel == CompetitionLevel.HIGH ? cfg.competitionPressureHigh
                                        : cfg.competitionPressureMedium,
                    Mix = { [Segment.LOW] = rc.mixLow, [Segment.MEDIUM] = rc.mixMedium, [Segment.HIGH] = rc.mixHigh },
                    WeekdayProfile = (rc.weekdayProfile != null && rc.weekdayProfile.Length == 7 ? rc.weekdayProfile : new float[] { 1, 1, 1, 1, 1, 1, 1 })
                                     .Select(v => (double)v).ToArray(),
                    RegionalIndex = rc.regionalIndex
                };
            }

            // ---- D2 e D3: RestaurantData + ProductData + RestaurantConfig
            foreach (var rd in Resources.LoadAll<RestaurantData>(RestaurantsPath))
            {
                var rc = cfg.restaurants.Find(r => r.type == rd.type);
                if (rc == null) { Debug.LogWarning($"[Simulação] Restaurante sem RestaurantConfig: {rd.type}"); continue; }
                var profile = new RestaurantProfile
                {
                    Type = rd.type, Name = rd.displayName, AllowedSegments = rd.allowedSegments ?? new Segment[0],
                    InitialCapital = rc.initialCapital, SpecialistRole = rc.specialistRole,
                    AttendantCapacityPerDay = rc.attendantCapacityPerDay, BaseServiceMinutes = rc.baseServiceMinutes,
                    PerishabilityPerDay = rc.perishabilityPerDay, CmvMin = rc.cmvMin, CmvMax = rc.cmvMax, IsPremium = rc.isPremium,
                    Attractiveness = { [Segment.LOW] = rc.attractivenessLow, [Segment.MEDIUM] = rc.attractivenessMedium, [Segment.HIGH] = rc.attractivenessHigh }
                };
                foreach (var p in rd.products.Where(p => p != null))
                {
                    var pc = cfg.products.Find(x => x.productId == p.id);
                    double unitCost = pc != null && pc.unitCostOverride > 0 ? pc.unitCostOverride : p.price * p.inputCostRatio;
                    profile.Products.Add(new ProductProfile
                    {
                        Id = p.id, Name = p.displayName, ReferencePrice = p.price,
                        MinPrice = p.minPrice > 0 ? p.minPrice : p.price, MaxPrice = p.maxPrice > 0 ? p.maxPrice : p.price,
                        UnitCost = System.Math.Round(unitCost, 2)
                    });
                }
                cat.Restaurants[rd.type] = profile;
            }

            // ---- D6: RoleData + RoleConfig
            foreach (var role in Resources.LoadAll<RoleData>(RolesPath))
            {
                RoleTypeById[role.id] = role.roleType;
                var rc = cfg.roles.Find(r => r.role == role.roleType);
                cat.Roles[role.roleType] = new RoleProfile { Role = role.roleType, Salary = role.salary, KitchenCapacityPerDay = rc?.kitchenCapacityPerDay ?? 0 };
            }

            // ---- D5: EquipmentData + perfil. Sem perfil = Preparo / intermediário (regra do GDD)
            foreach (var e in Resources.LoadAll<EquipmentData>(EquipmentPath))
            {
                var pc = cfg.equipmentProfiles.Find(x => x.equipmentId == e.id);
                cat.Equipment[e.id] = new EquipmentProfile
                {
                    Id = e.id, Name = e.displayName, Price = e.cost,
                    Category = pc?.category ?? EquipmentReliabilityCategory.Preparation,
                    Level = pc?.level ?? EquipmentLevel.Standard,
                    KitchenCapacityBonus = pc != null ? pc.kitchenCapacityBonus : e.capacityBonus,
                    DiningCapacityBonus = pc?.diningCapacityBonus ?? 0,
                    KwhPerMonth = pc?.kwhPerMonth ?? 0
                };
            }
            foreach (var l in cfg.levels)
                cat.Levels[l.level] = new LevelProfile { Level = l.level, HazardRatio = l.hazardRatio, EnergyFactor = l.energyFactor, LifeMonths = l.lifeMonths, RepairFraction = l.repairFraction, QualityDelta = l.qualityDelta, StockLossFactor = l.stockLossFactor };
            foreach (var c in cfg.categories)
                cat.Categories[c.category] = new CategoryProfile
                {
                    Category = c.category, FailuresPerMonth = c.failuresPerMonth, KitchenFactorWhenDown = c.kitchenFactorWhenDown,
                    ServiceFactorWhenDown = c.serviceFactorWhenDown, DiningFactorWhenDown = c.diningFactorWhenDown,
                    StockLossOnFailure = c.stockLossOnFailure, FailureEventId = string.IsNullOrEmpty(c.failureEventId) ? null : c.failureEventId
                };

            // ---- D4: CreditLineData ("sem_credito" e linhas com limite zero ficam de fora)
            foreach (var cl in Resources.LoadAll<CreditLineData>(CreditPath))
                if (cl.id != NoCreditId && cl.maxAmount > 0)
                    cat.CreditLines[cl.id] = new CreditLineProfile { Id = cl.id, Name = cl.displayName, Limit = cl.maxAmount, MonthlyRate = cl.monthlyInterestRate };

            // ---- Políticas
            foreach (var o in cfg.policies)
            {
                if (!cat.Policies.TryGetValue(o.policyId, out var list)) cat.Policies[o.policyId] = list = new List<PolicyOption>();
                list.Add(new PolicyOption
                {
                    PolicyId = o.policyId, OptionId = o.optionId, Label = o.label, FixedMonthlyCost = o.fixedMonthlyCost,
                    CostPerOrder = o.costPerOrder, AssetValueRate = o.assetValueRate,
                    Levers = o.levers.Select(l => new Lever(l.key, l.value)).ToList()
                });
            }

            // ---- Eventos: os do SimulationConfig; depois os EventData sem definição (conversor legado)
            var legacyAssets = Resources.LoadAll<EventData>(EventsPath);
            foreach (var e in cfg.events)
            {
                var def = ToDomain(e);
                var asset = legacyAssets.FirstOrDefault(a => a.id == e.id);
                if (asset != null && !string.IsNullOrEmpty(asset.title)) def.Title = asset.title;   // texto do asset (admin)
                cat.Events.Add(def);
            }
            foreach (var legacy in legacyAssets.Where(ed => cat.Event(ed.id) == null))
                cat.Events.Add(FromLegacy(legacy));

            return cat;
        }

        private static EventDefinition ToDomain(EventConfig e) => new EventDefinition
        {
            Id = e.id, Title = e.title, Category = e.category, Group = string.IsNullOrEmpty(e.group) ? null : e.group,
            Scope = e.scope, BaseRatePerMonth = e.baseRatePerMonth,
            Covariates = e.covariates.Select(c => new Covariate(c.variable, c.reference, c.beta)).ToList(),
            Conditions = e.conditions.Select(c => new Condition(c.variable, c.op, c.value)).ToList(),
            OnlyRestaurants = e.onlyRestaurants != null && e.onlyRestaurants.Length > 0 ? e.onlyRestaurants : null,
            OnlyRegions = e.onlyRegions != null && e.onlyRegions.Length > 0 ? e.onlyRegions : null,
            MinIntervalDays = e.minIntervalDays, MaxPerMatch = e.maxPerMatch > 0 ? e.maxPerMatch : int.MaxValue, Repeats = e.repeats,
            Severity = e.severity, IsNegative = e.isNegative, RequiresDecision = e.requiresDecision,
            Deterministic = e.deterministic, ActiveDays = e.activeDays,
            AutomaticEffects = e.automaticEffects.Select(Effect).ToList(),
            Reactions = e.reactions.Select(r => new ReactionDefinition
            {
                Id = r.id, Label = r.label, CostKind = r.costKind, CostValue = r.costValue, CostExtra = r.costExtra,
                RepairsUnit = r.repairsUnit, KeepsStaff = r.keepsStaff, UsesOverdraft = r.usesOverdraft, Renegotiates = r.renegotiates,
                Effects = r.effects.Select(Effect).ToList()
            }).ToList()
        };

        private static EffectDefinition Effect(EffectConfig e) => new EffectDefinition(e.key, e.value, e.duration, e.days);

        /// Conversor de eventos legados (GDD): taxa = peso x 0,3 por mês; cada opção do EventData vira uma reação.
        public static EventDefinition FromLegacy(EventData ed)
        {
            bool negative = ed.polarity == EventPolarity.NEGATIVE;
            var def = new EventDefinition
            {
                Id = ed.id, Title = ed.title, Category = "Legado", Scope = EventScope.Global,
                Group = ed.exclusionGroup == EventExclusionGroup.NONE ? null : ed.exclusionGroup.ToString(),
                BaseRatePerMonth = ed.baseWeight * 0.3, IsNegative = negative, Severity = negative ? 1 : 0,
                RequiresDecision = true, Repeats = ed.canRepeat, MaxPerMatch = ed.canRepeat ? int.MaxValue : 1,
                OnlyRestaurants = ed.applicableRestaurantTypes != null && ed.applicableRestaurantTypes.Length > 0 ? ed.applicableRestaurantTypes : null
            };
            foreach (var o in ed.options ?? new EventOption[0])
            {
                var r = new ReactionDefinition { Id = o.id, Label = string.IsNullOrEmpty(o.title) ? o.description : o.title };
                var fx = o.effects;
                if (fx != null)
                {
                    if (System.Math.Abs(fx.cashDelta) > 0.005f) { r.CostKind = ReactionCostKind.Fixed; r.CostValue = -fx.cashDelta; } // ganho = custo negativo
                    var duration = fx.duration == EventEffectDuration.CURRENT_MONTH ? EffectDuration.EndOfMonth : EffectDuration.Days;
                    void Add(string key, float value) { if (System.Math.Abs(value - 1f) > 0.0001f) r.Effects.Add(new EffectDefinition(key, value, duration, 1)); }
                    Add(LeverKeys.DemandFactor, fx.demandMultiplier);
                    Add(LeverKeys.ServiceCapFactor, fx.capacityMultiplier);
                    Add(LeverKeys.KitchenCapFactor, fx.capacityMultiplier);
                    Add(LeverKeys.SuppliesCostFactor, fx.ingredientCostMultiplier);
                }
                def.Reactions.Add(r);
            }
            if (!def.Reactions.Any(r => r.CostKind == ReactionCostKind.None || r.CostValue <= 0))
                def.Reactions.Add(new ReactionDefinition { Id = "none", Label = "Não fazer nada" });   // regra: sempre há uma reação sem custo
            return def;
        }
    }
}
