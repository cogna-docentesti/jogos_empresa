using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameSessionDebugTest : MonoBehaviour
{
    public List<EquipmentData> testEquipments;
    public List<RoleData> testRoles;

    private GameSessionService service;

    private IEnumerator Start()
    {
        yield return null; // espera GameManager.Start() rodar

        service = new GameSessionService("local_user_01", "prof_01");

        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager n�o encontrado.");
            yield break;
        }

        if (!GameSessionState.HasSession)
        {
            Debug.LogError("Nenhuma sess�o carregada/criada.");
            yield break;
        }

        TestStateMachineFlow();
    }


    private void TestStateMachineFlow()
    {
        var sm = GameManager.Instance.StateMachine;

        Debug.Log("Estado inicial do teste: " + sm.CurrentState);

        if (sm.CurrentState == GameState.MainMenu)
            sm.TryChangeState(GameState.Config_Location);

        Debug.Log("Antes ConfirmLocation");
        service.ConfirmLocation(LocationZone.COMMERCIAL);
        Debug.Log("Depois ConfirmLocation: " + sm.CurrentState);

        service.ConfirmRestaurant(RestaurantType.JAPONES);
        Debug.Log("Depois ConfirmRestaurant: " + sm.CurrentState);

        service.ConfirmTargetSegment(Segment.MEDIUM);
        Debug.Log("Depois ConfirmTargetSegment: " + sm.CurrentState);

        service.ConfirmStructuralConfiguration("IDEAL");
        Debug.Log("Depois ConfirmStructuralConfiguration: " + sm.CurrentState);

        service.ConfirmInitialEquipment(testEquipments);
        Debug.Log("Depois ConfirmInitialEquipment: " + sm.CurrentState);

        foreach (var eq in testEquipments)
            Debug.Log($"Equipamento {eq.id}: {service.HasEquipment(eq)}");

        foreach (var role in testRoles)
            service.AddTeamMember(role, 1);

        service.ConfirmInitialTeam();
        Debug.Log("Depois ConfirmInitialTeam: " + sm.CurrentState);

        foreach (var role in testRoles)
            Debug.Log($"Funcion�rio {role.id}: {service.GetTeamMemberQuantity(role)}");

        service.ConfirmInitialCapital();
        Debug.Log("Estado final: " + sm.CurrentState);
    }
}