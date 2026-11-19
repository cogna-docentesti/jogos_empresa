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
        private static T Ensure<T>(Component target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.gameObject.AddComponent<T>();
        }
        private static RectTransform Node(Transform p,string name,float x,float y,float w,float h)
        {
            var r=p.Find(name) as RectTransform;
            if(r==null) { r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform; r.SetParent(p,false); }
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); r.localScale=Vector3.one; r.gameObject.SetActive(true); return r;
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
        private static TextMeshProUGUI Flow(Transform p,string name,string value,TypographyToken size,bool bold=false)
            => Text(p,name,value,0,0,480,32,size,bold);
        private static UnityEngine.UI.ScrollRect Scroll(Transform p,string name,float x,float y,float w,float h)
        {
            var root=Node(p,name,x,y,w,h); var scroll=Ensure<UnityEngine.UI.ScrollRect>(root);
            scroll.horizontal=false; scroll.vertical=true; scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=44;
            var vp=Image(root,"Viewport",null,0,0,w-16,h,true); Ensure<UnityEngine.UI.Mask>(vp).showMaskGraphic=false;
            var content=Node(vp.transform,"Content",0,0,w-24,h);
            var layout=Ensure<UnityEngine.UI.VerticalLayoutGroup>(content); layout.spacing=8; layout.padding=new RectOffset(0,0,0,8);
            layout.childControlWidth=layout.childControlHeight=true; layout.childForceExpandHeight=false; layout.childForceExpandWidth=true;
            var fit=Ensure<UnityEngine.UI.ContentSizeFitter>(content); fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=vp.rectTransform; scroll.content=content;
            var track=Image(root,"Scrollbar","UI_SCROLLBAR_TRACK",w-12,0,12,h);
            var area=Node(track.transform,"SlidingArea",0,0,12,h); var handle=Image(area,"Handle","UI_SCROLLBAR_THUMB",0,0,12,80,true);
            handle.rectTransform.anchorMin=Vector2.zero; handle.rectTransform.anchorMax=Vector2.one;
            handle.rectTransform.offsetMin=handle.rectTransform.offsetMax=Vector2.zero;
            var bar=Ensure<UnityEngine.UI.Scrollbar>(track); bar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop; bar.handleRect=handle.rectTransform; bar.targetGraphic=handle;
            bar.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            scroll.verticalScrollbar=bar; scroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
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

        internal static void ApplyCompatibilityLayout(Transform panel, bool conceptLayout)
        {
            var view = panel.GetComponent<RestaurantScreenView>();
            var sidebar = panel.Find("DecisionSidebar");
            var legacy = sidebar.Find("Compatibility");
            if (legacy != null) legacy.gameObject.SetActive(false);

            var trigger = conceptLayout
                ? Node(sidebar, "CompatibilityToggle", 884, 4, 336, 52)
                : Node(sidebar, "CompatibilityToggle", 24, 284, 520, 68);
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
                Image(r,"SelectedCheck","ICON_CHECK",354,16,32,32).gameObject.SetActive(false);
                var facade=Image(r,"Facade",null,16,60,378,284); facade.sprite=restaurant.facade; facade.preserveAspect=true;
                var info=Scroll(r,"CardDetails",24,360,362,292);
                Flow(info.content,"RestaurantName",restaurant.displayName,GameTypography.Tokens.SectionTitle,true);
                Flow(info.content,"RestaurantDesc",restaurant.description,GameTypography.Tokens.Body);
                Flow(info.content,"CostLabel","Custo fixo / mês",GameTypography.Tokens.Label);
                var cost=Flow(info.content,"MonthlyCost","—",GameTypography.Tokens.BodyStrong,true); cost.alignment=TextAlignmentOptions.MidlineRight;
                Flow(info.content,"TicketLabel","Ticket de referência",GameTypography.Tokens.Label);
                var ticket=Flow(info.content,"TicketRange","—",GameTypography.Tokens.BodyStrong,true); ticket.alignment=TextAlignmentOptions.MidlineRight;
                Text(r,"SelectionStatus","Selecionar restaurante",24,660,362,36,GameTypography.Tokens.Caption,true).alignment=TextAlignmentOptions.Center;
                SharedUiStyles.Bind(b,SharedControlKind.Card);
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
            ApplyCompatibilityLayout(panel, false);
            foreach (var restaurant in data)
                view.SetRestaurantCardData(restaurant);
            var footer=Image(panel,"DecisionFooter","UI_NAVBAR_PLUM",0,984,1920,96);
            Action(view,"backButton",footer.transform,"Back","Voltar à localização",24,332,SharedControlKind.Secondary);
            Action(view,"confirmButton",footer.transform,"Confirm","Confirmar restaurante",1500,396,SharedControlKind.Primary);
            Text(footer.transform,"DraftHint","Escolha em rascunho",388,24,520,48,GameTypography.Tokens.Label,false,GamePalette.OnDark);
            view.SelectRestaurantCard(null); view.SelectSegmentButton(null); view.SetConfirmEnabled(false); panel.gameObject.SetActive(false);
            Font06Migration.Apply(new[] { panel.gameObject }, "Assets/Scenes/GameScene.unity");
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
