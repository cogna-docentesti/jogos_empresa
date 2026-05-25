using System.Collections;
using UnityEngine;

public class RoundFlowDebugTest : MonoBehaviour
{
    private GameSessionService service;

    private IEnumerator Start()
    {
        yield return null; // espera GameManager iniciar

        service = new GameSessionService("local_user_01", "prof_01");

        if (!GameSessionState.HasSession)
        {
            Debug.LogError("Nenhuma sessão ativa.");
            yield break;
        }

        var sm = GameManager.Instance.StateMachine;

        sm.ForceState(GameState.Management_Hub);

        Debug.Log("=== TESTE ROUND INICIADO ===");
        Debug.Log("Estado inicial: " + sm.CurrentState);
        Debug.Log("Round inicial: " + GameSessionState.Current.currentRound);
        Debug.Log("Cash inicial: " + GameSessionState.Current.currentCash);

        service.StartRound();

        Debug.Log("Depois StartRound: " + sm.CurrentState);

        var result = service.ProcessCurrentRound();

        if (result == null)
        {
            Debug.LogError("RoundResult veio null.");
            yield break;
        }

        Debug.Log("Depois ProcessCurrentRound: " + sm.CurrentState);
        Debug.Log("RoundResult ID: " + result.roundResultId);
        Debug.Log("NetResult: " + result.netResult);
        Debug.Log("OpeningCash: " + result.openingCash);
        Debug.Log("ClosingCash: " + result.closingCash);

        Debug.Log("Round atual da sessão: " + GameSessionState.Current.currentRound);
        Debug.Log("Cash atual da sessão: " + GameSessionState.Current.currentCash);
        Debug.Log("Status: " + GameSessionState.Current.status);

        Debug.Log("=== TESTE ROUND FINALIZADO ===");
    }
}
