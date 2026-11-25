using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Simulation;
using UnityEngine;

namespace Game.Adapter.Out.Simulation
{
    /// <summary>
    /// Monta o SimulationCatalog a partir dos ScriptableObjects do projeto e do SimulationConfigAsset.
    ///
    /// Os nomes, precos, salarios, custos, demanda base e capacidade continuam vindo dos assets
    /// que a equipe ja edita. O SimulationConfigAsset acrescenta os perfis do simulador. Nada aqui
    /// grava em asset: a configuracao e copiada antes de ser usada.
    /// </summary>
    public sealed class UnitySimulationCatalogProvider
    {
        private readonly Func<SimulationConfigAsset> _loadAsset;
        private readonly Func<string, ScriptableObject[]> _loadAll;

        public List<string> Warnings { get; } = new List<string>();
        public int OutlookRuns { get; private set; } = 40;

        public UnitySimulationCatalogProvider()
            : this(SimulationConfigAsset.LoadFromResources, LoadAllFromResources) { }

        /// <summary>Construtor para testes: troca o carregamento de Resources por listas montadas em codigo.</summary>
        public UnitySimulationCatalogProvider(Func<SimulationConfigAsset> loadAsset, Func<string, ScriptableObject[]> loadAll)
        {
            _loadAsset = loadAsset ?? (() => null);
            _loadAll = loadAll ?? (_ => Array.Empty<ScriptableObject>());
        }

        private static ScriptableObject[] LoadAllFromResources(string path) => Resources.LoadAll<ScriptableObject>(path);

        private IEnumerable<T> All<T>(string path) where T : ScriptableObject
            => (_loadAll(path) ?? Array.Empty<ScriptableObject>()).OfType<T>().Where(item => item != null);

        public SimulationCatalog Load()
        {
            Warnings.Clear();
            var asset = _loadAsset();
            SimulationConfig config;
            bool includeLegacy = true, useTexts = false;
            if (asset == null)
            {
                Warnings.Add("SimulationConfig.asset nao encontrado em Resources/" + SimulationConfigAsset.ResourcesPath
                             + ". Usando os valores padrao (menu Game > Simulacao > Criar configuracao).");
                config = DefaultSimulationConfig.Create();
            }
            else
            {
                config = asset.CloneConfig();
                includeLegacy = asset.includeLegacyEvents;
                useTexts = asset.useEventAssetTexts;
                OutlookRuns = asset.outlookRuns;
            }

            var catalog = new SimulationCatalog { Config = config };
            AddLocations(catalog);
            AddRestaurants(catalog);
            AddRoles(catalog);
            AddEquipment(catalog);
            AddCredits(catalog);

            var eventAssets = All<EventData>("Events").ToList();
            if (useTexts) OverlayTexts(config, eventAssets);
            if (includeLegacy)
            {
                int added = LegacyEventConverter.AppendMissing(config, eventAssets.Select(ToLegacySpec), Warnings);
                if (added > 0) Warnings.Add(added + " evento(s) legado(s) convertido(s) para o motor diario.");
            }

            foreach (var warning in Warnings) Debug.LogWarning("[Simulacao] " + warning);
            return catalog;
        }

        // -----------------------------------------------------------------
        //  Mapeamento dos ScriptableObjects
        // -----------------------------------------------------------------

        private void AddLocations(SimulationCatalog catalog)
        {
            foreach (var data in All<LocationData>("Locations"))
            {
                if (string.IsNullOrWhiteSpace(data.id)) { Warnings.Add("LocationData sem id: " + data.name); continue; }
                catalog.Locations[data.id] = new LocationSpec
                {
                    Id = data.id,
                    ZoneId = data.zone.ToString(),
                    Name = data.displayName,
                    Rent = data.rent,
                    BaseDailyDemand = data.baseDailyDemand,
                    PhysicalCapacity = data.initialPhysicalCapacity,
                    ReferencePriceFactor = data.referencePriceFactor,
                    CompetitionIndex = MarketModel.CompetitionIndex(data.competitionLevel.ToString(), catalog.Config.market),
                    PrimarySegment = data.primarySegment.ToString(),
                    Profile = catalog.Config.Location(data.id)
                };
            }
        }

        private void AddRestaurants(SimulationCatalog catalog)
        {
            foreach (var data in All<RestaurantData>("Restaurants"))
            {
                string typeId = data.type.ToString();
                var spec = new RestaurantSpec { TypeId = typeId, Name = data.displayName, Profile = catalog.Config.Restaurant(typeId) };
                foreach (var product in data.products ?? Array.Empty<ProductData>())
                {
                    if (product == null || string.IsNullOrWhiteSpace(product.id)) continue;
                    spec.Products.Add(new ProductSpec
                    {
                        Id = product.id,
                        Name = product.displayName,
                        ReferencePrice = product.price,
                        UnitCost = product.price * (double)product.inputCostRatio,
                        MinPrice = product.minPrice,
                        MaxPrice = product.maxPrice > 0f ? product.maxPrice : product.price
                    });
                }
                foreach (var requirement in data.requiredRoles ?? Array.Empty<RoleRequirement>())
                {
                    if (requirement == null || string.IsNullOrWhiteSpace(requirement.roleId) || requirement.quantity <= 0) continue;
                    spec.RequiredRoles.TryGetValue(requirement.roleId, out int count);
                    spec.RequiredRoles[requirement.roleId] = count + requirement.quantity;
                }
                if (data.requiredEquipmentIds != null) spec.RequiredEquipment.AddRange(data.requiredEquipmentIds.Where(id => !string.IsNullOrWhiteSpace(id)));
                catalog.Restaurants[typeId] = spec;
            }
        }

