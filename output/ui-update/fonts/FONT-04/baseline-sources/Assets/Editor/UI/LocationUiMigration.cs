using System;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using Game.Adapter.In.UI.Navigation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>UI-05 reuses the exact UI-04 MapMaster, its compact art, hit polygons and marker appearance.</summary>
    public static class LocationUiMigration
    {
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameTypography.FontAssetPath);
        private static T Ensure<T>(Component component) where T:Component => component.GetComponent<T>() ?? component.gameObject.AddComponent<T>();
        private static RectTransform Node(Transform parent,string name,float x,float y,float w,float h)
        {
            var t=parent.Find(name) as RectTransform;
            if(t==null) { t=(RectTransform)new GameObject(name,typeof(RectTransform)).transform; t.SetParent(parent,false); }
            t.anchorMin=t.anchorMax=t.pivot=new Vector2(0,1); t.anchoredPosition=new Vector2(x,-y);
            t.sizeDelta=new Vector2(w,h); t.localScale=Vector3.one; return t;
        }
        private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
        { var so=new SerializedObject(target); so.FindProperty(field).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static UnityEngine.Object Get(UnityEngine.Object target,string field) => new SerializedObject(target).FindProperty(field).objectReferenceValue;
        private static UnityEngine.UI.Image Image(Transform p,string name,string id,float x,float y,float w,float h,bool hit=false)
        {
            var r=Node(p,name,x,y,w,h); var image=Ensure<UnityEngine.UI.Image>(r);
            image.sprite=id==null?null:SharedUiSkin.Current.Get(id); image.color=Color.white; image.raycastTarget=hit;
            if(image.sprite==null && id!=null && id.StartsWith("UI_PIN_"))
                image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/location/markers/"+id.Substring(3).ToLowerInvariant()+".png");
            image.type=image.sprite!=null && image.sprite.border!=Vector4.zero?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            return image;
        }
        private static TextMeshProUGUI Text(Transform p,string name,string value,float x,float y,float w,float h,float size,bool bold=false,Color? color=null)
        {
            var t=Ensure<TextMeshProUGUI>(Node(p,name,x,y,w,h)); GameTypography.Apply(t,Font,size,bold);
            t.text=value; t.color=color??GamePalette.Ink; t.alignment=TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode=TextWrappingModes.Normal; t.overflowMode=TextOverflowModes.Overflow; return t;
        }
        private static void Height(Component component,float value)
        { var l=Ensure<UnityEngine.UI.LayoutElement>(component); l.preferredHeight=value; l.flexibleHeight=0; }
        private static TextMeshProUGUI FlowText(Transform p,string name,string value,float size,bool bold=false)
        {
            var t=Text(p,name,value,0,0,496,32,size,bold);
            // TMP provides preferred height to the parent's layout; no fixed cap on long copy.
            return t;
        }
        private static TextMeshProUGUI Row(Transform p,string name,string label,string icon)
        {
            var row=Image(p,name,"UI_PANEL_LAVENDER",0,0,496,48); Height(row,48);
            Image(row.transform,"Icon",icon,12,10,28,28).color=GamePalette.Plum;
            Text(row.transform,"Label",label,52,6,216,36,GameTypography.Label);
            var value=Text(row.transform,"Value","—",272,6,212,36,GameTypography.Label,true);
            value.alignment=TextAlignmentOptions.MidlineRight; return value;
        }
        private static UnityEngine.UI.Button Action(Transform p,string name,string label,SharedControlKind kind,float x,float w)
        {
            var image=Image(p,name,kind==SharedControlKind.Primary?"UI_PRIMARY_VIOLET_NORMAL":"UI_SECONDARY_IVORY_NORMAL",x,14,w,68,true);
            var b=Ensure<UnityEngine.UI.Button>(image); b.targetGraphic=image;
            var t=Text(b.transform,"Label",label,16,8,w-32,52,GameTypography.Button,true);
            t.alignment=TextAlignmentOptions.Center; SharedUiStyles.Bind(b,kind); return b;
        }
        [MenuItem("Tools/Jogos de Empresa/UI-05/Migrate location in GameScene")]
        public static void Apply()
        {
            if(UnityEngine.Application.dataPath!="D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use the Cogna Editor outside Play Mode.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            var canvas=scene.GetRootGameObjects().First(r=>r.name=="Canvas").transform;
            var panel=canvas.Find("Panel_Location"); var view=panel.GetComponent<LocationScreenView>();
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject,"UI-05 location migration");
            var panelRect=(RectTransform)panel;
            panelRect.anchorMin=Vector2.zero; panelRect.anchorMax=Vector2.one; panelRect.pivot=new Vector2(.5f,.5f);
            panelRect.offsetMin=panelRect.offsetMax=Vector2.zero; panelRect.localScale=Vector3.one;
            panel.GetComponent<Game.Adapter.In.UI.Layout.SafeAreaFitter>()?.Recapture();
            // Existing controller and serialized panel/state references stay in place.
            foreach(var name in new[]{"MapArea","SidePanel","Header","BottomBar"})
            {
                var old=panel.Find(name); old.gameObject.SetActive(false);
                foreach(var graphic in old.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget=false;
            }
            panel.Find("LocationScreenContext").gameObject.SetActive(false);
            foreach(var old in panel.GetComponentsInChildren<LocationScreenV2View>(true)) old.enabled=false;
            view.enabled=true;
            var background=Image(panel,"DecisionBackground",null,0,0,1920,1080);
            background.color=GamePalette.Surface; background.transform.SetAsFirstSibling();
            RectTransform master=panel.Find("MapMaster") as RectTransform;
            if(master==null)
            {
                master=UnityEngine.Object.Instantiate(canvas.Find("Panel_Menu/MapMaster").gameObject,panel,false).GetComponent<RectTransform>();
                master.name="MapMaster";
            }
            // No coordinate recalculation: art, polygons and label hit targets keep the UI-04 matrix.
            var nodes=master.GetComponentsInChildren<MapMenuNode>(true);
            var center=nodes.Single(n=>string.IsNullOrEmpty(n.LocationId)); center.gameObject.SetActive(false);
            var ids=new[]{"university","bank","store","condominium","marketing"};
            var labels=new[]{"Educacional","Financeira","Comercial","Residencial","Corporativa"};
            var so=new SerializedObject(view); var zones=so.FindProperty("zones"); zones.arraySize=5;
            for(int i=0;i<5;i++)
            {
                var node=nodes.Single(n=>n.LocationId==ids[i]); node.gameObject.SetActive(true);
                var nav=node.GetComponent<PanelNavButton>(); if(nav!=null) UnityEngine.Object.DestroyImmediate(nav);
                var b=node.GetComponent<UnityEngine.UI.Button>(); b.onClick=new UnityEngine.UI.Button.ButtonClickedEvent(); b.interactable=true;
                node.transform.Find("MapCaption").GetComponent<TMP_Text>().text=labels[i]; node.SetSelected(false);
                var z=zones.GetArrayElementAtIndex(i); z.FindPropertyRelative("id").stringValue=ids[i];
                z.FindPropertyRelative("marker").objectReferenceValue=node;
                z.FindPropertyRelative("overlay").objectReferenceValue=master.Find("OVERLAY_"+ids[i].ToUpperInvariant()).GetComponent<UnityEngine.UI.Image>();
                z.FindPropertyRelative("vignette").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/location/illustrations/vignette_"+ids[i]+".png");
                so.FindProperty(ids[i]+"Button").objectReferenceValue=b;
                so.FindProperty(ids[i]+"Area").objectReferenceValue=null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var restaurants=so.FindProperty("restaurants"); restaurants.arraySize=3;
            var restIds=new[]{"FRANCES","JAPONES","LANCHES"}; var types=new[]{RestaurantType.FRANCES,RestaurantType.JAPONES,RestaurantType.PODRAO};
            for(int i=0;i<3;i++)
            {
                var r=restaurants.GetArrayElementAtIndex(i); r.FindPropertyRelative("type").intValue=(int)types[i];
                r.FindPropertyRelative("image").objectReferenceValue=master.Find("MAP_REST_"+restIds[i]).GetComponent<UnityEngine.UI.Image>();
                master.Find("MAP_REST_"+restIds[i]).GetComponent<UnityEngine.UI.Image>().enabled=false;
            }
            so.FindProperty("centerOverlay").objectReferenceValue=master.Find("OVERLAY_CENTER").GetComponent<UnityEngine.UI.Image>();
            so.ApplyModifiedPropertiesWithoutUndo(); master.Find("OVERLAY_CENTER").GetComponent<UnityEngine.UI.Image>().enabled=false;
            var bar=Image(panel,"DecisionHeader","UI_TOPBAR_VIOLET",0,0,1920,88);
            Text(bar.transform,"Title","Escolha sua localização",24,14,1000,60,GameTypography.ScreenTitle,true,GamePalette.OnDark);
            Text(bar.transform,"Step","PASSO 2 DE 7 · LOCALIZAÇÃO",1210,24,686,40,GameTypography.Label,true,GamePalette.OnDark).alignment=TextAlignmentOptions.MidlineRight;
            var region=Text(panel,"Region","Localização",24,112,1240,48,GameTypography.SectionTitle,true);
            var hint=Text(panel,"SelectionHint","Selecione uma área no mapa para continuar.",24,918,1280,42,GameTypography.Body);
            Image(panel,"AvailableLegend","UI_PIN_NORMAL",822,118,32,32);
            Text(panel,"AvailableLegendLabel","Disponível",862,116,160,36,GameTypography.Caption);
            Image(panel,"SelectedLegend","UI_PIN_SELECTED",1040,118,32,32);
            Text(panel,"SelectedLegendLabel","Selecionada",1080,116,200,36,GameTypography.Caption);
            // Scroll stays inside the sidebar; longer native copy expands vertically, never over the map.
            var side=Image(panel,"DecisionSidebar","UI_PANEL_IVORY",1328,112,568,848,true);
            var empty=Node(side.transform,"Empty",24,24,520,800);
            Image(empty,"Icon","ICON_PIN",216,144,88,88).color=GamePalette.Primary;
            Text(empty,"Title","Onde vamos começar?",16,270,488,96,GameTypography.SectionTitle,true).alignment=TextAlignmentOptions.Center;
            var emptyText=Text(empty,"Message","Selecione uma área",16,390,488,140,GameTypography.Body);
            emptyText.alignment=TextAlignmentOptions.Center;
            Text(empty,"Help","Cada área tem seu público, custo de aluguel e potencial de demanda.",16,578,488,116,GameTypography.Body).alignment=TextAlignmentOptions.Center;
            var scrollRoot=Node(side.transform,"Details",24,20,520,800);
            var scroll=Ensure<UnityEngine.UI.ScrollRect>(scrollRoot); scroll.horizontal=false; scroll.vertical=true;
            scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=44;
            var viewport=Image(scrollRoot,"Viewport",null,0,0,504,800,true); viewport.color=Color.white;
            Ensure<UnityEngine.UI.Mask>(viewport).showMaskGraphic=false;
            var content=Node(viewport.transform,"Content",0,0,496,800);
            var layout=Ensure<UnityEngine.UI.VerticalLayoutGroup>(content);
            layout.spacing=8; layout.padding=new RectOffset(0,0,0,8); layout.childControlWidth=true; layout.childControlHeight=true;
            layout.childForceExpandHeight=false; layout.childForceExpandWidth=true;
            var fit=Ensure<UnityEngine.UI.ContentSizeFitter>(content); fit.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport.rectTransform; scroll.content=content;
            var vignette=Image(content,"Vignette",null,0,0,496,196); vignette.preserveAspect=true; Height(vignette,196);
            var title=FlowText(content,"ZoneTitle","Área Educacional",GameTypography.SectionTitle,true);
            var segment=FlowText(content,"Segment","Classe C",GameTypography.Label,true); segment.color=GamePalette.Primary;
            var description=FlowText(content,"Description","Descrição da área.",GameTypography.Body);
            var rent=Row(content,"Rent","Aluguel / mês","ICON_CASH");
            var demand=Row(content,"Demand","Demanda base","ICON_CHART_BARS");
            var capacity=Row(content,"Capacity","Capacidade inicial","ICON_TEAM");
            var competition=Row(content,"Competition","Concorrência","ICON_COMMERCE");
            var price=Row(content,"Price","Fator de preço","ICON_CASH");
            var channelsTitle=FlowText(content,"ChannelsLabel","CANAIS",GameTypography.Caption,true); channelsTitle.color=GamePalette.Muted;
            var channels=FlowText(content,"Channels","Não informados",GameTypography.Label);
            var track=Image(scrollRoot,"Scrollbar","UI_SCROLLBAR_TRACK",508,0,12,800);
            var sliding=Node(track.transform,"SlidingArea",0,0,12,800);
            var handle=Image(sliding,"Handle","UI_SCROLLBAR_THUMB",0,0,12,80,true);
            // Scrollbar drives the anchors. Fixed sizeDelta would extend the thumb past the track.
            handle.rectTransform.anchorMin=Vector2.zero; handle.rectTransform.anchorMax=Vector2.one;
            handle.rectTransform.offsetMin=handle.rectTransform.offsetMax=Vector2.zero;
            var scrollbar=Ensure<UnityEngine.UI.Scrollbar>(track); scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect=handle.rectTransform; scrollbar.targetGraphic=handle;
            scroll.verticalScrollbar=scrollbar; scroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
            var footer=Image(panel,"DecisionFooter","UI_NAVBAR_PLUM",0,984,1920,96);
            var back=Action(footer.transform,"Back","Voltar à identificação",SharedControlKind.Secondary,24,332);
            var confirm=Action(footer.transform,"Confirm","Confirmar localização",SharedControlKind.Primary,1500,396);
            Text(footer.transform,"DraftHint","Escolha em rascunho",388,24,448,48,GameTypography.Label,false,GamePalette.OnDark);
            Set(view,"regionText",region); Set(view,"hintText",hint); Set(view,"emptyStatePanel",empty.gameObject);
            Set(view,"emptyMessage",emptyText); Set(view,"detailsPanel",scrollRoot.gameObject); Set(view,"infoPanel",null);
            Set(view,"detailZoneNameText",title); Set(view,"detailZoneSegmentText",segment); Set(view,"detailSegmentText",null);
            Set(view,"detailZoneDescriptionText",description); Set(view,"detailRentText",rent); Set(view,"detailDemandaText",demand);
            Set(view,"detailCapacidadeText",capacity); Set(view,"detailConcorrenciaText",competition); Set(view,"referencePriceText",price);
            Set(view,"channelsText",channels); Set(view,"zoneVignette",vignette); Set(view,"detailScroll",scroll);
            Set(view,"confirmButton",confirm); Set(view,"backButton",back);
            var controls=ids.Select(id=>nodes.Single(n=>n.LocationId==id).GetComponent<UnityEngine.UI.Button>()).Concat(new[]{back,confirm}).ToArray();
            for(int i=0;i<controls.Length;i++)
            {
                var nav=controls[i].navigation; nav.mode=UnityEngine.UI.Navigation.Mode.Explicit;
                nav.selectOnRight=nav.selectOnDown=controls[(i+1)%controls.Length];
                nav.selectOnLeft=nav.selectOnUp=controls[(i+controls.Length-1)%controls.Length]; controls[i].navigation=nav;
            }
            scrollbar.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            view.ShowEmptyState(); panel.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
