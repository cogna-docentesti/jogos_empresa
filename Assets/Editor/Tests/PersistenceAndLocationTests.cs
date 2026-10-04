using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.Out.Persistence;
using Game.Infrastructure.Session;
using NUnit.Framework;
using SQLite4Unity3d;
using UnityEngine;

/// <summary>
/// Testes de Edit Mode do merge com a branch events e das tarefas E-04, E-05 e E-06.
/// Usam um banco SQLite em memoria (":memory:"): nao tocam no game.db de verdade.
/// Window > General > Test Runner > aba EditMode > Run All.
/// </summary>
public class PersistenceAndLocationTests
{
    private GameObject _databaseObject;
    private SQLiteConnection _db;

    [SetUp]
    public void SetUp()
    {
        GameSessionState.Clear();
        PlayerSession.ClearDecisions();

        // Mesmo truque da bateria da Thaysla (RoundFlowDebugTest.RunBatch):
        // pluga uma conexao em memoria no DatabaseInitializer.
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
        if (_databaseObject != null)
            Object.DestroyImmediate(_databaseObject);
    }

    // ─────────────────────────────────────────────────────────────
    //  Merge: LoadActiveSession
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void Merge_partida_carregada_do_banco_continua_marcada_como_gravada()
    {
        // Este era o bug do merge: o if sem chaves fazia toda partida carregada
        // ficar com IsPersisted = false, e nenhum Save() seguinte gravava nada.
        var session = NewSavedSession("aluna");

        GameSessionState.Clear();
        GameSessionState.LoadActiveSession("aluna");

        Assert.IsNotNull(GameSessionState.Current, "a partida deveria ter sido carregada");
        Assert.AreEqual(session.sessionId, GameSessionState.Current.sessionId);
        Assert.IsTrue(GameSessionState.IsPersisted, "partida vinda do banco precisa estar marcada como gravada");

        GameSessionState.SetCash(1234f);   // save = true por padrao
        Assert.AreEqual(1234f, _db.Table<GameSessionEntity>().Single().currentCash, "o Save depois de carregar precisa gravar");
    }

    [Test]
    public void Merge_save_do_ciclo_antigo_fica_separado_como_incompativel()
    {
        var session = NewSavedSession("antiga");
        session.currentRound = 7;
        GameSessionState.Save();

        GameSessionState.Clear();
        GameSessionState.LoadActiveSession("antiga");

        Assert.IsTrue(GameSessionState.HasIncompatibleSave);
        Assert.IsNull(GameSessionState.Current);
        Assert.IsFalse(GameSessionState.IsPersisted);
        Assert.Throws<System.Exception>(() => new GameSessionService("antiga", "p").CreateNewSession(),
            "nova sessao so pode nascer pelo RestartSession, depois de confirmar");
    }

    [Test]
    public void Merge_RestartSession_apaga_tudo_e_a_nova_sessao_nasce_so_em_memoria()
    {
        var session = NewSavedSession("restart");
        new SessionEventHistoryRepository(_db).Record(session.sessionId, 1, 1, null, null, -100f, -2, -5);

        var replacement = new GameSessionService("restart", "p").RestartSession();

        Assert.AreEqual(0, _db.Table<GameSessionEntity>().Count(), "a sessao antiga deveria sair do banco");
        Assert.AreEqual(0, _db.Table<SessionEventHistoryEntity>().Count(), "os eventos da sessao antiga deveriam sair do banco");
        Assert.AreNotEqual(session.sessionId, replacement.sessionId);
        Assert.AreSame(replacement, GameSessionState.Current);
        Assert.IsFalse(GameSessionState.IsPersisted, "regra da E-03: a sessao nova so grava no confirm da D3");
    }

    [Test]
    public void Merge_capital_inicial_da_Thaysla_vale_no_confirm_da_D3()
    {
        GameSessionState.Set(new GameSessionService("capital", "p").CreateNewSession());
        PlayerSession.SaveLocation("store", "Área Comercial", LocationZone.Comercio);
        PlayerSession.SaveRestaurant(RestaurantType.FRANCES, Segment.HIGH);
        var menu = new MenuPricingData();
        menu.items.Add(new MenuPricingItem { productId = "X", selectedPrice = 100f });
        PlayerSession.SetMenu(RestaurantType.FRANCES, menu);

        Assert.IsTrue(new GameSessionService("capital", "p").CommitInitialDecisions());

        var row = _db.Table<GameSessionEntity>().Single();
        Assert.AreEqual(72000f, row.initialCapital, "capital do frances (SetRestaurant da Thaysla)");
        Assert.AreEqual(72000f, row.currentCash);
    }

