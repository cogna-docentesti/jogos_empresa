using System;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>UI-17 adds feedback to existing production panels, without rebuilding their composition.</summary>
    public static class FeedbackUiMigration
    {
        private static readonly string[] artIds = { "UI_SPINNER", "TUTORIAL_TAP", "TUTORIAL_DRAG", "TUTORIAL_FOCUS_RING" };
        private static Sprite Art(string id)
        {
            var shared = SharedUiSkin.Current.Get(id); if (shared != null) return shared;
            var data = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("docs/art/manifest.json"));
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/" + (string)data["assets"].Single(a => (string)a["id"] == id)["path"]);
        }
        private static T Ensure<T>(Component c) where T : Component
        { var found = c.GetComponent<T>(); return found != null ? found : c.gameObject.AddComponent<T>(); }
        private static RectTransform Node(Transform p, string n, float x, float y, float w, float h)
        {
            var r = p.Find(n) as RectTransform;
            if (r == null) { r = (RectTransform)new GameObject(n, typeof(RectTransform)).transform; r.SetParent(p, false); }
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        private static UnityEngine.UI.Image Picture(Transform p, string n, string id, float x, float y, float w, float h, bool hit = false)
        {
            var i = Ensure<UnityEngine.UI.Image>(Node(p, n, x, y, w, h)); i.sprite = Art(id); i.color = Color.white; i.raycastTarget = hit;
            i.type = i.sprite.border == Vector4.zero ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced; i.preserveAspect = i.type == UnityEngine.UI.Image.Type.Simple; return i;
        }
        private static TMP_Text Text(Transform p, string n, string value, float x, float y, float w, float h)
        {
            var t = Ensure<TextMeshProUGUI>(Node(p, n, x, y, w, h)); GameTypography.Apply(t, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameTypography.FontAssetPath), GameTypography.Body, false);
            t.text = value; t.color = GamePalette.Ink; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false; return t;
        }
        private static UnityEngine.UI.Button Button(Transform p, string n, string label, float x, float y, float w, float h)
        {
            var b = Ensure<UnityEngine.UI.Button>(Node(p, n, x, y, w, h)); b.targetGraphic = Ensure<UnityEngine.UI.Image>(b); var t = Text(b.transform, "Label", label, 8, 8, w - 16, h - 16); t.alignment = TextAlignmentOptions.Center;
            SharedUiStyles.Bind(b, SharedControlKind.Secondary); return b;
        }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var s = new SerializedObject(target); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Motion(Transform p, string id, float x, float y, float size)
        {
            int index = Array.IndexOf(artIds, id);
            Picture(p, id + "Backdrop", "UI_PANEL_PLUM", x - 4, y - 4, size + 8, size + 8).transform.SetAsFirstSibling();
            var r = Node(p, id, x, y, size, size); var i = Ensure<UnityEngine.UI.Image>(r); i.sprite = Art(id); i.color = Color.white; i.preserveAspect = true; i.raycastTarget = false;
            r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = new Vector2(x + size / 2, -y - size / 2);
            Ensure<UiMotion>(r).Configure((UiMotion.Motion)index);
        }
        private static void Help(Transform p, GameState state, string id, string message, Transform hint, UnityEngine.UI.ScrollRect scroll, bool bank = false)
        {
            var host = Ensure<ContextualHelp>(p); var surface = Picture(p, "ContextualHelp", "UI_TOOLTIP_SURFACE", bank ? 24 : 1056, bank ? 744 : 108, bank ? 480 : 840, bank ? 216 : 96);
            Motion(surface.transform, id, 16, 16, 64);
            Text(surface.transform, "Message", message, 96, 12, bank ? 360 : 584, bank ? 108 : 76);
            var dismiss = Button(surface.transform, "Dismiss", "Entendi", bank ? 96 : 696, bank ? 136 : 16, bank ? 360 : 128, 64);
            Set(host, "panel", surface.gameObject); Set(host, "regularHint", hint?.gameObject); Set(host, "dismiss", dismiss);
            if (scroll != null && !bank) Ensure<HelpScrollInput>(scroll).help = host;
            var so = new SerializedObject(host); so.FindProperty("stage").intValue = (int)state; so.ApplyModifiedPropertiesWithoutUndo(); surface.gameObject.SetActive(false);
        }
        private static void Loading(Transform equipment)
        {
            var loader = Ensure<SharedLoading>(equipment); var input = Ensure<CanvasGroup>(equipment); Set(loader, "input", input);
            var panel = Picture(equipment, "CatalogLoading", "UI_TOOLTIP_SURFACE", 228, 360, 944, 264, true);
            var group = Ensure<CanvasGroup>(panel); group.ignoreParentGroups = true;
            Motion(panel.transform, "UI_SPINNER", 40, 40, 48);
            Text(panel.transform, "Message", "Carregando o catálogo de equipamentos…\nVocê pode cancelar esta consulta.", 112, 36, 784, 92);
            var cancel = Button(panel.transform, "Cancel", "Cancelar carregamento", 264, 164, 416, 68);
            Set(loader, "overlay", panel.gameObject); Set(loader, "cancel", cancel); panel.gameObject.SetActive(false);
            var catalog = (UnityEngine.UI.ScrollRect)new SerializedObject(equipment.GetComponent<EquipmentScreenView>()).FindProperty("catalogScroll").objectReferenceValue;
            var retry = Button(catalog.transform.parent, "RetryCatalog", "Tentar novamente", 416, 400, 496, 68);
            Set(equipment.GetComponent<EquipmentScreenView>(), "retryCatalogButton", retry); retry.gameObject.SetActive(false);
        }
        private static void Toast(Transform canvas)
        {
            var host = Ensure<FeedbackToast>(Node(canvas, "Feedback", 0, 0, 1920, 1080));
            // Reserved central header area; never intersects cards, navigation or footer actions.
            var panel = Picture(host.transform, "Toast", "UI_TOAST_NEUTRAL", 488, 8, 944, 72);
            var message = Text(panel.transform, "Message", "", 20, 10, 812, 52); message.fontSize = GameTypography.Caption;
            var close = Button(panel.transform, "Close", "×", 856, 4, 68, 64);
            Set(host, "panel", panel.gameObject); Set(host, "message", message); Set(host, "surface", panel); Set(host, "close", close);
            Set(host, "success", Art("UI_TOAST_SUCCESS")); Set(host, "warning", Art("UI_TOAST_WARNING")); Set(host, "error", Art("UI_TOAST_NEUTRAL"));
            panel.gameObject.SetActive(false);
        }
        private static void Equipment(Transform equipment)
        {
            Loading(equipment);
            var catalog = (UnityEngine.UI.ScrollRect)new SerializedObject(equipment.GetComponent<EquipmentScreenView>()).FindProperty("catalogScroll").objectReferenceValue;
            Help(equipment, GameState.Initial_Equipment, "TUTORIAL_DRAG", "Arraste ou role o catálogo. Use + para adicionar; Tab e Enter também funcionam.", equipment.Find("Guidance"), catalog);
        }
        [MenuItem("Tools/Jogos de Empresa/UI-17/Integrate contextual feedback")]
        public static void Apply()
        {
            if (UnityEngine.Application.dataPath != "D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if (scene.path != "Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            foreach (string id in artIds)
            {
                var temp = new GameObject(id, typeof(RectTransform));
                var image = temp.AddComponent<UnityEngine.UI.Image>(); image.sprite = Art(id); image.color = Color.white; image.preserveAspect = true; image.raycastTarget = false;
                ((RectTransform)temp.transform).sizeDelta = Vector2.one * (id == "UI_SPINNER" ? 48 : 64);
                temp.AddComponent<UiMotion>().Configure((UiMotion.Motion)Array.IndexOf(artIds, id));
                PrefabUtility.SaveAsPrefabAsset(temp, "Assets/Prefabs/SharedUI/" + id + ".prefab"); UnityEngine.Object.DestroyImmediate(temp);
            }
            const string path = "Assets/Resources/EquipmentCatalogIndex.asset";
            var index = AssetDatabase.LoadAssetAtPath<EquipmentCatalogIndex>(path);
            if (index == null) { index = ScriptableObject.CreateInstance<EquipmentCatalogIndex>(); AssetDatabase.CreateAsset(index, path); }
            index.resourcePaths = AssetDatabase.FindAssets("t:EquipmentData", new[] { "Assets/Resources/Equipments" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).Select(p => p.Substring(p.IndexOf("Resources/", StringComparison.Ordinal) + 10).Replace(".asset", "")).ToArray(); EditorUtility.SetDirty(index);
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform;
            // Keep the existing Menu action inside the footer while feedback is visible on revisits.
            var menuAction = (RectTransform)canvas.Find("MenuAccess/Button");
            menuAction.anchoredPosition = new Vector2(menuAction.anchoredPosition.x, Mathf.Max(menuAction.anchoredPosition.y, -998));
            var bank = canvas.Find("Panel_Financial");
            Help(bank, GameState.Initial_Capital, "TUTORIAL_TAP", "Selecione uma opção e confira as condições. Você também pode continuar sem crédito.", null, bank.Find("CreditOptionsScroll").GetComponent<UnityEngine.UI.ScrollRect>(), true);
            var help = Button(bank, "CreditHelp", "?", 832, 112, 68, 68);
            var tip = Picture(bank, "CreditTooltip", "UI_TOOLTIP_SURFACE", 24, 208, 480, 224);
            Text(tip.transform, "Message", "O crédito entra no caixa. As parcelas incluem amortização e juros e são pagas no fechamento mensal.\nToque novamente em ? para fechar.", 24, 16, 432, 192);
            Ensure<SharedTooltip>(help).panel = tip.gameObject; tip.gameObject.SetActive(false);
            var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Panel_Equipment.prefab");
            try { Equipment(prefab.transform); PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Prefabs/Panel_Equipment.prefab"); } finally { PrefabUtility.UnloadPrefabContents(prefab); }
            Equipment(canvas.Find("Panel_Equipment"));
            var team = canvas.Find("Panel_Team");
            Help(team, GameState.Initial_Team, "TUTORIAL_FOCUS_RING", "Use Tab para focar e Enter para ativar + e −. Toque ou clique para ajustar a equipe.", team.Find("Guidance"), null);
            Toast(canvas); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
