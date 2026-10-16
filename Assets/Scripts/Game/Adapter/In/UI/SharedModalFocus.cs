using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Adapter.In.UI
{
    public sealed class SharedModalFocus : MonoBehaviour
    {
        public static int LastCancelFrame { get; private set; } = -1;
        private GameObject previous;
        private void OnEnable()
        {
            if(EventSystem.current==null) return;
            previous=EventSystem.current.currentSelectedGameObject;
            var first=GetComponentInChildren<UnityEngine.UI.Selectable>();
            if(first!=null) first.Select();
        }
        private void Update()
        {
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
            { LastCancelFrame=Time.frameCount; gameObject.SetActive(false); }
            if(EventSystem.current==null) return;
            var target=EventSystem.current.currentSelectedGameObject;
            if(target!=null && target.transform.IsChildOf(transform)) return;
            var first=GetComponentInChildren<UnityEngine.UI.Selectable>(); if(first!=null) first.Select();
        }
        private void OnDisable()
        {
            if(EventSystem.current!=null && previous!=null && previous.activeInHierarchy) EventSystem.current.SetSelectedGameObject(previous);
        }
    }
}
