// Assets/Editor/Tests/Simulation/MotorTests.cs
// Testes EditMode do motor (Window > General > Test Runner > EditMode).
// Usam o catálogo de exemplo (SampleCatalog) e o gateway em memória: não abrem cena nem banco.
using System;
using System.Linq;
using Game.Application.Ports.Out;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using NUnit.Framework;
using UnityEngine;

public static class MotorTestKit
{
    public static SessionSetup Lanches(string region = "university", Segment target = Segment.LOW, int seed = 12345)
    {
        var s = new SessionSetup
        {
            SessionId = "sessao-lanches-" + region + "-" + seed, Seed = seed, RegionId = region,
            Restaurant = RestaurantType.PODRAO, TargetSegment = target,
            EquipmentIds = { "bancada", "panelas", "fogao6", "freezer", "geladeira", "mobiliario", "pia", "pdv", "chapa", "fritadeira" },
            Team = { RoleType.ATENDENTE, RoleType.ATENDENTE, RoleType.CHAPEIRO }
        };
        foreach (var p in SampleCatalog.Build().Restaurants[RestaurantType.PODRAO].Products) s.Menu.Add(new MenuPrice(p.Id, p.ReferencePrice));
        return s;
    }

    public static SessionSetup Frances(string region, int seed = 12345)
    {
        var s = new SessionSetup
        {
            SessionId = "sessao-frances-" + region + "-" + seed, Seed = seed, RegionId = region,
            Restaurant = RestaurantType.FRANCES, TargetSegment = Segment.HIGH,
            EquipmentIds = { "bancada", "panelas", "fogao6", "freezer", "geladeira", "mobiliario", "pia", "pdv", "adega", "forno_combinado", "mobiliario_luxo" },
            Team = { RoleType.ATENDENTE, RoleType.ATENDENTE, RoleType.CHEF }
        };
        foreach (var p in SampleCatalog.Build().Restaurants[RestaurantType.FRANCES].Products) s.Menu.Add(new MenuPrice(p.Id, p.ReferencePrice));
        return s;
    }

    public static double Fit(SimulationCatalog c, SessionSetup s)
        => MarketModel.MarketFit(c, c.Regions[s.RegionId], c.Restaurants[s.Restaurant], s.TargetSegment, s.Menu, 0.5, 0).Fit;

    public static MonthSimulation RunMonth(SimulationCatalog cat, SessionSetup s, MonthPolicies p, SimulationState start = null)
    {
        var sim = new MonthSimulation(cat, s, start ?? InitialStateFactory.Create(cat, s), p);
        Autopilot.RunToEnd(sim);
        return sim;
    }

    public static void PickFree(MonthSimulation sim)
    {
        var occ = sim.PendingDecisions[0];
        var opts = sim.ReactionOptions(occ);
        var pick = opts.FirstOrDefault(o => o.Enabled && o.Cost == 0 && !o.Reaction.Renegotiates) ?? opts.First(o => o.Enabled);
        sim.Choose(occ.OccurrenceId, pick.Reaction.Id);
    }

    /// Joga N meses pelo serviço (como o jogo faz) e devolve a maior diferença de balanço.
    public static double PlayMonths(SimulationCatalog cat, SessionSetup setup, MonthPolicies policies, int months)
    {
        var gw = new InMemorySimulationGateway(cat);
        gw.CreateSession(setup);
        var svc = new SystemicMonthService(gw);
        double worst = 0;
        for (int m = 1; m <= months; m++)
        {
            Assert.IsTrue(svc.SavePolicies(setup.SessionId, policies).Ok);
            var sim = svc.Begin(setup.SessionId);
            while (!sim.IsFinished) { svc.Step(); while (sim.IsAwaitingDecision) PickFree(sim); }
            var (result, report) = svc.Finish();
            Assert.IsTrue(result.Ok, "Commit do mês " + m + ": " + result.Status);
            worst = Math.Max(worst, Math.Abs(report.BalanceDifference));
            if (report.Bankrupt) break;
        }
        return worst;
    }
}

public class MarketAndModelTests
{
    private SimulationCatalog _cat;
    [SetUp] public void SetUp() => _cat = SampleCatalog.Build();

    [Test] public void FrancesNaEducacional_TemMenosDaMetadeDoEncaixeDaFinanceira()
        => Assert.Less(MotorTestKit.Fit(_cat, MotorTestKit.Frances("university")), 0.5 * MotorTestKit.Fit(_cat, MotorTestKit.Frances("bank")));

