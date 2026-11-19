// Assets/Editor/Tests/Simulation/ConferenciaTests.cs
// Roda cenários de decisão pelo serviço (como o jogo faz) e passa cada mês pela Conferencia:
// todas as fórmulas do dia e do fechamento recalculadas de forma independente têm de bater com o motor.
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using NUnit.Framework;

public static class ConferenciaRunner
{
    /// Escolhe reações variadas (com custo, sem custo, com ganho, cheque especial) para exercitar todas as linhas.
    public static void PickRotating(MonthSimulation sim)
    {
        var occ = sim.PendingDecisions[0];
        var enabled = sim.ReactionOptions(occ).Where(o => o.Enabled).ToList();
        sim.Choose(occ.OccurrenceId, enabled[(sim.Day + sim.Month) % enabled.Count].Reaction.Id);
    }

    public sealed class MonthRun
    {
        public int Month;
        public MonthSimulation Sim;
        public MonthReport Report;
        public SimulationState Start;
        public List<CheckLine> Checks;
    }

    public static List<MonthRun> Play(SimulationCatalog cat, SessionSetup setup, MonthPolicies policies, int months,
                                      Action<MonthSimulation> picker = null, Action<InMemorySimulationGateway, int> betweenMonths = null)
    {
        picker ??= PickRotating;
        var gw = new InMemorySimulationGateway(cat);
        gw.CreateSession(setup);
        var svc = new SystemicMonthService(gw);
        var runs = new List<MonthRun>();
        for (int m = 1; m <= months; m++)
        {
            if (m > 1) betweenMonths?.Invoke(gw, m);
            Assert.IsTrue(svc.SavePolicies(setup.SessionId, policies).Ok);
            var start = gw.LoadActiveMonth(setup.SessionId).State.Clone();
            var sim = svc.Begin(setup.SessionId);
            while (!sim.IsFinished) { svc.Step(); while (sim.IsAwaitingDecision) picker(sim); }
            var (result, report) = svc.Finish();
            Assert.IsTrue(result.Ok, "Commit do mês " + m + ": " + result.Status);
            runs.Add(new MonthRun { Month = m, Sim = sim, Report = report, Start = start, Checks = Conferencia.Month(sim, report, start) });
            if (report.Bankrupt) break;
        }
        return runs;
    }

    public static void AssertAllOk(List<MonthRun> runs)
    {
        var bad = runs.SelectMany(r => r.Checks.Where(c => !c.Ok).Select(c => $"mês {r.Month} {c}")).ToList();
        Assert.IsEmpty(bad, $"{bad.Count} conferências falharam:\n" + string.Join("\n", bad.Take(25)));
        Assert.Greater(runs.Sum(r => r.Checks.Count), 1000, "a conferência precisa cobrir dias e fechamento");
    }
}

public class ConferenciaTests
{
    private SimulationCatalog _cat;

    [SetUp] public void SetUp() => _cat = SampleCatalog.Build();

    private static SessionSetup Japones(string region, int seed)
    {
        var s = new SessionSetup
        {
            SessionId = "sessao-japones-" + region + "-" + seed, Seed = seed, RegionId = region,
            Restaurant = RestaurantType.JAPONES, TargetSegment = Segment.MEDIUM,
            EquipmentIds = { "bancada", "panelas", "fogao6", "freezer", "geladeira", "mobiliario", "pia", "pdv", "balcao_refrigerado", "facas", "panela_arroz" },
            Team = { RoleType.ATENDENTE, RoleType.ATENDENTE, RoleType.SUSHIMAN }
        };
        foreach (var p in SampleCatalog.Build().Restaurants[RestaurantType.JAPONES].Products) s.Menu.Add(new MenuPrice(p.Id, p.ReferencePrice));
        return s;
    }

    [Test] public void Lanches_PoliticasPadrao_TresMeses()
        => ConferenciaRunner.AssertAllOk(ConferenciaRunner.Play(_cat, MotorTestKit.Lanches(), new MonthPolicies(), 3));

    [Test] public void Frances_TodasAsPoliticasLigadas_TresMeses()
    {
        var setup = MotorTestKit.Frances("bank", 777);
        setup.Team.Add(RoleType.GERENTE);
        setup.CreditLineId = "padrao"; setup.LoanAmount = 80000;
        var policies = new MonthPolicies
        {
            Marketing = "high", Loyalty = "program", Supplier = "premium", SupplierPayment = "installments",
            Maintenance = "preventive", Stock = "high", Pay = "above", Training = "advanced"
        };
        ConferenciaRunner.AssertAllOk(ConferenciaRunner.Play(_cat, setup, policies, 3));
    }

    [Test] public void Japones_FornecedorEconomico_EstoqueBaixo_TresMeses()
    {
        var policies = new MonthPolicies
        {
            Marketing = "low", Supplier = "economy", Stock = "low", Pay = "below", Maintenance = "intensive", Training = "basic"
        };
        ConferenciaRunner.AssertAllOk(ConferenciaRunner.Play(_cat, Japones("store", 99), policies, 3));
    }

