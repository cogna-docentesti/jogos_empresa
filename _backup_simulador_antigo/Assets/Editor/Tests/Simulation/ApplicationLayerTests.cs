using System.Collections.Generic;
using System.Linq;
using Game.Application.Ports.Out;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using NUnit.Framework;

namespace Game.Tests.Simulation
{
    /// <summary>Porta de saida em memoria: faz o papel do SQLite nos testes do caso de uso.</summary>
    public sealed class InMemorySimulationGateway : ISimulationGateway
    {
        public SimulationCatalog Catalog;
        public SimulationSessionSnapshot Session;
        public StoredSimulationState Stored = new StoredSimulationState();
        public readonly List<MonthlyReport> Committed = new List<MonthlyReport>();
        public int FailNextCommits;
        public int CommitCalls;

        public SimulationCatalog LoadCatalog() => Catalog;

        public MonthInputs LoadActiveMonth(SimulationCatalog catalog, out string error)
            => SimulationInputsBuilder.Build(catalog, Session, Stored, out error);

        public MonthCommitResult Commit(MonthInputs inputs, MonthOutcome outcome)
        {
            CommitCalls++;
            if (FailNextCommits > 0)
            {
                FailNextCommits--;
                return new MonthCommitResult { Success = false, Error = "banco ocupado" };
            }
            var plan = MonthSettlementPlan.From(inputs, outcome, Catalog.Config.calendar.months);
            Session.CurrentCash = plan.CurrentCash;
            Session.ReputationScore = plan.ReputationScore;
            Session.LoanBalance = plan.LoanBalance;
            Session.ConsecutiveNegativeRounds = plan.ConsecutiveNegativeRounds;
            Session.CurrentRound = plan.NextRound;
            Session.Status = plan.Status;
            Stored.State = outcome.NextState.Clone();
            Stored.PreviousReport = outcome.Report;
            Committed.Add(outcome.Report);
            return new MonthCommitResult
            {
                Success = true,
                Bankrupt = plan.Status == SessionStatusName.Bankrupt,
                Completed = plan.Status == SessionStatusName.Completed,
                NextMonth = plan.NextRound
            };
        }

        public IDictionary<string, string> LoadPolicies(string sessionId) => new Dictionary<string, string>(Stored.Policies);

        public void SavePolicies(string sessionId, IDictionary<string, string> policies)
            => Stored.Policies = new Dictionary<string, string>(policies);

        public IReadOnlyList<MonthlyReport> LoadReports(string sessionId) => Committed;

        public static InMemorySimulationGateway Create(SimulationCatalog catalog, string type = "PODRAO", string zone = "Comercio", string segment = "LOW")
        {
            var restaurant = catalog.Restaurant(type);
            var session = new SimulationSessionSnapshot
            {
                SessionId = "sessao-teste",
                RestaurantType = type,
                LocationZone = zone,
                TargetSegment = segment,
                CurrentCash = 30000d,
                InitialCapital = 65000d,
                ReputationScore = 50
            };
            foreach (var product in restaurant.Products) session.MenuPrices[product.Id] = product.ReferencePrice;
            foreach (var role in restaurant.RequiredRoles) session.Team[role.Key] = role.Value;
            session.EquipmentIds.AddRange(DefaultSimulationConfig.BasicEquipment());
            session.EquipmentIds.AddRange(DefaultSimulationConfig.SpecificEquipment(type));
            return new InMemorySimulationGateway { Catalog = catalog, Session = session };
        }
    }

    public class ApplicationLayerTests
    {
        private static void PlayMonth(SystemicMonthService service, string preferredReaction = null)
        {
            int guard = 0;
            while (!service.IsFinished && guard++ < 500)
            {
                service.Step();
                while (service.IsAwaitingDecision)
                {
                    var pending = service.Simulation.Pending;
                    var option = pending.Options.FirstOrDefault(o => o.Enabled && o.ReactionId == preferredReaction)
                                 ?? pending.Options.First(o => o.Enabled);
                    service.Choose(pending.OccurrenceId, option.ReactionId);
                }
            }
            Assert.IsTrue(service.IsFinished, "o mes deveria terminar");
        }