    [Test] public void PratoAcessivel_MelhoraOEncaixe()
    {
        var cheap = MotorTestKit.Lanches();
        cheap.Menu[0].Price = 35;
        Assert.Greater(MotorTestKit.Fit(_cat, cheap), MotorTestKit.Fit(_cat, MotorTestKit.Lanches()));
    }

    [Test] public void LanchesNaFinanceira_EncaixeAcimaDe09()
        => Assert.Greater(MotorTestKit.Fit(_cat, MotorTestKit.Lanches("bank")), 0.9);

    [Test] public void Aceitacao_Combo42_ClasseC_Educacional()
        => Assert.AreEqual(0.469, MarketModel.Acceptance(42, 45, 0.90, 1.15), 0.001);

    [Test] public void FreezerIntermediarioNovo_Risco0233PorDia()
    {
        var hz = ReliabilityModel.DailyHazard(_cat.Categories[EquipmentReliabilityCategory.Refrigeration], _cat.Levels[EquipmentLevel.Standard],
                                              0, 0.8, 0, 0, 1, 1, _cat.Constants);
        Assert.AreEqual(0.00233, hz.Probability, 0.00002);
    }

    [Test] public void Erros_OcupacaoLimitadaA125()
        => Assert.AreEqual(0.04 + 0.12 * 0.25, OperationsModel.ErrorRate(1.0, 6.0), 1e-9);
}

public class DeterminismTests
{
    private SimulationCatalog _cat;
    [SetUp] public void SetUp() => _cat = SampleCatalog.Build();

    [Test] public void MesmaSemente_MesmoMes()
    {
        var a = MotorTestKit.RunMonth(_cat, MotorTestKit.Lanches(), new MonthPolicies());
        var b = MotorTestKit.RunMonth(_cat, MotorTestKit.Lanches(), new MonthPolicies());
        CollectionAssert.AreEqual(a.DayResults.Select(d => d.Revenue).ToList(), b.DayResults.Select(d => d.Revenue).ToList());
    }

    [Test] public void SairNoDia17EVoltar_RepeteOsDias1a16()
    {
        var full = MotorTestKit.RunMonth(_cat, MotorTestKit.Lanches(), new MonthPolicies());
        var partial = new MonthSimulation(_cat, MotorTestKit.Lanches(), InitialStateFactory.Create(_cat, MotorTestKit.Lanches()), new MonthPolicies());
        while (partial.DayResults.Count < 16) { if (partial.IsAwaitingDecision) MotorTestKit.PickFree(partial); else partial.AdvanceDay(); }
        CollectionAssert.AreEqual(full.DayResults.Take(16).Select(d => d.Revenue).ToList(), partial.DayResults.Select(d => d.Revenue).ToList());
    }

    [Test] public void SementeDiferente_MesDiferente()
    {
        var a = MotorTestKit.RunMonth(_cat, MotorTestKit.Lanches(), new MonthPolicies());
        var c = MotorTestKit.RunMonth(_cat, MotorTestKit.Lanches(seed: 999), new MonthPolicies());
        CollectionAssert.AreNotEqual(a.DayResults.Select(d => d.Revenue).ToList(), c.DayResults.Select(d => d.Revenue).ToList());
    }

    [Test] public void EstadoGravadoEmJsonUtility_GeraOMesmoMesSeguinte()
    {
        var s = MotorTestKit.Lanches(seed: 21);
        var next = MonthCloser.Close(MotorTestKit.RunMonth(_cat, s, new MonthPolicies())).NextState;
        var reloaded = JsonUtility.FromJson<SimulationState>(JsonUtility.ToJson(next));
        var a2 = MotorTestKit.RunMonth(_cat, s, new MonthPolicies(), next);
        var b2 = MotorTestKit.RunMonth(_cat, s, new MonthPolicies(), reloaded);
        CollectionAssert.AreEqual(a2.DayResults.Select(d => d.Revenue).ToList(), b2.DayResults.Select(d => d.Revenue).ToList());
        Assert.AreEqual(a2.State.Cash, b2.State.Cash, 1e-6);
    }
}

public class FinanceTests
{
    private SimulationCatalog _cat;
    [SetUp] public void SetUp() => _cat = SampleCatalog.Build();

    [Test] public void BalancoFecha_Padrao()
        => Assert.Less(MotorTestKit.PlayMonths(_cat, MotorTestKit.Lanches(), new MonthPolicies(), 3), 0.01);

