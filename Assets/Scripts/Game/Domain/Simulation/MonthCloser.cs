// Game/Domain/Simulation/MonthCloser.cs
// Fechamento do dia 30: acertos, depreciação, DRE, fluxo de caixa, balanço, pontuação,
// falência, feedback e o estado do mês seguinte. Ainda não grava nada: quem grava é o
// serviço de aplicação, pela porta ISimulationGateway, numa transação.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    [Serializable]
    public sealed class NamedValue
    {
        public string Name;
        public double Value;
        public NamedValue() { }
        public NamedValue(string name, double value) { Name = name; Value = value; }
    }

    [Serializable]
    public sealed class DaySummary
    {
        public int Day;
        public double Demand, Served, Capacity, Revenue, CashEnd, Nps;
        public string Bottleneck;
        public int SalesBand;                    // 0 = fraco, 1 = médio, 2 = forte (cor do calendário)
    }

    [Serializable]
    public sealed class ForecastResult
    {
        public int Scenarios;
        public double Growth, Closure, Stagnation;        // probabilidades
        public double CashP10, CashMedian, CashP90;
    }

    [Serializable]
    public sealed class MonthReport
    {
        public string SessionId;
        public int Month;
        public string ConfigVersion;
        public List<NamedValue> Dre = new List<NamedValue>();
        public List<NamedValue> CashFlow = new List<NamedValue>();
        public List<NamedValue> Balance = new List<NamedValue>();
        public double NetIncome, CashResult, BalanceDifference;
        public double Orders, LostRatio, NpsAverage, CmvRatio, CurrentLiquidity, AvgTicket;
        public double ReputationStart, ReputationEnd, BaseDemandPerDay;
        public int Score;
        public string Classification;
        public bool Bankrupt;
        public List<string> ScoreItems = new List<string>();
        public List<string> Feedback = new List<string>();
        public ForecastResult Forecast;
        public List<EventRecord> Events = new List<EventRecord>();
        public List<DaySummary> Days = new List<DaySummary>();
    }

    public sealed class MonthOutcome
    {
        public MonthReport Report;
        public SimulationState NextState;
    }

    public static class MonthCloser
    {
        public static MonthOutcome Close(MonthSimulation sim, string sessionId = null)
        {
            if (!sim.IsFinished) throw new InvalidOperationException("O mês ainda não chegou ao dia 30.");
            var cat = sim.Catalog;
            var k = cat.Constants;
            var s = sim.State;
            var books = sim.Books;
            int day = k.DaysPerMonth;
            var rest = cat.Restaurants[sim.Setup.Restaurant];
            var region = cat.Regions[sim.Setup.RegionId];

            // 1. acertos: contas de consumo pagas pela projeção e impostos dos últimos dias
            books.PayBills(day, s.BillsPayable, "acerto das contas de consumo");
            books.PayTaxes(day);

            // 2. depreciação linear pela vida útil do nível
            foreach (var u in s.Equipment)
                books.Depreciate(day, u, u.PurchasePrice / cat.Levels[u.Level].LifeMonths);

            // 3. demonstrativos
            var r = new MonthReport { SessionId = sessionId, Month = s.Month, ConfigVersion = cat.ConfigVersion };
            foreach (DreLine line in Enum.GetValues(typeof(DreLine)))
                r.Dre.Add(new NamedValue(line.ToString(), books.Total(line)));
            foreach (CashLine line in Enum.GetValues(typeof(CashLine)))
                r.CashFlow.Add(new NamedValue(line.ToString(), books.Total(line)));
            r.NetIncome = books.NetIncome;
            r.CashResult = books.CashResult;

            double equipmentNet = s.Equipment.Sum(u => u.BookValue);
            double cashAsset = Math.Max(0, s.Cash), overdraft = Math.Max(0, -s.Cash);
            double assets = cashAsset + s.InventoryValue + equipmentNet;
            double liabilities = overdraft + s.SupplierPayables + s.TaxesPayable + s.BillsPayable + s.LoanBalance;
            double equity = s.Capital + s.RetainedEarnings + r.NetIncome;
            r.BalanceDifference = assets - liabilities - equity;              // tem que dar zero
            r.Balance.Add(new NamedValue("Caixa", cashAsset));
            r.Balance.Add(new NamedValue("Estoque", s.InventoryValue));
            r.Balance.Add(new NamedValue("Equipamentos líquidos", equipmentNet));
            r.Balance.Add(new NamedValue("Cheque especial", overdraft));
            r.Balance.Add(new NamedValue("Fornecedores", s.SupplierPayables));
            r.Balance.Add(new NamedValue("Contas a pagar", s.TaxesPayable + s.BillsPayable));
            r.Balance.Add(new NamedValue("Empréstimos", s.LoanBalance));
            r.Balance.Add(new NamedValue("Capital", s.Capital));
            r.Balance.Add(new NamedValue("Lucros acumulados", s.RetainedEarnings + r.NetIncome));

            // 4. indicadores
            double revenue = books.Total(DreLine.GrossRevenue);
            r.Orders = sim.SumServed;
            r.LostRatio = sim.SumDemand > 0 ? (sim.SumLostCapacity + sim.SumLostStock) / sim.SumDemand : 0;
            r.NpsAverage = sim.SumNps / k.DaysPerMonth;
            r.CmvRatio = revenue > 0 ? books.Total(DreLine.Cmv) / revenue : 0;
            r.CurrentLiquidity = liabilities > 0 ? (cashAsset + s.InventoryValue) / liabilities : 99;
            r.AvgTicket = sim.SumServed > 0 ? revenue / sim.SumServed : 0;
            r.ReputationStart = sim.ReputationAtStart;
            r.ReputationEnd = s.Reputation;
            r.BaseDemandPerDay = region.BaseDemandPerDay;
            bool lateBill = sim.Occurrences.Any(o => o.Definition.Id == EventIds.CashShortfall);

            // 5. pontuação (Bloco 10)
            int score = 0;
            void Add(bool condition, int points, string label) { if (condition) { score += points; r.ScoreItems.Add($"{label}: {points:+#;-#}"); } }
            Add(r.NetIncome > 0, 20, "Lucro líquido positivo");
            Add(s.Cash - sim.CashAtStart > 0, 20, "Fluxo de caixa do mês positivo");
            Add(r.NpsAverage > 70, 15, "NPS médio acima de 70");
            Add(r.CmvRatio >= rest.CmvMin && r.CmvRatio <= rest.CmvMax, 15, "CMV dentro do padrão");
            Add(r.CurrentLiquidity > 1.2, 10, "Liquidez corrente acima de 1,2");
            Add(r.LostRatio < 0.05, 10, "Pedidos perdidos abaixo de 5%");
            Add(!lateBill, 10, "Nenhuma conta paga com atraso");
            Add(r.NetIncome < 0, -20, "Prejuízo líquido");
            Add(s.Cash < 0, -25, "Caixa negativo");
            Add(r.NpsAverage < 30, -15, "NPS abaixo de 30");
            r.Score = score;
            r.Classification = score >= 80 ? "Gestão excelente" : score >= 60 ? "Adequada com pontos de atenção"
                             : score >= 40 ? "Instável" : score >= 0 ? "Em risco" : "Crise financeira e operacional";

            // 6. falência (regra ECONOMIC)
            int lossMonths = r.NetIncome < 0 ? s.ConsecutiveLossMonths + 1 : 0;
            r.Bankrupt = lossMonths >= 3 || s.Cash < -k.OverdraftLimit;

            // 7. eventos do mês e feedback com os números que o motor usou
            r.Events = s.EventHistory.Where(e => e.AbsDay > (s.Month - 1) * k.DaysPerMonth).Select(e => e.Clone()).ToList();
            r.Feedback.AddRange(Feedback(sim, r, region, rest));

            // 8. resumo por dia (calendário colorido da tela do mês)
            double avgRevenue = sim.DayResults.Average(d => d.Revenue);
            foreach (var d in sim.DayResults)
                r.Days.Add(new DaySummary
                {
                    Day = d.Day, Demand = d.Demand.Potential, Served = d.Serve.Served, Capacity = d.Capacity.Max, Revenue = d.Revenue,
                    CashEnd = d.CashEnd, Nps = d.Nps.Total, Bottleneck = d.Capacity.Bottleneck,
                    SalesBand = d.Revenue < 0.85 * avgRevenue ? 0 : d.Revenue > 1.15 * avgRevenue ? 2 : 1
                });

            // 9. estado do mês seguinte
            var next = s.Clone();
            next.RetainedEarnings += r.NetIncome;
            next.PreviousMonthMargin = revenue > 0 ? r.NetIncome / revenue : 0;
            next.ConsecutiveLossMonths = lossMonths;
            next.Bankrupt = r.Bankrupt;
            bool hadPreventive = sim.BuildLevers(sim.AbsDay).Add(LeverKeys.PreventiveFlag) > 0;
            next.MonthsWithoutPreventive = hadPreventive ? 0 : s.MonthsWithoutPreventive + 1;
            foreach (var u in next.Equipment) u.AgeMonths++;
            int monthEnd = Days.Abs(s.Month, k.DaysPerMonth, k.DaysPerMonth);
            next.Effects.RemoveAll(e => e.ExpiresAfterAbsDay <= monthEnd);
            next.Month = s.Month + 1;

            return new MonthOutcome { Report = r, NextState = next };
        }

        private static IEnumerable<string> Feedback(MonthSimulation sim, MonthReport r, RegionProfile region, RestaurantProfile rest)
        {
            var k = sim.Catalog.Constants;
            int n = sim.DayResults.Count;
            string[] names = { "encaixe", "concorrência", "reputação", "fidelização", "marketing", "calendário", "eventos", "ruído" };
            double totalLog = sim.SumLogFactors.Sum() / n;
            double avgPotential = sim.SumDemand / n;
            double totalChangePct = (avgPotential / region.BaseDemandPerDay - 1) * 100;
            var parts = new List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                double avgLog = sim.SumLogFactors[i] / n;
                double pp = Math.Abs(totalLog) > 1e-9 ? avgLog / totalLog * totalChangePct : 0;
                parts.Add($"{names[i]} {pp:+0.0;-0.0} p.p.");
            }
            yield return $"Demanda potencial média de {avgPotential:0.0} pedidos/dia, {totalChangePct:+0;-0}% em relação à base da região ({region.BaseDemandPerDay}). Por fator: {string.Join(", ", parts)}.";

            var main = region.Mix.OrderByDescending(m => m.Value).First();
            double avgFit = sim.DayResults.Average(d => d.Demand.Fit);
            yield return $"O público predominante da {region.Name} é {Label(main.Key)} ({main.Value:P0}); o encaixe do {rest.Name} ficou em {avgFit:0.00}.";

            var bottleneck = sim.BottleneckDays.OrderByDescending(b => b.Value).First();
            int saturated = sim.DayResults.Count(d => d.Serve.LostCapacity > 0.01);
            string front = bottleneck.Key == "cozinha" ? "da cozinha" : bottleneck.Key == "salão" ? "do salão" : "do atendimento";
            yield return $"A menor capacidade foi a {front} em {bottleneck.Value} de {n} dias; em {saturated} dias a demanda passou da capacidade.";
            yield return $"Pedidos perdidos: {r.LostRatio:P1} da demanda ({sim.SumLostCapacity:0} por capacidade, {sim.SumLostStock:0} por falta de estoque).";
            yield return $"CMV de {r.CmvRatio:P1} da receita (padrão do {rest.Name}: {rest.CmvMin:P0} a {rest.CmvMax:P0}).";
            int events = r.Events.Count;
            yield return $"{events} eventos no mês; R$ {r.Events.Sum(e => e.Cost):N0} gastos em reações.";
            yield return $"Lucro líquido de R$ {r.NetIncome:N0} e resultado de caixa de R$ {r.CashResult:N0}.";
        }

        private static string Label(Segment s) => s == Segment.LOW ? "Classe C" : s == Segment.MEDIUM ? "Classe B" : "Classe A";
    }
}
