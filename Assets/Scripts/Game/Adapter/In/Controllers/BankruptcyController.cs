using System;
using Game.Adapter.In.UI;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class BankruptcyController : MonoBehaviour
    {
        [SerializeField] private BankruptcyView view;
        [SerializeField] private MonthlyResultController monthly;
        [SerializeField] private QuarterlyResultController quarterly;
        public BankruptcyPresentation.Report Report { get; private set; }
        private void OnEnable() { view.Actions(OpenMonth, OpenLastMonth, OpenQuarterly, Refresh, StartNewGame); Refresh(); }
        public void Refresh()
        {
            Report = null;
            try
            {
                var s = GameSessionState.Current;
                if (s == null || s.status != GameSessionStatus.BANKRUPT)
                { view.State("empty", "Nenhum encerramento por falência disponível", "O saldo negativo isolado não encerra a campanha. Esta tela apresenta apenas uma falência registrada após três meses consecutivos de prejuízo."); return; }
                var db = DatabaseInitializer.DatabaseService?.Connection;
                if (db == null) throw new InvalidOperationException("Database unavailable.");
                var saved = new GameSessionRepository(db).GetById(s.sessionId);
                if (saved == null || saved.status != GameSessionStatus.BANKRUPT)
                { view.State("empty", "Encerramento não encontrado", "Não foi encontrado um registro de falência salvo para esta campanha."); return; }
                Report = BankruptcyPresentation.Build(saved, new RoundResultRepository(db).GetBySessionId(saved.sessionId));
                view.Show(Report);
            }
            catch (Exception)
            { view.State("error", "Não foi possível consultar o encerramento", "Os registros salvos estão indisponíveis ou inválidos. Tente novamente. Seus resultados continuam preservados."); }
        }
        public void OpenMonth(int month)
        {
            if (month < 1 || month > 3 || Report?.Quarter.Months[month - 1] == null) return;
            Open(month, view.MonthButtons[month - 1]);
        }
        public void OpenLastMonth() { if (Report?.LastMonth > 0) Open(Report.LastMonth, view.ReviewButton); }
        private void Open(int month, UnityEngine.UI.Button focus)
        { monthly.OpenFromFinal(month, () => Return(focus)); }
        public void OpenQuarterly()
        { if (Report == null) return; quarterly.OpenFromFinal(() => Return(view.QuarterlyButton)); }
        private void Return(UnityEngine.UI.Button focus)
        { if (GameManager.Instance?.StateMachine?.CurrentState != GameState.GameOver_Bankruptcy) return; UIStateListener.PresentScreen(gameObject); focus.Select(); }
        public void StartNewGame()
        {
            try
            {
                var s = GameSessionState.Current;
                if (s == null || s.status != GameSessionStatus.BANKRUPT) return;
                new GameSessionService(s.userId, s.professorId).StartNewCampaignPreservingReport();
                view.CloseNewGame(); GameManager.Instance.BeginNewIdentification();
            }
            catch (Exception) { view.NewGameFailure("Não foi possível iniciar o novo jogo. A campanha e os resultados continuam salvos. Tente novamente ou continue consultando."); }
        }
    }
}
