using System.IO;
using System.Linq;
using Game.Infrastructure.Session;
using NUnit.Framework;
using SQLite4Unity3d;
using UnityEngine;

/// <summary>
/// Testes de Edit Mode das tarefas E-02, E-03 e E-04.
/// Rodam sem abrir cena e sem tocar no game.db de verdade:
/// Window > General > Test Runner > aba EditMode > Run All.
/// </summary>
public class InitialDecisionsTests
{
    [SetUp]
    public void SetUp()
    {
        PlayerSession.ClearDecisions();
        GameSessionState.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        PlayerSession.ClearDecisions();
        GameSessionState.Clear();
    }

    // ─────────────────────────────────────────────────────────────
    //  E-02: rascunho em memoria (PlayerSession)
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void Rascunho_comeca_vazio_e_incompleto()
    {
        Assert.IsFalse(PlayerSession.HasLocation);
        Assert.IsFalse(PlayerSession.HasRestaurant);
        Assert.IsFalse(PlayerSession.HasMenu);
        Assert.IsFalse(PlayerSession.IsComplete);
    }

    [Test]
    public void Rascunho_fica_completo_com_as_tres_decisoes()
    {
        FillDraft(RestaurantType.JAPONES);

        Assert.IsTrue(PlayerSession.HasLocation);
        Assert.IsTrue(PlayerSession.HasRestaurant);
        Assert.IsTrue(PlayerSession.HasMenu);
        Assert.IsTrue(PlayerSession.IsComplete);
    }

    [Test]
    public void Financas_escolhida_e_diferente_de_nada_escolhido()
    {
        // LocationZone.Financas vale 0. Sem o "?" no SelectedZone isso seria
        // indistinguivel de "ainda nao escolheu".
        Assert.IsFalse(PlayerSession.SelectedZone.HasValue);

        PlayerSession.SaveLocation("bank", "Área Financeira", LocationZone.Financas);

        Assert.IsTrue(PlayerSession.SelectedZone.HasValue);
        Assert.AreEqual(LocationZone.Financas, PlayerSession.SelectedZone.Value);
    }

    [Test]
    public void Trocar_de_restaurante_descarta_o_cardapio_do_anterior()
    {
        FillDraft(RestaurantType.JAPONES);

        PlayerSession.SaveRestaurant(RestaurantType.FRANCES, Segment.HIGH);

        Assert.IsFalse(PlayerSession.HasMenu, "Cardápio do japonês não pode valer para o francês.");
        Assert.IsFalse(PlayerSession.IsComplete);
    }

    [Test]
    public void Manter_o_restaurante_preserva_o_cardapio()
    {
        FillDraft(RestaurantType.JAPONES);

        // Jogador volta da D3 para a D2, troca so a classe social e avanca.
        PlayerSession.SaveRestaurant(RestaurantType.JAPONES, Segment.HIGH);

        Assert.IsTrue(PlayerSession.HasMenu);
        Assert.AreEqual(55f, PlayerSession.GetMenu().items[0].selectedPrice);
    }

    [Test]
    public void GetMenu_devolve_copia_que_nao_altera_o_rascunho()
    {
        FillDraft(RestaurantType.JAPONES);

        var copy = PlayerSession.GetMenu();
        copy.items[0].selectedPrice = 999f;
        copy.items.Clear();

        Assert.AreEqual(1, PlayerSession.GetMenu().items.Count);
        Assert.AreEqual(55f, PlayerSession.GetMenu().items[0].selectedPrice);
    }

    [Test]
    public void LoadDecisionsFrom_preenche_o_rascunho_com_a_sessao_salva()
    {
        var session = NewSession();
        session.locationZone = LocationZone.Residencial;
        session.restaurantType = RestaurantType.PODRAO;
        session.targetSegment = Segment.LOW;
        session.menuPricingJson = "{\"items\":[{\"productId\":\"X\",\"selectedPrice\":12.0}]}";

        PlayerSession.LoadDecisionsFrom(session);

        Assert.AreEqual("condominium", PlayerSession.SelectedEstablishmentId);
        Assert.AreEqual(LocationZone.Residencial, PlayerSession.SelectedZone);
        Assert.AreEqual(RestaurantType.PODRAO, PlayerSession.SelectedRestaurantType);
        Assert.AreEqual(Segment.LOW, PlayerSession.SelectedTargetSegment);
        Assert.IsTrue(PlayerSession.IsComplete);
    }

    [Test]
    public void Mapa_de_zonas_ida_e_volta()
    {
        foreach (LocationZone zone in System.Enum.GetValues(typeof(LocationZone)))
            Assert.AreEqual(zone, LocationZoneMap.ToZone(LocationZoneMap.ToId(zone)));
    }

    [Test]
    public void Pedido_de_edicao_da_identificacao_vale_uma_vez()
    {
        PlayerSession.RequestIdentificationEdit();

        Assert.IsTrue(PlayerSession.ConsumeIdentificationEditRequest());
        Assert.IsFalse(PlayerSession.ConsumeIdentificationEditRequest());
    }

