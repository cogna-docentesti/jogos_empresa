using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Adapter.In.UI
{
    /// <summary>Tab traversal supplements the EventSystem's arrows/submit and touch. A modal limits the traversal scope.</summary>
    public sealed class SharedUiNavigation : MonoBehaviour
    {
        private void Update()
        {
            if(EventSystem.current==null) return;
            bool tab=Keyboard.current!=null && Keyboard.current.tabKey.wasPressedThisFrame;
            bool backwards=Keyboard.current!=null && Keyboard.current.shiftKey.isPressed;
#if ENABLE_LEGACY_INPUT_MANAGER
            tab |= Input.GetKeyDown(KeyCode.Tab);
            backwards |= Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
            if(!tab) return;
            var modal=FindFirstObjectByType<SharedModalFocus>();
            var preview=FindFirstObjectByType<SharedComponentsShowcase>();
            // Identification keeps its validated Name -> RA -> Register traversal.
            if(modal==null && preview==null && FindFirstObjectByType<UIIndentification>()!=null) return;
            Transform scope=modal!=null?modal.transform:preview!=null?preview.transform:transform;
            var controls=scope.GetComponentsInChildren<UnityEngine.UI.Selectable>()
                .Where(s=>s.IsActive() && s.IsInteractable() && s.navigation.mode!=UnityEngine.UI.Navigation.Mode.None).ToArray();
            if(controls.Length==0) return;
            int current=System.Array.FindIndex(controls,s=>s.gameObject==EventSystem.current.currentSelectedGameObject);
            int direction=backwards?-1:1;
            int next=current<0?(direction>0?0:controls.Length-1):(current+direction+controls.Length)%controls.Length;
            controls[next].Select();
        }
    }
}
