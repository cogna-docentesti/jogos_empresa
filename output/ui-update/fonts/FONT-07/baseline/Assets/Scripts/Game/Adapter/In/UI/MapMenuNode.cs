using Game.Adapter.In.UI.Navigation;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Adapter.In.UI
{
    /// <summary>Map-specific appearance; the existing Button/PanelNavButton owns activation.</summary>
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class MapMenuNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private string locationId;
        [SerializeField] private PanelId destination;
        [SerializeField] private UnityEngine.UI.Image pin, label, tip, icon, focus;
        [SerializeField] private TMP_Text caption;
        [SerializeField] private Sprite[] pinStates, labelStates;
        private UnityEngine.UI.Button button;
        private bool selected, hovered, pressed;
        public string LocationId => locationId;
        public PanelId Destination => destination;
        public bool IsSelected => selected;
        public string CurrentState { get; private set; }

        public void Configure(string id, PanelId target, UnityEngine.UI.Image pinImage,
            UnityEngine.UI.Image labelImage, UnityEngine.UI.Image tipImage, UnityEngine.UI.Image iconImage,
            UnityEngine.UI.Image focusImage, TMP_Text text, Sprite[] pins, Sprite[] labels)
        {
            locationId=id; destination=target; pin=pinImage; label=labelImage; tip=tipImage;
            icon=iconImage; focus=focusImage; caption=text; pinStates=pins; labelStates=labels;
            Refresh();
        }
        private void OnEnable() { hovered=pressed=false; Refresh(); }
        private void OnDisable() { hovered=pressed=false; }
        private void LateUpdate() => Refresh();
        public void SetSelected(bool value) { selected=value; Refresh(); }
        public void OnPointerEnter(PointerEventData e) { hovered=e.pointerId<0; Refresh(); }
        public void OnPointerExit(PointerEventData e) { hovered=false; Refresh(); }
        public void OnPointerDown(PointerEventData e) { pressed=e.button==PointerEventData.InputButton.Left; Refresh(); }
        public void OnPointerUp(PointerEventData e) { pressed=false; Refresh(); }
        public void OnSelect(BaseEventData e) => Refresh();
        public void OnDeselect(BaseEventData e) { hovered=pressed=false; Refresh(); }

        public void Refresh()
        {
            if (button==null) button=GetComponent<UnityEngine.UI.Button>();
            bool enabledState=button!=null && button.IsInteractable();
            bool focused=EventSystem.current!=null && EventSystem.current.currentSelectedGameObject==gameObject;
            bool highlighted=selected || hovered || focused || pressed;
            CurrentState=!enabledState?"disabled":pressed?"pressed":focused?"focus":hovered?"hover":selected?"selected":"normal";
            int state=!enabledState?2:highlighted?1:0;
            if(pin!=null && pinStates!=null && pinStates.Length==3) { pin.sprite=pinStates[state]; pin.color=Color.white; }
            if(label!=null && labelStates!=null && labelStates.Length==3) { label.sprite=labelStates[state]; label.color=Color.white; }
            var content=!enabledState?GamePalette.Parse("#a69ea3"):highlighted?Color.white:GamePalette.Plum;
            if(icon!=null) icon.color=content;
            if(caption!=null) caption.color=content;
            if(tip!=null) tip.color=!enabledState?GamePalette.Parse("#e7e2dc"):highlighted?GamePalette.Primary:GamePalette.Surface;
            if(focus!=null) { focus.enabled=enabledState && focused; focus.color=GamePalette.Primary; }
        }
    }
}
