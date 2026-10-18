using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using Newtonsoft.Json.Linq;

namespace Game.EditorTools
{
    /// <summary>Reproducible composition revision against the five approved concepts.</summary>
    public static class ConceptUiRevision
    {
        public const string Output = "output/ui-update/concept-revision";
        static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
        static Transform canvas;
        static System.Collections.Generic.Dictionary<string,string> art;
        static T Ensure<T>(Component c) where T:Component => c.GetComponent<T>() ?? c.gameObject.AddComponent<T>();
        static Transform Find(Transform p,string path) => p.Find(path) ?? throw new InvalidOperationException("Missing " + PathOf(p) + "/" + path);
        static void Place(Transform t,float x,float y,float w,float h)
        {
            var r=(RectTransform)t; r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
            r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); r.localScale=Vector3.one;
        }
        static void Move(Transform p,string path,float x,float y,float w,float h) => Place(Find(p,path),x,y,w,h);
        static Transform Node(Transform p,string n,float x,float y,float w,float h)
        {
            var t=p.Find(n); if(t==null) { t=new GameObject(n,typeof(RectTransform)).transform; t.SetParent(p,false); }
            Place(t,x,y,w,h); return t;
        }
        static void Off(Transform p,params string[] names) { foreach(var n in names) {var t=p.Find(n);if(t!=null)t.gameObject.SetActive(false);} }
        static void Set(UnityEngine.Object o,string n,UnityEngine.Object v)
        {var so=new SerializedObject(o);var f=so.FindProperty(n);if(f==null)throw new InvalidOperationException(o.name+" field "+n);f.objectReferenceValue=v;so.ApplyModifiedPropertiesWithoutUndo();}
        static UnityEngine.UI.Image Picture(Transform p,string n,string id,float x,float y,float w,float h)
        {
            var i=Ensure<UnityEngine.UI.Image>(Node(p,n,x,y,w,h)); i.sprite=id==null?null:AssetDatabase.LoadAssetAtPath<Sprite>(art[id]);
            i.color=Color.white;i.raycastTarget=false;i.preserveAspect=i.sprite!=null&&i.sprite.border==Vector4.zero;
            i.type=i.sprite!=null&&i.sprite.border!=Vector4.zero?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple; return i;
        }
        static TMP_Text Label(Transform p,string n,string value,float x,float y,float w,float h,TypographyToken size = default,bool bold=false,Color? color=null)
        {
            var t=Ensure<TextMeshProUGUI>(Node(p,n,x,y,w,h));
            GameTypography.Apply(t,size.Size > 0 ? size : (TypographyToken)24,bold);
            t.text=value;t.color=color??GamePalette.Plum;t.raycastTarget=false;t.alignment=TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Overflow;return t;
        }
        static void Type(Transform p,string n,TypographyToken size,bool bold=true)
        {var t=Find(p,n).GetComponent<TMP_Text>();FontRoleMigration.TryApply(t);GameTypography.Apply(t,size.Size > 0 ? size : (TypographyToken)24,bold);}
        static void Height(Component c,float value)
        {var e=Ensure<UnityEngine.UI.LayoutElement>(c);e.minHeight=e.preferredHeight=value;e.flexibleHeight=0;}
        static void ResizeScroll(Transform p,string path,float x,float y,float w,float h)
        {
            var t=Find(p,path);Place(t,x,y,w,h);var s=t.GetComponent<UnityEngine.UI.ScrollRect>();
            Place(s.viewport,0,0,w-20,h);s.content.anchorMin=new Vector2(0,1);s.content.anchorMax=new Vector2(1,1);s.content.sizeDelta=new Vector2(0,s.content.sizeDelta.y);
            if(t.Find("Scrollbar")!=null)Move(t,"Scrollbar",w-12,0,12,h);
            if(t.Find("Scrollbar/SlidingArea")!=null)Move(t,"Scrollbar/SlidingArea",0,0,12,h);
        }
        static void Header(Transform p,string path)
        {
            var h=Find(p,path);Place(h,0,0,1920,88);
            Off(h,"Brand","Title","GameTitle","GameSubtitle","Step","Context","Period","Round","RegionText","TopBar");
            Picture(h,"ConceptMark","ICON_COMMERCE",24,16,54,54).color=GamePalette.OnDark;
            Label(h,"ConceptBrand","Jogos de Empresa",100,12,480,64,new TypographyToken(TypographyRole.Brand,44),true,GamePalette.OnDark);
            Label(h,"ConceptSubtitle","GESTÃO DE RESTAURANTES",600,22,400,44,new TypographyToken(TypographyRole.Caption,22),false,GamePalette.OnDark);
            var hud=Ensure<ConceptHudView>(h);
            Set(hud,"context",Find(h,"ConceptSubtitle").GetComponent<TMP_Text>());
            string setup=PathOf(p).Contains("Panel_Identification")?"PASSO 1 DE 7 · IDENTIFICAÇÃO":p.name switch {
                "Panel_Location"=>"PASSO 2 DE 7 · LOCALIZAÇÃO","Panel_Restaurant"=>"PASSO 3 DE 7 · RESTAURANTE",
                "Panel_MenuPricing"=>"PASSO 4 DE 7 · CARDÁPIO","Panel_Financial"=>"PASSO 5 DE 7 · BANCO",
                "Panel_Equipment"=>"PASSO 6 DE 7 · EQUIPAMENTOS","Panel_Team"=>"PASSO 7 DE 7 · EQUIPE",_=>null};
            var contextSo=new SerializedObject(hud);contextSo.FindProperty("setupContext").stringValue=setup;contextSo.ApplyModifiedPropertiesWithoutUndo();
            string[] names={"Month","Cash","Score"};string[] icons={"ICON_CALENDAR","ICON_CASH","ICON_STAR"};
            float[] xx={1080,1314,1600};float[] ww={220,272,144};string[] fields={"month","cash","score"};
            for(int i=0;i<3;i++)
            {
                var pill=Picture(h,"Concept"+names[i],"UI_HUD_PILL",xx[i],18,ww[i],52);
                Picture(pill.transform,"Icon",icons[i],12,12,28,28).color=GamePalette.Lavender;
                Set(hud,fields[i],Label(pill.transform,"Value","",50,5,ww[i]-62,42,new TypographyToken(TypographyRole.Kpi,23),true,GamePalette.OnDark));
            }
        }
        static void Map(Transform p)
        {
            Off(p,"MapHeading","MapHint","Region","SelectionHint","AvailableLegend","AvailableLegendLabel","SelectedLegend","SelectedLegendLabel");
            var master=Find(p,"MapMaster");Place(master,-104,88,1920,1080);master.localScale=Vector3.one*(896f/1080f);
            // Clip the city as one composition, keeping all polygons and art in the same transform.
            var clip=Ensure<UnityEngine.UI.RectMask2D>(master);
            clip.padding=new Vector4(125.36f,0,145.72f,0);
            foreach(Transform node in master)
            {
                if(!node.name.StartsWith("Node_"))continue;
                var card=node.Find("Card");if(card==null)continue;
                if(node.name.Contains("Store")||node.name.Contains("MenuPricing"))
                    foreach(var n in new[]{"MapLabel","MapCaption","MapFocus","MapLabelTip","MarkerTouchTarget"})
                    {var t=card.Find(n) as RectTransform;if(t!=null){
                        float xx=n=="MapCaption"?1296:n=="MapFocus"?1268:n=="MapLabelTip"?1548:n=="MarkerTouchTarget"?1256:1272;
                        t.anchoredPosition=new Vector2(xx,t.anchoredPosition.y);
                    }}
            }
            Font05Migration.ApplyGeometry(new[]{p.gameObject});
        }
        static void Identification()
        {
            var p=Find(canvas,"Panel_Identification/DesignFrame/Content"); Header(p,"IdentificationHeader");
            Move(p,"CH01_WELCOME",-140,110,1100,1650); Move(p,"Popup",1160,132,712,880);
            Ensure<UnityEngine.UI.RectMask2D>(p);
            var f=Find(p,"Popup");Off(f,"Intro","NameLimits","RALimits","CognaLogo");
            Move(f,"Title",60,48,592,104);Type(f,"Title",new TypographyToken(TypographyRole.ScreenTitle,72));
            Move(f,"InputName",60,198,592,124);Move(f,"InputName/InputName",0,44,592,80);
            Move(f,"InputRA",60,394,592,124);Move(f,"InputRA/InputRA",0,44,592,80);
            Move(f,"Validation",60,548,592,80);Move(f,"ButtonRegister",60,660,592,100);
            Type(f,"ButtonRegister/Text (TMP)",new TypographyToken(TypographyRole.Button,36));
            foreach(var field in f.GetComponentsInChildren<TMP_InputField>(true))
            {
                GameTypography.Apply(field.textComponent,TypographyRole.Input,30);
                GameTypography.Apply(field.placeholder as TMP_Text,TypographyRole.Input,30);
            }
            foreach(var path in new[]{"InputName/Nome","InputRA/RA"}) {Type(f,path,new TypographyToken(TypographyRole.Label,30),false);Height(Find(f,path),40);}
            Move(f,"StorageNote",60,804,592,36);
        }
        static void Location()
        {
            var p=Find(canvas,"Panel_Location");Header(p,"DecisionHeader");Map(p);
            Move(p,"DecisionSidebar",1368,96,544,880);
            ResizeScroll(p,"DecisionSidebar/Details",20,16,504,730);
            var c=Find(p,"DecisionSidebar/Details").GetComponent<UnityEngine.UI.ScrollRect>().content;
            Height(Find(c,"Vignette"),248);Type(c,"ZoneTitle",36);
            Find(c,"ZoneTitle").SetAsFirstSibling();Find(c,"Vignette").SetSiblingIndex(1);
            // Footer children are positioned relative to the footer.
            var b=p.Find("DecisionFooter/Confirm")??Find(p,"DecisionSidebar/Confirm");b.SetParent(Find(p,"DecisionSidebar"),false);b.name="Confirm";Place(b,24,770,496,76);
        }
        static void Restaurant()
        {
            var p=Find(canvas,"Panel_Restaurant");Header(p,"DecisionHeader");Off(p,"ChoiceTitle","SelectionHint");
            string[] cards={"Card_Podrao","Card_Japones","Card_Frances"};
            for(int i=0;i<3;i++)
            {
                var c=Find(p,cards[i]);Place(c,24+i*632,116,608,666);
                Off(c,"TypeIcon","OptionLabel","SelectionStatus");
                Move(c,"Facade",16,16,576,432);Move(c,"SelectedCheck",544,20,44,44);
                var type=i==0?RestaurantType.PODRAO:i==1?RestaurantType.JAPONES:RestaurantType.FRANCES;
                Find(c,"Facade").GetComponent<UnityEngine.UI.Image>().sprite=Resources.LoadAll<RestaurantData>("Restaurants").Single(r=>r.type==type).facade;
                ResizeScroll(c,"CardDetails",24,464,560,180);
                var content=Find(c,"CardDetails").GetComponent<UnityEngine.UI.ScrollRect>().content;
                Type(content,"RestaurantName",32);var lg=content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();lg.spacing=0;
                foreach(var n in new[]{"RestaurantName","RestaurantDesc","CostLabel","MonthlyCost","TicketLabel","TicketRange"})
                {
                    var t=Find(content,n);TypographyToken size=n=="RestaurantName"?32:n.EndsWith("Label")?20:24;
                    Type(content,n,size,n=="RestaurantName"||n=="MonthlyCost"||n=="TicketRange");Height(t,n=="RestaurantName"?40:n.EndsWith("Label")?24:28);
                }
                Picture(c,"FeaturedDish",i==0?"PRODUCT_LAN_COMBO_SIMPLES":i==1?"PRODUCT_JAP_SUSHI_EXECUTIVO":"PRODUCT_FRA_PRATO_EXECUTIVO",320,282,250,172);
            }
            var side=Find(p,"DecisionSidebar");Place(side,24,800,1240,160);
            Move(side,"Title",20,8,650,40);Type(side,"Title",28);Off(side,"Help");
            string[] buttons={"Button_Low","Button_Medium","Button_Segment"};
            for(int i=0;i<3;i++) {
                Move(side,buttons[i],20+i*398,60,378,76);var b=Find(side,buttons[i]);
                Move(b,"SegmentLabel",56,8,304,36);Move(b,"Availability",56,42,304,28);Move(b,"Radio",16,24,28,28);
            }
            // Compatibility remains accessible below the choices, using its existing scroll.
            ResizeScroll(side,"Compatibility",1264,-684,608,844);
            var compat=Find(side,"Compatibility");compat.gameObject.SetActive(false);
            var surface=Ensure<UnityEngine.UI.Image>(compat);surface.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(art["UI_PANEL_IVORY"]);surface.type=UnityEngine.UI.Image.Type.Sliced;surface.color=Color.white;
            var toggle=Node(side,"CompatibilityToggle",884,8,336,44);var button=Ensure<UnityEngine.UI.Button>(toggle);button.targetGraphic=Ensure<UnityEngine.UI.Image>(toggle);
            Label(toggle,"Label","Ver compatibilidade",12,0,312,44,22,true);SharedUiStyles.Bind(button,SharedControlKind.Secondary);Set(Ensure<ConceptDetailsToggle>(toggle),"details",compat.gameObject);
            var confirm=p.Find("DecisionFooter/Confirm")??Find(p,"Confirm");confirm.SetParent(p,false);Place(confirm,1312,860,560,84);
        }
        static void Menu()
        {
            var p=Find(canvas,"Panel_MenuPricing");Header(p,"DecisionHeader");
            Move(p,"Restaurant",32,112,1264,54);Type(p,"Restaurant",30);
            ResizeScroll(p,"Products",24,184,1280,744);
            Move(p,"DecisionSidebar",1328,112,568,848);
            // Keep the native strategy controls, but give the compact summary room to breathe.
            var s=Find(p,"DecisionSidebar/Strategy").GetComponent<UnityEngine.UI.ScrollRect>();
            s.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing=16;
            Off(p,"ValidationHint");
        }
        static void Catalogs()
        {
            var p=Find(canvas,"Panel_Equipment");Header(p,"Header");
            Move(p,"TitleText",32,112,900,60);Type(p,"TitleText",44);Off(p,"ContextText");
            Move(p,"Guidance",32,174,1856,26);Type(p,"Guidance",20,false);
            Move(p,"MainArea_Equipment",24,200,1872,768);var body=Find(p,"MainArea_Equipment");
            for(int i=0;i<4;i++)Move(body,"Filter"+i,840+i*256,-88,240,60);
            Off(body,"ResultCount");ResizeScroll(body,"CatalogScroll",0,0,1352,768);
            var grid=Find(body,"CatalogScroll").GetComponent<UnityEngine.UI.ScrollRect>().content.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.constraintCount=3;grid.cellSize=new Vector2(418,704);grid.spacing=new Vector2(22,24);
            Move(body,"Summary",1376,0,496,768);var summary=Find(body,"Summary");
            Type(summary,"Title",36);Picture(summary,"CartIcon","ICON_CART",24,24,40,40).color=GamePalette.Primary;
            Move(summary,"Title",80,22,380,52);
            Move(summary,"Empty",32,264,432,256);Move(summary,"Empty/Artwork",116,0,200,134);
            Move(summary,"Empty/Message",0,150,432,90);
            ResizeScroll(summary,"SummaryScroll",24,200,448,278);
            Prefab("Assets/Prefabs/Card_equipment.prefab",r=>{
                ((RectTransform)r).sizeDelta=new Vector2(418,704);
                Move(r,"EquipmentName",24,24,370,64);Type(r,"EquipmentName",28);
                Move(r,"CategoryChip",24,100,370,36);Move(r,"CategoryChip/Label",8,0,354,36);
                Move(r,"EquipmentArtwork",24,136,370,260);Move(r,"EquipmentPrice",24,408,370,44);Type(r,"EquipmentPrice",30);
                Move(r,"Description",24,456,370,60);Type(r,"Description",21,false);
                Move(r,"Status",24,526,370,36);Move(r,"Remove",24,588,68,68);Move(r,"Quantity",100,588,214,68);Move(r,"Add",326,588,68,68);
            });
            p=Find(canvas,"Panel_Team");Header(p,"Header");Off(p,"ContextText","Guidance");
            Move(p,"TitleText",32,108,1100,64);Find(p,"TitleText").GetComponent<TMP_Text>().text="Contrate sua equipe";
            Move(p,"Body",24,192,1872,776);body=Find(p,"Body");ResizeScroll(body,"CatalogScroll",0,0,1352,776);
            grid=Find(body,"CatalogScroll").GetComponent<UnityEngine.UI.ScrollRect>().content.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            grid.constraintCount=3;grid.cellSize=new Vector2(418,704);grid.spacing=new Vector2(22,24);
            Move(body,"Summary",1376,0,496,776);
            Prefab("Assets/Prefabs/TeamRoleCard.prefab",r=>{
                ((RectTransform)r).sizeDelta=new Vector2(418,704);
                Move(r,"Name",20,20,378,48);Type(r,"Name",30);
                Move(r,"Environment",20,80,378,330);Move(r,"Portrait",20,80,378,330);
                Move(r,"Salary",20,422,378,40);Type(r,"Salary",27);
                Move(r,"Capacity",20,466,378,60);Type(r,"Capacity",21,false);
                Move(r,"Requirement",20,534,378,54);Type(r,"Requirement",20,false);
                Move(r,"Remove",20,600,68,68);Move(r,"Quantity",96,600,214,68);Move(r,"Add",322,600,68,68);
            });
        }
        static void Prefab(string path,Action<Transform> action)
        {var root=PrefabUtility.LoadPrefabContents(path);try{action(root.transform);Font06Migration.Apply(new[]{root},path);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
        static void Bank()
        {
            var p=Find(canvas,"Panel_Financial");Header(p,"Header");
            Move(p,"BankEnvironment",0,88,640,896);Move(p,"BankAdvisor",8,152,624,832);
            Move(p,"TitleText",672,108,700,64);Find(p,"TitleText").GetComponent<TMP_Text>().text="Opções de crédito";
            Move(p,"CapitalLabel",1380,108,220,32);Move(p,"CapitalValue",1380,144,220,42);Type(p,"CapitalValue",28);
            Move(p,"CashLabel",1632,108,264,32);Move(p,"CashValue",1632,144,264,42);Type(p,"CashValue",28);
            Move(p,"CreditHelp",600,112,52,52);
            ResizeScroll(p,"CreditOptionsScroll",664,208,1232,624);
            var c=Find(p,"CreditOptionsScroll").GetComponent<UnityEngine.UI.ScrollRect>().content;
            var oldLayout=c.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();if(oldLayout!=null)UnityEngine.Object.DestroyImmediate(oldLayout);
            var grid=Ensure<UnityEngine.UI.GridLayoutGroup>(c);grid.constraint=UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=4;
            grid.cellSize=new Vector2(290,660);grid.spacing=new Vector2(12,16);grid.padding=new RectOffset(4,4,4,4);
            Move(p,"CreditDetails",664,852,1232,116);Move(p,"CreditDetails/Availability",20,8,1192,40);Move(p,"CreditDetails/Summary",20,52,1192,56);
            Prefab("Assets/Prefabs/Card_loan.prefab",r=>{
                ((RectTransform)r).sizeDelta=new Vector2(290,660);
                Move(r,"CreditArtwork",54,24,182,152);Move(r,"Name",16,190,258,60);Type(r,"Name",28);
                Move(r,"Principal",16,262,258,42);Type(r,"Principal",27);
                foreach(var n in new[]{"Interest","Term","Installment","Description","Availability"}) {Move(r,n,16,320,258,n=="Installment"||n=="Availability"?64:80);Type(r,n,20,false);}
                Move(r,"SelectionMark",240,14,34,34);
            });
        }
        static void Hub()
        {
            var p=Find(canvas,"Panel_Menu");Header(p,"Header");Map(p);
            var oldTop=Find(p,"Header/TopBar");oldTop.gameObject.SetActive(true);
            foreach(Transform child in oldTop)child.gameObject.SetActive(false);
            var side=Find(p,"HubSummary");Place(side,1368,100,544,872);
            var image=side.GetComponent<UnityEngine.UI.Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(art["UI_PANEL_IVORY"]);image.color=Color.white;
            Off(side,"Caption","Help");Label(side,"Title","MEU ESTABELECIMENTO",28,16,488,60,34,true);
            Move(side,"Company",28,384,488,88);Move(side,"Restaurant",28,476,488,32);Move(side,"LocationIcon",28,512,26,26);Move(side,"Location",64,512,450,32);
            var hud=Ensure<ConceptHudView>(side);Set(hud,"facade",Picture(side,"Facade",null,28,92,488,288));
            string[] labels={"Receita prevista","Resultado previsto","Reputação"};string[] ids={"ICON_REVENUE","ICON_TREND_UP","ICON_STAR"};string[] fields={"revenue","result","reputation"};
            for(int i=0;i<3;i++)
            {
                Picture(side,"MetricIcon"+i,ids[i],28,558+i*48,32,32).color=i==1?GamePalette.Money:GamePalette.Primary;
                Label(side,"MetricLabel"+i,labels[i],76,552+i*48,230,44,GameTypography.Tokens.Label);
                var value=Label(side,"MetricValue"+i,"",298,552+i*48,218,44,new TypographyToken(TypographyRole.Kpi,26),true,i==1?GamePalette.Money:GamePalette.Plum);value.alignment=TextAlignmentOptions.MidlineRight;Set(hud,fields[i],value);
            }
            Move(side,"Progress",28,696,488,28);Move(side,"ProgressTrack",28,730,488,16);
            Move(side,"Establishment",28,750,236,48);Move(side,"MonthlyResult",280,750,236,48);
            Move(side,"QuarterlyResult",28,808,236,48);Find(side,"QuarterlyResult").gameObject.SetActive(true);
            Find(side,"Establishment/Label").GetComponent<TMP_Text>().text="Restaurante";
            Find(side,"MonthlyResult/Label").GetComponent<TMP_Text>().text="Ver DRE";
            var actions=Find(p,"EventActions");Place(actions,1512,998,384,68);
            Move(actions,"CloseMonth",0,0,384,68);
            var history=actions.Find("History")??side.Find("History");history.SetParent(side,false);Place(history,280,808,236,48);history.gameObject.SetActive(true);
            foreach(var buttonName in new[]{"Establishment","MonthlyResult","QuarterlyResult","History"})
            {
                var b=Find(side,buttonName);var label=b.GetComponentInChildren<TMP_Text>(true);Place(label.transform,44,4,180,40);label.fontSize=22;
                var icon=b.Find("Icon");if(icon!=null)Place(icon,10,12,24,24);
            }
            Find(side,"QuarterlyResult/Label").GetComponent<TMP_Text>().text="Trimestre";
            SharedUiStyles.Bind(Find(actions,"CloseMonth").GetComponent<UnityEngine.UI.Button>(),SharedControlKind.Accent);
            var nav=Find(p,"HubNavbar");Off(nav,"NavMenu");
            string[] names={"NavEstablishment","NavFinancial","NavEquipmentStore","NavTeam","NavMenuPricing"};
            for(int i=0;i<names.Length;i++) {Move(nav,names[i],24+i*292,14,276,68);SharedUiStyles.Bind(Find(nav,names[i]).GetComponent<UnityEngine.UI.Button>(),SharedControlKind.Navigation);}
        }
        static void ImportStreetViews()
        {
            foreach(var name in new[]{"lanches","japones","frances"})
            {
                string path="Assets/Art/new/restaurant/street-views/facade_street_"+name+".png";
                AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
                var data=Resources.LoadAll<RestaurantData>("Restaurants").Single(r=>r.type==(name=="lanches"?RestaurantType.PODRAO:name=="japones"?RestaurantType.JAPONES:RestaurantType.FRANCES));
                data.facade=AssetDatabase.LoadAssetAtPath<Sprite>(path);EditorUtility.SetDirty(data);
            }
        }
        static void Establishments()
        {
            foreach(var name in new[]{"Panel_Establishment","Panel_Review"})
            {
                var p=Find(canvas,name);Header(p,"OverviewHeader");Off(p,"OverviewTitle","Context","Readiness");
                Move(p,"FacadeTeamPanel",0,100,864,872);ResizeScroll(p,"FacadeTeamPanel/BusinessScroll",16,16,832,840);
                var c=Find(p,"FacadeTeamPanel/BusinessScroll").GetComponent<UnityEngine.UI.ScrollRect>().content;
                Find(c,"Facade").SetAsFirstSibling();Height(Find(c,"Facade"),560);
                Move(p,"MetricsReviewPanel",880,100,1016,872);ResizeScroll(p,"MetricsReviewPanel/ReviewScroll",16,16,984,840);
                c=Find(p,"MetricsReviewPanel/ReviewScroll").GetComponent<UnityEngine.UI.ScrollRect>().content;
                var grid=Find(c,"Metrics").GetComponent<UnityEngine.UI.GridLayoutGroup>();grid.cellSize=new Vector2(464,124);grid.spacing=new Vector2(16,16);Height(grid,264);
                for(int i=0;i<4;i++){Move(c,"Metrics/Kpi"+i+"/Title",80,16,364,32);Move(c,"Metrics/Kpi"+i+"/Value",80,54,364,52);Type(c,"Metrics/Kpi"+i+"/Value",36);}
                for(int i=0;i<6;i++)Height(Find(c,"Decision"+i),96);
            }
        }
        static void Results()
        {
            foreach(var name in new[]{"Panel_MonthlyResult","Panel_QuarterlyResult"})
            {
                bool monthly=name.Contains("Monthly");var p=Find(canvas,name);Header(p,"Header");
                Move(p,"Title",40,108,1224,68);Type(p,"Title",46);
                if(monthly)
                {
                    var period=p.Find("Header/Period")??p.Find("SavedPeriod");period.SetParent(p,false);period.name="SavedPeriod";period.gameObject.SetActive(true);Place(period,40,170,1224,30);
                    var t=period.GetComponent<TMP_Text>();t.fontSize=20;t.color=GamePalette.Muted;
                }
                var loaded=Find(p,"LoadedBody");
                for(int i=0;i<3;i++)
                {
                    Move(loaded,"Kpi"+i,40+i*414,204,400,112);
                    Move(loaded,"Kpi"+i+"/Label",90,12,294,36);Move(loaded,"Kpi"+i+"/Value",90,50,294,52);Type(loaded,"Kpi"+i+"/Value",38);
                    Picture(Find(loaded,"Kpi"+i),"ConceptIcon",i==0?"ICON_REVENUE":i==1?"ICON_TREND_UP":"ICON_CASH",22,32,52,52).color=i==1?GamePalette.Money:GamePalette.Primary;
                }
                Move(p,monthly?"MonthlyIllustration":"QuarterlyIllustration",1312,100,584,462);
                // Scale the complete layered illustration from its approved clip, preserving aspect.
                var ill=Find(p,monthly?"MonthlyIllustration":"QuarterlyIllustration");
                var contract=JObject.Parse(File.ReadAllText("docs/art/layouts/dre-layout.json"));
                var clip=monthly?contract["clip_rect_px"]:contract["quarterly"]["clip_rect_px"];float oldW=(float)clip[2],oldH=(float)clip[3];
                Place(ill,1312,100,oldW,oldH);ill.localScale=Vector3.one*Mathf.Min(584/oldW,462/oldH);
                Move(loaded,"TableHeader",40,340,1224,56);
                ResizeScroll(loaded,"TableScroll",40,400,1244,560);
                if(!monthly){Move(loaded,"AggregationNote",40,166,1224,30);Type(loaded,"AggregationNote",18,false);}
                var chart=monthly?(loaded.Find("CashChart")??Find(loaded,"TableScroll/Viewport/Content/CashChart")):Find(loaded,"ResultChart");Place(chart,1312,576,584,384);
                Type(chart,"Title",28);Move(chart,"Title",20,14,544,44);
                // Financial details remain scrollable; table spacing is compact enough for ten monetary lines.
                var scroll=Find(loaded,"TableScroll").GetComponent<UnityEngine.UI.ScrollRect>();scroll.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing=2;
                for(int i=0;i<(monthly?22:25);i++)
                {
                    var row=Find(scroll.content,"Row"+i);
                    if(monthly){Height(row,48);Type(row,"Label",24,false);Type(row,"Value",24,i==9||i==11);Off(row,"TotalColumn");}
                    else {
                        Type(row,"Label",22,true);Type(row,"Rule",18,false);
                        float[] xx={304,464,624,784,1008};float[] ww={160,160,160,224,224};
                        for(int j=0;j<5;j++){
                            Move(row,"Cell"+j,xx[j]+6,10,ww[j]-12,48);Type(row,"Cell"+j,20,j==3);
                            Move(loaded,"TableHeader/Column"+j,xx[j]+6,8,ww[j]-12,40);
                        }
                        Move(row,"AggregateColumn",784,0,224,54);
                        var total=(RectTransform)Find(row,"AggregateColumn");total.anchorMin=new Vector2(0,0);total.anchorMax=new Vector2(0,1);total.offsetMin=new Vector2(784,0);total.offsetMax=new Vector2(1008,0);
                    }
                }
                if(monthly)
                {
                    chart.SetParent(scroll.content,false);Height(chart,396);
                    var bridge=Picture(loaded,"CashBridge","UI_PANEL_IVORY",1312,576,584,384).transform;
                    Picture(bridge,"Icon","ICON_CASH",20,16,44,44).color=GamePalette.Plum;
                    Label(bridge,"Title","Do resultado ao caixa",80,12,484,52,32,true);
                    var view=p.GetComponent<MonthlyResultView>();
                    string[] titles={"Caixa inicial","Resultado do mês","Caixa final","Variação de caixa"};
                    string[] fields={"openingValue","bridgeResultValue","closingValue","cashChangeValue"};
                    for(int j=0;j<4;j++)
                    {
                        if(j>=2)Picture(bridge,"Row"+j,j==2?"UI_TABLE_ROW_SUBTOTAL":"UI_TABLE_ROW_POSITIVE",16,82+j*58,552,54);
                        Label(bridge,"Label"+j,titles[j],30,82+j*58,270,50,24,j>=2);
                        var val=Label(bridge,"Value"+j,"",290,82+j*58,260,50,28,true);val.alignment=TextAlignmentOptions.MidlineRight;Set(view,fields[j],val);
                    }
                    Label(bridge,"Note","A parcela já está incluída no resultado.",30,324,524,44,19);
                    Label(Find(loaded,"Kpi0"),"Label","Receita",90,12,294,36,24);
                    Label(Find(loaded,"Kpi1"),"Label","Resultado",90,12,294,36,24);
                    Set(view,"revenueValue",Find(loaded,"Kpi0/Value").GetComponent<TMP_Text>());
                    Set(view,"resultValue",Find(loaded,"Kpi1/Value").GetComponent<TMP_Text>());
                    Set(view,"cashValue",Find(bridge,"Value2").GetComponent<TMP_Text>());
                    Find(loaded,"TableHeader/Label").GetComponent<TMP_Text>().text="Demonstrativo do mês";
                }
                else
                {
                    var so=new SerializedObject(p.GetComponent<QuarterlyResultView>());so.FindProperty("verticalChart").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();so.FindProperty("compactRows").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
                    Off(chart,"Zero","Note");
                    for(int j=0;j<3;j++){Move(chart,"BarValue"+j,34+j*166,80,176,36);Type(chart,"BarValue"+j,22);Move(chart,"BarLabel"+j,64+j*166,346,116,30);}
                }
            }
        }
        static void Secondary()
        {
            foreach(var name in new[]{"Panel_Event","Panel_FinalReport","Panel_Bankruptcy"})Header(Find(canvas,name),"Header");
            var p=Find(canvas,"Panel_Event");Move(p,"TitleText",40,112,1300,64);Type(p,"TitleText",46);
            Move(p,"Guidance",40,180,1840,36);Move(p,"DecisionBody",24,232,1872,736);
            p=Find(canvas,"Panel_Bankruptcy");Move(p,"BusinessDifficulty",24,180,560,380);Move(p,"ConcernedCharacter",100,480,400,490);
            NormalizeMenuAccess();
            // Global actions have a small consistent place in the header.
            foreach(var spec in new[]{("MenuAccess/Button","ICON_HOME",1760f,68f),("ExitUI/ExitButton","ICON_EXIT",1840f,56f)})
            {
                var b=Find(canvas,spec.Item1);Place(b,spec.Item3,16,spec.Item4,56);
                foreach(var t in b.GetComponentsInChildren<TMP_Text>(true))t.gameObject.SetActive(false);
                Picture(b,"ConceptIcon",spec.Item2,(spec.Item4-28)/2,14,28,28).color=GamePalette.OnDark;
                SharedUiStyles.Bind(b.GetComponent<UnityEngine.UI.Button>(),SharedControlKind.Primary);
            }
        }
        static void NormalizeMenuAccess()
        {
            var r=(RectTransform)Find(canvas,"MenuAccess");r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,.5f);
            r.localScale=Vector3.one;r.offsetMin=r.offsetMax=Vector2.zero;
        }
        public static void PolishAndExit()
        {
            try
            {
                var scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");canvas=scene.GetRootGameObjects().Single(g=>g.name=="Canvas").transform;
                NormalizeMenuAccess();var p=Find(canvas,"Panel_Equipment");Move(p,"Guidance",32,174,1856,26);Type(p,"Guidance",20,false);
                foreach(var c in Find(p,"Guidance").GetComponents<Component>())if(PrefabUtility.IsPartOfPrefabInstance(c))PrefabUtility.RecordPrefabInstancePropertyModifications(c);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [MenuItem("Tools/Jogos de Empresa/Concept/Apply composition revision")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before changing serialized layouts.");
            Directory.CreateDirectory(Output);var scene=EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            canvas=scene.GetRootGameObjects().Single(g=>g.name=="Canvas").transform;
            art=JObject.Parse(File.ReadAllText("docs/art/manifest.json"))["assets"].ToDictionary(a=>(string)a["id"],a=>"Assets/Art/new/"+(string)a["path"]);
            ImportStreetViews();Identification();Location();Restaurant();Menu();Catalogs();Bank();Hub();Establishments();Results();Secondary();
            Font06Migration.Apply(new[]{canvas.gameObject},"Assets/Scenes/GameScene.unity");
            foreach(var t in canvas.GetComponentsInChildren<TMP_Text>(true)) if((t.fontStyle&FontStyles.Bold)!=0)t.characterSpacing=0;
            // Prefab instances need explicit override recording for direct component property changes.
            foreach(var c in canvas.GetComponentsInChildren<Component>(true).Where(c=>c!=null))
                if(PrefabUtility.IsPartOfPrefabInstance(c)){EditorUtility.SetDirty(c);PrefabUtility.RecordPrefabInstancePropertyModifications(c);}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText(Output+"/implementation.json",JsonConvert.SerializeObject(new{date=DateTime.UtcNow,scene=scene.path,concepts=Directory.GetFiles("art-source/references/concepts","*.png"),scope="UI-01 through UI-20 composition review",verified=false},Formatting.Indented));
        }
        public static void ApplyAndExit()
        {try{Apply();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void AuditAndExit()
        {
            try
            {
                Directory.CreateDirectory(Output);
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
                var nodes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RectTransform>(true));
                File.WriteAllText(Output + "/baseline-hierarchy.json", JsonConvert.SerializeObject(nodes.Select(t => new {
                    path = PathOf(t), active = t.gameObject.activeSelf,
                    position = new[]{t.anchoredPosition.x,t.anchoredPosition.y}, size = new[]{t.sizeDelta.x,t.sizeDelta.y},
                    scale = new[]{t.localScale.x,t.localScale.y},
                    components = t.GetComponents<Component>().Where(c=>c!=null).Select(c=>c.GetType().Name),
                    text = t.GetComponent<TMP_Text>()?.text,
                    sprite = AssetDatabase.GetAssetPath(t.GetComponent<UnityEngine.UI.Image>()?.sprite)
                }), Formatting.Indented));
                EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
