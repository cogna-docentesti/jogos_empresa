using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Game.Adapter.In.UI
{
    /// <summary>Read-only aggregation of saved cash statements. No forecast or settlement.</summary>
    public static class QuarterlyResultPresentation
    {
        public sealed class Cell
        {
            public string Text;
            public double? Amount;
            public Cell(string text, double? amount = null) { Text = text; Amount = amount; }
        }
        public sealed class Row
        {
            public string Label, Rule;
            public Cell[] Months;
            public Cell Aggregate, Change;
            public bool Monetary, Highlight;
        }
        public sealed class Report
        {
            public RoundResultEntity[] Months;
            public List<Row> Rows = new();
            public int Count, First, Last;
            public bool Complete => Count == 3;
            public string Period => Complete ? "TRIMESTRE COMPLETO • 3/3 MESES SALVOS" : $"TRIMESTRE PARCIAL • {Count}/3 MESES SALVOS";
            public string ChangeLabel => Count < 2 ? "Δ indisponível" : $"Δ M{Last} − M{First}";
            public double Revenue, Net, Opening, Closing;
        }
        private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
        public static string Money(double value) => value.ToString("C2", Pt);
        public static string SignedMoney(double value) => (value > 0 ? "+" : "") + Money(value);
        // Persisted floats approximate cent values. Sum the displayed cents, never a new settlement.
        private static double Cents(double value) => Math.Round(value, 2);
        private static string Number(double value) => value.ToString("N0", Pt);
        private static Cell Missing() => new("Não salvo");

        public static Report Build(string sessionId, IEnumerable<RoundResultEntity> saved)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("A campaign is required.");
            if (saved == null) throw new ArgumentNullException(nameof(saved));
            var list = saved.ToArray();
            if (list.Any(r => r == null || r.sessionId != sessionId || r.round < 1 || r.round > 3)
                || list.GroupBy(r => r.round).Any(g => g.Count() != 1))
                throw new InvalidOperationException("Invalid campaign history.");
            foreach (var r in list) MonthlyResultPresentation.Build(r); // Validate the same persisted fields as the monthly view.
            var report = new Report { Months = new RoundResultEntity[3], Count = list.Length };
            foreach (var r in list) report.Months[r.round - 1] = r;
            if (list.Length == 0) return report;
            var ordered = list.OrderBy(r => r.round).ToArray();
            var first = ordered[0]; var last = ordered[ordered.Length - 1];
            report.First = first.round; report.Last = last.round;
            report.Revenue = ordered.Sum(r => Cents(r.grossRevenue));
            report.Net = ordered.Sum(r => Cents(r.netResult));
            report.Opening = first.openingCash; report.Closing = last.closingCash;

            void Metric(string label, string rule, Func<RoundResultEntity, double> get,
                Func<IEnumerable<RoundResultEntity>, double?> aggregate, Func<double, string> format,
                bool money = false, bool highlight = false, string changeSuffix = "")
            {
                double? total = aggregate(ordered);
                Func<RoundResultEntity, double> read = money ? r => Cents(get(r)) : get;
                double delta = read(last) - read(first);
                report.Rows.Add(new Row { Label = label, Rule = rule, Monetary = money, Highlight = highlight,
                    Months = report.Months.Select(r => r == null ? Missing() : new Cell(format(read(r)), read(r))).ToArray(),
                    Aggregate = total.HasValue ? new Cell(format(total.Value), total) : new Cell("Sem clientes"),
                    Change = ordered.Length < 2 ? new Cell("Requer 2 meses") : new Cell((money ? SignedMoney(delta) : (delta > 0 && !format(delta).StartsWith("+") ? "+" : "") + format(delta)) + changeSuffix, delta) });
            }
            void Flow(string label, Func<RoundResultEntity, double> get, bool highlight = false) =>
                Metric(label, "Soma dos meses salvos", r => Cents(get(r)), rs => rs.Sum(r => Cents(get(r))), SignedMoney, true, highlight);
            Flow("Receita de vendas", r => r.grossRevenue);
            Flow("Insumos", r => -r.supplyCost);
            Flow("Aluguel", r => -r.rent);
            Flow("Folha de pagamento", r => -r.salaries);
            Flow("Despesas operacionais", r => -r.utilities);
            Flow("13º salário", r => -r.thirteenthSalary);
            Flow("Parcela • principal + juros", r => -r.loanPayment);
            Flow("Saídas • inclui a parcela", r => -r.TotalCosts, true);
            Flow("Eventos • impacto assinado", r => r.eventCashImpact);
            Flow("Resultado de caixa", r => r.netResult, true);
            Metric("Caixa na abertura", $"Abertura do primeiro salvo (M{first.round})", r => r.openingCash, _ => first.openingCash, Money, true);
            Metric("Caixa no fechamento", $"Fechamento do último salvo (M{last.round})", r => r.closingCash, _ => last.closingCash, Money, true, true);
            Flow("Principal • já na parcela", r => -r.loanPrincipalPayment);
            Flow("Juros • já na parcela", r => -(r.loanPayment - r.loanPrincipalPayment));
            Metric("Clientes atendidos", "Soma dos meses salvos", r => r.customers, rs => rs.Sum(r => (double)r.customers), Number);
            Metric("Demanda base", "Soma dos meses salvos", r => r.baseDemand, rs => rs.Sum(r => (double)r.baseDemand), Number);
            Metric("Demanda potencial", "Soma dos meses salvos", r => r.potentialDemand, rs => rs.Sum(r => (double)r.potentialDemand), Number);
            Metric("Capacidade de atendimento", "Soma das capacidades mensais", r => r.serviceCapacity, rs => rs.Sum(r => (double)r.serviceCapacity), Number);
            Metric("Ticket médio", "Média ponderada por clientes", r => r.averageTicket,
                rs => rs.Sum(r => (double)r.customers) == 0 ? null : rs.Sum(r => Cents(r.averageTicket) * r.customers) / rs.Sum(r => (double)r.customers), Money, true);
            Metric("Pontuação de alinhamento", "Média simples dos meses salvos", r => r.alignmentScore, rs => rs.Average(r => r.alignmentScore), v => v.ToString("0.##", Pt), changeSuffix: " pontos");
            report.Rows.Add(new Row { Label = "Classificação de alinhamento", Rule = $"Última salva (M{last.round}); sem soma",
                Months = report.Months.Select(r => r == null ? Missing() : new Cell(MonthlyResultPresentation.ClassificationLabel(r.alignmentClassification))).ToArray(),
                Aggregate = new Cell(MonthlyResultPresentation.ClassificationLabel(last.alignmentClassification)), Change = new Cell("Não se aplica") });
            Metric("Fator de coerência", "Média simples dos meses salvos", r => r.coherenceFactor, rs => rs.Average(r => (double)r.coherenceFactor), v => v.ToString("0.##", Pt) + "x");
            Metric("Reputação na abertura", $"Primeira salva (M{first.round})", r => r.reputationAtStart, _ => first.reputationAtStart, Number, changeSuffix: " pontos");
            Metric("Reputação no fechamento", $"Última salva (M{last.round})", r => r.reputationAtEnd, _ => last.reputationAtEnd, Number, changeSuffix: " pontos");
            Metric("Variação de reputação", "Soma dos deltas mensais salvos", r => r.reputationDelta, rs => rs.Sum(r => (double)r.reputationDelta), v => (v > 0 ? "+" : "") + Number(v));
            return report;
        }
    }
}
