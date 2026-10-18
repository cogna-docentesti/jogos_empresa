using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Standalone evaluator checks; does not load assets or change saved sessions.</summary>
public static class EventConditionEvaluatorDebugTest
{
    public static void RunTests()
    {
        EventIndicatorCalculatorDebugTest.RunTests();
        int passed = 0;
        Action<bool, string> check = (result, name) =>
        {
            if (!result) throw new InvalidOperationException("[EventConditions] FAIL: " + name);
            passed++;
            Debug.Log("[EventConditions] PASS: " + name);
        };

        var equipment = ScriptableObject.CreateInstance<EquipmentData>();
        var restaurant = ScriptableObject.CreateInstance<RestaurantData>();
        try
        {
            equipment.id = "equipment";
            equipment.qualityBonus = 0.5f;
            var catalog = new[] { equipment };
            restaurant.requiredRoles = new[] { new RoleRequirement { roleId = "cook", quantity = 2 } };
            var session = new GameSessionEntity
            {
                reputationScore = 50,
                equipmentJson = EquipmentSelectionHelper.ToJson(new EquipmentSelectionData { equipmentIds = new List<string> { "equipment" } }),
                teamJson = TeamSelectionHelper.ToJson(new TeamSelectionData { members = new List<TeamSelectionItem> { new TeamSelectionItem { roleId = "cook", quantity = 1 } } })
            };
            string diagnostic;
            var operators = new[]
            {
                EventConditionOperator.EQUALS, EventConditionOperator.NOT_EQUALS,
                EventConditionOperator.GREATER_THAN, EventConditionOperator.GREATER_OR_EQUAL,
                EventConditionOperator.LESS_THAN, EventConditionOperator.LESS_OR_EQUAL
            };
            bool[] below = { false, true, true, true, false, false };
            bool[] equal = { true, false, false, true, false, true };
            bool[] above = { false, true, false, false, true, true };
            for (int i = 0; i < operators.Length; i++)
            {
                var condition = Condition(EventConditionType.REPUTATION_SCORE, operators[i], 49f);
                check(EventConditionEvaluator.Evaluate(condition, session, null, null, out diagnostic) == below[i] && diagnostic == null, operators[i] + " threshold below reputation");
                condition.value = 50f;
                check(EventConditionEvaluator.Evaluate(condition, session, null, null, out diagnostic) == equal[i] && diagnostic == null, operators[i] + " equal reputation");
                condition.value = 51f;
                check(EventConditionEvaluator.Evaluate(condition, session, null, null, out diagnostic) == above[i] && diagnostic == null, operators[i] + " threshold above reputation");
            }

            var quality = Condition(EventConditionType.EQUIPMENT_QUALITY_SCORE, EventConditionOperator.EQUALS, 0.5f);
            check(EventConditionEvaluator.Evaluate(quality, session, null, catalog, out diagnostic), "equipment quality from session JSON");
            quality.value = 0.500005f;
            check(EventConditionEvaluator.Evaluate(quality, session, null, catalog, out diagnostic), "near floats equal");
            quality.comparison = EventConditionOperator.NOT_EQUALS;
            check(!EventConditionEvaluator.Evaluate(quality, session, null, catalog, out diagnostic), "near floats not unequal");
            quality.value = 0.501f;
            check(EventConditionEvaluator.Evaluate(quality, session, null, catalog, out diagnostic), "separated floats unequal");
            quality.comparison = EventConditionOperator.EQUALS;
            check(!EventConditionEvaluator.Evaluate(quality, session, null, catalog, out diagnostic), "separated floats not equal");
            quality.comparison = EventConditionOperator.LESS_THAN;
            quality.value = 0.500005f;
            check(EventConditionEvaluator.Evaluate(quality, session, null, catalog, out diagnostic), "strict ordering does not use equality tolerance");

            var coverage = Condition(EventConditionType.TEAM_COVERAGE_RATIO, EventConditionOperator.LESS_THAN, 1f);
            check(EventConditionEvaluator.Evaluate(coverage, session, restaurant, null, out diagnostic), "partial team is below one");
            session.teamJson = TeamSelectionHelper.ToJson(new TeamSelectionData { members = new List<TeamSelectionItem> { new TeamSelectionItem { roleId = "cook", quantity = 2 } } });
            check(!EventConditionEvaluator.Evaluate(coverage, session, restaurant, null, out diagnostic) && diagnostic == null, "complete team is not insufficient");
            session.teamJson = "";
            check(EventConditionEvaluator.Evaluate(coverage, session, restaurant, null, out diagnostic), "empty team with valid requirements");
            var reputation = Condition(EventConditionType.REPUTATION_SCORE, EventConditionOperator.LESS_OR_EQUAL, 50f);
            quality.value = 1f;
            check(EventConditionEvaluator.EvaluateAll(new[] { quality, coverage, reputation }, session, restaurant, catalog, out diagnostic), "AND all satisfied");
            reputation.value = 49f;
            check(!EventConditionEvaluator.EvaluateAll(new[] { quality, coverage, reputation }, session, restaurant, catalog, out diagnostic) && diagnostic == null, "AND one mismatch");
            reputation.value = 50f;
            check(EventConditionEvaluator.EvaluateAll(Array.Empty<EventConditionData>(), null, null, null, out diagnostic) && diagnostic == null, "empty array has no restrictions");
            check(!EventConditionEvaluator.EvaluateAll(null, session, restaurant, catalog, out diagnostic) && diagnostic != null, "missing collection unavailable");
            check(!EventConditionEvaluator.EvaluateAll(new EventConditionData[] { reputation, null }, session, restaurant, catalog, out diagnostic) && diagnostic.Contains("conditions[1]"), "AND invalid entry diagnostic has index");

            check(!EventConditionEvaluator.Evaluate(quality, session, null, null, out diagnostic) && diagnostic != null, "missing equipment catalog unavailable");
            check(!EventConditionEvaluator.Evaluate(coverage, session, null, null, out diagnostic) && diagnostic != null, "missing restaurant unavailable");
            restaurant.requiredRoles = Array.Empty<RoleRequirement>();
            check(!EventConditionEvaluator.Evaluate(coverage, session, restaurant, null, out diagnostic) && diagnostic != null, "empty requirements unavailable");
            check(!EventConditionEvaluator.Evaluate(reputation, null, null, null, out diagnostic) && diagnostic != null, "missing session unavailable");
            check(!EventConditionEvaluator.Evaluate(null, session, null, null, out diagnostic) && diagnostic != null, "missing condition unavailable");
            foreach (int serializedType in new[] { 2, 4 })
                check(!EventConditionEvaluator.Evaluate(Condition((EventConditionType)serializedType, EventConditionOperator.NOT_EQUALS, 999f), session, null, null, out diagnostic) && diagnostic.Contains("Deprecated"), "deprecated type " + serializedType);
            check(!EventConditionEvaluator.Evaluate(Condition((EventConditionType)999, EventConditionOperator.EQUALS, 0f), session, null, null, out diagnostic) && diagnostic != null, "unknown type");
            var invalid = Condition(EventConditionType.REPUTATION_SCORE, (EventConditionOperator)999, 50f);
            check(!EventConditionEvaluator.Evaluate(invalid, session, null, null, out diagnostic) && diagnostic != null, "unknown operator");
            foreach (float threshold in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                check(!EventConditionEvaluator.Evaluate(Condition(EventConditionType.REPUTATION_SCORE, EventConditionOperator.NOT_EQUALS, threshold), session, null, null, out diagnostic) && diagnostic != null, "nonfinite threshold " + threshold);

            session.equipmentJson = "{invalid";
            check(!EventConditionEvaluator.Evaluate(quality, session, null, catalog, out diagnostic) && diagnostic != null, "malformed equipment JSON does not throw");
            restaurant.requiredRoles = new[] { new RoleRequirement { roleId = "cook", quantity = 2 } };
            session.teamJson = "{invalid";
            check(!EventConditionEvaluator.Evaluate(coverage, session, restaurant, null, out diagnostic) && diagnostic != null, "malformed team JSON does not throw");
            session.equipmentJson = EquipmentSelectionHelper.ToJson(new EquipmentSelectionData { equipmentIds = new List<string> { "missing" } });
            quality.comparison = EventConditionOperator.NOT_EQUALS;
            check(!EventConditionEvaluator.Evaluate(quality, session, restaurant, catalog, out diagnostic) && diagnostic != null, "unavailable indicator never satisfies NOT_EQUALS");
            session.equipmentJson = "";
            quality.comparison = EventConditionOperator.EQUALS;
            quality.value = 0f;
            check(EventConditionEvaluator.Evaluate(quality, session, null, null, out diagnostic), "empty equipment is valid zero");
            string beforeEquipment = session.equipmentJson;
            string beforeTeam = session.teamJson;
            int beforeReputation = session.reputationScore;
            EventConditionEvaluator.Evaluate(reputation, session, restaurant, catalog, out diagnostic);
            check(session.equipmentJson == beforeEquipment && session.teamJson == beforeTeam && session.reputationScore == beforeReputation && equipment.qualityBonus == 0.5f && reputation.value == 50f, "inputs unchanged");
            Debug.Log($"[EventConditions] SUCCESS: {passed} checks passed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(equipment);
            UnityEngine.Object.DestroyImmediate(restaurant);
        }
    }

    private static EventConditionData Condition(EventConditionType type, EventConditionOperator comparison, float value)
        => new EventConditionData { type = type, comparison = comparison, value = value };
}
