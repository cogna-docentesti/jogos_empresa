using System.Linq;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    /// <summary>Same composition for serialized templates and controls created by the catalog views.</summary>
    public static class SharedUiStyles
    {
        public static UnityEngine.UI.Image Surface(GameObject target,string id,bool hit=false)
        {
            var image=target.GetComponent<UnityEngine.UI.Image>() ?? target.AddComponent<UnityEngine.UI.Image>();
            var sprite=SharedUiSkin.Current?.Get(id);
            image.sprite=sprite; image.color=Color.white; image.raycastTarget=hit;
            image.type=sprite!=null && sprite.border!=Vector4.zero?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            image.pixelsPerUnitMultiplier=1; image.preserveAspect=image.type==UnityEngine.UI.Image.Type.Simple;
            return image;
        }

        public static SharedControlAppearance Bind(UnityEngine.UI.Selectable control,SharedControlKind kind)
        {
            var image=control.targetGraphic as UnityEngine.UI.Image ?? control.GetComponent<UnityEngine.UI.Image>();
            if(image==null) image=Surface(control.gameObject,"UI_PRIMARY_VIOLET_NORMAL",true);
            control.targetGraphic=image; control.transition=UnityEngine.UI.Selectable.Transition.None;
            image.CrossFadeColor(Color.white,0,true,true);
            image.raycastTarget=true;
            bool round=kind==SharedControlKind.Checkbox || kind==SharedControlKind.Radio || kind==SharedControlKind.Slider;
            image.type=round?UnityEngine.UI.Image.Type.Simple:UnityEngine.UI.Image.Type.Sliced;
            image.preserveAspect=round; image.pixelsPerUnitMultiplier=1;
            var navigation=control.navigation;
            if(navigation.mode==UnityEngine.UI.Navigation.Mode.None) navigation.mode=UnityEngine.UI.Navigation.Mode.Automatic;
            control.navigation=navigation;
            if(control is UnityEngine.UI.Slider || control is UnityEngine.UI.Scrollbar)
            {
                var target=(RectTransform)control.transform;
                var hit=control.transform.Find("SharedTouchTarget") as RectTransform;
                if(hit==null) { hit=(RectTransform)new GameObject("SharedTouchTarget",typeof(RectTransform)).transform; hit.SetParent(control.transform,false); }
                hit.SetAsFirstSibling(); hit.anchorMin=Vector2.zero; hit.anchorMax=Vector2.one;
                float x=Mathf.Max(0,(68-target.rect.width)*.5f),y=Mathf.Max(0,(68-target.rect.height)*.5f);
                hit.offsetMin=new Vector2(-x,-y); hit.offsetMax=new Vector2(x,y);
                var graphic=hit.GetComponent<UnityEngine.UI.Image>() ?? hit.gameObject.AddComponent<UnityEngine.UI.Image>();
                graphic.color=Color.clear; graphic.sprite=null; graphic.raycastTarget=true;
            }
            var focus = FocusImage(control.transform);
            var labels=control.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.GetComponentInParent<UnityEngine.UI.Selectable>(true)==control).ToArray();
            var icons=control.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(i=>i.sprite!=null && i.sprite.name.StartsWith("icon_") && i.GetComponentInParent<UnityEngine.UI.Selectable>(true)==control).ToArray();
            foreach(var t in labels) t.raycastTarget=false;
            foreach(var i in icons) { i.raycastTarget=false; i.preserveAspect=true; i.type=UnityEngine.UI.Image.Type.Simple; }
            var appearance=control.GetComponent<SharedControlAppearance>() ?? control.gameObject.AddComponent<SharedControlAppearance>();
            appearance.Configure(kind,image,focus,labels,icons);
            if(control is UnityEngine.UI.Toggle toggle && toggle.graphic!=null) toggle.graphic.enabled=false;
            return appearance;
        }

        private static UnityEngine.UI.Image FocusImage(Transform parent)
        {
            var existing = parent.Find("SharedFocus");
            var child = existing != null ? existing.gameObject : new GameObject("SharedFocus", typeof(RectTransform));
            child.transform.SetParent(parent, false);
            child.transform.SetAsLastSibling();
            var layout = child.GetComponent<UnityEngine.UI.LayoutElement>() ?? child.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.ignoreLayout = true;
            var image = child.GetComponent<UnityEngine.UI.Image>() ?? child.AddComponent<UnityEngine.UI.Image>();
            image.enabled = false;
            return image;
        }
    }
}
