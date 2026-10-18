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
    /// <summary>In-place migration: retain every production button, controller and scene reference.</summary>
    public static class DialogUiMigration
    {
        private static T Ensure<T>(Component c) where T : Component
        { var component=c.GetComponent<T>(); return component!=null?component:c.gameObject.AddComponent<T>(); }
        private static void Set(UnityEngine.Object o, string name, UnityEngine.Object value)
        { var so = new SerializedObject(o); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        private static T Get<T>(UnityEngine.Object o, string name) where T : UnityEngine.Object => (T)new SerializedObject(o).FindProperty(name).objectReferenceValue;
        private static RectTransform Node(Transform p, string name, float x, float y, float w, float h)
        {
            var r = p.Find(name) as RectTransform;
            if (r == null) { r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; r.SetParent(p, false); }
            Place(r,x,y,w,h); return r;
        }
        private static void Place(RectTransform r,float x,float y,float w,float h)
        { r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h); r.localScale=Vector3.one; r.localRotation=Quaternion.identity; }
        private static void Stretch(RectTransform r)
        { r.anchorMin=Vector2.zero; r.anchorMax=Vector2.one; r.offsetMin=r.offsetMax=Vector2.zero; r.localScale=Vector3.one; }
        private static TMP_Text Text(RectTransform r,string value,TypographyToken size,bool bold=false,Color? color=null)
        {
            var t=Ensure<TextMeshProUGUI>(r); GameTypography.Apply(t,size,bold);
            t.text=value; t.color=color??GamePalette.Ink; t.alignment=TextAlignmentOptions.TopLeft; t.textWrappingMode=TextWrappingModes.Normal; t.overflowMode=TextOverflowModes.Overflow; t.raycastTarget=false;
            return t;
        }
        private static void Icon(Transform p,string name,string id,float x,float y,float size,Color? tint=null)
        { var i=SharedUiStyles.Surface(Node(p,name,x,y,size,size).gameObject,id); i.gameObject.SetActive(true); i.color=tint??Color.white; i.preserveAspect=true; }
        private static void Modal(GameObject root,UnityEngine.UI.Button cancel)
        {
            Stretch((RectTransform)root.transform);
            var c=Ensure<Canvas>(root.transform); c.overrideSorting=true; c.sortingOrder=1001; Ensure<UnityEngine.UI.GraphicRaycaster>(root.transform);
            Ensure<SharedModalFocus>(root.transform).Configure(cancel,cancel); EditorUtility.SetDirty(root);
        }
        private static void Scrim(GameObject root)
        { var i=SharedUiStyles.Surface(root,"UI_SCRIM",true); i.preserveAspect=false; i.type=UnityEngine.UI.Image.Type.Simple; }
        private static void Button(UnityEngine.UI.Button b,string label,SharedControlKind kind,float x,float y,float w,string icon=null)
        {
            Place((RectTransform)b.transform,x,y,w,68);
            var layout=b.GetComponent<UnityEngine.UI.LayoutGroup>(); if(layout!=null) layout.enabled=false;
            var t=b.GetComponentInChildren<TMP_Text>(true); t.gameObject.SetActive(true);
            foreach(var old in b.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(i=>i.transform!=b.transform && i.name!="SharedFocus")) old.gameObject.SetActive(false);
            Place(t.rectTransform,icon==null?16:56,12,w-(icon==null?32:72),44); Text(t.rectTransform,label,GameTypography.Tokens.Button,true); t.alignment=TextAlignmentOptions.Center;
            if(icon!=null) Icon(b.transform,"ActionIcon",icon,16,18,32);
            SharedUiStyles.Bind(b,kind); b.transform.Find("SharedFocus").gameObject.SetActive(true);
        }
        private static void Header(Transform panel,TMP_Text title,string value,string icon,float width)
        {
            var strip=Node(panel,"ModalHeader",0,0,width,104); SharedUiStyles.Surface(strip.gameObject,"UI_PANEL_PLUM"); strip.SetAsFirstSibling();
            Icon(strip,"ContextIcon",icon,40,28,48);
            title.transform.SetParent(strip,false); Place(title.rectTransform,112,30,width-152,52); Text(title.rectTransform,value,GameTypography.Tokens.SectionTitle,true,GamePalette.OnDark);
        }
        [MenuItem("Tools/Jogos de Empresa/UI-18/Migrate production dialogs")]
        public static void Apply()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(UnityEngine.Application.dataPath!="D:/Cogna/jogos_empresa/Assets" || EditorApplication.isPlaying || scene.path!="Assets/Scenes/GameScene.unity") throw new InvalidOperationException("Use Cogna GameScene outside Play Mode.");
            var canvas=scene.GetRootGameObjects().Single(g=>g.name=="Canvas").transform;
            var exit=canvas.Find("ExitUI").GetComponent<ExitDialogController>(); var root=Get<GameObject>(exit,"dialogRoot");
            var panel=(RectTransform)root.transform.Find("DialogPanel");
            panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f); panel.anchoredPosition=Vector2.zero; panel.sizeDelta=new Vector2(1120,560); panel.localScale=Vector3.one;
            SharedUiStyles.Surface(panel.gameObject,"UI_MODAL_SURFACE",true);
            var back=Node(root.transform,"Backdrop",0,0,1920,1080); Stretch(back); Scrim(back.gameObject); back.SetAsFirstSibling();
            var cancel=Get<UnityEngine.UI.Button>(exit,"cancelButton"); var discard=Get<UnityEngine.UI.Button>(exit,"discardButton"); var save=Get<UnityEngine.UI.Button>(exit,"saveExitButton");
            var row=panel.Find("ButtonRow"); var group=row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>(); group.enabled=true;
            Place((RectTransform)row,48,440,1024,72); group.spacing=24; group.childControlWidth=false; group.childControlHeight=false; group.childForceExpandWidth=group.childForceExpandHeight=false; group.childAlignment=TextAnchor.MiddleCenter;
            Button(cancel,"Cancelar",SharedControlKind.Secondary,0,0,320);
            Button(discard,"Sair sem salvar",SharedControlKind.Danger,0,0,320,"ICON_EXIT");
            Button(save,"Salvar e sair",SharedControlKind.Primary,0,0,336,"ICON_SAVE");
            Header(panel,Get<TMP_Text>(exit,"titleText"),"Sair do jogo","ICON_EXIT",1120);
            var body=Get<TMP_Text>(exit,"bodyText"); Place(body.rectTransform,48,144,1024,252); Text(body.rectTransform,"As opções de saída dependem do progresso salvo da sua campanha.",GameTypography.Tokens.Body);
            Modal(root,cancel); root.SetActive(false); EditorUtility.SetDirty(exit);
            var view=canvas.Find("Panel_Establishment").GetComponent<EstablishmentSummaryView>();
            root=Get<GameObject>(view,"resetWarning"); panel=(RectTransform)root.transform.Find("Dialog");
            panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f); panel.anchoredPosition=Vector2.zero; panel.sizeDelta=new Vector2(1120,640); panel.localScale=Vector3.one; SharedUiStyles.Surface(panel.gameObject,"UI_MODAL_SURFACE",true); Scrim(root);
            var title=(panel.Find("Title") ?? panel.Find("ModalHeader/Title")).GetComponent<TMP_Text>(); var message=panel.Find("Text (TMP)").GetComponent<TMP_Text>();
            Header(panel,title,"Reiniciar esta campanha?","ICON_ALERT",1120); Place(message.rectTransform,48,144,1024,260);
            Text(message.rectTransform,"As decisões, compras e resultados desta campanha serão apagados permanentemente. O cadastro também será limpo.\n\nVocê voltará à identificação para refazer as escolhas iniciais. Esta ação não pode ser desfeita.",GameTypography.Tokens.Body);
            var error=Text(Node(panel,"Error",48,404,1024,76),"",GameTypography.Tokens.Label,false,GamePalette.ErrorText); error.gameObject.SetActive(false);
            Set(view,"resetTitle",title); Set(view,"resetBody",message); Set(view,"resetError",error);
            cancel=Get<UnityEngine.UI.Button>(view,"cancelResetButton"); var confirm=Get<UnityEngine.UI.Button>(view,"confirmResetButton");
            Button(cancel,"Cancelar",SharedControlKind.Secondary,48,524,496); Button(confirm,"Reiniciar jogo",SharedControlKind.Danger,576,524,496,"ICON_RESET");
            Modal(root,cancel); root.SetActive(false); EditorUtility.SetDirty(view);
            foreach(var name in new[]{"Panel_FinalReport","Panel_Bankruptcy"})
            {
                root=canvas.Find(name+"/NewGameDialog").gameObject; Scrim(root); cancel=root.transform.Find("Dialog/Cancel").GetComponent<UnityEngine.UI.Button>(); Modal(root,cancel);
                var heading=root.transform.Find("Dialog/Title").GetComponent<TMP_Text>(); Place(heading.rectTransform,96,32,832,64); Icon(root.transform.Find("Dialog"),"ContextIcon","ICON_PLAY",32,36,40,GamePalette.Primary);
                root.SetActive(false);
            }
            // Update the actual reusable equipment templates; retain their callbacks and field references.
            foreach(var path in new[]{"Assets/Prefabs/MainArea_Equipment.prefab","Assets/Prefabs/Panel_Equipment.prefab"})
            {
                var prefab=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var cart=prefab.GetComponentsInChildren<SharedModalFocus>(true).FirstOrDefault(); if(cart==null) continue;
                    cancel=cart.transform.Find("CartPanel/Close").GetComponent<UnityEngine.UI.Button>(); Modal(cart.gameObject,cancel);
                    var heading=cart.transform.Find("CartPanel/Title").GetComponent<TMP_Text>(); Place(heading.rectTransform,96,24,1036,64); Icon(heading.transform.parent,"ContextIcon","ICON_EQUIPMENT",32,32,40,GamePalette.Primary);
                    PrefabUtility.SaveAsPrefabAsset(prefab,path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
