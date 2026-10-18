using System;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI.Navigation;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    public static class TeamUiMigration
    {
        private static Sprite Art(string id)
        {
            var shared = SharedUiSkin.Current.Get(id); if (shared != null) return shared;
            var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("docs/art/manifest.json"));
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/" + (string)manifest["assets"].Single(a => (string)a["id"] == id)["path"]);
        }
        private static T Ensure<T>(Component c) where T : Component => c.GetComponent<T>() ?? c.gameObject.AddComponent<T>();
        private static RectTransform Node(Transform parent, string name, float x, float y, float w, float h)
        {
            var r = parent.Find(name) as RectTransform;
            if (r == null) { r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; r.SetParent(parent, false); }
            r.gameObject.SetActive(true); r.localScale = Vector3.one; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        private static UnityEngine.UI.Image Picture(Transform p, string n, string id, float x, float y, float w, float h, bool hit = false)
        {
            var i = Ensure<UnityEngine.UI.Image>(Node(p, n, x, y, w, h)); i.sprite = id == null ? null : Art(id); i.color = Color.white; i.raycastTarget = hit;
            i.type = i.sprite != null && i.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            i.preserveAspect = i.type == UnityEngine.UI.Image.Type.Simple && i.sprite != null; return i;
        }
        private static TextMeshProUGUI Text(Transform p, string n, string value, float x, float y, float w, float h, float size, bool bold = false, Color? color = null)
        {
            var t = Ensure<TextMeshProUGUI>(Node(p, n, x, y, w, h));
            GameTypography.Apply(t, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameTypography.FontAssetPath), size, bold);
            t.text = value; t.color = color ?? GamePalette.Ink; t.alignment = TextAlignmentOptions.TopLeft; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false; return t;
        }
        private static void Set(UnityEngine.Object o, string n, UnityEngine.Object v)
        { var so = new SerializedObject(o); so.FindProperty(n).objectReferenceValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Retire(Transform root)
        {
            var layout = root.GetComponent<UnityEngine.UI.LayoutGroup>(); if (layout != null) layout.enabled = false;
            var fitter = root.GetComponent<UnityEngine.UI.ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
            foreach (Transform c in root) { c.gameObject.SetActive(false); foreach (var g in c.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.raycastTarget = false; }
        }
        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static UnityEngine.UI.Button Button(Transform p, string n, string label, float x, float y, float w, float h, SharedControlKind kind = SharedControlKind.Primary)
        {
            var r = Node(p, n, x, y, w, h); Retire(r); var b = Ensure<UnityEngine.UI.Button>(r); b.targetGraphic = Ensure<UnityEngine.UI.Image>(r);
            var t = Text(r, "Label", label, 12, 8, w - 24, h - 16, GameTypography.Button, true); t.alignment = TextAlignmentOptions.Center;
            SharedUiStyles.Bind(b, kind); r.Find("SharedFocus").gameObject.SetActive(true); return b;
        }
        private static void Card()
        {
            var go = new GameObject("TeamRoleCard", typeof(RectTransform));
            try
            {
                var r = (RectTransform)go.transform; r.sizeDelta = new Vector2(648, 368); var card = go.AddComponent<TeamRoleCardView>();
                var surface = SharedUiStyles.Surface(go, "UI_CARD_NORMAL", true);
                var control = go.AddComponent<UnityEngine.UI.Button>(); control.targetGraphic = surface;
                Set(card, "environment", Picture(r, "Environment", "TEAM_ENV_SERVICE_COUNTER", 20, 20, 242, 136));
                Set(card, "portrait", Picture(r, "Portrait", "PORTRAIT_ATTENDANT", 34, 164, 216, 192));
                Set(card, "nameText", Text(r, "Name", "Função", 286, 24, 338, 64, GameTypography.SectionTitle, true));
                Set(card, "salaryText", Text(r, "Salary", "", 286, 100, 338, 40, GameTypography.Body, true));
                Set(card, "capacityText", Text(r, "Capacity", "", 286, 144, 338, 64, GameTypography.Label));
                Set(card, "requirementText", Text(r, "Requirement", "", 286, 216, 338, 56, GameTypography.Caption));
                Set(card, "removeButton", Button(r, "Remove", "−", 286, 276, 68, 68, SharedControlKind.Secondary));
                var q = Text(r, "Quantity", "0", 366, 276, 178, 68, GameTypography.Body, true); q.alignment = TextAlignmentOptions.Center; Set(card, "quantityText", q);
                Set(card, "addButton", Button(r, "Add", "+", 556, 276, 68, 68));
                SharedUiStyles.Bind(control, SharedControlKind.Card);
                var outline = Picture(r, "InteractionOutline", "UI_CARD_SELECTED_OUTLINE", 0, 0, 648, 368); Stretch(outline.rectTransform); Set(card, "interactionOutline", outline); outline.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/TeamRoleCard.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [MenuItem("Tools/Jogos de Empresa/UI-11/Migrate team screen")]
        public static void Apply()
        {
            if (UnityEngine.Application.dataPath != "D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if (scene.path != "Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform;
            var panel = canvas.Find("Panel_Team") as RectTransform;
            if (panel == null) throw new InvalidOperationException("The existing Panel_Team is required.");
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "UI-11 team screen"); Card(); Retire(panel); Stretch(panel);
            var safe = panel.GetComponent<Game.Adapter.In.UI.Layout.SafeAreaFitter>(); if (safe != null) safe.Recapture();
            var bg = Ensure<UnityEngine.UI.Image>(panel); bg.sprite = null; bg.color = GamePalette.Surface; bg.raycastTarget = true;
            var view = Ensure<TeamScreenView>(panel); Set(Ensure<TeamScreenController>(panel), "view", view);
            var header = Picture(panel, "Header", "UI_TOPBAR_VIOLET", 0, 0, 1920, 88); Retire(header.transform);
            Text(header.transform, "Brand", "Jogos de Empresa", 24, 18, 800, 60, GameTypography.BrandTitle, true, GamePalette.OnDark);
            Set(view, "stepText", Text(header.transform, "Context", "PASSO 7 DE 7  •  EQUIPE", 1040, 28, 856, 40, GameTypography.Body, false, GamePalette.OnDark));
            Text(panel, "TitleText", "Equipe e RH", 24, 112, 1000, 56, GameTypography.ScreenTitle, true);
            Set(view, "contextText", Text(panel, "ContextText", "", 24, 172, 1000, 32, GameTypography.Body));
            Set(view, "hintText", Text(panel, "Guidance", "", 1056, 116, 840, 88, GameTypography.Body));
            var body = Node(panel, "Body", 24, 216, 1872, 752); Retire(body);
            var catalog = Node(body, "CatalogScroll", 0, 0, 1352, 752); var scroll = Ensure<UnityEngine.UI.ScrollRect>(catalog);
            scroll.vertical = true; scroll.horizontal = false; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 48;
            var viewport = Picture(catalog, "Viewport", null, 0, 0, 1328, 752, true); Ensure<UnityEngine.UI.Mask>(viewport).showMaskGraphic = false;
            var content = Node(viewport.transform, "Content", 0, 0, 1328, 0); content.anchorMax = new Vector2(1, 1); content.sizeDelta = Vector2.zero;
            var grid = Ensure<UnityEngine.UI.GridLayoutGroup>(content); grid.cellSize = new Vector2(648, 368); grid.spacing = new Vector2(16, 16); grid.padding = new RectOffset(6, 6, 6, 6);
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
            Ensure<UnityEngine.UI.ContentSizeFitter>(content).verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = viewport.rectTransform;
            var track = Picture(catalog, "Scrollbar", "UI_SCROLLBAR_TRACK", 1340, 0, 12, 752); var area = Node(track.transform, "SlidingArea", 0, 0, 12, 752);
            var handle = Picture(area, "Handle", "UI_SCROLLBAR_THUMB", 0, 0, 12, 80, true); Stretch(handle.rectTransform);
            var bar = Ensure<UnityEngine.UI.Scrollbar>(track); bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            bar.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None }; scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            Set(view, "cardContainer", content); Set(view, "catalogScroll", scroll); Set(view, "cardPrefab", AssetDatabase.LoadAssetAtPath<TeamRoleCardView>("Assets/Prefabs/TeamRoleCard.prefab"));
            var error = Text(body, "CatalogError", "Equipe indisponível.\nConfira os dados para continuar.", 80, 280, 1152, 120, GameTypography.Body); error.alignment = TextAlignmentOptions.Center; Set(view, "catalogError", error.gameObject); error.gameObject.SetActive(false);
            var summary = Picture(body, "Summary", "UI_PANEL_IVORY", 1376, 0, 496, 752);
            Text(summary.transform, "Title", "Sua equipe", 24, 24, 448, 48, GameTypography.SectionTitle, true);
            Set(view, "countText", Text(summary.transform, "Count", "", 24, 84, 448, 40, GameTypography.Body, true));
            Text(summary.transform, "PayrollLabel", "Folha mensal", 24, 140, 448, 32, GameTypography.Label);
            var payroll = Text(summary.transform, "Payroll", "", 24, 180, 448, 48, GameTypography.SectionTitle, true); payroll.alignment = TextAlignmentOptions.Right; Set(view, "payrollText", payroll);
            Text(summary.transform, "CapacityLabel", "Capacidade da equipe", 24, 244, 448, 32, GameTypography.Label);
            Set(view, "capacityText", Text(summary.transform, "Capacity", "", 24, 282, 448, 40, GameTypography.Body, true));
            Text(summary.transform, "CapacityNote", "A operação também depende do espaço e dos equipamentos.", 24, 326, 448, 64, GameTypography.Caption);
            Text(summary.transform, "AlignmentLabel", "Alinhamento das escolhas", 24, 406, 448, 32, GameTypography.Label);
            Set(view, "alignmentText", Text(summary.transform, "Alignment", "", 24, 444, 448, 40, GameTypography.Body, true));
            Text(summary.transform, "RequirementsLabel", "Mínimos do restaurante", 24, 500, 448, 32, GameTypography.Label);
            Set(view, "requirementsText", Text(summary.transform, "Requirements", "", 24, 538, 448, 80, GameTypography.Label));
            var empty = Picture(summary.transform, "EmptyTeam", "EMPTY_TEAM", 164, 616, 168, 112); Set(view, "emptyTeam", empty.gameObject);
            var footer = Picture(panel, "BottomBar", "UI_NAVBAR_PLUM", 0, 984, 1920, 96); Retire(footer.transform);
            Text(footer.transform, "Note", "Use + e − para ajustar as contratações.", 304, 26, 848, 44, GameTypography.Body, false, GamePalette.OnDark);
            var confirm = Button(footer.transform, "Confirm", "Confirmar equipe e abrir hub", 1128, 14, 480, 68); Set(view, "confirmButton", confirm); Set(view, "confirmLabel", confirm.GetComponentInChildren<TextMeshProUGUI>());
            var so = new SerializedObject(view); var visuals = so.FindProperty("roleVisuals");
            var ids = new[] { "attendant", "grill_cook", "head_chef", "manager", "sushi_chef" };
            var envs = new[] { "SERVICE_COUNTER", "GRILL_KITCHEN", "FRENCH_KITCHEN", "MANAGEMENT_OFFICE", "SUSHI_COUNTER" }; visuals.arraySize = ids.Length;
            for (int i = 0; i < ids.Length; i++) { var v = visuals.GetArrayElementAtIndex(i); v.FindPropertyRelative("roleId").stringValue = ids[i]; v.FindPropertyRelative("portrait").objectReferenceValue = Art("PORTRAIT_" + ids[i].ToUpperInvariant()); v.FindPropertyRelative("environment").objectReferenceValue = Art("TEAM_ENV_" + envs[i]); }
            so.ApplyModifiedPropertiesWithoutUndo();
            // Baseline already contains both bindings. Only fill a genuinely missing reference.
            var listener = UnityEngine.Object.FindFirstObjectByType<UIStateListener>();
            var listenerSo = new SerializedObject(listener); if (listenerSo.FindProperty("initialTeamPanel").objectReferenceValue == null) Set(listener, "initialTeamPanel", panel.gameObject);
            var registry = canvas.GetComponentInChildren<PanelRegistry>(true); if (registry.Resolve(PanelId.Team) == null) registry.EditorSetBinding(PanelId.Team, panel.gameObject);
            panel.gameObject.SetActive(false); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
