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
            Debug.LogError("GameManager não encontrado.");
            yield break;
        }

        if (!GameSessionState.HasSession)
        {
            Debug.LogError("Nenhuma sessão carregada/criada.");
            yield break;
        }

        TestStateMachineFlow();
    }


    private void TestStateMachineFlow()
    {
        var sm = GameManager.Instance.StateMachine;

        sm.ForceState(GameState.Config_Location);

        service.ConfirmLocation(LocationZone.COMMERCIAL);
        service.ConfirmRestaurant(RestaurantType.JAPONES);
        service.ConfirmTargetSegmentAndPrice(Segment.MEDIUM, PriceStrategy.VALUE_ADDED);

        service.ConfirmStructuralConfiguration();

        service.ConfirmInitialCapital();

        service.ConfirmInitialEquipment(testEquipments);

        foreach (var role in testRoles)
        {
            service.AddTeamMember(role, 1);
        }

        service.ConfirmInitialTeam(testRoles);

        Debug.Log("Estado final: " + sm.CurrentState);
        Debug.Log("Alignment Score: " + GameSessionState.Current.alignmentScore);
        Debug.Log("Alignment Class: " + GameSessionState.Current.alignmentClassification);
        Debug.Log("Alignment Factor: " + GameSessionState.Current.alignmentFactor);
    }
}