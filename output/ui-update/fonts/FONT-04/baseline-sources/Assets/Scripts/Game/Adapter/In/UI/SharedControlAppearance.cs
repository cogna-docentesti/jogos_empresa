using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Adapter.In.UI
{
    public enum SharedControlKind { Primary, Secondary, Danger, Accent, Icon, Card, Tab, Pill, Checkbox, Radio, Toggle, Input, Slider, Scrollbar, Navigation }

    /// <summary>Visual states only: never replaces a Selectable or registers a business callback.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.Selectable))]
    public sealed class SharedControlAppearance : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField] private SharedControlKind kind;
        [SerializeField] private UnityEngine.UI.Image surface;
        [SerializeField] private UnityEngine.UI.Image focusLayer;
        [SerializeField] private UnityEngine.UI.Image selectionLayer;
        [SerializeField] private UnityEngine.UI.Image disabledLayer;
        [SerializeField] private TMP_Text[] labels = System.Array.Empty<TMP_Text>();
        [SerializeField] private UnityEngine.UI.Image[] icons = System.Array.Empty<UnityEngine.UI.Image>();
        private UnityEngine.UI.Selectable control;
        private SharedUiSkin skin;
        private bool hovered, pressed, selected;
        private float submitUntil;
        public bool IsInvalid { get; set; }
        public bool IsSelected => selected;
        public SharedControlKind Kind => kind;
        public string CurrentState { get; private set; }

        public void Configure(SharedControlKind role, UnityEngine.UI.Image image, UnityEngine.UI.Image focus,
            UnityEngine.UI.Image selection, UnityEngine.UI.Image disabled, TMP_Text[] text, UnityEngine.UI.Image[] glyphs)
        {
            kind=role; surface=image; focusLayer=focus; selectionLayer=selection; disabledLayer=disabled;
            labels=text; icons=glyphs; Cache(); Refresh();
        }
        private void Cache() { if(control==null) control=GetComponent<UnityEngine.UI.Selectable>(); if(skin==null) skin=SharedUiSkin.Current; }
        private void OnEnable() { hovered=pressed=false; Cache(); Refresh(); }
        private void OnDisable() { hovered=pressed=false; }
        private void LateUpdate() => Refresh();
        public void SetSelected(bool value) { selected=value; Refresh(); }
        public void OnPointerEnter(PointerEventData e) { hovered=e.pointerId<0; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hovered=false; Refresh(); }
        public void OnPointerDown(PointerEventData e) { if(e.button==PointerEventData.InputButton.Left) pressed=true; Refresh(); }
        public void OnPointerUp(PointerEventData e) { pressed=false; if(e.pointerId>=0) hovered=false; Refresh(); }
        public void OnSelect(BaseEventData e) { Refresh(); RevealInScroll(); }
        public void OnDeselect(BaseEventData e) { pressed=hovered=false; Refresh(); }
        public void OnSubmit(BaseEventData e) { submitUntil=Time.unscaledTime+.15f; Refresh(); }

        public void Refresh()
        {
            Cache(); if(control==null || skin==null || surface==null) return;
            bool enabledState=control.IsInteractable();
            bool focus=EventSystem.current!=null && EventSystem.current.currentSelectedGameObject==gameObject;
            var input=control as TMP_InputField;
            var toggle=control as UnityEngine.UI.Toggle;
            bool chosen=selected || (toggle!=null && toggle.isOn);
            bool down=pressed || Time.unscaledTime<submitUntil;
            CurrentState=!enabledState?"disabled":IsInvalid?"invalid":down?"pressed":chosen?"selected":focus?"focus":hovered?"hover":"normal";
            string prefix=kind switch {
                SharedControlKind.Primary=>"UI_PRIMARY_VIOLET", SharedControlKind.Secondary=>"UI_SECONDARY_IVORY",
                SharedControlKind.Danger=>"UI_DANGER_CORAL", SharedControlKind.Accent=>"UI_ACCENT_AMBER",
                SharedControlKind.Icon=>"UI_ICON_BUTTON", SharedControlKind.Tab=>"UI_TAB", SharedControlKind.Pill=>"UI_PILL",
                SharedControlKind.Input=>"UI_INPUT", SharedControlKind.Checkbox=>"UI_CHECKBOX", SharedControlKind.Radio=>"UI_RADIO",
                SharedControlKind.Toggle=>"UI_TOGGLE", _=>"UI_CARD" };
            string state=!enabledState?"DISABLED":down?"PRESSED":hovered?"HOVER":"NORMAL";
            if(kind==SharedControlKind.Input) state=!enabledState?"DISABLED":IsInvalid?"INVALID":(focus || input!=null && input.isFocused)?"FOCUS":"NORMAL";
            if(kind==SharedControlKind.Checkbox || kind==SharedControlKind.Radio || kind==SharedControlKind.Toggle)
                state=!enabledState?"DISABLED":chosen?"ON":"OFF";
            if(kind==SharedControlKind.Pill) state=!enabledState?"DISABLED":chosen?"SELECTED":"NORMAL";
            if(kind==SharedControlKind.Tab && chosen && enabledState && !down && !hovered) state="SELECTED";
            Sprite sprite=kind==SharedControlKind.Slider ? skin.Get("UI_SLIDER_THUMB")
                : kind==SharedControlKind.Scrollbar ? skin.Get("UI_SCROLLBAR_THUMB")
                : kind==SharedControlKind.Card ? skin.Get("UI_CARD_NORMAL") : skin.Get(prefix+"_"+state);
            if(sprite!=null) { surface.overrideSprite=null; surface.sprite=sprite; }
            surface.color=kind==SharedControlKind.Slider || kind==SharedControlKind.Scrollbar
                ? (!enabledState?GamePalette.Gray:down?GamePalette.Plum:hovered?GamePalette.Primary:Color.white) : Color.white;
            if(focusLayer!=null) { focusLayer.enabled=enabledState && focus; focusLayer.color=DarkSurface?GamePalette.Lavender:GamePalette.Primary; }
            if(selectionLayer!=null) selectionLayer.enabled=chosen && enabledState;
            if(disabledLayer!=null) disabledLayer.enabled=!enabledState;
            Color textColor=!enabledState?GamePalette.Muted:DarkSurface?GamePalette.OnDark:GamePalette.Ink;
            foreach(var label in labels) if(label!=null) label.color=textColor;
            foreach(var icon in icons) if(icon!=null) icon.color=textColor;
            if(kind==SharedControlKind.Navigation)
            {
                surface.color=new Color(1,1,1,(hovered || down) ? .10f : 0);
                foreach(var label in labels)if(label!=null)label.color=chosen?GamePalette.OnDark:GamePalette.Lavender;
                foreach(var icon in icons)if(icon!=null)icon.color=GamePalette.Lavender;
            }
        }
        private bool DarkSurface => kind==SharedControlKind.Primary || kind==SharedControlKind.Danger
            || surface!=null && surface.sprite!=null && (surface.sprite.name=="pill_selected" || surface.sprite.name=="tab_selected" || surface.sprite.name=="tab_pressed");
        private void RevealInScroll()
        {
            var scroll=GetComponentInParent<UnityEngine.UI.ScrollRect>();
            if(scroll==null || scroll.content==null || scroll.viewport==null || !scroll.vertical) return;
            Canvas.ForceUpdateCanvases();
            var corners=new Vector3[4]; ((RectTransform)transform).GetWorldCorners(corners);
            float bottom=scroll.viewport.InverseTransformPoint(corners[0]).y, top=scroll.viewport.InverseTransformPoint(corners[1]).y;
            float delta=top>scroll.viewport.rect.yMax?scroll.viewport.rect.yMax-top:bottom<scroll.viewport.rect.yMin?scroll.viewport.rect.yMin-bottom:0;
            scroll.content.anchoredPosition+=new Vector2(0,delta); scroll.velocity=Vector2.zero;
        }
    }
}
