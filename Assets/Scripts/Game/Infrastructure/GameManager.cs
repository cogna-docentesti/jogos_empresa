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

        // Todo o preparo da sessao vai dentro do try. O LoadScene fica FORA e
        // depois dele: seja qual for a falha - banco indisponivel, sessao
        // corrompida, entidade que nao desserializa - o jogador precisa sair da
        // Bootstrap. Antes, uma excecao aqui abortava o Start() antes da ultima
        // linha e o app ficava parado numa tela vazia, sem aviso nenhum.
        try
        {
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
        }
        catch (System.Exception e)
        {
            Debug.LogError("[GameManager] Falha ao preparar a sessao: " + e.Message + "\n" + e);

            // Sem sessao valida, o unico ponto de entrada coerente e o comeco
            // da configuracao.
            StateMachine.TryChangeState(GameState.Config_Location);
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
        // 1. Salva antes de fechar qualquer coisa
        GameSessionState.Save();

        // 2. Agora é seguro fechar o banco
        DatabaseInitializer.DatabaseService?.Close();
    }
}
