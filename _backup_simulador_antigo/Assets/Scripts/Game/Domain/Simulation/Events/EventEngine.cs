using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    /// <summary>Um funcionario presente no dia, para eventos de escopo EMPLOYEE.</summary>
    public sealed class EmployeeSlot
    {
        public string RoleId;
        public double Count;
    }

    public sealed class EventContext
    {
        public SimulationConfig Config;
        public IndicatorSet Indicators;
        public SimulationState State;
        public LeverSet Levers;
        public DeterministicRandom Rng;
        public int Month;
        public int Day;
        public int AbsDay;
        public string RestaurantTypeId;
        public string LocationId;
        /// <summary>Hazard ratio de risco operacional (gerente: 0,85).</summary>
        public double ManagementRiskRatio = 1d;
        public List<EmployeeSlot> Employees = new List<EmployeeSlot>();
        /// <summary>Grupos de exclusao com efeito ativo hoje.</summary>
        public HashSet<string> ActiveGroups = new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>Evento sorteado (ou determinado) no dia.</summary>
    public sealed class EventFire
    {
        public EventDefinition Definition;
        public double Probability;
        public double RatePerDay;
        public string RoleId;
        public string UnitKey;
        public List<string> Drivers = new List<string>();
    }

    /// <summary>Probabilidade calculada de um evento, com a explicacao de cada fator.</summary>
    public sealed class EventProbability
    {
        public bool Eligible;
        public string Reason;
        public double RatePerDay;
        public double Probability;
        public double MercyFactor = 1d;
        public List<string> Drivers = new List<string>();
    }

    /// <summary>
    /// Motor de eventos condicionais.
    ///
    /// Para cada evento elegivel no dia:
    ///   taxa = (ocorrencias_por_mes / 30) x exp(soma coef_k x (indicador_k - referencia_k))
    ///          x HR do evento vindo de politicas/efeitos x HR de gestao x amortecimento
    ///   p = 1 - e^(-taxa)
    /// e um numero uniforme com chave (mes, dia, id do evento) decide se ocorre.
    ///
    /// Elegibilidade: condicoes (E), tipo de restaurante, localizacao, faixa de meses,
    /// recorrencia, intervalo minimo, limite por partida e grupo de exclusao.
    /// Eventos DETERMINISTIC nao sorteiam: ocorrem quando as condicoes valem.
    /// </summary>
    public static class EventEngine
    {
        public static double MercyFactor(SimulationState state, int absDay, EventEngineParameters p)
        {
            if (state?.recentNegatives == null) return 1d;
            double severity = state.recentNegatives.Where(r => absDay - r.absDay >= 0 && absDay - r.absDay < p.mercyWindowDays).Sum(r => r.severity);
            return Math.Max(p.mercyMinimumFactor, Math.Exp(-p.mercyStrength * severity));
        }

        public static bool IsEligible(EventDefinition d, EventContext ctx, out string reason)
        {
            reason = null;
            if (d == null || string.IsNullOrEmpty(d.id)) { reason = "definicao invalida"; return false; }
            if (ctx.Month < d.minMonth || ctx.Month > d.maxMonth) { reason = "fora da faixa de meses"; return false; }
            if (ctx.Day < d.minDayOfMonth) { reason = "antes do dia minimo"; return false; }
            if (d.restaurantTypes != null && d.restaurantTypes.Count > 0 && !d.restaurantTypes.Contains(ctx.RestaurantTypeId)) { reason = "tipo de restaurante"; return false; }
            if (d.locations != null && d.locations.Count > 0 && !d.locations.Contains(ctx.LocationId)) { reason = "localizacao"; return false; }
            var counter = ctx.State.eventCounters.Find(c => c.eventId == d.id);
            if (counter != null)
            {
                if (!d.canRecur && counter.occurrences > 0) { reason = "nao recorrente"; return false; }
                if (d.maxPerGame > 0 && counter.occurrences >= d.maxPerGame) { reason = "limite por partida"; return false; }
                if (ctx.AbsDay - counter.lastAbsDay < Math.Max(0, d.cooldownDays)) { reason = "intervalo minimo"; return false; }
            }
            if (!string.IsNullOrEmpty(d.exclusionGroup) && ctx.ActiveGroups.Contains(d.exclusionGroup)) { reason = "grupo de exclusao ativo"; return false; }
            if (!ctx.Indicators.Satisfies(d.conditions, out string failed)) { reason = "condicao: " + failed; return false; }
            if (d.kind == EventKind.Probabilistic && d.IsNegative && ctx.AbsDay <= ctx.Config.eventEngine.openingGraceDays)
            {
                reason = "periodo de abertura";
                return false;
            }
            return true;
        }

        /// <summary>Taxa diaria de um evento GLOBAL (ou por funcionario, para EMPLOYEE).</summary>
        public static double RatePerDay(EventDefinition d, EventContext ctx, List<string> drivers)
        {
            double rate = Math.Max(0d, d.baseRatePerMonth) / 30d;
            double logRatio = 0d;
            if (d.modifiers != null)
            {
                foreach (var m in d.modifiers)
                {
                    if (m == null || string.IsNullOrEmpty(m.indicator)) continue;
                    double term = m.coefficient * (ctx.Indicators[m.indicator] - m.reference);
                    logRatio += term;
                    if (drivers != null && Math.Abs(term) >= 0.1d && !string.IsNullOrEmpty(m.description))
                        drivers.Add(m.description + " (x" + Math.Exp(term).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ")");
                }
            }
            logRatio = SimMath.Clamp(logRatio, -6d, 4d);
            rate *= Math.Exp(logRatio);

            double leverRatio = ctx.Levers?.Factor(Lever.EventHazard(d.id)) ?? 1d;
            if (drivers != null && Math.Abs(leverRatio - 1d) >= 0.05d)
                drivers.Add("decisoes e efeitos ativos (x" + leverRatio.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ")");
            rate *= leverRatio;

            bool operational = d.IsNegative && d.category != EventCategory.Market && d.scope != EventScope.Employee;
            if (operational) rate *= ctx.ManagementRiskRatio;

            var scales = ctx.Config.eventEngine;
            rate *= d.IsNegative ? scales.negativeRateScale : (d.impact == EventImpact.Positive ? scales.positiveRateScale : 1d);
            return rate;
        }

        public static EventProbability Evaluate(EventDefinition d, EventContext ctx)
        {
            var result = new EventProbability();
            result.Eligible = IsEligible(d, ctx, out result.Reason);
            if (!result.Eligible) return result;
            if (d.kind == EventKind.Deterministic)
            {
                result.Probability = 1d;
                result.RatePerDay = double.PositiveInfinity;
                return result;
            }
            double rate = RatePerDay(d, ctx, result.Drivers);
            if (d.scope == EventScope.Employee)
            {
                double people = ctx.Employees.Sum(e => Math.Max(0d, e.Count));
                rate *= people;
            }
            if (d.IsNegative)
            {
                result.MercyFactor = MercyFactor(ctx.State, ctx.AbsDay, ctx.Config.eventEngine);
                rate *= result.MercyFactor;
            }
            result.RatePerDay = rate;
            result.Probability = SimMath.HazardToProbability(rate);
            return result;
        }

        /// <summary>
        /// Sorteia os eventos GLOBAL e EMPLOYEE do dia. A ordem e alfabetica por id e cada
        /// sorteio tem a sua chave, entao o resultado nao depende da ordem do catalogo.
        /// Eventos de escopo EQUIPMENT_UNIT sao tratados pela simulacao, unidade a unidade.
        /// </summary>
        public static List<EventFire> DrawDay(IEnumerable<EventDefinition> catalog, EventContext ctx, List<string> diagnostics = null)
        {
            var fired = new List<EventFire>();
            var groupsToday = new HashSet<string>(ctx.ActiveGroups, StringComparer.Ordinal);
            foreach (var d in catalog.Where(e => e != null && e.scope != EventScope.EquipmentUnit).OrderBy(e => e.id, StringComparer.Ordinal))
            {
                if (!string.IsNullOrEmpty(d.exclusionGroup) && groupsToday.Contains(d.exclusionGroup)) continue;
                var probability = Evaluate(d, ctx);
                if (!probability.Eligible || probability.Probability <= 0d) continue;
                bool happens = d.kind == EventKind.Deterministic
                               || ctx.Rng.Uniform(ctx.Month, ctx.Day, "event", d.id) < probability.Probability;
                if (!happens) continue;

                var fire = new EventFire { Definition = d, Probability = probability.Probability, RatePerDay = probability.RatePerDay, Drivers = probability.Drivers };
                if (d.scope == EventScope.Employee) fire.RoleId = PickEmployee(ctx, d.id);
                if (d.scope == EventScope.Employee && fire.RoleId == null) continue;
                fired.Add(fire);
                if (!string.IsNullOrEmpty(d.exclusionGroup)) groupsToday.Add(d.exclusionGroup);
            }

            // Limites diarios: mantem os mais severos (empate: ordem por id).
            var ordered = fired.OrderByDescending(f => f.Definition.kind == EventKind.Deterministic)
                               .ThenByDescending(f => f.Definition.severity)
                               .ThenBy(f => f.Definition.id, StringComparer.Ordinal).ToList();
            var kept = new List<EventFire>();
            int decisions = 0;
            foreach (var fire in ordered)
            {
                if (kept.Count >= ctx.Config.eventEngine.maxEventsPerDay && fire.Definition.kind != EventKind.Deterministic)
                {
                    diagnostics?.Add("Dia " + ctx.Day + ": " + fire.Definition.id + " descartado pelo limite diario de eventos.");
                    continue;
                }
                if (fire.Definition.requiresDecision)
                {
                    if (decisions >= ctx.Config.eventEngine.maxDecisionEventsPerDay && fire.Definition.kind != EventKind.Deterministic)
                    {
                        diagnostics?.Add("Dia " + ctx.Day + ": " + fire.Definition.id + " descartado pelo limite diario de decisoes.");
                        continue;
                    }
                    decisions++;
                }
                kept.Add(fire);
            }
            return kept;
        }

        private static string PickEmployee(EventContext ctx, string eventId)
        {
            double total = ctx.Employees.Sum(e => Math.Max(0d, e.Count));
            if (total <= 0d) return null;
            double cursor = ctx.Rng.Uniform(ctx.Month, ctx.Day, "employee", eventId) * total;
            foreach (var slot in ctx.Employees.OrderBy(e => e.RoleId, StringComparer.Ordinal))
            {
                if (slot.Count <= 0d) continue;
                if (cursor < slot.Count) return slot.RoleId;
                cursor -= slot.Count;
            }
            return ctx.Employees.Where(e => e.Count > 0d).OrderBy(e => e.RoleId, StringComparer.Ordinal).Last().RoleId;
        }
    }
}
