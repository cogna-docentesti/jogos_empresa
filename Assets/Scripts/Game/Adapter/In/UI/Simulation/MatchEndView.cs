// Assets/Scripts/Game/Adapter/In/UI/Simulation/MatchEndView.cs
// Panel_FinalReport (estado FinalReport) e Panel_Bankruptcy (estado GameOver_Bankruptcy).
// Mostra os meses fechados da partida, lidos do banco pelo serviço (SimulationMonthEntity).
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public class MatchEndView : MonoBehaviour
    {
        [SerializeField] private SystemicRoundFlow flow;
        [SerializeField] private bool bankruptcy;
        [SerializeField] private TextMeshProUGUI titleText, bodyText;

        private void OnEnable()
        {
            titleText.text = bankruptcy ? "Falência" : "Relatório final da partida";
            var reports = flow != null ? flow.MatchReports() : null;
            if (reports == null || reports.Count == 0)
            {
                bodyText.text = "Nenhum mês fechado encontrado para esta sessão.";
                return;
            }

            var sb = new StringBuilder();
            if (bankruptcy)
                sb.AppendLine("A empresa quebrou: três meses seguidos de prejuízo ou cheque especial acima do limite.\n");
            sb.AppendLine("<b>Mês<pos=14%>Lucro líquido<pos=40%>Caixa do mês<pos=64%>Pontos<pos=78%>Classificação</b>");
            foreach (var r in reports.OrderBy(r => r.Month))
                sb.AppendLine($"{r.Month}<pos=14%>{SimulationText.Money(r.NetIncome)}<pos=40%>{SimulationText.Money(r.CashResult)}<pos=64%>{r.Score}<pos=78%>{r.Classification}");

            sb.AppendLine();
            sb.AppendLine(SimulationText.Pair("Lucro acumulado", SimulationText.Money(reports.Sum(r => r.NetIncome))));
            sb.AppendLine(SimulationText.Pair("Pontuação média", reports.Average(r => r.Score).ToString("0", SimulationText.PtBr)));
            var last = reports.OrderBy(r => r.Month).Last();
            var cash = last.Balance.FirstOrDefault(b => b.Name == "Caixa");
            var overdraft = last.Balance.FirstOrDefault(b => b.Name == "Cheque especial");
            double cashEnd = (cash?.Value ?? 0) - (overdraft?.Value ?? 0);
            sb.Append(SimulationText.Pair("Caixa no fim da partida", SimulationText.Money(cashEnd)));
            bodyText.text = sb.ToString();
        }
    }
}
