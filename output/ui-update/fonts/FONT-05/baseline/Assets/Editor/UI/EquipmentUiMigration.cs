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
    public static class EquipmentUiMigration
    {
        private static Sprite Art(string id)
        {
            var shared = SharedUiSkin.Current.Get(id); if (shared != null) return shared;
            var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("docs/art/manifest.json"));
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/" + (string)manifest["assets"].Single(a => (string)a["id"] == id)["path"]);
        }
        private static T Ensure<T>(Component c) where T : Component
        { var found = c.GetComponent<T>(); return found != null ? found : c.gameObject.AddComponent<T>(); }
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
        private static TextMeshProUGUI Text(Transform p, string n, string value, float x, float y, float w, float h, TypographyToken size, bool bold = false, Color? color = null)
        {
            var t = Ensure<TextMeshProUGUI>(Node(p, n, x, y, w, h));
            GameTypography.Apply(t, size, bold);
            t.text = value; t.color = color ?? GamePalette.Ink; t.alignment = TextAlignmentOptions.TopLeft; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false; return t;
        }
        private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Retire(Transform root)
        {
            var layout = root.GetComponent<UnityEngine.UI.LayoutGroup>(); if (layout != null) layout.enabled = false;
            var fitter = root.GetComponent<UnityEngine.UI.ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
            foreach (Transform child in root) { child.gameObject.SetActive(false); foreach (var g in child.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.raycastTarget = false; }
        }
        private static UnityEngine.UI.Button Button(Transform p, string n, string label, float x, float y, float w, float h, SharedControlKind kind = SharedControlKind.Primary)
        {
            var r = Node(p, n, x, y, w, h); Retire(r); var b = Ensure<UnityEngine.UI.Button>(r); b.targetGraphic = Ensure<UnityEngine.UI.Image>(r);
            var t = Text(r, "Label", label, 12, 8, w - 24, h - 16, GameTypography.Tokens.Button, true); t.alignment = TextAlignmentOptions.Center;
            SharedUiStyles.Bind(b, kind); r.Find("SharedFocus").gameObject.SetActive(true); return b;
        }
        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static UnityEngine.UI.ScrollRect Scroll(Transform p, string name, float x, float y, float w, float h, bool grid = false)
        {
            var root = Node(p, name, x, y, w, h); var scroll = Ensure<UnityEngine.UI.ScrollRect>(root);
            scroll.vertical = true; scroll.horizontal = false; scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 48;
            var vp = Picture(root, "Viewport", null, 0, 0, w - 24, h, true); Ensure<UnityEngine.UI.Mask>(vp).showMaskGraphic = false;
            var content = Node(vp.transform, "Content", 0, 0, w - 24, 0); content.anchorMax = new Vector2(1, 1); content.sizeDelta = Vector2.zero;
            if (grid)
            {
                var layout = Ensure<UnityEngine.UI.GridLayoutGroup>(content); layout.enabled = true; layout.cellSize = new Vector2(648, 360); layout.spacing = new Vector2(16, 16);
                layout.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; layout.constraintCount = 2; layout.padding = new RectOffset(6, 6, 6, 6);
            }
            else
            {
                var layout = Ensure<UnityEngine.UI.VerticalLayoutGroup>(content); layout.enabled = true; layout.padding = new RectOffset(6, 6, 6, 6); layout.spacing = 12;
                layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            }
            Ensure<UnityEngine.UI.ContentSizeFitter>(content).verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = vp.rectTransform; scroll.content = content;
            var track = Picture(root, "Scrollbar", "UI_SCROLLBAR_TRACK", w - 12, 0, 12, h); var area = Node(track.transform, "SlidingArea", 0, 0, 12, h);
            var handle = Picture(area, "Handle", "UI_SCROLLBAR_THUMB", 0, 0, 12, 80, true); Stretch(handle.rectTransform);
            var bar = Ensure<UnityEngine.UI.Scrollbar>(track); bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            bar.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None }; scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }
        private static void Empty(Transform p, string n, float x, float y, float w, float h, string message)
        {
            var r = Node(p, n, x, y, w, h); Picture(r, "Artwork", "EMPTY_CART", (w - 360) / 2, 0, 360, 240);
            var label = Text(r, "Message", message, 16, 248, w - 32, 72, GameTypography.Tokens.Body); label.alignment = TextAlignmentOptions.Center;
        }
        private static void Body(RectTransform body, EquipmentScreenView view)
        {
            Retire(body); var bg = body.GetComponent<UnityEngine.UI.Image>(); if (bg != null) bg.color = Color.clear;
            var filters = new[] { "Todos", "Básicos", "Específicos", "Adquiridos" };
            var buttons = new UnityEngine.UI.Button[4];
            for (int i = 0; i < 4; i++) buttons[i] = Button(body, "Filter" + i, filters[i], i * 230, 0, 214, 60, SharedControlKind.Pill);
            var result = Text(body, "ResultCount", "0 equipamentos", 968, 14, 336, 36, GameTypography.Tokens.Label); result.alignment = TextAlignmentOptions.Right;
            var catalog = Scroll(body, "CatalogScroll", 0, 88, 1352, 664, true);
            var empty = Text(body, "CatalogEmpty", "Nenhum equipamento neste filtro.\nEscolha outra categoria para continuar.", 48, 240, 1232, 104, GameTypography.Tokens.Body); empty.alignment = TextAlignmentOptions.Center;
            var summary = Picture(body, "Summary", "UI_PANEL_IVORY", 1376, 0, 496, 752);
            Text(summary.transform, "Title", "Seu carrinho", 24, 24, 448, 44, GameTypography.Tokens.SectionTitle, true);
            Text(summary.transform, "CashLabel", "Caixa disponível", 24, 84, 448, 32, GameTypography.Tokens.Label);
            var cash = Text(summary.transform, "Cash", "Indisponível", 24, 120, 448, 52, GameTypography.Tokens.DisplayValue, true);
            var items = Scroll(summary.transform, "SummaryScroll", 24, 192, 448, 284);
            var text = Text(items.content, "Items", "", 0, 0, 412, 284, GameTypography.Tokens.Body);
            var oldHeight = text.GetComponent<UnityEngine.UI.LayoutElement>(); if (oldHeight != null) UnityEngine.Object.DestroyImmediate(oldHeight);
            var oldFitter = text.GetComponent<UnityEngine.UI.ContentSizeFitter>(); if (oldFitter != null) UnityEngine.Object.DestroyImmediate(oldFitter);
            Empty(summary.transform, "Empty", 24, 180, 448, 320, "Seu carrinho está vazio.\nAdicione itens no catálogo.");
            Text(summary.transform, "TotalLabel", "Total da compra", 24, 508, 448, 30, GameTypography.Tokens.Label);
            var total = Text(summary.transform, "Total", "R$ 0,00", 24, 544, 448, 44, GameTypography.Tokens.SectionTitle, true);
            Text(summary.transform, "RemainingLabel", "Caixa após a compra", 24, 600, 272, 32, GameTypography.Tokens.Label);
            var remaining = Text(summary.transform, "Remaining", "R$ 0,00", 24, 632, 448, 34, GameTypography.Tokens.BodyStrong,true); remaining.alignment = TextAlignmentOptions.Right;
            var cart = Button(summary.transform, "ReviewCart", "Revisar carrinho (0)", 24, 676, 448, 60);
            if (view == null) return;
            Set(view, "cardContainer", catalog.content); Set(view, "catalogScroll", catalog); Set(view, "resultText", result); Set(view, "catalogEmpty", empty.gameObject);
            Set(view, "availableCashText", cash); Set(view, "cartTotalValueText", total); Set(view, "remainingCashValueText", remaining);
            Set(view, "summaryItemsText", text); Set(view, "summaryEmpty", summary.transform.Find("Empty").gameObject); Set(view, "cartButton", cart);
            var so = new SerializedObject(view); var a = so.FindProperty("filterButtons"); a.arraySize = 4; for (int i = 0; i < 4; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i]; so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void Modal(EquipmentScreenView view)
        {
            var scrim = Picture(view.transform, "CartModal", "UI_SCRIM", 0, 0, 1920, 1080, true); Stretch(scrim.rectTransform); scrim.preserveAspect = false;
            var modalCanvas = Ensure<Canvas>(scrim); modalCanvas.overrideSorting = true; modalCanvas.sortingOrder = 200; Ensure<UnityEngine.UI.GraphicRaycaster>(scrim);
            var panel = Picture(scrim.transform, "CartPanel", "UI_MODAL_SURFACE", 240, 112, 1440, 856, true);
            Text(panel.transform, "Title", "Revise seu carrinho", 32, 24, 1100, 64, GameTypography.Tokens.ScreenTitle, true);
            var close = Button(panel.transform, "Close", "Fechar", 1200, 24, 208, 68, SharedControlKind.Secondary);
            var items = Scroll(panel.transform, "ItemsScroll", 24, 112, 912, 696);
            Empty(panel.transform, "Empty", 120, 264, 712, 324, "Nenhum item no carrinho.\nVolte ao catálogo para escolher.");
            var detail = Picture(panel.transform, "Totals", "UI_PANEL_IVORY", 960, 112, 456, 696);
            Text(detail.transform, "CashLabel", "Caixa disponível", 24, 24, 408, 32, GameTypography.Tokens.Label);
            Set(view, "cartCashText", Text(detail.transform, "Cash", "", 24, 64, 408, 52, GameTypography.Tokens.SectionTitle, true));
            Text(detail.transform, "TotalLabel", "Total da compra", 24, 160, 408, 32, GameTypography.Tokens.Label);
            Set(view, "cartTotalText", Text(detail.transform, "Total", "", 24, 200, 408, 52, GameTypography.Tokens.SectionTitle, true));
            Text(detail.transform, "RemainingLabel", "Caixa após a compra", 24, 296, 408, 32, GameTypography.Tokens.Label);
            Set(view, "cartRemainingText", Text(detail.transform, "Remaining", "", 24, 336, 408, 52, GameTypography.Tokens.SectionTitle, true));
            Set(view, "cartStatusText", Text(detail.transform, "Status", "", 24, 440, 408, 120, GameTypography.Tokens.Body));
            Set(view, "checkoutButton", Button(detail.transform, "Checkout", "Concluir compra", 24, 596, 408, 68));
            Set(view, "closeCartButton", close); Set(view, "cartModal", scrim.gameObject); Set(view, "cartEmpty", panel.transform.Find("Empty").gameObject); Set(view, "cartLines", items.content);
            Set(view, "cartLinePrefab", AssetDatabase.LoadAssetAtPath<EquipmentCartLineView>("Assets/Prefabs/EquipmentCartLine.prefab"));
            Ensure<SharedModalFocus>(scrim); scrim.gameObject.SetActive(false);
        }
        private static void Panel(RectTransform panel)
        {
            Retire(panel); Stretch(panel); panel.GetComponent<Game.Adapter.In.UI.Layout.SafeAreaFitter>()?.Recapture();
            var bg = Ensure<UnityEngine.UI.Image>(panel); bg.sprite = null; bg.color = GamePalette.Surface; bg.raycastTarget = true;
            var view = panel.GetComponent<EquipmentScreenView>();
            var header = Picture(panel, "Header", "UI_TOPBAR_VIOLET", 0, 0, 1920, 88); Retire(header.transform);
            Text(header.transform, "Brand", "Jogos de Empresa", 24, 18, 800, 60, GameTypography.Tokens.BrandTitle, true, GamePalette.OnDark);
            Set(view, "stepText", Text(header.transform, "Context", "PASSO 6 DE 7  •  EQUIPAMENTOS", 1040, 28, 856, 40, GameTypography.Tokens.Body, false, GamePalette.OnDark));
            Text(panel, "TitleText", "Loja de equipamentos", 24, 112, 1000, 56, GameTypography.Tokens.ScreenTitle, true);
            Set(view, "contextText", Text(panel, "ContextText", "Selecione os equipamentos do seu restaurante.", 24, 172, 1000, 32, GameTypography.Tokens.Body));
            var body = Node(panel, "MainArea_Equipment", 24, 216, 1872, 752); Body(body, view);
            var footer = Picture(panel, "BottomBar", "UI_NAVBAR_PLUM", 0, 984, 1920, 96); Retire(footer.transform);
            Set(view, "hintText", Text(panel, "Guidance", "Adicione equipamentos ao carrinho.", 1056, 116, 840, 88, GameTypography.Tokens.Body));
            Set(view, "footerText", Text(footer.transform, "Help", "Revise o carrinho antes de continuar.", 328, 20, 328, 64, GameTypography.Tokens.Caption, false, GamePalette.OnDark));
            Set(view, "bankBackButton", Button(footer.transform, "ReturnToBank", "Voltar ao banco", 328, 14, 320, 68, SharedControlKind.Secondary));
            // Setup back occupies Help; hub history occupies the global back position.
            footer.transform.Find("Help").gameObject.SetActive(false);
            var confirm = Button(footer.transform, "ConfirmButton", "Continuar para equipe", 1216, 14, 384, 68);
            Set(view, "confirmButton", confirm); Set(view, "confirmLabel", confirm.transform.Find("Label").GetComponent<TMP_Text>());
            Set(view, "cardPrefab", AssetDatabase.LoadAssetAtPath<EquipmentCardView>("Assets/Prefabs/Card_equipment.prefab"));
            Modal(view); EditorUtility.SetDirty(view);
        }
        private static void Card()
        {
            const string path = "Assets/Prefabs/Card_equipment.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Retire(root.transform); var r = (RectTransform)root.transform; r.sizeDelta = new Vector2(648, 360); var card = root.GetComponent<EquipmentCardView>();
                Set(card, "equipmentImage", Picture(r, "EquipmentArtwork", "EQUIPMENT_SISTEMA_PDV_COMPUTADOR", 24, 24, 250, 250));
                Set(card, "nameText", Text(r, "EquipmentName", "Nome do equipamento", 286, 24, 338, 72, GameTypography.Tokens.BodyStrong,true));
                Set(card, "priceText", Text(r, "EquipmentPrice", "R$ 0,00", 286, 108, 338, 40, GameTypography.Tokens.SectionTitle, true));
                Set(card, "descriptionText", Text(r, "Description", "Para todos os restaurantes", 286, 156, 338, 56, GameTypography.Tokens.Body));
                var chip = Picture(r, "CategoryChip", "UI_PILL_NORMAL", 24, 280, 238, 32);
                var label = Text(chip.transform, "Label", "BÁSICO", 8, 0, 222, 32, GameTypography.Tokens.Caption, true); label.alignment = TextAlignmentOptions.Center; Set(card, "categoryText", label);
                Set(card, "statusText", Text(r, "Status", "Disponível", 24, 320, 250, 32, GameTypography.Tokens.Caption, true));
                Set(card, "removeButton", Button(r, "Remove", "−", 286, 228, 68, 68, SharedControlKind.Secondary));
                var plus = Button(r, "Add", "+", 556, 228, 68, 68); Set(card, "buyButton", plus); Set(card, "buyButtonText", plus.transform.Find("Label").GetComponent<TMP_Text>());
                var quantity = Text(r, "Quantity", "0", 366, 228, 178, 68, GameTypography.Tokens.BodyStrong,true); quantity.alignment = TextAlignmentOptions.Center; Set(card, "quantityText", quantity);
                var b = Ensure<UnityEngine.UI.Button>(r); SharedUiStyles.Bind(b, SharedControlKind.Card);
                foreach (var n in new[] { "SharedFocus", "SharedSelection", "SharedDisabled" }) { var t = root.transform.Find(n); t.gameObject.SetActive(true); Ensure<UnityEngine.UI.LayoutElement>(t).ignoreLayout = true; }
                var outline = Picture(r, "InteractionOutline", "UI_CARD_SELECTED_OUTLINE", 0, 0, 648, 360); Stretch(outline.rectTransform); Set(card, "interactionOutline", outline); outline.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var line = new GameObject("EquipmentCartLine", typeof(RectTransform));
            try
            {
                var r = (RectTransform)line.transform; r.sizeDelta = new Vector2(876, 144); var view = line.AddComponent<EquipmentCartLineView>();
                SharedUiStyles.Surface(line, "UI_CARD_NORMAL"); var height = line.AddComponent<UnityEngine.UI.LayoutElement>(); height.preferredHeight = height.minHeight = 144;
                Set(view, "artwork", Picture(r, "Artwork", "EQUIPMENT_SISTEMA_PDV_COMPUTADOR", 16, 16, 112, 112));
                Set(view, "nameText", Text(r, "Name", "Equipamento", 140, 20, 454, 56, GameTypography.Tokens.BodyStrong,true));
                Set(view, "priceText", Text(r, "Price", "R$ 0,00", 140, 84, 454, 40, GameTypography.Tokens.Body));
                Set(view, "removeButton", Button(r, "Remove", "−", 612, 36, 68, 68, SharedControlKind.Secondary));
                var q = Text(r, "Quantity", "1", 692, 36, 68, 68, GameTypography.Tokens.BodyStrong,true); q.alignment = TextAlignmentOptions.Center;
                var plus = Button(r, "Maximum", "+", 772, 36, 68, 68); plus.interactable = false;
                PrefabUtility.SaveAsPrefabAsset(line, "Assets/Prefabs/EquipmentCartLine.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(line); }
        }
        [MenuItem("Tools/Jogos de Empresa/UI-10/Migrate equipment store")]
        public static void Apply()
        {
            if (UnityEngine.Application.dataPath != "D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if (scene.path != "Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            Card();
            foreach (var equipment in Resources.LoadAll<EquipmentData>("Equipments")) { equipment.icon = Art("EQUIPMENT_" + equipment.id.ToUpperInvariant()); EditorUtility.SetDirty(equipment); }
            var prefab = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Panel_Equipment.prefab");
            try { Panel((RectTransform)prefab.transform); PrefabUtility.SaveAsPrefabAsset(prefab, "Assets/Prefabs/Panel_Equipment.prefab"); } finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var body = PrefabUtility.LoadPrefabContents("Assets/Prefabs/MainArea_Equipment.prefab");
            try { ((RectTransform)body.transform).sizeDelta = new Vector2(1872, 752); Body((RectTransform)body.transform, null); PrefabUtility.SaveAsPrefabAsset(body, "Assets/Prefabs/MainArea_Equipment.prefab"); } finally { PrefabUtility.UnloadPrefabContents(body); }
            var panel = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform.Find("Panel_Equipment") as RectTransform;
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject, "UI-10 equipment store"); Panel(panel); panel.gameObject.SetActive(false);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
