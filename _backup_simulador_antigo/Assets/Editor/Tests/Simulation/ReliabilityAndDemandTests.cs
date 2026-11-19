using System.Linq;
using Game.Domain.Simulation;
using NUnit.Framework;

namespace Game.Tests.Simulation
{
    public class ReliabilityTests
    {
        private SimulationCatalog _catalog;
        private EquipmentCategoryProfile _fridge;

        [SetUp]
        public void SetUp()
        {
            _catalog = SimulationTestKit.Catalog();
            _fridge = _catalog.Category(EquipmentCategoryId.Refrigeration);
        }

        private HazardBreakdown Hazard(string tier, double age = 0d, bool preventive = false, double policy = 1d, double utilization = 0.8d, int recent = 0, int monthsWithout = 0)
            => ReliabilityModel.Hazard(_fridge, _catalog.Tier(tier), _catalog.Config.reliability, age, utilization, recent, monthsWithout, preventive, policy, 1d, 1d);

        [Test]
        public void EconomicFridgeFailsMoreThanPremiumUnderSameConditions()
        {
            var economic = Hazard(EquipmentTierId.Economic);
            var premium = Hazard(EquipmentTierId.Premium);
            Assert.Greater(economic.DailyProbability, premium.DailyProbability);
            Assert.Greater(economic.MonthlyProbability, premium.MonthlyProbability * 2d);
        }

        [Test]
        public void PreventiveMaintenanceReducesFailureProbability()
        {
            var corrective = Hazard(EquipmentTierId.Standard, monthsWithout: 2);
            var preventive = Hazard(EquipmentTierId.Standard, preventive: true, policy: 0.55d);
            Assert.Less(preventive.DailyProbability, corrective.DailyProbability);
        }

        [Test]
        public void FailureRiskGrowsWithAge()
        {
            var fresh = Hazard(EquipmentTierId.Standard, age: 0d);
            var old = Hazard(EquipmentTierId.Standard, age: 48d);
            Assert.Greater(old.DailyProbability, fresh.DailyProbability);
            Assert.Greater(old.AgeRatio, 1d);
        }

        [Test]
        public void IntenseUseAndRecentFailuresIncreaseRisk()
        {
            var normal = Hazard(EquipmentTierId.Standard);
            Assert.Greater(Hazard(EquipmentTierId.Standard, utilization: 1.3d).DailyProbability, normal.DailyProbability);
            Assert.Greater(Hazard(EquipmentTierId.Standard, recent: 2).DailyProbability, normal.DailyProbability);
        }

        [Test]
        public void QueueAboveCapacityDoesNotWearEquipmentBeyondTheCap()
        {
            var p = _catalog.Config.reliability;
            var atCap = Hazard(EquipmentTierId.Standard, utilization: p.usageCap);
            var hugeQueue = Hazard(EquipmentTierId.Standard, utilization: 3d);
            Assert.AreEqual(atCap.UsageRatio, hugeQueue.UsageRatio, 1e-9);
            Assert.LessOrEqual(hugeQueue.UsageRatio, System.Math.Exp(p.usageBeta * (p.usageCap - p.usageThreshold)) + 1e-9);
        }

        [Test]
        public void ProbabilityStaysBetweenZeroAndOneAndPremiumIsNeverImmune()
        {
            var best = ReliabilityModel.Hazard(_fridge, _catalog.Tier(EquipmentTierId.Premium), _catalog.Config.reliability, 0d, 0d, 0, 0, true, 0.01d, 0.01d, 0.5d);
            Assert.Greater(best.DailyProbability, 0d);
            Assert.GreaterOrEqual(best.CombinedRatio, _catalog.Config.reliability.minimumHazardRatio);
            var worst = ReliabilityModel.Hazard(_fridge, _catalog.Tier(EquipmentTierId.Economic), _catalog.Config.reliability, 500d, 2d, 20, 12, false, 50d, 50d, 2d);
            Assert.LessOrEqual(worst.DailyProbability, 1d);
            Assert.GreaterOrEqual(worst.DailyProbability, 0d);
        }

        [Test]
        public void EconomicUnitsFailMoreOftenInFullSimulations()
        {
            int Failures(string tier)
            {
                var catalog = SimulationTestKit.Catalog(c =>
                {
                    c.events.RemoveAll(e => e.scope != EventScope.EquipmentUnit);
                    foreach (var profile in c.equipment) profile.tierId = tier;
                });
                int total = 0;
                for (ulong seed = 1; seed <= 40; seed++)
                {
                    var inputs = SimulationTestKit.Inputs(catalog, seed: seed);
                    var campaign = CampaignRunner.Run(catalog, inputs, new DefaultReactionPolicy());
                    total += campaign.Months.Sum(m => m.events.Count(e => e.eventId == "freezer_breakdown" || e.eventId == "kitchen_equipment_failure"));
                }
                return total;
            }
            Assert.Greater(Failures(EquipmentTierId.Economic), Failures(EquipmentTierId.Premium));
        }
    }

    public class DemandTests
    {
        private SimulationCatalog _catalog;

