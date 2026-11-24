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
    /// <summary>UI-06 keeps the D2 controller, selectable instances and panel registry references.</summary>
    public static class RestaurantUiMigration
    {
        private const float CardWidth = 608;
        private const float CardPadding = 24;
        private const float DetailWidth = CardWidth - 2 * CardPadding;

        private static T Ensure<T>(Component target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.gameObject.AddComponent<T>();
        }
        private static RectTransform Node(Transform p,string name,float x,float y,float w,float h)
        {
            var r=p.Find(name) as RectTransform;
            if(r==null) { r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform; r.SetParent(p,false); }
            Place(r, x, y, w, h);
            r.gameObject.SetActive(true);
            return r;
        }
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }
        private static UnityEngine.UI.Image Image(Transform p,string name,string id,float x,float y,float w,float h,bool hit=false)
        {
            var image=Ensure<UnityEngine.UI.Image>(Node(p,name,x,y,w,h));
            image.sprite=id==null?null:SharedUiSkin.Current.Get(id); image.color=Color.white; image.raycastTarget=hit;
            image.type=image.sprite!=null && image.sprite.border!=Vector4.zero?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect=image.type==UnityEngine.UI.Image.Type.Simple && id!=null; return image;
        }
        private static TextMeshProUGUI Text(Transform p,string name,string value,float x,float y,float w,float h,TypographyToken size,bool bold=false,Color? color=null)
        {
            var t=Ensure<TextMeshProUGUI>(Node(p,name,x,y,w,h));
            GameTypography.Apply(t,size,bold);
            t.text=value; t.color=color??GamePalette.Ink; t.alignment=TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode=TextWrappingModes.Normal; t.overflowMode=TextOverflowModes.Overflow; return t;
        }
        private static void LayoutCardDetails(RectTransform card, RestaurantData data)
        {
            var details = Node(card, "CardDetails", CardPadding, CardPadding, DetailWidth, card.rect.height - 2 * CardPadding);
            var oldScroll = details.GetComponent<UnityEngine.UI.ScrollRect>();
            if (oldScroll != null)
            {
                // Keep the existing text objects while removing the viewport and scrollbar.
                var content = oldScroll.content;
                content.SetParent(details, false);
                content.name = "Copy";
                UnityEngine.Object.DestroyImmediate(oldScroll);
                UnityEngine.Object.DestroyImmediate(details.Find("Viewport").gameObject);
                UnityEngine.Object.DestroyImmediate(details.Find("Scrollbar").gameObject);
            }

            Stack(details, 12);
            var artwork = Node(details, "Artwork", 0, 0, DetailWidth, 420);
            artwork.SetAsFirstSibling();
            var artLayout = Ensure<UnityEngine.UI.LayoutElement>(artwork);
            // Give the measured copy its preferred height before expanding the facade.
            artLayout.minHeight = artLayout.preferredHeight = 96;
            artLayout.flexibleHeight = 1;
            MoveChild(card, artwork, "Facade");
            var facade = Image(artwork, "Facade", null, 0, 0, DetailWidth, 420);
            facade.sprite = data.facade;
            facade.preserveAspect = true;
            facade.rectTransform.pivot = new Vector2(.5f, .5f);
            Stretch(facade.rectTransform);
            MoveChild(card, artwork, "FeaturedDish");
            var featuredDish = artwork.Find("FeaturedDish");
            if (featuredDish != null) UnityEngine.Object.DestroyImmediate(featuredDish.gameObject);

            var copy = Node(details, "Copy", 0, 0, DetailWidth, 0);
            Stack(copy, 4);
            var fitter = copy.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (fitter != null) UnityEngine.Object.DestroyImmediate(fitter);

            var heading = Node(copy, "Heading", 0, 0, DetailWidth, 40);
            heading.SetAsFirstSibling();
            var headingLayout = Ensure<UnityEngine.UI.HorizontalLayoutGroup>(heading);
            headingLayout.spacing = 16;
            headingLayout.childControlWidth = headingLayout.childControlHeight = true;
            headingLayout.childForceExpandWidth = headingLayout.childForceExpandHeight = false;
            MoveChild(copy, heading, "RestaurantName");
            var title = Text(heading, "RestaurantName", data.displayName, 0, 0, DetailWidth, 40,
                GameTypography.Tokens.SectionTitle, true);
            var titleLayout = Ensure<UnityEngine.UI.LayoutElement>(title);
            titleLayout.minHeight = titleLayout.preferredHeight = -1;
            titleLayout.flexibleWidth = 1;
            title.raycastTarget = false;
            var description = Text(copy, "RestaurantDesc", data.description, 0, 0, DetailWidth, 32,
                GameTypography.Tokens.Body);
            var oldHeight = description.GetComponent<UnityEngine.UI.LayoutElement>();
            if (oldHeight != null) UnityEngine.Object.DestroyImmediate(oldHeight);
            description.raycastTarget = false;

            var indicator = Node(heading, "Indicator", DetailWidth - 32, 0, 32, 32);
            var indicatorLayout = Ensure<UnityEngine.UI.LayoutElement>(indicator);
            indicatorLayout.minWidth = indicatorLayout.minHeight = 32;
            MoveChild(card, indicator, "SelectedCheck");
            var check = indicator.Find("SelectedCheck") as RectTransform;
            if (check == null)
            {
                check = Image(indicator, "SelectedCheck", "ICON_CHECK", 0, 0, 32, 32).rectTransform;
                check.gameObject.SetActive(false);
            }
            Stretch(check);

            var metrics = Node(details, "Metrics", 0, 0, DetailWidth, 64);
            Ensure<UnityEngine.UI.LayoutElement>(metrics).minHeight = 64;
            foreach (var name in new[] { "CostLabel", "MonthlyCost", "TicketLabel", "TicketRange" })
            {
                MoveChild(copy, metrics, name);
                MoveChild(details, metrics, name);
            }
            MetricRow(metrics, 0, "CostLabel", "Custo fixo / mês", "MonthlyCost");
            MetricRow(metrics, 34, "TicketLabel", "Ticket de referência", "TicketRange");
            LayoutMenu(details, copy.GetSiblingIndex() + 1, data.products);
        }

        private static void LayoutMenu(RectTransform details, int siblingIndex, ProductData[] products)
        {
            const float height = 208;
            const float padding = 12;
            const float gap = 8;
            const float rowWidth = DetailWidth - 2 * padding;
            const float artHeight = 98;
            const float nameHeight = 64;
            const float rowHeight = artHeight + nameHeight;

            var menu = Image(details, "MenuPreview", "UI_PANEL_IVORY", 0, 0, DetailWidth, height).rectTransform;
            menu.SetSiblingIndex(siblingIndex);
            var layout = Ensure<UnityEngine.UI.LayoutElement>(menu);
            layout.minHeight = layout.preferredHeight = height;
            Text(menu, "MenuTitle", "Cardápio", padding, 6, rowWidth, 34,
                new TypographyToken(TypographyRole.SectionTitle, 26));

            var row = Node(menu, "Products", padding, 39, rowWidth, rowHeight);
            int count = products?.Length ?? 0;
            for (int i = row.childCount - 1; i >= count; i--)
                UnityEngine.Object.DestroyImmediate(row.GetChild(i).gameObject);

            for (int i = 0; i < count; i++)
            {
                float width = (rowWidth - (count - 1) * gap) / count;
                var product = products[i];
                var slot = Node(row, $"Product{i + 1}", i * (width + gap), 0, width, rowHeight);
                var thumbnail = Image(slot, "Artwork", null, 0, 0, width, artHeight);
                var thumbnailRect = thumbnail.rectTransform;
                thumbnailRect.anchorMin = thumbnailRect.anchorMax = new Vector2(.5f, 1);
                thumbnailRect.pivot = new Vector2(.5f, .5f);
                thumbnailRect.anchoredPosition = new Vector2(0, -artHeight / 2);
                thumbnail.sprite = product != null ? product.productSprite : null;
                thumbnail.preserveAspect = true;
                thumbnail.enabled = thumbnail.sprite != null;

                var label = Text(slot, "Name", product != null ? product.displayName : string.Empty, 0, artHeight, width, nameHeight,
                    new TypographyToken(TypographyRole.Label, 24));
                label.alignment = TextAlignmentOptions.Top;
                slot.gameObject.SetActive(product != null);
            }
            menu.gameObject.SetActive(products != null && products.Any(product => product != null));
        }

        private static void MoveChild(Transform source, Transform destination, string name)
        {
            var child = source.Find(name);
            if (child != null) child.SetParent(destination, false);
        }

        private static void Stack(RectTransform parent, float spacing)
        {
            var layout = Ensure<UnityEngine.UI.VerticalLayoutGroup>(parent);
            layout.padding = new RectOffset();
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void MetricRow(Transform details, float y, string labelName, string caption, string valueName)
        {
            Text(details, labelName, caption, 0, y, 246, 30, GameTypography.Tokens.Label).raycastTarget = false;
            var value = details.Find(valueName)?.GetComponent<TMP_Text>();
            var metric = Text(details, valueName, value != null ? value.text : "—", 262, y, DetailWidth - 262, 30,
                GameTypography.Tokens.BodyStrong, true);
            metric.alignment = TextAlignmentOptions.MidlineRight;
            metric.raycastTarget = false;
        }

        internal static void ApplyDecisionLayout(Transform panel)
        {
            var view = panel.GetComponent<RestaurantScreenView>();
            var types = new[] { RestaurantType.PODRAO, RestaurantType.JAPONES, RestaurantType.FRANCES };
            var cardFields = new[] { "cardPodrao", "cardJapones", "cardFrances" };
            var restaurants = Resources.LoadAll<RestaurantData>("Restaurants");
            for (int i = 0; i < cardFields.Length; i++)
            {
                var card = (RectTransform)Get(view, cardFields[i]).transform;
                Place(card, CardPadding + i * (CardWidth + CardPadding), 116, CardWidth, 660);
                LayoutCardDetails(card, restaurants.Single(data => data.type == types[i]));
            }

            var audience = Node(panel, "DecisionSidebar", 24, 800, 1872, 160);
            Place((RectTransform)audience.Find("Title"), 24, 8, 650, 40);
            var segmentFields = new[] { "btnLow", "btnMedium", "btnHigh" };
            for (int i = 0; i < segmentFields.Length; i++)
            {
                var button = (RectTransform)Get(view, segmentFields[i]).transform;
                Place(button, 24 + i * 616, 60, 592, 76);
                Place((RectTransform)button.Find("Radio"), 16, 24, 28, 28);
                Place((RectTransform)button.Find("SegmentLabel"), 56, 8, 512, 36);
                var availability = button.Find("Availability").GetComponent<TMP_Text>();
                Place(availability.rectTransform, 56, 44, 512, 26);
                availability.alignment = TextAlignmentOptions.MidlineLeft;
            }
            var trigger = (RectTransform)audience.Find("CompatibilityToggle");
            Place(trigger, 1512, 4, 336, 52);
            Place((RectTransform)trigger.Find("Label"), 12, 0, 312, 52);

            var footer = panel.Find("DecisionFooter");
            var confirm = (RectTransform)Get(view, "confirmButton").transform;
            confirm.SetParent(footer, false);
            confirm.anchorMin = confirm.anchorMax = confirm.pivot = new Vector2(.5f, .5f);
            confirm.anchoredPosition = Vector2.zero;
            confirm.sizeDelta = new Vector2(520, 68);
            confirm.localScale = Vector3.one;
            Place((RectTransform)confirm.Find("Label"), 56, 8, 408, 52);

            var draft = footer.Find("DraftHint").GetComponent<TMP_Text>();
            Place(draft.rectTransform, 1312, 24, 584, 48);
            draft.alignment = TextAlignmentOptions.MidlineRight;
        }
        private static UnityEngine.UI.Button Get(RestaurantScreenView v,string name) => (UnityEngine.UI.Button)new SerializedObject(v).FindProperty(name).objectReferenceValue;
        private static void Set(RestaurantScreenView v,string name,UnityEngine.Object value) { var so=new SerializedObject(v); so.FindProperty(name).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void RetireChildren(UnityEngine.UI.Button b)
        {
            foreach(Transform c in b.transform) if(!c.name.StartsWith("Shared")) { c.gameObject.SetActive(false); foreach(var g in c.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.raycastTarget=false; }
            foreach(var e in b.GetComponents<UnityEngine.UI.Shadow>()) e.enabled=false;
            var layout=b.GetComponent<UnityEngine.UI.LayoutGroup>(); if(layout!=null) layout.enabled=false;
        }
        private static UnityEngine.UI.Button Action(RestaurantScreenView v,string field,Transform p,string name,string label,float x,float w,SharedControlKind kind)
        {
            var b=Get(v,field); b.transform.SetParent(p,false); b.name=name; RetireChildren(b);
            var r=Node(p,name,x,14,w,68); r.gameObject.SetActive(true);
            var image=Ensure<UnityEngine.UI.Image>(r); b.targetGraphic=image;
            Text(r,"Label",label,48,8,w-64,52,GameTypography.Tokens.Button,true).alignment=TextAlignmentOptions.Center;
            Image(r,"ActionIcon",field=="backButton"?"ICON_BACK":"ICON_NEXT",14,18,32,32);
            SharedUiStyles.Bind(b,kind); return b;
        }

        internal static void ApplyCompatibilityLayout(Transform panel)
        {
            var view = panel.GetComponent<RestaurantScreenView>();
            var sidebar = panel.Find("DecisionSidebar");
            var legacy = sidebar.Find("Compatibility");
            if (legacy != null) legacy.gameObject.SetActive(false);

            var trigger = Node(sidebar, "CompatibilityToggle", 1512, 4, 336, 52);
            var lateralToggle = trigger.GetComponent<ConceptDetailsToggle>();
            if (lateralToggle != null) UnityEngine.Object.DestroyImmediate(lateralToggle);
            var open = Ensure<UnityEngine.UI.Button>(trigger);
            open.targetGraphic = Ensure<UnityEngine.UI.Image>(trigger);
            Text(trigger, "Label", "Ver compatibilidade", 12, 0, trigger.rect.width - 24, trigger.rect.height,
                new TypographyToken(TypographyRole.Button, 22), true).alignment = TextAlignmentOptions.Center;
            SharedUiStyles.Bind(open, SharedControlKind.Secondary);

            var root = Node(panel, "CompatibilityModal", 0, 0, 1920, 1080);
            root.SetAsLastSibling();
            Stretch(root);
            var canvas = Ensure<Canvas>(root);
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1001;
            Ensure<UnityEngine.UI.GraphicRaycaster>(root);
            var focus = Ensure<SharedModalFocus>(root);
            var backdrop = Image(root, "Backdrop", null, 0, 0, 1920, 1080, true);
            backdrop.color = new Color(0f, 0f, 0f, .80f);
            backdrop.transform.SetAsFirstSibling();
            Stretch(backdrop.rectTransform);

            var dialog = Image(root, "Dialog", "UI_MODAL_SURFACE", 0, 0, 1080, 552, true).rectTransform;
            dialog.anchorMin = dialog.anchorMax = dialog.pivot = new Vector2(.5f, .5f);
            dialog.anchoredPosition = Vector2.zero;
            Text(dialog, "Title", "Compatibilidade", 48, 26, 888, 64,
                GameTypography.Tokens.ScreenTitle, true).raycastTarget = false;

            var restaurantTypes = new[] { RestaurantType.PODRAO, RestaurantType.JAPONES, RestaurantType.FRANCES };
            var restaurants = Resources.LoadAll<RestaurantData>("Restaurants");
            var tabs = new UnityEngine.UI.Button[restaurantTypes.Length];
            for (int i = 0; i < restaurantTypes.Length; i++)
            {
                var type = restaurantTypes[i];
                var data = restaurants.FirstOrDefault(r => r.type == type);
                var tab = Node(dialog, type.ToString(), 48 + i * 336, 108, 312, 132);
                tabs[i] = Ensure<UnityEngine.UI.Button>(tab);
                tabs[i].targetGraphic = Ensure<UnityEngine.UI.Image>(tab);
                var facade = Image(tab, "Facade", null, 14, 18, 128, 96);
                facade.sprite = data?.facade;
                facade.preserveAspect = true;
                facade.enabled = facade.sprite != null;
                Text(tab, "Label", data != null ? data.displayName : "Indisponível", 150, 24, 142, 84,
                    new TypographyToken(TypographyRole.Navigation, 28), true);
                SharedUiStyles.Bind(tabs[i], SharedControlKind.Tab);
            }

            CompatibilitySection(view, dialog, "Location", "Localização", 48, "coh1");
            CompatibilitySection(view, dialog, "Audience", "Público", 552, "coh2");
            var closeRoot = Node(dialog, "Fechar compatibilidade", 964, 24, 68, 68);
            var close = Ensure<UnityEngine.UI.Button>(closeRoot);
            close.targetGraphic = Ensure<UnityEngine.UI.Image>(closeRoot);
            Image(closeRoot, "Icon", "ICON_CLOSE", 17, 17, 34, 34);
            SharedUiStyles.Bind(close, SharedControlKind.Icon);
            focus.Configure(tabs[0], close);

            Set(view, "compatibilityModal", focus);
            Set(view, "compatibilityButton", open);
            Set(view, "compatibilityCloseButton", close);
            var serializedView = new SerializedObject(view);
            var tabRefs = serializedView.FindProperty("compatibilityTabs");
            tabRefs.arraySize = tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
                tabRefs.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void CompatibilitySection(RestaurantScreenView view, Transform dialog,
            string name, string title, float x, string field)
        {
            var section = Image(dialog, name, "UI_PANEL_IVORY", x, 268, 480, 236).rectTransform;
            Text(section, "Title", title, 24, 18, 432, 48, GameTypography.Tokens.SectionTitle, true).raycastTarget = false;
            var tip = Text(section, "Context", "", 24, 73, 432, 62, GameTypography.Tokens.Body);
            tip.alignment = TextAlignmentOptions.TopLeft;
            tip.raycastTarget = false;
            Set(view, field + "Tip", tip);
            var value = Text(section, "Score", "Sem seleção", 24, 143, 432, 38, GameTypography.Tokens.BodyStrong, true);
            value.raycastTarget = false;
            Set(view, field + "Value", value);
            var track = Image(section, "Track", null, 24, 192, 432, 12);
            track.color = GamePalette.SurfaceAlt;
            var fill = Image(track.transform, "Fill", null, 0, 0, 432, 12);
            fill.color = GamePalette.Money;
            Set(view, field + "BarFill", fill);
        }

        [MenuItem("Tools/Jogos de Empresa/UI-06/Migrate restaurant in GameScene")]
        public static void Apply()
        {
            if(UnityEngine.Application.dataPath!="D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            var panel=scene.GetRootGameObjects().First(r=>r.name=="Canvas").transform.Find("Panel_Restaurant"); var view=panel.GetComponent<RestaurantScreenView>();
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject,"UI-06 restaurant migration");
            var rect=(RectTransform)panel; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
            panel.GetComponent<Game.Adapter.In.UI.Layout.SafeAreaFitter>()?.Recapture();
            foreach(var name in new[]{"Header","MainArea","SidePanel","Footer"})
            { var old=panel.Find(name); old.gameObject.SetActive(false); foreach(var g in old.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) g.raycastTarget=false; }
            var bg=Image(panel,"DecisionBackground",null,0,0,1920,1080); bg.color=GamePalette.Surface; bg.transform.SetAsFirstSibling();
            var header=Image(panel,"DecisionHeader","UI_TOPBAR_VIOLET",0,0,1920,88);
            Text(header.transform,"Title","Restaurante e público",24,14,1050,60,GameTypography.Tokens.ScreenTitle,true,GamePalette.OnDark);
            Text(header.transform,"Step","RESTAURANTE",1210,24,686,40,GameTypography.Tokens.Label,true,GamePalette.OnDark).alignment=TextAlignmentOptions.MidlineRight;
            Text(panel,"ChoiceTitle","Qual é a sua proposta?",24,112,1280,48,GameTypography.Tokens.SectionTitle,true);
            var data=Resources.LoadAll<RestaurantData>("Restaurants");
            var types=new[]{RestaurantType.PODRAO,RestaurantType.JAPONES,RestaurantType.FRANCES};
            var fields=new[]{"cardPodrao","cardJapones","cardFrances"}; var fileNames=new[]{"lanches","japones","frances"};
            for(int i=0;i<3;i++)
            {
                var restaurant=data.Single(d=>d.type==types[i]); restaurant.facade=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/restaurant/facades/rest_"+fileNames[i]+".png");
                restaurant.displayName=i==0?"Lanches":i==1?"Japonês":"Francês";
                restaurant.description=i==0?"Popular e volume alto":i==1?"Intermediário a premium":"Premium e experiência";
                EditorUtility.SetDirty(restaurant);
                var b=Get(view,fields[i]); b.transform.SetParent(panel,false); RetireChildren(b);
                var r=Node(panel,b.name,24+i*434,184,410,704); r.gameObject.SetActive(true); b.targetGraphic=Ensure<UnityEngine.UI.Image>(r);
                Image(r,"TypeIcon","ICON_RESTAURANT",24,16,32,32);
                Text(r,"OptionLabel","OPÇÃO 0"+(i+1),68,14,268,36,GameTypography.Tokens.Caption,true);
                Text(r,"SelectionStatus","Selecionar restaurante",24,660,362,36,GameTypography.Tokens.Caption,true).alignment=TextAlignmentOptions.Center;
                r.Find("TypeIcon").gameObject.SetActive(false);
                r.Find("OptionLabel").gameObject.SetActive(false);
                r.Find("SelectionStatus").gameObject.SetActive(false);
            }
            Set(view,"hintText",Text(panel,"SelectionHint","Escolha seu restaurante e público.",24,918,1280,42,GameTypography.Tokens.Body));
            var side=Image(panel,"DecisionSidebar","UI_PANEL_IVORY",1328,112,568,848);
            Text(side.transform,"Title","Quem você vai atender?",24,24,520,48,GameTypography.Tokens.SectionTitle,true);
            Text(side.transform,"Help","Escolha uma classe disponível para o restaurante selecionado.",24,80,520,60,GameTypography.Tokens.Body);
            var segments=new[]{"btnLow","btnMedium","btnHigh"};
            for(int i=0;i<3;i++)
            {
                var b=Get(view,segments[i]); b.transform.SetParent(side.transform,false); RetireChildren(b);
                var r=Node(side.transform,b.name,24+i*176,156,168,104); r.gameObject.SetActive(true); b.targetGraphic=Ensure<UnityEngine.UI.Image>(r);
                // Keep the prefab label inactive; the new native label has its own stable name.

                Image(r,"Radio","UI_RADIO_OFF",12,16,28,28);
                Text(r,"SegmentLabel","Classe "+(i==0?"C":i==1?"B":"A"),48,12,108,40,GameTypography.Tokens.Button,true);
                Text(r,"Availability","Disponível",8,60,152,32,GameTypography.Tokens.Caption).alignment=TextAlignmentOptions.Center;
                SharedUiStyles.Bind(b,SharedControlKind.Card);
            }
            side.transform.Find("Help").gameObject.SetActive(false);
            ApplyCompatibilityLayout(panel);
            var footer=Image(panel,"DecisionFooter","UI_NAVBAR_PLUM",0,984,1920,96);
            Action(view,"backButton",footer.transform,"Back","Voltar à localização",24,332,SharedControlKind.Secondary);
            Action(view,"confirmButton",footer.transform,"Confirm","Confirmar restaurante",1500,396,SharedControlKind.Primary);
            Text(footer.transform,"DraftHint","Escolha em rascunho",388,24,520,48,GameTypography.Tokens.Label,false,GamePalette.OnDark);
            ApplyDecisionLayout(panel);
            foreach (var field in fields)
                SharedUiStyles.Bind(Get(view, field), SharedControlKind.Card);
            foreach (var restaurant in data)
                view.SetRestaurantCardData(restaurant);
            view.SelectRestaurantCard(null); view.SelectSegmentButton(null); view.SetConfirmEnabled(false); panel.gameObject.SetActive(false);
            Font06Migration.Apply(new[] { panel.gameObject }, "Assets/Scenes/GameScene.unity");
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
