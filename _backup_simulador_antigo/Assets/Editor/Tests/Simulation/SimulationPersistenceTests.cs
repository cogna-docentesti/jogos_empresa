using System.Collections.Generic;
using System.Linq;
using Game.Adapter.Out.Simulation;
using Game.Application.Ports.Out;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using Game.Infrastructure.Session;
using NUnit.Framework;
using SQLite4Unity3d;
using UnityEngine;

namespace Game.Tests.Simulation
{
    /// <summary>
    /// Integracao do simulador com os assets reais (Resources), o SQLite e o GameSessionState.
    /// Banco em memoria (":memory:"), como em PersistenceAndLocationTests: nao toca no game.db.
    /// Window > General > Test Runner > EditMode > Run All.
    /// </summary>
    public class SimulationPersistenceTests
    {
        private GameObject _databaseObject;
        private SQLiteConnection _db;

        [SetUp]
        public void SetUp()
        {
            GameSessionState.Clear();
            PlayerSession.ClearDecisions();
            _databaseObject = new GameObject("TestDatabase");
            var service = _databaseObject.AddComponent<DatabaseService>();
            _db = new SQLiteConnection(":memory:");
            typeof(DatabaseService).GetProperty("Connection").SetValue(service, _db);
            typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, service);
            _db.CreateTable<GameSessionEntity>();
            _db.CreateTable<RoundResultEntity>();
            _db.CreateTable<SessionEventHistoryEntity>();
            _db.CreateTable<LocationEntity>();
        }