        [Test]
        public void FullCycleCommitsEachMonthOnceAndEndsTheGame()
        {
            var gateway = InMemorySimulationGateway.Create(SimulationTestKit.Catalog());
            var service = new SystemicMonthService(gateway) { OutlookRuns = 4 };
            int months = gateway.Catalog.Config.calendar.months;
            for (int month = 1; month <= months; month++)
            {
                Assert.IsTrue(service.Begin(), service.LastError);
                Assert.AreEqual(month, service.Simulation.Month);
                PlayMonth(service);
                var report = service.Finish();
                Assert.IsTrue(service.LastCommit.Success);
                Assert.AreEqual(month, report.month);
                Assert.IsNotNull(report.outlook);
                if (gateway.Session.Status != SessionStatusName.InProgress) break;
            }
            Assert.AreEqual(months, gateway.Committed.Count);
            Assert.AreEqual(months, service.Reports("sessao-teste").Count);
            Assert.AreEqual(SessionStatusName.Completed, gateway.Session.Status);
            Assert.IsFalse(service.Begin());
            StringAssert.Contains("terminou", service.LastError);
        }

        [Test]
        public void FinishingTwiceDoesNotCommitTwice()
        {
            var gateway = InMemorySimulationGateway.Create(SimulationTestKit.QuietCatalog());
            var service = new SystemicMonthService(gateway) { OutlookRuns = 0 };
            Assert.IsTrue(service.Begin());
            PlayMonth(service);
            service.Finish();
            service.Finish();
            Assert.AreEqual(1, gateway.CommitCalls);
        }

        [Test]
        public void FinishBeforeTheEndOfTheMonthIsRejected()
        {
            var gateway = InMemorySimulationGateway.Create(SimulationTestKit.QuietCatalog());
            var service = new SystemicMonthService(gateway);
            Assert.IsTrue(service.Begin());
            service.Step();
            Assert.Throws<System.InvalidOperationException>(() => service.Finish());
        }

        [Test]
        public void FailedCommitCanBeRetriedWithoutRecalculating()
        {
            var gateway = InMemorySimulationGateway.Create(SimulationTestKit.QuietCatalog());
            gateway.FailNextCommits = 1;
            var service = new SystemicMonthService(gateway) { OutlookRuns = 0 };
            Assert.IsTrue(service.Begin());
            PlayMonth(service);
            var report = service.Finish();
            Assert.IsFalse(service.LastCommit.Success);
            Assert.AreEqual(1, gateway.Session.CurrentRound);
            var retry = service.RetryCommit();
            Assert.IsTrue(retry.Success);
            Assert.AreSame(report, gateway.Committed.Single());
            Assert.AreEqual(2, gateway.Session.CurrentRound);
        }

        [Test]
        public void RestartingAMonthReplaysTheSameDraws()
        {
            var gateway = InMemorySimulationGateway.Create(SimulationTestKit.Catalog());
            var service = new SystemicMonthService(gateway) { OutlookRuns = 0 };
            Assert.IsTrue(service.Begin());
            for (int i = 0; i < 12 && !service.IsFinished; i++)
            {
                service.Step();
                while (service.IsAwaitingDecision)
                    service.Choose(service.Simulation.Pending.OccurrenceId, service.Simulation.Pending.Options.First(o => o.Enabled).ReactionId);
            }
            var firstDays = service.Simulation.Days.Select(d => d.served).ToList();
            service.Abandon();
            Assert.AreEqual(0, gateway.CommitCalls);

            Assert.IsTrue(service.Begin());
            PlayMonth(service);
            var replay = service.Simulation.Days.Take(firstDays.Count).Select(d => d.served).ToList();
            CollectionAssert.AreEqual(firstDays, replay);
        }

