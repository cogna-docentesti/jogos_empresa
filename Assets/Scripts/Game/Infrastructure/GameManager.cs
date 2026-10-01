using Game.Infrastructure;
using Game.Infrastructure.Session;
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
                // Sessao nova: nasce so em memoria. A linha no banco so aparece
                // no confirm da D3.
                Debug.Log("Nenhuma sessao encontrada, criando nova sessao (so em memoria)");

                PlayerSession.ClearDecisions();
                service.CreateNewSession();

                StateMachine.TryChangeState(InitialDecisionFlow.Location);
            }
            else if (!HasCommittedInitialDecisions(GameSessionState.Current))
            {
                // Linha antiga, criada pelo codigo anterior assim que o jogo
                // abria (antes de qualquer escolha). Ela existe no banco mas nao
                // tem cardapio. Em vez de mandar o jogador para o hub com uma
                // empresa vazia, refaz as decisoes iniciais. O confirm da D3
                // sobrescreve esta mesma linha (mesmo sessionId).
                Debug.Log("Sessao encontrada sem decisoes iniciais, voltando para a D1");

                PlayerSession.ClearDecisions();
                StateMachine.TryChangeState(InitialDecisionFlow.Location);
            }
            else
            {
                Debug.Log("Sessao carregada com sucesso");

                // Copia as decisoes gravadas para o rascunho, para que as telas
                // que leem dele (ex.: Cardapio aberto pelo hub) mostrem o que esta no banco.
                PlayerSession.LoadDecisionsFrom(GameSessionState.Current);

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

        SceneManager.LoadScene(SceneNames.Game);
    }

    /// <summary>
    /// Uma sessao so e considerada "configurada" se o confirm da D3 ja
    /// aconteceu, e o sinal disso e ter cardapio gravado.
    /// </summary>
    private static bool HasCommittedInitialDecisions(GameSessionEntity session)
    {
        if (session == null)
            return false;

        return MenuPricingHelper.FromJson(session.menuPricingJson).items.Count > 0;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            GameSessionState.Save();
    }

    private void OnApplicationQuit()
    {
        // 1. Salva antes de fechar qualquer coisa.
        //    Antes do confirm da D3 este Save() nao faz nada (GameSessionState.IsPersisted
        //    ainda e false), entao fechar o jogo no meio das decisoes nao cria linha.
        //    A pergunta "deseja salvar?" e a E-05.
        GameSessionState.Save();

        // 2. Agora é seguro fechar o banco
        DatabaseInitializer.DatabaseService?.Close();
    }
}
