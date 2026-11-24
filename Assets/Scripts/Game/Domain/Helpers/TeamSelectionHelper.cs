using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class TeamSelectionHelper
{
    public static Dictionary<string, int> GetMinimums(RestaurantData restaurant)
    {
        return (restaurant.requiredRoles ?? Array.Empty<RoleRequirement>())
            .Where(requirement => requirement.quantity > 0)
            .GroupBy(requirement => requirement.roleId)
            .ToDictionary(group => group.Key, group => group.Sum(requirement => requirement.quantity));
    }

    public static Dictionary<string, int> GetShortfalls(TeamSelectionData team, IReadOnlyDictionary<string, int> minimums)
    {
        var shortfalls = new Dictionary<string, int>();
        foreach (var minimum in minimums)
        {
            int missing = minimum.Value - Math.Max(0, GetMemberQuantity(team, minimum.Key));
            if (missing > 0)
                shortfalls.Add(minimum.Key, missing);
        }
        return shortfalls;
    }

    public static bool HasMinimumStaff(RestaurantData restaurant, string teamJson)
    {
        if (restaurant == null || (restaurant.requiredRoles?.Any(requirement => requirement == null
            || string.IsNullOrWhiteSpace(requirement.roleId) || requirement.quantity < 0) ?? false))
            return false;

        try
        {
            var team = FromJson(teamJson);
            return team.members.All(member => member != null && !string.IsNullOrWhiteSpace(member.roleId) && member.quantity >= 0)
                && team.members.Select(member => member.roleId).Distinct().Count() == team.members.Count
                && GetShortfalls(team, GetMinimums(restaurant)).Count == 0;
        }
        catch (ArgumentException) { return false; }
        catch (OverflowException) { return false; }
    }

    public static TeamSelectionData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new TeamSelectionData();

        var data = JsonUtility.FromJson<TeamSelectionData>(json);

        if (data == null || data.members == null)
            return new TeamSelectionData();

        return data;
    }

    public static string ToJson(TeamSelectionData data)
    {
        if (data == null)
            data = new TeamSelectionData();

        if (data.members == null)
            data.members = new List<TeamSelectionItem>();

        return JsonUtility.ToJson(data);
    }

    public static void AddMember(TeamSelectionData data, string roleId, int quantity = 1)
    {
        if (data == null || string.IsNullOrWhiteSpace(roleId) || quantity <= 0)
            return;

        if (data.members == null)
            data.members = new List<TeamSelectionItem>();

        var existing = data.members.FirstOrDefault(x => x.roleId == roleId);

        if (existing != null)
        {
            existing.quantity += quantity;
        }
        else
        {
            data.members.Add(new TeamSelectionItem
            {
                roleId = roleId,
                quantity = quantity
            });
        }
    }

    public static void SetMemberQuantity(TeamSelectionData data, string roleId, int quantity)
    {
        if (data == null || string.IsNullOrWhiteSpace(roleId))
            return;

        if (data.members == null)
            data.members = new List<TeamSelectionItem>();

        var existing = data.members.FirstOrDefault(x => x.roleId == roleId);

        if (quantity <= 0)
        {
            if (existing != null)
                data.members.Remove(existing);

            return;
        }

        if (existing != null)
        {
            existing.quantity = quantity;
        }
        else
        {
            data.members.Add(new TeamSelectionItem
            {
                roleId = roleId,
                quantity = quantity
            });
        }
    }

    public static void RemoveMember(TeamSelectionData data, string roleId, int quantity = 1)
    {
        if (data == null || data.members == null || string.IsNullOrWhiteSpace(roleId) || quantity <= 0)
            return;

        var existing = data.members.FirstOrDefault(x => x.roleId == roleId);

        if (existing == null)
            return;

        existing.quantity -= quantity;

        if (existing.quantity <= 0)
            data.members.Remove(existing);
    }

    public static int GetMemberQuantity(TeamSelectionData data, string roleId)
    {
        if (data == null || data.members == null || string.IsNullOrWhiteSpace(roleId))
            return 0;

        var existing = data.members.FirstOrDefault(x => x.roleId == roleId);
        return existing?.quantity ?? 0;
    }

    public static bool HasMember(TeamSelectionData data, string roleId)
    {
        return GetMemberQuantity(data, roleId) > 0;
    }

    public static List<TeamSelectionItem> GetMembers(TeamSelectionData data)
    {
        if (data == null || data.members == null)
            return new List<TeamSelectionItem>();

        return data.members
            .Select(x => new TeamSelectionItem
            {
                roleId = x.roleId,
                quantity = x.quantity
            })
            .ToList();
    }
}
