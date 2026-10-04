using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MonthlyEventSelectorDebugTest
{
    public static void ValidateCatalog(IReadOnlyList<EventData> events)
    {
        var expected = new Dictionary<EventExclusionGroup, string[]>
        {
            [EventExclusionGroup.SOCIAL_REPUTATION] = new[] { "negative_review", "positive_online_review", "influencer_mention" },
            [EventExclusionGroup.SUPPLIER] = new[] { "ingredient_price_increase", "supplier_delay", "supplier_discount", "bulk_purchase_opportunity" },
            [EventExclusionGroup.COMPETITION] = new[] { "new_competitor", "competitor_temporary_closure" },
            [EventExclusionGroup.DEMAND_SPIKE] = new[] { "extra_local_demand", "lunch_demand_peak", "local_festival", "corporate_order" },
            [EventExclusionGroup.NONE] = new[] { "freezer_breakdown", "insufficient_team", "health_inspection", "excessive_food_waste", "heavy_rain", "payment_system_failure", "team_productivity_boost" }
        };
        if (events == null || events.Count != 20 || events.Any(e => e == null) || events.Select(e => e.id).Distinct().Count() != 20 ||
            events.Count(e => e.triggerType == EventTriggerType.RANDOM) != 17 ||
            events.Count(e => e.triggerType == EventTriggerType.CONDITIONAL && e.polarity == EventPolarity.NEGATIVE) != 3)
            throw new InvalidOperationException("Invalid 20-event catalog/distribution.");
        foreach (var group in expected)
            foreach (string id in group.Value)
            {
                var e = events.Single(item => item.id == id);
                if (e.exclusionGroup != group.Key || e.canRepeat || e.baseWeight <= 0 || float.IsNaN(e.baseWeight) || float.IsInfinity(e.baseWeight) ||
                    e.minRound != 1 || e.maxRound != 3 || e.options == null || e.options.Length != 3 ||
                    (e.triggerType == EventTriggerType.RANDOM && e.conditions != null && e.conditions.Length != 0) ||
                    (e.triggerType == EventTriggerType.CONDITIONAL && (e.conditions == null || e.conditions.Length == 0 || e.conditions.Any(condition => condition == null || (int)condition.type == 2 || (int)condition.type == 4))))
                    throw new InvalidOperationException("Invalid catalog event: " + id);
            }
        Debug.Log("[MonthlyEvents] CATALOG PASS: 20 valid assets, exact groups, 17 RANDOM / 3 CONDITIONAL NEGATIVE.");
    }

    public static void RunTests()
    {
        var assets = new List<ScriptableObject>();
        int passed = 0;
        Action<bool, string> check = (valid, name) =>
        {
            if (!valid) throw new InvalidOperationException("[MonthlyEvents] FAIL: " + name);
            passed++;
            Debug.Log("[MonthlyEvents] PASS: " + name);
        };
        Func<string, EventData> make = id =>
        {
            var e = ScriptableObject.CreateInstance<EventData>();
            e.id = id;
            e.triggerType = EventTriggerType.RANDOM;
            e.minRound = 1; e.maxRound = 3; e.baseWeight = 1f;
            assets.Add(e);
            return e;
        };
        var session = new GameSessionEntity { sessionId = "monthly_test", currentRound = 1, reputationScore = 50 };
        Func<EventData[], MonthlyEventSelectionContext> context = events => new MonthlyEventSelectionContext
        {
            Month = 1, Session = session, EventCatalog = events
        };
        var selector = new MonthlyEventSelector(() => 0d);
        try
        {
            var one = make("one");
            var c = context(new[] { one });
            check(selector.SelectEventsForMonth(c).Events.Count == 1, "inside month window");
            one.minRound = 2;
            check(selector.SelectEventsForMonth(c).Events.Count == 0, "before minRound excluded");
            c.Month = 3; one.maxRound = 2;
            check(selector.SelectEventsForMonth(c).Events.Count == 0, "after maxRound excluded");
            one.minRound = 1; one.maxRound = 3;
            foreach (int month in new[] { 0, 4, 12 })
            {
                c.Month = month;
                var invalid = selector.SelectEventsForMonth(c);
                check(!invalid.IsValid && invalid.Events.Count == 0 && invalid.Diagnostics.Count > 0, "invalid month " + month);
            }
            c.Month = 1;
            one.conditions = new[] { new EventConditionData { type = (EventConditionType)2 } };
            check(selector.SelectEventsForMonth(c).Events.Count == 1, "RANDOM ignores conditions");
            one.triggerType = EventTriggerType.CONDITIONAL;
            one.conditions = new[] { new EventConditionData { type = EventConditionType.REPUTATION_SCORE, comparison = EventConditionOperator.LESS_OR_EQUAL, value = 50 } };
            check(selector.SelectEventsForMonth(c).Events.Count == 1, "true CONDITIONAL eligible");
            one.conditions[0].value = 49;
            check(selector.SelectEventsForMonth(c).Events.Count == 0, "false CONDITIONAL excluded");
            one.conditions[0].type = EventConditionType.TEAM_COVERAGE_RATIO;
            check(selector.SelectEventsForMonth(c).Events.Count == 0, "unavailable indicator excluded");
            one.conditions[0].type = (EventConditionType)4;
            check(selector.SelectEventsForMonth(c).Events.Count == 0, "deprecated condition excluded");
            one.conditions = Array.Empty<EventConditionData>();
            check(selector.SelectEventsForMonth(c).Events.Count == 0, "conditional with no conditions excluded");
            one.triggerType = EventTriggerType.RANDOM;
            one.conditions = Array.Empty<EventConditionData>();
            c.OccurredEventIds = new[] { one.id };
            check(selector.SelectEventsForMonth(c).Events.Count == 0, "nonrepeat occurred event excluded");
            one.canRepeat = true;
            check(selector.SelectEventsForMonth(c).Events.Count == 1, "repeatable occurred event allowed");
            c.ReservedEventIds = new[] { one.id };
            check(selector.SelectEventsForMonth(c).Events.Count == 0, "reserved ID excluded even if repeatable");
            c.OccurredEventIds = Array.Empty<string>(); c.ReservedEventIds = Array.Empty<string>();
            one.canRepeat = false;
            one.applicableRestaurantTypes = new[] { RestaurantType.JAPONES };
            check(selector.SelectEventsForMonth(c).Events.Count == 0, "incompatible restaurant excluded");
            one.applicableRestaurantTypes = Array.Empty<RestaurantType>();

            var socialA = make("social_a"); var socialB = make("social_b");
            socialA.exclusionGroup = socialB.exclusionGroup = EventExclusionGroup.SOCIAL_REPUTATION;
            var supplierA = make("supplier_a"); var supplierB = make("supplier_b");
            supplierA.exclusionGroup = supplierB.exclusionGroup = EventExclusionGroup.SUPPLIER;
            var noneA = make("none_a"); var noneB = make("none_b");
            c = context(new[] { socialA, socialB, supplierA, supplierB, noneA, noneB });
            var result = selector.SelectEventsForMonth(c);
            check(result.Events.Count == 4, "target four after group filtering");
            check(result.Events.Count(e => e.exclusionGroup == EventExclusionGroup.SOCIAL_REPUTATION) == 1, "one SOCIAL_REPUTATION");
            check(result.Events.Count(e => e.exclusionGroup == EventExclusionGroup.SUPPLIER) == 1, "one SUPPLIER");
            check(result.Events.Count(e => e.exclusionGroup == EventExclusionGroup.NONE) == 2, "multiple NONE allowed");
            c.UsedGroupsInMonth = new[] { EventExclusionGroup.SOCIAL_REPUTATION, EventExclusionGroup.NONE };
            result = selector.SelectEventsForMonth(c);
            check(result.Events.All(e => e.exclusionGroup != EventExclusionGroup.SOCIAL_REPUTATION) && result.Events.Count == 3, "previously used group blocked; NONE unrestricted");
            var competitionA = make("competition_a"); var competitionB = make("competition_b");
            competitionA.exclusionGroup = competitionB.exclusionGroup = EventExclusionGroup.COMPETITION;
            var demandA = make("demand_a"); var demandB = make("demand_b");
            demandA.exclusionGroup = demandB.exclusionGroup = EventExclusionGroup.DEMAND_SPIKE;
            result = selector.SelectEventsForMonth(context(new[] { competitionA, competitionB, demandA, demandB }));
            check(result.Events.Count == 2, "competition and demand groups exclusive");

            var low = make("a_low"); var high = make("b_high"); high.baseWeight = 9f;
            c = context(new[] { low, high });
            check(new MonthlyEventSelector(() => 0.099d).SelectEventsForMonth(c, 1, 0, 5).Events.Single() == low, "weighted lower interval");
            check(new MonthlyEventSelector(() => 0.1d).SelectEventsForMonth(c, 1, 0, 5).Events.Single() == high, "weighted boundary enters higher interval");
            int higherCount = 0;
            for (int i = 0; i < 1000; i++)
            {
                double draw = (i + 0.5d) / 1000d;
                if (new MonthlyEventSelector(() => draw).SelectEventsForMonth(c, 1, 0, 5).Events.Single() == high) higherCount++;
            }
            check(higherCount == 900, "weight nine receives nine tenths of deterministic probability intervals");
            var ordered = new MonthlyEventSelector(() => 0.1d).SelectEventsForMonth(c).Events.Select(e => e.id).ToArray();
            check(ordered.SequenceEqual(new[] { "b_high", "a_low" }), "weights recalculated without replacement");
            c.EventCatalog = new[] { high, low };
            check(ordered.SequenceEqual(new MonthlyEventSelector(() => 0.1d).SelectEventsForMonth(c).Events.Select(e => e.id)), "reproducible regardless of catalog order");
            foreach (float weight in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                low.baseWeight = weight;
                check(selector.SelectEventsForMonth(c).Events.SequenceEqual(new[] { high }), "invalid weight excluded " + weight);
            }
            low.baseWeight = 1f;

            var many = Enumerable.Range(0, 12).Select(i => make("event_" + i.ToString("D2"))).ToArray();
            c = context(many);
            result = selector.SelectEventsForMonth(c);
            check(result.IsValid && result.MinimumCountReached && result.Events.Count == 4, "default target four");
            check(result.Events.Select(e => e.id).Distinct().Count() == 4, "no duplicate event");
            check(selector.SelectEventsForMonth(context(many.Take(3).ToArray())).Events.Count == 3, "three candidates returned");
            result = selector.SelectEventsForMonth(context(many.Take(2).ToArray()));
            check(result.IsValid && !result.MinimumCountReached && result.Events.Count == 2 && result.Diagnostics.Count > 0, "below minimum returns available with diagnostic");
            check(selector.SelectEventsForMonth(context(Array.Empty<EventData>())).Events.Count == 0, "no candidates returns empty");
            check(selector.SelectEventsForMonth(c, 5, 3, 5).Events.Count == 5, "explicit target five");
            check(!selector.SelectEventsForMonth(c, 6, 3, 6).IsValid, "maximum above five rejected");
            check(!selector.SelectEventsForMonth(c, 2, 3, 5).IsValid, "target below minimum rejected");
            check(selector.SelectEventsForMonth(context(new[] { one, one })).Events.Count == 0, "duplicate catalog IDs excluded");
            one.exclusionGroup = (EventExclusionGroup)999;
            check(selector.SelectEventsForMonth(context(new[] { one })).Events.Count == 0, "unknown group excluded");
            one.exclusionGroup = EventExclusionGroup.NONE;

            var monthOne = selector.SelectEventsForMonth(c).Events.Select(e => e.id).ToArray();
            c.Month = 2; c.OccurredEventIds = monthOne.Take(2); c.ReservedEventIds = monthOne.Skip(2);
            var monthTwo = selector.SelectEventsForMonth(c).Events.Select(e => e.id).ToArray();
            check(monthTwo.Length == 4 && !monthTwo.Intersect(monthOne).Any(), "month two respects month one history and reservations");
            c.Month = 3; c.OccurredEventIds = monthOne; c.ReservedEventIds = monthTwo;
            var monthThree = selector.SelectEventsForMonth(c).Events.Select(e => e.id).ToArray();
            check(monthThree.Length == 4 && !monthThree.Intersect(monthOne.Concat(monthTwo)).Any(), "month three respects entire session");
            c = context(many); c.ReservedEventIds = monthOne; c.AlreadySelectedCountInMonth = 3;
            check(selector.SelectEventsForMonth(c).Events.Count == 1, "top-up counts previous selections toward target");
            c.AlreadySelectedCountInMonth = 4;
            check(selector.SelectEventsForMonth(c).Events.Count == 0, "full month not selected again");
            c.AlreadySelectedCountInMonth = 5;
            check(selector.SelectEventsForMonth(c, 5).Events.Count == 0, "five already selected never exceeds cap");

            int historyReads = 0;
            var withHistory = new MonthlyEventSelector(() => 0d, id =>
            {
                historyReads++;
                if (id != session.sessionId) throw new Exception("Wrong session");
                return monthOne;
            });
            c = context(many);
            result = withHistory.SelectEventsForMonth(c);
            check(historyReads == 1 && !result.Events.Any(e => monthOne.Contains(e.id)), "history loaded once for the correct session");
            c.Month = 4;
            check(!withHistory.SelectEventsForMonth(c).IsValid && historyReads == 1, "invalid month does not read history");
            c.Month = 1;
            check(!new MonthlyEventSelector(() => 0d, id => throw new Exception()).SelectEventsForMonth(c).IsValid, "history read failure returns diagnostic");
            foreach (double draw in new[] { -1d, 1d, double.NaN, double.PositiveInfinity })
                check(!new MonthlyEventSelector(() => draw).SelectEventsForMonth(c).IsValid, "invalid random source " + draw);
            check(session.currentRound == 1 && session.reputationScore == 50 && !c.ReservedEventIds.Any() && c.AlreadySelectedCountInMonth == 0, "no mutation or automatic reservation/history");
            Debug.Log($"[MonthlyEvents] SUCCESS: {passed} checks passed.");
        }
        finally
        {
            foreach (var asset in assets) UnityEngine.Object.DestroyImmediate(asset);
        }
    }
}
