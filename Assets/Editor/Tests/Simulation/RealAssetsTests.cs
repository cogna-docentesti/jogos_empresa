// Assets/Editor/Tests/Simulation/RealAssetsTests.cs
// Testes com os ScriptableObjects REAIS do jogo (Resources/Locations, Restaurants, Products, Roles,
// Equipments, CreditLines, Events) e o SimulationConfig gerado pelo menu do editor.
using System;
using System.Linq;
using Game.Adapter.Out.Simulation;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using NUnit.Framework;
using UnityEngine;

public class RealAssetsTests
{
    private SimulationCatalog _cat;

    [SetUp]
    public void SetUp()
    {
        var config = ScriptableObject.CreateInstance<SimulationConfigAsset>();
        SimulationConfigBuilder.Fill(config);
        _cat = new UnitySimulationCatalogProvider(config).Build();
    }

    [Test] public void Catalogo_TemAsCincoRegioesEOsTresRestaurantes()
    {
        CollectionAssert.AreEquivalent(new[] { "bank", "store", "marketing", "university", "condominium" }, _cat.Regions.Keys.ToArray());
        Assert.AreEqual(3, _cat.Restaurants.Count);
        Assert.IsTrue(_cat.Restaurants.Values.All(r => r.Products.Count == 3));
    }

    [Test] public void Regiao_VemDoLocationData()
    {
        var edu = _cat.Regions["university"];
        Assert.AreEqual(6000, edu.Rent, 0.01);
        Assert.AreEqual(65, edu.BaseDemandPerDay, 0.01);
        Assert.AreEqual(55, edu.PhysicalCapacityPerDay, 0.01);
        Assert.AreEqual(0.90, edu.PriceFactor, 0.001);
        Assert.AreEqual(1.25, edu.CompetitionPressure, 0.001);
    }

    [Test] public void CustoDosPratos_FichaTecnicaDoGdd()
    {
        var costs = _cat.Restaurants[RestaurantType.PODRAO].Products.OrderBy(p => p.ReferencePrice).Select(p => p.UnitCost).ToArray();
        Assert.AreEqual(14, costs[0], 0.05);
        Assert.AreEqual(19, costs[1], 0.05);
        Assert.AreEqual(27, costs[2], 0.05);
    }

    [Test] public void TodoEquipamentoDoJogo_TemPerfilDeConfiabilidade()
    {
        Assert.AreEqual(16, _cat.Equipment.Count);
        Assert.IsTrue(_cat.Equipment.Keys.All(SimulationConfigBuilder.EquipmentProfiles.ContainsKey));
    }

    [Test] public void Credito_TresLinhasSemASemCredito()
        => CollectionAssert.AreEquivalent(new[] { "microcredito_empresarial", "credito_empresarial_padrao", "credito_de_investimento" }, _cat.CreditLines.Keys.ToArray());

    [Test] public void EventosDoProjeto_TodosTemReacaoSemCusto()
    {
        Assert.GreaterOrEqual(_cat.Events.Count, 20);
        foreach (var e in _cat.Events.Where(e => e.RequiresDecision && !e.Deterministic))
            Assert.IsTrue(e.Reactions.Any(r => r.CostKind == ReactionCostKind.None || r.CostValue <= 0), e.Id);
    }

    [Test] public void TresMesesComOsAssetsReais_BalancoFechaEVende()
    {
        var setup = new SessionSetup
        {
            SessionId = "real-lanches", Seed = 4242, RegionId = "university", Restaurant = RestaurantType.PODRAO, TargetSegment = Segment.LOW,
            EquipmentIds = { "bancada_inox_preparo", "conjunto_panelas_industriais", "fogao_industrial_6_bocas", "freezer_horizontal",
                             "geladeira_comercial_dupla", "mobiliario_basico", "pia_industrial_inox", "sistema_pdv_computador",
                             "chapa_profissional_podrao", "fritadeira_eletrica_dupla_podrao" },
            Team = { RoleType.ATENDENTE, RoleType.ATENDENTE, RoleType.CHAPEIRO }
        };
        foreach (var p in _cat.Restaurants[RestaurantType.PODRAO].Products) setup.Menu.Add(new MenuPrice(p.Id, p.ReferencePrice));
        Assert.AreEqual(0, SetupRules.Validate(_cat, setup).errors.Count);

        var gw = new InMemorySimulationGateway(_cat);
        gw.CreateSession(setup);
        var svc = new SystemicMonthService(gw);
        for (int m = 1; m <= 3; m++)
        {
            var sim = svc.Begin(setup.SessionId);
            Autopilot.RunToEnd(sim);
            var (result, report) = svc.Finish();
            Assert.IsTrue(result.Ok);
            Assert.Less(Math.Abs(report.BalanceDifference), 0.01);
            Assert.Greater(report.Orders, 0);
        }
    }
}
