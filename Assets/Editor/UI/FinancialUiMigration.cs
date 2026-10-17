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
    public static class FinancialUiMigration
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
            var image = Ensure<UnityEngine.UI.Image>(Node(p, n, x, y, w, h)); image.sprite = id == null ? null : Art(id); image.color = Color.white; image.raycastTarget = hit;
            image.type = image.sprite != null && image.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = image.type == UnityEngine.UI.Image.Type.Simple && image.sprite != null; return image;
        }
        private static TMP_Text Text(Transform p, string n, string value, float x, float y, float w, float h, TypographyToken size, bool bold = false, Color? color = null)
        {
            var t = Ensure<TextMeshProUGUI>(Node(p, n, x, y, w, h));
            GameTypography.Apply(t, size, bold);
            t.text = value; t.color = color ?? GamePalette.Ink; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false; return t;
        }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Retire(Transform root)
        {
            var layout = root.GetComponent<UnityEngine.UI.LayoutGroup>(); if (layout != null) layout.enabled = false;
            var fitter = root.GetComponent<UnityEngine.UI.ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
            foreach (Transform child in root) { child.gameObject.SetActive(false); foreach (var g in child.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.raycastTarget = false; }
        }
        private static UnityEngine.UI.Button Button(Transform p, string n, string label, float x, float y, float w, float h)
        {
            var r = Node(p, n, x, y, w, h); var b = Ensure<UnityEngine.UI.Button>(r); b.targetGraphic = Ensure<UnityEngine.UI.Image>(r);
            var t = Text(r, "Label", label, 16, 12, w - 32, h - 24, GameTypography.Tokens.Button, true); t.alignment = TextAlignmentOptions.Center;
            SharedUiStyles.Bind(b, SharedControlKind.Primary); return b;
        }
        private static void Ignore(Component c) => Ensure<UnityEngine.UI.LayoutElement>(c).ignoreLayout = true;
        private static UnityEngine.UI.ScrollRect Scroll(Transform p)
        {
            var root = Node(p, "CreditOptionsScroll", 528, 208, 1368, 544); var scroll = Ensure<UnityEngine.UI.ScrollRect>(root);
            scroll.vertical = true; scroll.horizontal = false; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 48;
            var vp = Picture(root, "Viewport", null, 0, 0, 1344, 544, true); Ensure<UnityEngine.UI.Mask>(vp).showMaskGraphic = false;
            var content = Node(vp.transform, "CreditOptions", 0, 0, 1344, 0); content.anchorMax = Vector2.one; content.anchorMax = new Vector2(1, 1); content.sizeDelta = Vector2.zero;
            var layout = Ensure<UnityEngine.UI.VerticalLayoutGroup>(content); layout.padding = new RectOffset(6, 6, 6, 6); layout.spacing = 16; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            Ensure<UnityEngine.UI.ContentSizeFitter>(content).verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = vp.rectTransform; scroll.content = content;
            var track = Picture(root, "Scrollbar", "UI_SCROLLBAR_TRACK", 1356, 0, 12, 544); var area = Node(track.transform, "SlidingArea", 0, 0, 12, 544);
            var handle = Picture(area, "Handle", "UI_SCROLLBAR_THUMB", 0, 0, 12, 80, true); handle.rectTransform.anchorMin = Vector2.zero; handle.rectTransform.anchorMax = Vector2.one; handle.rectTransform.offsetMin = handle.rectTransform.offsetMax = Vector2.zero;
            var bar = Ensure<UnityEngine.UI.Scrollbar>(track); bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            bar.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None }; scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }
        [MenuItem("Tools/Jogos de Empresa/UI-09/Migrate financial screen")]
        public static void Apply()
        {
            if (UnityEngine.Application.dataPath != "D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if (scene.path != "Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform; var panel = canvas.Find("Panel_Financial") as RectTransform;
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "UI-09 financial composition");
            Retire(panel); panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.GetComponent<Game.Adapter.In.UI.Layout.SafeAreaFitter>()?.Recapture();
            var bg = Ensure<UnityEngine.UI.Image>(panel); bg.sprite = null; bg.color = GamePalette.Surface; bg.raycastTarget = true;
            var view = panel.GetComponent<FinancialScreenView>();
            var header = Picture(panel, "Header", "UI_TOPBAR_VIOLET", 0, 0, 1920, 88); Retire(header.transform);
            Text(header.transform, "Brand", "Jogos de Empresa", 24, 18, 700, 60, GameTypography.Tokens.BrandTitle, true, GamePalette.OnDark);
            Set(view, "stepText", Text(header.transform, "Context", "PASSO 5 DE 7  •  BANCO E CAPITAL", 1040, 28, 856, 40, GameTypography.Tokens.Body, false, GamePalette.OnDark));
            Set(view, "titleText", Text(panel, "TitleText", "Banco CogCred", 24, 112, 740, 64, GameTypography.Tokens.ScreenTitle, true));
            Text(panel, "CapitalLabel", "Capital inicial", 1000, 112, 400, 32, GameTypography.Tokens.Label);
            Set(view, "capitalValueText", Text(panel, "CapitalValue", "Indisponível", 1000, 148, 400, 48, GameTypography.Tokens.SectionTitle, true));
            Text(panel, "CashLabel", "Caixa disponível", 1456, 112, 440, 32, GameTypography.Tokens.Label);
            Set(view, "cashValueText", Text(panel, "CashValue", "Indisponível", 1456, 148, 440, 48, GameTypography.Tokens.SectionTitle, true));
            Picture(panel, "BankEnvironment", "BANK_ENVIRONMENT", 24, 208, 480, 752);
            Picture(panel, "BankAdvisor", "CH01_BANK", 54, 240, 420, 720);
            var scroll = Scroll(panel); Set(view, "cardsScrollRect", scroll); Set(view, "bankCardsContainer", scroll.content);
            var detail = Picture(panel, "CreditDetails", "UI_PANEL_IVORY", 528, 776, 1368, 184);
            Set(view, "hintText", Text(detail.transform, "Availability", "Selecione uma opção.", 24, 20, 1320, 64, GameTypography.Tokens.BodyStrong,true));
            Set(view, "summaryText", Text(detail.transform, "Summary", "Nenhuma opção selecionada.", 24, 96, 1320, 68, GameTypography.Tokens.Body));
            var footer = Picture(panel, "Footer", "UI_NAVBAR_PLUM", 0, 984, 1920, 96); Retire(footer.transform);
            Set(view, "footerHintText", Text(footer.transform, "Help", "Selecione, confira e confirme.", 328, 22, 504, 56, GameTypography.Tokens.Label, false, GamePalette.OnDark));
            var confirm = Button(footer.transform, "ConfirmCredit", "Confirmar crédito", 1120, 14, 480, 68); Set(view, "confirmButton", confirm); Set(view, "confirmLabel", confirm.transform.Find("Label").GetComponent<TMP_Text>());
            UpdateCard(); Set(view, "bankCardPrefab", AssetDatabase.LoadAssetAtPath<BankCardView>("Assets/Prefabs/Card_loan.prefab"));
            var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("docs/art/manifest.json"));
            foreach (var line in Resources.LoadAll<CreditLineData>("CreditLines"))
            {
                string id = (string)manifest["assets"].Single(a => (string)a["catalog_id"] == line.id)["id"];
                line.icon = Art(id); EditorUtility.SetDirty(line);
            }
            // Only restore the missing setup return; the equipment composition stays with UI-10.
            var equipment = canvas.Find("Panel_Equipment"); var eqFooter = equipment.Find("BottomBar");
            var back = Button(eqFooter, "ReturnToBank", "Voltar ao banco", 328, 14, 320, 68); SharedUiStyles.Bind(back, SharedControlKind.Secondary);
            var backRect = (RectTransform)back.transform; backRect.anchorMin = backRect.anchorMax = new Vector2(.5f, 0); backRect.pivot = Vector2.zero; backRect.anchoredPosition = new Vector2(-632, 14);
            Set(equipment.GetComponent<EquipmentScreenView>(), "bankBackButton", back);
            EditorUtility.SetDirty(view); panel.gameObject.SetActive(false); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        private static void UpdateCard()
        {
            const string path = "Assets/Prefabs/Card_loan.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Retire(root.transform); var r = (RectTransform)root.transform; r.sizeDelta = new Vector2(1332, 212);
                var oldLayout = root.GetComponent<UnityEngine.UI.LayoutGroup>(); if (oldLayout != null) oldLayout.enabled = false;
                var height = Ensure<UnityEngine.UI.LayoutElement>(r); height.preferredHeight = height.minHeight = 212; height.flexibleHeight = 0;
                var card = root.GetComponent<BankCardView>(); var button = Ensure<UnityEngine.UI.Button>(r); Set(card, "selectButton", button);
                Set(card, "creditLineIcon", Picture(r, "CreditArtwork", "CREDIT_SEM_CREDITO", 24, 40, 120, 120));
                Set(card, "nameText", Text(r, "Name", "Linha de crédito", 168, 24, 600, 40, GameTypography.Tokens.SectionTitle, true));
                Set(card, "descriptionText", Text(r, "Description", "Descrição da linha.", 168, 72, 600, 60, GameTypography.Tokens.Body));
                Set(card, "availabilityText", Text(r, "Availability", "Disponibilidade", 168, 140, 600, 36, GameTypography.Tokens.Label, true));
                var principal = Text(r, "Principal", "Capital próprio", 816, 24, 440, 52, GameTypography.Tokens.DisplayValue, true); principal.alignment = TextAlignmentOptions.Right; Set(card, "maxAmountText", principal);
                Set(card, "interestRateText", Text(r, "Interest", "Sem juros", 816, 86, 484, 32, GameTypography.Tokens.Label));
                Set(card, "termText", Text(r, "Term", "Sem parcelas", 816, 120, 484, 32, GameTypography.Tokens.Label));
                Set(card, "installmentText", Text(r, "Installment", "", 816, 154, 484, 52, GameTypography.Tokens.Label, true));
                Set(card, "selectionMark", Picture(r, "SelectionMark", "UI_RADIO_ON", 1280, 24, 28, 28));
                SharedUiStyles.Bind(button, SharedControlKind.Card);
                foreach (var n in new[] { "SharedFocus", "SharedSelection", "SharedDisabled" }) { root.transform.Find(n).gameObject.SetActive(true); Ignore(root.transform.Find(n)); }
                root.transform.Find("SelectionMark").SetAsLastSibling(); PrefabUtility.SaveAsPrefabAsset(root, path);
                var interaction = Picture(r, "InteractionOutline", "UI_CARD_SELECTED_OUTLINE", 0, 0, 1332, 212);
                interaction.rectTransform.anchorMin = Vector2.zero; interaction.rectTransform.anchorMax = Vector2.one; interaction.rectTransform.offsetMin = interaction.rectTransform.offsetMax = Vector2.zero;
                Set(card, "interactionOutline", interaction); interaction.enabled = false; PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
