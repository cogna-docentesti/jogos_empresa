using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using Newtonsoft.Json;
using NUnit.Framework;

public sealed class MonthlyEventPlanTests
{
    private static SimulationCatalog Catalog()
    {
        var catalog = SampleCatalog.Build();
        // Plenty of independent eligible narratives to test the contract rather than catalog exhaustion.
        for (int i = 0; i < 25; i++) catalog.Events.Add(new EventDefinition
        {
            Id = "planned_test_" + i, Title = "Planned test " + i, Scope = EventScope.Global,
            BaseRatePerMonth = 1, RequiresDecision = false, Repeats = true
        });
        return catalog;
    }

    [Test]
    public void ThreeMonthsDrawFourOrFiveDifferentEventsAndNeverReuseACampaignEvent()
    {
        for (int seed = 1; seed <= 20; seed++)
        {
            var catalog = Catalog();
            var setup = MotorTestKit.Lanches(seed: seed);
            var state = InitialStateFactory.Create(catalog, setup);
            var seen = new HashSet<string>();
            for (int month = 1; month <= 3; month++)
            {
                var draft = new MonthSimulation(catalog, setup, state, new MonthPolicies());
                var plan = draft.ReserveMonthlyEvents();
                Assert.That(plan.Events.Count, Is.InRange(4, 5));
                Assert.AreEqual(plan.Events.Count, plan.Events.Select(e => e.Day).Distinct().Count());
                foreach (var entry in plan.Events)
                {
                    Assert.IsTrue(seen.Add(entry.EventId), "Event repeated across the campaign: " + entry.EventId);
                    Assert.That(entry.Day, Is.InRange(1, 30));
                    if (month == 1 && catalog.Event(entry.EventId).IsNegative)
                        Assert.Greater(entry.Day, catalog.Constants.GraceDaysWithoutNegative);
                }
                var sim = new MonthSimulation(catalog, setup, state, new MonthPolicies(), plan);
                Autopilot.RunToEnd(sim);
                CollectionAssert.AreEquivalent(plan.Events.Select(e => e.EventId), sim.Occurrences.Select(e => e.Definition.Id));
                Assert.AreEqual(plan.Events.Count, sim.Occurrences.Count);
                state = MonthCloser.Close(sim).NextState;
            }
        }
    }

    [Test]
    public void EvenCertainDailyAbsenceCannotAddEventsOutsideTheFrozenPlan()
    {
        var catalog = Catalog(); catalog.Constants.AbsencePerPersonDay = 1;
        var setup = MotorTestKit.Lanches();
        setup.Team.AddRange(Enumerable.Repeat(RoleType.ATENDENTE, 8));
        var start = InitialStateFactory.Create(catalog, setup);
        var plan = new MonthSimulation(catalog, setup, start, new MonthPolicies()).ReserveMonthlyEvents();
        Assert.AreEqual(1, plan.Events.Count(e => e.EventId == EventIds.StaffAbsent));
        var sim = new MonthSimulation(catalog, setup, start, new MonthPolicies(), plan);
        Autopilot.RunToEnd(sim);
        Assert.AreEqual(1, sim.Occurrences.Count(e => e.Definition.Id == EventIds.StaffAbsent));
        Assert.AreEqual(plan.Events.Count, sim.Occurrences.Count);
    }

    [Test]
    public void SameOpeningInputsKeepTheSamePlanAndPreviouslyOccurredIdsAreExcludedEvenIfRepeatsIsTrue()
    {
        var catalog = Catalog(); var setup = MotorTestKit.Lanches();
        var start = InitialStateFactory.Create(catalog, setup);
        start.EventHistory.Add(new EventRecord { EventId = EventIds.StaffAbsent, AbsDay = 1 });
        start.Month = 2;
        var a = new MonthSimulation(catalog, setup, start, new MonthPolicies()).ReserveMonthlyEvents();
        var b = new MonthSimulation(catalog, setup, start, new MonthPolicies()).ReserveMonthlyEvents();
        Assert.AreEqual(JsonConvert.SerializeObject(a), JsonConvert.SerializeObject(b));
        Assert.IsFalse(a.Events.Any(e => e.EventId == EventIds.StaffAbsent));
    }

    [Test]
    public void ServiceUsesMonthlyReservationsByDefaultAndReportKeepsThePlanForExactReplay()
    {
        var catalog = Catalog(); var setup = MotorTestKit.Lanches();
        var gateway = new InMemorySimulationGateway(catalog); gateway.CreateSession(setup);
        var service = new SystemicMonthService(gateway);
        var sim = service.Begin(setup.SessionId);
        Assert.IsNotNull(sim.EventPlan);
        Autopilot.RunToEnd(sim);
        var (result, report) = service.Finish();
        Assert.IsTrue(result.Ok);
        Assert.AreSame(sim.EventPlan, report.EventPlan);
        Assert.Less(Math.Abs(report.BalanceDifference), .01);
    }

    [Test]
    public void LoanCashShortfallStillRequiresAndAppliesTheFinancialDecision()
    {
        var catalog = Catalog(); var setup = MotorTestKit.Lanches();
        setup.CreditLineId = catalog.CreditLines.Keys.First(); setup.LoanAmount = 10000;
        var start = InitialStateFactory.Create(catalog, setup);
        start.RetainedEarnings += -100000 - start.Cash; start.Cash = -100000; start.Month = 3;
        var plan = new MonthSimulation(catalog, setup, start, new MonthPolicies()).ReserveMonthlyEvents();
        Assert.AreEqual(4, plan.TargetCount);
        var sim = new MonthSimulation(catalog, setup, start, new MonthPolicies(), plan);
        Autopilot.RunToEnd(sim);
        Assert.AreEqual(1, sim.Occurrences.Count(e => e.Definition.Id == EventIds.CashShortfall));
        Assert.LessOrEqual(sim.Occurrences.Count, 5);
        Assert.Greater(sim.Trace.LoanInterest + sim.Trace.LoanAmortization, 0);
    }

    [Test]
    public void ExhaustedCatalogDoesNotInventOrRepeatIneligibleEvents()
    {
        var catalog = Catalog(); var rng = new DeterministicRandom(1);
        var definition = catalog.Events.First(e => e.Scope == EventScope.Global);
        var plan = MonthlyEventPlanner.Draw(catalog, 3, rng,
            new[] { new MonthlyEventCandidate { Definition = definition, Weight = 1 } }, new[] { definition.Id });
        Assert.IsEmpty(plan.Events);
    }

    [Test]
    public void LegacyAndSystemicAbsenceIdsShareTheSameCampaignExclusion()
    {
        var catalog = Catalog();
        var old = new EventDefinition { Id = "insufficient_team", Title = "Funcionário ausente", BaseRatePerMonth = 1 };
        var current = catalog.Event(EventIds.StaffAbsent);
        var candidates = new[] { new MonthlyEventCandidate { Definition = current, Weight = 1 },
            new MonthlyEventCandidate { Definition = old, Weight = 1 } };
        var plan = MonthlyEventPlanner.Draw(catalog, 1, new DeterministicRandom(1), candidates, Array.Empty<string>());
        Assert.AreEqual(1, plan.Events.Count);
        var next = MonthlyEventPlanner.Draw(catalog, 2, new DeterministicRandom(1), candidates, new[] { "insufficient_team" });
        Assert.IsEmpty(next.Events);
    }
}
