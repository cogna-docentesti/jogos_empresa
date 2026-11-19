using System;
using System.IO;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    public static class SharedUiMigration
    {
        public static readonly string[] Templates={"Button_Segment","Chip","DetailRow","Card_Generic","ItemCard","Card_loan","Card_equipment","Card_ProductItem","MainArea_Equipment","Panel_Equipment"};
        [MenuItem("Tools/Jogos de Empresa/UI-03/Apply shared components")]
        public static void Apply()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(EditorApplication.isPlaying || UnityEngine.Application.dataPath.Replace('\\','/')!="D:/Cogna/jogos_empresa/Assets" || scene.path!="Assets/Scenes/GameScene.unity")
                throw new InvalidOperationException("Open Cogna GameScene outside Play Mode.");
            SharedUiImportRules.ApplyManifest();
            var skin=AssetDatabase.LoadAssetAtPath<SharedUiSkin>("Assets/Resources/SharedUiSkin.asset");
            if(skin==null) { skin=ScriptableObject.CreateInstance<SharedUiSkin>(); AssetDatabase.CreateAsset(skin,"Assets/Resources/SharedUiSkin.asset"); }
            skin.entries=SharedUiImportRules.Read().Where(r=>r.path.StartsWith("shared/")||r.path.StartsWith("hud/")||r.path.StartsWith("dre/components/"))
                .Select(r=>new SharedUiSkin.Entry{id=r.id,sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/new/"+r.path)}).ToArray();
            EditorUtility.SetDirty(skin); AssetDatabase.SaveAssets();
            foreach(string name in Templates)
            {
                string path="Assets/Prefabs/"+name+".prefab";
                var prefab=PrefabUtility.LoadPrefabContents(path);
                try { Style(prefab); PrefabUtility.SaveAsPrefabAsset(prefab,path); }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            foreach(var root in scene.GetRootGameObjects()) { Undo.RegisterFullObjectHierarchyUndo(root,"UI-03 shared components"); Style(root); }
            var canvas=scene.GetRootGameObjects().First(g=>g.name=="Canvas");
            var confirm=canvas.transform.Find("Panel_Equipment/BottomBar/ConfirmButton") as RectTransform;
            if(confirm!=null)
            {
                // Reserve a separate hit target for the floating Menu action.
                confirm.anchorMin=confirm.anchorMax=new Vector2(.5f,0); confirm.pivot=new Vector2(.5f,0);
                confirm.anchoredPosition=new Vector2(390,24); confirm.sizeDelta=new Vector2(380,68);
            }
            if(canvas.GetComponent<SharedUiNavigation>()==null) canvas.AddComponent<SharedUiNavigation>();
            SharedUiShowcaseBuilder.Build(canvas.transform);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static bool In(Transform t,string name) { for(;t!=null;t=t.parent) if(t.name==name) return true; return false; }
        public static void Style(GameObject root)
        {
            foreach(var card in root.GetComponentsInChildren<EquipmentCardView>(true))
            {
                var fields=new SerializedObject(card);
                var caption=fields.FindProperty("nameText").objectReferenceValue as TMP_Text;
                if(caption==null) continue;
                ((RectTransform)card.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,500);
                var picture=fields.FindProperty("equipmentImage").objectReferenceValue as UnityEngine.UI.Image;
                if(picture!=null)
                {
                    picture.rectTransform.anchorMin=picture.rectTransform.anchorMax=new Vector2(.5f,1);
                    picture.rectTransform.pivot=new Vector2(.5f,1); picture.rectTransform.anchoredPosition=new Vector2(0,-24);
                    picture.rectTransform.sizeDelta=new Vector2(220,220);
                }
                caption.rectTransform.anchorMin=caption.rectTransform.anchorMax=new Vector2(.5f,1);
                caption.rectTransform.pivot=new Vector2(.5f,1); caption.rectTransform.anchoredPosition=new Vector2(0,-252);
                caption.rectTransform.sizeDelta=new Vector2(268,96); caption.textWrappingMode=TextWrappingModes.Normal;
                caption.alignment=TextAlignmentOptions.Top;
                var price=fields.FindProperty("priceText").objectReferenceValue as TMP_Text;
                if(price!=null)
                {
                    price.rectTransform.anchorMin=price.rectTransform.anchorMax=new Vector2(.5f,1);
                    price.rectTransform.pivot=new Vector2(.5f,1); price.rectTransform.anchoredPosition=new Vector2(0,-356);
                    price.rectTransform.sizeDelta=new Vector2(268,36);
                }
                var buy=fields.FindProperty("buyButton").objectReferenceValue as UnityEngine.UI.Button;
                if(buy!=null)
                {
                    var r=(RectTransform)buy.transform; r.anchorMin=r.anchorMax=new Vector2(.5f,0); r.pivot=new Vector2(.5f,0);
                    r.anchoredPosition=new Vector2(0,16); r.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,68);
                }
            }
            if(root.GetComponent<UnityEngine.UI.Image>()!=null)
            {
                if(root.name=="DetailRow") SharedUiStyles.Surface(root,"UI_TABLE_ROW_NEUTRAL");
                if(root.name=="ItemCard" || root.name=="Card_ProductItem") SharedUiStyles.Surface(root,"UI_CARD_NORMAL");
            }
            foreach(var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if(image.name.StartsWith("Shared")) continue;
                if(image.name=="Image" && image.transform.parent!=null && image.transform.parent.name=="Button"
                    && image.transform.parent.parent!=null && image.transform.parent.parent.name=="MenuAccess")
                { image.enabled=false; image.raycastTarget=false; continue; }
                string name=image.name.Trim().ToLowerInvariant();
                string sprite=image.sprite!=null?image.sprite.name.ToLowerInvariant():"";
                bool mask=image.GetComponent<UnityEngine.UI.Mask>()!=null || image.GetComponent<UnityEngine.UI.RectMask2D>()!=null;
                bool input=image.GetComponentInParent<TMP_InputField>()!=null;
                bool hit=name.Contains("hitarea")||name.Contains("scrim")||name.Contains("viewport")||image.GetComponent<UnityEngine.UI.Selectable>()!=null;
                if(!mask && !input && !hit) image.raycastTarget=false;
                if(name.Contains("selectionborder")) { SharedUiStyles.Surface(image.gameObject,"UI_CARD_SELECTED_OUTLINE"); image.enabled=false; }
                else if(name.Contains("shadow") && image.sprite!=null) SharedUiStyles.Surface(image.gameObject,"UI_PANEL_SHADOW");
                else if(name.StartsWith("chip_") || name=="chip") SharedUiStyles.Surface(image.gameObject,"UI_PILL_NORMAL");
                else if(name.EndsWith("pill") && image.GetComponentInParent<UnityEngine.UI.Button>()==null) SharedUiStyles.Surface(image.gameObject,"UI_HUD_PILL");
                if(sprite.StartsWith("icon_pictoicon_") || sprite.Contains("chevron-right-branco"))
                {
                    string id=sprite.Contains("bank")?"ICON_BANK":sprite.Contains("toolbox")?"ICON_EQUIPMENT":sprite.Contains("home")?"ICON_HOME":sprite.Contains("user")?"ICON_CLIENT":sprite.Contains("map")?"ICON_PIN":sprite.Contains("money")?"ICON_CASH":sprite.Contains("chevron")?"ICON_BACK":sprite.Contains("add_friend")?"ICON_TEAM":sprite.Contains("piechart")?"ICON_COHERENCE":"ICON_DOCUMENT";
                    SharedUiStyles.Surface(image.gameObject,id); image.color=GamePalette.TextOnSurface(image.transform.parent);
                    if(sprite.Contains("chevron")) image.transform.localRotation=Quaternion.identity;
                }
                string path=image.sprite!=null?AssetDatabase.GetAssetPath(image.sprite):"";
                if(image.sprite!=null && image.sprite.name=="icon_back") image.transform.localRotation=Quaternion.identity;
                if(path.StartsWith("Assets/Art/new/") && !image.sprite.name.StartsWith("icon_")) image.color=Color.white;
                if(path.StartsWith("Assets/Art/new/") && image.sprite.border==Vector4.zero)
                { image.type=UnityEngine.UI.Image.Type.Simple; image.preserveAspect=!path.Contains("/bars/") && !path.EndsWith("scrim.png"); }
            }
            foreach(var control in root.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
            {
                if(In(control.transform,"EstablishmentsLayer")) continue; // Map pin contracts belong to UI-04.
                SharedControlKind kind=Role(control);
                if(control.GetComponent<IdentificationInputAppearance>()!=null) continue; // Its validation state is owned by the form.
                SharedUiStyles.Bind(control,kind);
                if(control is UnityEngine.UI.Button || control is UnityEngine.UI.Toggle)
                {
                    var rect=(RectTransform)control.transform;
                    if(rect.rect.height>0 && rect.rect.height<68) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,68);
                    if(rect.rect.width>0 && rect.rect.width<68) rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,68);
                    var element=control.GetComponent<UnityEngine.UI.LayoutElement>();
                    if(element!=null && element.preferredHeight>0 && element.preferredHeight<68) element.preferredHeight=68;
                }
                if(control is UnityEngine.UI.Slider slider)
                {
                    var bg=slider.transform.Find("Background"); if(bg!=null) SharedUiStyles.Surface(bg.gameObject,"UI_SLIDER_TRACK");
                    if(slider.fillRect!=null) SharedUiStyles.Surface(slider.fillRect.gameObject,"UI_SLIDER_FILL");
                    if(bg is RectTransform track) track.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,12);
                    if(slider.fillRect!=null) slider.fillRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,12);
                    if(slider.handleRect!=null)
                    {
                        slider.handleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,64);
                        slider.handleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,64);
                    }
                }
                if(control is UnityEngine.UI.Scrollbar) SharedUiStyles.Surface(control.gameObject,"UI_SCROLLBAR_TRACK",true);
                foreach(var outline in control.GetComponents<UnityEngine.UI.Outline>()) outline.enabled=false;
            }
            foreach(var input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                input.image.color=Color.white; input.image.pixelsPerUnitMultiplier=1;
                if(input.textComponent!=null) input.textComponent.raycastTarget=false;
            }
            foreach(var picture in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                string path=picture.sprite!=null?AssetDatabase.GetAssetPath(picture.sprite):"";
                if(path.Contains("/Products/")||path.Contains("/Equipments/")||path.Contains("/characters/")||path.Contains("/products/")||path.Contains("/equipment/"))
                { picture.type=UnityEngine.UI.Image.Type.Simple; picture.preserveAspect=true; picture.color=Color.white; picture.raycastTarget=false; }
            }
        }
        private static SharedControlKind Role(UnityEngine.UI.Selectable control)
        {
            string name=control.name.ToLowerInvariant();
            if(control is TMP_InputField) return SharedControlKind.Input;
            if(control is UnityEngine.UI.Slider) return SharedControlKind.Slider;
            if(control is UnityEngine.UI.Scrollbar) return SharedControlKind.Scrollbar;
            if(control is UnityEngine.UI.Toggle) return SharedControlKind.Checkbox;
            if(name.Contains("card") || control.GetComponent<BankCardView>()!=null) return SharedControlKind.Card;
            if(name.Contains("segment") || name=="button_low" || name=="button_medium") return SharedControlKind.Tab;
            if(name.Contains("discard")||name.Contains("reset")) return SharedControlKind.Danger;
            if(name.Contains("cancel")||name.Contains("back")||name.Contains("close")) return SharedControlKind.Secondary;
            if(name.Contains("plus")||name.Contains("minus")) return SharedControlKind.Icon;
            return SharedControlKind.Primary;
        }
    }
}
