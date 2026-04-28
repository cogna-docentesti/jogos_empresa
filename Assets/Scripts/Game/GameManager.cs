using UnityEngine;

public class GameManager : MonoBehaviour
{
    private GameSessionService service;

    private void Start()
    {
        service = new GameSessionService("local_user_01", "prof_01");

        GameSessionState.LoadActiveSession("local_user_01");

        if (!GameSessionState.HasSession)
        {
            Debug.Log("Nenhuma sessão encontrada, criando nova sessão");

            service.CreateNewSession();
        }
        else
        {
            Debug.Log("Sessão carregada com sucesso");
        }
    }

    /*
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            GameSessionState.Save();
    }

    private void OnApplicationQuit()
    {
        GameSessionState.Save();
    }*/
}
