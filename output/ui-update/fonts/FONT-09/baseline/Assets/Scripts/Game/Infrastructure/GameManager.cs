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

    private bool resumeAfterIdentification;
    private GameState resumeDestination = GameState.Management_Hub;

    private void Start()
    {
        try
        {
            PrepareSession(UserId, ProfessorId);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[GameManager] Falha ao preparar a sessao: " + e);
            StateMachine.ForceState(InitialDecisionFlow.Identification);
        }

        // Legacy Bootstrap remains usable, but GameScene never reloads itself.
        if (SceneManager.GetActiveScene().name != SceneNames.Game)
            SceneManager.LoadScene(SceneNames.Game);
    }

    public bool ConfirmIdentification()
    {
        if (!PlayerSession.HasValidIdentification
            || StateMachine.CurrentState != InitialDecisionFlow.Identification)
            return false;

        if (resumeAfterIdentification)
        { StateMachine.ForceState(resumeDestination); return true; }
        return StateMachine.TryChangeState(InitialDecisionFlow.Location);
    }

    public void BeginNewIdentification()
    {
        resumeAfterIdentification = false;
        resumeDestination = GameState.Management_Hub;
        StateMachine.ForceState(InitialDecisionFlow.Identification);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Carrega a partida do banco (se houver) e decide a primeira tela.
    /// Uma funcao so, usada pelo jogo e pelos testes da Thaysla
    /// (RoundFlowDebugTest chama este metodo direto).
    ///
    ///  - Save incompativel (ciclo antigo de 12 meses): nao abre nada; o
    ///    UIStateListener mostra o aviso com o botao de reinicio.
    ///  - Sem partida: cria uma sessao SO em memoria e abre a identificacao.
    ///  - Partida sem cardapio (linha antiga, criada antes da regra da D3):
    ///    abre identificacao; o confirm da D3 sobrescreve a mesma linha.
    ///  - Partida configurada: reidrata as escolhas e retoma o passo operacional,
    ///    evento ou resumo pendente; sem pendencia, abre o hub. Cadastro invalido
    ///    e corrigido antes de entregar o destino persistido.
    /// </summary>
    internal void PrepareSession(string userId, string professorId)
    {
        service = new GameSessionService(userId, professorId);
        service.LoadActiveSession();
        GameSessionState.TryLoadCompletedReport(userId);
        PlayerSession.TryLoadIdentification();
        resumeAfterIdentification = false;
        resumeDestination = GameState.Management_Hub;
        StateMachine.ForceState(GameState.MainMenu);

        if (GameSessionState.HasIncompatibleSave)
        {
            // Keep the save intact until the player confirms the existing dialog.
            return;
        }

        if (GameSessionState.Current?.status == GameSessionStatus.COMPLETED || GameSessionState.Current?.status == GameSessionStatus.BANKRUPT)
        {
            PlayerSession.LoadDecisionsFrom(GameSessionState.Current);
            StateMachine.ForceState(GameSessionState.Current.finalSummaryAcknowledged
                ? (GameSessionState.Current.status == GameSessionStatus.BANKRUPT ? GameState.GameOver_Bankruptcy : GameState.FinalReport)
                : GameState.Round_Summary);
            return;
        }

        if (!GameSessionState.HasSession)
        {
            PlayerSession.ClearDecisions();
            service.CreateNewSession();
        }
        else if (HasCommittedInitialDecisions(GameSessionState.Current))
        {
            PlayerSession.LoadDecisionsFrom(GameSessionState.Current);
            resumeAfterIdentification = true;
            var session = GameSessionState.Current;
            if (!string.IsNullOrEmpty(session.pendingSummaryResultId)) resumeDestination = GameState.Round_Summary;
            else if (session.initialSetupResumeState == (int)GameState.Initial_Capital
                || session.initialSetupResumeState == (int)GameState.Initial_Equipment
                || session.initialSetupResumeState == (int)GameState.Initial_Team)
                resumeDestination = (GameState)session.initialSetupResumeState.Value;
            else
            {
                var db = DatabaseInitializer.DatabaseService.Connection;
                db.CreateTable<MonthlyEventPlanEntity>();
                if (db.Find<MonthlyEventPlanEntity>(session.sessionId + ":" + session.currentRound) != null)
                    resumeDestination = GameState.Round_Event;
            }
        }
        else
        {
            PlayerSession.ClearDecisions();
        }

        // Prefilled registration never skips step 1 for a new game.
        bool canResume = resumeAfterIdentification && PlayerSession.HasValidIdentification
            && !PlayerSession.IdentificationEditRequested;
        if (canResume) StateMachine.ForceState(resumeDestination);
        else StateMachine.TryChangeState(InitialDecisionFlow.Identification);
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
        if (!TryShutdown(salvar, out var error)) Debug.LogWarning("[GameManager] " + error);
    }

    public bool TryShutdown(bool salvar, out string error)
    {
        error = null;
        if (_shutdownDone) return true;
        if (salvar && !GameSessionState.TrySave(out error)) return false;
        try { DatabaseInitializer.DatabaseService?.Close(); }
        catch (System.Exception)
        {
            error = "Não foi possível fechar o banco de dados. Tente novamente.";
            return false;
        }
        _shutdownDone = true;
        Debug.Log("[GameManager] Banco fechado após a opção de saída escolhida.");
        return true;
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
