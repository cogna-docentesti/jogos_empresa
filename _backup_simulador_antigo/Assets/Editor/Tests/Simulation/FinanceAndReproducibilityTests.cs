using System;
using System.Linq;
using Game.Domain.Simulation;
using NUnit.Framework;

namespace Game.Tests.Simulation
{
    public class FinanceTests
    {
        [Test]
        public void LoanRaisesCashFirstAndCreatesFutureObligations()
        {
            var catalog = SimulationTestKit.QuietCatalog();
            var withoutLoan = SimulationTestKit.Inputs(catalog);
            var withLoan = SimulationTestKit.Inputs(catalog, strategyId: "equilibrado");
            withLoan.CreditLineId = "credito_empresarial_padrao";
            withLoan.LoanBalance = 120000d;
            withLoan.OpeningCash += 120000d;
            Assert.Greater(withLoan.OpeningCash, withoutLoan.OpeningCash);

            var campaign = CampaignRunner.Run(catalog, withLoan, new DefaultReactionPolicy());
            var baseline = CampaignRunner.Run(catalog, withoutLoan, new DefaultReactionPolicy());
            Assert.Greater(campaign.Months[0].cashFlow.loanPrincipal, 0d);
            Assert.Greater(campaign.Months[0].income.loanInterest, 0d);
            Assert.AreEqual(0d, campaign.Months.Last().balance.loans, 0.01d);
            Assert.AreEqual(120000d, campaign.Months.Sum(m => m.cashFlow.loanPrincipal), 0.05d);
            Assert.Less(campaign.TotalNetIncome, baseline.TotalNetIncome);
        }

        [Test]
        public void CashFlowAndBalanceSheetAreConsistentEveryMonth()
        {
            var catalog = SimulationTestKit.Catalog();
            foreach (var strategy in StrategyProfile.Archetypes())
            {
                var inputs = BalanceSimulator.BuildInputs(catalog, "JAPONES", "store", "MEDIUM", strategy, 7UL);
                var campaign = CampaignRunner.Run(catalog, inputs, strategy.Reactions);
                foreach (var month in campaign.Months)
                {
                    var flow = month.cashFlow;
                    Assert.AreEqual(flow.openingCash + flow.TotalInflows - flow.TotalOutflows, flow.closingCash, 0.05d, strategy.Id);
                    var r = month.cashResult;
                    double formula = r.grossRevenue - (r.supplyCost + r.rent + r.salaries + r.utilities + r.loanPayment + r.otherOperatingCosts) + r.eventCashImpact;
                    Assert.AreEqual(r.netResult, formula, 0.05d, strategy.Id);
                    Assert.AreEqual(0d, month.balance.Difference, 1d, strategy.Id + " mes " + month.month);
                }
                for (int i = 1; i < campaign.Months.Count; i++)
                    Assert.AreEqual(campaign.Months[i - 1].cashFlow.closingCash, campaign.Months[i].cashFlow.openingCash, 0.01d);
            }
        }

        [Test]
        public void IncomeStatementLinesAddUpToNetIncome()
        {
            var catalog = SimulationTestKit.Catalog();
            var report = SimulationTestKit.RunMonth(catalog, SimulationTestKit.Inputs(catalog, "FRANCES", "bank", "HIGH")).Report;
            var i = report.income;
            double expected = i.grossRevenue - i.salesTaxes - i.cmv - i.payroll - i.rent - i.marketing - i.utilities
                              - i.maintenanceAndLosses - i.otherExpenses + i.otherIncome - i.depreciation - i.financialExpenses;
            Assert.AreEqual(expected, i.netIncome, 0.2d);
            Assert.AreEqual(i.grossRevenue * catalog.Config.finance.salesTaxRate, i.salesTaxes, 0.5d);
        }

        [Test]
        public void SpecificationRuleNextMonthBillsLeavePayablesAtTheEnd()
        {
            var catalog = SimulationTestKit.QuietCatalog(c => c.finance.billTiming = BillTiming.NextMonthDay5);
            var campaign = CampaignRunner.Run(catalog, SimulationTestKit.Inputs(catalog), new DefaultReactionPolicy());
            Assert.AreEqual(0d, campaign.Months[0].cashFlow.rent, 0.01d);
            Assert.Greater(campaign.Months[1].cashFlow.rent, 0d);
            Assert.Greater(campaign.Months.Last().balance.billsPayable, 0d);
            Assert.AreEqual(0d, campaign.Months.Last().balance.Difference, 1d);
        }

        [Test]
        public void EconomicBankruptcyRuleDoesNotPunishLoanRepayment()
        {
            var catalog = SimulationTestKit.QuietCatalog();
            var inputs = SimulationTestKit.Inputs(catalog);
            inputs.CreditLineId = "credito_empresarial_padrao";
            inputs.LoanBalance = 120000d;
            inputs.OpeningCash += 120000d;
            var economic = CampaignRunner.Run(catalog, inputs, new DefaultReactionPolicy());
            Assert.IsFalse(economic.Bankrupt);
            var legacyCatalog = SimulationTestKit.QuietCatalog(c => c.finance.bankruptcyRule = BankruptcyRule.LegacyCashResult);
            var legacy = CampaignRunner.Run(legacyCatalog, inputs, new DefaultReactionPolicy());
            Assert.IsTrue(legacy.Months.All(m => m.cashResult.netResult < 0d) == legacy.Bankrupt);
        }
    }

