using System;
using System.Linq;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Idempotent style migration; preserves hierarchy, coordinates and serialized callbacks.</summary>
    public static class GlobalThemeMigration
    {
        private const string Art = "Assets/Art/new/";
        private static TMP_FontAsset font;

        [MenuItem("Tools/Jogos de Empresa/UI-02/Apply global theme")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || UnityEngine.Application.dataPath.Replace('\\','/') != "D:/Cogna/jogos_empresa/Assets")
                throw new InvalidOperationException("Use the Cogna Editor outside Play Mode.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Open GameScene.");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(GameTypography.FontAssetPath);
            foreach (var path in new[]{GameTypography.FontAssetPath, "Assets/TextMesh Pro/Fonts/InterTight-Italic.asset"})
            {
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                var material = asset.material;
                material.DisableKeyword("UNDERLAY_ON"); material.DisableKeyword("UNDERLAY_INNER"); material.DisableKeyword("OUTLINE_ON");
                material.SetFloat("_OutlineWidth",0); material.SetColor("_FaceColor",Color.white);
                EditorUtility.SetDirty(material);
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                Undo.RegisterFullObjectHierarchyUndo(root,"Apply UI-02 theme");
                Style(root);
                foreach (var scaler in root.GetComponentsInChildren<UnityEngine.UI.CanvasScaler>(true))
                {
                    scaler.referenceResolution = new Vector2(1920,1080);
                    scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 1;
                    // Header canvases only sort their graphics. The root owns the scale.
                    scaler.enabled = scaler.transform.parent == null;
                }
                foreach (var header in root.GetComponentsInChildren<RectTransform>(true).Where(t=>t.name=="Header" && t.parent!=null && t.parent.name.StartsWith("Panel_")))
                {
                    HeaderText(header,"TitleText",32,12,800,60,TextAlignmentOptions.MidlineLeft);
                    HeaderText(header,"RegionText",32,12,800,60,TextAlignmentOptions.MidlineLeft);
                    HeaderText(header,"HintText",32,84,1800,60,TextAlignmentOptions.TopLeft);
                    var steps=header.Find("StepsText") as RectTransform;
                    if(steps!=null)
                    {
                        steps.anchorMin=steps.anchorMax=new Vector2(1,1); steps.pivot=new Vector2(1,1);
                        steps.anchoredPosition=new Vector2(-32,-24); steps.sizeDelta=new Vector2(420,32);
                        steps.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineRight;
                    }
                }
            }
            var canvas=scene.GetRootGameObjects().First(g=>g.name=="Canvas");
            var confirm=canvas.transform.Find("Panel_Financial/Footer/ConfirmButton") as RectTransform;
            if(confirm!=null)
            {
                confirm.anchorMin=confirm.anchorMax=new Vector2(.5f,0); confirm.pivot=new Vector2(.5f,0);
                confirm.anchoredPosition=new Vector2(390,24); confirm.sizeDelta=new Vector2(380,68);
            }
            foreach(Transform panel in canvas.transform)
                if(panel.name.StartsWith("Panel_") && panel.GetComponentInChildren<Game.Adapter.In.UI.Layout.SafeAreaFitter>(true)==null)
                    panel.gameObject.AddComponent<Game.Adapter.In.UI.Layout.SafeAreaFitter>();
            // Templates are instantiated by the real catalog views on every revisit.
            foreach (var path in new[]{"Assets/Prefabs/Card_loan.prefab","Assets/Prefabs/Card_equipment.prefab",
                "Assets/Prefabs/Card_ProductItem.prefab","Assets/Prefabs/Button_Segment.prefab",
                "Assets/Prefabs/Chip.prefab","Assets/Prefabs/DetailRow.prefab","Assets/Prefabs/Card_Generic.prefab","Assets/Prefabs/ItemCard.prefab"})
            {
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try { Style(prefab); PrefabUtility.SaveAsPrefabAsset(prefab,path); }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            UpdateLocationSkin();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }

        private static void Style(GameObject root)
        {
            var bank=root.GetComponent<Game.Adapter.In.UI.BankCardView>();
            if(bank!=null)
            {
                var term=(TMP_Text)new SerializedObject(bank).FindProperty("termText").objectReferenceValue;
                if(term!=null)
                {
                    term.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,260);
                    term.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,80);
                    term.textWrappingMode=TextWrappingModes.Normal;
                }
            }
            foreach (var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (InsideIdentification(image.transform)) continue;
                string n=image.name.ToLowerInvariant();
                string path=image.sprite != null ? AssetDatabase.GetAssetPath(image.sprite) : "";
                if (image.color.a < .01f) continue; // Hit targets.
                if (n.Contains("shadow")) { image.color=GamePalette.Shadow; continue; }
                if (path.StartsWith(Art)) { image.color=Color.white; continue; }
                if(path.Contains("REDESIGN-UI") && (path.Contains("btn-") || path.Contains("menu-dropdown")))
                { image.sprite=Load("shared/components/buttons/primary_violet_normal.png"); image.color=Color.white; }
                if(path.Contains("REDESIGN-UI") && path.Contains("branco") && image.GetComponentInParent<UnityEngine.UI.Button>()!=null)
                    image.color=GamePalette.OnDark;
                if(image.transform.parent!=null && image.transform.parent.name=="Button" && image.transform.parent.parent!=null && image.transform.parent.parent.name=="MenuAccess")
                {
                    if(n=="image") image.enabled=false; // Retired gold frame; the button owns the surface.
                    if(n=="icon") { image.sprite=Load("shared/icons/icon_menu.png"); image.color=GamePalette.OnDark; }
                }
                bool oldSurface=path.Contains("REDESIGN-UI") && (n.Contains("background") || n.Contains("fill") || n.Contains("plate") || n.Contains("bar"));
                if (n.Contains("border") || n.Contains("outline") || n.Contains("divider")) { image.color=GamePalette.Lavender; continue; }
                var button=image.GetComponent<UnityEngine.UI.Button>();
                if (button!=null && !n.Contains("card") && !n.Contains("node"))
                {
                    image.sprite=Load("shared/components/buttons/primary_violet_normal.png");
                    image.type=UnityEngine.UI.Image.Type.Sliced; image.color=Color.white;
                    button.transition=UnityEngine.UI.Selectable.Transition.ColorTint;
                    var colors=button.colors; colors.normalColor=Color.white; colors.selectedColor=new Color(.95f,.92f,1);
                    colors.highlightedColor=new Color(.96f,.94f,1); colors.pressedColor=new Color(.8f,.75f,.9f);
                    colors.disabledColor=new Color(.7f,.65f,.75f,.6f); colors.colorMultiplier=1; button.colors=colors;
                    continue;
                }
                bool procedural=string.IsNullOrEmpty(path) || path.Contains("unity_builtin_extra") || path.EndsWith("botao.png");
                if (!procedural && !oldSurface) continue; // Authored illustrations retain neutral tint.
                Color old=image.color;
                if (n.Contains("header") || n.Contains("topbar"))
                { image.sprite=Load("hud/bars/topbar_violet.png"); image.color=Color.white; }
                else if(n.Contains("footer") || n.Contains("bottombar") || n.Contains("navbar"))
                { image.sprite=Load("hud/bars/navbar_plum.png"); image.color=Color.white; }
                else if (n.Contains("scrim") || n.Contains("veil") || n.Contains("overlay")) image.color=GamePalette.MapScrim;
                else if (n.Contains("fill") && (n.Contains("bar") || n.Contains("progress"))) image.color=GamePalette.Primary;
                else if (old.grayscale < .65f || n.Contains("card") || n.StartsWith("panel") || n.Contains("background"))
                {
                    bool dark=n.StartsWith("panel_") || n.Contains("maplabel");
                    image.color=dark ? GamePalette.Plum : GamePalette.Surface;
                    if(oldSurface) image.sprite=Load(dark?"shared/components/panels/panel_plum.png":"shared/components/panels/panel_ivory.png");
                }
            }
            foreach(var effect in root.GetComponentsInChildren<Game.Adapter.In.UI.Effects.VerticalGradient>(true)) effect.enabled=false;
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if(text.name.IndexOf("shadow",StringComparison.OrdinalIgnoreCase)>=0) { text.enabled=false; continue; }
                bool id=InsideIdentification(text.transform);
                float size=id ? (text.name=="GameTitle" ? GameTypography.BrandTitle : text.name=="Title" ? GameTypography.ScreenTitle
                    : text.name.Contains("Limits") || text.name=="StorageNote" ? GameTypography.Caption
                    : text.name=="Label" ? GameTypography.Label : GameTypography.Body) : Role(text);
                bool bold=(text.fontStyle & FontStyles.Bold)!=0 || size==GameTypography.ScreenTitle || size==GameTypography.SectionTitle;
                GameTypography.Apply(text,font,size,bold);
                if(!id && text.transform.parent!=null && text.transform.parent.GetComponent<UnityEngine.UI.Button>()!=null)
                    text.gameObject.SetActive(true); // Former button sprites contained lettering; new ones use native text.
                if (!id && text.rectTransform.rect.height < size * 1.3f)
                    text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,size * 1.3f);
                if(!id)
                {
                    text.color=GamePalette.TextOn(Surface(text.transform.parent));
                    text.overflowMode=TextOverflowModes.Truncate;
                    // Existing multiline boxes keep wrapping. Inputs keep their horizontal scroll/mask.
                    if(text.GetComponentInParent<TMP_InputField>()==null && (text.name.Contains("Desc") || text.name.Contains("Hint") || text.name.Contains("Tip")))
                        text.textWrappingMode=TextWrappingModes.Normal;
                    if(text.name=="maxAmountText" || text.name=="CapitalValue") text.alignment=TextAlignmentOptions.MidlineRight;
                }
                foreach(var shadow in text.GetComponents<UnityEngine.UI.Shadow>()) UnityEngine.Object.DestroyImmediate(shadow);
            }
        }

        private static float Role(TMP_Text text)
        {
            string n=text.name.ToLowerInvariant();
            if(n=="titletext" || n=="screentitle" || n=="regiontext") return GameTypography.ScreenTitle;
            if(n.Contains("step") || n.Contains("caption")) return GameTypography.Caption;
            if(n.Contains("capitalvalue") || n=="kpivalue") return GameTypography.DisplayValue;
            if(n=="title" || n=="paneltitle" || n.Trim()=="restaurantlabel" || n.Trim()=="segmentlabel" || n.Trim()=="equipmentslabel") return GameTypography.SectionTitle;
            if(n.Contains("label")) return GameTypography.Label;
            return GameTypography.Body;
        }

        private static void HeaderText(Transform parent,string name,float x,float y,float w,float h,TextAlignmentOptions alignment)
        {
            var t=parent.Find(name) as RectTransform; if(t==null) return;
            t.anchorMin=t.anchorMax=new Vector2(0,1); t.pivot=new Vector2(0,1);
            t.anchoredPosition=new Vector2(x,-y); t.sizeDelta=new Vector2(w,h);
            t.GetComponent<TMP_Text>().alignment=alignment;
        }

        private static Color Surface(Transform t)
        {
            for(;t!=null;t=t.parent)
            {
                var image=t.GetComponent<UnityEngine.UI.Image>();
                if(image==null || image.color.a<.5f) continue;
                string path=image.sprite!=null?AssetDatabase.GetAssetPath(image.sprite):"";
                if(path.Contains("primary_violet") || path.Contains("topbar_violet")) return GamePalette.Primary;
                if(path.Contains("navbar_plum") || path.Contains("panel_plum")) return GamePalette.Plum;
                if(path.Contains("ivory") || path.Contains("card_normal")) return GamePalette.Surface;
                if(image.color!=Color.white) return image.color;
            }
            return GamePalette.Surface;
        }

        private static bool InsideIdentification(Transform t)
        { for(;t!=null;t=t.parent) if(t.name=="Panel_Identification") return true; return false; }
        private static Sprite Load(string relative) => AssetDatabase.LoadAssetAtPath<Sprite>(Art+relative);

        public static void UpdateLocationSkin()
        {
            var skin=AssetDatabase.LoadAssetAtPath<LocationSkinV2>("Assets/Resources/LocationSkinV2.asset");
            skin.panelTitleColor=GamePalette.Ink; skin.valueGood=GamePalette.Money;
            skin.valueWarn=GamePalette.Parse(GamePalette.HexOnWarn); skin.valueBad=GamePalette.ErrorText;
            skin.panelBackgroundNeutral=Load("shared/components/panels/panel_ivory.png");
            skin.statRowBackground=Load("shared/components/panels/panel_lavender.png");
            skin.continueNormal=Load("shared/components/buttons/primary_violet_normal.png");
            skin.continueHover=Load("shared/components/buttons/primary_violet_hover.png");
            skin.continuePressed=Load("shared/components/buttons/primary_violet_pressed.png");
            skin.menuNormal=skin.continueNormal; skin.menuHover=skin.continueHover;
            skin.topbarBackground=Load("hud/bars/topbar_violet.png");
            skin.menuDropdown=Load("shared/components/panels/panel_plum.png");
            skin.menuItemHover=Load("shared/components/panels/panel_lavender.png");
            skin.labelPill=Load("shared/components/badges/pill_normal.png");
            skin.toastBackground=Load("shared/components/feedback/toast_neutral.png");
            foreach(var zone in skin.Zones)
            { zone.panelBackground=skin.panelBackgroundNeutral; zone.highlightBox=skin.statRowBackground; }
            EditorUtility.SetDirty(skin);
        }
    }
}
