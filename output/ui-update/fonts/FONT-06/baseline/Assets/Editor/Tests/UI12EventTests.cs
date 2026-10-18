using System;
using System.Collections;
using System.Linq;
using Game.Adapter.In.UI;
using NUnit.Framework;
using SQLite4Unity3d;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class UI12EventTests
{
    private SQLiteConnection db;
    private GameObject databaseObject;
    private DatabaseService previous;
    private GameSessionEntity session;
    private MonthlyEventDecisionService service;
    private static EventData[] Catalog => Resources.LoadAll<EventData>("Events");
    public static IEnumerable Events => Catalog.Select(e => new TestCaseData(e.id).SetName("UI12.ArtAndOptions." + e.id));
    public static IEnumerable Choices => Catalog.SelectMany(e => e.options.Select(o => new TestCaseData(e.id, o.id).SetName("UI12.ConfirmOnce." + e.id + "." + o.id)));
    [SetUp] public void Setup()
    {
        GameSessionState.Clear(); previous = DatabaseInitializer.DatabaseService;
        db = new SQLiteConnection(":memory:"); db.CreateTable<GameSessionEntity>(); db.CreateTable<RoundResultEntity>(); db.CreateTable<SessionEventHistoryEntity>();
        databaseObject = new GameObject("UI12IsolatedDatabase"); var database = databaseObject.AddComponent<DatabaseService>();
        typeof(DatabaseService).GetProperty("Connection").SetValue(database, db); typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, database);
        session = new GameSessionService("ui12-test", "test-professor").CreateNewSession();
        var restaurant = Resources.LoadAll<RestaurantData>("Restaurants").Single(r => r.type == RestaurantType.PODRAO);
        session.restaurantType = restaurant.type; session.locationZone = LocationZone.Comercio; session.targetSegment = Segment.LOW;
        session.currentCash = session.initialCapital = 65000; session.reputationScore = 50;
        session.menuPricingJson = MenuPricingHelper.ToJson(MenuPricingHelper.FromProducts(restaurant.products));
        Assert.IsTrue(GameSessionState.Persist()); service = new MonthlyEventDecisionService(db, new MonthlyEventSelector(() => .15));
    }
    [TearDown] public void Cleanup()
    { GameSessionState.Clear(); db.Close(); typeof(DatabaseInitializer).GetProperty("DatabaseService").SetValue(null, previous); Object.DestroyImmediate(databaseObject); }
    [TestCaseSource(nameof(Events))] public void ArtAndOptions(string id)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try
        {
        var view = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EventScreenView>(true)).Single();
        var data = Catalog.Single(e => e.id == id); var sprite = view.Illustration(id);
        Assert.NotNull(sprite); Assert.AreEqual("Assets/Art/new/events/illustrations/" + id + ".png", AssetDatabase.GetAssetPath(sprite));
        Assert.AreEqual(3, data.options.Length); Assert.AreEqual(3, data.options.Select(o => o.id).Distinct().Count());
        Assert.AreEqual(new Rect(0, 0, 768, 512), sprite.rect);
        foreach (var option in data.options) { Assert.IsNotEmpty(option.title); Assert.IsNotEmpty(option.description); Assert.That(EventEffectText.Format(option.effects), Does.Contain("Duração operacional:")); }
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
    }
    [TestCaseSource(nameof(Choices))] public void ConfirmOnce(string id, string choice)
    {
        var data = Catalog.Single(e => e.id == id); var option = data.options.Single(o => o.id == choice);
        var first = service.Confirm(session.sessionId, 1, 16, data, option);
        var second = new MonthlyEventDecisionService(db).Confirm(session.sessionId, 1, 16, data, option);
        Assert.AreEqual(first.historyId, second.historyId); Assert.AreEqual(1, new SessionEventHistoryRepository(db).GetBySession(session.sessionId).Count);
        Assert.AreEqual(option.id, first.selectedChoiceId); Assert.AreEqual(option.effects.cashDelta, first.cashChange); Assert.AreEqual(option.effects.reputationDelta, first.reputationChange);
        Assert.AreEqual(JsonUtility.ToJson(option.effects), first.effectsJson); Assert.AreEqual(65000, GameSessionState.Current.currentCash); Assert.AreEqual(50, GameSessionState.Current.reputationScore);
        var forecast = MonthlySimulationEngine.Calculate(MonthlySimulationCatalog.Load(session, new[] { first }));
        var settled = new RoundService(db, MonthlySimulationCatalog.Load).ProcessRound(1);
        Assert.AreEqual(forecast.closingCash, settled.closingCash); Assert.AreEqual(forecast.reputationAtEnd, settled.reputationAtEnd);
        float cash = GameSessionState.Current.currentCash; int reputation = GameSessionState.Current.reputationScore;
        Assert.AreEqual(settled.roundResultId, new RoundService(db, MonthlySimulationCatalog.Load).ProcessRound(1).roundResultId);
        Assert.AreEqual(first.historyId, service.Confirm(session.sessionId, 1, 16, data, option).historyId);
        Assert.AreEqual(cash, GameSessionState.Current.currentCash); Assert.AreEqual(reputation, GameSessionState.Current.reputationScore);
    }
    [Test] public void MonthlyDrawIsPersistedAndReopeningCannotReroll()
    {
        var context = MonthlySimulationCatalog.Load(session);
        var plan = service.OpenMonth(session, Catalog, context.Restaurant, context.Equipment, 30);
        Assert.AreEqual(4, plan.Count); Assert.AreEqual(4, plan.Select(p => p.eventId).Distinct().Count()); Assert.That(plan.Select(p => p.day), Is.EqualTo(new[] { 6, 12, 18, 24 }));
        var selected = plan.Select(p => Catalog.Single(e => e.id == p.eventId)).ToArray();
        Assert.IsTrue(selected.All(e => e.applicableRestaurantTypes.Length == 0 || e.applicableRestaurantTypes.Contains(session.restaurantType)));
        foreach (var e in selected.Where(e => e.triggerType == EventTriggerType.CONDITIONAL)) Assert.IsTrue(EventConditionEvaluator.EvaluateAll(e.conditions, session, context.Restaurant, context.Equipment, out _));
        service.Confirm(session.sessionId, 1, plan[0].day, selected[0], selected[0].options[0]);
        var reopened = new MonthlyEventDecisionService(db, new MonthlyEventSelector(() => throw new Exception("Rerolled"))).OpenMonth(session, Catalog, context.Restaurant, context.Equipment, 30);
        CollectionAssert.AreEqual(plan.Select(p => p.eventId), reopened.Select(p => p.eventId)); Assert.AreEqual(1, db.Table<MonthlyEventPlanEntity>().Count());
    }
    [Test] public void ConflictingAndStaleChoicesAreRejected()
    {
        var data = Catalog[0]; service.Confirm(session.sessionId, 1, 6, data, data.options[0]);
        Assert.Throws<InvalidOperationException>(() => service.Confirm(session.sessionId, 1, 6, data, data.options[1]));
        Assert.Throws<InvalidOperationException>(() => service.Confirm(session.sessionId, 2, 6, data, data.options[0]));
        Assert.Throws<InvalidOperationException>(() => service.Confirm(session.sessionId, 1, 0, data, data.options[0]));
        Assert.AreEqual(1, db.Table<SessionEventHistoryEntity>().Count());
    }
    [Test] public void FailedInsertRollsBackAndRetryRecordsOnce()
    {
        db.Execute("CREATE TRIGGER ui12_fail BEFORE INSERT ON SessionEventHistoryEntity BEGIN SELECT RAISE(ABORT, 'isolated failure'); END");
        var data = Catalog[0]; Assert.Throws<SQLiteException>(() => service.Confirm(session.sessionId, 1, 6, data, data.options[0]));
        Assert.AreEqual(0, db.Table<SessionEventHistoryEntity>().Count()); Assert.AreEqual(65000, GameSessionState.Current.currentCash);
        db.Execute("DROP TRIGGER ui12_fail"); service.Confirm(session.sessionId, 1, 6, data, data.options[0]); Assert.AreEqual(1, db.Table<SessionEventHistoryEntity>().Count());
    }
    [Test] public void RestartDeletesReservationsAndHistoryWithSession()
    {
        var context = MonthlySimulationCatalog.Load(session); service.OpenMonth(session, Catalog, context.Restaurant, context.Equipment, 30);
        service.Confirm(session.sessionId, 1, 6, Catalog[0], Catalog[0].options[0]);
        new GameSessionService(session.userId, session.professorId).RestartSession();
        Assert.AreEqual(0, db.Table<MonthlyEventPlanEntity>().Count()); Assert.AreEqual(0, db.Table<SessionEventHistoryEntity>().Count()); Assert.AreEqual(0, db.Table<GameSessionEntity>().Count()); Assert.IsFalse(GameSessionState.IsPersisted);
    }
    [Test] public void EffectsUseAllRealFieldsAndDurationWithoutInventedPreviews()
    {
        var text = EventEffectText.Format(new EventEffectData { cashDelta = -3500, reputationDelta = 2, demandMultiplier = 1.1f, capacityMultiplier = .9f, stockMultiplier = .8f, ingredientCostMultiplier = 1.05f, averageTicketMultiplier = 1.2f, duration = EventEffectDuration.CURRENT_MONTH });
        foreach (var label in new[] { "Caixa:", "Reputação:", "Demanda:", "Capacidade:", "Disponibilidade de insumos:", "Custo de ingredientes:", "Ticket médio:", "fim do mês" }) Assert.That(text, Does.Contain(label));
        Assert.That(EventEffectText.Format(null), Is.EqualTo("Efeitos indisponíveis."));
    }
    [Test] public void CatalogCopyContainsNoTestResidues()
    {
        var freezer = Catalog.Single(e => e.id == "freezer_breakdown");
        Assert.AreEqual("Quebra do freezer", freezer.title);
        Assert.AreEqual("O freezer apresenta uma falha e parte dos ingredientes armazenados fica em risco.", freezer.description);
        foreach (var data in Catalog)
        {
            var texts = new[] { data.title, data.description, data.educationalConcept }.Concat(data.options.SelectMany(o => new[] { o.title, o.description }));
            foreach (var text in texts.Where(t => t != null))
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(teste|placeholder|lorem|dummy|debug)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase), data.id + ": " + text);
        }
    }
}