    public class ReproducibilityTests
    {
        private static string Fingerprint(CampaignResult campaign)
        {
            return string.Join("|", campaign.Months.Select(m =>
                m.income.netIncome.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ";" +
                m.cashFlow.closingCash.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ";" +
                string.Join(",", m.events.Select(e => e.day + e.eventId + e.reactionId))));
        }

        [Test]
        public void SameSeedAndDecisionsReproduceTheSameScenario()
        {
            var catalog = SimulationTestKit.Catalog();
            var first = CampaignRunner.Run(catalog, SimulationTestKit.Inputs(catalog, seed: 99UL), new DefaultReactionPolicy());
            var second = CampaignRunner.Run(catalog, SimulationTestKit.Inputs(catalog, seed: 99UL), new DefaultReactionPolicy());
            Assert.AreEqual(Fingerprint(first), Fingerprint(second));
        }

        [Test]
        public void DifferentSeedsProduceDifferentScenarios()
        {
            var catalog = SimulationTestKit.Catalog();
            var a = CampaignRunner.Run(catalog, SimulationTestKit.Inputs(catalog, seed: 1UL), new DefaultReactionPolicy());
            var b = CampaignRunner.Run(catalog, SimulationTestKit.Inputs(catalog, seed: 2UL), new DefaultReactionPolicy());
            Assert.AreNotEqual(Fingerprint(a), Fingerprint(b));
        }

        [Test]
        public void AddingAnUnrelatedEventDoesNotChangeDemandNoise()
        {
            var plain = SimulationTestKit.QuietCatalog(c => c.market.demandNoiseSigma = 0.12d);
            var extra = SimulationTestKit.QuietCatalog(c =>
            {
                c.market.demandNoiseSigma = 0.12d;
                c.events.Add(SimulationTestKit.ForcedEvent("never", 99, null));
            });
            var a = SimulationTestKit.RunMonth(plain, SimulationTestKit.Inputs(plain)).Report;
            var b = SimulationTestKit.RunMonth(extra, SimulationTestKit.Inputs(extra)).Report;
            for (int d = 0; d < a.days.Count; d++) Assert.AreEqual(a.days[d].fNoise, b.days[d].fNoise, 1e-12);
        }

        [Test]
        public void StatelessRandomIsUniformInRange()
        {
            var rng = new DeterministicRandom(123UL);
            double sum = 0d;
            for (int i = 0; i < 5000; i++)
            {
                double u = rng.Uniform(1, i % 30 + 1, "test", "k", i);
                Assert.GreaterOrEqual(u, 0d);
                Assert.Less(u, 1d);
                sum += u;
            }
            Assert.AreEqual(0.5d, sum / 5000d, 0.03d);
        }
    }

    public class FeedbackAndBalanceTests
    {
        [Test]
        public void FeedbackExplainsMismatchWithComputedValues()
        {
            var catalog = SimulationTestKit.QuietCatalog();
            var report = SimulationTestKit.RunMonth(catalog, SimulationTestKit.Inputs(catalog, "FRANCES", "university", "HIGH", 0.6d)).Report;
            var market = report.feedback.Where(f => f.topic == "demanda").ToList();
            Assert.IsTrue(market.Any(f => f.text.Contains("público predominante") && f.values.Count >= 3));
            Assert.IsTrue(report.demandFactors.Any(f => f.factor == "fit" && f.contributionPoints < -20d));
        }

        [Test]
        public void FeedbackComparesWithPreviousMonth()
        {
            var catalog = SimulationTestKit.Catalog();
            var campaign = CampaignRunner.Run(catalog, SimulationTestKit.Inputs(catalog), new DefaultReactionPolicy());
            Assert.IsTrue(campaign.Months[1].feedback.Any(f => f.topic == "demanda" && f.text.Contains("em relação ao mês anterior")));
        }

        [Test]
        public void DefaultCatalogIsValid()
        {
            var problems = DefaultSimulationConfig.CreateCatalog().Validate();
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void MonteCarloProducesFiniteResultsWithoutDominantStrategy()
        {
            var catalog = SimulationTestKit.Catalog();
            var report = BalanceSimulator.Run(catalog, 3);
            Assert.AreEqual(25, report.Contexts);
            foreach (var cell in report.Cells)
            {
                Assert.IsFalse(double.IsNaN(cell.MeanNetIncome) || double.IsInfinity(cell.MeanNetIncome));
                Assert.IsFalse(double.IsNaN(cell.MedianEquity));
            }
            Assert.IsFalse(report.Warnings.Any(w => w.Contains("dominante")), report.Summary());
        }

        [Test]
        public void BusinessModelCanvasHasNineBlocksFromDecisions()
        {
            var catalog = SimulationTestKit.Catalog();
            var canvas = BusinessModelCanvasBuilder.Build(catalog, SimulationTestKit.Inputs(catalog, "FRANCES", "university", "HIGH"));
            Assert.AreEqual(9, canvas.blocks.Count);
            Assert.IsTrue(canvas.Block("customer_segments").notes.Any());
            Assert.IsTrue(canvas.blocks.All(b => b.items.Count > 0));
        }
    }
}
