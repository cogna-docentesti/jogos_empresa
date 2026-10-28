using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.Adapter.In.UI
{
    /// <summary>Owns modal layering, keyboard scope, cancellation and restoration as one lifecycle.</summary>
    public sealed class SharedModalFocus : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private UnityEngine.UI.Selectable initialFocus;
        [SerializeField] private UnityEngine.UI.Button cancelButton;
        [SerializeField] private bool allowCancel = true;
        private static readonly List<SharedModalFocus> stack = new();
        public static SharedModalFocus Active => stack.LastOrDefault(m => m != null && m.isActiveAndEnabled);
        public static int LastCancelFrame { get; private set; } = -1;
        private GameObject previous;
        private bool cancelling;
        private bool initializing;

        public static void CloseForNavigation()
        {
            // State handoff releases modal ownership; it does not invoke a destructive/cancel action.
            foreach (var modal in stack.ToArray())
                if (modal != null && modal.gameObject.activeSelf) modal.gameObject.SetActive(false);
        }

        public void Configure(UnityEngine.UI.Selectable first, UnityEngine.UI.Button cancel, bool cancellable = true)
        { initialFocus = first; cancelButton = cancel; allowCancel = cancellable; }

        private void OnEnable()
        {
            initializing = true;
            previous = EventSystem.current?.currentSelectedGameObject;
            stack.Remove(this); stack.Add(this);
            var canvas = GetComponent<Canvas>();
            if (canvas != null) { canvas.overrideSorting = true; canvas.sortingOrder = 1000 + stack.Count; }
            FeedbackToast.Current?.Hide();
            foreach (var tooltip in FindObjectsByType<SharedTooltip>(FindObjectsSortMode.None)) tooltip.Dismiss();
            SelectInitial();
        }

        private void Update()
        {
            if (Active != this) return;
            bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
            escape |= Input.GetKeyDown(KeyCode.Escape);
#endif
            if (escape) Cancel();
        }

        private void LateUpdate()
        {
            if (Active != this || EventSystem.current == null) return;
            // Child Selectables finish enabling after their parent's OnEnable.
            if (initializing) { initializing = false; SelectInitial(); }
            var controls = GetComponentsInChildren<UnityEngine.UI.Selectable>()
                .Where(s => s.IsActive() && s.IsInteractable() && s.navigation.mode != UnityEngine.UI.Navigation.Mode.None).ToArray();
            for (int i = 0; i < controls.Length; i++)
            {
                var before = controls[(i + controls.Length - 1) % controls.Length];
                var after = controls[(i + 1) % controls.Length];
                controls[i].navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnLeft = before, selectOnUp = before, selectOnRight = after, selectOnDown = after };
            }
            var target = EventSystem.current.currentSelectedGameObject;
            var control = target != null ? target.GetComponent<UnityEngine.UI.Selectable>() : null;
            if (target == null || !target.activeInHierarchy || !target.transform.IsChildOf(transform)
                || control == null || !control.IsInteractable()) SelectInitial();
        }

        private void SelectInitial()
        {
            var first = initialFocus != null && initialFocus.IsActive() && initialFocus.IsInteractable()
                ? initialFocus : GetComponentsInChildren<UnityEngine.UI.Selectable>().FirstOrDefault(s => s.IsActive() && s.IsInteractable());
            first?.Select();
        }

        public void OnCancel(BaseEventData eventData) => Cancel();
        public void Cancel()
        {
            if (Active != this || cancelling) return;
            // Explicit-action-only warnings consume Escape without opening Exit underneath.
            LastCancelFrame = Time.frameCount;
            if (!allowCancel) return;
            cancelling = true;
            try
            {
                if (cancelButton != null && cancelButton.IsActive() && cancelButton.IsInteractable()) cancelButton.onClick.Invoke();
                else gameObject.SetActive(false);
            }
            finally { cancelling = false; }
        }

        private void OnDisable()
        {
            stack.Remove(this);
            if (EventSystem.current == null) return;
            if (Active != null) { Active.SelectInitial(); return; }
            // Loading may finish while the dialog is open. Restore an available control in its panel.
            for (var scope = previous != null ? previous.transform : null; scope != null; scope = scope.parent)
            {
                if (!scope.gameObject.activeInHierarchy) continue;
                var candidate = scope.GetComponentsInChildren<UnityEngine.UI.Selectable>()
                    .FirstOrDefault(s => s.IsActive() && s.IsInteractable() && !s.transform.IsChildOf(transform));
                if (candidate != null) { candidate.Select(); return; }
            }
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