    // ─────────────────────────────────────────────────────────────
    //  E-04
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void E04_registro_de_evento_grava_os_tres_impactos()
    {
        var session = NewSavedSession("eventos");
        var eventData = ScriptableObject.CreateInstance<EventData>();
        eventData.id = "heavy_rain";
        eventData.title = "Chuva forte";
        var option = new EventOption
        {
            id = "delivery",
            title = "Reforcar delivery",
            effects = new EventEffectData { cashDelta = -800f, reputationDelta = 3 }
        };

        try
        {
            var repository = new SessionEventHistoryRepository(_db);
            repository.RecordFromOption(session.sessionId, 2, 5, eventData, option, clientsChange: -12);

            var row = repository.GetBySessionAndRound(session.sessionId, 2).Single();
            Assert.AreEqual("heavy_rain", row.eventId);
            Assert.AreEqual("delivery", row.selectedChoiceId);
            Assert.AreEqual(-800f, row.cashChange);
            Assert.AreEqual(3, row.reputationChange);
            Assert.AreEqual(-12, row.clientsChange);
        }
        finally
        {
            Object.DestroyImmediate(eventData);
        }
    }

    [Test]
    public void E04_migracao_cria_a_tabela_de_localizacoes_num_banco_antigo()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "e06_migracao_" + System.Guid.NewGuid().ToString("N") + ".db");
        try
        {
            using (var old = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create))
                old.Execute("create table \"GameSessionEntity\"(\"sessionId\" varchar primary key not null, \"userId\" varchar)");

            using (var db = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create))
            {
                db.CreateTable<LocationEntity>();
                var columns = db.GetTableInfo("LocationEntity").Select(c => c.Name).ToArray();
                CollectionAssert.IsSubsetOf(new[] { "id", "zone", "rent", "description", "seededAt" }, columns);
            }
        }
        finally
        {
            if (System.IO.File.Exists(path))
                System.IO.File.Delete(path);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  E-05
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void E05_dialogo_antes_da_D3_nao_oferece_salvar()
    {
        GameSessionState.Set(new GameSessionService("e05a", "p").CreateNewSession());

        Assert.AreEqual(ExitDialogController.ExitMode.BeforeFirstSave, ExitDialogController.EvaluateMode());
    }

    [Test]
    public void E05_alteracao_sem_save_aparece_como_pendente_e_some_ao_salvar()
    {
        NewSavedSession("e05b");
        Assert.IsFalse(GameSessionState.HasUnsavedChanges, "logo depois de gravar nao ha nada pendente");
        Assert.AreEqual(ExitDialogController.ExitMode.AllSaved, ExitDialogController.EvaluateMode());

        GameSessionState.SetCash(500f, save: false);
        Assert.IsTrue(GameSessionState.HasUnsavedChanges);
        Assert.AreEqual(ExitDialogController.ExitMode.UnsavedChanges, ExitDialogController.EvaluateMode());

        GameSessionState.Save();
        Assert.IsFalse(GameSessionState.HasUnsavedChanges);
    }

    [Test]
    public void E05_Shutdown_sem_salvar_descarta_e_fecha_o_banco_uma_vez_so()
    {
        NewSavedSession("e05c");
        GameSessionState.SetCash(999f, save: false);

        var managerObject = new GameObject("TestGameManager");
        try
        {
            var manager = managerObject.AddComponent<GameManager>();

            manager.Shutdown(salvar: false);
            Assert.IsTrue(manager.IsShuttingDown);
            Assert.IsNull(DatabaseInitializer.DatabaseService.Connection, "o Shutdown deveria fechar o banco");

            // Segunda chamada (o OnApplicationQuit depois do botao Sair) nao pode
            // tentar salvar num banco fechado nem lancar excecao.
            Assert.DoesNotThrow(() => manager.Shutdown(salvar: true));
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            _db = null; // ja foi fechado pelo Shutdown
        }
    }

    [Test]
    public void E05_Shutdown_salvando_grava_o_que_estava_pendente()
    {
        var session = NewSavedSession("e05d");
        GameSessionState.SetCash(4321f, save: false);

        // Le pela mesma conexao antes do Shutdown fechar: grava e confere.
        var managerObject = new GameObject("TestGameManager");
        try
        {
            var manager = managerObject.AddComponent<GameManager>();
            var connection = DatabaseInitializer.DatabaseService.Connection;
            float before = connection.Table<GameSessionEntity>().Single().currentCash;
            Assert.AreNotEqual(4321f, before);

            GameSessionState.Save();   // o mesmo Save que o Shutdown(true) chama
            Assert.AreEqual(4321f, connection.Table<GameSessionEntity>().Single(x => x.sessionId == session.sessionId).currentCash);

            manager.Shutdown(salvar: true);
            Assert.IsNull(DatabaseInitializer.DatabaseService.Connection);
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
            _db = null;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  E-06
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void E06_primeira_carga_copia_os_assets_para_o_banco()
    {
        var repository = new SqliteLocationRepository(_db);
        int assets = Resources.LoadAll<LocationData>("Locations").Length;

        Assert.Greater(assets, 0);
        Assert.AreEqual(assets, _db.Table<LocationEntity>().Count());
        Assert.AreEqual(assets, repository.GetLocationScreenData("x").Establishments.Count);
    }

    [Test]
    public void E06_abrir_de_novo_nao_duplica_linhas()
    {
        new SqliteLocationRepository(_db);
        int first = _db.Table<LocationEntity>().Count();

        new SqliteLocationRepository(_db);
        Assert.AreEqual(first, _db.Table<LocationEntity>().Count());
    }

    [Test]
    public void E06_banco_e_assets_mostram_os_mesmos_dados_na_D1()
    {
        StaticLocationRepository.ClearCache();
        var fromAssets = new StaticLocationRepository().GetLocationScreenData("x").Establishments.OrderBy(e => e.Id).ToList();
        var fromDatabase = new SqliteLocationRepository(_db).GetLocationScreenData("x").Establishments.OrderBy(e => e.Id).ToList();

        Assert.AreEqual(fromAssets.Count, fromDatabase.Count);
        for (int i = 0; i < fromAssets.Count; i++)
        {
            var a = fromAssets[i];
            var d = fromDatabase[i];
            Assert.AreEqual(a.Id, d.Id);
            Assert.AreEqual(a.Name, d.Name, a.Id);
            Assert.AreEqual(a.Description, d.Description, a.Id);
            Assert.AreEqual(a.Segment, d.Segment, a.Id);
            Assert.AreEqual(a.RentCost, d.RentCost, a.Id);
            Assert.AreEqual(a.InitialPhysicalCapacity, d.InitialPhysicalCapacity, a.Id);
            Assert.AreEqual(a.BaseDailyDemand, d.BaseDailyDemand, a.Id);
            Assert.AreEqual(a.ReferencePriceFactor, d.ReferencePriceFactor, 0.0001f, a.Id);
            Assert.AreEqual(a.CompetitionLevel, d.CompetitionLevel, a.Id);
        }
    }

    [Test]
    public void E06_o_jogo_le_do_banco_e_nao_dos_assets()
    {
        var repository = new SqliteLocationRepository(_db);
        var row = _db.Table<LocationEntity>().First(x => x.id == "bank");
        row.rent = 12345;
        _db.Update(row);

        var bank = repository.GetLocationScreenData("x").Establishments.Single(e => e.Id == "bank");
        Assert.AreEqual(12345, bank.RentCost, "o valor alterado no banco deveria aparecer na D1");
    }

    [Test]
    public void E06_assets_nao_tem_mais_lorem_ipsum_nem_descricao_vazia()
    {
        foreach (var location in Resources.LoadAll<LocationData>("Locations"))
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(location.description), location.id + ": descricao vazia");
            StringAssert.DoesNotContain("Lorem", location.description, location.id);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Bateria da Thaysla (ciclo trimestral, credito, saves incompativeis)
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void Thaysla_bateria_do_ciclo_trimestral_passa_depois_do_merge()
    {
        // RunBatch monta o proprio banco em memoria: desliga o deste teste antes.
        _db.Close();
        _db = null;
        typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, null);

        Assert.DoesNotThrow(RoundFlowDebugTest.RunBatch);
    }

    // ─────────────────────────────────────────────────────────────

    /// <summary>Sessao no estado "depois do confirm da D3": com cardapio e gravada.</summary>
    private static GameSessionEntity NewSavedSession(string userId)
    {
        GameSessionState.Clear();
        var session = new GameSessionService(userId, "p").CreateNewSession();
        session.menuPricingJson = "{\"items\":[{\"productId\":\"X\",\"selectedPrice\":10.0}]}";
        Assert.IsTrue(GameSessionState.Persist());
        return session;
    }
}
