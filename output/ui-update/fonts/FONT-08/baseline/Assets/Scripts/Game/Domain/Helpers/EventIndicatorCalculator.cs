using System;
using System.Collections.Generic;

/// <summary>Calculates event indicators without reading or changing external state.</summary>
public static class EventIndicatorCalculator
{
    /// <summary>False means unavailable; score remains zero on failure.</summary>
    public static bool TryCalculateEquipmentQuality(
        EquipmentSelectionData owned,
        IReadOnlyList<EquipmentData> catalog,
        out float score)
    {
        score = 0f;
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        if (owned?.equipmentIds != null)
        {
            foreach (string id in owned.equipmentIds)
                if (!string.IsNullOrWhiteSpace(id))
                    ids.Add(id);
        }

        if (ids.Count == 0)
            return true;
        if (catalog == null)
            return false;

        var byId = new Dictionary<string, EquipmentData>(StringComparer.Ordinal);
        foreach (var equipment in catalog)
        {
            if (equipment == null || string.IsNullOrWhiteSpace(equipment.id))
                return false;
            if (byId.ContainsKey(equipment.id))
                return false;
            byId.Add(equipment.id, equipment);
        }

        double total = 0d;
        foreach (string id in ids)
        {
            if (!byId.TryGetValue(id, out var equipment))
                return false;
            float bonus = equipment.qualityBonus;
            // EquipmentData.qualityBonus is declared with Range(0f, 1f).
            if (float.IsNaN(bonus) || float.IsInfinity(bonus) || bonus < 0f || bonus > 1f)
                return false;
            total += bonus;
        }

        score = (float)total;
        return true;
    }

    /// <summary>False means unavailable; ratio remains zero on failure.</summary>
    public static bool TryCalculateTeamCoverage(
        TeamSelectionData team,
        RestaurantData restaurant,
        out float ratio)
    {
        ratio = 0f;
        if (restaurant == null || restaurant.requiredRoles == null)
            return false;

        var required = new Dictionary<string, long>(StringComparer.Ordinal);
        long totalRequired = 0;
        foreach (var requirement in restaurant.requiredRoles)
        {
            if (requirement == null || requirement.quantity < 0)
                return false;
            if (requirement.quantity == 0)
                continue;
            if (string.IsNullOrWhiteSpace(requirement.roleId))
                return false;

            required.TryGetValue(requirement.roleId, out long count);
            required[requirement.roleId] = count + requirement.quantity;
            totalRequired += requirement.quantity;
        }

        if (totalRequired == 0)
            return false;

        var hired = new Dictionary<string, long>(StringComparer.Ordinal);
        if (team?.members != null)
        {
            foreach (var member in team.members)
            {
                if (member == null || member.quantity < 0)
                    return false;
                if (member.quantity == 0)
                    continue;
                if (string.IsNullOrWhiteSpace(member.roleId))
                    return false;
                if (!required.ContainsKey(member.roleId))
                    continue;

                hired.TryGetValue(member.roleId, out long count);
                hired[member.roleId] = count + member.quantity;
            }
        }

        long totalCovered = 0;
        foreach (var requirement in required)
        {
            hired.TryGetValue(requirement.Key, out long count);
            totalCovered += Math.Min(count, requirement.Value);
        }

        ratio = (float)((double)totalCovered / totalRequired);
        return true;
    }
}
