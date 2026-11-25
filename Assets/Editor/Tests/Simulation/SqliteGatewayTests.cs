// Assets/Editor/Tests/Simulation/SqliteGatewayTests.cs
// Ponta a ponta dentro do Unity com o SQLite de verdade (SQLite4Unity3d, banco em memória: não toca no game.db).
// GameSessionEntity gravada -> SqliteSimulationGateway -> SystemicMonthService -> Commit em transação.
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Adapter.Out.Simulation;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using NUnit.Framework;
using SQLite4Unity3d;
using UnityEngine;
using UnityEngine.TestTools;

public class SqliteGatewayTests
{
    private const string SessionId = "sqlite-e2e";

    private SQLiteConnection _db;
    private SimulationConfigAsset _config;
    private SqliteSimulationGateway _gateway;
    private SystemicMonthService _service;

    [SetUp]
    public void SetUp()
    {
        GameSessionState.Clear();
        _db = new SQLiteConnection(":memory:");
        _db.CreateTable<GameSessionEntity>();
        _db.CreateTable<RoundResultEntity>();
        _db.CreateTable<SessionEventHistoryEntity>();

        var session = NewSession();
        _db.InsertOrReplace(session);
        GameSessionState.Set(session.Copy());

        _config = ScriptableObject.CreateInstance<SimulationConfigAsset>();
        SimulationConfigBuilder.Fill(_config);
        _gateway = new SqliteSimulationGateway(_db, new UnitySimulationCatalogProvider(_config));
        _service = new SystemicMonthService(_gateway);
    }

    [TearDown]
    public void TearDown()
    {
        _db?.Close();
        if (_config != null) UnityEngine.Object.DestroyImmediate(_config);
        GameSessionState.Clear();
    }

    [Test]
    public void SessaoGravada_ViraSetupDoMotor()
    {
        var active = _gateway.LoadActiveMonth(SessionId);
        Assert.AreEqual("university", active.Setup.RegionId, "zona Educacao vira a região university");
        Assert.AreEqual(38, active.Setup.PriceOf("LAN_COMBO_SIMPLES"), 1e-6, "preço do menuPricingJson");
        Assert.AreEqual(3, active.Setup.Team.Count, "teamJson lido");
        Assert.AreEqual(10, active.Setup.EquipmentIds.Count, "equipmentJson lido");
        Assert.AreEqual(65000 + 50000 - 27300, active.State.Cash, 1e-6, "caixa = capital + empréstimo - equipamentos");
    }

