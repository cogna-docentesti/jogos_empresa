// Shared setup helpers for the simulation and calendar validation suites.
#if DEVELOPMENT_BUILD || UNITY_EDITOR || FONT10_QA_PLAYER
using System;
using System.Linq;
using UnityEngine;
public static class MotorMonthQA
{
    public static void SkipSetupTutorials(bool withMinimumTeam = false)
    {
        var s = GameSessionState.Current;
        if (s == null) throw new InvalidOperationException("Sem sessão para marcar os tutoriais");
        PlayerPrefs.SetInt("loan_tutorial_completed:" + s.sessionId, 1);
        PlayerPrefs.Save();
        s.equipmentTutorialStep = Game.Adapter.In.UI.EquipmentTutorial.CompletedStep;
        s.equipmentTutorialVersion = Game.Adapter.In.UI.EquipmentTutorial.Version;
        s.teamTutorialStep = 3;
        var owned = EquipmentSelectionHelper.FromJson(s.equipmentJson) ?? new EquipmentSelectionData();
        var basic = Resources.LoadAll<EquipmentData>("Equipments").Where(e => e.category == EquipmentCategory.BASIC).Select(e => e.id);
        EquipmentSelectionHelper.AddUniqueIds(owned, basic.ToArray());
        s.equipmentJson = EquipmentSelectionHelper.ToJson(owned);
        if (withMinimumTeam)
        {
            var restaurant = Resources.LoadAll<RestaurantData>("Restaurants").FirstOrDefault(r => r.type == s.restaurantType);
            var team = TeamSelectionHelper.FromJson(s.teamJson) ?? new TeamSelectionData();
            if (restaurant != null)
                foreach (var req in restaurant.requiredRoles) TeamSelectionHelper.SetMemberQuantity(team, req.roleId, req.quantity);
            s.teamJson = TeamSelectionHelper.ToJson(team);
        }
        if (!GameSessionState.TrySave(out string error)) throw new InvalidOperationException("Tutoriais: " + error);
        foreach (var t in UnityEngine.Object.FindObjectsByType<Game.Adapter.In.UI.LoanTutorial>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.Hide();
        foreach (var t in UnityEngine.Object.FindObjectsByType<Game.Adapter.In.UI.EquipmentTutorial>(FindObjectsInactive.Include, FindObjectsSortMode.None)) t.Hide();
    }

}
#endif
