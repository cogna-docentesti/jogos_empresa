using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Adapter.In.UI
{
    /// <summary>A native text panel shared by mouse hover, keyboard focus and touch activation.</summary>
    public sealed class SharedTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler,
        IDeselectHandler, IPointerClickHandler, ISubmitHandler
    {
        public GameObject panel;
        private bool hovered, focused, pinned;
        private void Show() { if(panel!=null) panel.SetActive(hovered || focused || pinned); }
        public void OnPointerEnter(PointerEventData e) { hovered=e.pointerId<0; Show(); }
        public void OnPointerExit(PointerEventData e) { hovered=false; Show(); }
        public void OnSelect(BaseEventData e) { focused=true; Show(); }
        public void OnDeselect(BaseEventData e) { focused=pinned=false; Show(); }
        public void OnPointerClick(PointerEventData e) { if(e.pointerId>=0) { pinned=!pinned; Show(); } }
        public void OnSubmit(BaseEventData e) { pinned=!pinned; Show(); }
        private void OnDisable() { hovered=focused=pinned=false; Show(); }
    }
}
