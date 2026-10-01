using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Read-only monthly selection. History may be supplied as IDs or loaded once through a callback.</summary>
public sealed class MonthlyEventSelector
{
    private readonly Func<double> nextRandom;
    private readonly Func<string, IEnumerable<string>> loadOccurredEventIds;

    public MonthlyEventSelector(Func<double> nextRandom = null,
        Func<string, IEnumerable<string>> loadOccurredEventIds = null)
    {
        this.nextRandom = nextRandom ?? new Random().NextDouble;
        this.loadOccurredEventIds = loadOccurredEventIds;
    }

    /// <summary>
    /// Returns newly selected events only. Existing monthly selections count towards target/max.
    /// The caller must reserve returned IDs and carry forward monthly groups and counts.
    /// </summary>
    public MonthlyEventSelectionResult SelectEventsForMonth(MonthlyEventSelectionContext context,
        int targetCount = 4, int minCount = 3, int maxCount = 5)
    {
        var diagnostics = new List<string>();
        var selected = new List<EventData>();
        if (context == null || context.Session == null || string.IsNullOrWhiteSpace(context.Session.sessionId))
            return Invalid("Session/context is unavailable.", diagnostics);
        if (context.Month < 1 || context.Month > 3)
            return Invalid("Month must be between 1 and 3.", diagnostics);
        if (minCount < 0 || maxCount > 5 || minCount > targetCount || targetCount > maxCount ||
            context.AlreadySelectedCountInMonth < 0 || context.AlreadySelectedCountInMonth > maxCount)
            return Invalid("Invalid monthly count limits or existing selection count.", diagnostics);
        if (context.EventCatalog == null)
            return Invalid("Event catalog is unavailable.", diagnostics);

        var occurred = IdSet(context.OccurredEventIds);
        if (loadOccurredEventIds != null)
        {
            try
            {
                var loaded = loadOccurredEventIds(context.Session.sessionId);
                if (loaded == null)
                    return Invalid("Session history is unavailable.", diagnostics);
                occurred.UnionWith(IdSet(loaded));
            }
            catch (Exception error)
            {
                return Invalid("Session history could not be read: " + error.GetType().Name + ".", diagnostics);
            }
        }
        var reserved = IdSet(context.ReservedEventIds);
        var groups = new HashSet<EventExclusionGroup>();
        foreach (var group in context.UsedGroupsInMonth ?? Array.Empty<EventExclusionGroup>())
        {
            if (!Enum.IsDefined(typeof(EventExclusionGroup), group))
                return Invalid("Unknown previously used exclusion group.", diagnostics);
            if (group != EventExclusionGroup.NONE)
                groups.Add(group);
        }

        var duplicates = new HashSet<string>(context.EventCatalog.Where(e => e != null && !string.IsNullOrWhiteSpace(e.id))
            .GroupBy(e => e.id, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key), StringComparer.Ordinal);
        var candidates = new List<EventData>();
        foreach (var e in context.EventCatalog)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.id))
            {
                diagnostics.Add("Event with missing data/ID excluded.");
                continue;
            }
            if (duplicates.Contains(e.id) || float.IsNaN(e.baseWeight) || float.IsInfinity(e.baseWeight) || e.baseWeight <= 0f ||
                !Enum.IsDefined(typeof(EventExclusionGroup), e.exclusionGroup) ||
                !Enum.IsDefined(typeof(EventTriggerType), e.triggerType) ||
                e.minRound < 1 || e.maxRound > 3 || e.minRound > e.maxRound)
            {
                diagnostics.Add(e.id + ": invalid event definition excluded.");
                continue;
            }
            if (context.Month < e.minRound || context.Month > e.maxRound || reserved.Contains(e.id) ||
                (!e.canRepeat && occurred.Contains(e.id)) || groups.Contains(e.exclusionGroup))
                continue;
            if (e.applicableRestaurantTypes != null && e.applicableRestaurantTypes.Length > 0 &&
                Array.IndexOf(e.applicableRestaurantTypes, context.Session.restaurantType) < 0)
                continue;
            if (e.triggerType == EventTriggerType.CONDITIONAL)
            {
                if (e.conditions == null || e.conditions.Length == 0)
                {
                    diagnostics.Add(e.id + ": conditional event has no conditions.");
                    continue;
                }
                if (!EventConditionEvaluator.EvaluateAll(e.conditions, context.Session, context.Restaurant,
                    context.EquipmentCatalog, out string conditionDiagnostic))
                {
                    if (conditionDiagnostic != null)
                        diagnostics.Add(e.id + ": " + conditionDiagnostic);
                    continue;
                }
            }
            candidates.Add(e);
        }
        candidates.Sort((a, b) => StringComparer.Ordinal.Compare(a.id, b.id));

        int remaining = Math.Max(0, targetCount - context.AlreadySelectedCountInMonth);
        while (selected.Count < remaining && candidates.Count > 0)
        {
            double random;
            try { random = nextRandom(); }
            catch (Exception error) { return Invalid("Random source failed: " + error.GetType().Name + ".", diagnostics); }
            if (double.IsNaN(random) || double.IsInfinity(random) || random < 0d || random >= 1d)
                return Invalid("Random source must return a finite value in [0, 1).", diagnostics);

            double totalWeight = candidates.Sum(e => (double)e.baseWeight);
            double cursor = random * totalWeight;
            var chosen = candidates[candidates.Count - 1];
            foreach (var candidate in candidates)
            {
                if (cursor < candidate.baseWeight) { chosen = candidate; break; }
                cursor -= candidate.baseWeight;
            }
            selected.Add(chosen);
            // Remove the chosen ID even for canRepeat=true: no duplicates within this selection.
            candidates.RemoveAll(e => e.id == chosen.id ||
                (chosen.exclusionGroup != EventExclusionGroup.NONE && e.exclusionGroup == chosen.exclusionGroup));
        }

        int total = context.AlreadySelectedCountInMonth + selected.Count;
        if (total < targetCount)
            diagnostics.Add("Only " + total + " event(s) possible this month; target is " + targetCount + ".");
        bool minimumReached = total >= minCount;
        if (!minimumReached)
            diagnostics.Add("Monthly minimum of " + minCount + " could not be reached; no invalid events were forced.");
        return new MonthlyEventSelectionResult(true, selected, diagnostics, minimumReached);
    }

    private static HashSet<string> IdSet(IEnumerable<string> ids)
        => new HashSet<string>((ids ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)), StringComparer.Ordinal);

    private static MonthlyEventSelectionResult Invalid(string message, List<string> diagnostics)
    {
        diagnostics.Add(message);
        return new MonthlyEventSelectionResult(false, new List<EventData>(), diagnostics, false);
    }
}
