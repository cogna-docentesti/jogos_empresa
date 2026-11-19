using System.Collections.Generic;
using System.Linq;
using Game.Domain.Simulation;

namespace Game.Tests.Simulation
{
    /// <summary>Montagem de cenarios para os testes do motor sistemico (sem Unity, sem banco).</summary>
    public static class SimulationTestKit
    {
        public static SimulationCatalog Catalog(System.Action<SimulationConfig> customize = null)
        {
            var config = DefaultSimulationConfig.Create();
            customize?.Invoke(config);
            return DefaultSimulationConfig.CreateCatalog(config);
        }

        /// <summary>Catalogo sem eventos aleatorios: isola o efeito das decisoes.</summary>
        public static SimulationCatalog QuietCatalog(System.Action<SimulationConfig> customize = null)
        {
            return Catalog(c =>
            {
                c.events.Clear();
                foreach (var category in c.equipmentCategories) category.baseFailuresPerMonth = 0d;
                c.market.demandNoiseSigma = 0d;
                customize?.Invoke(c);
            });
        }

        public static MonthInputs Inputs(SimulationCatalog catalog, string type = "PODRAO", string location = "store", string segment = "LOW",
            double pricePosition = 0.45d, ulong seed = 42UL, string strategyId = "equilibrado")
        {
            var strategy = StrategyProfile.Archetypes().First(s => s.Id == strategyId);
            strategy.PricePosition = pricePosition;
            return BalanceSimulator.BuildInputs(catalog, type, location, segment, strategy, seed);
        }

        public static MarketResult Market(SimulationCatalog catalog, string type, string location, string segment, double pricePosition,
            double quality = 0.5d, double reach = 0d, double awareness = 0d)
        {
            var restaurant = catalog.Restaurant(type);
            var prices = restaurant.Products.ToDictionary(p => p.Id, p => p.MinPrice + (p.MaxPrice - p.MinPrice) * pricePosition);
            return MarketModel.Evaluate(new MarketQuery
            {
                Restaurant = restaurant,
                Location = catalog.Location(location),
                TargetSegmentId = segment,
                MenuPrices = prices,
                Quality = quality,
                Reach = reach * awareness,
                Awareness = awareness,
                Reputation = 50d,
                Satisfaction = 50d,
                CompetitionPressure = catalog.Location(location).CompetitionIndex
            }, catalog.Config);
        }

        public static MonthOutcome RunMonth(SimulationCatalog catalog, MonthInputs inputs, IReactionPolicy policy = null)
        {
            var sim = new MonthSimulation(catalog, inputs, policy ?? new DefaultReactionPolicy());
            return sim.RunToEnd();
        }

        /// <summary>Evento deterministico que dispara no dia indicado com os modificadores dados.</summary>
        public static EventDefinition ForcedEvent(string id, int day, string group, params Modifier[] modifiers)
        {
            var definition = new EventDefinition
            {
                id = id,
                title = id,
                description = id,
                kind = EventKind.Deterministic,
                impact = EventImpact.Negative,
                requiresDecision = false,
                cooldownDays = 0,
                exclusionGroup = group,
                baseEffect = new EffectSpec { durationDays = 1 }
            };
            definition.conditions.Add(new EventCondition(Indicator.Day, "==", day));
            definition.baseEffect.modifiers.AddRange(modifiers);
            return definition;
        }

        public static double TotalServed(MonthlyReport report) => report.days.Sum(d => d.served);
    }

    /// <summary>Politica que sempre escolhe a mesma reacao quando ela existe.</summary>
    public sealed class FixedReactionPolicy : IReactionPolicy
    {
        private readonly string _reactionId;
        public readonly List<string> Seen = new List<string>();

        public FixedReactionPolicy(string reactionId) { _reactionId = reactionId; }

        public string Choose(PendingDecision decision, IndicatorSet indicators)
        {
            Seen.Add(decision.Definition.id);
            if (decision.Options.Any(o => o.ReactionId == _reactionId && o.Enabled)) return _reactionId;
            return new DefaultReactionPolicy().Choose(decision, indicators);
        }
    }
}
