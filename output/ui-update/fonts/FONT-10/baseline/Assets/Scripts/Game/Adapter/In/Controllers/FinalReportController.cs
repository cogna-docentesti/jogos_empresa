using System;
using Game.Adapter.In.UI;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class FinalReportController : MonoBehaviour
    {
        [SerializeField] private FinalReportView view;
        [SerializeField] private MonthlyResultController monthly;
        [SerializeField] private QuarterlyResultController quarterly;
        public FinalReportPresentation.Report Report { get; private set; }
        private void OnEnable() { view.Actions(OpenMonth, OpenQuarterly, Refresh, StartNewGame); Refresh(); }
        public void Refresh()
        {
            Report = null;
            try
            {
                var s = GameSessionState.Current;
                if (s == null || s.status != GameSessionStatus.COMPLETED)
                { view.State("empty", "Nenhuma campanha concluída disponível", "Os resultados e as escolhas serão apresentados após a conclusão dos três meses. Uma campanha em falência segue seu próprio destino."); return; }
                var db = DatabaseInitializer.DatabaseService?.Connection;
                if (db == null) throw new InvalidOperationException("Database unavailable.");
                var saved = new GameSessionRepository(db).GetById(s.sessionId);
                if (saved == null || saved.status != GameSessionStatus.COMPLETED)
                { view.State("empty", "Campanha concluída não encontrada", "Não há uma campanha concluída salva para esta consulta."); return; }
                Report = FinalReportPresentation.Build(saved, new RoundResultRepository(db).GetBySessionId(saved.sessionId), new SessionEventHistoryRepository(db).GetBySession(saved.sessionId));
                view.Show(Report);
            }
            catch (Exception)
            { view.State("error", "Não foi possível consultar o relatório", "Os registros salvos estão indisponíveis ou inválidos. Tente novamente. Nenhum resultado foi alterado."); }
        }
        public void OpenMonth(int month)
        {
            if (month < 1 || month > 3 || Report?.Quarter.Months[month - 1] == null) return;
            var focus = view.MonthButtons[month - 1];
            monthly.OpenFromFinal(month, () => Return(focus));
        }
        public void OpenQuarterly()
        {
            if (Report == null) return;
            var focus = view.QuarterlyButton;
            quarterly.OpenFromFinal(() => Return(focus));
        }
        private void Return(UnityEngine.UI.Button focus)
        {
            if (GameManager.Instance?.StateMachine?.CurrentState != GameState.FinalReport) return;
            UIStateListener.PresentScreen(gameObject); focus.Select();
        }
        public void StartNewGame()
        {
            try
            {
                var s = GameSessionState.Current;
                if (s == null) return;
                new GameSessionService(s.userId, s.professorId).StartNewCampaignPreservingReport();
                view.CloseNewGame(); GameManager.Instance.BeginNewIdentification();
            }
            catch (Exception) { view.NewGameFailure("Não foi possível iniciar a nova campanha. Seu relatório continua salvo. Tente novamente."); }
        }
    }
}