        [TearDown]
        public void TearDown()
        {
            GameSessionState.Clear();
            PlayerSession.ClearDecisions();
            _db?.Close();
            typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, null);
            if (_databaseObject != null) Object.DestroyImmediate(_databaseObject);
        }

        private static GameSessionEntity NewSavedSession(string userId, RestaurantType type = RestaurantType.PODRAO,
            LocationZone zone = LocationZone.Comercio, Segment segment = Segment.LOW)
        {
            var session = new GameSessionService(userId, "prof_teste").CreateNewSession();
            session.cityId = "sp";
            GameSessionState.SetLocation(zone);
            GameSessionState.SetRestaurant(type);
            GameSessionState.SetTargetSegment(segment);
            var restaurant = Resources.LoadAll<RestaurantData>("Restaurants").Single(r => r.type == type);
            GameSessionState.SetMenuPricingJson(MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(restaurant.products)));
            var team = new TeamSelectionData();
            foreach (var requirement in restaurant.requiredRoles) TeamSelectionHelper.AddMember(team, requirement.roleId, requirement.quantity);
            GameSessionState.SetTeamJson(TeamSelectionHelper.ToJson(team), save: false);
            var equipment = Resources.LoadAll<EquipmentData>("Equipments")
                .Where(e => e.category == EquipmentCategory.BASIC || (e.applicableTypes != null && e.applicableTypes.Contains(type)))
                .ToList();
            var ids = new EquipmentSelectionData();
            EquipmentSelectionHelper.AddUniqueIds(ids, equipment.Select(e => e.id));
            GameSessionState.SetEquipmentJson(EquipmentSelectionHelper.ToJson(ids), save: false);
            GameSessionState.AddCash(-equipment.Sum(e => e.cost), save: false);
            Assert.IsTrue(GameSessionState.Persist(), "a sessao de teste precisa ser gravada");
            return GameSessionState.Current;
        }

        private SystemicMonthService NewService()
            => new SystemicMonthService(new SqliteSimulationGateway(_db, new UnitySimulationCatalogProvider())) { OutlookRuns = 2 };

        private static void RunToEnd(SystemicMonthService service)
        {
            int guard = 0;
            while (!service.IsFinished && guard++ < 1000)
            {
                service.Step();
                while (service.IsAwaitingDecision)
                {
                    var pending = service.Simulation.Pending;
                    service.Choose(pending.OccurrenceId, new DefaultReactionPolicy().Choose(pending, null));
                }
            }
        }

        [Test]
        public void Catalogo_montado_dos_assets_e_valido()
        {
            var provider = new UnitySimulationCatalogProvider();
            var catalog = provider.Load();
            Assert.IsEmpty(catalog.Validate(), string.Join("\n", catalog.Validate()));
            Assert.AreEqual(5, catalog.Locations.Count);
            Assert.AreEqual(3, catalog.Restaurants.Count);
            Assert.IsTrue(catalog.Restaurants.Values.All(r => r.Products.Count == 3));
        }

        [Test]
        public void Fechamento_grava_o_mes_inteiro_e_avanca_a_sessao()
        {
            var session = NewSavedSession("aluna");
            var service = NewService();
            Assert.IsTrue(service.Begin(), service.LastError);
            RunToEnd(service);
            var report = service.Finish();
            Assert.IsTrue(service.LastCommit.Success, service.LastCommit.Error);

            var row = new RoundResultRepository(_db).GetBySessionAndRound(session.sessionId, 1);
            Assert.IsNotNull(row);
            Assert.AreEqual(row.grossRevenue - row.TotalCosts + row.eventCashImpact, row.netResult, 1.0);
            Assert.AreEqual(report.cashResult.closingCash, row.closingCash, 0.05);
            Assert.AreEqual(report.events.Count, new SessionEventHistoryRepository(_db).GetBySessionAndRound(session.sessionId, 1).Count);
            Assert.IsNotNull(new SimulationMonthRepository(_db).GetBySessionAndMonth(session.sessionId, 1));
            Assert.AreEqual(1, new SimulationStateRepository(_db).GetBySessionId(session.sessionId).lastClosedMonth);
            Assert.AreEqual(2, GameSessionState.Current.currentRound);
            Assert.AreEqual(2, new GameSessionRepository(_db).GetById(session.sessionId).currentRound);
            Assert.IsFalse(GameSessionState.HasUnsavedChanges);
        }

        [Test]
        public void Falha_no_fechamento_nao_deixa_rastro_e_permite_tentar_de_novo()
        {
            var session = NewSavedSession("aluna");
            var service = NewService();
            Assert.IsTrue(service.Begin(), service.LastError);
            var intruder = new RoundResultEntity { roundResultId = "intruso", sessionId = session.sessionId, round = 1 };
            _db.Insert(intruder);
            RunToEnd(service);
            service.Finish();
            Assert.IsFalse(service.LastCommit.Success);
            Assert.AreEqual(0, new SessionEventHistoryRepository(_db).GetBySession(session.sessionId).Count);
            Assert.AreEqual(0, new SimulationMonthRepository(_db).GetBySessionId(session.sessionId).Count);
            Assert.AreEqual(1, GameSessionState.Current.currentRound);

            _db.Delete(intruder);
            Assert.IsTrue(service.RetryCommit().Success);
            Assert.AreEqual(2, GameSessionState.Current.currentRound);
        }

        [Test]
        public void Partida_recarregada_continua_do_estado_gravado()
        {
            var session = NewSavedSession("aluna", RestaurantType.JAPONES, LocationZone.Financas, Segment.MEDIUM);
            var service = NewService();
            Assert.IsTrue(service.Begin());
            RunToEnd(service);
            service.Finish();
            Assert.IsTrue(service.LastCommit.Success);

            GameSessionState.Clear();
            GameSessionState.LoadActiveSession("aluna");
            Assert.IsTrue(GameSessionState.HasActiveSession);
            var reloaded = NewService();
            Assert.IsTrue(reloaded.Begin(), reloaded.LastError);
            Assert.AreEqual(2, reloaded.Simulation.Month);
            Assert.IsNotNull(reloaded.Simulation.Inputs.State, "o estado do mes 1 precisa voltar do banco");
            Assert.AreEqual(1, reloaded.Simulation.Inputs.State.lastClosedMonth);
        }

        [Test]
        public void Politicas_so_sao_gravadas_depois_do_confirm_da_D3()
        {
            new GameSessionService("aluna", "prof_teste").CreateNewSession();
            var gateway = new SqliteSimulationGateway(_db, new UnitySimulationCatalogProvider());
            gateway.SavePolicies(GameSessionState.Current.sessionId, new Dictionary<string, string> { ["marketing"] = "high" });
            Assert.IsNull(new SimulationStateRepository(_db).GetBySessionId(GameSessionState.Current.sessionId));
            GameSessionState.Clear();

            var session = NewSavedSession("aluna");
            var service = NewService();
            string option = service.PolicyCategories().First(p => p.id == "marketing").options.Last().id;
            service.SavePolicies(session.sessionId, new Dictionary<string, string> { ["marketing"] = option });
            Assert.AreEqual(option, service.CurrentPolicies(session.sessionId)["marketing"]);
        }

        [Test]
        public void Ciclo_completo_encerra_a_partida()
        {
            var session = NewSavedSession("aluna");
            var service = NewService();
            for (int month = 1; month <= 3 && GameSessionState.Current.status == GameSessionStatus.IN_PROGRESS; month++)
            {
                Assert.IsTrue(service.Begin(), service.LastError);
                RunToEnd(service);
                service.Finish();
                Assert.IsTrue(service.LastCommit.Success, service.LastCommit.Error);
            }
            var stored = new GameSessionRepository(_db).GetById(session.sessionId);
            Assert.AreNotEqual(GameSessionStatus.IN_PROGRESS, stored.status);
            Assert.IsTrue(stored.completedAt.HasValue);
            Assert.IsFalse(service.Begin());
        }
    }
}
