using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    /// <summary>
    /// Estado dinamico que atravessa os meses (o que o GameSessionEntity nao guarda).
    /// Serializavel por JsonUtility: so listas e campos publicos.
    /// Gravado uma vez por mes, no fechamento, na tabela SimulationStateEntity.
    /// </summary>
    [Serializable]
    public class SimulationState
    {
        public int schemaVersion = 1;
        /// <summary>Ultimo mes fechado (0 = nenhum).</summary>
        public int lastClosedMonth;

        public double satisfaction = 60d;
        public double morale = 65d;
        public double awareness;
        public double experienceMonths;
        public double expectedDailyDemand;
        /// <summary>Capacidade diaria media recente (suavizada). Com a demanda, define quanto vale comprar.</summary>
        public double expectedDailyCapacity;

        public double stockMeals;
        public double stockValue;

        public double lastMonthNetMargin;
        public double lastMonthMinMenuPrice;
        public double lastMonthDemand;

        /// <summary>Patrimonio esperado na abertura do proximo mes (para conciliar movimentos feitos no hub).</summary>
        public double expectedOpeningEquity;
        public bool hasOpeningEquity;
        public double capitalAdjustment;
        public double retainedEarnings;

        public List<EquipmentUnitState> units = new List<EquipmentUnitState>();
        public List<ActiveEffectState> effects = new List<ActiveEffectState>();
        public List<EventCounterState> eventCounters = new List<EventCounterState>();
        public List<RecentEventState> recentNegatives = new List<RecentEventState>();
        public List<RoleCountState> vacancies = new List<RoleCountState>();
        public List<RoleCountState> lastTeam = new List<RoleCountState>();
        public List<PayableState> payables = new List<PayableState>();
        public List<DailyWasteState> wasteWindow = new List<DailyWasteState>();
        public int lastPromotionAbsDay = -999;
        public int monthsWithoutPreventive;

        public SimulationState Clone()
        {
            var copy = (SimulationState)MemberwiseClone();
            copy.units = units.Select(u => u.Clone()).ToList();
            copy.effects = effects.Select(e => e.Clone()).ToList();
            copy.eventCounters = eventCounters.Select(e => e.Clone()).ToList();
            copy.recentNegatives = recentNegatives.Select(e => e.Clone()).ToList();
            copy.vacancies = vacancies.Select(e => e.Clone()).ToList();
            copy.lastTeam = lastTeam.Select(e => e.Clone()).ToList();
            copy.payables = payables.Select(e => e.Clone()).ToList();
            copy.wasteWindow = wasteWindow.Select(e => e.Clone()).ToList();
            return copy;
        }

        public EventCounterState Counter(string eventId)
        {
            var counter = eventCounters.Find(c => c.eventId == eventId);
            if (counter == null)
            {
                counter = new EventCounterState { eventId = eventId, lastAbsDay = -9999 };
                eventCounters.Add(counter);
            }
            return counter;
        }

        public int Vacancies(string roleId) => vacancies.Find(v => v.roleId == roleId)?.count ?? 0;

        public void SetVacancies(string roleId, int count)
        {
            var item = vacancies.Find(v => v.roleId == roleId);
            if (item == null)
            {
                if (count <= 0) return;
                vacancies.Add(new RoleCountState { roleId = roleId, count = count });
                return;
            }
            item.count = Math.Max(0, count);
            if (item.count == 0) vacancies.Remove(item);
        }
    }

    [Serializable]
    public class EquipmentUnitState
    {
        /// <summary>Chave estavel da unidade: id do equipamento + numero de serie no jogo.</summary>
        public string unitKey;
        public string equipmentId;
        public string tierId;
        public string categoryId;
        public double purchaseCost;
        public double ageMonths;
        public double accumulatedDepreciation;
        public bool broken;
        public int brokenSinceAbsDay;
        public int lastReminderAbsDay;
        public int failures;
        public List<int> failureDays = new List<int>();
        /// <summary>Hazard ratio temporario da unidade (reparo emergencial, conserto especializado).</summary>
        public double hazardRatio = 1d;
        public int hazardRatioUntilAbsDay;

        public double BookValue => Math.Max(0d, purchaseCost - accumulatedDepreciation);

        public EquipmentUnitState Clone()
        {
            var copy = (EquipmentUnitState)MemberwiseClone();
            copy.failureDays = new List<int>(failureDays ?? new List<int>());
            return copy;
        }
    }

    [Serializable]
    public class ActiveEffectState
    {
        public string occurrenceId;
        public string eventId;
        public string label;
        public List<Modifier> modifiers = new List<Modifier>();
        public int startAbsDay;
        /// <summary>Ultimo dia (inclusive) para efeitos com fim em DAYS.</summary>
        public int endAbsDay;
        public string end = EffectEnd.Days;
        public string unitKey;
        public bool unresolved;
        public string exclusionGroup;

        public ActiveEffectState Clone()
        {
            var copy = (ActiveEffectState)MemberwiseClone();
            copy.modifiers = (modifiers ?? new List<Modifier>()).Select(m => new Modifier(m.key, m.value)).ToList();
            return copy;
        }
    }

    [Serializable]
    public class EventCounterState
    {
        public string eventId;
        public int occurrences;
        public int lastAbsDay = -9999;

        public EventCounterState Clone() => (EventCounterState)MemberwiseClone();
    }

    [Serializable]
    public class RecentEventState
    {
        public int absDay;
        public double severity;

        public RecentEventState Clone() => (RecentEventState)MemberwiseClone();
    }

    [Serializable]
    public class RoleCountState
    {
        public string roleId;
        public int count;

        public RoleCountState Clone() => (RoleCountState)MemberwiseClone();
    }

    public static class PayableKind
    {
        public const string Supplier = "SUPPLIER";
        public const string Bill = "BILL";
    }

    [Serializable]
    public class PayableState
    {
        public string kind = PayableKind.Supplier;
        public string label;
        /// <summary>Linha do fluxo de caixa quando for paga (SUPPLIERS, RENT, PAYROLL, UTILITIES, TAXES, MARKETING, MAINTENANCE, OTHER).</summary>
        public string cashLine;
        public double amount;
        public int dueAbsDay;

        public PayableState Clone() => (PayableState)MemberwiseClone();
    }

    [Serializable]
    public class DailyWasteState
    {
        public int absDay;
        public double wasted;
        public double consumed;

        public DailyWasteState Clone() => (DailyWasteState)MemberwiseClone();
    }
}