    [Test] public void BalancoFecha_CreditoParceladoEconomico()
    {
        var s = MotorTestKit.Lanches(seed: 7);
        s.CreditLineId = "padrao";
        s.LoanAmount = 120000;
        var p = new MonthPolicies { SupplierPayment = "installments", Supplier = "economy", Marketing = "high", Maintenance = "preventive" };
        Assert.Less(MotorTestKit.PlayMonths(_cat, s, p, 3), 0.01);
    }

    [Test] public void BalancoFecha_FrancesNaEducacional()
        => Assert.Less(MotorTestKit.PlayMonths(_cat, MotorTestKit.Frances("university", 3),
                                               new MonthPolicies { Training = "advanced", Pay = "above", Loyalty = "program" }, 3), 0.01);

    [Test] public void ReacaoComGanho_EntraComoOutraReceita()
    {
        var cat = SampleCatalog.Build();
        cat.Events.Add(new EventDefinition
        {
            Id = "corporate_order_test", Title = "Pedido corporativo", Scope = EventScope.Global, BaseRatePerMonth = 30, RequiresDecision = true,
            Reactions = { new ReactionDefinition { Id = "accept", Label = "Aceitar", CostKind = ReactionCostKind.Fixed, CostValue = -800 } }
        });
        var sim = new MonthSimulation(cat, MotorTestKit.Lanches(), InitialStateFactory.Create(cat, MotorTestKit.Lanches()), new MonthPolicies());
        while (!sim.IsFinished)
        {
            sim.AdvanceDay();
            while (sim.IsAwaitingDecision)
            {
                var occ = sim.PendingDecisions[0];
                if (occ.Definition.Id == "corporate_order_test") sim.Choose(occ.OccurrenceId, "accept");
                else MotorTestKit.PickFree(sim);
            }
        }
        Assert.Greater(sim.Books.Total(DreLine.OtherIncome), 0);
        Assert.Less(Math.Abs(MonthCloser.Close(sim).Report.BalanceDifference), 0.01);
    }
}

public class ApplicationRulesTests
{
    private SimulationCatalog _cat;
    [SetUp] public void SetUp() => _cat = SampleCatalog.Build();

    private (InMemorySimulationGateway gw, SystemicMonthService svc, MonthSimulation sim, SessionSetup s) MonthRunning()
    {
        var gw = new InMemorySimulationGateway(_cat);
        var s = MotorTestKit.Lanches(seed: 5);
        gw.CreateSession(s);
        var svc = new SystemicMonthService(gw);
        var sim = svc.Begin(s.SessionId);
        sim.AdvanceDay();
        while (sim.IsAwaitingDecision) MotorTestKit.PickFree(sim);
        return (gw, svc, sim, s);
    }

    [Test] public void Politicas_TravadasComOMesRodando()
    {
        var (_, svc, _, s) = MonthRunning();
        Assert.IsFalse(svc.SavePolicies(s.SessionId, new MonthPolicies { Marketing = "high" }).Ok);
    }

    [Test] public void Politicas_RecusadasAntesDeAPartidaExistir()
        => Assert.IsFalse(new SystemicMonthService(new InMemorySimulationGateway(_cat)).SavePolicies("nao-existe", new MonthPolicies()).Ok);

    [Test] public void CommitFalhou_RetryGravaSemRecalcular_EDuplicadoERecusado()
    {
        var (gw, svc, sim, _) = MonthRunning();
        Autopilot.RunToEnd(sim);
        gw.FailNextCommit = true;
        var (first, _) = svc.Finish();
        Assert.IsFalse(first.Ok);
        Assert.IsTrue(svc.RetryCommit().Ok);
        Assert.AreEqual(CommitStatus.AlreadyClosed, svc.RetryCommit().Status);
    }

    [Test] public void Abandonar_LiberaAsPoliticasEORecomecoDoMes()
    {
        var (_, svc, _, s) = MonthRunning();
        svc.Abandon();
        Assert.IsTrue(svc.SavePolicies(s.SessionId, new MonthPolicies()).Ok);
        Assert.AreEqual(0, svc.Begin(s.SessionId).Day);
    }

    [Test] public void ValidacaoDaMontagem()
    {
        var (errors, warnings) = SetupRules.Validate(_cat, new SessionSetup
        {
            RegionId = "university", Restaurant = RestaurantType.FRANCES, TargetSegment = Segment.LOW,
            Menu = MotorTestKit.Frances("bank").Menu, Team = { RoleType.ATENDENTE }
        });
        Assert.AreEqual(2, errors.Count, "público não liberado + sem especialista de cozinha");
        Assert.AreEqual(1, warnings.Count, "um atendente só: abaixo do mínimo");
    }
}
