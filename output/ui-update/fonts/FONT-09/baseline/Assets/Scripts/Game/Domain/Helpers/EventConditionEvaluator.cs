using System;
using System.Collections.Generic;

/// <summary>Pure condition evaluation. Diagnostics are returned for the caller to record.</summary>
public static class EventConditionEvaluator
{
    private const double AbsoluteEqualityTolerance = 0.00001d;
    private const double RelativeEqualityTolerance = 0.000001d;

    /// <summary>
    /// False means unmet or unavailable. A non-null diagnostic identifies invalid or unavailable data;
    /// a normal comparison mismatch has no diagnostic. No inputs are modified.
    /// </summary>
    public static bool Evaluate(
        EventConditionData condition,
        GameSessionEntity session,
        RestaurantData restaurant,
        IReadOnlyList<EquipmentData> catalog,
        out string diagnostic)
    {
        diagnostic = null;
        if (condition == null)
            return Fail("Condition is missing.", out diagnostic);

        switch (condition.type)
        {
            // Serialized compatibility values: SANITARY_RISK_SCORE = 2, STOCK_COVERAGE_RATIO = 4.
            case (EventConditionType)2:
            case (EventConditionType)4:
                return Fail("Deprecated indicator cannot be evaluated: " + condition.type + ".", out diagnostic);
            case EventConditionType.EQUIPMENT_QUALITY_SCORE:
            case EventConditionType.TEAM_COVERAGE_RATIO:
            case EventConditionType.REPUTATION_SCORE:
                break;
            default:
                return Fail("Unknown indicator: " + condition.type + ".", out diagnostic);
        }

        if (session == null)
            return Fail("Session is unavailable.", out diagnostic);
        if (!IsFinite(condition.value))
            return Fail("Condition threshold must be finite.", out diagnostic);

        float indicator;
        try
        {
            switch (condition.type)
            {
                case EventConditionType.EQUIPMENT_QUALITY_SCORE:
                    if (!EventIndicatorCalculator.TryCalculateEquipmentQuality(
                        EquipmentSelectionHelper.FromJson(session.equipmentJson), catalog, out indicator))
                        return Fail("EQUIPMENT_QUALITY_SCORE is unavailable.", out diagnostic);
                    break;
                case EventConditionType.TEAM_COVERAGE_RATIO:
                    if (!EventIndicatorCalculator.TryCalculateTeamCoverage(
                        TeamSelectionHelper.FromJson(session.teamJson), restaurant, out indicator))
                        return Fail("TEAM_COVERAGE_RATIO is unavailable.", out diagnostic);
                    break;
                default:
                    indicator = session.reputationScore;
                    break;
            }
        }
        catch (ArgumentException)
        {
            return Fail("Invalid selection JSON for " + condition.type + ".", out diagnostic);
        }

        if (!IsFinite(indicator))
            return Fail("Indicator must be finite: " + condition.type + ".", out diagnostic);

        double difference = Math.Abs((double)indicator - condition.value);
        double scale = Math.Max(Math.Abs((double)indicator), Math.Abs((double)condition.value));
        bool approximatelyEqual = difference <= Math.Max(AbsoluteEqualityTolerance, RelativeEqualityTolerance * scale);

        switch (condition.comparison)
        {
            case EventConditionOperator.EQUALS: return approximatelyEqual;
            case EventConditionOperator.NOT_EQUALS: return !approximatelyEqual;
            case EventConditionOperator.GREATER_THAN: return indicator > condition.value;
            case EventConditionOperator.GREATER_OR_EQUAL: return indicator >= condition.value;
            case EventConditionOperator.LESS_THAN: return indicator < condition.value;
            case EventConditionOperator.LESS_OR_EQUAL: return indicator <= condition.value;
            default: return Fail("Unknown comparison operator: " + condition.comparison + ".", out diagnostic);
        }
    }

    /// <summary>AND, with short circuit. An empty set is true; a missing set is unavailable.</summary>
    public static bool EvaluateAll(
        IReadOnlyList<EventConditionData> conditions,
        GameSessionEntity session,
        RestaurantData restaurant,
        IReadOnlyList<EquipmentData> catalog,
        out string diagnostic)
    {
        diagnostic = null;
        if (conditions == null)
            return Fail("Condition collection is missing.", out diagnostic);

        for (int i = 0; i < conditions.Count; i++)
        {
            if (!Evaluate(conditions[i], session, restaurant, catalog, out diagnostic))
            {
                if (diagnostic != null)
                    diagnostic = "conditions[" + i + "]: " + diagnostic;
                return false;
            }
        }
        return true;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool Fail(string message, out string diagnostic)
    {
        diagnostic = message;
        return false;
    }
}