        [SetUp]
        public void SetUp() => _catalog = SimulationTestKit.Catalog();

        [Test]
        public void HighEndRestaurantInLowIncomeRegionHasLowerDemandAllElseEqual()
        {
            var university = SimulationTestKit.Market(_catalog, "FRANCES", "university", "HIGH", 0.45d);
            var financial = SimulationTestKit.Market(_catalog, "FRANCES", "bank", "HIGH", 0.45d);
            Assert.Less(university.FitFactor, financial.FitFactor * 0.5d);
        }

        [Test]
        public void AdequatePricingImprovesCompatibilityWithLocalPublic()
        {
            var expensive = SimulationTestKit.Market(_catalog, "FRANCES", "university", "HIGH", 0.9d);
            var accessible = SimulationTestKit.Market(_catalog, "FRANCES", "university", "HIGH", 0.05d);
            Assert.Greater(accessible.FitFactor, expensive.FitFactor);
            var low = accessible.Segments.First(s => s.SegmentId == "LOW");
            var lowExpensive = expensive.Segments.First(s => s.SegmentId == "LOW");
            Assert.Greater(low.Acceptance, lowExpensive.Acceptance);
        }

        [Test]
        public void DifferentiationAndReachMakeTheOutcomeNotInevitable()
        {
            var baseline = SimulationTestKit.Market(_catalog, "FRANCES", "university", "HIGH", 0.45d);
            var differentiated = SimulationTestKit.Market(_catalog, "FRANCES", "university", "HIGH", 0.45d, quality: 0.9d);
            var reach = SimulationTestKit.Market(_catalog, "FRANCES", "university", "HIGH", 0.45d, reach: 0.18d, awareness: 1d);
            Assert.Greater(differentiated.PotentialDemand, baseline.PotentialDemand);
            Assert.Greater(reach.PotentialDemand, baseline.PotentialDemand * 1.2d);
        }

        [Test]
        public void PopularRestaurantInNobleRegionStillAttractsLocalWorkers()
        {
            var noble = SimulationTestKit.Market(_catalog, "PODRAO", "bank", "LOW", 0.45d);
            Assert.Greater(noble.FitFactor, 0.9d);
            Assert.Greater(noble.Segments.First(s => s.SegmentId == "LOW").Demand + noble.Segments.First(s => s.SegmentId == "MEDIUM").Demand, noble.PotentialDemand * 0.5d);
        }

        [Test]
        public void ServedNeverExceedsDemandCapacityOrStock()
        {
            var catalog = SimulationTestKit.Catalog();
            var inputs = SimulationTestKit.Inputs(catalog, strategyId: "expansao_credito");
            inputs.Policies["marketing"] = "high";
            var campaign = CampaignRunner.Run(catalog, inputs, new DefaultReactionPolicy());
            foreach (var day in campaign.Months.SelectMany(m => m.days))
            {
                Assert.LessOrEqual(day.served, day.potentialDemand + 1e-6d);
                Assert.LessOrEqual(day.served, day.capacity + 1e-6d);
                Assert.GreaterOrEqual(day.served, 0d);
            }
        }

        [Test]
        public void PurchasesFollowExpectedSalesNotUnservedDemand()
        {
            // Demanda bem acima da capacidade: 1 atendente e marketing alto.
            var catalog = SimulationTestKit.QuietCatalog();
            var inputs = SimulationTestKit.Inputs(catalog);
            inputs.Team["attendant"] = 1;
            inputs.Policies["marketing"] = "high";
            var report = SimulationTestKit.RunMonth(catalog, inputs).Report;
            double demand = report.days.Average(d => d.potentialDemand);
            double capacity = report.days.Average(d => d.capacity);
            Assert.Greater(demand, capacity * 1.5d, "o cenario precisa ter demanda bem acima da capacidade");
            // Politica de estoque "medio" = 4 dias de vendas esperadas. Comprar pela demanda daria ~4 x demanda.
            Assert.LessOrEqual(report.days.Skip(5).Average(d => d.stockMealsEnd), 4.5d * capacity);
            // Perdas por perecibilidade proporcionais ao que se vende, nao ao que se deixou de vender.
            Assert.Less(report.indicators.wasteShare, 12d);
        }

        [Test]
        public void InsufficientTeamCompromisesService()
        {
            var catalog = SimulationTestKit.QuietCatalog();
            var full = SimulationTestKit.Inputs(catalog);
            var understaffed = SimulationTestKit.Inputs(catalog);
            understaffed.Team["attendant"] = 1;
            var fullReport = SimulationTestKit.RunMonth(catalog, full).Report;
            var shortReport = SimulationTestKit.RunMonth(catalog, understaffed).Report;
            Assert.Less(shortReport.days.Average(d => d.capacity), fullReport.days.Average(d => d.capacity));
            Assert.Greater(shortReport.indicators.lostOrders, fullReport.indicators.lostOrders);
            Assert.Greater(shortReport.indicators.waitAverage, fullReport.indicators.waitAverage);
            Assert.Less(shortReport.indicators.npsAverage, fullReport.indicators.npsAverage);
        }
    }
}
