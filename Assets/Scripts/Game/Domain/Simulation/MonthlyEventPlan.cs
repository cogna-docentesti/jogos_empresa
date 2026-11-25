using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    [Serializable]
    public sealed class MonthlyEventReservation
    {
        public string EventId, StaffId, UnitId;
        public int Day;
        public bool Announced;
    }

    /// <summary>Private, frozen draw. Only announced or already revealed entries belong in calendar projections.</summary>
    [Serializable]
    public sealed class MonthlyEventPlan
    {
        public int TargetCount;
        public List<MonthlyEventReservation> Events = new List<MonthlyEventReservation>();
    }

    public sealed class MonthlyEventCandidate
    {
        public EventDefinition Definition;
        public double Weight;
        public string StaffId, UnitId;
    }

    public static class MonthlyEventPlanner
    {
        // The old catalog and the systemic catalog both contain the same "Funcionário ausente" incident.
        public static string CanonicalId(string eventId) => eventId == "insufficient_team" ? EventIds.StaffAbsent : eventId;

        public static MonthlyEventPlan Draw(SimulationCatalog catalog, int month, DeterministicRandom rng,
            IEnumerable<MonthlyEventCandidate> eligible, IEnumerable<string> occurred, bool reserveLoanDecision = false)
        {
            var excluded = new HashSet<string>(occurred.Select(CanonicalId), StringComparer.Ordinal);
            var candidates = eligible.Where(c => c.Definition != null && !excluded.Contains(CanonicalId(c.Definition.Id))
                    && c.Weight > 0 && !double.IsNaN(c.Weight) && !double.IsInfinity(c.Weight))
                .GroupBy(c => CanonicalId(c.Definition.Id), StringComparer.Ordinal).Select(g => g.First())
                .OrderBy(c => c.Definition.Id, StringComparer.Ordinal).ToList();
            var result = new MonthlyEventPlan { TargetCount = rng.Uniform(month, 0, "monthly-event-count") < .5 ? 4 : 5 };
            if (reserveLoanDecision) result.TargetCount = 4; // leave room for a real cash-shortfall decision on the due date
            // Different dates keep one decision per day; dates and selection use independent random keys.
            var dates = Enumerable.Range(1, catalog.Constants.DaysPerMonth)
                .OrderBy(day => rng.Uniform(month, day, "monthly-event-date")).ToList();
            while (result.Events.Count < result.TargetCount && candidates.Count > 0)
            {
                int index = result.Events.Count;
                double cursor = rng.Uniform(month, 0, "monthly-event-selection", index) * candidates.Sum(c => c.Weight);
                var selected = candidates.Last();
                foreach (var candidate in candidates)
                {
                    if (cursor < candidate.Weight) { selected = candidate; break; }
                    cursor -= candidate.Weight;
                }
                int firstDay = selected.Definition.IsNegative && month == 1 ? catalog.Constants.GraceDaysWithoutNegative + 1 : 1;
                int day = dates.FirstOrDefault(d => d >= firstDay);
                if (day == 0) break;
                dates.Remove(day);
                result.Events.Add(new MonthlyEventReservation
                {
                    EventId = selected.Definition.Id, Day = day, Announced = selected.Definition.Announced,
                    StaffId = selected.StaffId, UnitId = selected.UnitId
                });
                candidates.RemoveAll(c => c.Definition.Id == selected.Definition.Id
                    || selected.Definition.Group != null && c.Definition.Group == selected.Definition.Group);
            }
            result.Events = result.Events.OrderBy(e => e.Day).ToList();
            return result;
        }
    }
}
