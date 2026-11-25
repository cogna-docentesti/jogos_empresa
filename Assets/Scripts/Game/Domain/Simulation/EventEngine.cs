// Game/Domain/Simulation/EventEngine.cs
// Sorteia os eventos do dia. A chance de cada evento depende dos "observáveis":
// números que o próprio motor calculou nos dias anteriores (desperdício, qualidade, moral...).
// É aqui que "o motor calcula as eventualidades a partir dos cálculos".
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    /// <summary>Os x_k da fórmula: estado do restaurante que os eventos conseguem "enxergar".</summary>
    public sealed class Observables
    {
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>();
        public double this[string name]
        {
            get => _values.TryGetValue(name, out var v) ? v : 0.0;
            set => _values[name] = value;
        }
        public IReadOnlyDictionary<string, double> All => _values;

        // Nomes usados nas covariáveis e condições do catálogo
        public const string Quality = "quality", Satisfaction = "satisfaction", Reputation = "reputation",
                            Morale = "morale", ErrorRate = "errors.rate", WaitFactor = "wait.factor",
                            LostRatio = "lost.ratio", WasteRatio = "waste.ratio",
                            RefrigerationDown = "refrigeration.down", BrandRecall = "brand.recall",
                            CompetitionPressure = "competition.pressure", PreviousMargin = "prev.margin",
                            MaintenanceGap = "maintenance.gap", StockCoverDays = "stock.coverDays";
    }

    /// <summary>Ids de eventos que o orquestrador trata de forma especial.</summary>
    public static class EventIds
    {
        public const string StaffAbsent = "staff_absent", StaffResigned = "staff_resigned",
                            CashShortfall = "cash_shortfall", EquipmentStillDown = "equipment_still_down";
    }

    /// <summary>Uma ocorrência de evento em um dia. Vai para o popup se pedir decisão.</summary>
    public sealed class EventOccurrence
    {
        public string OccurrenceId;           // "m1-d12-supplier_delay"
        public EventDefinition Definition;
        public int Month, Day;
        public string UnitId;                 // evento de equipamento
        public string StaffId;                // evento de funcionário
        public double ChanceToday;            // P mostrada no popup
        public List<(string factor, double value)> Explanation = new List<(string, double)>();
        public string ChosenReactionId;
        public double PendingInterest, PendingAmortization;   // "Caixa insuficiente para a parcela"
        public bool RequiresDecision => Definition.RequiresDecision;
    }

    public sealed class EventContext
    {
        public int Month, Day, AbsDay;
        public SessionSetup Setup;
        public SimulationState State;
        public Observables X;
        public LeverSet Levers;
        public bool HasManager;
        public HashSet<string> ActiveGroups = new HashSet<string>();
    }

    public sealed class EventEngine
    {
        private readonly SimulationCatalog _cat;
        private readonly DeterministicRandom _rng;

        public EventEngine(SimulationCatalog catalog, DeterministicRandom rng)
        {
            _cat = catalog;
            _rng = rng;
        }

        // ------------------------------------------------------------ fórmula
        /// h = taxa/30 * e^(soma beta*(x-ref)) * HR_alavancas * HR_gestão * max(0,35; e^(-0,35*severidade_10dias))
        public double DailyHazard(EventDefinition def, EventContext ctx, List<(string, double)> explanation = null)
        {
            var k = _cat.Constants;
            double basePerDay = def.BaseRatePerMonth / k.DaysPerMonth;

            double linear = 0;
            foreach (var c in def.Covariates) linear += c.Beta * (ctx.X[c.Variable] - c.Reference);
            double stateFactor = Math.Min(k.CovariateFactorMax, Math.Max(k.CovariateFactorMin, Math.Exp(linear)));

            double leverHr = ctx.Levers.Factor(LeverKeys.EventHr(def.Id));
            double managementHr = def.IsNegative && ctx.HasManager ? k.ManagerRiskHr : 1.0;
            double antiStreak = def.IsNegative ? AntiStreakFactor(ctx) : 1.0;

            double h = basePerDay * stateFactor * leverHr * managementHr * antiStreak;
            if (explanation != null)
            {
                explanation.Add(("taxa base por dia", basePerDay));
                explanation.Add(("estado do restaurante (covariáveis)", stateFactor));
                explanation.Add(("políticas e reações ativas", leverHr));
                explanation.Add(("gerente", managementHr));
                explanation.Add(("proteção contra azar", antiStreak));
            }
            return h;
        }

        /// max(0,35; e^(-0,35 * severidade dos eventos negativos dos últimos 10 dias))
        public double AntiStreakFactor(EventContext ctx)
        {
            var k = _cat.Constants;
            double severity = ctx.State.EventHistory
                .Where(r => r.IsNegative && r.AbsDay > ctx.AbsDay - 10 && r.AbsDay < ctx.AbsDay)
                .Sum(r => r.Severity);
            return Math.Max(k.AntiStreakFloor, Math.Exp(-k.AntiStreakK * severity));
        }

        // ------------------------------------------------------------ elegibilidade
        public bool IsEligible(EventDefinition def, EventContext ctx)
        {
            var k = _cat.Constants;
            if (def.Deterministic) return false;                                   // disparado pelo motor, não sorteado
            if (def.IsNegative && ctx.AbsDay <= k.GraceDaysWithoutNegative) return false;
            if (def.OnlyRestaurants != null && !def.OnlyRestaurants.Contains(ctx.Setup.Restaurant)) return false;
            if (def.OnlyRegions != null && !def.OnlyRegions.Contains(ctx.Setup.RegionId)) return false;
            if (def.Group != null && ctx.ActiveGroups.Contains(def.Group)) return false;

            var history = ctx.State.EventHistory.Where(r => r.EventId == def.Id).ToList();
            if (history.Count >= def.MaxPerMatch) return false;
            if (!def.Repeats && history.Count > 0) return false;
            if (history.Count > 0 && ctx.AbsDay - history.Max(r => r.AbsDay) < def.MinIntervalDays) return false;

            foreach (var c in def.Conditions)
                if (!Compare(ctx.X[c.Variable], c.Operator, c.Value)) return false;
            return true;
        }

        private static bool Compare(double x, string op, double v) => op switch
        {
            ">=" => x >= v, "<=" => x <= v, ">" => x > v, "<" => x < v, "==" => Math.Abs(x - v) < 1e-9,
            _ => throw new ArgumentException("Operador desconhecido: " + op)
        };

        // ------------------------------------------------------------ sorteio do dia
        /// Eventos de escopo global: um sorteio por evento elegível, com chave (mês, dia, id do evento).
        public List<EventOccurrence> DrawGlobal(EventContext ctx)
        {
            var result = new List<EventOccurrence>();
            foreach (var def in _cat.Events.Where(e => e.Scope == EventScope.Global))
            {
                if (!IsEligible(def, ctx)) continue;
                var explanation = new List<(string, double)>();
                double p = 1.0 - Math.Exp(-DailyHazard(def, ctx, explanation));
                if (_rng.Happens(p, ctx.Month, ctx.Day, "event." + def.Id))
                    result.Add(NewOccurrence(def, ctx, p, explanation));
            }
            return result;
        }

        /// Eventos por funcionário: um sorteio por pessoa. Chance de acontecer com alguém = 1 - produto(1 - p_i).
        public List<EventOccurrence> DrawPerEmployee(EventContext ctx, string eventId, Func<StaffMember, double> probabilityOf)
        {
            var def = _cat.Event(eventId);
            var result = new List<EventOccurrence>();
            if (def == null || !IsEligible(def, ctx)) return result;
            for (int i = 0; i < ctx.State.Staff.Count; i++)
            {
                var person = ctx.State.Staff[i];
                double p = probabilityOf(person);
                if (_rng.Happens(p, ctx.Month, ctx.Day, "event." + eventId, i))
                {
                    var occ = NewOccurrence(def, ctx, p, new List<(string, double)> { ("chance por pessoa", p) });
                    occ.StaffId = person.StaffId;
                    occ.OccurrenceId += "-" + person.StaffId;
                    result.Add(occ);
                }
            }
            return result;
        }

        public EventOccurrence NewOccurrence(EventDefinition def, EventContext ctx, double chance, List<(string, double)> explanation)
            => new EventOccurrence
            {
                OccurrenceId = $"m{ctx.Month}-d{ctx.Day}-{def.Id}",
                Definition = def,
                Month = ctx.Month,
                Day = ctx.Day,
                ChanceToday = chance,
                Explanation = explanation
            };

        // ------------------------------------------------------------ limites do dia
        /// No máximo 2 eventos por dia e 1 com decisão. Ordem de prioridade fixa (a do catálogo),
        /// para o resultado não depender da ordem do sorteio.
        public List<EventOccurrence> ApplyDailyLimits(List<EventOccurrence> drawn, int decisionSlotsAlreadyUsed)
        {
            var k = _cat.Constants;
            var kept = new List<EventOccurrence>();
            int decisions = decisionSlotsAlreadyUsed;
            var groups = new HashSet<string>();
            foreach (var occ in drawn.OrderBy(o => _cat.Events.IndexOf(o.Definition)).ThenBy(o => o.OccurrenceId, StringComparer.Ordinal))
            {
                if (kept.Count >= k.MaxEventsPerDay) break;
                if (occ.RequiresDecision && decisions >= k.MaxDecisionEventsPerDay) continue;
                if (occ.Definition.Group != null && !groups.Add(occ.Definition.Group)) continue;
                kept.Add(occ);
                if (occ.RequiresDecision) decisions++;
            }
            return kept;
        }
    }
}
