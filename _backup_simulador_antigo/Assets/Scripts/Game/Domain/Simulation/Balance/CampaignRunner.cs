using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    /// <summary>Resultado de uma partida simulada do inicio ao fim (ou ate a falencia).</summary>
    public sealed class CampaignResult
    {
        public List<MonthlyReport> Months = new List<MonthlyReport>();
        public bool Bankrupt;
        public double FinalCash;
        public double FinalEquity;
        public double TotalNetIncome;
        public double AverageNps;
        public int DecisionEvents;
    }

    /// <summary>
    /// Encadeia meses: o fechamento de um mes vira a entrada do proximo (caixa, reputacao,
    /// saldo do emprestimo, estado dinamico). Usado no balanceamento e na previsao.
    /// A falencia segue a regra configurada em FinanceParameters.bankruptcyRule (BankruptcyRule).
    /// </summary>
    public static class CampaignRunner
    {
        public static CampaignResult Run(SimulationCatalog catalog, MonthInputs first, IReactionPolicy policy, int lastMonth = 0)
        {
            var result = new CampaignResult();
            var inputs = first.Clone();
            int final = lastMonth > 0 ? lastMonth : catalog.Config.calendar.months;
            int consecutive = inputs.ConsecutiveNegativeMonths;
            for (int month = inputs.Month; month <= final; month++)
            {
                inputs.Month = month;
                inputs.ConsecutiveNegativeMonths = consecutive;
                var sim = new MonthSimulation(catalog, inputs, policy ?? new DefaultReactionPolicy());
                var outcome = sim.RunToEnd();
                var report = outcome.Report;
                result.Months.Add(report);
                result.DecisionEvents += report.events.Count(e => !e.automatic && !string.IsNullOrEmpty(e.reactionId));
                consecutive = outcome.ConsecutiveNegativeMonths;

                var next = inputs.Clone();
                next.State = outcome.NextState;
                next.OpeningCash = report.cashResult.closingCash;
                next.OpeningReputation = report.cashResult.reputationAtEnd;
                next.LoanBalance = outcome.NextLoanBalance;
                next.PreviousReport = report;
                inputs = next;
                if (outcome.Bankrupt) { result.Bankrupt = true; break; }
            }
            var last = result.Months.Last();
            result.FinalCash = last.cashFlow.closingCash;
            result.FinalEquity = last.balance.Equity;
            result.TotalNetIncome = result.Months.Sum(m => m.income.netIncome);
            result.AverageNps = result.Months.Average(m => m.indicators.npsAverage);
            return result;
        }
    }

    /// <summary>
    /// Probabilidade de crescimento, estagnacao ou encerramento: roda o restante da partida
    /// (ou um horizonte extra no ultimo mes) com varias sementes e as decisoes atuais.
    /// </summary>
    public static class OutlookEstimator
    {
        public static OutlookEstimate Estimate(SimulationCatalog catalog, MonthInputs nextMonth, double equityNow, int runs = 60, int horizonMonths = 2)
        {
            var cfg = catalog.Config;
            int last = Math.Max(nextMonth.Month, Math.Min(cfg.calendar.months, nextMonth.Month + horizonMonths - 1));
            var runCatalog = catalog;
            if (nextMonth.Month > cfg.calendar.months)
            {
                // Depois do ultimo mes: projeta a continuidade com um calendario estendido.
                var calendar = new CalendarParameters
                {
                    operatingDays = cfg.calendar.operatingDays,
                    months = nextMonth.Month + horizonMonths - 1,
                    firstWeekday = cfg.calendar.firstWeekday,
                    firstCalendarMonth = cfg.calendar.firstCalendarMonth,
                    monthlySeasonality = new List<double>(cfg.calendar.monthlySeasonality)
                };
                runCatalog = new SimulationCatalog
                {
                    Config = cfg.WithCalendar(calendar), Locations = catalog.Locations, Restaurants = catalog.Restaurants,
                    Roles = catalog.Roles, Equipment = catalog.Equipment, Credits = catalog.Credits
                };
                last = calendar.months;
            }
            var finals = new List<double>();
            int growth = 0, closure = 0;
            for (int k = 0; k < runs; k++)
            {
                var inputs = nextMonth.Clone();
                inputs.Seed = unchecked(nextMonth.Seed * 6364136223846793005UL + (ulong)(k + 1) * 1442695040888963407UL);
                var campaign = CampaignRunner.Run(runCatalog, inputs, new DefaultReactionPolicy(), last);
                finals.Add(campaign.FinalCash);
                bool closed = campaign.Bankrupt || campaign.FinalCash < -cfg.finance.overdraftLimit;
                if (closed) closure++;
                else if (campaign.FinalEquity > equityNow * 1.05d && campaign.Months.Last().income.netIncome > 0d) growth++;
            }
            finals.Sort();
            return new OutlookEstimate
            {
                runs = runs,
                growthProbability = growth / (double)runs,
                closureProbability = closure / (double)runs,
                stagnationProbability = (runs - growth - closure) / (double)runs,
                medianCashEnd = Percentile(finals, 0.5d),
                p10CashEnd = Percentile(finals, 0.1d),
                p90CashEnd = Percentile(finals, 0.9d)
            };
        }

        internal static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0d;
            double index = p * (sorted.Count - 1);
            int lower = (int)Math.Floor(index);
            int upper = Math.Min(sorted.Count - 1, lower + 1);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (index - lower);
        }
    }
}