    // ─────────────────────────────────────────────────────────────
    //  E-02: transicoes da state machine
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void StateMachine_permite_avancar_e_voltar_entre_D1_D2_D3()
    {
        var sm = new GameStateMachine();
        sm.ForceState(InitialDecisionFlow.Location);

        Assert.IsTrue(sm.TryChangeState(InitialDecisionFlow.Restaurant), "D1 -> D2");
        Assert.IsTrue(sm.TryChangeState(InitialDecisionFlow.Menu), "D2 -> D3");
        Assert.IsTrue(sm.TryChangeState(InitialDecisionFlow.Restaurant), "D3 -> D2 (voltar)");
        Assert.IsTrue(sm.TryChangeState(InitialDecisionFlow.Location), "D2 -> D1 (voltar)");
    }

    [Test]
    public void StateMachine_nao_deixa_pular_da_D1_para_a_D3()
    {
        var sm = new GameStateMachine();
        sm.ForceState(InitialDecisionFlow.Location);

        // Gera um Warning no Console ("Transição inválida"). Warning nao reprova teste.
        Assert.IsFalse(sm.TryChangeState(InitialDecisionFlow.Menu));
    }

    [Test]
    public void StateMachine_D3_segue_para_o_primeiro_tutorial_pela_revisao()
    {
        var sm = new GameStateMachine();
        sm.ForceState(InitialDecisionFlow.Menu);

        Assert.IsTrue(sm.TryChangeState(InitialDecisionFlow.Review));
        Assert.IsTrue(sm.TryChangeState(InitialDecisionFlow.AfterCommit));
    }

    // ─────────────────────────────────────────────────────────────
    //  E-03: nada vai para o banco antes da D3
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void Sessao_nova_nasce_sem_linha_no_banco()
    {
        GameSessionState.Set(NewSession());

        Assert.IsTrue(GameSessionState.HasSession);
        Assert.IsFalse(GameSessionState.IsPersisted);

        // Sem banco nesta execucao: se o Save tentasse gravar, o getter do
        // repositorio faria Debug.LogError e o Test Runner reprovaria o teste
        // (todo LogError inesperado reprova). Ele precisa sair calado.
        GameSessionState.Save();
        GameSessionState.SetCash(1f);
        GameSessionState.SetTeamJson("{}");
        Assert.IsFalse(GameSessionState.IsPersisted);
    }

    [Test]
    public void Commit_recusa_rascunho_incompleto()
    {
        GameSessionState.Set(NewSession());
        PlayerSession.SaveLocation("bank", "Área Financeira", LocationZone.Financas);

        var service = new GameSessionService("u", "p");

        Assert.IsFalse(service.CommitInitialDecisions());
        Assert.IsFalse(GameSessionState.IsPersisted);
    }

    [Test]
    public void Commit_sem_sessao_avisa_e_nao_grava()
    {
        FillDraft(RestaurantType.JAPONES);
        var service = new GameSessionService("u", "p");

        bool result = true;
        WithoutConsole(() => result = service.CommitInitialDecisions());   // loga um erro esperado

        Assert.IsFalse(result);
    }

    [Test]
    public void Persist_sem_banco_devolve_false_sem_quebrar()
    {
        GameSessionState.Set(NewSession());

        bool result = true;
        WithoutConsole(() => result = GameSessionState.Persist());   // loga um erro esperado

        Assert.IsFalse(result);
        Assert.IsFalse(GameSessionState.IsPersisted);
    }

    // ─────────────────────────────────────────────────────────────
    //  E-04: migracao do schema sobre um banco no formato antigo
    // ─────────────────────────────────────────────────────────────

