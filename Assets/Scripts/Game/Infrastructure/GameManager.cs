using UnityEngine;
using UnityEngine.SceneManagement; 

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameStateMachine StateMachine { get; private set; }

    private GameSessionService service;

    private const string UserId = "local_user_01";
    private const string ProfessorId = "prof_01";

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        StateMachine = new GameStateMachine();
    }

    private void Start()
    {
        service = new GameSessionService(UserId, ProfessorId);

        GameSessionState.LoadActiveSession(UserId);

        StateMachine.TryChangeState(GameState.MainMenu);

        if (!GameSessionState.HasSession)
        {
            Debug.Log("Nenhuma sessao encontrada, criando nova sessao");

            service.CreateNewSession();

            StateMachine.TryChangeState(GameState.Config_Location);
        }
        else
        {
            Debug.Log("Sessao carregada com sucesso");

            StateMachine.TryChangeState(GameState.Management_Hub);
        }

        SceneManager.LoadScene("GameScene");
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            GameSessionState.Save();
    }

    private void OnApplicationQuit()
    {
        GameSessionState.Save();
    }
}
