using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Infrastructure.Session;
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

        // D1, D2 e D3 vao para o rascunho em memoria; o banco so e tocado no
        // ConfirmInitialDecisions (mesmo caminho do botao Confirmar da D3).
        PlayerSession.SaveLocation("store", "Área Comercial", LocationZone.Comercio);
        PlayerSession.SaveRestaurant(RestaurantType.JAPONES, Segment.MEDIUM);
        sm.ForceState(InitialDecisionFlow.Menu);

        var restaurant = Resources.LoadAll<RestaurantData>("Restaurants")
            .FirstOrDefault(r => r != null && r.type == RestaurantType.JAPONES);
        PlayerSession.SetMenu(RestaurantType.JAPONES, MenuPricingHelper.FromProducts(restaurant?.products));

        if (!service.ConfirmInitialDecisions())
        {
            Debug.LogError("ConfirmInitialDecisions falhou. Veja o aviso logo acima no Console.");
            return;
        }

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
