using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Adapter.In.UI.Theme;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Targeted migration of the existing hub. Geometry comes directly from the approved contracts.</summary>
    public static class HubUiMigration
    {
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameTypography.FontAssetPath);
        private static Dictionary<string,string> art;
        private static Sprite Sprite(string id) => AssetDatabase.LoadAssetAtPath<Sprite>(art[id]);
        private static RectTransform Child(Transform parent,string name)
        {
            var found=parent.Find(name);
            if(found!=null) return (RectTransform)found;
            var child=new GameObject(name,typeof(RectTransform)); child.transform.SetParent(parent,false);
            return (RectTransform)child.transform;
        }
        private static void Rect(RectTransform r,float x,float y,float width,float height)
        {
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1); r.localScale=Vector3.one;
            r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(width,height);
        }
        private static void Stretch(RectTransform r)
        { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.pivot=new Vector2(.5f,.5f); r.localScale=Vector3.one; r.offsetMin=r.offsetMax=Vector2.zero; }
        private static T Ensure<T>(Component c) where T:Component => c.GetComponent<T>() ?? c.gameObject.AddComponent<T>();
        private static void Set(object component,string field,UnityEngine.Object value)
        {
            var so=new SerializedObject((UnityEngine.Object)component); so.FindProperty(field).objectReferenceValue=value; so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static UnityEngine.UI.Image Image(Transform parent,string name,string id,float x,float y,float w,float h,bool hit=false)
        {
            var r=Child(parent,name); Rect(r,x,y,w,h);
            r.gameObject.SetActive(true);
            var image=Ensure<UnityEngine.UI.Image>(r); image.sprite=id==null?null:Sprite(id); image.color=Color.white;
            image.raycastTarget=hit; image.preserveAspect=false; image.pixelsPerUnitMultiplier=1;
            image.type=image.sprite!=null && image.sprite.border!=Vector4.zero?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            return image;
        }
        private static TextMeshProUGUI Text(Transform parent,string name,string value,float x,float y,float w,float h,TypographyToken size,bool bold=false,Color? color=null)
        {
            var r=Child(parent,name); Rect(r,x,y,w,h); var text=Ensure<TextMeshProUGUI>(r);
            r.gameObject.SetActive(true);
            GameTypography.Apply(text,size,bold); text.text=value; text.color=color??GamePalette.Ink;
            text.alignment=TextAlignmentOptions.MidlineLeft; text.textWrappingMode=TextWrappingModes.Normal;
            text.overflowMode=TextOverflowModes.Overflow;
            return text;
        }
        private static UnityEngine.UI.Button Action(Transform parent,string name,string title,string icon,PanelId target,float x,float y,float width,PanelNavButton.NavAction action=PanelNavButton.NavAction.Open)
        {
            var image=Image(parent,name,"UI_SECONDARY_IVORY_NORMAL",x,y,width,68,true);
            var button=Ensure<UnityEngine.UI.Button>(image); button.targetGraphic=image;
            Image(image.transform,"Icon",icon,16,18,32,32).color=GamePalette.Ink;
            Text(image.transform,"Label",title,60,8,width-76,52,new TypographyToken(TypographyRole.Navigation,24),true);
            Ensure<PanelNavButton>(button).EditorConfigure(action,target);
            SharedUiStyles.Bind(button,SharedControlKind.Secondary);
            return button;
        }

        [MenuItem("Tools/Jogos de Empresa/UI-04/Migrate hub in GameScene")]
        public static void Apply()
        {
            if(UnityEngine.Application.dataPath.Replace('\\','/')!="D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying)
                throw new InvalidOperationException("Use the Cogna GameScene outside Play Mode.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene first.");
            var manifest=JObject.Parse(File.ReadAllText("docs/art/manifest.json"));
            art=manifest["assets"].ToDictionary(a=>(string)a["id"],a=>"Assets/Art/new/"+(string)a["path"]);
            var canvas=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).First(c=>c.isRootCanvas).transform;
            var panel=canvas.Find("Panel_Menu"); if(panel==null) throw new InvalidOperationException("Existing Panel_Menu required.");
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject,"UI-04 hub migration");
            // Preserve the previous nodes and callbacks; retire their old geometry and decoration.
            foreach(var name in new[]{"Backdrop","MapBackground","MapScrim"}) if(panel.Find(name)!=null) panel.Find(name).gameObject.SetActive(false);
            var master=Child(panel,"MapMaster"); Rect(master,24,176,1920,1080); master.localScale=Vector3.one*(2f/3f);
            var layout=JObject.Parse(File.ReadAllText("docs/art/layouts/map-layout.json"));
            var layers=new Dictionary<string,UnityEngine.UI.Image>();
            foreach(var layer in layout["layers"].OrderBy(l=>(int)l["order"]))
            {
                string id=(string)layer["asset_id"]; var sprite=Sprite(id);
                var image=Image(master,id,id,(float)layer["position_px"][0],(float)layer["position_px"][1],sprite.rect.width,sprite.rect.height);
                image.transform.SetAsLastSibling(); image.enabled=!id.StartsWith("OVERLAY_") && (!id.StartsWith("MAP_REST_") || id=="MAP_REST_FRANCES");
                layers[id]=image;
            }
            var selection=JObject.Parse(File.ReadAllText("docs/art/layouts/map-selection-compact.json"));
            var nodes=new List<MapMenuNode>();
            var specs=new[] {
                ("university","Node_Rh","Equipe",PanelId.Team,"ICON_UNIVERSITY"),
                ("bank","Node_Bank","Banco",PanelId.Financial,"ICON_BANK"),
                ("store","Node_Store","Equipamentos",PanelId.EquipmentStore,"ICON_COMMERCE"),
                ("condominium","Node_Residential","Localização",PanelId.Establishment,"ICON_RESIDENTIAL"),
                ("marketing","Node_MenuPricing","Cardápio",PanelId.MenuPricing,"ICON_CORPORATE"),
                ("center","Node_Establishment","Meu restaurante",PanelId.Establishment,"ICON_RESTAURANT") };
            foreach(var spec in specs)
            {
                var old=panel.Find("MapLayer/"+spec.Item2+"/Card");
                var root=Child(master,spec.Item2);
                UnityEngine.UI.Button button;
                if(old!=null) { old.SetParent(root,false); button=old.GetComponent<UnityEngine.UI.Button>(); }
                else button=Ensure<UnityEngine.UI.Button>(Child(root,"Card"));
                Rect(root,0,0,1920,1080); Rect((RectTransform)button.transform,0,0,1920,1080);
                foreach(Transform child in button.transform)
                {
                    child.gameObject.SetActive(false);
                    foreach(var graphic in child.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) graphic.raycastTarget=false;
                }
                if(button.GetComponent<SharedControlAppearance>()!=null) UnityEngine.Object.DestroyImmediate(button.GetComponent<SharedControlAppearance>());
                var oldImage=button.GetComponent<UnityEngine.UI.Image>(); if(oldImage!=null) { oldImage.enabled=false; oldImage.raycastTarget=false; }
                button.transition=UnityEngine.UI.Selectable.Transition.None;
                var proposal=selection["marker_instance_proposal"]["nodes"].FirstOrDefault(n=>(string)n["catalog_id"]==spec.Item1);
                var zone=layout["zones"].FirstOrDefault(n=>(string)n["catalog_id"]==spec.Item1);
                var polygon=spec.Item1=="center"?layout["central_hit_area"]["polygon_px"]:zone["polygon_px"];
                Vector2 anchor=spec.Item1=="center"?new Vector2(1165,417):new Vector2((float)proposal["anchor_master_px"][0],(float)proposal["anchor_master_px"][1]);
                var area=Image(button.transform,"MasterPolygonHit",null,0,0,1920,1080,true); area.color=Color.clear;
                Ensure<MapPolygonHitArea>(area).Configure(polygon.Select(p=>new Vector2((float)p[0],(float)p[1])).ToArray());
                float px=anchor.x-40, py=anchor.y-74.375f;
                float labelWidth=spec.Item1=="center"?320:spec.Item1=="store"?280:260;
                var pin=Image(button.transform,"MapPin","UI_PIN_NORMAL",px,py,80,80);
                var icon=Image(button.transform,"MapIcon",spec.Item5,px+25,py+18.125f,30,30);
                var tip=Image(button.transform,"MapLabelTip","UI_MAP_LABEL_TIP",px+58,py+26,20,20);
                var label=Image(button.transform,"MapLabel","UI_MAP_LABEL_NORMAL",px+72,py-2,labelWidth,76);
                var caption=Text(button.transform,"MapCaption",spec.Item3,px+96,py+14,labelWidth-48,44,new TypographyToken(TypographyRole.MapMarker,32),true);
                var focus=Image(button.transform,"MapFocus","UI_CARD_SELECTED_OUTLINE",px+68,py-6,labelWidth+8,84); focus.enabled=false;
                var hit=Image(button.transform,"MarkerTouchTarget",null,px-8,py-14,labelWidth+88,104,true); hit.color=Color.clear;
                button.targetGraphic=pin;
                Ensure<PanelNavButton>(button).EditorConfigure(PanelNavButton.NavAction.Open,spec.Item4);
                var node=Ensure<MapMenuNode>(button);
                node.Configure(spec.Item1=="center"?"":spec.Item1,spec.Item4,pin,label,tip,icon,focus,caption,
                    new[]{Sprite("UI_PIN_NORMAL"),Sprite("UI_PIN_SELECTED"),Sprite("UI_PIN_DISABLED")},
                    new[]{Sprite("UI_MAP_LABEL_NORMAL"),Sprite("UI_MAP_LABEL_SELECTED"),Sprite("UI_MAP_LABEL_DISABLED")});
                root.SetAsLastSibling(); nodes.Add(node);
            }
            if(panel.Find("MapLayer")!=null) panel.Find("MapLayer").gameObject.SetActive(false);
            for(int i=0;i<nodes.Count;i++)
            {
                var button=nodes[i].GetComponent<UnityEngine.UI.Button>(); var navigation=button.navigation;
                navigation.mode=UnityEngine.UI.Navigation.Mode.Explicit;
                navigation.selectOnRight=navigation.selectOnDown=nodes[(i+1)%nodes.Count].GetComponent<UnityEngine.UI.Button>();
                navigation.selectOnLeft=navigation.selectOnUp=nodes[(i+nodes.Count-1)%nodes.Count].GetComponent<UnityEngine.UI.Button>();
                button.navigation=navigation;
            }
            var background=Image(panel,"HubBackground",null,0,0,1920,1080); background.color=GamePalette.Surface; background.transform.SetAsFirstSibling(); Stretch(background.rectTransform);
            Text(panel,"MapHeading","Mapa do negócio",24,110,700,48,GameTypography.Tokens.SectionTitle,true);
            Text(panel,"MapHint","Selecione um destino para gerenciar seu restaurante.",24,918,1280,42,GameTypography.Tokens.Body);
            var side=Image(panel,"HubSummary","UI_PANEL_LAVENDER",1328,112,568,848);
            Text(side.transform,"Title","Seu negócio",32,28,504,52,GameTypography.Tokens.SectionTitle,true);
            Text(side.transform,"Caption","GESTÃO DO RESTAURANTE",32,92,504,32,GameTypography.Tokens.Caption,true);
            var company=Text(side.transform,"Company","Meu restaurante",32,140,504,112,GameTypography.Tokens.BodyStrong,true);
            var restaurant=Text(side.transform,"Restaurant","Restaurante",32,260,504,40,GameTypography.Tokens.Body);
            Image(side.transform,"LocationIcon","ICON_PIN",32,324,32,32).color=GamePalette.Plum;
            var location=Text(side.transform,"Location","Localização",80,316,456,52,GameTypography.Tokens.BodyStrong,true);
            Text(side.transform,"Help","A área destacada no mapa é a localização atual. Use os destinos para consultar caixa, cardápio, equipamentos e equipe.",32,398,504,144,GameTypography.Tokens.Body);
            var progress=Text(side.transform,"Progress","0 de 3 meses concluídos",32,574,504,40,GameTypography.Tokens.Label,true);
            var track=Image(side.transform,"ProgressTrack","UI_PROGRESS_TRACK",32,634,504,16);
            var fill=Image(track.transform,"Fill","UI_PROGRESS_FILL",0,0,0,16); Stretch(fill.rectTransform);
            fill.rectTransform.anchorMax=new Vector2(0,1); fill.color=GamePalette.Primary;
            Action(side.transform,"Establishment","Meu estabelecimento","ICON_RESTAURANT",PanelId.Establishment,32,712,504);
            // Reuse the HUD and its serialized financial references.
            var header=(RectTransform)panel.Find("Header"); Rect(header,0,0,1920,88);
            foreach(Transform child in header) if(child.name!="TopBar") child.gameObject.SetActive(false);
            var headerImage=Ensure<UnityEngine.UI.Image>(header); headerImage.sprite=Sprite("UI_TOPBAR_VIOLET"); headerImage.color=Color.white; headerImage.raycastTarget=false;
            var top=(RectTransform)header.Find("TopBar"); Stretch(top); var topImage=top.GetComponent<UnityEngine.UI.Image>(); topImage.enabled=false;
            Image(top,"LogoSurface","UI_PANEL_IVORY",24,14,200,60);
            // The complete square PNG is preserved; only transparent margins extend beyond the bar.
            Image(top,"Logo","BRAND_COGNA_LOGO",40,-40,168,168).preserveAspect=true;
            var companyTop=Text(top,"Company","Meu restaurante",248,12,580,64,GameTypography.Tokens.BodyStrong,true,GamePalette.OnDark);
            Pill(top,"RoundPill","ICON_CALENDAR","Período",848,12,294,false);
            Pill(top,"ScorePill","ICON_STAR","Score",1166,12,258,true);
            Pill(top,"CashPill","ICON_CASH","Caixa",1448,12,448,true);
            var hud=top.GetComponent<MenuHudView>(); Set(hud,"companyValue",companyTop); Set(hud,"progressValue",progress); Set(hud,"progressFill",fill.rectTransform);
            Set(hud,"scoreValue",top.Find("ScorePill/Value").GetComponent<TextMeshProUGUI>());
            Set(hud,"cashValue",top.Find("CashPill/Value").GetComponent<TextMeshProUGUI>());
            Set(hud,"roundValue",top.Find("RoundPill/Value").GetComponent<TextMeshProUGUI>());
            var hudProperties=new SerializedObject(hud); hudProperties.FindProperty("negativeCashColor").colorValue=GamePalette.Parse(GamePalette.HexNegativeOnDark); hudProperties.ApplyModifiedPropertiesWithoutUndo();
            // Full company name in the summary, refreshed alongside the shared HUD.
            var mirror=Ensure<HubCompanyLabel>(company); mirror.Configure(company);
            var footer=Image(panel,"HubNavbar","UI_NAVBAR_PLUM",0,984,1920,96);
            var navSpecs=new[]{("Mapa","ICON_HOME",PanelId.Menu),("Restaurante","ICON_RESTAURANT",PanelId.Establishment),("Cardápio","ICON_MENU_FOOD",PanelId.MenuPricing),("Banco","ICON_BANK",PanelId.Financial),("Equipamentos","ICON_EQUIPMENT",PanelId.EquipmentStore),("Equipe","ICON_TEAM",PanelId.Team)};
            for(int i=0;i<navSpecs.Length;i++)
            {
                var s=navSpecs[i]; var button=Action(footer.transform,"Nav"+s.Item3,s.Item1,s.Item2,s.Item3,24+i*268,14,244,i==0?PanelNavButton.NavAction.OpenMenu:PanelNavButton.NavAction.Open);
                if(i==0) Image(button.transform,"Underline","UI_NAV_UNDERLINE",20,62,204,4).color=GamePalette.Primary;
            }
            var registry=canvas.Find("UiNavigation").GetComponent<PanelRegistry>();
            var bindings=new[]{(PanelId.Menu,"Panel_Menu"),(PanelId.Establishment,"Panel_Establishment"),(PanelId.Financial,"Panel_Financial"),(PanelId.MenuPricing,"Panel_MenuPricing"),(PanelId.Team,"Panel_Team"),(PanelId.EquipmentStore,"Panel_Equipment")};
            foreach(var binding in bindings)
            {
                var target=canvas.Find(binding.Item2); if(target==null) throw new InvalidOperationException("Missing "+binding.Item2);
                registry.EditorSetBinding(binding.Item1,target.gameObject);
            }
            EditorUtility.SetDirty(registry);
            Set(canvas.Find("UiNavigation").GetComponent<MenuNavigator>(),"registry",registry);
            var listener=UnityEngine.Object.FindFirstObjectByType<UIStateListener>(FindObjectsInactive.Include); Set(listener,"managementHubPanel",panel.gameObject);
            Set(listener,"initialTeamPanel",canvas.Find("Panel_Team").gameObject);
            var backRoot=Child(canvas,"HubHistory"); Stretch(backRoot);
            var back=Action(backRoot,"Back","Voltar","ICON_BACK",PanelId.None,24,1000,208,PanelNavButton.NavAction.Back);
            Ensure<HubBackButton>(backRoot).Configure(back.gameObject,(RectTransform)canvas.Find("ExitUI/ExitButton"),panel.gameObject); back.gameObject.SetActive(false); backRoot.SetAsLastSibling();
            Ensure<HubMapView>(panel).Configure(nodes.ToArray(),layout["zones"].Select(z=>new HubMapView.Zone{id=(string)z["catalog_id"],overlay=layers["OVERLAY_"+((string)z["catalog_id"]).ToUpperInvariant()]}).ToArray(),
                new[]{new HubMapView.Restaurant{type=RestaurantType.FRANCES,image=layers["MAP_REST_FRANCES"]},new HubMapView.Restaurant{type=RestaurantType.JAPONES,image=layers["MAP_REST_JAPONES"]},new HubMapView.Restaurant{type=RestaurantType.PODRAO,image=layers["MAP_REST_LANCHES"]}},layers["OVERLAY_CENTER"],location,restaurant);
            panel.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static void Pill(Transform top,string name,string icon,string title,float x,float y,float w,bool withCaption)
        {
            var pill=Image(top,name,"UI_HUD_PILL",x,y,w,64);
            foreach(Transform child in pill.transform) if(child.name=="Border") child.gameObject.SetActive(false);
            Image(pill.transform,"Icon",icon,18,17,30,30).color=GamePalette.OnDark;
            if(withCaption) Text(pill.transform,"Caption",title,60,12,100,40,GameTypography.Tokens.Caption,false,GamePalette.OnDark);
            var value=pill.transform.Find("Value").GetComponent<TextMeshProUGUI>();
            Rect(value.rectTransform,withCaption?154:60,12,withCaption?w-174:w-80,40);
            GameTypography.Apply(value,GameTypography.Tokens.BodyStrong,true); value.color=GamePalette.OnDark;
            value.textWrappingMode=TextWrappingModes.NoWrap; value.alignment=TextAlignmentOptions.MidlineRight;
        }
    }
}
