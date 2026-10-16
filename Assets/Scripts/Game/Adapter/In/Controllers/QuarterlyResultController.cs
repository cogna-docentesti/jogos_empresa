using System;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class QuarterlyResultController : MonoBehaviour
    {
        [SerializeField] private QuarterlyResultView view;
        [SerializeField] private GameObject monthlyPanel;
        private bool fromMonthly;
        public QuarterlyResultPresentation.Report Report { get; private set; }
        private void OnEnable() { view.Actions(Back, Refresh); Refresh(); }
        public void OpenFromMonthly()
        {
            fromMonthly = true;
            monthlyPanel.SetActive(false);
            gameObject.SetActive(true);
        }
        public void Refresh()
        {
            Report = null;
            try
            {
                var s = GameSessionState.Current;
                if (s == null) { view.State("empty", "Nenhuma campanha disponível", "Inicie uma campanha e conclua um mês para consultar o trimestre."); return; }
                var db = DatabaseInitializer.DatabaseService?.Connection;
                if (db == null) throw new InvalidOperationException("Database unavailable.");
                var data = QuarterlyResultPresentation.Build(s.sessionId, new RoundResultRepository(db).GetBySessionId(s.sessionId));
                if (data.Count == 0) { view.State("empty", "Nenhum mês salvo neste trimestre", "Os três períodos serão preenchidos após cada fechamento. Não há resultados persistidos nesta campanha."); return; }
                view.Show(data); Report = data;
            }
            catch (Exception)
            { view.State("error", "Não foi possível consultar o trimestre", "O histórico salvo está indisponível ou contém registros inválidos. Tente novamente para atualizar a consulta."); }
        }
        public void Back()
        {
            if (fromMonthly)
            {
                fromMonthly = false; gameObject.SetActive(false); monthlyPanel.SetActive(true);
                monthlyPanel.GetComponent<MonthlyResultView>().QuarterlyButton.Select();
            }
            else MenuNavigator.Instance?.Back();
        }
        private void OnDisable() { fromMonthly = false; }
        private void Update()
        {
            // A state transition owns visibility; never leave a consultation above a different flow.
            if (fromMonthly && GameManager.Instance?.StateMachine?.CurrentState != GameState.Round_Summary && GameManager.Instance?.StateMachine?.CurrentState != GameState.Management_Hub)
            { fromMonthly = false; gameObject.SetActive(false); }
        }
    }
}
