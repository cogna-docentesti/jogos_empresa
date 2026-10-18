using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public static class SharedUiShowcaseBuilder
    {
        private static RectTransform Box(Transform parent,string name,float x,float y,float w,float h)
        {
            var r=UiFactory.EnsureChild(parent,name); r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); return r;
        }
        private static TMP_Text Label(Transform p,string n,string value,float x,float y,float w,float h,float size=24,bool dark=false)
        {
            return UiFactory.Text(Box(p,n,x,y,w,h),value,size,size>=32?FontStyles.Bold:FontStyles.Normal,
                dark?GamePalette.OnDark:GamePalette.Ink,TextAlignmentOptions.MidlineLeft,true);
        }
        private static UnityEngine.UI.Button Button(Transform p,string n,string value,float x,float y,float w,SharedControlKind role=SharedControlKind.Primary,string icon=null)
        {
            var r=Box(p,n,x,y,w,68); var image=UiFactory.Ensure<UnityEngine.UI.Image>(r.gameObject);
            var button=UiFactory.Ensure<UnityEngine.UI.Button>(r.gameObject); button.targetGraphic=image;
            var label=Label(r,"Label",value,icon!=null?60:20,0,w-(icon!=null?80:40),68);
            label.alignment=TextAlignmentOptions.Center; GameTypography.KeepSize(label,TypographyRole.Button);
            if(icon!=null) SharedUiStyles.Surface(Box(r,"Icon",20,18,32,32).gameObject,icon);
            SharedUiStyles.Bind(button,role); return button;
        }
        private static UnityEngine.UI.Toggle Toggle(Transform p,string n,string text,float x,float y,SharedControlKind role)
        {
            var r=Box(p,n,x,y,320,68); var t=UiFactory.Ensure<UnityEngine.UI.Toggle>(r.gameObject);
            var visual=Box(r,"Control",0,0,role==SharedControlKind.Toggle?112:68,68);
            t.targetGraphic=UiFactory.Ensure<UnityEngine.UI.Image>(visual.gameObject);
            Label(r,"Label",text,role==SharedControlKind.Toggle?128:84,0,200,68);
            SharedUiStyles.Bind(t,role); return t;
        }
        public static void Build(Transform canvas)
        {
            UiFactory.BeginRun(true);
            var root=UiFactory.EnsureChild(canvas,"SharedComponentsPreview"); root.anchorMin=Vector2.zero; root.anchorMax=Vector2.one; root.offsetMin=root.offsetMax=Vector2.zero;
            var overlay=UiFactory.Ensure<Canvas>(root.gameObject); overlay.overrideSorting=true; overlay.sortingOrder=30000;
            UiFactory.Ensure<UnityEngine.UI.GraphicRaycaster>(root.gameObject);
            UiFactory.Solid(root,GamePalette.Plum,true);
            Label(root,"Title","Componentes compartilhados",48,24,1300,64,44,true);
            Label(root,"Subtitle","UI-03 · GameScene · teclado, mouse e toque",48,88,1300,40,24,true);
            var left=Box(root,"ControlsPanel",48,152,1120,856); SharedUiStyles.Surface(left.gameObject,"UI_PANEL_IVORY");
            Label(left,"ActionsTitle","Ações e estados",32,24,1000,44,32);
            var primary=Button(left,"Primary","Continuar",32,88,240,icon:"ICON_NEXT");
            Button(left,"Secondary","Voltar",296,88,240,SharedControlKind.Secondary,"ICON_BACK");
            Button(left,"Danger","Remover",560,88,240,SharedControlKind.Danger,"ICON_TRASH");
            Button(left,"Accent","Destaque",824,88,264,SharedControlKind.Accent,"ICON_STAR");
            var disabled=Button(left,"Disabled","Indisponível",32,180,240); disabled.interactable=false;
            var open=Button(left,"OpenModal","Abrir modal",296,180,240,SharedControlKind.Secondary,"ICON_DOCUMENT");
            var help=Button(left,"IconOnly","",560,180,68,SharedControlKind.Icon,"ICON_HELP");
            Label(left,"InteractionHint","Tab / Shift+Tab · setas · Enter / Espaço",652,180,430,68,22);
            Label(left,"InputLabel","Nome do componente · mínimo de 5 caracteres",32,272,1000,32,22);
            var field=Box(left,"Input",32,316,656,68); var input=UiFactory.Ensure<TMP_InputField>(field.gameObject);
            input.targetGraphic=UiFactory.Ensure<UnityEngine.UI.Image>(field.gameObject);
            var viewport=UiFactory.EnsureChild(field,"TextArea"); UiFactory.Stretch(viewport,24,12,24,12); UiFactory.Ensure<UnityEngine.UI.RectMask2D>(viewport.gameObject);
            var text=(TextMeshProUGUI)UiFactory.Text(UiFactory.EnsureChild(viewport,"Text"),"",24,FontStyles.Normal,GamePalette.Ink);
            UiFactory.Stretch(text.rectTransform); var placeholder=Label(viewport,"Placeholder","Digite um nome longo",0,0,608,44); placeholder.color=GamePalette.Muted;
            input.textViewport=viewport; input.textComponent=text; input.placeholder=placeholder; input.characterLimit=80;
            SharedUiStyles.Bind(input,SharedControlKind.Input);
            var disabledField=Box(left,"DisabledInput",712,316,376,68); var other=UiFactory.Ensure<TMP_InputField>(disabledField.gameObject);
            other.targetGraphic=UiFactory.Ensure<UnityEngine.UI.Image>(disabledField.gameObject);
            other.textComponent=(TextMeshProUGUI)Label(disabledField,"Text","Somente leitura",24,0,328,68); other.text="Somente leitura"; other.interactable=false; SharedUiStyles.Bind(other,SharedControlKind.Input);
            var checkbox=Toggle(left,"Checkbox","Checkbox",32,412,SharedControlKind.Checkbox);
            var radio=Toggle(left,"Radio","Radio",392,412,SharedControlKind.Radio);
            var toggle=Toggle(left,"Toggle","Alternar",752,412,SharedControlKind.Toggle);
            var card=Button(left,"SelectableCard","Selecionar card",32,528,320,SharedControlKind.Card,"ICON_CHECK");
            card.GetComponent<RectTransform>().sizeDelta=new Vector2(320,192);
            var cardText=card.transform.Find("Label") as RectTransform; cardText.anchoredPosition=new Vector2(60,-124);
            SharedUiStyles.Surface(Box(card.transform,"Illustration",108,16,104,104).gameObject,"UI_BADGE_CIRCLE");
            Button(left,"Tab","Aba selecionável",384,528,320,SharedControlKind.Tab).gameObject.AddComponent<SharedShowcaseSelection>();
            Button(left,"Pill","Filtro selecionável",736,528,352,SharedControlKind.Pill).gameObject.AddComponent<SharedShowcaseSelection>();
            Label(left,"Feedback","Estados com texto nativo, foco e contornos separados",384,628,704,84);
            var tip=Box(left,"Tooltip",32,760,1056,64); SharedUiStyles.Surface(tip.gameObject,"UI_TOOLTIP_SURFACE");
            Label(tip,"Text","Dica: os controles preservam seus callbacks e dados.",24,0,1008,64,22);
            UiFactory.Ensure<SharedTooltip>(help.gameObject).panel=tip.gameObject; tip.gameObject.SetActive(false);
            var right=Box(root,"ScrollPanel",1192,152,680,856); SharedUiStyles.Surface(right.gameObject,"UI_PANEL_LAVENDER");
            Label(right,"ScrollTitle","Seleção e rolagem",32,24,616,44,32);
            var scrollBox=Box(right,"Scroll",32,88,616,564); var scroll=UiFactory.Ensure<UnityEngine.UI.ScrollRect>(scrollBox.gameObject);
            var vp=Box(scrollBox,"Viewport",0,0,536,564); UiFactory.Solid(vp,Color.white,true); UiFactory.Ensure<UnityEngine.UI.Mask>(vp.gameObject).showMaskGraphic=false;
            var content=Box(vp,"Content",0,0,536,0); var layout=UiFactory.Ensure<UnityEngine.UI.VerticalLayoutGroup>(content.gameObject);
            layout.spacing=16; layout.childControlHeight=true; layout.childForceExpandHeight=false; layout.childControlWidth=true; layout.childForceExpandWidth=true;
            var fit=UiFactory.Ensure<UnityEngine.UI.ContentSizeFitter>(content.gameObject); fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            for(int i=0;i<12;i++)
            {
                var row=Button(content,"Row"+i,"Componente "+(i+1).ToString("00"),0,0,536,SharedControlKind.Pill,"ICON_DOCUMENT");
                var e=UiFactory.Ensure<UnityEngine.UI.LayoutElement>(row.gameObject); e.preferredHeight=80;
                if(row.GetComponent<SharedShowcaseSelection>()==null) row.gameObject.AddComponent<SharedShowcaseSelection>();
            }
            scroll.content=content; scroll.viewport=vp; scroll.horizontal=false; scroll.vertical=true; scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var bar=Box(scrollBox,"Scrollbar",552,0,64,564); var scrollbar=UiFactory.Ensure<UnityEngine.UI.Scrollbar>(bar.gameObject);
            SharedUiStyles.Surface(bar.gameObject,"UI_SCROLLBAR_TRACK",true);
            var handle=UiFactory.EnsureChild(bar,"Handle"); UiFactory.Stretch(handle,20,0,20,0); scrollbar.handleRect=handle;
            scrollbar.targetGraphic=SharedUiStyles.Surface(handle.gameObject,"UI_SCROLLBAR_THUMB",true); scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            SharedUiStyles.Bind(scrollbar,SharedControlKind.Scrollbar); scroll.verticalScrollbar=scrollbar;
            Label(right,"SliderTitle","Ajuste por toque ou setas",32,680,616,36,22);
            var sliderRoot=Box(right,"Slider",32,728,616,68); var slider=UiFactory.Ensure<UnityEngine.UI.Slider>(sliderRoot.gameObject);
            var track=Box(sliderRoot,"Background",0,28,616,12); SharedUiStyles.Surface(track.gameObject,"UI_SLIDER_TRACK",true);
            UiFactory.Solid(sliderRoot,new Color(1,1,1,0),true);
            var fillArea=Box(sliderRoot,"FillArea",34,28,548,12); var fill=UiFactory.EnsureChild(fillArea,"Fill"); UiFactory.Stretch(fill); SharedUiStyles.Surface(fill.gameObject,"UI_SLIDER_FILL");
            var handleArea=Box(sliderRoot,"HandleArea",34,0,548,68); var thumb=Box(handleArea,"Thumb",0,0,68,68);
            slider.fillRect=fill; slider.handleRect=thumb; slider.targetGraphic=SharedUiStyles.Surface(thumb.gameObject,"UI_SLIDER_THUMB",true); slider.minValue=0; slider.maxValue=100; slider.value=50;
            SharedUiStyles.Bind(slider,SharedControlKind.Slider);
            var status=Label(root,"Status","Pronto para interação",48,1020,1824,40,22,true);
            var modal=UiFactory.EnsureChild(root,"Modal"); UiFactory.Stretch(modal); SharedUiStyles.Surface(modal.gameObject,"UI_SCRIM",true).preserveAspect=false;
            var shadow=Box(modal,"Shadow",548,326,824,428); SharedUiStyles.Surface(shadow.gameObject,"UI_PANEL_SHADOW");
            var panel=Box(modal,"Surface",560,320,800,416); SharedUiStyles.Surface(panel.gameObject,"UI_MODAL_SURFACE",true);
            Label(panel,"Title","Confirmar interação",40,32,720,60,32);
            Label(panel,"Body","Este modal demonstra scrim, camadas e foco. As ações desta composição não alteram a partida.",40,112,720,128);
            var close=Button(panel,"Close","Voltar",40,300,320,SharedControlKind.Secondary);
            var confirm=Button(panel,"Confirm","Confirmar",400,300,360);
            UiFactory.Ensure<SharedModalFocus>(modal.gameObject); modal.gameObject.SetActive(false);
            var view=UiFactory.Ensure<SharedComponentsShowcase>(root.gameObject);
            view.primary=primary; view.openModal=open; view.closeModal=close; view.confirmModal=confirm; view.modal=modal.gameObject;
            view.selectCard=card; view.checkbox=checkbox; view.radio=radio; view.toggle=toggle; view.slider=slider; view.status=status; view.input=input;
            if(!AssetDatabase.IsValidFolder("Assets/Prefabs/SharedUI")) AssetDatabase.CreateFolder("Assets/Prefabs","SharedUI");
            foreach(var control in new[]{"Primary","Secondary","Danger","Accent","IconOnly","Input","Checkbox","Radio","Toggle","SelectableCard","Tab","Pill"})
            {
                // Tooltip panel is a scene sibling, so keep that sample reference out of the icon template.
                if(control=="IconOnly") continue;
                PrefabUtility.SaveAsPrefabAsset(left.Find(control).gameObject,"Assets/Prefabs/SharedUI/"+control+".prefab");
            }
            PrefabUtility.SaveAsPrefabAsset(sliderRoot.gameObject,"Assets/Prefabs/SharedUI/Slider.prefab");
            root.gameObject.SetActive(false); UiFactory.BeginRun(false);
        }
        [MenuItem("Tools/Jogos de Empresa/UI-03/Show composition in GameScene")]
        public static void Show()
        {
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            { var preview=root.transform.Find("SharedComponentsPreview"); if(preview!=null) preview.gameObject.SetActive(true); }
        }
    }
}
