using System.Linq;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI.Theme;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI.Simulation
{
    /// <summary>
    /// Relatorio final (estado FinalReport) e encerramento por falencia (GameOver_Bankruptcy).
    /// Le os relatorios gravados de cada mes (SimulationMonthEntity) pelo caso de uso.
    /// </summary>
    public sealed class FinalReportView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text verdictText;
        [SerializeField] private Transform rowsContainer;
        [SerializeField] private ReportRowView rowTemplate;
        [SerializeField] private Button canvasButton;
        [SerializeField] private BusinessModelCanvasView canvasView;

        private MonthSimulationController _controller;
        private bool _wired;

        private GameObject Root => root != null ? root : gameObject;

        public void Bind(MonthSimulationController controller)
        {
            _controller = controller;
            if (_wired) return;
            _wired = true;
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
            if (canvasButton != null) canvasButton.onClick.AddListener(() =>
            {
                var canvas = _controller.Service.Canvas();
                if (canvasView != null && canvas != null) canvasView.Show(canvas);
            });
        }

        public void Show(string sessionId, bool bankrupt)
        {
            if (_controller == null) return;
            Root.SetActive(true);
            var reports = _controller.SavedReports();
            if (titleText != null) titleText.text = bankrupt ? "Encerramento das atividades" : "Relatório final do trimestre";
            if (verdictText != null)
            {
                double total = reports.Sum(r => r.income.netIncome);
                var last = reports.LastOrDefault();
                verdictText.text = reports.Count == 0
                    ? "Nenhum mês foi simulado pelo motor sistêmico nesta partida."
                    : (bankrupt ? "O restaurante encerrou as atividades no mês " + last.month + ". " : "Trimestre concluído. ")
                      + "Lucro líquido acumulado: " + SimulationUi.ColoredMoney(total)
                      + " · Caixa final: " + SimulationUi.ColoredMoney(last.cashFlow.closingCash)
                      + " · Patrimônio líquido: " + SimulationUi.ColoredMoney(last.balance.Equity);
            }
            if (rowsContainer == null || rowTemplate == null) return;
            SimulationUi.Clear(rowsContainer, rowTemplate.gameObject);
            foreach (var report in reports)
            {
                var header = SimulationUi.Spawn(rowTemplate, rowsContainer);
                header.Set("Mês " + report.month, report.scorecard.total + " pontos · " + report.scorecard.classification, null, emphasis: true);
                Row("Receita bruta", report.income.grossRevenue);
                Row("Lucro líquido", report.income.netIncome, sign: true);
                Row("Caixa no fim do mês", report.cashFlow.closingCash, sign: true);
                var nps = SimulationUi.Spawn(rowTemplate, rowsContainer);
                nps.Set("NPS médio · Clientes atendidos", SimFormat.Number(report.indicators.npsAverage) + " · " + SimFormat.Number(report.cashResult.customers), null, false, 1);
                var key = report.feedback.FirstOrDefault(f => f.tone == FeedbackTone.Negative || f.tone == FeedbackTone.Warning) ?? report.feedback.FirstOrDefault();
                if (key != null) SimulationUi.Spawn(rowTemplate, rowsContainer).SetParagraph(key.text, SimulationUi.ToneColor(key.tone));
            }
        }

        private void Row(string label, double value, bool sign = false)
        {
            var row = SimulationUi.Spawn(rowTemplate, rowsContainer);
            row.Set(label, SimFormat.Money(value), sign ? SimulationUi.AmountColor(value) : (Color?)null, false, 1);
        }

        public void Hide() => Root.SetActive(false);
    }
}
