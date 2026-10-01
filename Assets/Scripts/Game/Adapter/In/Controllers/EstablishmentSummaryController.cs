using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Domain.Service;
using Game.Infrastructure.Session;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    /// <summary>
    /// Liga o servico de dominio a view da tela "Meu Estabelecimento".
    ///
    /// Recalcula em todo OnEnable: como a tela e aberta pelo mapa depois de
    /// o jogador mexer no Banco, no Cardapio, no RH ou na Loja, os numeros
    /// sempre refletem a ultima escolha sem precisar de evento nenhum.
    /// </summary>
    public sealed class EstablishmentSummaryController : MonoBehaviour
    {
        [SerializeField] private EstablishmentSummaryView view;

        private void Awake()
        {
            if (view == null)
                view = GetComponent<EstablishmentSummaryView>();
        }

        private void OnEnable()
        {
            view?.BindReset(ResetGame);
            Refresh();
        }

        private void ResetGame()
        {
            GameSessionEntity session = GameSessionState.Current;
            var databaseService = DatabaseInitializer.DatabaseService;

            if (session == null || databaseService == null || databaseService.Connection == null)
            {
                Debug.LogError("[EstablishmentSummaryController] Nao foi possivel resetar: sessao ou banco indisponivel.");
                return;
            }

            try
            {
                var roundRepository = new RoundResultRepository(databaseService.Connection);
                var eventRepository = new SessionEventHistoryRepository(databaseService.Connection);
                var sessionRepository = new GameSessionRepository(databaseService.Connection);

                // Apaga tudo o que pertence a esta partida: meses, eventos e a sessao.
                roundRepository.DeleteBySessionId(session.sessionId);
                eventRepository.DeleteBySessionId(session.sessionId);
                sessionRepository.Delete(session);
                Debug.Log($"[Banco] RESET: sessao {session.sessionId} apagada (meses, eventos e sessao).");

                string userId = session.userId;
                string professorId = session.professorId;

                GameSessionState.Clear();
                PlayerSession.Clear();

                // Reset = jogo do comeco. Sem isto, o cadastro salvo em PlayerPrefs
                // fazia a identificacao reaparecer preenchida (ou ser pulada).
                PlayerSession.ClearSavedIdentification();

                MenuNavigator.Instance?.CloseAll();

                // Nova sessao SO em memoria. Ela so vai para o banco no confirm da D3.
                var sessionService = new GameSessionService(userId, professorId);
                sessionService.CreateNewSession();

                view.HideResetWarning();

                if (GameManager.Instance != null)
                    GameManager.Instance.StateMachine.ForceState(InitialDecisionFlow.Location);

                // Volta para a primeira cena, com o cadastro vazio. Ao clicar em
                // Cadastrar, a GameScene abre na D1 (o GameManager guarda o estado).
                UnityEngine.SceneManagement.SceneManager.LoadScene(Game.Infrastructure.SceneNames.Identification);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[EstablishmentSummaryController] Falha ao resetar o jogo: " + exception);
            }
        }

        public void Refresh()
        {
            if (view == null)
            {
                Debug.LogError("[EstablishmentSummaryController] View nao configurada.");
                return;
            }

            view.Bind(EstablishmentSummaryService.Build());
        }
    }
}
