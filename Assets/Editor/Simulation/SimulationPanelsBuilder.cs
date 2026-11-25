using Game.Adapter.In.Controllers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>The original calendar owns review, policies and monthly progression.</summary>
public static class SimulationPanelsBuilder
{
    [MenuItem("Game/Simulação/Montar painéis na GameScene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) return;
        var settlement = Object.FindFirstObjectByType<MonthlySettlementController>(FindObjectsInactive.Include);
        if (settlement == null) return;
        var canvas = settlement.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        Game.EditorTools.MonthlyCalendarUiMigration.Wire(canvas.transform, settlement);
        EditorSceneManager.MarkSceneDirty(settlement.gameObject.scene);
    }
}
