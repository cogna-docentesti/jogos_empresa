using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    /// <summary>Condicao de um EventData legado (tipo e operador como texto do enum).</summary>
    public sealed class LegacyConditionSpec
    {
        public string Type;
        public string Operator;
        public double Value;
    }

    /// <summary>Opcao de um EventData legado, com os campos do EventEffectData.</summary>
    public sealed class LegacyOptionSpec
    {
        public string Id;
        public string Title;
        public string Description;
        public double CashDelta;
        public double DemandMultiplier = 1d;
        public double CapacityMultiplier = 1d;
        public double StockMultiplier = 1d;
        public double IngredientCostMultiplier = 1d;
        public double AverageTicketMultiplier = 1d;
        public int ReputationDelta;
        /// <summary>IMMEDIATE, CURRENT_DAY ou CURRENT_MONTH.</summary>
        public string Duration = "IMMEDIATE";
    }

    /// <summary>
    /// Copia neutra de um EventData (ScriptableObject da Thaysla). O adaptador Unity preenche;
    /// assim a conversao fica no dominio e pode ser testada sem a Unity.
    /// </summary>
    public sealed class LegacyEventSpec
    {
        public string Id;
        public string Title;
        public string Description;
        public string EducationalConcept;
        public bool Positive;
        public bool Conditional;
        /// <summary>NONE ou o nome do grupo (SOCIAL_REPUTATION, SUPPLIER, COMPETITION, DEMAND_SPIKE).</summary>
        public string ExclusionGroup;
        public double BaseWeight = 1d;
        public bool CanRepeat;
        public List<string> RestaurantTypes = new List<string>();
        public int MinRound = 1;
        public int MaxRound = 3;
        public List<LegacyConditionSpec> Conditions = new List<LegacyConditionSpec>();
        public List<LegacyOptionSpec> Options = new List<LegacyOptionSpec>();
    }

    /// <summary>
    /// Converte eventos do formato antigo (um sorteio por mes, efeitos fixos) para o formato do
    /// motor diario. Usado so para eventos que NAO tem definicao propria no SimulationConfig:
    /// um EventData novo criado pela equipe passa a aparecer no jogo sem programacao.
    ///
    /// Regras de conversao:
    ///   baseWeight            -> baseRatePerMonth = baseWeight x legacyRatePerWeight
    ///   canRepeat = false     -> maxPerGame = 1
    ///   minRound / maxRound   -> minMonth / maxMonth
    ///   cashDelta &lt; 0        -> custo da reacao (saida de caixa)
    ///   cashDelta &gt; 0        -> cash.instant (entrada, linha "outras receitas")
    ///   reputationDelta       -> reputation.instant
    ///   demandMultiplier      -> demand.factor
    ///   capacityMultiplier    -> capacity.front.factor e capacity.kitchen.factor
    ///   stockMultiplier &lt; 1  -> stock.loss.instant = 1 - multiplicador
    ///   stockMultiplier &gt; 1  -> stock.boost.instant = legacyStockBoostDays x (multiplicador - 1)
    ///   ingredientCost...     -> cost.ingredient.factor
    ///   averageTicket...      -> ticket.factor
    ///   IMMEDIATE/CURRENT_DAY -> efeito de 1 dia; CURRENT_MONTH -> ate o fim do mes
    ///   EQUIPMENT_QUALITY_SCORE -> indicador "quality" (0..1)
    ///   TEAM_COVERAGE_RATIO     -> indicador "team.coverage" (0..1)
    ///   REPUTATION_SCORE        -> indicador "reputation" (0..100)
    /// Indicadores descontinuados (SANITARY_RISK_SCORE, STOCK_COVERAGE_RATIO) sao ignorados com aviso.
    /// Toda decisao ganha uma reacao sem custo ("nao agir"), se o evento nao tiver uma.
    /// </summary>
    public static class LegacyEventConverter
    {
        public const string DeclineReactionId = "legacy_decline";

        public static EventDefinition Convert(LegacyEventSpec spec, EventEngineParameters parameters, List<string> warnings = null)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (string.IsNullOrWhiteSpace(spec.Id)) throw new InvalidOperationException("Evento legado sem id.");
            parameters = parameters ?? new EventEngineParameters();

            var definition = new EventDefinition
            {
                id = spec.Id,
                title = string.IsNullOrWhiteSpace(spec.Title) ? spec.Id : spec.Title,
                description = spec.Description,
                educationalConcept = spec.EducationalConcept,
                category = EventCategory.Operations,
                impact = spec.Positive ? EventImpact.Positive : EventImpact.Negative,
                kind = EventKind.Probabilistic,
                scope = EventScope.Global,
                baseRatePerMonth = Math.Max(0d, spec.BaseWeight) * parameters.legacyRatePerWeight,
                minMonth = Math.Max(1, spec.MinRound),
                maxMonth = Math.Max(Math.Max(1, spec.MinRound), spec.MaxRound),
                canRecur = spec.CanRepeat,
                maxPerGame = spec.CanRepeat ? 0 : 1,
                cooldownDays = 10,
                exclusionGroup = string.IsNullOrEmpty(spec.ExclusionGroup) || spec.ExclusionGroup == "NONE" ? null : spec.ExclusionGroup,
                severity = spec.Positive ? 0d : 1d,
                requiresDecision = spec.Options.Count > 0,
                baseEffect = new EffectSpec { durationDays = 0 }
            };
            definition.restaurantTypes.AddRange(spec.RestaurantTypes.Where(t => !string.IsNullOrEmpty(t)));

            foreach (var condition in spec.Conditions)
            {
                string indicator = MapIndicator(condition.Type);
                if (indicator == null)
                {
                    warnings?.Add("Evento " + spec.Id + ": condicao " + condition.Type + " nao tem indicador no motor diario e foi ignorada.");
                    continue;
                }
                definition.conditions.Add(new EventCondition(indicator, MapOperator(condition.Operator), condition.Value));
            }

            foreach (var option in spec.Options)
            {
                if (option == null || string.IsNullOrWhiteSpace(option.Id)) continue;
                definition.reactions.Add(ConvertOption(option, parameters));
            }

            if (definition.requiresDecision)
            {
                bool hasFree = definition.reactions.Any(r => r.cost <= 0d && r.conditions.Count == 0);
                if (!hasFree)
                {
                    definition.reactions.Add(new EventReaction
                    {
                        id = DeclineReactionId,
                        title = spec.Positive ? "Não aproveitar" : "Não agir agora",
                        description = spec.Positive
                            ? "Deixar a oportunidade passar, sem custo."
                            : "Seguir a operação sem gastar agora e aceitar o risco.",
                        outcome = "Nenhum valor foi gasto com esta ocorrência."
                    });
                }
                definition.defaultReactionId = definition.reactions
                    .Where(r => r.conditions.Count == 0)
                    .OrderBy(r => r.cost)
                    .ThenBy(r => r.id, StringComparer.Ordinal)
                    .Select(r => r.id)
                    .First();
            }
            return definition;
        }

        /// <summary>Converte os eventos legados que o catalogo ainda nao conhece e os acrescenta ao config.</summary>
        public static int AppendMissing(SimulationConfig config, IEnumerable<LegacyEventSpec> legacy, List<string> warnings = null)
        {
            if (config == null || legacy == null) return 0;
            int added = 0;
            foreach (var spec in legacy)
            {
                if (spec == null || string.IsNullOrWhiteSpace(spec.Id) || config.Event(spec.Id) != null) continue;
                try
                {
                    config.events.Add(Convert(spec, config.eventEngine, warnings));
                    added++;
                }
                catch (Exception exception)
                {
                    warnings?.Add("Evento legado " + spec.Id + " ignorado: " + exception.Message);
                }
            }
            return added;
        }

        private static EventReaction ConvertOption(LegacyOptionSpec option, EventEngineParameters parameters)
        {
            var reaction = new EventReaction
            {
                id = option.Id,
                title = string.IsNullOrWhiteSpace(option.Title) ? option.Id : option.Title,
                description = option.Description,
                cost = option.CashDelta < 0d ? -option.CashDelta : 0d,
                financeable = true,
                costLine = CostLine.Other,
                outcome = option.Description
            };
            var effect = reaction.effect;
            if (option.Duration == "CURRENT_MONTH")
            {
                effect.end = EffectEnd.MonthEnd;
                effect.durationDays = 31;
            }
            else
            {
                effect.durationDays = 1;
            }
            if (option.CashDelta > 0d)
            {
                effect.modifiers.Add(new Modifier(Lever.CashInstant, option.CashDelta));
                effect.costLine = CostLine.Revenue;
            }
            if (option.ReputationDelta != 0) effect.modifiers.Add(new Modifier(Lever.ReputationInstant, option.ReputationDelta));
            AddFactor(effect, Lever.DemandFactor, option.DemandMultiplier);
            if (IsSet(option.CapacityMultiplier))
            {
                AddFactor(effect, Lever.FrontCapacityFactor, option.CapacityMultiplier);
                AddFactor(effect, Lever.KitchenCapacityFactor, option.CapacityMultiplier);
            }
            if (IsSet(option.StockMultiplier))
            {
                if (option.StockMultiplier < 1d)
                    effect.modifiers.Add(new Modifier(Lever.StockLossInstant, SimMath.Clamp(1d - option.StockMultiplier, 0d, 1d)));
                else
                    effect.modifiers.Add(new Modifier(Lever.StockBoostDays, parameters.legacyStockBoostDays * (option.StockMultiplier - 1d)));
            }
            AddFactor(effect, Lever.IngredientCostFactor, option.IngredientCostMultiplier);
            AddFactor(effect, Lever.TicketFactor, option.AverageTicketMultiplier);
            return reaction;
        }

        private static bool IsSet(double multiplier) => multiplier > 0d && Math.Abs(multiplier - 1d) > 1e-6;

        private static void AddFactor(EffectSpec effect, string key, double multiplier)
        {
            if (IsSet(multiplier)) effect.modifiers.Add(new Modifier(key, multiplier));
        }

        internal static string MapIndicator(string legacyType)
        {
            switch (legacyType)
            {
                case "EQUIPMENT_QUALITY_SCORE": return Indicator.Quality;
                case "TEAM_COVERAGE_RATIO": return Indicator.TeamCoverage;
                case "REPUTATION_SCORE": return Indicator.Reputation;
                default: return null;
            }
        }

        internal static string MapOperator(string legacyOperator)
        {
            switch (legacyOperator)
            {
                case "EQUALS": return "==";
                case "NOT_EQUALS": return "!=";
                case "GREATER_THAN": return ">";
                case "GREATER_OR_EQUAL": return ">=";
                case "LESS_THAN": return "<";
                case "LESS_OR_EQUAL": return "<=";
                default: throw new InvalidOperationException("Operador legado desconhecido: " + legacyOperator);
            }
        }
    }
}
