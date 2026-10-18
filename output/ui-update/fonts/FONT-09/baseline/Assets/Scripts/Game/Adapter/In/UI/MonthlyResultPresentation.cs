using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Adapter.In.UI
{
    /// <summary>Read-only labels for the saved cash-result model. Never settles or updates a session.</summary>
    public static class MonthlyResultPresentation
    {
        public sealed class Row
        {
            public string Label, Value;
            public float Amount;
            public bool Monetary, Total;
        }
        private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");
        public static string Money(float value) => ((double)value).ToString("C2", Pt);
        public static string SignedMoney(float value) => (value > 0 ? "+" : "") + Money(value);
        public static string Number(int value) => value.ToString("N0", Pt);
        public static string ClassificationLabel(string value) => value switch
        {
            "HIGH" => "Alto", "ADEQUATE" => "Adequado", "FRAGILE" => "Frágil", "CRITICAL" => "Crítico",
            _ => string.IsNullOrWhiteSpace(value) ? "Não registrada" : value
        };
        public static IReadOnlyList<Row> Build(RoundResultEntity r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            foreach (var value in new[] { r.grossRevenue, r.supplyCost, r.rent, r.salaries, r.utilities,
                r.thirteenthSalary, r.loanPayment, r.loanPrincipalPayment, r.eventCashImpact, r.netResult,
                r.openingCash, r.closingCash, r.averageTicket, r.coherenceFactor, r.TotalCosts })
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidOperationException("O resultado salvo contém valores inválidos.");
            var rows = new List<Row>();
            void Cash(string label, float value, bool total = false) => rows.Add(new Row { Label = label, Value = SignedMoney(value), Amount = value, Monetary = true, Total = total });
            void Metric(string label, string value) => rows.Add(new Row { Label = label, Value = value });
            Cash("Receita de vendas", r.grossRevenue);
            Cash("Insumos", -r.supplyCost);
            Cash("Aluguel", -r.rent);
            Cash("Folha de pagamento", -r.salaries);
            Cash("Despesas operacionais", -r.utilities);
            Cash("13º salário", -r.thirteenthSalary);
            Cash("Parcela de empréstimo • principal + juros", -r.loanPayment);
            Cash("Saídas do mês • inclui a parcela", -r.TotalCosts, true);
            Cash("Impacto dos eventos • entrada ou saída", r.eventCashImpact);
            Cash("Resultado do mês • modelo de caixa", r.netResult, true);
            Cash("Caixa no início do mês", r.openingCash);
            Cash("Caixa no fechamento", r.closingCash, true);
            // Details only: these two lines are already included in the loan payment above.
            Cash("Principal já incluído na parcela", -r.loanPrincipalPayment);
            Cash("Juros já incluídos na parcela", -(r.loanPayment - r.loanPrincipalPayment));
            Metric("Clientes atendidos", Number(r.customers));
            Metric("Demanda base / potencial", Number(r.baseDemand) + " / " + Number(r.potentialDemand));
            Metric("Capacidade mensal", Number(r.serviceCapacity));
            Metric("Ticket médio", Money(r.averageTicket));
            Metric("Alinhamento • pontuação e classificação", Number(r.alignmentScore) + " • " + ClassificationLabel(r.alignmentClassification));
            Metric("Fator de coerência", r.coherenceFactor.ToString("0.##", Pt) + "x");
            Metric("Reputação • início / fim", Number(r.reputationAtStart) + " / " + Number(r.reputationAtEnd));
            Metric("Variação de reputação", (r.reputationDelta > 0 ? "+" : "") + Number(r.reputationDelta));
            return rows;
        }
    }
}
