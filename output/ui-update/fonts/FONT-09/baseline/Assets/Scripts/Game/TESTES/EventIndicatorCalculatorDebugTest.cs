using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Standalone checks: no database, scene, session or resource catalog required.</summary>
public static class EventIndicatorCalculatorDebugTest
{
    public static void RunTests()
    {
        var created = new List<ScriptableObject>();
        int passed = 0;
        Action<bool, string> check = (condition, name) =>
        {
            if (!condition)
                throw new InvalidOperationException("[EventIndicators] FAIL: " + name);
            passed++;
            Debug.Log("[EventIndicators] PASS: " + name);
        };

        try
        {
            var basic = Equipment("basic", 0.75f, created);
            var extra = Equipment("extra", 0.5f, created);
            extra.category = EquipmentCategory.SPECIFIC;
            var catalog = new[] { basic, extra };
            float value;
            check(EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned(), catalog, out value) && value == 0f, "empty equipment");
            check(EventIndicatorCalculator.TryCalculateEquipmentQuality(null, null, out value) && value == 0f, "absent selection is empty");
            check(EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned("", " ", null), null, out value) && value == 0f, "blank equipment IDs ignored");
            check(EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned("extra", "basic"), catalog, out value) && value == 1.25f, "sum basic and extra without normalization or cap");
            check(EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned("basic", "basic", "extra"), catalog, out value) && value == 1.25f, "selection duplicates counted once");
            check(EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned("basic", "extra"), new[] { extra, basic }, out value) && value == 1.25f, "order independent");
            check(!EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned("basic", "missing"), catalog, out value) && value == 0f, "unknown ID leaves output zero");
            check(!EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned("BASIC"), catalog, out value) && value == 0f, "exact case-sensitive IDs");
            check(!EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned("basic"), null, out value) && value == 0f, "missing catalog");
            check(!EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned("basic"), new[] { basic, basic }, out value), "duplicate catalog ID");
            var unrelated = Equipment("unused", 0.1f, created);
            check(!EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned("basic"), new[] { basic, unrelated, unrelated }, out value), "duplicate unowned catalog ID");
            check(!EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned("basic"), new EquipmentData[] { basic, null }, out value), "null catalog entry");
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -0.1f, 1.1f })
            {
                extra.qualityBonus = invalid;
                check(!EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned("basic", "extra"), catalog, out value) && value == 0f, "invalid quality " + invalid);
            }
            extra.qualityBonus = 1f;
            basic.qualityBonus = 0f;
            check(EventIndicatorCalculator.TryCalculateEquipmentQuality(Owned("basic", "extra"), catalog, out value) && value == 1f, "inclusive quality boundaries");

            var restaurant = ScriptableObject.CreateInstance<RestaurantData>();
            created.Add(restaurant);
            restaurant.requiredRoles = new[] { Required("service", 2), Required("cook", 1) };
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("service", 2), Member("cook", 1)), restaurant, out value) && value == 1f, "full coverage");
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("service", 1), Member("cook", 1)), restaurant, out value) && Near(value, 2f / 3f), "partial coverage");
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(Team(), restaurant, out value) && value == 0f, "empty team");
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(null, restaurant, out value) && value == 0f, "absent team is empty");
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("service", 50)), restaurant, out value) && Near(value, 2f / 3f), "surplus does not compensate missing role");
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("cook", 1)), restaurant, out value) && Near(value, 1f / 3f), "required role absent");
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("service", 50), Member("cook", 50)), restaurant, out value) && value == 1f, "surplus never exceeds one");
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("other", 100)), restaurant, out value) && value == 0f, "unrequired roles ignored");
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("service", 1), Member("service", 1), Member("cook", 1)), restaurant, out value) && value == 1f, "duplicate hired quantities summed");
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("SERVICE", 2), Member("cook", 1)), restaurant, out value) && Near(value, 1f / 3f), "exact role IDs");
            restaurant.requiredRoles = new[] { Required("service", 1), Required("service", 2) };
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("service", 2)), restaurant, out value) && Near(value, 2f / 3f), "duplicate requirements summed");
            restaurant.requiredRoles = new[] { Required("service", int.MaxValue), Required("service", int.MaxValue) };
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("service", int.MaxValue), Member("service", int.MaxValue)), restaurant, out value) && value == 1f, "aggregates avoid int overflow");
            restaurant.requiredRoles = Array.Empty<RoleRequirement>();
            check(!EventIndicatorCalculator.TryCalculateTeamCoverage(Team(), restaurant, out value) && value == 0f, "empty requirements unavailable");
            restaurant.requiredRoles = new[] { Required("", 0) };
            check(!EventIndicatorCalculator.TryCalculateTeamCoverage(Team(), restaurant, out value), "no positive requirements unavailable");
            restaurant.requiredRoles = new[] { Required(" ", 1) };
            check(!EventIndicatorCalculator.TryCalculateTeamCoverage(Team(), restaurant, out value), "missing required role ID");
            restaurant.requiredRoles = new[] { Required("service", -1) };
            check(!EventIndicatorCalculator.TryCalculateTeamCoverage(Team(), restaurant, out value), "negative required quantity");
            restaurant.requiredRoles = new RoleRequirement[] { null };
            check(!EventIndicatorCalculator.TryCalculateTeamCoverage(Team(), restaurant, out value), "null requirement");
            restaurant.requiredRoles = null;
            check(!EventIndicatorCalculator.TryCalculateTeamCoverage(Team(), restaurant, out value), "null requirements");
            check(!EventIndicatorCalculator.TryCalculateTeamCoverage(Team(), null, out value), "missing restaurant");
            restaurant.requiredRoles = new[] { Required("service", 2), Required("unused", 0) };
            check(!EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("service", -1)), restaurant, out value) && value == 0f, "negative hired quantity");
            check(!EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("other", -1)), restaurant, out value), "negative unrequired quantity is invalid");
            check(!EventIndicatorCalculator.TryCalculateTeamCoverage(Team(Member("", 1)), restaurant, out value), "missing hired role ID");
            check(!EventIndicatorCalculator.TryCalculateTeamCoverage(Team((TeamSelectionItem)null), restaurant, out value), "null hired entry");
            var team = Team(Member("service", 2), Member("", 0));
            string before = JsonUtility.ToJson(team);
            check(EventIndicatorCalculator.TryCalculateTeamCoverage(team, restaurant, out value) && value == 1f, "zero quantities ignored");
            check(JsonUtility.ToJson(team) == before && restaurant.requiredRoles[0].quantity == 2, "inputs unchanged");
            Debug.Log($"[EventIndicators] SUCCESS: {passed} checks passed.");
        }
        finally
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);
        }
    }

    private static bool Near(float actual, float expected) => Math.Abs(actual - expected) < 0.000001f;
    private static EquipmentSelectionData Owned(params string[] ids) => new EquipmentSelectionData { equipmentIds = new List<string>(ids) };
    private static TeamSelectionData Team(params TeamSelectionItem[] members) => new TeamSelectionData { members = new List<TeamSelectionItem>(members) };
    private static TeamSelectionItem Member(string id, int quantity) => new TeamSelectionItem { roleId = id, quantity = quantity };
    private static RoleRequirement Required(string id, int quantity) => new RoleRequirement { roleId = id, quantity = quantity };
    private static EquipmentData Equipment(string id, float bonus, List<ScriptableObject> created)
    {
        var asset = ScriptableObject.CreateInstance<EquipmentData>();
        asset.id = id;
        asset.qualityBonus = bonus;
        created.Add(asset);
        return asset;
    }
}
