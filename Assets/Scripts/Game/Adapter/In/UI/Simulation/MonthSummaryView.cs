// Assets/Scripts/Game/Adapter/In/UI/Simulation/MonthSummaryView.cs
// Panel_MonthSummary (estado Round_Summary). Lê o relatório que o SystemicRoundFlow guardou
// no fechamento e desenha DRE, caixa, balanço, indicadores, pontuação e feedback.
// Nenhuma conta aqui: todos os números vêm prontos do MonthReport.
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.Controllers
{
    public class MonthSummaryView : MonoBehaviour
    {
        [SerializeField] private SystemicRoundFlow flow;
        [SerializeField] private TextMeshProUGUI titleText, headlineText, dreText, cashText, balanceText, indicatorsText, scoreText, feedbackText;
        [SerializeField] private Button continueButton;
        [SerializeField] private TextMeshProUGUI continueLabel;

        private void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(() => { if (flow != null) flow.LeaveSummary(); });
        }

        private void OnEnable()
        {
            var report = flow != null ? flow.LastReport : null;
            if (report == null)
            {
                titleText.text = "Resumo do mês";
                headlineText.text = "Nenhum mês fechado nesta execução.";
                return;
            }
            Render(report, flow.Service != null ? flow.Service.MonthsPerMatch : 3);
        }

        public void Render(MonthReport r, int monthsPerMatch)
        {
            titleText.text = $"Resumo do mês {r.Month} de {monthsPerMatch}";
            headlineText.text = $"Lucro líquido {SimulationText.Money(r.NetIncome)}  ·  Resultado de caixa {SimulationText.Money(r.CashResult)}  ·  {r.Classification}";

            dreText.text = SimulationText.Block("DRE (competência)",
                r.Dre.Where(v => System.Math.Abs(v.Value) > 0.005).Select(v => (SimulationText.DreLabel(v.Name), SimulationText.DreSign(v.Name) * v.Value)),
                ("Lucro líquido", r.NetIncome));

            cashText.text = SimulationText.Block("Fluxo de caixa",
                r.CashFlow.Where(v => System.Math.Abs(v.Value) > 0.005).Select(v => (SimulationText.CashLabel(v.Name), v.Value)),
                ("Resultado de caixa", r.CashResult));

            var sb = new StringBuilder("<b>Balanço no dia 30</b>\n");
            foreach (var v in r.Balance) sb.AppendLine(SimulationText.Line(v.Name, v.Value));
            sb.Append(SimulationText.Line("Diferença (tem que ser 0)", r.BalanceDifference));
            balanceText.text = sb.ToString();

            indicatorsText.text =
                "<b>Indicadores</b>\n" +
                SimulationText.Pair("Pedidos atendidos", r.Orders.ToString("N0", SimulationText.PtBr)) + "\n" +
                SimulationText.Pair("Pedidos perdidos", r.LostRatio.ToString("P1", SimulationText.PtBr)) + "\n" +
                SimulationText.Pair("Ticket médio", SimulationText.Money(r.AvgTicket, 2)) + "\n" +
                SimulationText.Pair("CMV / receita", r.CmvRatio.ToString("P1", SimulationText.PtBr)) + "\n" +
                SimulationText.Pair("NPS médio", r.NpsAverage.ToString("0.0", SimulationText.PtBr)) + "\n" +
                SimulationText.Pair("Liquidez corrente", r.CurrentLiquidity.ToString("0.00", SimulationText.PtBr)) + "\n" +
                SimulationText.Pair("Reputação", $"{r.ReputationStart:0.0} no início, {r.ReputationEnd:0.0} no fim");

            scoreText.text = $"<b>Pontuação: {r.Score}</b>\n" + string.Join("\n", r.ScoreItems);
            feedbackText.text = string.Join("\n\n", r.Feedback);

            if (continueLabel != null)
                continueLabel.text = r.Bankrupt ? "VER FALÊNCIA" : r.Month >= monthsPerMatch ? "VER RELATÓRIO FINAL" : "CONTINUAR";
        }
    }

    /// Formatação compartilhada pelas telas do simulador (só texto, nenhuma regra).
    public static class SimulationText
    {
        public static readonly CultureInfo PtBr = new CultureInfo("pt-BR");

        public static string Money(double value, int decimals = 0)
            => (value < 0 ? "-R$ " : "R$ ") + System.Math.Abs(value).ToString(decimals == 0 ? "N0" : "N" + decimals, PtBr);

        public static string Line(string label, double value)
        {
            string color = value < -0.005 ? "#EF5350" : "#FFFFFF";
            return $"{label}<pos=62%><color={color}>{Money(value)}</color>";
        }

        public static string Pair(string label, string value) => $"{label}<pos=62%>{value}";

        public static string Block(string title, IEnumerable<(string label, double value)> lines, (string label, double value) total)
        {
            var sb = new StringBuilder($"<b>{title}</b>\n");
            foreach (var (label, value) in lines) sb.AppendLine(Line(label, value));
            sb.Append($"<b>{Line(total.label, total.value)}</b>");
            return sb.ToString();
        }

        /// Despesas aparecem com sinal negativo na tela; receitas positivas.
        public static double DreSign(string line) => line == nameof(DreLine.GrossRevenue) || line == nameof(DreLine.OtherIncome) ? 1 : -1;

        public static string DreLabel(string line)
        {
            switch (line)
            {
                case nameof(DreLine.GrossRevenue): return "Receita bruta";
                case nameof(DreLine.Taxes): return "Impostos sobre vendas";
                case nameof(DreLine.Cmv): return "CMV (insumos)";
                case nameof(DreLine.Payroll): return "Folha e encargos";
                case nameof(DreLine.Rent): return "Aluguel";
                case nameof(DreLine.Marketing): return "Marketing e fidelização";
                case nameof(DreLine.Utilities): return "Energia, água e taxas";
                case nameof(DreLine.MaintenanceAndLosses): return "Manutenção e perdas";
                case nameof(DreLine.OtherExpenses): return "Outras despesas";
                case nameof(DreLine.OtherIncome): return "Outras receitas";
                case nameof(DreLine.Depreciation): return "Depreciação";
                case nameof(DreLine.Interest): return "Juros";
                default: return line;
            }
        }

        public static string CashLabel(string line)
        {
            switch (line)
            {
                case nameof(CashLine.Sales): return "Vendas recebidas";
                case nameof(CashLine.OtherInflows): return "Outras entradas";
                case nameof(CashLine.Suppliers): return "Fornecedores";
                case nameof(CashLine.Salaries): return "Salários e encargos";
                case nameof(CashLine.Rent): return "Aluguel";
                case nameof(CashLine.Bills): return "Contas de consumo";
                case nameof(CashLine.Taxes): return "Impostos";
                case nameof(CashLine.Marketing): return "Marketing, fidelização e treinamento";
                case nameof(CashLine.Maintenance): return "Manutenção";
                case nameof(CashLine.Events): return "Reações a eventos";
                case nameof(CashLine.Interest): return "Juros";
                case nameof(CashLine.Amortization): return "Amortização do empréstimo";
                default: return line;
            }
        }
    }
}
