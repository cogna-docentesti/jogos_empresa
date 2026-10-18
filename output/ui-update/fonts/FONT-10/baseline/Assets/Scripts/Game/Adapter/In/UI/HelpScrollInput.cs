using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Adapter.In.UI
{
    /// <summary>Only user scroll gestures acknowledge guidance; layout and programmatic scroll resets do not.</summary>
    public sealed class HelpScrollInput : MonoBehaviour, IBeginDragHandler, IScrollHandler
    {
        public ContextualHelp help;
        public void OnBeginDrag(PointerEventData e) { if (help != null && help.IsVisible) help.Complete(); }
        public void OnScroll(PointerEventData e) { if (e.scrollDelta.sqrMagnitude > 0 && help != null && help.IsVisible) help.Complete(); }
    }
}
