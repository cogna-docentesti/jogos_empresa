using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class EquipmentSelectionHelper
{
    public static EquipmentSelectionData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new EquipmentSelectionData();

        var data = JsonUtility.FromJson<EquipmentSelectionData>(json);

        if (data == null || data.equipmentIds == null)
            return new EquipmentSelectionData();

        return data;
    }

    public static string ToJson(EquipmentSelectionData data)
    {
        if (data == null)
            data = new EquipmentSelectionData();

        if (data.equipmentIds == null)
            data.equipmentIds = new List<string>();

        return JsonUtility.ToJson(data);
    }

    public static void AddUniqueIds(EquipmentSelectionData currentData, IEnumerable<string> newIds)
    {
        if (currentData == null)
            return;

        if (currentData.equipmentIds == null)
            currentData.equipmentIds = new List<string>();

        if (newIds == null)
            return;

        foreach (var id in newIds)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (!currentData.equipmentIds.Contains(id))
                currentData.equipmentIds.Add(id);
        }
    }

    public static void RemoveIds(EquipmentSelectionData currentData, IEnumerable<string> idsToRemove)
    {
        if (currentData == null || currentData.equipmentIds == null || idsToRemove == null)
            return;

        foreach (var id in idsToRemove)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            currentData.equipmentIds.Remove(id);
        }
    }

    public static bool ContainsId(EquipmentSelectionData data, string id)
    {
        if (data == null || data.equipmentIds == null || string.IsNullOrWhiteSpace(id))
            return false;

        return data.equipmentIds.Contains(id);
    }

    public static List<string> GetIds(EquipmentSelectionData data)
    {
        if (data == null || data.equipmentIds == null)
            return new List<string>();

        return data.equipmentIds.ToList();
    }
}