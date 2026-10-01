using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Domain.Service;
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

        public void ResetGame()
        {
            GameSessionEntity session = GameSessionState.Current ?? GameSessionState.IncompatibleSession;
            var databaseService = DatabaseInitializer.DatabaseService;

            if (session == null || databaseService == null || databaseService.Connection == null)
            {
                Debug.LogError("[EstablishmentSummaryController] Nao foi possivel resetar: sessao ou banco indisponivel.");
                return;
            }

            try
            {
                var sessionService = new GameSessionService(session.userId, session.professorId);
                sessionService.RestartSession();
                MenuNavigator.Instance?.CloseAll();

                view.HideResetWarning();

                if (GameManager.Instance != null)
                    GameManager.Instance.StateMachine.ForceState(GameState.Config_Location);
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
