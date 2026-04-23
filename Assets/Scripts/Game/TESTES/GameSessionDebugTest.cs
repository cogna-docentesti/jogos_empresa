using System.Collections.Generic;
using UnityEngine;

using System.Collections.Generic;
using UnityEngine;

public class GameSessionDebugTest : MonoBehaviour
{
    public List<EquipmentData> testEquipments;
    public List<RoleData> testRoles;

    private GameSessionService service;

    void Start()
    {
        //PlayerPrefs.DeleteAll();

        service = new GameSessionService("local_user_01", "prof_01");

        service.CreateNewSession();

        Debug.Log("Sessão criada: " + GameSessionState.HasSession);

        TestConfiguration();
        TestEquipment();
        TestTeam();
    }

    void TestConfiguration()
    {
        service.SetCity("cidade_debug");
        service.SetRestaurant(RestaurantType.JAPONES, Segment.MEDIUM);
        service.SetLocation(LocationZone.COMMERCIAL);
        service.SetCoherence("IDEAL");

        service.ConfirmConfiguration();

        Debug.Log("Configuração salva");
    }

    void TestEquipment()
    {
        if (testEquipments == null || testEquipments.Count == 0)
            return;

        service.AddEquipments(testEquipments);

        foreach (var eq in testEquipments)
        {
            bool has = service.HasEquipment(eq);
            Debug.Log($"Equipamento {eq.id} presente? {has}");
        }
    }

    void TestTeam()
    {
        if (testRoles == null || testRoles.Count == 0)
            return;

        foreach (var role in testRoles)
        {
            service.AddTeamMember(role, 1);
        }

        foreach (var role in testRoles)
        {
            int qtd = service.GetTeamMemberQuantity(role);
            Debug.Log($"Role {role.id} quantidade: {qtd}");
        }
    }
}