    [Test]
    public void TresMeses_GravaTudoNoSqlite()
    {
        Assert.IsTrue(_service.SavePolicies(SessionId, new MonthPolicies { Marketing = "medium" }).Ok);

        for (int m = 1; m <= 3; m++)
        {
            var sim = _service.Begin(SessionId);
            Assert.AreEqual(m, sim.Month);
            Assert.AreEqual("medium", sim.Policies.Marketing, "políticas lidas do SimulationStateEntity");
            Autopilot.RunToEnd(sim);
            var (result, report) = _service.Finish();
            Assert.IsTrue(result.Ok, result.Message);
            Assert.Less(Math.Abs(report.BalanceDifference), 0.01, "balanço fecha");
            Debug.Log($"[SqliteGatewayTests] mês {m}: pedidos {report.Orders}, lucro {report.NetIncome:N0}, variação do caixa {report.CashResult:N0}");
        }

        var saved = _db.Table<GameSessionEntity>().First(x => x.sessionId == SessionId);
        Assert.AreEqual(GameSessionStatus.COMPLETED, saved.status);
        Assert.AreEqual(3, saved.currentRound);
        Assert.AreEqual(GameSessionStatus.COMPLETED, GameSessionState.Current.status, "sessão em memória sincronizada");
        var finalState = _gateway.LoadActiveMonth(SessionId).State;
        var savedTeam = TeamSelectionHelper.FromJson(saved.teamJson);
        Assert.AreEqual(finalState.Staff.Count, savedTeam.members.Sum(m => m.quantity), "teamJson acompanha a equipe do motor (demissões)");

        Assert.AreEqual(3, _db.Table<RoundResultEntity>().Count());
        Assert.AreEqual(3, _db.Table<SimulationMonthEntity>().Count());
        Assert.Greater(_db.Table<SessionEventHistoryEntity>().Count(), 0);

        var rounds = _db.Table<RoundResultEntity>().ToList().OrderBy(r => r.round).ToList();
        var first = rounds[0];
        Assert.Greater(first.grossRevenue, 0);
        Assert.Greater(first.customers, 0);
        Assert.Greater(first.loanPayment, 0);
        Assert.AreEqual(65000 + 50000 - 27300, first.openingCash, 0.05, "caixa inicial do mês 1");

        // As telas do layout novo leem o RoundResultEntity no modelo de caixa: as identidades têm de fechar.
        var monthReports = _gateway.LoadReports(SessionId);
        for (int i = 0; i < rounds.Count; i++)
        {
            var r = rounds[i];
            var rep = monthReports[i];
            Assert.AreEqual(r.closingCash, r.openingCash + r.netResult, 0.05, $"mês {r.round}: início + resultado = fechamento");
            Assert.AreEqual(r.netResult, r.grossRevenue - r.TotalCosts + r.eventCashImpact, 0.05, $"mês {r.round}: receita - saídas + eventos = resultado");
            Assert.AreEqual(rep.CashResult, r.netResult, 0.05, $"mês {r.round}: resultado da tela = variação do caixa do motor");
            Assert.AreEqual(0f, r.otherOperatingCosts, "tudo o que é despesa operacional aparece numa linha visível da tela");
            Assert.LessOrEqual(r.loanPrincipalPayment, r.loanPayment + 0.01f);
            Assert.AreEqual(r.reputationAtEnd - r.reputationAtStart, r.reputationDelta);
            if (i > 0) Assert.AreEqual(rounds[i - 1].closingCash, r.openingCash, 0.05, $"mês {r.round}: abre com o caixa do fechamento anterior");
        }
        Assert.AreEqual(rounds.Last().roundResultId, saved.pendingSummaryResultId, "marca o resultado como ainda não lido");
        Assert.AreEqual(saved.pendingSummaryResultId, GameSessionState.Current.pendingSummaryResultId);

        var reports = monthReports;
        Assert.AreEqual(3, reports.Count, "relatórios voltam do reportJson (JsonUtility)");
        Assert.AreEqual(new[] { 1, 2, 3 }, reports.Select(r => r.Month).ToArray());
        Assert.IsFalse(_service.SavePolicies(SessionId, new MonthPolicies()).Ok, "partida encerrada recusa políticas");

        // o menu "Conferir partida jogada" reproduz os 3 meses gravados e confere tudo
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "conferencia_jogada_teste");
        var provider = new UnitySimulationCatalogProvider(_config);
        var catalog = provider.Build();                                   // Build preenche os mapas zona -> região e cargo -> papel
        string summary = ConferenciaJogada.Run(_db, catalog, provider, folder, SessionId);
        StringAssert.Contains("3 mês(es)", summary);
        StringAssert.Contains(", 0 divergências", summary);
    }

    [Test]
    public void FalhaNoMeioDaTransacao_DesfazTudo_EDepoisRetryGrava()
    {
        var sim = _service.Begin(SessionId);
        Autopilot.RunToEnd(sim);
        Assert.IsTrue(_service.Finish().result.Ok);
        int historyAfterMonth1 = _db.Table<SessionEventHistoryEntity>().Count();

        sim = _service.Begin(SessionId);
        Autopilot.RunToEnd(sim);

        // Tira a tabela de estado: o Commit grava histórico, RoundResult e relatório e só então quebra.
        _db.Execute("DROP TABLE SimulationStateEntity");
        LogAssert.Expect(LogType.Error, new Regex("Falha no Commit"));
        var (bad, _) = _service.Finish();
        Assert.IsFalse(bad.Ok);

        Assert.AreEqual(1, _db.Table<SimulationMonthEntity>().Count(), "relatório do mês 2 desfeito");
        Assert.AreEqual(1, _db.Table<RoundResultEntity>().Count(), "RoundResult do mês 2 desfeito");
        Assert.AreEqual(historyAfterMonth1, _db.Table<SessionEventHistoryEntity>().Count(), "eventos do mês 2 desfeitos");
        Assert.AreEqual(2, _db.Table<GameSessionEntity>().First(x => x.sessionId == SessionId).currentRound, "sessão continua no mês 2");

        _db.CreateTable<SimulationStateEntity>();
        Assert.IsTrue(_service.RetryCommit().Ok, "mesmo pacote gravado sem recalcular");
        Assert.AreEqual(2, _db.Table<SimulationMonthEntity>().Count());
        Assert.AreEqual(3, _db.Table<GameSessionEntity>().First(x => x.sessionId == SessionId).currentRound);
    }

    private static GameSessionEntity NewSession()
    {
        var pricing = new MenuPricingData();
        pricing.items.Add(new MenuPricingItem { productId = "LAN_COMBO_SIMPLES", selectedPrice = 38f });

        var team = new TeamSelectionData();
        team.members.Add(new TeamSelectionItem { roleId = "attendant", quantity = 2 });
        team.members.Add(new TeamSelectionItem { roleId = "grill_cook", quantity = 1 });

        var equipment = new EquipmentSelectionData();
        equipment.equipmentIds.AddRange(new[]
        {
            "bancada_inox_preparo", "conjunto_panelas_industriais", "fogao_industrial_6_bocas", "freezer_horizontal",
            "geladeira_comercial_dupla", "mobiliario_basico", "pia_industrial_inox", "sistema_pdv_computador",
            "chapa_profissional_podrao", "fritadeira_eletrica_dupla_podrao"
        });

        return new GameSessionEntity
        {
            sessionId = SessionId, userId = "teste", professorId = "teste", status = GameSessionStatus.IN_PROGRESS,
            currentRound = 1, cityId = "teste",
            restaurantType = RestaurantType.PODRAO, locationZone = LocationZone.Educacao, targetSegment = Segment.LOW,
            menuPricingJson = MenuPricingHelper.ToJson(pricing), coherenceRating = "x",
            creditLineId = "microcredito_empresarial", loanBalance = 50000,
            teamJson = TeamSelectionHelper.ToJson(team), equipmentJson = EquipmentSelectionHelper.ToJson(equipment)
        };
    }
}
