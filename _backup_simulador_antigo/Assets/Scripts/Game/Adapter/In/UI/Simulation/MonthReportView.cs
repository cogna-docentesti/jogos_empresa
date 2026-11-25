using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI.Theme;
using Game.Application.Ports.Out;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI.Simulation
{
    /// <summary>
    /// Resumo do mes (estado Round_Summary) e "Ver DRE detalhada" durante o mes.
    /// Abas: DRE, Caixa, Balanco, Indicadores, Eventos, Analise. Todos os numeros vem do
    /// MonthlyReport calculado pelo dominio; a tela so formata.
    /// </summary>
    public sealed class MonthReportView : MonoBehaviour
    {
        public enum Tab { Income, Cash, Balance, Indicators, Events, Feedback }

        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text outlookText;

        [Header("Abas")]
        [SerializeField] private Button incomeTab;
        [SerializeField] private Button cashTab;
        [SerializeField] private Button balanceTab;
        [SerializeField] private Button indicatorsTab;
        [SerializeField] private Button eventsTab;
        [SerializeField] private Button feedbackTab;

        [Header("Conteudo")]
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private Transform rowsContainer;
        [SerializeField] private ReportRowView rowTemplate;

        [Header("Botoes")]
        [SerializeField] private Button continueButton;
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField] private Button closeButton;

        private MonthSimulationController _controller;
        private MonthlyReport _report;
        private IncomeStatement _partialIncome;
        private CashFlowStatement _partialCash;
        private IReadOnlyList<EventOccurrenceRecord> _partialEvents;
        private bool _partial;
        private Tab _tab = Tab.Income;
        private bool _wired;

        private GameObject Root => root != null ? root : gameObject;

        public void Bind(MonthSimulationController controller)
        {
            _controller = controller;
            if (_wired) return;
            _wired = true;
            Wire(incomeTab, Tab.Income);
            Wire(cashTab, Tab.Cash);
            Wire(balanceTab, Tab.Balance);
            Wire(indicatorsTab, Tab.Indicators);
            Wire(eventsTab, Tab.Events);
            Wire(feedbackTab, Tab.Feedback);
            if (continueButton != null) continueButton.onClick.AddListener(() => _controller.LeaveSummary());
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        private void Wire(Button button, Tab tab)
        {
            if (button != null) button.onClick.AddListener(() => { _tab = tab; Render(); });
        }

        public void Show(MonthlyReport report, MonthCommitResult commit)
        {
            _partial = false;
            _report = report;
            _tab = Tab.Feedback;
            Root.SetActive(true);
            SimulationUi.SetActive(continueButton, true);
            SimulationUi.SetActive(closeButton, false);
            if (continueLabel != null)
                continueLabel.text = commit != null && commit.Bankrupt ? "Ver encerramento"
                    : commit != null && commit.Completed ? "Ver relatório final"
                    : "Voltar ao hub e planejar o mês " + (report.month + 1);
            Render();
        }

        public void ShowPartial(MonthSimulation simulation)
        {
            _partial = true;
            _report = null;
            _partialIncome = simulation.PartialIncome();
            _partialCash = simulation.CashFlowSoFar();
            _partialEvents = simulation.OccurrencesSoFar();
            _tab = Tab.Income;
            Root.SetActive(true);
            SimulationUi.SetActive(continueButton, false);
            SimulationUi.SetActive(closeButton, true);
            if (titleText != null) titleText.text = "DRE parcial · até o dia " + Mathf.Max(1, simulation.Days.Count) + " de " + simulation.OperatingDays;
            if (scoreText != null) scoreText.text = "Valores por competência até hoje. Depreciação, balanço e pontuação entram no fechamento.";
            if (outlookText != null) outlookText.text = string.Empty;
            Render();
        }

        public void Hide() => Root.SetActive(false);

        // -----------------------------------------------------------------

        private void Render()
        {
            SetTabsVisible();
            HighlightTabs();
            if (rowsContainer == null || rowTemplate == null) return;
            SimulationUi.Clear(rowsContainer, rowTemplate.gameObject);
            var income = _partial ? _partialIncome : _report?.income;
            var cash = _partial ? _partialCash : _report?.cashFlow;
            if (!_partial && _report != null) RenderHeader(_report);
            switch (_tab)
            {
                case Tab.Income: if (income != null) RenderIncome(income); break;
                case Tab.Cash: if (cash != null) RenderCash(cash); break;
                case Tab.Balance: if (_report != null) RenderBalance(_report.balance); break;
                case Tab.Indicators: if (_report != null) RenderIndicators(_report.indicators); break;
                case Tab.Events: RenderEvents(_partial ? _partialEvents : _report?.events); break;
                case Tab.Feedback: if (_report != null) RenderFeedback(_report); break;
            }
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private void SetTabsVisible()
        {
            SimulationUi.SetActive(balanceTab, !_partial);
            SimulationUi.SetActive(indicatorsTab, !_partial);
            SimulationUi.SetActive(feedbackTab, !_partial);
            if (_partial && (_tab == Tab.Balance || _tab == Tab.Indicators || _tab == Tab.Feedback)) _tab = Tab.Income;
        }

        private void HighlightTabs()
        {
            Mark(incomeTab, _tab == Tab.Income);
            Mark(cashTab, _tab == Tab.Cash);
            Mark(balanceTab, _tab == Tab.Balance);
            Mark(indicatorsTab, _tab == Tab.Indicators);
            Mark(eventsTab, _tab == Tab.Events);
            Mark(feedbackTab, _tab == Tab.Feedback);
        }

        private static void Mark(Button button, bool on)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image != null) image.color = on ? GamePalette.Gold : GamePalette.SurfaceAlt;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null) label.color = on ? GamePalette.OnGold : GamePalette.Ink;
        }

        private void RenderHeader(MonthlyReport report)
        {
            var session = GameSessionState.Current;
            if (titleText != null) titleText.text = "Resultado do mês " + report.month + (session != null && !string.IsNullOrWhiteSpace(session.companyName) ? " · " + session.companyName : string.Empty);
            if (scoreText != null)
            {
                var score = report.scorecard;
                scoreText.text = "Pontuação do mês: <b>" + score.total + "</b> · " + score.classification
                                 + "   ·   Lucro líquido: " + SimulationUi.ColoredMoney(report.income.netIncome);
            }
            if (outlookText != null)
            {
                var o = report.outlook;
                outlookText.text = o == null || o.runs == 0 ? string.Empty
                    : "Se nada mudar (" + o.runs + " cenários simulados): crescimento " + SimFormat.Probability(o.growthProbability)
                      + " · estagnação " + SimFormat.Probability(o.stagnationProbability)
                      + " · encerramento " + SimFormat.Probability(o.closureProbability)
                      + "\nCaixa projetado: " + SimFormat.Money(o.p10CashEnd, false) + " a " + SimFormat.Money(o.p90CashEnd, false)
                      + " (mediana " + SimFormat.Money(o.medianCashEnd, false) + ")";
            }
        }

        private ReportRowView Row(string label, double value, bool emphasis = false, int indent = 0, bool signColor = false)
        {
            var row = SimulationUi.Spawn(rowTemplate, rowsContainer);
            row.Set(label, SimFormat.Money(value), signColor ? SimulationUi.AmountColor(value) : (Color?)null, emphasis, indent);
            return row;
        }

        private void Text(string label, string value, bool emphasis = false, int indent = 0)
            => SimulationUi.Spawn(rowTemplate, rowsContainer).Set(label, value, null, emphasis, indent);

        private void Paragraph(string text, Color color) => SimulationUi.Spawn(rowTemplate, rowsContainer).SetParagraph(text, color);

        private void RenderIncome(IncomeStatement i)
        {
            Row("Receita bruta de vendas", i.grossRevenue, emphasis: true);
            Row("(-) Impostos sobre vendas", -i.salesTaxes, indent: 1);
            Row("= Receita líquida", i.netRevenue, emphasis: true);
            Row("(-) CMV (ingredientes e embalagens vendidos)", -i.cmv, indent: 1);
            Row("= Lucro bruto", i.grossProfit, emphasis: true, signColor: true);
            Row("(-) Folha de pagamento e encargos", -i.payroll, indent: 1);
            Row("(-) Aluguel", -i.rent, indent: 1);
            Row("(-) Marketing", -i.marketing, indent: 1);
            Row("(-) Energia, água e limpeza", -i.utilities, indent: 1);
            Row("(-) Manutenção e perdas", -i.maintenanceAndLosses, indent: 1);
            if (i.spoilageLoss > 0.004d) Row("desperdício de estoque", -i.spoilageLoss, indent: 2);
            if (i.eventStockLoss > 0.004d) Row("perda de estoque por evento", -i.eventStockLoss, indent: 2);
            if (i.repairs > 0.004d) Row("consertos", -i.repairs, indent: 2);
            if (i.preventiveMaintenance > 0.004d) Row("manutenção preventiva", -i.preventiveMaintenance, indent: 2);
            if (i.assetWriteOffs > 0.004d) Row("baixa de equipamento", -i.assetWriteOffs, indent: 2);
            Row("(-) Outras despesas", -i.otherExpenses, indent: 1);
            Row("(+) Outras receitas", i.otherIncome, indent: 1);
            Row("(-) Depreciação", -i.depreciation, indent: 1);
            Row("= Resultado operacional", i.operatingResult, emphasis: true, signColor: true);
            Row("(-) Despesas financeiras (juros)", -i.financialExpenses, indent: 1);
            Row("= Lucro líquido", i.netIncome, emphasis: true, signColor: true);
        }

        private void RenderCash(CashFlowStatement c)
        {
            Row("Saldo inicial", c.openingCash, emphasis: true);
            Row("(+) Recebimento de vendas", c.salesReceipts, indent: 1);
            Row("(+) Outras entradas", c.otherReceipts, indent: 1);
            Row("(-) Fornecedores", -c.suppliers, indent: 1);
            Row("(-) Salários", -c.payroll, indent: 1);
            Row("(-) Aluguel", -c.rent, indent: 1);
            Row("(-) Energia, água e limpeza", -c.utilities, indent: 1);
            Row("(-) Impostos", -c.taxes, indent: 1);
            Row("(-) Marketing", -c.marketing, indent: 1);
            Row("(-) Manutenção", -c.maintenance, indent: 1);
            Row("(-) Outras saídas operacionais", -c.otherOperating, indent: 1);
            Row("(-) Pagamentos de eventos", -c.eventPayments, indent: 1);
            Row("(-) Juros do empréstimo", -c.loanInterest, indent: 1);
            Row("(-) Amortização do empréstimo", -c.loanPrincipal, indent: 1);
            Row("(-) Juros do cheque especial", -c.overdraftInterest, indent: 1);
            Row("Saldo final", c.closingCash, emphasis: true, signColor: true);
        }

        private void RenderBalance(BalanceSheet b)
        {
            Text("ATIVO", string.Empty, emphasis: true);
            Row("Caixa", b.cash, indent: 1);
            Row("Estoque", b.inventory, indent: 1);
            Row("Equipamentos (líquido de depreciação)", b.equipmentNet, indent: 1);
            Row("Total do ativo", b.TotalAssets, emphasis: true);
            Text("PASSIVO", string.Empty, emphasis: true);
            Row("Cheque especial", b.overdraft, indent: 1);
            Row("Fornecedores a pagar", b.suppliersPayable, indent: 1);
            Row("Contas a pagar", b.billsPayable, indent: 1);
            Row("Empréstimos", b.loans, indent: 1);
            Row("Total do passivo", b.TotalLiabilities, emphasis: true);
            Text("PATRIMÔNIO LÍQUIDO", string.Empty, emphasis: true);
            Row("Capital", b.capital + b.capitalAdjustment, indent: 1);
            Row("Lucros acumulados", b.retainedEarnings, indent: 1, signColor: true);
            Row("Total do patrimônio líquido", b.Equity, emphasis: true, signColor: true);
            if (System.Math.Abs(b.Difference) > 1d) Paragraph("Diferença de fechamento: " + SimFormat.Money(b.Difference), GamePalette.Danger);
        }

        private void RenderIndicators(MonthlyIndicators x)
        {
            Text("Ticket médio", SimFormat.Money(x.averageTicket));
            Text("CMV sobre a receita líquida", SimFormat.Percent(x.cmvPercent, 1));
            Text("Margem de contribuição", SimFormat.Percent(x.contributionMarginPercent, 1));
            Text("Ocupação da capacidade", SimFormat.Percent(x.capacityOccupancy, 0));
            Text("Pedidos perdidos", SimFormat.Number(x.lostOrders) + " (" + SimFormat.Percent(x.lostOrdersShare, 1) + ")");
            Text("por falta de capacidade", SimFormat.Number(x.lostToCapacity), indent: 1);
            Text("por falta de estoque", SimFormat.Number(x.lostToStock), indent: 1);
            Text("Tempo médio de espera", SimFormat.Number(x.waitAverage, 1) + " min");
            Text("NPS médio", SimFormat.Number(x.npsAverage));
            Text("Satisfação média (0 a 100)", SimFormat.Number(x.satisfactionAverage));
            Text("Produtividade da equipe", SimFormat.Number(x.teamProductivity, 1) + " pedidos/pessoa/dia");
            Text("Moral da equipe (0 a 100)", SimFormat.Number(x.moraleEnd));
            Text("Risco operacional (0 a 100)", SimFormat.Number(x.operationalRisk));
            Text("Competitividade (0 a 100)", SimFormat.Number(x.competitiveness));
            Text("Desperdício sobre o consumo", SimFormat.Percent(x.wasteShare, 1));
            Text("Liquidez corrente", Liquidity(x.currentLiquidity));
            Text("Liquidez seca", Liquidity(x.quickLiquidity));
            Text("Liquidez imediata", Liquidity(x.immediateLiquidity));
            Text("Endividamento", SimFormat.Percent(x.indebtedness, 1));
            Text("Cobertura de juros", x.interestCoverage >= 99d ? "sem juros" : SimFormat.Number(x.interestCoverage, 1) + "x");
            Text("Necessidade de capital de giro", SimFormat.Money(x.workingCapitalNeed));
            Text("Reserva de caixa recomendada", SimFormat.Money(x.recommendedCashReserve));
            Text("Ponto de equilíbrio contábil", BreakEven(x.breakEvenAccounting));
            Text("Ponto de equilíbrio financeiro", BreakEven(x.breakEvenFinancial));
            Text("Ponto de equilíbrio econômico", BreakEven(x.breakEvenEconomic));
            Text("Dias no cheque especial", x.overdraftDays.ToString());
            Text("Contas pagas com atraso", x.overdueBills.ToString());
        }

        private static string Liquidity(double value) => value >= 99d ? "sem dívidas de curto prazo" : SimFormat.Number(value, 2);

        private static string BreakEven(double orders) => orders < 0d ? "sem margem de contribuição" : SimFormat.Number(orders) + " pedidos/mês";

        private void RenderEvents(IReadOnlyList<EventOccurrenceRecord> events)
        {
            if (events == null || events.Count == 0)
            {
                Paragraph("Nenhum evento neste período.", GamePalette.Muted);
                return;
            }
            foreach (var e in events.OrderBy(e => e.day))
            {
                string reaction = string.IsNullOrEmpty(e.reactionTitle) ? string.Empty : " → " + e.reactionTitle + (e.automatic ? " (automático)" : string.Empty);
                string impact = System.Math.Abs(e.cashImpact) > 0.004d ? "  " + SimulationUi.ColoredMoney(e.cashImpact) : string.Empty;
                string reputation = e.reputationImpact != 0 ? "  reputação " + (e.reputationImpact > 0 ? "+" : string.Empty) + e.reputationImpact : string.Empty;
                string chance = e.probability > 0d && e.probability < 1d ? "\n<size=80%><color=" + GamePalette.HexMuted + ">chance no dia: " + SimFormat.Probability(e.probability)
                                + (string.IsNullOrEmpty(e.explanation) ? string.Empty : " · " + e.explanation) + "</color></size>" : string.Empty;
                Paragraph("<b>Dia " + e.day + "</b> · <color=" + SimulationUi.Hex(SimulationUi.ImpactColor(e.impact)) + ">" + e.title + "</color>" + reaction + impact + reputation + chance, GamePalette.Ink);
            }
        }

        private void RenderFeedback(MonthlyReport report)
        {
            foreach (var message in report.feedback)
                Paragraph(message.text, SimulationUi.ToneColor(message.tone));
            if (report.demandFactors.Count > 0)
            {
                Text("O que explicou a demanda", string.Empty, emphasis: true);
                foreach (var factor in report.demandFactors.OrderBy(f => f.contributionPoints))
                    Text(factor.label, (factor.contributionPoints >= 0 ? "+" : string.Empty) + SimFormat.Number(factor.contributionPoints, 1) + " p.p.", indent: 1);
            }
            Text("Pontuação", report.scorecard.total + " · " + report.scorecard.classification, emphasis: true);
            foreach (var line in report.scorecard.lines)
                Text(line.criterion, (line.points > 0 ? "+" : string.Empty) + line.points, indent: 1);
        }
    }
}
