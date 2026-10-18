using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Adapter.In.UI
{
    /// <summary>A native text panel shared by mouse hover, keyboard focus and touch activation.</summary>
    public sealed class SharedTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler,
        IDeselectHandler, IPointerClickHandler, ISubmitHandler, ICancelHandler
    {
        public GameObject panel;
        private bool hovered, focused, pinned;
        private TMPro.TMP_Text message;
        private float minimumHeight, minimumMessageHeight;
        private void Reflow()
        {
            message ??= panel.GetComponentInChildren<TMPro.TMP_Text>(true);
            if(message==null || panel.transform is not RectTransform surface)return;
            var text=message.rectTransform;
            if(minimumHeight<=0){minimumHeight=surface.rect.height;minimumMessageHeight=text.rect.height;}
            float height=Mathf.Max(minimumMessageHeight,Mathf.Ceil(message.GetPreferredValues(message.text,text.rect.width,Mathf.Infinity).y)+2);
            text.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            surface.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(minimumHeight,-text.anchoredPosition.y+height+16));
        }
        private void Show()
        {
            if(panel==null)return;
            bool visible=SharedModalFocus.Active == null && (hovered || focused || pinned);
            panel.SetActive(visible);
            if(visible)Reflow();
        }
        private void LateUpdate(){if(panel!=null && panel.activeInHierarchy)Reflow();}
        public void Dismiss() { hovered=focused=pinned=false; Show(); }
        public void OnPointerEnter(PointerEventData e) { hovered=e.pointerId<0; Show(); }
        public void OnPointerExit(PointerEventData e) { hovered=false; Show(); }
        public void OnSelect(BaseEventData e) { focused=true; Show(); }
        public void OnDeselect(BaseEventData e) { focused=pinned=false; Show(); }
        public void OnPointerClick(PointerEventData e) { if(e.pointerId>=0) { hovered=focused=false; pinned=!pinned; Show(); } }
        public void OnSubmit(BaseEventData e) { hovered=focused=false; pinned=!pinned; Show(); }
        public void OnCancel(BaseEventData e) { hovered=focused=pinned=false; Show(); }
        private void OnDisable() { hovered=focused=pinned=false; Show(); }
    }
}
