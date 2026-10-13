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

    // E-05: garante que a sequencia de desligamento roda uma vez so.
    private bool _shutdownDone;

    /// <summary>True depois que o Shutdown rodou (o banco ja esta fechado).</summary>
    public bool IsShuttingDown => _shutdownDone;

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
        // Todo o preparo da sessao vai dentro do try. O LoadScene fica FORA e
        // depois dele: seja qual for a falha - banco indisponivel, sessao
        // corrompida, entidade que nao desserializa - o jogador precisa sair
        // desta etapa. Antes, uma excecao aqui abortava o Start() antes da
        // ultima linha e o app ficava parado numa tela vazia, sem aviso nenhum.
        try
        {
            PrepareSession(UserId, ProfessorId);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[GameManager] Falha ao preparar a sessao: " + e.Message + "\n" + e);

            // Sem sessao valida, o unico ponto de entrada coerente e o comeco
            // da configuracao.
            StateMachine.ForceState(InitialDecisionFlow.Location);
        }

        SceneManager.LoadScene(SceneNames.Game);
    }

    /// <summary>
    /// Carrega a partida do banco (se houver) e decide a primeira tela.
    /// Uma funcao so, usada pelo jogo e pelos testes da Thaysla
    /// (RoundFlowDebugTest chama este metodo direto).
    ///
    ///  - Save incompativel (ciclo antigo de 12 meses): nao abre nada; o
    ///    UIStateListener mostra o aviso com o botao de reinicio.
    ///  - Sem partida: cria uma sessao SO em memoria e abre a D1.
    ///  - Partida sem cardapio (linha antiga, criada antes da regra da D3):
    ///    abre a D1; o confirm da D3 sobrescreve a mesma linha.
    ///  - Partida completa: copia as decisoes para o rascunho e abre o hub.
    /// </summary>
    internal void PrepareSession(string userId, string professorId)
    {
        service = new GameSessionService(userId, professorId);
        service.LoadActiveSession();
        StateMachine.TryChangeState(GameState.MainMenu);

        if (GameSessionState.HasIncompatibleSave)
        {
            Debug.Log("[GameManager] Save de versao anterior encontrado. Aguardando o jogador confirmar o reinicio.");
            return;
        }

        if (!GameSessionState.HasSession)
        {
            Debug.Log("[GameManager] Nenhuma sessao encontrada, criando nova sessao (so em memoria).");
            PlayerSession.ClearDecisions();
            service.CreateNewSession();
            StateMachine.TryChangeState(InitialDecisionFlow.Location);
        }
        else if (!HasCommittedInitialDecisions(GameSessionState.Current))
        {
            Debug.Log("[GameManager] Sessao encontrada sem decisoes iniciais, voltando para a D1.");
            PlayerSession.ClearDecisions();
            StateMachine.TryChangeState(InitialDecisionFlow.Location);
        }
        else
        {
            Debug.Log("[GameManager] Sessao carregada com sucesso.");
            PlayerSession.LoadDecisionsFrom(GameSessionState.Current);
            StateMachine.TryChangeState(GameState.Management_Hub);
        }
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

    // =============================
    // E-05: DESLIGAMENTO (dono unico)
    // =============================

    /// <summary>
    /// Salvar primeiro, fechar o banco depois. Nesta ordem, uma vez so.
    ///
    /// Chamado pelo dialogo Sair (salvar = true ou false) e pelo
    /// OnApplicationQuit (fechar pelo X da janela ou pelo sistema).
    /// Antes do confirm da D3 o Save() nao grava nada (regra da E-03):
    /// as decisoes iniciais so existem no rascunho.
    /// </summary>
    public void Shutdown(bool salvar)
    {
        if (_shutdownDone)
            return;

        _shutdownDone = true;

        if (salvar)
            GameSessionState.Save();
        else
            Debug.Log("[GameManager] Saindo sem salvar: alteracoes nao gravadas foram descartadas.");

        DatabaseInitializer.DatabaseService?.Close();
        Debug.Log("[GameManager] Banco fechado.");
    }

    private void OnApplicationQuit()
    {
        // Fechar pelo X da janela (ou o sistema encerrando o app) nao da tempo
        // de perguntar nada: salva o que ja pode ser salvo. Se o jogador saiu
        // pelo dialogo, o Shutdown ja rodou e este chamado nao faz nada.
        Shutdown(salvar: true);
    }

    private void OnApplicationPause(bool pausado)
    {
        // No Android o sistema pode matar o app minimizado sem chamar
        // OnApplicationQuit. Aqui tambem nao da para perguntar: salva calado.
        if (pausado && !_shutdownDone)
            GameSessionState.Save();
    }
}