    [Test] public void Lanches_Sobrecarregado_UmAtendente()
    {
        var setup = MotorTestKit.Lanches("store", Segment.MEDIUM, 4242);
        setup.Team.Clear();
        setup.Team.AddRange(new[] { RoleType.ATENDENTE, RoleType.CHAPEIRO });
        foreach (var m in setup.Menu) m.Price = _cat.Restaurants[RestaurantType.PODRAO].Products.First(p => p.Id == m.ProductId).MinPrice;
        var runs = ConferenciaRunner.Play(_cat, setup, new MonthPolicies { Marketing = "high" }, 3);
        ConferenciaRunner.AssertAllOk(runs);
        Assert.IsTrue(runs[0].Sim.DayResults.Any(d => d.Occupancy > 1), "o cenário precisa ter dias acima da capacidade");
    }

    [Test] public void Frances_NaEducacional_ChequeEspecial()
    {
        var setup = MotorTestKit.Frances("university", 31337);
        foreach (var m in setup.Menu) m.Price = _cat.Restaurants[RestaurantType.FRANCES].Products.First(p => p.Id == m.ProductId).MaxPrice;
        setup.CreditLineId = "investimento"; setup.LoanAmount = 20000;
        var runs = ConferenciaRunner.Play(_cat, setup, new MonthPolicies { Marketing = "high", Training = "advanced", Pay = "above" }, 3);
        ConferenciaRunner.AssertAllOk(runs);
        Assert.IsTrue(runs.Any(r => r.Sim.DayResults.Any(d => d.Trace.OverdraftInterest > 0)), "o cenário precisa entrar no cheque especial");
    }

    [Test] public void ContratacaoECompraNoHub_EntramNoMesSeguinte_ComConferencia()
    {
        var setup = MotorTestKit.Lanches("bank", Segment.MEDIUM, 2024);
        var runs = ConferenciaRunner.Play(_cat, setup, new MonthPolicies(), 3, betweenMonths: (gw, month) =>
        {
            if (month == 2) gw.ChangeSetup(setup.SessionId, s => { s.Team.Add(RoleType.ATENDENTE); s.EquipmentIds.Add("chapa"); });
        });
        ConferenciaRunner.AssertAllOk(runs);
        Assert.AreEqual(4, runs[1].Start.Staff.Count, "a contratação do hub entrou no mês 2");
        Assert.AreEqual(2, runs[1].Start.Equipment.Count(u => u.ProfileId == "chapa"), "a compra do hub entrou no mês 2");
        double cashAfterClose1 = runs[0].Sim.State.Cash;
        Assert.AreEqual(cashAfterClose1 - _cat.Equipment["chapa"].Price, runs[1].Start.Cash, 0.01, "o caixa pagou a chapa nova");
    }
}

public class IntegracaoHubTests
{
    private SimulationCatalog _cat;

    [SetUp] public void SetUp() => _cat = SampleCatalog.Build();

    [Test] public void SemEquipe_PlanejamentoBloqueia_EBeginRecusa()
    {
        var setup = MotorTestKit.Lanches();
        setup.Team.Clear();
        var gw = new InMemorySimulationGateway(_cat);
        gw.CreateSession(setup);
        var svc = new SystemicMonthService(gw);
        var planning = svc.Planning(setup.SessionId);
        Assert.IsFalse(planning.CanStart);
        Assert.AreEqual(2, planning.Errors.Count, "sem atendente e sem especialista");
        Assert.Throws<InvalidOperationException>(() => svc.Begin(setup.SessionId));
    }

    [Test] public void UmAtendente_SoAviso_MesComeca()
    {
        var setup = MotorTestKit.Lanches();
        setup.Team.Remove(RoleType.ATENDENTE);
        var (errors, warnings) = SetupRules.Validate(_cat, setup);
        Assert.IsEmpty(errors);
        Assert.AreEqual(1, warnings.Count);
    }

    [Test] public void Reconciliador_ContrataDesligaCompraEEmpresta()
    {
        var setup = MotorTestKit.Lanches();
        var state = InitialStateFactory.Create(_cat, setup);
        state.Month = 2;
        double cash = state.Cash;
        setup.Team.Add(RoleType.GERENTE);
        setup.Team.Remove(RoleType.ATENDENTE);
        setup.EquipmentIds.Add("fritadeira");
        setup.CreditLineId = "micro"; setup.LoanAmount = 10000;

        var changes = SetupReconciler.Apply(_cat, setup, state);

        Assert.AreEqual(1, state.Staff.Count(s => s.Role == RoleType.GERENTE));
        Assert.AreEqual(1, state.Staff.Count(s => s.Role == RoleType.ATENDENTE));
        Assert.AreEqual(2, state.Equipment.Count(u => u.ProfileId == "fritadeira"));
        Assert.AreEqual(10000, state.LoanBalance, 0.01);
        Assert.AreEqual(cash - _cat.Equipment["fritadeira"].Price + 10000, state.Cash, 0.01);
        Assert.AreEqual(4, changes.Count);
        Assert.IsEmpty(SetupReconciler.Apply(_cat, setup, state), "aplicar de novo não muda nada");
    }

    [Test] public void Covariaveis_TemTeto()
    {
        var engine = new EventEngine(_cat, new DeterministicRandom(1));
        var review = _cat.Event("negative_review");
        var x = new Observables();
        x[Observables.Satisfaction] = 0; x[Observables.WaitFactor] = 2.6; x[Observables.LostRatio] = 1.0;
        var state = new SimulationState();
        var ctx = new EventContext { Month = 1, Day = 10, AbsDay = 10, State = state, X = x, Levers = new LeverSet() };
        double h = engine.DailyHazard(review, ctx);
        Assert.AreEqual(review.BaseRatePerMonth / 30 * _cat.Constants.CovariateFactorMax, h, 1e-9);
    }
}
