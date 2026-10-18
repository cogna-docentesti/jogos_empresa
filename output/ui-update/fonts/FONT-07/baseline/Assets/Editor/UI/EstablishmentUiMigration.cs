using System;
using System.Linq;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    public static class EstablishmentUiMigration
    {
        private static Sprite Art(string id)
        {
            var shared=SharedUiSkin.Current.Get(id);if(shared!=null)return shared;
            var manifest=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("docs/art/manifest.json"));
            var entry=manifest["assets"].Single(a=>(string)a["id"]==id);
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/"+(string)entry["path"]);
            if(sprite==null)throw new InvalidOperationException("Missing approved artwork: "+id);return sprite;
        }
        private static T Ensure<T>(Component c) where T : Component => c.GetComponent<T>() ?? c.gameObject.AddComponent<T>();
        private static RectTransform Node(Transform p, string name, float x=0, float y=0, float w=0, float h=0)
        {
            var r=p.Find(name) as RectTransform;
            if(r==null) {r=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;r.SetParent(p,false);}
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;r.gameObject.SetActive(true);return r;
        }
        private static UnityEngine.UI.Image Image(Transform p,string name,string id,float x,float y,float w,float h,bool hit=false)
        {
            var t=Node(p,name,x,y,w,h);var image=Ensure<UnityEngine.UI.Image>(t);
            image.sprite=id==null?null:Art(id);image.color=Color.white;image.raycastTarget=hit;
            image.type=image.sprite!=null && image.sprite.border!=Vector4.zero?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect=image.type==UnityEngine.UI.Image.Type.Simple && id!=null;return image;
        }
        private static TMP_Text Text(Transform p,string name,string value,float x,float y,float w,float h,TypographyToken size,bool bold=false,Color? color=null)
        {
            var t=Ensure<TextMeshProUGUI>(Node(p,name,x,y,w,h));
            GameTypography.Apply(t,size,bold);
            t.text=value;t.color=color??GamePalette.Ink;t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;t.raycastTarget=false;return t;
        }
        private static void Set(UnityEngine.Object o,string f,UnityEngine.Object v){var so=new SerializedObject(o);so.FindProperty(f).objectReferenceValue=v;so.ApplyModifiedPropertiesWithoutUndo();}
        private static T Get<T>(UnityEngine.Object o,string f) where T:UnityEngine.Object =>(T)new SerializedObject(o).FindProperty(f).objectReferenceValue;
        private static void Height(Component c,float h){var e=Ensure<UnityEngine.UI.LayoutElement>(c);e.minHeight=e.preferredHeight=h;e.flexibleHeight=0;}
        private static UnityEngine.UI.VerticalLayoutGroup Vertical(Component c,int pad=0,int gap=12)
        {
            var l=Ensure<UnityEngine.UI.VerticalLayoutGroup>(c);l.padding=new RectOffset(pad,pad,pad,pad);l.spacing=gap;l.childControlHeight=l.childControlWidth=true;l.childForceExpandWidth=true;l.childForceExpandHeight=false;return l;
        }
        private static TMP_Text Flow(Transform p,string n,string value,TypographyToken size,bool bold=false)
        {var t=Text(p,n,value,0,0,1000,36,size,bold);var e=Ensure<UnityEngine.UI.LayoutElement>(t);e.minHeight=36;e.preferredHeight=-1;e.flexibleHeight=0;return t;}
        private static void Ignore(Component c)=>Ensure<UnityEngine.UI.LayoutElement>(c).ignoreLayout=true;
        private static UnityEngine.UI.Button Button(Transform p,string n,string value,float x,float y,float w,float h,SharedControlKind kind)
        {
            var t=Node(p,n,x,y,w,h);var b=Ensure<UnityEngine.UI.Button>(t);b.targetGraphic=Ensure<UnityEngine.UI.Image>(t);
            var label=Text(t,"Label",value,12,12,w-24,h-24,GameTypography.Tokens.Button,true);label.alignment=TextAlignmentOptions.Center;SharedUiStyles.Bind(b,kind);return b;
        }
        private static UnityEngine.UI.ScrollRect Scroll(Transform p,string n,float x,float y,float w,float h)
        {
            var r=Node(p,n,x,y,w,h);var scroll=Ensure<UnityEngine.UI.ScrollRect>(r);scroll.horizontal=false;scroll.vertical=true;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=48;
            var vp=Image(r,"Viewport",null,0,0,w-20,h,true);Ensure<UnityEngine.UI.Mask>(vp).showMaskGraphic=false;
            var content=Node(vp.transform,"Content",0,0,w-20,0);Vertical(content,4,16);Ensure<UnityEngine.UI.ContentSizeFitter>(content).verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;scroll.viewport=vp.rectTransform;scroll.content=content;
            var track=Image(r,"Scrollbar","UI_SCROLLBAR_TRACK",w-12,0,12,h);var area=Node(track.transform,"SlidingArea",0,0,12,h);var handle=Image(area,"Handle","UI_SCROLLBAR_THUMB",0,0,12,80,true);
            handle.rectTransform.anchorMin=Vector2.zero;handle.rectTransform.anchorMax=Vector2.one;handle.rectTransform.offsetMin=handle.rectTransform.offsetMax=Vector2.zero;
            var bar=Ensure<UnityEngine.UI.Scrollbar>(track);bar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;bar.handleRect=handle.rectTransform;bar.targetGraphic=handle;bar.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;return scroll;
        }
        private static void Retire(Transform panel)
        {
            foreach(Transform t in panel) {if(t.name=="PanelReset")continue;t.gameObject.SetActive(false);foreach(var g in t.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))g.raycastTarget=false;}
        }

        [MenuItem("Tools/Jogos de Empresa/UI-08/Migrate establishment and review")]
        public static void Apply()
        {
            if(UnityEngine.Application.dataPath!="D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying)throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/GameScene.unity")throw new InvalidOperationException("Open GameScene.");
            var canvas=scene.GetRootGameObjects().Single(g=>g.name=="Canvas").transform;
            var registry=canvas.Find("UiNavigation").GetComponent<PanelRegistry>();
            Build((RectTransform)canvas.Find("Panel_Establishment"),false);
            Build((RectTransform)canvas.Find("Panel_Review"),true);
            registry.EditorSetBinding(PanelId.Review,canvas.Find("Panel_Review").gameObject);EditorUtility.SetDirty(registry);
            registry.EditorSetBinding(PanelId.Location,canvas.Find("Panel_Location").gameObject);
            registry.EditorSetBinding(PanelId.Restaurant,canvas.Find("Panel_Restaurant").gameObject);
            Font06Migration.Apply(new[] { canvas.Find("Panel_Establishment").gameObject, canvas.Find("Panel_Review").gameObject }, "Assets/Scenes/GameScene.unity");
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        private static void Build(RectTransform panel,bool review)
        {
            Undo.RegisterFullObjectHierarchyUndo(panel.gameObject,"UI-08 ART-27 composition");
            var view=panel.GetComponent<EstablishmentSummaryView>();
            if(review && view!=null){Undo.DestroyObjectImmediate(view);view=null;}
            if(!review)view=Ensure<EstablishmentSummaryView>(panel);
            var controller=Ensure<EstablishmentSummaryController>(panel);Set(controller,"view",view);
            var reset=view!=null?Get<UnityEngine.UI.Button>(view,"resetButton"):null;
            Retire(panel);var skin=Ensure<EstablishmentReviewContent>(panel);if(view!=null)Set(view,"presentation",skin);Set(controller,"reviewContent",review?skin:null);
            panel.anchorMin=Vector2.zero;panel.anchorMax=Vector2.one;panel.offsetMin=panel.offsetMax=Vector2.zero;
            panel.GetComponent<Game.Adapter.In.UI.Layout.SafeAreaFitter>()?.Recapture();
            var bg=Ensure<UnityEngine.UI.Image>(panel);bg.sprite=null;bg.color=GamePalette.Surface;bg.raycastTarget=true;
            var header=Image(panel,"OverviewHeader","UI_TOPBAR_VIOLET",0,0,1920,88);
            var logo=Image(header.transform,"Icon","ICON_COMMERCE",24,22,44,44);logo.color=GamePalette.OnDark;
            Text(header.transform,"Brand","Jogos de Empresa",88,18,700,60,GameTypography.Tokens.BrandTitle,true,GamePalette.OnDark);
            Set(skin,"round",Text(header.transform,"Round","Indicadores após a confirmação",1040,28,856,40,GameTypography.Tokens.Body,false,GamePalette.OnDark));
            Text(panel,"OverviewTitle",review?"Revisão das escolhas":"Meu estabelecimento",24,112,1220,60,GameTypography.Tokens.ScreenTitle,true);
            Set(skin,"context",Text(panel,"Context","Nenhuma escolha disponível",24,172,1200,32,GameTypography.Tokens.Label));
            var badge=Image(panel,"Readiness","UI_PILL_NORMAL",1376,124,520,56);Set(skin,"status",Text(badge.transform,"Label","Escolhas pendentes",16,12,488,40,GameTypography.Tokens.BodyStrong,true));
            var left=Image(panel,"FacadeTeamPanel","UI_PANEL_IVORY",24,208,784,752);
            var right=Image(panel,"MetricsReviewPanel","UI_PANEL_IVORY",832,208,1064,752);
            var leftScroll=Scroll(left.transform,"BusinessScroll",24,24,736,704);var lc=leftScroll.content;Vertical(lc,4,8);
            var selected=Ensure<SelectedEstablishmentView>(lc);Set(skin,"selected",selected);
            Set(selected,"companyText",Flow(lc,"Company","Meu estabelecimento",GameTypography.Tokens.SectionTitle,true));
            Set(selected,"selectedText",Flow(lc,"Restaurant","Restaurante pendente",GameTypography.Tokens.BodyStrong,true));
            Set(selected,"locationText",Flow(lc,"Location","Localização pendente",GameTypography.Tokens.Label));
            Set(selected,"segmentText",null);if(lc.Find("Audience")!=null)lc.Find("Audience").gameObject.SetActive(false);
            var facade=Image(lc,"Facade",null,0,0,708,240);facade.preserveAspect=true;facade.rectTransform.pivot=new Vector2(.5f,.5f);Height(facade,240);Set(selected,"facade",facade);
            var empty=Flow(facade.transform,"Empty","Escolha um restaurante para visualizar a fachada.",GameTypography.Tokens.Body);Ignore(empty);var er=empty.rectTransform;er.anchorMin=Vector2.zero;er.anchorMax=Vector2.one;er.offsetMin=new Vector2(24,24);er.offsetMax=new Vector2(-24,-24);empty.alignment=TextAlignmentOptions.Center;Set(selected,"emptyText",empty);
            Flow(lc,"TeamTitle","Equipe contratada",GameTypography.Tokens.SectionTitle,true);
            Set(skin,"teamEmpty",Flow(lc,"TeamEmpty","Nenhuma contratação registrada. Consulte Equipe para montar seu time.",GameTypography.Tokens.Body));
            var grid=Node(lc,"TeamStrip");Height(grid,252);Set(skin,"teamGrid",grid);var gl=Ensure<UnityEngine.UI.GridLayoutGroup>(grid);gl.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;gl.constraintCount=3;gl.cellSize=new Vector2(220,240);gl.spacing=new Vector2(12,12);
            var so=new SerializedObject(skin);var members=so.FindProperty("members");members.arraySize=5;
            for(int i=0;i<5;i++)
            {
                var card=Image(grid,"Member"+i,"UI_CARD_NORMAL",0,0,220,240);var env=Image(card.transform,"Environment",null,8,8,204,164);env.preserveAspect=true;
                var portrait=Image(card.transform,"Portrait",null,8,8,204,164);portrait.preserveAspect=true;
                foreach(var picture in new[]{env,portrait}){picture.rectTransform.pivot=new Vector2(.5f,.5f);picture.rectTransform.anchoredPosition=new Vector2(110,-90);}
                var label=Text(card.transform,"Name","",8,180,204,56,GameTypography.Tokens.Caption,true);label.alignment=TextAlignmentOptions.Center;
                var item=members.GetArrayElementAtIndex(i);item.FindPropertyRelative("root").objectReferenceValue=card.gameObject;item.FindPropertyRelative("portrait").objectReferenceValue=portrait;item.FindPropertyRelative("environment").objectReferenceValue=env;item.FindPropertyRelative("label").objectReferenceValue=label;
            }
            var arts=so.FindProperty("roleArtwork");arts.arraySize=5;string[] roleIds={"attendant","grill_cook","head_chef","manager","sushi_chef"};string[] envIds={"SERVICE_COUNTER","GRILL_KITCHEN","FRENCH_KITCHEN","MANAGEMENT_OFFICE","SUSHI_COUNTER"};
            for(int i=0;i<5;i++){var a=arts.GetArrayElementAtIndex(i);a.FindPropertyRelative("id").stringValue=roleIds[i];a.FindPropertyRelative("portrait").objectReferenceValue=Art("PORTRAIT_"+roleIds[i].ToUpperInvariant());a.FindPropertyRelative("environment").objectReferenceValue=Art("TEAM_ENV_"+envIds[i]);}so.ApplyModifiedPropertiesWithoutUndo();
            var rs=Scroll(right.transform,"ReviewScroll",24,24,1016,704);var rc=rs.content;
            var metrics=Node(rc,"Metrics");Height(metrics,248);var mg=Ensure<UnityEngine.UI.GridLayoutGroup>(metrics);mg.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;mg.constraintCount=2;mg.cellSize=new Vector2(482,112);mg.spacing=new Vector2(24,24);
            string[] fields={"cash","revenue","result","score"};string[] names={"Caixa disponível","Receita prevista","Resultado previsto","Pontuação"};string[] icons={"ICON_CASH","ICON_REVENUE","ICON_TREND_UP","ICON_STAR"};
            for(int i=0;i<4;i++)
            {
                var tile=Image(metrics,"Kpi"+i,"UI_KPI_TILE",0,0,482,112);var icon=Image(tile.transform,"Icon",icons[i],20,34,44,44);icon.color=i==2?GamePalette.Money:GamePalette.Primary;
                var title=Text(tile.transform,"Title",names[i],88,16,374,32,GameTypography.Tokens.Label,true);if(i==1)Set(skin,"revenueTitle",title);if(i==2)Set(skin,"resultTitle",title);
                Set(skin,fields[i],Text(tile.transform,"Value","A calcular",88,52,374,52,GameTypography.Tokens.DisplayValue,true));
            }
            var coherence=Node(rc,"Coherence");Height(coherence,80);Text(coherence,"Title","Coerência das escolhas",0,0,660,40,GameTypography.Tokens.BodyStrong,true);Set(skin,"coherence",Text(coherence,"State","A calcular",720,0,268,40,GameTypography.Tokens.Label));
            var track=Image(coherence,"Track","UI_PROGRESS_TRACK",0,56,988,16);var fill=Image(track.transform,"Fill",null,0,0,988,16);fill.color=GamePalette.Money;fill.rectTransform.anchorMin=Vector2.zero;fill.rectTransform.anchorMax=Vector2.one;fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;Set(skin,"scoreFill",fill.rectTransform);
            Flow(rc,"ChecklistTitle","Revisão das decisões",GameTypography.Tokens.SectionTitle,true);
            so=new SerializedObject(skin);var decisions=so.FindProperty("decisions");decisions.arraySize=6;
            var destinations=new[]{PanelId.Location,PanelId.Restaurant,PanelId.MenuPricing,PanelId.Financial,PanelId.EquipmentStore,PanelId.Team};
            for(int i=0;i<6;i++)
            {
                var button=Button(rc,"Decision"+i,"",0,0,988,120,SharedControlKind.Secondary);button.transform.Find("Label").gameObject.SetActive(false);
                var layout=Vertical(button,16,6);layout.padding.left=64;layout.padding.right=164;
                var title=Flow(button.transform,"Title","Decisão",GameTypography.Tokens.Label,true);var detail=Flow(button.transform,"Detail","Pendente",GameTypography.Tokens.Label);
                var check=Image(button.transform,"Check","UI_CHECKBOX_OFF",16,20,32,32);Ignore(check);
                var tick=Image(button.transform,"Tick","ICON_CHECK",20,24,24,24);tick.color=GamePalette.OnDark;Ignore(tick);
                var state=Text(button.transform,"State","Pendente",822,20,134,36,GameTypography.Tokens.Label,true);Ignore(state);
                var next=Image(button.transform,"Next","ICON_NEXT",940,66,28,28);next.color=GamePalette.Primary;Ignore(next);
                var focus=button.transform.Find("SharedFocus").GetComponent<UnityEngine.UI.Image>();Ignore(focus);
                button.GetComponent<SharedControlAppearance>().Configure(SharedControlKind.Secondary,button.GetComponent<UnityEngine.UI.Image>(),focus,null,null,new[]{title,detail},new[]{next});
                var d=decisions.GetArrayElementAtIndex(i);d.FindPropertyRelative("button").objectReferenceValue=button;d.FindPropertyRelative("check").objectReferenceValue=check;d.FindPropertyRelative("tick").objectReferenceValue=tick;d.FindPropertyRelative("title").objectReferenceValue=title;d.FindPropertyRelative("detail").objectReferenceValue=detail;d.FindPropertyRelative("state").objectReferenceValue=state;d.FindPropertyRelative("destination").enumValueIndex=(int)destinations[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Flow(rc,"MenuTitle","Cardápio e preços atuais",GameTypography.Tokens.SectionTitle,true);Set(skin,"menuDetails",Flow(rc,"MenuDetails","Nenhum preço definido.",GameTypography.Tokens.Body));
            Flow(rc,"EquipmentTitle","Equipamentos adquiridos",GameTypography.Tokens.SectionTitle,true);Set(skin,"equipmentDetails",Flow(rc,"EquipmentDetails","Nenhum equipamento adquirido.",GameTypography.Tokens.Body));
            Flow(rc,"TeamDetailsTitle","Contratações",GameTypography.Tokens.SectionTitle,true);Set(skin,"teamDetails",Flow(rc,"TeamDetails","Nenhuma contratação registrada.",GameTypography.Tokens.Body));
            Set(skin,"notice",Flow(rc,"Notice","Escolha os dados iniciais para consultar seus indicadores.",GameTypography.Tokens.Body));
            var footer=Image(panel,"OverviewFooter","UI_NAVBAR_PLUM",0,984,1920,96);
            if(!review)Set(skin,"reviewButton",Button(footer.transform,"ReviewChoices","Revisar escolhas",1120,14,380,68,SharedControlKind.Primary));
            if(reset!=null){reset.transform.SetParent(footer.transform,false);var r=Node(footer.transform,reset.name,328,14,364,68);foreach(Transform child in r)child.gameObject.SetActive(false);var layout=r.GetComponent<UnityEngine.UI.LayoutGroup>();if(layout!=null)layout.enabled=false;var icon=Image(r,"ResetIcon","ICON_RESET",16,18,32,32);icon.color=GamePalette.Ink;var label=Text(r,"ResetLabel","Reiniciar jogo",64,14,284,44,GameTypography.Tokens.Button,true);label.alignment=TextAlignmentOptions.Center;SharedUiStyles.Bind(reset,SharedControlKind.Secondary);r.Find("SharedFocus").gameObject.SetActive(true);r.gameObject.SetActive(true);}
            var warning=panel.Find("PanelReset") as RectTransform;
            if(warning!=null)
            {
                var surface=Image(warning,"Dialog","UI_MODAL_SURFACE",400,100,1120,880);
                foreach(Transform child in warning.Cast<Transform>().Where(t=>t!=surface.transform).ToArray())child.SetParent(surface.transform,false);
                warning.anchorMin=Vector2.zero;warning.anchorMax=Vector2.one;warning.offsetMin=warning.offsetMax=Vector2.zero;
                var veil=Ensure<UnityEngine.UI.Image>(warning);veil.sprite=null;veil.color=GamePalette.Parse(GamePalette.HexPlum,.6f);veil.raycastTarget=true;
                var cancel=Get<UnityEngine.UI.Button>(view,"cancelResetButton");var confirm=Get<UnityEngine.UI.Button>(view,"confirmResetButton");
                cancel.GetComponentInChildren<TMP_Text>(true).text="Cancelar";confirm.GetComponentInChildren<TMP_Text>(true).text="Reiniciar jogo";
                var confirmIcon=confirm.transform.Find("Image")?.GetComponent<UnityEngine.UI.Image>();if(confirmIcon!=null){confirmIcon.sprite=Art("ICON_RESET");confirmIcon.color=GamePalette.OnDark;confirmIcon.preserveAspect=true;}
                SharedUiStyles.Bind(confirm,SharedControlKind.Danger);warning.SetAsLastSibling();warning.gameObject.SetActive(false);
            }
            EditorUtility.SetDirty(skin);if(view!=null)EditorUtility.SetDirty(view);EditorUtility.SetDirty(controller);panel.gameObject.SetActive(false);
        }
    }
}
