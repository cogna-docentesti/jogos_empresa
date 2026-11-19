using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    public static class QuarterlyResultUiMigration
    {
        private static T Ensure<T>(Component c) where T : Component => c.GetComponent<T>() ?? c.gameObject.AddComponent<T>();
        private static Sprite Art(string id)
        {
            var shared = SharedUiSkin.Current.Get(id); if (shared != null) return shared;
            var m = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("docs/art/manifest.json"));
            return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/" + (string)m["assets"].Single(a => (string)a["id"] == id)["path"]);
        }
        private static RectTransform Node(Transform p, string n, float x, float y, float w, float h)
        {
            var r = p.Find(n) as RectTransform;
            if (r == null) { r = (RectTransform)new GameObject(n, typeof(RectTransform)).transform; r.SetParent(p, false); }
            r.localScale = Vector3.one; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
        }
        private static void Set(UnityEngine.Object o, string n, UnityEngine.Object v)
        { var so = new SerializedObject(o); so.FindProperty(n).objectReferenceValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static UnityEngine.UI.Image Picture(Transform p, string n, string id, float x, float y, float w, float h, bool hit = false)
        {
            var i = Ensure<UnityEngine.UI.Image>(Node(p, n, x, y, w, h)); i.sprite = id == null ? null : Art(id); i.color = Color.white; i.raycastTarget = hit;
            i.type = i.sprite != null && i.sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            i.preserveAspect = i.type == UnityEngine.UI.Image.Type.Simple && i.sprite != null; return i;
        }
        private static TextMeshProUGUI Text(Transform p, string n, string value, float x, float y, float w, float h, TypographyToken size, bool bold = false, Color? color = null)
        {
            var t = Ensure<TextMeshProUGUI>(Node(p, n, x, y, w, h)); GameTypography.Apply(t, size, bold);
            t.text = value; t.color = color ?? GamePalette.Ink; t.alignment = TextAlignmentOptions.TopLeft; t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow; t.richText = false; t.raycastTarget = false; return t;
        }
        private static UnityEngine.UI.Button Button(Transform p, string n, string label, float x, float y, float w, float h, SharedControlKind kind = SharedControlKind.Primary)
        {
            var r = Node(p, n, x, y, w, h); var b = Ensure<UnityEngine.UI.Button>(r); b.targetGraphic = Ensure<UnityEngine.UI.Image>(r);
            var t = Text(r, "Label", label, 16, 8, w - 32, h - 16, GameTypography.Tokens.Button, true); t.alignment = TextAlignmentOptions.Center; SharedUiStyles.Bind(b, kind); return b;
        }
        private static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        private static UnityEngine.UI.VerticalLayoutGroup Vertical(RectTransform r, int padding = 6)
        {
            var v = Ensure<UnityEngine.UI.VerticalLayoutGroup>(r); v.spacing = 16; v.padding = new RectOffset(padding, padding, padding, padding);
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false; return v;
        }
        private static UnityEngine.UI.ScrollRect Scroll(Transform p, string n, float x, float y, float w, float h)
        {
            var root = Node(p, n, x, y, w, h); var s = Ensure<UnityEngine.UI.ScrollRect>(root); s.horizontal = false; s.vertical = true; s.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped; s.scrollSensitivity = 48;
            var viewport = Picture(root, "Viewport", null, 0, 0, w - 28, h, true); Ensure<UnityEngine.UI.Mask>(viewport).showMaskGraphic = false;
            var content = Node(viewport.transform, "Content", 0, 0, w - 28, 0); content.anchorMax = new Vector2(1, 1); content.sizeDelta = Vector2.zero; Vertical(content);
            Ensure<UnityEngine.UI.ContentSizeFitter>(content).verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize; s.content = content; s.viewport = viewport.rectTransform;
            var track = Picture(root, "Scrollbar", "UI_SCROLLBAR_TRACK", w - 12, 0, 12, h); var area = Node(track.transform, "SlidingArea", 0, 0, 12, h); var handle = Picture(area, "Handle", "UI_SCROLLBAR_THUMB", 0, 0, 12, 80, true); Stretch(handle.rectTransform);
            var bar = Ensure<UnityEngine.UI.Scrollbar>(track); bar.handleRect = handle.rectTransform; bar.targetGraphic = handle; bar.direction = UnityEngine.UI.Scrollbar.Direction.BottomToTop; bar.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            s.verticalScrollbar = bar; s.verticalScrollbarVisibility = UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide; return s;
        }

        [MenuItem("Tools/Jogos de Empresa/UI-14/Migrate quarterly result")]
        public static void Apply()
        {
            if (UnityEngine.Application.dataPath != "D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if(scene.path!="Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            var canvas=scene.GetRootGameObjects().Single(g=>g.name=="Canvas").transform;
            var panel=Node(canvas,"Panel_QuarterlyResult",0,0,1920,1080);Stretch(panel);
            var bg=Ensure<UnityEngine.UI.Image>(panel);bg.sprite=null;bg.color=GamePalette.Surface;bg.raycastTarget=true;
            var view=Ensure<QuarterlyResultView>(panel);var controller=Ensure<QuarterlyResultController>(panel);Set(controller,"view",view);
            var monthly=canvas.Find("Panel_MonthlyResult");Set(controller,"monthlyPanel",monthly.gameObject);
            var header=Picture(panel,"Header","UI_TOPBAR_VIOLET",0,0,1920,88);
            Text(header.transform,"Brand","Jogos de Empresa",24,18,800,60,GameTypography.Tokens.BrandTitle,true,GamePalette.OnDark);
            Set(view,"period",Text(header.transform,"Period","COMPARATIVO DA CAMPANHA",940,28,956,40,new TypographyToken(TypographyRole.Caption,24),false,GamePalette.OnDark));
            Text(panel,"Title","DRE • comparativo trimestral",24,112,1240,60,GameTypography.Tokens.ScreenTitle,true);
            var loaded=Node(panel,"LoadedBody",0,0,1920,984);Set(view,"loadedBody",loaded.gameObject);
            string[] kpis={"Receita acumulada","Resultado acumulado","Último caixa salvo"};string[] fields={"revenueValue","resultValue","cashValue"};
            for(int i=0;i<3;i++){var k=Picture(loaded,"Kpi"+i,"UI_KPI_TILE",24+i*416,184,408,108);Text(k.transform,"Label",kpis[i],20,14,368,30,GameTypography.Tokens.Label);Set(view,fields[i],Text(k.transform,"Value","",20,48,368,52,GameTypography.Tokens.DisplayValue,true));}
            Set(view,"note",Text(loaded,"AggregationNote","",24,308,1240,92,GameTypography.Tokens.Caption));
            var tableHeader=Picture(loaded,"TableHeader","UI_TABLE_HEADER",24,408,1240,72);
            Text(tableHeader.transform,"Label","Métrica / agregação",14,18,282,48,GameTypography.Tokens.Label,false);
            float[] xs={310,486,662,838,1040};float[] widths={176,176,176,202,200};
            var so=new SerializedObject(view);var monthLabels=so.FindProperty("monthLabels");monthLabels.arraySize=3;
            for(int i=0;i<5;i++){
                var t=Text(tableHeader.transform,"Column"+i,i<3?"Mês "+(i+1):i==3?"Agregado":"Variação",xs[i]+8,8,widths[i]-16,56,new TypographyToken(i<3?TypographyRole.Caption:i==3?TypographyRole.Label:TypographyRole.Body,22),false);t.alignment=TextAlignmentOptions.TopRight;
                if(i<3)monthLabels.GetArrayElementAtIndex(i).objectReferenceValue=t;else if(i==4)so.FindProperty("changeLabel").objectReferenceValue=t;
            }
            var scroll=Scroll(loaded,"TableScroll",24,488,1268,472);var layout=scroll.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=4;layout.padding=new RectOffset(0,0,0,8);
            // Refresh after assigning another SerializedObject so none of its writes are overwritten.
            so.ApplyModifiedPropertiesWithoutUndo();Set(view,"tableScroll",scroll);so=new SerializedObject(view);var rows=so.FindProperty("rows");rows.arraySize=25;
            for(int i=0;i<25;i++){
                var row=Picture(scroll.content,"Row"+i,"UI_TABLE_ROW_NEUTRAL",0,0,1240,88);Ensure<UnityEngine.UI.LayoutElement>(row).preferredHeight=88;
                var total=Picture(row.transform,"AggregateColumn","UI_TABLE_TOTAL_COLUMN",838,0,202,88);total.rectTransform.anchorMin=new Vector2(0,0);total.rectTransform.anchorMax=new Vector2(0,1);total.rectTransform.offsetMin=new Vector2(838,0);total.rectTransform.offsetMax=new Vector2(1040,0);
                var item=rows.GetArrayElementAtIndex(i);item.FindPropertyRelative("background").objectReferenceValue=row;
                item.FindPropertyRelative("label").objectReferenceValue=Text(row.transform,"Label","",14,12,282,30,(i==7||i==9||i==11?GameTypography.Tokens.TableTotal:GameTypography.Tokens.Table),i==7||i==9||i==11);
                item.FindPropertyRelative("rule").objectReferenceValue=Text(row.transform,"Rule","",14,46,282,48,GameTypography.Tokens.Caption,false,GamePalette.Muted);
                var cells=item.FindPropertyRelative("cells");cells.arraySize=5;
                for(int c=0;c<5;c++){
                    var t=Text(row.transform,"Cell"+c,"",xs[c]+8,12,widths[c]-16,64,(c==3||i==7||i==9||i==11?GameTypography.Tokens.TableTotal:GameTypography.Tokens.Table),c==3||i==7||i==9||i==11);t.alignment=TextAlignmentOptions.TopRight;
                    var r=t.rectTransform;r.anchorMin=new Vector2(0,0);r.anchorMax=new Vector2(0,1);r.offsetMin=new Vector2(xs[c]+8,12);r.offsetMax=new Vector2(xs[c]+widths[c]-8,-12);
                    cells.GetArrayElementAtIndex(c).objectReferenceValue=t;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            foreach(string id in new[]{"NEUTRAL","SUBTOTAL","POSITIVE","NEGATIVE"})Set(view,id.ToLowerInvariant()+"Row",Art("UI_TABLE_ROW_"+id));
            var spec=JObject.Parse(System.IO.File.ReadAllText("docs/art/layouts/dre-layout.json"))["quarterly"];var clip=spec["clip_rect_px"];
            var art=Node(panel,"QuarterlyIllustration",(float)clip[0],(float)clip[1],(float)clip[2],(float)clip[3]);Ensure<UnityEngine.UI.RectMask2D>(art);
            foreach(var layer in spec["layers"].OrderBy(l=>(int)l["order"])){
                string id=(string)layer["asset_id"];var sprite=Art(id);float scale=(float)layer["scale"],w=sprite.rect.width*scale,h=sprite.rect.height*scale;
                var image=Picture(art,id,id,(float)layer["position_px"][0]-w*(float)layer["pivot"][0]-(float)clip[0],(float)layer["position_px"][1]-h*(float)layer["pivot"][1]-(float)clip[1],w,h);image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;image.color=Color.white;
            }
            var chart=Picture(loaded,"ResultChart","UI_CHART_PANEL",1320,564,560,396);
            Text(chart.transform,"Title","Evolução do resultado de caixa",20,18,520,42,new TypographyToken(TypographyRole.SectionTitle,24),true);
            Text(chart.transform,"Note","Mesma escala • negativo à esquerda do zero",20,62,520,54,GameTypography.Tokens.Caption);
            var axis=Picture(chart.transform,"Zero",null,279,124,2,244);axis.color=GamePalette.Hairline;
            so=new SerializedObject(view);var bars=so.FindProperty("bars");bars.arraySize=3;
            for(int i=0;i<3;i++){
                float y=128+i*78;var label=Text(chart.transform,"BarLabel"+i,"Mês "+(i+1),24,y,150,30,GameTypography.Tokens.Caption);var value=Text(chart.transform,"BarValue"+i,"Não salvo",180,y,356,30,GameTypography.Tokens.TableTotal,true);value.alignment=TextAlignmentOptions.TopRight;
                var fill=Picture(chart.transform,"BarFill"+i,null,280,y+46,0,14);fill.rectTransform.pivot=new Vector2(0,.5f);fill.rectTransform.anchoredPosition=new Vector2(280,-y-46);
                var b=bars.GetArrayElementAtIndex(i);b.FindPropertyRelative("label").objectReferenceValue=label;b.FindPropertyRelative("value").objectReferenceValue=value;b.FindPropertyRelative("fill").objectReferenceValue=fill.rectTransform;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var state=Node(panel,"StateBody",24,190,1240,770);Set(view,"stateBody",state.gameObject);
            Picture(state,"EmptyResults","EMPTY_RESULTS",370,24,480,320);
            Set(view,"stateTitle",Text(state,"Title","Nenhum mês salvo",80,380,1080,90,GameTypography.Tokens.SectionTitle,true));
            Set(view,"stateMessage",Text(state,"Message","",80,490,1080,150,GameTypography.Tokens.Body));
            Set(view,"retryButton",Button(state,"Retry","Tentar novamente",440,660,360,68,SharedControlKind.Secondary));
            var footer=Picture(panel,"BottomBar","UI_NAVBAR_PLUM",0,984,1920,96);
            Text(footer.transform,"Note","Consulta dos meses salvos da campanha. Voltar retorna à tela anterior.",304,16,640,64,GameTypography.Tokens.Caption,false,GamePalette.OnDark);
            Set(view,"backButton",Button(footer.transform,"Back","Voltar",1244,14,364,68));
            var monthlyFooter=monthly.Find("BottomBar");var open=Button(monthlyFooter,"Quarterly","Comparar trimestre",304,14,300,68,SharedControlKind.Secondary);
            while(open.onClick.GetPersistentEventCount()>0) UnityEventTools.RemovePersistentListener(open.onClick,0);
            UnityEventTools.AddPersistentListener(open.onClick,controller.OpenFromMonthly);Set(monthly.GetComponent<MonthlyResultView>(),"quarterlyButton",open);
            var monthlyNote=(RectTransform)monthlyFooter.Find("Note");monthlyNote.anchoredPosition=new Vector2(624,-12);monthlyNote.sizeDelta=new Vector2(320,72);
            // Hub access stays within the existing sidebar; monthly summary also exposes it at campaign end.
            var hub=canvas.Find("Panel_Menu/HubSummary");
            var establishment=(RectTransform)hub.Find("Establishment");establishment.anchoredPosition=new Vector2(32,-658);establishment.sizeDelta=new Vector2(504,56);
            var monthLink=(RectTransform)hub.Find("MonthlyResult");monthLink.anchoredPosition=new Vector2(32,-722);monthLink.sizeDelta=new Vector2(504,56);
            var quarterLink=Button(hub,"QuarterlyResult","Comparar trimestre",32,786,504,56,SharedControlKind.Secondary);Ensure<PanelNavButton>(quarterLink).EditorConfigure(PanelNavButton.NavAction.Open,PanelId.QuarterlyResults);
            canvas.GetComponentInChildren<PanelRegistry>(true).EditorSetBinding(PanelId.QuarterlyResults,panel.gameObject);
            canvas.GetComponentInChildren<HubBackButton>(true).ConfigureQuarterlyResults(panel.gameObject);
            int menuIndex=canvas.Find("MenuAccess").GetSiblingIndex();
            panel.SetSiblingIndex(menuIndex-(panel.GetSiblingIndex()<menuIndex?1:0));panel.gameObject.SetActive(false);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
