using System;
using System.Linq;
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
    public static class EventUiMigration
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
        private static void Card()
        {
            var go = new GameObject("EventOptionCard", typeof(RectTransform));
            try
            {
                var r = (RectTransform)go.transform; r.sizeDelta = new Vector2(1120, 200); Ensure<UnityEngine.UI.LayoutElement>(r).preferredHeight = 200;
                var card = go.AddComponent<EventOptionCardView>(); var button = go.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = SharedUiStyles.Surface(go, "UI_CARD_NORMAL", true); Set(card, "button", button);
                Set(card, "titleText", Text(r, "Title", "Opção", 24, 20, 1072, 36, GameTypography.Tokens.BodyStrong,true));
                Set(card, "descriptionText", Text(r, "Description", "", 24, 68, 1072, 32, GameTypography.Tokens.Label));
                Set(card, "effectsText", Text(r, "Effects", "", 24, 112, 1072, 64, GameTypography.Tokens.Label)); SharedUiStyles.Bind(button, SharedControlKind.Card);
                PrefabUtility.SaveAsPrefabAsset(go, "Assets/Prefabs/EventOptionCard.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        [MenuItem("Tools/Jogos de Empresa/UI-12/Migrate events and history")]
        public static void Apply()
        {
            if (UnityEngine.Application.dataPath != "D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if (scene.path != "Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform; Card();
            var panel = Node(canvas, "Panel_Event", 0, 0, 1920, 1080); Stretch(panel);
            var bg = Ensure<UnityEngine.UI.Image>(panel); bg.sprite = null; bg.color = GamePalette.Surface; bg.raycastTarget = true;
            var view = Ensure<EventScreenView>(panel); var controller = Ensure<EventScreenController>(panel); Set(controller, "view", view);
            var header = Picture(panel, "Header", "UI_TOPBAR_VIOLET", 0, 0, 1920, 88);
            Text(header.transform, "Brand", "Jogos de Empresa", 24, 18, 800, 60, GameTypography.Tokens.BrandTitle, true, GamePalette.OnDark);
            Set(view, "periodText", Text(header.transform, "Period", "EVENTOS DO MÊS", 1000, 28, 896, 40, GameTypography.Tokens.Body, false, GamePalette.OnDark));
            Set(view, "screenTitle", Text(panel, "TitleText", "Decisões do mês", 24, 112, 1100, 60, GameTypography.Tokens.ScreenTitle, true));
            Set(view, "guidanceText", Text(panel, "Guidance", "", 24, 176, 1872, 36, GameTypography.Tokens.Body));
            var body = Node(panel, "DecisionBody", 24, 232, 1872, 736); Set(view, "decisionBody", body.gameObject);
            var left = Picture(body, "IllustrationPanel", "UI_PANEL_IVORY", 0, 0, 640, 736);
            Set(view, "illustration", Picture(left.transform, "Illustration", "EVENT_BULK_PURCHASE_OPPORTUNITY", 16, 24, 608, 406));
            Text(left.transform, "ChoiceHeading", "Sua decisão faz diferença", 24, 462, 592, 48, GameTypography.Tokens.SectionTitle, true);
            Text(left.transform, "ChoiceNote", "Os efeitos de cada opção vêm do catálogo do jogo. Caixa e reputação são atualizados somente no fechamento mensal.", 24, 530, 592, 112, GameTypography.Tokens.Body);
            Text(left.transform, "ChoiceHint", "Consulte o histórico a qualquer momento.", 24, 670, 592, 48, GameTypography.Tokens.Caption);
            var scroll = Scroll(body, "DecisionScroll", 664, 0, 1208, 736); Set(view, "decisionScroll", scroll);
            Set(view, "titleText", Text(scroll.content, "EventTitle", "", 0, 0, 1150, 40, GameTypography.Tokens.SectionTitle, true));
            Set(view, "descriptionText", Text(scroll.content, "EventDescription", "", 0, 0, 1150, 60, GameTypography.Tokens.Body));
            Set(view, "conceptText", Text(scroll.content, "Concept", "", 0, 0, 1150, 30, GameTypography.Tokens.Caption));
            Text(scroll.content, "OptionsHeading", "Escolha uma opção", 0, 0, 1150, 40, GameTypography.Tokens.BodyStrong,true);
            var options = Node(scroll.content, "Options", 0, 0, 1150, 0); Vertical(options, 0); Set(view, "optionContainer", options);
            var history = Scroll(panel, "HistoryBody", 24, 232, 1872, 736); Set(view, "historyBody", history.gameObject); Set(view, "historyScroll", history); Set(view, "historyContainer", history.content);
            Set(view, "cardPrefab", AssetDatabase.LoadAssetAtPath<EventOptionCardView>("Assets/Prefabs/EventOptionCard.prefab"));
            var footer = Picture(panel, "BottomBar", "UI_NAVBAR_PLUM", 0, 984, 1920, 96);
            Set(view, "footerText", Text(footer.transform, "Note", "", 304, 26, 660, 44, GameTypography.Tokens.Caption, false, GamePalette.OnDark));
            Set(view, "historyButton", Button(footer.transform, "History", "Ver histórico", 968, 14, 252, 68, SharedControlKind.Secondary));
            Set(view, "confirmButton", Button(footer.transform, "Confirm", "Confirmar escolha", 1244, 14, 364, 68));
            Set(view, "returnButton", Button(footer.transform, "Return", "Concluir consulta", 1244, 14, 364, 68, SharedControlKind.Secondary));
            var so = new SerializedObject(view); var visuals = so.FindProperty("visuals"); var events = Resources.LoadAll<EventData>("Events").OrderBy(e => e.id).ToArray(); visuals.arraySize = events.Length;
            for (int i = 0; i < events.Length; i++) { var v = visuals.GetArrayElementAtIndex(i); v.FindPropertyRelative("eventId").stringValue = events[i].id; v.FindPropertyRelative("illustration").objectReferenceValue = Art("EVENT_" + events[i].id.ToUpperInvariant()); }
            so.ApplyModifiedPropertiesWithoutUndo();
            canvas.GetComponentInChildren<PanelRegistry>(true).EditorSetBinding(PanelId.EventHistory, panel.gameObject);
            Set(UnityEngine.Object.FindFirstObjectByType<UIStateListener>(), "roundEventPanel", panel.gameObject);
            canvas.GetComponentInChildren<HubBackButton>(true).ConfigureEvents(panel.gameObject, (RectTransform)canvas.Find("MenuAccess/Button"));
            panel.SetSiblingIndex(Mathf.Min(panel.GetSiblingIndex(), canvas.Find("MenuAccess").GetSiblingIndex()));
            var hub = canvas.Find("Panel_Menu"); var actions = Node(hub, "EventActions", 736, 100, 568, 68);
            var close = Button(actions, "CloseMonth", "Fechar mês", 0, 0, 272, 68); var settlement = Ensure<MonthlySettlementController>(canvas.Find("UiNavigation"));
            UnityEventTools.RemovePersistentListener(close.onClick, settlement.CloseMonth); UnityEventTools.AddPersistentListener(close.onClick, settlement.CloseMonth);
            var historyButton = Button(actions, "History", "Eventos e histórico", 296, 0, 272, 68, SharedControlKind.Secondary);
            var action = Ensure<PanelNavButton>(historyButton); action.EditorConfigure(PanelNavButton.NavAction.Open, PanelId.EventHistory);
            panel.gameObject.SetActive(false); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
