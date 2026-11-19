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
    public static class MonthlyResultUiMigration
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

        [MenuItem("Tools/Jogos de Empresa/UI-13/Migrate monthly result")]
        public static void Apply()
        {
            if (UnityEngine.Application.dataPath != "D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Use Cogna outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); if (scene.path != "Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            var canvas = scene.GetRootGameObjects().Single(g => g.name == "Canvas").transform;
            var panel = Node(canvas, "Panel_MonthlyResult", 0, 0, 1920, 1080); Stretch(panel);
            var bg = Ensure<UnityEngine.UI.Image>(panel); bg.sprite = null; bg.color = GamePalette.Surface; bg.raycastTarget = true;
            var view = Ensure<MonthlyResultView>(panel); var controller = Ensure<MonthlyResultController>(panel); Set(controller,"view",view);
            var header = Picture(panel,"Header","UI_TOPBAR_VIOLET",0,0,1920,88);
            Text(header.transform,"Brand","Jogos de Empresa",24,18,800,60,GameTypography.Tokens.BrandTitle,true,GamePalette.OnDark);
            Set(view,"period",Text(header.transform,"Period","RESULTADOS MENSAIS",1000,28,896,40,new TypographyToken(TypographyRole.Caption,24),false,GamePalette.OnDark));
            Set(view,"title",Text(panel,"Title","DRE mensal",24,112,1240,60,GameTypography.Tokens.ScreenTitle,true));
            var loaded=Node(panel,"LoadedBody",0,0,1920,984); Set(view,"loadedBody",loaded.gameObject);
            string[] kpis={"Resultado do mês","Caixa no fechamento","Clientes atendidos"}; string[] fields={"resultValue","cashValue","clientsValue"};
            for(int i=0;i<3;i++) { var k=Picture(loaded,"Kpi"+i,"UI_KPI_TILE",24+i*416,184,408,108); Text(k.transform,"Label",kpis[i],20,14,368,30,GameTypography.Tokens.Label); Set(view,fields[i],Text(k.transform,"Value","",20,48,368,52,GameTypography.Tokens.DisplayValue,true)); }
            var tableHeader=Picture(loaded,"TableHeader","UI_TABLE_HEADER",24,312,1240,52);
            Text(tableHeader.transform,"Label","Movimentação e indicadores do mês",20,10,830,36,GameTypography.Tokens.Label,false);
            Text(tableHeader.transform,"Total","Total salvo",864,10,356,36,GameTypography.Tokens.Label,false).alignment=TextAlignmentOptions.TopRight;
            var scroll=Scroll(loaded,"TableScroll",24,372,1268,588); Set(view,"tableScroll",scroll); var layout=scroll.content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=4;layout.padding=new RectOffset(0,0,0,8);
            var so=new SerializedObject(view); var rows=so.FindProperty("rows"); rows.arraySize=22;
            for(int i=0;i<22;i++)
            {
                var row=Picture(scroll.content,"Row"+i,"UI_TABLE_ROW_NEUTRAL",0,0,1240,48); Ensure<UnityEngine.UI.LayoutElement>(row).preferredHeight=48;
                var total=Picture(row.transform,"TotalColumn","UI_TABLE_TOTAL_COLUMN",850,0,390,48);total.rectTransform.anchorMin=new Vector2(0,0);total.rectTransform.anchorMax=new Vector2(0,1);total.rectTransform.offsetMin=new Vector2(850,0);total.rectTransform.offsetMax=new Vector2(1240,0);
                var label=Text(row.transform,"Label","",20,10,810,28,(i==7||i==9||i==11?GameTypography.Tokens.TableTotal:GameTypography.Tokens.Table)); var value=Text(row.transform,"Value","",866,10,354,28,(i==7||i==9||i==11?GameTypography.Tokens.TableTotal:GameTypography.Tokens.Table),i==7||i==9||i==11); value.alignment=TextAlignmentOptions.TopRight;
                foreach(var text in new[]{label,value}) { var r=text.rectTransform; r.anchorMin=new Vector2(0,0);r.anchorMax=new Vector2(0,1);r.offsetMin=new Vector2(r.offsetMin.x,8);r.offsetMax=new Vector2(r.offsetMax.x,-8); }
                var item=rows.GetArrayElementAtIndex(i);item.FindPropertyRelative("label").objectReferenceValue=label;item.FindPropertyRelative("value").objectReferenceValue=value;item.FindPropertyRelative("background").objectReferenceValue=row;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(view,"neutralRow",Art("UI_TABLE_ROW_NEUTRAL"));Set(view,"subtotalRow",Art("UI_TABLE_ROW_SUBTOTAL"));Set(view,"positiveRow",Art("UI_TABLE_ROW_POSITIVE"));Set(view,"negativeRow",Art("UI_TABLE_ROW_NEGATIVE"));
            // The contract positions are exported pivots, with top-left y-down coordinates.
            var spec=JObject.Parse(System.IO.File.ReadAllText("docs/art/layouts/dre-layout.json"));var clip=spec["clip_rect_px"];
            var art=Node(panel,"MonthlyIllustration",(float)clip[0],(float)clip[1],(float)clip[2],(float)clip[3]);Ensure<UnityEngine.UI.RectMask2D>(art);
            foreach(var layer in spec["layers"].OrderBy(l=>(int)l["order"]))
            {
                string id=(string)layer["asset_id"];var sprite=Art(id);float scale=(float)layer["scale"];float w=sprite.rect.width*scale,h=sprite.rect.height*scale;
                var image=Picture(art,id,id,(float)layer["position_px"][0]-w*(float)layer["pivot"][0]-(float)clip[0],(float)layer["position_px"][1]-h*(float)layer["pivot"][1]-(float)clip[1],w,h);image.type=UnityEngine.UI.Image.Type.Simple;image.preserveAspect=true;image.color=Color.white;
            }
            var chart=Picture(loaded,"CashChart","UI_CHART_PANEL",1320,564,560,396);
            Text(chart.transform,"Title","Entradas e saídas do mês",20,18,520,42,new TypographyToken(TypographyRole.SectionTitle,24),true);
            Text(chart.transform,"Note","Escala comum • saídas à esquerda do zero",20,62,520,30,GameTypography.Tokens.Caption);
            var axis=Picture(chart.transform,"Zero",null,279,102,2,250);axis.color=GamePalette.Hairline;
            so=new SerializedObject(view); var bars=so.FindProperty("bars");bars.arraySize=4;string[] labels={"Receita","Saídas (com parcela)","Eventos","Resultado"};
            for(int i=0;i<4;i++)
            {
                float y=104+i*66;var label=Text(chart.transform,"BarLabel"+i,labels[i],24,y,260,30,GameTypography.Tokens.Caption);var value=Text(chart.transform,"BarValue"+i,"",290,y,246,30,new TypographyToken(TypographyRole.TableTotal,20),true);value.alignment=TextAlignmentOptions.TopRight;
                var fill=Picture(chart.transform,"BarFill"+i,null,280,y+42,0,12);fill.rectTransform.pivot=new Vector2(0,.5f);fill.rectTransform.anchoredPosition=new Vector2(280,-y-42);
                var b=bars.GetArrayElementAtIndex(i);b.FindPropertyRelative("label").objectReferenceValue=label;b.FindPropertyRelative("value").objectReferenceValue=value;b.FindPropertyRelative("fill").objectReferenceValue=fill.rectTransform;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            var state=Node(panel,"StateBody",24,190,1240,770);Set(view,"stateBody",state.gameObject);
            Picture(state,"EmptyResults","EMPTY_RESULTS",370,24,480,320);
            Set(view,"stateTitle",Text(state,"Title","Nenhum resultado disponível",80,380,1080,90,GameTypography.Tokens.SectionTitle,true));
            Set(view,"stateMessage",Text(state,"Message","",80,490,1080,150,GameTypography.Tokens.Body));
            Set(view,"retryButton",Button(state,"Retry","Tentar novamente",440,660,360,68,SharedControlKind.Secondary));
            var footer=Picture(panel,"BottomBar","UI_NAVBAR_PLUM",0,984,1920,96);
            Set(view,"footer",Text(footer.transform,"Note","",304,16,640,64,GameTypography.Tokens.Caption,false,GamePalette.OnDark));
            Set(view,"continueButton",Button(footer.transform,"Continue","Continuar",1244,14,364,68));
            canvas.GetComponentInChildren<PanelRegistry>(true).EditorSetBinding(PanelId.MonthlyResults,panel.gameObject);
            Set(UnityEngine.Object.FindFirstObjectByType<UIStateListener>(),"roundSummaryPanel",panel.gameObject);
            canvas.GetComponentInChildren<HubBackButton>(true).ConfigureMonthlyResults(panel.gameObject);
            panel.SetSiblingIndex(Mathf.Min(panel.GetSiblingIndex(),canvas.Find("MenuAccess").GetSiblingIndex()));
            var hub=canvas.Find("Panel_Menu/HubSummary");
            var establishment=(RectTransform)hub.Find("Establishment");establishment.anchoredPosition=new Vector2(32,-678);
            var open=Button(hub,"MonthlyResult","Consultar DRE mensal",32,770,504,68,SharedControlKind.Secondary);Ensure<PanelNavButton>(open).EditorConfigure(PanelNavButton.NavAction.Open,PanelId.MonthlyResults);
            panel.gameObject.SetActive(false);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
    }
}
