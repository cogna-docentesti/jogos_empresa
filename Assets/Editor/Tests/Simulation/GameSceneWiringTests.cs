// Assets/Editor/Tests/Simulation/GameSceneWiringTests.cs
// Garante que a GameScene usa o motor novo. Um merge que traga a cena de outra branch sem os painéis do
// simulador quebra o jogo em silêncio (o hub volta a chamar o fechamento do motor antigo); este teste acusa.
// Para consertar: Game > Simulação > Montar painéis na GameScene.
using System.Linq;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI.Navigation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSceneWiringTests
{
    private const string ScenePath = "Assets/Scenes/GameScene.unity";
    private const string Fix = " Rode Game > Simulação > Montar painéis na GameScene e salve a cena.";
    private Scene _scene;
    private bool _opened;

    [SetUp]
    public void Open()
    {
        _scene = SceneManager.GetSceneByPath(ScenePath);
        _opened = !_scene.isLoaded;
        if (_opened) _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }

    [TearDown]
    public void Close()
    {
        if (_opened) EditorSceneManager.CloseScene(_scene, true);
    }

    private T Find<T>() where T : Component
        => _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

    [Test]
    public void SimulationFlow_ExisteELigadoAosPaineis()
    {
        var flow = Find<SystemicRoundFlow>();
        Assert.IsNotNull(flow, "A GameScene não tem o SimulationFlow (SystemicRoundFlow)." + Fix);
        var so = new UnityEditor.SerializedObject(flow);
        Assert.IsNotNull(so.FindProperty("monthController").objectReferenceValue, "monthController vazio." + Fix);
        Assert.IsNotNull(so.FindProperty("planningView").objectReferenceValue, "planningView vazio." + Fix);
        var month = so.FindProperty("monthPanel").objectReferenceValue as GameObject;
        Assert.IsNotNull(month, "monthPanel vazio." + Fix);
        Assert.IsNotNull(month.GetComponent<MonthSimulationView>(), "monthPanel não é o Panel_MonthSimulation." + Fix);
    }

    [Test]
    public void Planejamento_RegistradoNoPanelRegistry()
    {
        var registry = Find<PanelRegistry>();
        Assert.IsNotNull(registry);
        var binding = registry.Bindings.FirstOrDefault(b => b.id == PanelId.Planning);
        Assert.IsNotNull(binding?.root, "PanelId.Planning não está no PanelRegistry." + Fix);
        Assert.IsNotNull(binding.root.GetComponent<ManagementPoliciesView>(), "Planning aponta para o painel errado." + Fix);
    }

    [Test]
    public void BotaoDoHub_AbreOPlanejamento_ENaoChamaOMotorAntigo()
    {
        var canvas = _scene.GetRootGameObjects().First(r => r.name == "Canvas").transform;
        var button = canvas.Find("Panel_Menu/EventActions/CloseMonth")?.GetComponent<Button>();
        Assert.IsNotNull(button, "Botão Panel_Menu/EventActions/CloseMonth não encontrado.");
        int count = button.onClick.GetPersistentEventCount();
        var calls = Enumerable.Range(0, count)
            .Select(i => (target: button.onClick.GetPersistentTarget(i), method: button.onClick.GetPersistentMethodName(i))).ToList();
        Assert.IsTrue(calls.Any(c => c.target is SystemicRoundFlow && c.method == nameof(SystemicRoundFlow.OpenPlanning)),
            "O botão do hub não chama SystemicRoundFlow.OpenPlanning." + Fix);
        Assert.IsFalse(calls.Any(c => c.target is MonthlySettlementController),
            "O botão do hub ainda chama o fechamento do motor antigo (MonthlySettlementController)." + Fix);
    }

    [Test]
    public void EventosDoHub_AbremOCalendario()
    {
        var registry = Find<PanelRegistry>();
        var binding = registry.Bindings.FirstOrDefault(b => b.id == PanelId.EventHistory);
        Assert.IsNotNull(binding?.root, "PanelId.EventHistory não está no PanelRegistry." + Fix);
        Assert.IsNotNull(binding.root.GetComponent<EventCalendarScreen>(), "Eventos do hub não abre o Panel_EventCalendar." + Fix);
        var month = new UnityEditor.SerializedObject(Find<MonthSimulationView>());
        Assert.IsNotNull(month.FindProperty("calendar").objectReferenceValue, "Tela do mês sem o calendário de eventos." + Fix);
    }

    [Test]
    public void AbandonarMes_PedeConfirmacao()
    {
        var view = new UnityEditor.SerializedObject(Find<MonthSimulationView>());
        Assert.IsNotNull(view.FindProperty("abandonConfirmPanel").objectReferenceValue, "ABANDONAR MÊS sem diálogo de confirmação." + Fix);
        var month = (GameObject)new UnityEditor.SerializedObject(Find<SystemicRoundFlow>()).FindProperty("monthPanel").objectReferenceValue;
        var button = month.transform.Find("AbandonButton").GetComponent<Button>();
        var calls = Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Select(i => button.onClick.GetPersistentMethodName(i)).ToList();
        CollectionAssert.Contains(calls, nameof(SystemicRoundFlow.RequestAbandonMonth), "ABANDONAR MÊS abandona sem perguntar." + Fix);
        CollectionAssert.DoesNotContain(calls, nameof(SystemicRoundFlow.AbandonMonth));
    }

    [Test]
    public void FechamentoDoMotorAntigo_Desligado()
    {
        foreach (var settlement in _scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonthlySettlementController>(true)))
            Assert.IsFalse(settlement.enabled, "MonthlySettlementController ligado: o motor antigo pode fechar o mês." + Fix);
    }
}