        [Test]
        public void InvalidConfigurationIsReportedInsteadOfCrashing()
        {
            var catalog = SimulationTestKit.Catalog(c => c.calendar.operatingDays = 0);
            var service = new SystemicMonthService(InMemorySimulationGateway.Create(catalog));
            Assert.IsFalse(service.Begin());
            StringAssert.Contains("operatingDays", service.LastError);
        }

        [Test]
        public void PoliciesAreValidatedSavedAndAppliedToTheNextMonth()
        {
            var gateway = InMemorySimulationGateway.Create(SimulationTestKit.QuietCatalog());
            var service = new SystemicMonthService(gateway) { OutlookRuns = 0 };
            var marketing = service.PolicyCategories().First(p => p.id == "marketing");
            string strongest = marketing.options.OrderByDescending(o => o.monthlyCost).First().id;
            Assert.Throws<System.InvalidOperationException>(() =>
                service.SavePolicies("sessao-teste", new Dictionary<string, string> { ["marketing"] = "nao_existe" }));
            service.SavePolicies("sessao-teste", new Dictionary<string, string> { ["marketing"] = strongest });
            var current = service.CurrentPolicies("sessao-teste");
            Assert.AreEqual(strongest, current["marketing"]);
            Assert.AreEqual(service.PolicyCategories().Count, current.Count);

            Assert.IsTrue(service.Begin());
            Assert.Throws<System.InvalidOperationException>(() => service.SavePolicies("sessao-teste", current));
            PlayMonth(service);
            var report = service.Finish();
            Assert.Greater(report.income.marketing, 0d);
        }

        [Test]
        public void SessionWithUnknownZoneGivesAClearError()
        {
            var gateway = InMemorySimulationGateway.Create(SimulationTestKit.QuietCatalog());
            gateway.Session.LocationZone = "Lua";
            var service = new SystemicMonthService(gateway);
            Assert.IsFalse(service.Begin());
            StringAssert.Contains("Lua", service.LastError);
        }

        [Test]
        public void SettlementPlanFollowsTheSessionRules()
        {
            var catalog = SimulationTestKit.QuietCatalog();
            var inputs = SimulationTestKit.Inputs(catalog);
            var outcome = SimulationTestKit.RunMonth(catalog, inputs);
            var plan = MonthSettlementPlan.From(inputs, outcome, 3);
            Assert.AreEqual(2, plan.NextRound);
            Assert.AreEqual(SessionStatusName.InProgress, plan.Status);
            Assert.AreEqual(outcome.Report.cashResult.closingCash, plan.CurrentCash, 1e-9);

            inputs.Month = 3;
            Assert.AreEqual(SessionStatusName.Completed, MonthSettlementPlan.From(inputs, outcome, 3).Status);
            outcome.Bankrupt = true;
            var bankrupt = MonthSettlementPlan.From(inputs, outcome, 3);
            Assert.AreEqual(SessionStatusName.Bankrupt, bankrupt.Status);
            Assert.IsTrue(bankrupt.Ended);
        }
    }

    public class LegacyEventConverterTests
    {
        private static LegacyEventSpec FreezerLikeSpec()
        {
            var spec = new LegacyEventSpec
            {
                Id = "legacy_freezer",
                Title = "Quebra do freezer",
                Positive = false,
                BaseWeight = 2d,
                CanRepeat = false,
                MinRound = 1,
                MaxRound = 3
            };
            spec.Conditions.Add(new LegacyConditionSpec { Type = "EQUIPMENT_QUALITY_SCORE", Operator = "LESS_OR_EQUAL", Value = 1d });
            spec.Conditions.Add(new LegacyConditionSpec { Type = "SANITARY_RISK_SCORE", Operator = "GREATER_THAN", Value = 0.5d });
            spec.Options.Add(new LegacyOptionSpec { Id = "repair_now", Title = "Consertar", CashDelta = -3500d });
            spec.Options.Add(new LegacyOptionSpec { Id = "temporary_repair", Title = "Reparo", CashDelta = -1500d, CapacityMultiplier = 0.9d, Duration = "CURRENT_DAY" });
            spec.Options.Add(new LegacyOptionSpec { Id = "postpone", Title = "Adiar", StockMultiplier = 0.85d });
            return spec;
        }

