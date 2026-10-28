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
    public static class BankruptcyUiMigration
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
        [MenuItem("Tools/Jogos de Empresa/UI-16/Migrate bankruptcy")]
        public static void Apply()
        {
            if (UnityEngine.Application.dataPath != "D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if (scene.path != "Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform;
            var panel = Node(canvas, "Panel_Bankruptcy", 0, 0, 1920, 1080); Stretch(panel);
            var background = Ensure<UnityEngine.UI.Image>(panel); background.sprite = null; background.color = GamePalette.Surface; background.raycastTarget = true;
            var view = Ensure<BankruptcyView>(panel); var controller = Ensure<BankruptcyController>(panel); Set(controller, "view", view);
            Set(controller, "monthly", canvas.Find("Panel_MonthlyResult").GetComponent<MonthlyResultController>());
            Set(controller, "quarterly", canvas.Find("Panel_QuarterlyResult").GetComponent<QuarterlyResultController>());
            var listener = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UIStateListener>(true)).Single(); Set(listener, "bankruptcyPanel", panel.gameObject);
            var header = Picture(panel, "Header", "UI_TOPBAR_VIOLET", 0, 0, 1920, 88);
            Text(header.transform, "Brand", "Jogos de Empresa", 24, 18, 800, 60, GameTypography.Tokens.BrandTitle, true, GamePalette.OnDark);
            Set(view, "period", Text(header.transform, "Period", "ENCERRAMENTO DA CAMPANHA", 940, 28, 956, 40, new TypographyToken(TypographyRole.Caption,24), false, GamePalette.OnDark));
            Set(view, "screenTitle", Text(panel, "Title", "Seu negócio encerrou esta campanha", 24, 112, 1848, 60, GameTypography.Tokens.ScreenTitle, true));
            Picture(panel, "BusinessDifficulty", "BANKRUPTCY_OVERLAY", 64, 220, 480, 320);
            Picture(panel, "ConcernedCharacter", "CH01_CONCERNED", 144, 540, 320, 440);
            var loaded = Node(panel, "LoadedBody", 0, 0, 1920, 984); Set(view, "loadedBody", loaded.gameObject);
            var scroll = Scroll(loaded, "Scroll", 624, 208, 1272, 752); Set(view, "scroll", scroll);
            var content = scroll.content; Vertical(content, 6);
            foreach (var item in new[] { ("company", "Company", GameTypography.Tokens.SectionTitle), ("reason", "Reason", GameTypography.Tokens.Body), ("counter", "Counter", new TypographyToken(TypographyRole.Caption,24)) })
                Set(view, item.Item1, FlowText(content, item.Item2, 1192, item.Item3, item.Item1 != "reason"));
            var rule = FlowText(content, "CashRule", 1192, GameTypography.Tokens.Body); rule.text = "Saldo negativo isolado não causa falência. Um mês com resultado zero ou positivo interrompe a sequência de prejuízos."; rule.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 72;
            var kpis = Node(content, "Kpis", 0, 0, 1232, 128); Ensure<UnityEngine.UI.LayoutElement>(kpis).preferredHeight = 128;
            string[] fields = { "net", "cash" }; string[] captions = { "Último resultado de caixa salvo", "Último saldo de caixa salvo" };
            for (int i = 0; i < 2; i++)
            {
                var card = Picture(kpis, "Kpi" + i, "UI_KPI_TILE", i * 620, 0, 600, 128);
                Text(card.transform, "Label", captions[i], 20, 16, 560, 32, GameTypography.Tokens.Label);
                Set(view, fields[i], Text(card.transform, "Value", "", 20, 60, 560, 56, GameTypography.Tokens.DisplayValue, true));
            }
            Set(view, "reputation", FlowText(content, "Reputation", 1192, GameTypography.Tokens.Body));
            Set(view, "history", FlowText(content, "History", 1192, GameTypography.Tokens.Body));
            var so = new SerializedObject(view); var buttons = so.FindProperty("monthButtons"); buttons.arraySize = 3; var values = so.FindProperty("monthValues"); values.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                var row = Picture(content, "Month" + (i + 1), "UI_CARD_NORMAL", 0, 0, 1232, 88); Ensure<UnityEngine.UI.LayoutElement>(row).preferredHeight = 88;
                Text(row.transform, "Label", "Resultado • mês " + (i + 1), 24, 26, 320, 40,new TypographyToken(TypographyRole.Label,24),false);
                var amount = Text(row.transform, "Value", "", 380, 26, 440, 40,GameTypography.Tokens.Body,false); amount.alignment = TextAlignmentOptions.TopRight;
                values.GetArrayElementAtIndex(i).objectReferenceValue = amount;
                buttons.GetArrayElementAtIndex(i).objectReferenceValue = Button(row.transform, "Review", "Consultar mês " + (i + 1), 856, 12, 344, 64, SharedControlKind.Secondary);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var quarter = Button(content, "Quarterly", "Comparar os três meses", 0, 0, 1232, 68, SharedControlKind.Secondary); Ensure<UnityEngine.UI.LayoutElement>(quarter).preferredHeight = 68; Set(view, "quarterlyButton", quarter);
            var note = FlowText(content, "Preservation", 1192, GameTypography.Tokens.Caption); note.text = "Revise os resultados para planejar outras escolhas em uma nova campanha. Consultar e voltar não altera valores nem aplica novamente os efeitos dos eventos."; note.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 72;
            var state = Node(panel, "StateBody", 624, 208, 1272, 752); Set(view, "stateBody", state.gameObject);
            Set(view, "stateTitle", Text(state, "Title", "", 32, 96, 1208, 100, GameTypography.Tokens.SectionTitle, true));
            Set(view, "stateMessage", Text(state, "Message", "", 32, 240, 1208, 180, GameTypography.Tokens.Body));
            Set(view, "retryButton", Button(state, "Retry", "Tentar novamente", 32, 488, 432, 68, SharedControlKind.Secondary));
            var footer = Picture(panel, "BottomBar", "UI_NAVBAR_PLUM", 0, 984, 1920, 96);
            Set(view, "newGameButton", Button(footer.transform, "NewGame", "Novo jogo", 24, 14, 336, 68, SharedControlKind.Secondary));
            Text(footer.transform, "Note", "Seus registros permanecem salvos.", 392, 14, 680, 68, GameTypography.Tokens.Caption, false, GamePalette.OnDark);
            Set(view, "reviewButton", Button(footer.transform, "Review", "Consultar último mês", 1132, 14, 476, 68));
            var modal = Node(panel, "NewGameDialog", 0, 0, 1920, 1080); Stretch(modal); var shade = Ensure<UnityEngine.UI.Image>(modal); shade.color = new Color(.14f, .06f, .23f, .82f); shade.raycastTarget = true; Ensure<SharedModalFocus>(modal);
            var dialog = Picture(modal, "Dialog", "UI_MODAL_SURFACE", 480, 290, 960, 500, true);
            Text(dialog.transform, "Title", "Iniciar uma nova campanha?", 32, 32, 896, 64, GameTypography.Tokens.SectionTitle, true);
            Set(view, "newGameMessage", Text(dialog.transform, "Message", "", 32, 120, 896, 220, GameTypography.Tokens.Body));
            Set(view, "cancelNewGame", Button(dialog.transform, "Cancel", "Continuar consultando", 32, 392, 432, 68, SharedControlKind.Secondary));
            Set(view, "confirmNewGame", Button(dialog.transform, "Confirm", "Iniciar novo jogo", 496, 392, 432, 68));
            Set(view, "newGameDialog", modal.gameObject); modal.gameObject.SetActive(false);
            canvas.GetComponentInChildren<HubBackButton>(true).ConfigureBankruptcy(panel.gameObject);
            state.gameObject.SetActive(false);
            int menuIndex = canvas.Find("MenuAccess").GetSiblingIndex(); panel.SetSiblingIndex(menuIndex - (panel.GetSiblingIndex() < menuIndex ? 1 : 0)); panel.gameObject.SetActive(false);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