    [Test]
    public void Migracao_adiciona_colunas_novas_sem_perder_linhas()
    {
        string path = Path.Combine(Path.GetTempPath(), "e04_migracao_" + System.Guid.NewGuid().ToString("N") + ".db");

        try
        {
            // 1. Banco no formato ANTIGO (copiado do game.db de 30/09/2026).
            using (var old = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create))
            {
                old.Execute(OldGameSessionSchema);
                old.Execute(OldRoundResultSchema);
                old.Execute(OldEventHistorySchema);
                old.Execute("insert into GameSessionEntity (sessionId,userId,professorId,status,currentRound,cityId,coherenceRating,teamJson,equipmentJson,locationZone,restaurantType) "
                          + "values ('antiga','local_user_01','prof_01',0,1,'','','{}','{}',2,2)");
                old.Execute("insert into RoundResultEntity (roundResultId,sessionId,round,netResult) values ('r1','antiga',1,123.5)");
                old.Execute("insert into SessionEventHistoryEntity (historyId,sessionId,eventId,round) values ('h1','antiga','heavy_rain',1)");
            }

            // 2. Abre com o codigo NOVO, exatamente como o DatabaseInitializer faz.
            using (var db = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create))
            {
                db.CreateTable<GameSessionEntity>();
                db.CreateTable<RoundResultEntity>();
                db.CreateTable<SessionEventHistoryEntity>();

                AssertHasColumns(db, "GameSessionEntity", "studentName", "studentRA", "companyName");
                AssertHasColumns(db, "RoundResultEntity", "customers", "reputationAtEnd");
                AssertHasColumns(db, "SessionEventHistoryEntity", "cashChange", "reputationChange", "clientsChange");

                // Os atalhos [Ignore] nao podem ter virado coluna.
                Assert.IsFalse(ColumnNames(db, "RoundResultEntity").Contains("FixedCosts"));

                // 3. Linhas antigas continuam la, com os campos novos vazios.
                var session = db.Table<GameSessionEntity>().Single();
                Assert.AreEqual("antiga", session.sessionId);
                Assert.AreEqual(LocationZone.Comercio, session.locationZone);
                Assert.IsNull(session.studentName);
                Assert.IsNull(session.companyName);

                var round = db.Table<RoundResultEntity>().Single();
                Assert.AreEqual(123.5f, round.netResult);
                Assert.AreEqual(0, round.customers);

                Assert.AreEqual(1, db.Table<SessionEventHistoryEntity>().Count());

                // 4. InsertOrReplace duas vezes com o mesmo sessionId = uma linha so.
                session.studentName = "Aluna Teste";
                session.companyName = "Sushi da Esquina";
                db.InsertOrReplace(session);
                db.InsertOrReplace(session);

                Assert.AreEqual(1, db.Table<GameSessionEntity>().Count());
                Assert.AreEqual("Sushi da Esquina", db.Table<GameSessionEntity>().Single().companyName);
            }
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  Auxiliares
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Roda uma acao com o Console desligado. Usado so nos testes em que o
    /// Debug.LogError faz parte do comportamento esperado (o Test Runner
    /// reprovaria o teste ao ver o erro).
    /// </summary>
    private static void WithoutConsole(System.Action action)
    {
        bool previous = Debug.unityLogger.logEnabled;
        Debug.unityLogger.logEnabled = false;
        try { action(); }
        finally { Debug.unityLogger.logEnabled = previous; }
    }

    private static void FillDraft(RestaurantType type)
    {
        PlayerSession.SaveLocation("university", "Área Educacional", LocationZone.Educacao);
        PlayerSession.SaveRestaurant(type, Segment.MEDIUM);

        var menu = new MenuPricingData();
        menu.items.Add(new MenuPricingItem { productId = "PRATO_TESTE", selectedPrice = 55f });
        PlayerSession.SetMenu(type, menu);
    }

    private static GameSessionEntity NewSession() => new GameSessionEntity
    {
        sessionId = System.Guid.NewGuid().ToString(),
        userId = "u",
        professorId = "p",
        currentRound = 1,
        cityId = string.Empty,
        coherenceRating = string.Empty,
        teamJson = "{}",
        equipmentJson = "{}"
    };

    private static string[] ColumnNames(SQLiteConnection db, string table) =>
        db.GetTableInfo(table).Select(c => c.Name).ToArray();

    private static void AssertHasColumns(SQLiteConnection db, string table, params string[] expected)
    {
        var columns = ColumnNames(db, table);
        foreach (var name in expected)
            Assert.Contains(name, columns, $"Coluna {name} não foi criada em {table}.");
    }

    // Schema exato do game.db antes da E-04.
    private const string OldGameSessionSchema = @"CREATE TABLE ""GameSessionEntity""(
""sessionId"" varchar primary key not null , ""userId"" varchar not null , ""professorId"" varchar not null ,
""status"" integer not null , ""currentRound"" integer , ""cityId"" varchar not null , ""restaurantType"" integer ,
""locationZone"" integer , ""targetSegment"" integer , ""coherenceRating"" varchar not null , ""initialCapital"" float ,
""currentCash"" float , ""loanBalance"" float , ""creditLineId"" varchar , ""reputationScore"" integer ,
""teamJson"" varchar not null , ""equipmentJson"" varchar not null , ""consecutiveNegativeRounds"" integer ,
""startedAt"" bigint , ""completedAt"" bigint , ""syncedAt"" bigint , ""priceStrategy"" integer, ""selectedPrice"" float,
""menuPricingJson"" varchar, ""alignmentScore"" integer, ""alignmentClassification"" varchar, ""alignmentFactor"" float)";

    private const string OldRoundResultSchema = @"CREATE TABLE ""RoundResultEntity""(
""roundResultId"" varchar primary key not null , ""sessionId"" varchar , ""round"" integer , ""grossRevenue"" float ,
""supplyCost"" float , ""rent"" float , ""salaries"" float , ""utilities"" float , ""loanPayment"" float ,
""thirteenthSalary"" float , ""eventCashImpact"" float , ""netResult"" float , ""openingCash"" float ,
""closingCash"" float , ""eventId"" varchar , ""eventChoice"" integer , ""reputationDelta"" integer ,
""coherenceFactor"" float , ""createdAt"" bigint )";

    private const string OldEventHistorySchema = @"CREATE TABLE ""SessionEventHistoryEntity""(
""historyId"" varchar primary key not null , ""sessionId"" varchar , ""eventId"" varchar , ""round"" integer ,
""day"" integer , ""eventName"" varchar , ""polarity"" varchar , ""selectedChoiceId"" varchar ,
""selectedChoiceTitle"" varchar , ""selectedChoiceText"" varchar , ""occurredAt"" bigint )";
}