        [Test]
        public void LegacyEventIsConvertedWithEquivalentEffects()
        {
            var warnings = new List<string>();
            var definition = LegacyEventConverter.Convert(FreezerLikeSpec(), new EventEngineParameters(), warnings);
            Assert.AreEqual(EventImpact.Negative, definition.impact);
            Assert.AreEqual(0.6d, definition.baseRatePerMonth, 1e-9);
            Assert.AreEqual(1, definition.maxPerGame);
            Assert.AreEqual(1, definition.conditions.Count);
            Assert.AreEqual(Indicator.Quality, definition.conditions[0].indicator);
            Assert.AreEqual("<=", definition.conditions[0].op);
            Assert.AreEqual(1, warnings.Count);
            Assert.AreEqual(3500d, definition.Reaction("repair_now").cost, 1e-9);
            var temporary = definition.Reaction("temporary_repair").effect;
            Assert.IsTrue(temporary.modifiers.Any(m => m.key == Lever.FrontCapacityFactor && System.Math.Abs(m.value - 0.9d) < 1e-9));
            Assert.AreEqual(1, temporary.durationDays);
            Assert.IsTrue(definition.Reaction("postpone").effect.modifiers.Any(m => m.key == Lever.StockLossInstant && System.Math.Abs(m.value - 0.15d) < 1e-9));
            Assert.AreEqual("postpone", definition.defaultReactionId);
        }

        [Test]
        public void ConversionAddsAFreeReactionWhenEveryOptionCosts()
        {
            var spec = new LegacyEventSpec { Id = "legacy_paid", Positive = true };
            spec.Options.Add(new LegacyOptionSpec { Id = "buy", CashDelta = -800d, DemandMultiplier = 1.2d, Duration = "CURRENT_MONTH" });
            var definition = LegacyEventConverter.Convert(spec, new EventEngineParameters());
            Assert.IsNotNull(definition.Reaction(LegacyEventConverter.DeclineReactionId));
            Assert.AreEqual(LegacyEventConverter.DeclineReactionId, definition.defaultReactionId);
            Assert.AreEqual(EffectEnd.MonthEnd, definition.Reaction("buy").effect.end);
        }

        [Test]
        public void AppendMissingKeepsSystemicDefinitionsAndProducesAValidCatalog()
        {
            var config = DefaultSimulationConfig.Create();
            var known = new LegacyEventSpec { Id = "freezer_breakdown", Title = "titulo legado" };
            int before = config.events.Count;
            int added = LegacyEventConverter.AppendMissing(config, new[] { known, FreezerLikeSpec() });
            Assert.AreEqual(1, added);
            Assert.AreEqual(before + 1, config.events.Count);
            Assert.AreNotEqual("titulo legado", config.Event("freezer_breakdown").title);
            var problems = DefaultSimulationConfig.CreateCatalog(config).Validate();
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void ConvertedEventCanHappenInTheSimulation()
        {
            var spec = new LegacyEventSpec { Id = "legacy_bonus", Positive = true, BaseWeight = 1000d, CanRepeat = true };
            spec.Options.Add(new LegacyOptionSpec { Id = "accept", CashDelta = 500d });
            var catalog = SimulationTestKit.QuietCatalog(c => LegacyEventConverter.AppendMissing(c, new[] { spec }));
            var report = SimulationTestKit.RunMonth(catalog, SimulationTestKit.Inputs(catalog), new FixedReactionPolicy("accept")).Report;
            var occurrences = report.events.Where(e => e.eventId == "legacy_bonus").ToList();
            Assert.Greater(occurrences.Count, 0);
            Assert.IsTrue(occurrences.All(e => e.reactionId == "accept" && e.cashImpact > 0d));
        }
    }
}
