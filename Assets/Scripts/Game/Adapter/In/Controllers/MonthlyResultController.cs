using System;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    /// <summary>Queries a saved statement. An explicit action releases Round_Summary.</summary>
    public sealed class MonthlyResultController : MonoBehaviour
    {
        [SerializeField] private MonthlyResultView view;
        public RoundResultEntity Result { get; private set; }
        public bool IsAwaitingContinue => GameManager.Instance?.StateMachine?.CurrentState == GameState.Round_Summary;
        public event Action<RoundResultEntity> Continued;
        private void OnEnable() { view.Actions(Continue, Refresh); Refresh(); }
        public void Refresh()
        {
            Result = null;
            try
            {
                var session = GameSessionState.Current;
                if (session == null) { view.State("empty", "Nenhum resultado disponível", "Inicie e conclua um mês para consultar sua DRE. Não há resultados salvos nesta sessão.", IsAwaitingContinue); return; }
                var db = DatabaseInitializer.DatabaseService?.Connection;
                if (db == null) throw new InvalidOperationException("Banco de dados indisponível. Tente consultar novamente.");
                var saved = new RoundResultRepository(db).GetLastRoundBySessionId(session.sessionId);
                if (saved == null) { view.State("empty", "Seu primeiro resultado ainda não está disponível", "A DRE será exibida após o fechamento do mês e a confirmação das decisões de evento.", IsAwaitingContinue); return; }
                view.Show(saved, IsAwaitingContinue); Result = saved;
            }
            catch (Exception)
            { view.State("error", "Não foi possível consultar o resultado", "O resultado salvo está indisponível. Tente novamente para atualizar esta consulta.", IsAwaitingContinue); }
        }
        public void Continue()
        {
            if (IsAwaitingContinue)
            {
                if (Result == null) return;
                var session = GameSessionState.Current;
                try
                {
                    if (session == null || !new GameSessionService(session.userId, session.professorId).ContinueAfterSummary(Result.roundResultId)) return;
                }
                catch (Exception) { Refresh(); return; }
                Continued?.Invoke(Result);
            }
            else MenuNavigator.Instance?.Back();
        }
    }
}
