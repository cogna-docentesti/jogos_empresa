#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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
            BuildTeam(team);
            var listener = Object.FindObjectsByType<UIStateListener>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(r => r.gameObject.scene == scene);
            Set(listener, "initialTeamPanel", team);
            BuildEquipment(registry.Resolve(PanelId.EquipmentStore).GetComponent<EquipmentScreenView>());
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

        private static void BuildEquipment(EquipmentScreenView view)
        {
            var serialized = new SerializedObject(view);
            var prepared = serialized.FindProperty("preparedCards");
            if (prepared.arraySize == 0)
            {
                var prefab = (EquipmentCardView)serialized.FindProperty("cardPrefab").objectReferenceValue;
                var container = (RectTransform)serialized.FindProperty("cardContainer").objectReferenceValue;
                var catalog = Resources.LoadAll<EquipmentData>("Equipments");
                prepared.arraySize = catalog.Length;
                for (int i = 0; i < catalog.Length; i++)
                {
                    var card = Object.Instantiate(prefab, container);
                    card.name = "EquipmentSlot_" + (i + 1);
                    card.Setup(catalog[i]);
                    card.gameObject.SetActive(false);
                    prepared.GetArrayElementAtIndex(i).objectReferenceValue = card;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            // Legacy cart authoring now runs only in the Editor; references are serialized.
            typeof(EquipmentScreenView).GetMethod("EnsureCartUi", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
            EditorUtility.SetDirty(view);
        }

        private static void BuildTeam(GameObject root)
        {
            if (root.GetComponent<TeamScreenController>() != null) return;
            foreach (Transform child in root.transform) child.gameObject.SetActive(false);
            var controller = root.AddComponent<TeamScreenController>();
            var screen = TutorialUiFactory.Object("TeamScreen", root.transform, typeof(Image));
            TutorialUiFactory.Stretch(screen.GetComponent<RectTransform>());
            screen.GetComponent<Image>().color = new Color(.04f, .09f, .16f, 1);
            var title = TutorialUiFactory.Text(screen.transform, "RH — Equipe", 32);
            TutorialUiFactory.Position(title.rectTransform, new Vector2(0, .9f), Vector2.one, new Vector2(30, 0), new Vector2(-30, -15));
            var content = TutorialUiFactory.Scroll(screen.transform);
            Set(controller, "minimumText", TutorialUiFactory.Text(content, "Equipe mínima do restaurante"));
            var roles = Resources.LoadAll<RoleData>("Roles");
            var serialized = new SerializedObject(controller);
            var cards = serialized.FindProperty("roleCards");
            cards.arraySize = roles.Length;
            for (int i = 0; i < roles.Length; i++)
            {
                var role = roles[i];
                var card = TutorialUiFactory.Object("Role_" + role.id, content, typeof(VerticalLayoutGroup));
                var layout = card.GetComponent<VerticalLayoutGroup>();
                layout.spacing = 10;
                layout.childControlHeight = layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                var label = RoleName(role);
                TutorialUiFactory.Text(card.transform, label + $"\nPor funcionário: R$ {role.salary:N2}/mês · +{role.maxClientsSupported:N0} de capacidade mensal · +{role.qualityContribution:P0} de contribuição de qualidade");
                var quantity = TutorialUiFactory.Text(card.transform, "Selecionados: 0");
                var hire = TutorialUiFactory.Button(card.transform, "Contratar +1 " + label, () => { });
                var undo = TutorialUiFactory.Button(card.transform, "Desfazer nova contratação", () => { });
                var entry = cards.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("role").objectReferenceValue = role;
                entry.FindPropertyRelative("root").objectReferenceValue = card;
                entry.FindPropertyRelative("quantity").objectReferenceValue = quantity;
                entry.FindPropertyRelative("hire").objectReferenceValue = hire;
                entry.FindPropertyRelative("undo").objectReferenceValue = undo;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Set(controller, "summary", TutorialUiFactory.Text(content, "Folha mensal: R$ 0,00"));
            Set(controller, "confirm", TutorialUiFactory.Button(screen.transform, "Confirmar equipe e continuar", () => { }, .02f, .12f));
            Set(controller, "back", TutorialUiFactory.Button(content, "Voltar sem contratar", () => { }));
            EditorUtility.SetDirty(controller);
        }

        private static string RoleName(RoleData role) => role.roleType switch
        {
            RoleType.ATENDENTE => "Atendente", RoleType.CHAPEIRO => "Chapeiro", RoleType.CHEF => "Chef de cozinha",
            RoleType.SUSHIMAN => "Sushiman", RoleType.GERENTE => "Gerente", _ => role.displayName
        };

        private static void Set(Object component, string field, Object value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
