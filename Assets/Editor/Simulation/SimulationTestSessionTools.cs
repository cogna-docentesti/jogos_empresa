// Assets/Editor/Simulation/SimulationTestSessionTools.cs
// Ferramentas de TESTE enquanto a Agência de RH (D6) e a loja completa (D5) não estão no hub.
// Rodam em Play, sobre a sessão carregada (GameSessionState.Current), e gravam no game.db.
// No mês seguinte o SetupReconciler leva as mudanças para o estado do motor (contrata e compra).
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SimulationTestSessionTools
{
    [MenuItem("Game/Simulação/Teste: contratar a equipe mínima na sessão (Play)")]
    public static void HireMinimumTeam()
    {
        var session = RequireSession();
        if (session == null) return;
        var restaurant = Resources.LoadAll<RestaurantData>("Restaurants").FirstOrDefault(r => r.type == session.restaurantType);
        if (restaurant == null || restaurant.requiredRoles == null) { Debug.LogError("[Simulação] RestaurantData do tipo da sessão não encontrado."); return; }

        var team = TeamSelectionHelper.FromJson(session.teamJson) ?? new TeamSelectionData();
        foreach (var required in restaurant.requiredRoles)
        {
            var member = team.members.Find(m => m.roleId == required.roleId);
            if (member == null) team.members.Add(new TeamSelectionItem { roleId = required.roleId, quantity = required.quantity });
            else member.quantity = Math.Max(member.quantity, required.quantity);
        }
        GameSessionState.SetTeamJson(TeamSelectionHelper.ToJson(team), true);
        Debug.Log("[Simulação] Equipe da sessão: " + string.Join(", ", team.members.Select(m => $"{m.quantity}x {m.roleId}")) +
                  ". Abra o PLANEJAR MÊS de novo para ver a contratação.");
    }

    [MenuItem("Game/Simulação/Teste: comprar equipamentos básicos e do tipo na sessão (Play)")]
    public static void BuyRequiredEquipment()
    {
        var session = RequireSession();
        if (session == null) return;
        var equipment = EquipmentSelectionHelper.FromJson(session.equipmentJson) ?? new EquipmentSelectionData();
        int added = 0;
        foreach (var item in Resources.LoadAll<EquipmentData>("Equipments"))
        {
            bool applies = item.applicableTypes == null || item.applicableTypes.Length == 0 || item.applicableTypes.Contains(session.restaurantType);
            if (!applies || equipment.equipmentIds.Contains(item.id)) continue;
            equipment.equipmentIds.Add(item.id);
            added++;
        }
        GameSessionState.SetEquipmentJson(EquipmentSelectionHelper.ToJson(equipment), true);
        Debug.Log($"[Simulação] {added} equipamentos adicionados à sessão. O motor paga por eles no início do próximo mês.");
    }

    private static GameSessionEntity RequireSession()
    {
        if (!EditorApplication.isPlaying) { Debug.LogError("[Simulação] Entre em Play e abra o hub antes."); return null; }
        var session = GameSessionState.Current;
        if (session == null) Debug.LogError("[Simulação] Nenhuma sessão carregada.");
        return session;
    }
}
