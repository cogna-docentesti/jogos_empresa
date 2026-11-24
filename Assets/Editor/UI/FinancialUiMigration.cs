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
        private static void Retire(Transform root, Transform keep = null)
        {
            var layout = root.GetComponent<UnityEngine.UI.LayoutGroup>(); if (layout != null) layout.enabled = false;
            var fitter = root.GetComponent<UnityEngine.UI.ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
            foreach (Transform child in root)
            {
                if (child == keep) continue;
                child.gameObject.SetActive(false);
                foreach (var graphic in child.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget = false;
            }
        }
        private static UnityEngine.UI.Button Button(Transform p, string n, string label, float x, float y, float w, float h)
        {
            var r = Node(p, n, x, y, w, h); var b = Ensure<UnityEngine.UI.Button>(r); b.targetGraphic = Ensure<UnityEngine.UI.Image>(r);
            var t = Text(r, "Label", label, 16, 12, w - 32, h - 24, GameTypography.Tokens.Button, true); t.alignment = TextAlignmentOptions.Center;
            SharedUiStyles.Bind(b, SharedControlKind.Primary); return b;
        }
        internal static void ApplyBankLayout(Transform panel)
        {
            const int cardWidth = 296;
            const int cardHeight = 624;
            const int gap = 16;

            var oldScroll = panel.Find("CreditOptionsScroll");
            var options = panel.Find("CreditOptions");
            if (options == null && oldScroll != null)
            {
                options = oldScroll.Find("Viewport/CreditOptions");
                if (options != null) options.SetParent(panel, false);
            }
            if (oldScroll != null) UnityEngine.Object.DestroyImmediate(oldScroll.gameObject);
            foreach (var name in new[] { "CreditHelp", "CreditTooltip" })
            {
                var obsolete = panel.Find(name);
                if (obsolete != null) UnityEngine.Object.DestroyImmediate(obsolete.gameObject);
            }

            var cards = Node(panel, "CreditOptions", 664, 208, 4 * cardWidth + 3 * gap, cardHeight);
            foreach (var obsolete in cards.GetComponents<UnityEngine.UI.LayoutGroup>())
                if (obsolete is not UnityEngine.UI.GridLayoutGroup) UnityEngine.Object.DestroyImmediate(obsolete);
            var fitter = cards.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (fitter != null) UnityEngine.Object.DestroyImmediate(fitter);
            var grid = Ensure<UnityEngine.UI.GridLayoutGroup>(cards);
            grid.enabled = true;
            grid.padding = new RectOffset();
            grid.spacing = new Vector2(gap, 0);
            grid.cellSize = new Vector2(cardWidth, cardHeight);
            grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            var view = panel.GetComponent<FinancialScreenView>();
            Set(view, "bankCardsContainer", cards);
            var detail = Picture(panel, "CreditDetails", "UI_PANEL_IVORY", 664, 852, 1232, 116);
            Set(view, "hintText", Text(detail.transform, "Availability", "Nenhuma opção selecionada.", 20, 8, 1192, 40, GameTypography.Tokens.BodyStrong, true));
            Set(view, "summaryText", Text(detail.transform, "Summary", "", 20, 48, 1192, 64, GameTypography.Tokens.Body));
            UpdateCard(cardWidth, cardHeight);
            Set(view, "bankCardPrefab", AssetDatabase.LoadAssetAtPath<BankCardView>("Assets/Prefabs/Card_loan.prefab"));
        }

        [MenuItem("Tools/Jogos de Empresa/UI-09/Migrate financial screen")]
        public static void Apply()
        {
            if (UnityEngine.Application.dataPath != "D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if (scene.path != "Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform; var panel = canvas.Find("Panel_Financial") as RectTransform;
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "UI-09 financial composition");
            Retire(panel, panel.Find("ContextualHelp"));
            panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.GetComponent<Game.Adapter.In.UI.Layout.SafeAreaFitter>()?.Recapture();
            var bg = Ensure<UnityEngine.UI.Image>(panel); bg.sprite = null; bg.color = GamePalette.Surface; bg.raycastTarget = true;
            var view = panel.GetComponent<FinancialScreenView>();
            var header = Picture(panel, "Header", "UI_TOPBAR_VIOLET", 0, 0, 1920, 88); Retire(header.transform);
            Text(header.transform, "Brand", "Jogos de Empresa", 24, 18, 700, 60, GameTypography.Tokens.BrandTitle, true, GamePalette.OnDark);
            Set(view, "stepText", Text(header.transform, "Context", "BANCO E CAPITAL", 1040, 28, 856, 40, GameTypography.Tokens.Body, false, GamePalette.OnDark));
            Set(view, "titleText", Text(panel, "TitleText", "Banco CogCred", 24, 112, 740, 64, GameTypography.Tokens.ScreenTitle, true));
            Text(panel, "CapitalLabel", "Capital inicial", 1000, 112, 400, 32, GameTypography.Tokens.Label);
            Set(view, "capitalValueText", Text(panel, "CapitalValue", "Indisponível", 1000, 148, 400, 48, GameTypography.Tokens.SectionTitle, true));
            Text(panel, "CashLabel", "Caixa disponível", 1456, 112, 440, 32, GameTypography.Tokens.Label);
            Set(view, "cashValueText", Text(panel, "CashValue", "Indisponível", 1456, 148, 440, 48, GameTypography.Tokens.SectionTitle, true));
            Picture(panel, "BankEnvironment", "BANK_ENVIRONMENT", 24, 208, 480, 752);
            Picture(panel, "BankAdvisor", "CH01_BANK", 54, 240, 420, 720);
            ApplyBankLayout(panel);
            var footer = Picture(panel, "Footer", "UI_NAVBAR_PLUM", 0, 984, 1920, 96); Retire(footer.transform);
            Set(view, "footerHintText", Text(footer.transform, "Help", "", 328, 22, 504, 56, GameTypography.Tokens.Label, false, GamePalette.OnDark));
            var confirm = Button(footer.transform, "ConfirmCredit", "Confirmar crédito", 1120, 14, 480, 68); Set(view, "confirmButton", confirm); Set(view, "confirmLabel", confirm.transform.Find("Label").GetComponent<TMP_Text>());
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
            Font07Migration.Apply(new[] { panel.gameObject }, "Assets/Scenes/GameScene.unity");
            EditorUtility.SetDirty(view); panel.gameObject.SetActive(false); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        private static void UpdateCard(int width, int height)
        {
            const string path = "Assets/Prefabs/Card_loan.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rect = (RectTransform)root.transform;
                Retire(rect);
                rect.sizeDelta = new Vector2(width, height);
                var layout = root.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layout != null) UnityEngine.Object.DestroyImmediate(layout);
                var mark = rect.Find("SelectionMark");
                if (mark != null) UnityEngine.Object.DestroyImmediate(mark.gameObject);

                var card = root.GetComponent<BankCardView>();
                var button = Ensure<UnityEngine.UI.Button>(rect);
                Set(card, "selectButton", button);
                Set(card, "creditLineIcon", Picture(rect, "CreditArtwork", "CREDIT_SEM_CREDITO", 67, 22, 162, 138));

                const int inset = 16;
                int textWidth = width - 2 * inset;
                var name = Text(rect, "Name", "Linha de crédito", inset, 177, textWidth, 74,
                    new TypographyToken(TypographyRole.SectionTitle, 28), true);
                Set(card, "nameText", name);
                var principal = Text(rect, "Principal", "Capital próprio", inset, 256, textWidth, 44,
                    new TypographyToken(TypographyRole.Kpi, 27), true);
                principal.alignment = TextAlignmentOptions.MidlineRight;
                Set(card, "maxAmountText", principal);
                Set(card, "interestRateText", Text(rect, "Interest", "Sem juros", inset, 313, textWidth, 34, GameTypography.Tokens.Table));
                Set(card, "termText", Text(rect, "Term", "Sem parcelas", inset, 350, textWidth, 34, GameTypography.Tokens.Table));
                Set(card, "installmentText", Text(rect, "Installment", "", inset, 389, textWidth, 68, GameTypography.Tokens.Table, true));
                var description = Text(rect, "Description", "Descrição da linha.", inset, 467, textWidth, 92, GameTypography.Tokens.Body);
                Set(card, "descriptionText", description);
                Set(card, "availabilityText", Text(rect, "Availability", "Disponibilidade", inset, 565, textWidth, 50, GameTypography.Tokens.Caption));

                SharedUiStyles.Bind(button, SharedControlKind.Card);
                Font07Migration.Apply(new[] { root }, path);
                name.overflowMode = description.overflowMode = TextOverflowModes.Ellipsis;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