        private void AddRoles(SimulationCatalog catalog)
        {
            foreach (var data in All<RoleData>("Roles"))
            {
                if (string.IsNullOrWhiteSpace(data.id)) continue;
                catalog.Roles[data.id] = new RoleSpec
                {
                    Id = data.id,
                    Name = data.displayName,
                    Salary = data.salary,
                    QualityContribution = data.qualityContribution,
                    Profile = catalog.Config.Role(data.id)
                };
            }
        }

        private void AddEquipment(SimulationCatalog catalog)
        {
            int days = Math.Max(1, catalog.Config.calendar.operatingDays);
            foreach (var data in All<EquipmentData>("Equipments"))
            {
                if (string.IsNullOrWhiteSpace(data.id)) continue;
                var profile = catalog.Config.EquipmentProfileFor(data.id);
                if (profile == null)
                    Warnings.Add("Equipamento " + data.id + " sem perfil no SimulationConfig: tratado como PREPARATION / STANDARD.");
                var spec = new EquipmentSpec
                {
                    Id = data.id,
                    Name = data.displayName,
                    Cost = data.cost,
                    CategoryId = profile?.categoryId ?? EquipmentCategoryId.Preparation,
                    TierId = profile?.tierId ?? EquipmentTierId.Standard,
                    // Os dois bonus se somam: o do asset (hoje 0 em todos) e o do perfil do simulador.
                    QualityBonus = (profile?.qualityBonus ?? 0d) + data.qualityBonus,
                    // capacityBonus do asset e mensal (pedidos/mes); o simulador trabalha por dia.
                    KitchenCapacityBonus = (profile?.kitchenCapacityBonus ?? 0d) + data.capacityBonus / (double)days,
                    PhysicalCapacityBonus = profile?.physicalCapacityBonus ?? 0d
                };
                if (data.applicableTypes != null) spec.ApplicableTypes.AddRange(data.applicableTypes.Select(t => t.ToString()));
                catalog.Equipment[data.id] = spec;
            }
        }

        private void AddCredits(SimulationCatalog catalog)
        {
            foreach (var data in All<CreditLineData>("CreditLines"))
            {
                if (string.IsNullOrWhiteSpace(data.id)) continue;
                catalog.Credits[data.id] = new CreditSpec { Id = data.id, Name = data.displayName, MaxAmount = data.maxAmount, MonthlyRate = data.monthlyInterestRate };
            }
        }

        // -----------------------------------------------------------------
        //  Eventos legados (EventData da Thaysla)
        // -----------------------------------------------------------------

        private static void OverlayTexts(SimulationConfig config, List<EventData> assets)
        {
            foreach (var data in assets)
            {
                var definition = config.Event(data.id);
                if (definition == null) continue;
                if (!string.IsNullOrWhiteSpace(data.title)) definition.title = data.title;
                if (!string.IsNullOrWhiteSpace(data.description)) definition.description = data.description;
                if (!string.IsNullOrWhiteSpace(data.educationalConcept)) definition.educationalConcept = data.educationalConcept;
                foreach (var option in data.options ?? Array.Empty<EventOption>())
                {
                    var reaction = option != null ? definition.Reaction(option.id) : null;
                    if (reaction == null) continue;
                    if (!string.IsNullOrWhiteSpace(option.title)) reaction.title = option.title;
                    if (!string.IsNullOrWhiteSpace(option.description)) reaction.description = option.description;
                }
            }
        }

        public static LegacyEventSpec ToLegacySpec(EventData data)
        {
            var spec = new LegacyEventSpec
            {
                Id = data.id,
                Title = data.title,
                Description = data.description,
                EducationalConcept = data.educationalConcept,
                Positive = data.polarity == EventPolarity.POSITIVE,
                Conditional = data.triggerType == EventTriggerType.CONDITIONAL,
                ExclusionGroup = data.exclusionGroup.ToString(),
                BaseWeight = data.baseWeight,
                CanRepeat = data.canRepeat,
                MinRound = data.minRound,
                MaxRound = data.maxRound
            };
            if (data.applicableRestaurantTypes != null) spec.RestaurantTypes.AddRange(data.applicableRestaurantTypes.Select(t => t.ToString()));
            foreach (var condition in data.conditions ?? Array.Empty<EventConditionData>())
            {
                if (condition == null) continue;
                spec.Conditions.Add(new LegacyConditionSpec { Type = condition.type.ToString(), Operator = condition.comparison.ToString(), Value = condition.value });
            }
            foreach (var option in data.options ?? Array.Empty<EventOption>())
            {
                if (option == null) continue;
                var effects = option.effects ?? new EventEffectData();
                spec.Options.Add(new LegacyOptionSpec
                {
                    Id = option.id,
                    Title = option.title,
                    Description = option.description,
                    CashDelta = effects.cashDelta,
                    DemandMultiplier = effects.demandMultiplier,
                    CapacityMultiplier = effects.capacityMultiplier,
                    StockMultiplier = effects.stockMultiplier,
                    IngredientCostMultiplier = effects.ingredientCostMultiplier,
                    AverageTicketMultiplier = effects.averageTicketMultiplier,
                    ReputationDelta = effects.reputationDelta,
                    Duration = effects.duration.ToString()
                });
            }
            return spec;
        }
    }
}
