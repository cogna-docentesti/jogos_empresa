#if UNITY_EDITOR
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    public static class InitialTutorialSceneBuilder
    {
        [MenuItem("Tools/Jogos de Empresa/Preparar UIs dos tutoriais na cena")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            var registry = Object.FindObjectsByType<PanelRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(r => r.gameObject.scene == scene);
            var team = registry.Resolve(PanelId.Team);
            // The team controller and view are authored by the screen builder.
            var listener = Object.FindObjectsByType<UIStateListener>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(r => r.gameObject.scene == scene);
            Set(listener, "initialTeamPanel", team);
            BuildTutorial(registry.Resolve(PanelId.Financial), PanelId.Financial, TutorialDescriptions.Financial);
            BuildTutorial(registry.Resolve(PanelId.EquipmentStore), PanelId.EquipmentStore, TutorialDescriptions.Equipment);
            BuildTutorial(team, PanelId.Team, TutorialDescriptions.Team);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Tutorial] UIs e referências salvas na GameScene.");
        }

        private static void BuildTutorial(GameObject screen, PanelId id, string description)
        {
            if (screen.GetComponentInChildren<InitialTutorialView>(true) != null) return;
            TutorialUiFactory.Build(screen.transform, id, true, description);
        }

        private static void Set(Object component, string field, Object value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
