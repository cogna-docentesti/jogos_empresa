using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Domain.Service;
using Game.Infrastructure;
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
        [SerializeField] private EstablishmentReviewContent reviewContent;
        private bool resetting;
        private int lastResetFrame = -1;

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

        /// <summary>
        /// Botao Resetar e botao "Iniciar nova sessao" do aviso de save
        /// incompativel (UIStateListener). Jogo do comeco:
        ///  1. RestartSession (Thaysla) apaga meses, eventos e a sessao numa
        ///     transacao so e cria uma sessao nova SO em memoria.
        ///  2. Apaga o cadastro salvo, para a identificacao abrir vazia.
        ///  3. Abre o painel de identificacao na mesma GameScene.
        /// </summary>
        public void ResetGame()
        {
            if (resetting || lastResetFrame == Time.frameCount) return;
            GameSessionEntity session = GameSessionState.Current ?? GameSessionState.IncompatibleSession;
            var databaseService = DatabaseInitializer.DatabaseService;

            if (session == null || databaseService == null || databaseService.Connection == null)
            {
                view?.ShowResetFailure();
                return;
            }

            try
            {
                resetting = true;
                string oldSessionId = session.sessionId;

                var sessionService = new GameSessionService(session.userId, session.professorId);
                sessionService.RestartSession();
                lastResetFrame = Time.frameCount;
                Debug.Log($"[Banco] RESET: sessao {oldSessionId} apagada (meses, eventos e sessao). Nova sessao so em memoria.");

                PlayerSession.ClearSavedIdentification();
                view?.HideResetWarning();
                MenuNavigator.Instance?.CloseAll();

                if (GameManager.Instance != null)
                    GameManager.Instance.BeginNewIdentification();
            }
            catch (System.Exception exception)
            {
                view?.ShowResetFailure();
                Debug.LogWarning("[EstablishmentSummaryController] Falha ao resetar o jogo: " + exception.Message);
            }
            finally { resetting = false; }
        }

        public void Refresh()
        {
            if (reviewContent != null)
            {
                reviewContent.Bind(EstablishmentSummaryService.Build());
                return;
            }
            if (view == null)
            {
                Debug.LogError("[EstablishmentSummaryController] View nao configurada.");
                return;
            }

            view.Bind(EstablishmentSummaryService.Build());
        }
    }
}
