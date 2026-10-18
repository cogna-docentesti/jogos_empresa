using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    public static class FinalReportUiMigration
    {
        private static T Ensure<T>(Component c) where T : Component => c.GetComponent<T>() ?? c.gameObject.AddComponent<T>();
        private static Sprite Art(string id)
        {
            var shared = SharedUiSkin.Current.Get(id); if (shared != null) return shared;
            var m = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("docs/art/manifest.json"));
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/" + (string)m["assets"].Single(a => (string)a["id"] == id)["path"]);
        }
        private static RectTransform Node(Transform p, string n, float x, float y, float w, float h)
        {
            var r = p.Find(n) as RectTransform;
            if (r == null) { r = (RectTransform)new GameObject(n, typeof(RectTransform)).transform; r.SetParent(p, false); }
            r.localScale = Vector3.one; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        private static void Set(UnityEngine.Object o, string n, UnityEngine.Object v)
        { var so = new SerializedObject(o); so.FindProperty(n).objectReferenceValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static UnityEngine.UI.Image Picture(Transform p, string n, string id, float x, float y, float w, float h, bool hit = false)
        {
            var i = Ensure<UnityEngine.UI.Image>(Node(p, n, x, y, w, h)); i.sprite = id == null ? null : Art(id); i.color = Color.white; i.raycastTarget = hit;
            i.type = i.sprite != null && i.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            i.preserveAspect = i.type == UnityEngine.UI.Image.Type.Simple && i.sprite != null; return i;
        }
        private static TextMeshProUGUI Text(Transform p, string n, string value, float x, float y, float w, float h, TypographyToken size, bool bold = false, Color? color = null)
        {
            var t = Ensure<TextMeshProUGUI>(Node(p, n, x, y, w, h)); GameTypography.Apply(t, size, bold);
            t.text = value; t.color = color ?? GamePalette.Ink; t.alignment = TextAlignmentOptions.TopLeft; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; t.richText = false; t.raycastTarget = false; return t;
        }
        private static UnityEngine.UI.Button Button(Transform p, string n, string label, float x, float y, float w, float h, SharedControlKind kind = SharedControlKind.Primary)
        {
            var r = Node(p, n, x, y, w, h); var b = Ensure<UnityEngine.UI.Button>(r); b.targetGraphic = Ensure<UnityEngine.UI.Image>(r);
            var t = Text(r, "Label", label, 16, 8, w - 32, h - 16, GameTypography.Tokens.Button, true); t.alignment = TextAlignmentOptions.Center; SharedUiStyles.Bind(b, kind); return b;
        }
        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static UnityEngine.UI.VerticalLayoutGroup Vertical(RectTransform r, int padding = 6)
        {
            var v = Ensure<UnityEngine.UI.VerticalLayoutGroup>(r); v.spacing = 16; v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false; return v;
        }
        private static UnityEngine.UI.ScrollRect Scroll(Transform p, string n, float x, float y, float w, float h)
        {
            var root = Node(p, n, x, y, w, h); var s = Ensure<UnityEngine.UI.ScrollRect>(root); s.horizontal = false; s.vertical = true; s.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; s.scrollSensitivity = 48;
            var viewport = Picture(root, "Viewport", null, 0, 0, w - 28, h, true); Ensure<UnityEngine.UI.Mask>(viewport).showMaskGraphic = false;
            var content = Node(viewport.transform, "Content", 0, 0, w - 28, 0); content.anchorMax = new Vector2(1, 1); content.sizeDelta = Vector2.zero; Vertical(content);
            Ensure<UnityEngine.UI.ContentSizeFitter>(content).verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize; s.content = content; s.viewport = viewport.rectTransform;
            var track = Picture(root, "Scrollbar", "UI_SCROLLBAR_TRACK", w - 12, 0, 12, h); var area = Node(track.transform, "SlidingArea", 0, 0, 12, h); var handle = Picture(area, "Handle", "UI_SCROLLBAR_THUMB", 0, 0, 12, 80, true); Stretch(handle.rectTransform);
            var bar = Ensure<UnityEngine.UI.Scrollbar>(track); bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop; bar.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            s.verticalScrollbar = bar; s.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide; return s;
        }

        private static TextMeshProUGUI FlowText(Transform parent, string name, float width, TypographyToken size, bool bold = false)
        {
            var t = Text(parent, name, "", 0, 0, width, 48, size, bold);
            Ensure<UnityEngine.UI.LayoutElement>(t).preferredHeight = 48; return t;
        }
        private static RectTransform FlowRow(Transform parent, string name, float height, float spacing = 16)
        {
            var row = Node(parent, name, 0, 0, 1240, height); Ensure<UnityEngine.UI.LayoutElement>(row).preferredHeight = height;
            var group = Ensure<UnityEngine.UI.HorizontalLayoutGroup>(row); group.spacing = spacing;
            group.childControlWidth = group.childControlHeight = true; group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            return row;
        }
        private static void Block(Transform parent, SerializedProperty entry, string name, float width, string icon)
        {
            var card = Picture(parent, name, "UI_CARD_NORMAL", 0, 0, width, 320);
            var layout = Ensure<UnityEngine.UI.LayoutElement>(card); layout.preferredHeight = 320; layout.preferredWidth = width;
            var emblem = Picture(card.transform, "Icon", icon, 24, 24, 40, 40); emblem.color = GamePalette.Primary;
            entry.FindPropertyRelative("root").objectReferenceValue = card.rectTransform;
            entry.FindPropertyRelative("title").objectReferenceValue = Text(card.transform, "Title", "", 88, 24, width - 112, 64,new TypographyToken(TypographyRole.SectionTitle,24),true);
            entry.FindPropertyRelative("text").objectReferenceValue = Text(card.transform, "Text", "", 24, 88, width - 48, 160, GameTypography.Tokens.Body);
            entry.FindPropertyRelative("source").objectReferenceValue = Text(card.transform, "Source", "", 24, 260, width - 48, 64, GameTypography.Tokens.Caption, false, GamePalette.Muted);
        }
        [MenuItem("Tools/Jogos de Empresa/UI-15/Migrate final report")]
        public static void Apply()
        {
            if (UnityEngine.Application.dataPath != "D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if (scene.path != "Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform;
            var panel = Node(canvas, "Panel_FinalReport", 0, 0, 1920, 1080); Stretch(panel);
            var background = Ensure<UnityEngine.UI.Image>(panel); background.sprite = null; background.color = GamePalette.Surface; background.raycastTarget = true;
            var view = Ensure<FinalReportView>(panel); var controller = Ensure<FinalReportController>(panel); Set(controller, "view", view);
            Set(controller, "monthly", canvas.Find("Panel_MonthlyResult").GetComponent<MonthlyResultController>());
            Set(controller, "quarterly", canvas.Find("Panel_QuarterlyResult").GetComponent<QuarterlyResultController>());
            var listener = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UIStateListener>(true)).Single(); Set(listener, "finalReportPanel", panel.gameObject);
            var header = Picture(panel, "Header", "UI_TOPBAR_VIOLET", 0, 0, 1920, 88);
            Text(header.transform, "Brand", "Jogos de Empresa", 24, 18, 800, 60, GameTypography.Tokens.BrandTitle, true, GamePalette.OnDark);
            Set(view, "period", Text(header.transform, "Period", "RELATÓRIO FINAL", 940, 28, 956, 40, new TypographyToken(TypographyRole.Caption,24), false, GamePalette.OnDark));
            Text(panel, "Title", "Relatório final da campanha", 24, 112, 1848, 60, GameTypography.Tokens.ScreenTitle, true);
            var so = new SerializedObject(view); var tabs = so.FindProperty("tabs"); tabs.arraySize = 3;
            string[] labels = { "Conclusão", "Business Model Canvas", "Reflexão e justificativas" };
            for (int i = 0; i < 3; i++)
            {
                var button = Button(panel, "Tab" + i, labels[i], 24 + i * 624, 184, 608, 60, SharedControlKind.Tab);
                GameTypography.KeepSize(button.GetComponentInChildren<TMP_Text>(), TypographyRole.Navigation);
                tabs.GetArrayElementAtIndex(i).objectReferenceValue = button;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var loaded = Node(panel, "LoadedBody", 0, 0, 1920, 984); Set(view, "loadedBody", loaded.gameObject);
            var overview = Node(loaded, "Conclusion", 0, 0, 1920, 984); var bmc = Node(loaded, "BMC", 0, 0, 1920, 984); var reflection = Node(loaded, "Reflection", 0, 0, 1920, 984);
            so = new SerializedObject(view); var pages = so.FindProperty("pages"); pages.arraySize = 3;
            pages.GetArrayElementAtIndex(0).objectReferenceValue = overview.gameObject; pages.GetArrayElementAtIndex(1).objectReferenceValue = bmc.gameObject; pages.GetArrayElementAtIndex(2).objectReferenceValue = reflection.gameObject; so.ApplyModifiedPropertiesWithoutUndo();
            var overviewScroll = Scroll(overview, "Scroll", 24, 270, 1268, 690); Set(view, "overviewScroll", overviewScroll); Vertical(overviewScroll.content, 6);
            Set(view, "company", FlowText(overviewScroll.content, "Company", 1216, GameTypography.Tokens.SectionTitle, true));
            Set(view, "identity", FlowText(overviewScroll.content, "Identity", 1216, GameTypography.Tokens.Body));
            Set(view, "conclusion", FlowText(overviewScroll.content, "Conclusion", 1216, GameTypography.Tokens.Body));
            var kpis = FlowRow(overviewScroll.content, "Kpis", 120);
            string[] fields = { "revenue", "net", "cash" }; string[] titles = { "Receita acumulada", "Resultado acumulado", "Último caixa salvo" };
            for (int i = 0; i < 3; i++)
            {
                var card = Picture(kpis, "Kpi" + i, "UI_KPI_TILE", 0, 0, 392, 120); Ensure<UnityEngine.UI.LayoutElement>(card).preferredHeight = 120;
                Text(card.transform, "Label", titles[i], 20, 16, 352, 32, GameTypography.Tokens.Label);
                Set(view, fields[i], Text(card.transform, "Value", "", 20, 54, 352, 56, GameTypography.Tokens.DisplayValue, true));
            }
            Set(view, "reputation", FlowText(overviewScroll.content, "Reputation", 1216, GameTypography.Tokens.BodyStrong,true));
            Set(view, "alignment", FlowText(overviewScroll.content, "Alignment", 1216, GameTypography.Tokens.BodyStrong,true));
            var links = FlowRow(overviewScroll.content, "Results", 68);
            so = new SerializedObject(view); var months = so.FindProperty("monthButtons"); months.arraySize = 3;
            for (int i = 0; i < 3; i++) { var b = Button(links, "Month" + (i + 1), "DRE • mês " + (i + 1), 0, 0, 244, 68, SharedControlKind.Secondary); var layout = Ensure<UnityEngine.UI.LayoutElement>(b); layout.preferredWidth = 244; layout.preferredHeight = 68; months.GetArrayElementAtIndex(i).objectReferenceValue = b; }
            var quarter = Button(links, "Quarterly", "Comparar trimestre", 0, 0, 444, 68); var quarterLayout = Ensure<UnityEngine.UI.LayoutElement>(quarter); quarterLayout.preferredWidth = 444; quarterLayout.preferredHeight = 68; so.FindProperty("quarterlyButton").objectReferenceValue = quarter; so.ApplyModifiedPropertiesWithoutUndo();
            var notice = FlowText(overviewScroll.content, "Limit", 1216, GameTypography.Tokens.Caption); notice.text = "Os totais incluem apenas meses salvos. Caixa e indicadores finais usam o último mês disponível. Ausências permanecem identificadas."; notice.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 72;
            Picture(overview, "ConclusionIllustration", "REPORT_CONCLUSION", 1340, 300, 480, 320);
            Text(overview, "ArtNote", "Seus registros continuam disponíveis para consulta.\n\nO BMC reúne escolhas confirmadas e aponta o que ainda não possui fonte. A reflexão distingue decisões salvas de justificativas não registradas.", 1340, 660, 532, 260, GameTypography.Tokens.Body);
            var bmcScroll = Scroll(bmc, "Scroll", 24, 270, 1872, 690); Set(view, "bmcScroll", bmcScroll);
            so = new SerializedObject(view); var blocks = so.FindProperty("bmc"); blocks.arraySize = 9;
            string[] icons = { "ICON_PARTNERS", "ICON_RESTAURANT", "ICON_EQUIPMENT", "ICON_VALUE_PROPOSITION", "ICON_CUSTOMER_RELATIONSHIP", "ICON_SALES_CHANNELS", "ICON_CLIENTS", "ICON_CASH", "ICON_REVENUE" };
            for (int row = 0; row < 3; row++) { var line = FlowRow(bmcScroll.content, "Row" + row, 320); for (int col = 0; col < 3; col++) { int i = row * 3 + col; Block(line, blocks.GetArrayElementAtIndex(i), "Block" + i, 600, icons[i]); } }
            so.ApplyModifiedPropertiesWithoutUndo();
            Picture(reflection, "GuidingCharacter", "CH01_GUIDING", 24, 220, 420, 760);
            var reflectionScroll = Scroll(reflection, "Scroll", 480, 270, 1416, 690); Set(view, "reflectionScroll", reflectionScroll);
            so = new SerializedObject(view); blocks = so.FindProperty("reflection"); blocks.arraySize = 4;
            for (int i = 0; i < 4; i++) Block(reflectionScroll.content, blocks.GetArrayElementAtIndex(i), "Block" + i, 1384, i == 1 ? "ICON_CALENDAR" : "ICON_DOCUMENT"); so.ApplyModifiedPropertiesWithoutUndo();
            var state = Node(panel, "StateBody", 24, 270, 1872, 690); Set(view, "stateBody", state.gameObject);
            Picture(state, "Empty", "EMPTY_RESULTS", 40, 90, 480, 320);
            Set(view, "stateTitle", Text(state, "Title", "", 600, 130, 1160, 100, GameTypography.Tokens.SectionTitle, true));
            Set(view, "stateMessage", Text(state, "Message", "", 600, 260, 1160, 180, GameTypography.Tokens.Body));
            Set(view, "retryButton", Button(state, "Retry", "Tentar novamente", 600, 500, 360, 68, SharedControlKind.Secondary));
            var footer = Picture(panel, "BottomBar", "UI_NAVBAR_PLUM", 0, 984, 1920, 96);
            Set(view, "newGameButton", Button(footer.transform, "NewGame", "Novo jogo", 24, 14, 336, 68, SharedControlKind.Secondary));
            Text(footer.transform, "Note", "Resultado salvo. Novo jogo exige confirmação e preserva os registros anteriores.", 392, 14, 820, 68, GameTypography.Tokens.Caption, false, GamePalette.OnDark);
            Set(view, "backButton", Button(footer.transform, "Back", "Voltar à conclusão", 1244, 14, 364, 68));
            var modal = Node(panel, "NewGameDialog", 0, 0, 1920, 1080); Stretch(modal); var shade = Ensure<UnityEngine.UI.Image>(modal); shade.color = new Color(.14f, .06f, .23f, .82f); shade.raycastTarget = true; Ensure<SharedModalFocus>(modal);
            var dialog = Picture(modal, "Dialog", "UI_MODAL_SURFACE", 480, 290, 960, 500, true);
            Text(dialog.transform, "Title", "Iniciar uma nova campanha?", 32, 32, 896, 64, GameTypography.Tokens.SectionTitle, true);
            Set(view, "newGameMessage", Text(dialog.transform, "Message", "", 32, 120, 896, 220, GameTypography.Tokens.Body));
            Set(view, "cancelNewGame", Button(dialog.transform, "Cancel", "Continuar consultando", 32, 392, 432, 68, SharedControlKind.Secondary));
            Set(view, "confirmNewGame", Button(dialog.transform, "Confirm", "Iniciar novo jogo", 496, 392, 432, 68));
            Set(view, "newGameDialog", modal.gameObject); modal.gameObject.SetActive(false);
            canvas.GetComponentInChildren<HubBackButton>(true).ConfigureFinalReport(panel.gameObject);
            bmc.gameObject.SetActive(false); reflection.gameObject.SetActive(false); state.gameObject.SetActive(false);
            int menuIndex = canvas.Find("MenuAccess").GetSiblingIndex(); panel.SetSiblingIndex(menuIndex - (panel.GetSiblingIndex() < menuIndex ? 1 : 0)); panel.gameObject.SetActive(false);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
