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
    public static class MenuPricingUiMigration
    {
        private static T Ensure<T>(Component c) where T:Component => c.GetComponent<T>() ?? c.gameObject.AddComponent<T>();
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
            t.textWrappingMode=TextWrappingModes.Normal; t.overflowMode=TextOverflowModes.Overflow; t.raycastTarget=false; return t;
        }
        private static T Get<T>(UnityEngine.Object o,string field) where T:UnityEngine.Object => (T)new SerializedObject(o).FindProperty(field).objectReferenceValue;
        private static void Set(UnityEngine.Object o,string field,UnityEngine.Object value) { var so=new SerializedObject(o); so.FindProperty(field).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Array(UnityEngine.Object o,string field,TextMeshProUGUI[] values) {var so=new SerializedObject(o); var prop=so.FindProperty(field); prop.arraySize=values.Length; for(int i=0;i<values.Length;i++)prop.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; so.ApplyModifiedPropertiesWithoutUndo();}
        private static void Retire(Transform root)
        {
            foreach(Transform t in root) {t.gameObject.SetActive(false); foreach(var g in t.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))g.raycastTarget=false;}
            foreach(var s in root.GetComponents<UnityEngine.UI.Shadow>())s.enabled=false;
            var layout=root.GetComponent<UnityEngine.UI.LayoutGroup>(); if(layout!=null)layout.enabled=false;
        }
        private static UnityEngine.UI.Button Button(Transform p,string name,string label,float x,float y,float w,float h,SharedControlKind kind,UnityEngine.UI.Button existing=null)
        {
            if(existing!=null){existing.transform.SetParent(p,false);existing.name=name;Retire(existing.transform);}
            var r=Node(p,name,x,y,w,h); var b=Ensure<UnityEngine.UI.Button>(r); b.targetGraphic=Ensure<UnityEngine.UI.Image>(r);
            Text(r,"Label",label,8,8,w-16,h-16,GameTypography.Tokens.Button,true).alignment=TextAlignmentOptions.Center;
            SharedUiStyles.Bind(b,kind); return b;
        }
        private static UnityEngine.UI.ScrollRect Scroll(Transform p,string name,float x,float y,float w,float h)
        {
            var root=Node(p,name,x,y,w,h); var scroll=Ensure<UnityEngine.UI.ScrollRect>(root);
            scroll.horizontal=false; scroll.vertical=true; scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=44;
            var vp=Image(root,"Viewport",null,4,4,w-24,h-8,true); Ensure<UnityEngine.UI.Mask>(vp).showMaskGraphic=false;
            var content=Node(vp.transform,"Content",0,0,w-24,0);
            var layout=Ensure<UnityEngine.UI.VerticalLayoutGroup>(content); layout.spacing=16; layout.padding=new RectOffset(4,4,4,4);
            layout.childControlWidth=layout.childControlHeight=true; layout.childForceExpandHeight=false; layout.childForceExpandWidth=true;
            var fit=Ensure<UnityEngine.UI.ContentSizeFitter>(content); fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=vp.rectTransform; scroll.content=content;
            var track=Image(root,"Scrollbar","UI_SCROLLBAR_TRACK",w-12,0,12,h);
            var area=Node(track.transform,"SlidingArea",0,0,12,h); var handle=Image(area,"Handle","UI_SCROLLBAR_THUMB",0,0,12,80,true);
            handle.rectTransform.anchorMin=Vector2.zero; handle.rectTransform.anchorMax=Vector2.one; handle.rectTransform.offsetMin=handle.rectTransform.offsetMax=Vector2.zero;
            var bar=Ensure<UnityEngine.UI.Scrollbar>(track); bar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop; bar.handleRect=handle.rectTransform; bar.targetGraphic=handle;
            bar.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None}; scroll.verticalScrollbar=bar; scroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }
        private static TextMeshProUGUI Flow(Transform p,string name,string value,TypographyToken size,bool bold=false)
            => Text(p,name,value,0,0,512,32,size,bold);
        private static void Height(Component c,float h){var e=Ensure<UnityEngine.UI.LayoutElement>(c); e.minHeight=e.preferredHeight=h;e.flexibleHeight=0;}

        private static void ProductPrefab()
        {
            const string path="Assets/Prefabs/Card_ProductItem.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var item=root.GetComponent<ProductPricingItemView>(); var rect=(RectTransform)root.transform;
                var slider=Get<UnityEngine.UI.Slider>(item,"priceSlider"); var picture=Get<UnityEngine.UI.Image>(item,"productImage");
                Retire(rect); rect.sizeDelta=new Vector2(1250,228); SharedUiStyles.Surface(root,"UI_CARD_NORMAL");
                picture.transform.SetParent(rect,false); Node(rect,picture.name,12,4,228,220); picture.raycastTarget=false; picture.preserveAspect=true; picture.type=UnityEngine.UI.Image.Type.Simple;
                Set(item,"productNameText",Text(rect,"ProductName","Produto",280,28,420,116,GameTypography.Tokens.SectionTitle,true));
                Set(item,"editingText",Text(rect,"EditingStatus","Incluído no cardápio",280,176,420,36,GameTypography.Tokens.Caption));
                Text(rect,"PriceLabel","Preço de venda",744,8,456,32,GameTypography.Tokens.Label,true);
                Image(rect,"StepperBase","UI_STEPPER_BASE",740,44,472,76);
                Set(item,"decreaseButton",Button(rect,"Decrease","−",744,48,68,68,SharedControlKind.Secondary));
                Text(rect,"Currency","R$",828,56,48,52,GameTypography.Tokens.BodyStrong,true);
                var inputRect=Node(rect,"PriceInput",880,48,240,68); var input=Ensure<TMP_InputField>(inputRect); input.targetGraphic=Ensure<UnityEngine.UI.Image>(inputRect);
                var viewport=Node(inputRect,"TextArea",14,8,212,52); Ensure<UnityEngine.UI.RectMask2D>(viewport);
                var value=Text(viewport,"Value","0,00",0,0,212,52,GameTypography.Tokens.Input,true); value.alignment=TextAlignmentOptions.MidlineRight;
                value.textWrappingMode=TextWrappingModes.NoWrap; value.overflowMode=TextOverflowModes.Masking;
                input.textViewport=viewport; input.textComponent=value; input.contentType=TMP_InputField.ContentType.Standard; input.lineType=TMP_InputField.LineType.SingleLine;
                input.characterLimit=16; input.keyboardType=TouchScreenKeyboardType.DecimalPad; input.customCaretColor=true; input.caretColor=GamePalette.Ink; input.selectionColor=GamePalette.Lavender;
                SharedUiStyles.Bind(input,SharedControlKind.Input); Set(item,"priceInput",input);
                Set(item,"increaseButton",Button(rect,"Increase","+",1136,48,68,68,SharedControlKind.Secondary));
                slider.transform.SetParent(rect,false); Node(rect,slider.name,760,130,424,36); slider.interactable=true;
                foreach(var graphic in slider.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))graphic.raycastTarget=false;
                var bg=slider.transform.Find("Background").GetComponent<UnityEngine.UI.Image>(); bg.sprite=SharedUiSkin.Current.Get("UI_SLIDER_TRACK"); bg.type=UnityEngine.UI.Image.Type.Sliced; bg.color=Color.white;
                var fill=slider.fillRect.GetComponent<UnityEngine.UI.Image>(); fill.sprite=SharedUiSkin.Current.Get("UI_SLIDER_FILL");fill.color=Color.white;
                SharedUiStyles.Bind(slider,SharedControlKind.Slider); slider.targetGraphic.raycastTarget=true;
                Set(item,"minPriceText",Text(rect,"MinPrice","Mín.",744,170,228,32,GameTypography.Tokens.Caption));
                var max=Text(rect,"MaxPrice","Máx.",976,170,228,32,GameTypography.Tokens.Caption);max.alignment=TextAlignmentOptions.MidlineRight;Set(item,"maxPriceText",max);
                Set(item,"errorText",Text(rect,"PriceError","",744,200,460,30,GameTypography.Tokens.Caption,false,GamePalette.ErrorText));
                var outline=Image(rect,"EditingOutline","UI_CARD_SELECTED_OUTLINE",0,0,1250,228);outline.rectTransform.anchorMin=Vector2.zero;outline.rectTransform.anchorMax=Vector2.one;outline.rectTransform.offsetMin=outline.rectTransform.offsetMax=Vector2.zero;outline.color=GamePalette.Primary;outline.enabled=false;Set(item,"editingOutline",outline);
                // The old display is retained for serialized compatibility, but the input is the visible value.
                Get<TextMeshProUGUI>(item,"selectedPriceText").gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        [MenuItem("Tools/Jogos de Empresa/UI-07/Migrate menu pricing in GameScene")]
        public static void Apply()
        {
            if(UnityEngine.Application.dataPath!="D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying)throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if(scene.path!="Assets/Scenes/GameScene.unity")throw new InvalidOperationException("Open GameScene.");
            ProductPrefab();
            var manifest=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("docs/art/manifest.json"));
            foreach(var asset in manifest["assets"].Where(a=>((string)a["id"]).StartsWith("PRODUCT_")))
            {
                var product=AssetDatabase.LoadAssetAtPath<ProductData>((string)asset["source"]);
                if(product.id!=(string)asset["catalog_id"])throw new InvalidOperationException("Product ID mismatch");
                product.productSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/"+(string)asset["path"]);
                product.displayName=product.displayName.Replace("classico","clássico").Replace("medio","médio"); EditorUtility.SetDirty(product);
            }
            var view=UnityEngine.Object.FindFirstObjectByType<MenuPricingScreenView>(FindObjectsInactive.Include);var panel=(RectTransform)view.transform;
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject,"UI-07 menu pricing migration");
            panel.anchorMin=Vector2.zero;panel.anchorMax=Vector2.one;panel.offsetMin=panel.offsetMax=Vector2.zero;panel.GetComponent<Game.Adapter.In.UI.Layout.SafeAreaFitter>()?.Recapture();
            foreach(var name in new[]{"Header","MainArea","Footer"}) {var old=panel.Find(name);old.gameObject.SetActive(false);foreach(var g in old.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))g.raycastTarget=false;}
            var bg=Image(panel,"DecisionBackground",null,0,0,1920,1080);bg.color=GamePalette.Surface;bg.transform.SetAsFirstSibling();
            var header=Image(panel,"DecisionHeader","UI_TOPBAR_VIOLET",0,0,1920,88);
            Set(view,"titleText",Text(header.transform,"Title","Cardápio e preços",24,14,1050,60,GameTypography.Tokens.ScreenTitle,true,GamePalette.OnDark));
            var step=Text(header.transform,"Step","PASSO 4 DE 7 · CARDÁPIO",1160,24,736,40,GameTypography.Tokens.Label,true,GamePalette.OnDark);step.alignment=TextAlignmentOptions.MidlineRight;Set(view,"stepText",step);
            Set(view,"restaurantText",Text(panel,"Restaurant","Defina o preço de cada produto",24,112,1280,48,GameTypography.Tokens.SectionTitle,true));
            var products=Scroll(panel,"Products",24,176,1280,736);Set(view,"productsScrollRect",products);Set(view,"productItemsContainer",products.content);
            var so=new SerializedObject(view);so.FindProperty("productItemHeight").floatValue=228;so.FindProperty("productItemSpacing").floatValue=16;so.ApplyModifiedPropertiesWithoutUndo();
            Set(view,"hintText",Text(panel,"ValidationHint","Confira os três preços e confirme suas decisões.",24,928,1280,40,GameTypography.Tokens.Body));
            var side=Image(panel,"DecisionSidebar","UI_PANEL_IVORY",1328,112,568,848);
            var details=Scroll(side.transform,"Strategy",24,24,520,800);
            Flow(details.content,"Title","Sua estratégia de preços",GameTypography.Tokens.SectionTitle,true);
            Set(view,"strategyHelpText",Flow(details.content,"Help","Os três produtos fazem parte do cardápio. Ajuste cada preço dentro da faixa permitida.",GameTypography.Tokens.Body));
            Flow(details.content,"CoherenceLabel","Coerência com o restaurante",GameTypography.Tokens.Label,true);
            var track=Image(details.content,"CoherenceTrack","UI_PANEL_LAVENDER",0,0,496,16);Height(track,16);
            var bar=Image(track.transform,"Fill",null,0,0,496,16);bar.color=GamePalette.Money;bar.preserveAspect=false;bar.type=UnityEngine.UI.Image.Type.Simple;bar.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;Set(view,"priceCoherenceBarFill",bar);
            Set(view,"priceCoherenceText",Flow(details.content,"CoherenceText","Defina os preços do cardápio.",GameTypography.Tokens.Body));
            Flow(details.content,"SummaryTitle","Resumo do cardápio",GameTypography.Tokens.SectionTitle,true);
            var names=new TextMeshProUGUI[3];var prices=new TextMeshProUGUI[3];
            for(int i=0;i<3;i++)
            {
                names[i]=Flow(details.content,"SummaryName"+i,"Produto",GameTypography.Tokens.Label);
                prices[i]=Flow(details.content,"SummaryPrice"+i,"R$ 0,00",GameTypography.Tokens.BodyStrong,true);prices[i].alignment=TextAlignmentOptions.MidlineRight;
            }
            Array(view,"summaryNames",names);Array(view,"summaryPrices",prices);
            Set(view,"inputHelpText",Flow(details.content,"EditHelp","Digite com vírgula ou ponto. Os botões ajustam R$ 1,00 por vez; o controle deslizante permite ajustes finos.",GameTypography.Tokens.Caption));
            var footer=Image(panel,"DecisionFooter","UI_NAVBAR_PLUM",0,984,1920,96);
            var back=Button(footer.transform,"Back","Voltar ao restaurante",24,14,340,68,SharedControlKind.Secondary,Get<UnityEngine.UI.Button>(view,"backButton"));Set(view,"backButton",back);
            var confirm=Button(footer.transform,"Confirm","Confirmar decisões",1516,14,380,68,SharedControlKind.Primary,Get<UnityEngine.UI.Button>(view,"confirmButton"));Set(view,"confirmButton",confirm);
            Set(view,"footerText",Text(footer.transform,"DraftHint","Suas decisões serão salvas ao confirmar",396,24,440,48,GameTypography.Tokens.Label,false,GamePalette.OnDark));
            view.SetReviewMode(false);view.SetConfirmEnabled(false);panel.gameObject.SetActive(false);
            EditorUtility.SetDirty(view);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
