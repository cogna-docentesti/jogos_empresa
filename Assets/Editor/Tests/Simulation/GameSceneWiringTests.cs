using System.Linq;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class GameSceneWiringTests
{
    private Scene scene;
    private bool opened;
    [SetUp] public void Open()
    {
        scene = SceneManager.GetSceneByPath("Assets/Scenes/GameScene.unity");
        opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity", OpenSceneMode.Additive);
    }
    [TearDown] public void Close() { if (opened) EditorSceneManager.CloseScene(scene, true); }
    private T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).FirstOrDefault();

    [Test] public void ExistingCalendarAndSettlementRemainWired()
    {
        var settlement = Find<MonthlySettlementController>();
        Assert.IsNotNull(settlement); Assert.IsTrue(settlement.enabled);
        var calendar = new SerializedObject(settlement).FindProperty("calendar").objectReferenceValue as MonthlyCalendarController;
        Assert.IsNotNull(calendar); Assert.IsNotNull(calendar.GetComponent<MonthlyCalendarView>());
        Assert.AreEqual("Panel_MonthlyCalendar", calendar.name);
        Assert.IsNotNull(Resources.Load<SimulationConfigAsset>("Simulation/SimulationConfig"));
    }

    [Test] public void HubOpensOriginalCalendarAndHistoryUsesOriginalEventScreen()
    {
        var canvas = scene.GetRootGameObjects().First(r => r.name == "Canvas").transform;
        var button = canvas.Find("Panel_Menu/EventActions/CloseMonth").GetComponent<Button>();
        Assert.IsTrue(Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i =>
            button.onClick.GetPersistentTarget(i) is MonthlySettlementController
            && button.onClick.GetPersistentMethodName(i) == nameof(MonthlySettlementController.ShowCalendar)));
        var history = Find<PanelRegistry>().Bindings.First(b => b.id == PanelId.EventHistory);
        Assert.IsNotNull(history.root.GetComponent<EventScreenController>());
        Assert.IsNull(canvas.Find("Panel_EventCalendar")); Assert.IsNull(canvas.Find("Panel_MonthSimulation"));
    }
}
