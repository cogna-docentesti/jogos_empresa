using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Adapter.In.UI
{
    /// <summary>Owns input only while a real operation is pending. Cancellation also invalidates its continuation.</summary>
    public sealed class SharedLoading : MonoBehaviour
    {
        [SerializeField] private CanvasGroup input;
        [SerializeField] private GameObject overlay;
        [SerializeField] private UnityEngine.UI.Button cancel;
        private Coroutine running;
        private IEnumerator operation;
        private Action<Exception> failed;
        private Action cancelled;
        private bool previousInteractable;
        private GameObject previousSelection;
        private int generation;
        public bool IsBusy { get; private set; }
        private void Awake() { cancel?.onClick.AddListener(Cancel); }
        public void Run(IEnumerator work, Action<Exception> onFailure, Action onCancel)
        {
            Cancel();
            // A sibling controller may start its operation during the parent's first OnEnable,
            // before Unity has invoked this component's own Awake/OnEnable callbacks.
            if (!gameObject.activeInHierarchy || !enabled) { (work as IDisposable)?.Dispose(); return; }
            operation = work; failed = onFailure; cancelled = onCancel;
            previousSelection = EventSystem.current?.currentSelectedGameObject;
            previousInteractable = input == null || input.interactable;
            IsBusy = true; if (input != null) input.interactable = false; overlay?.SetActive(true);
            if (cancel != null) cancel.Select();
            int token = ++generation;
            var coroutine = StartCoroutine(Execute(token));
            if (IsBusy && generation == token) running = coroutine;
        }
        private IEnumerator Execute(int token)
        {
            while (IsBusy && token == generation)
            {
                bool more; object wait = null; Exception error = null;
                try { more = operation.MoveNext(); if (more) wait = operation.Current; }
                catch (Exception e) { error = e; more = false; }
                if (!more)
                {
                    var callback = failed; Release();
                    if (error != null) callback?.Invoke(error);
                    yield break;
                }
                yield return wait;
            }
        }
        private void Release()
        {
            ++generation; IsBusy = false; running = null;
            var old = operation; operation = null; failed = null; cancelled = null;
            if (input != null) input.interactable = previousInteractable;
            overlay?.SetActive(false); (old as IDisposable)?.Dispose();
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (isActiveAndEnabled && (selected == null || selected == cancel?.gameObject))
            {
                var previous = previousSelection != null ? previousSelection.GetComponent<UnityEngine.UI.Selectable>() : null;
                if (previous != null && previous.IsActive() && previous.IsInteractable()) previous.Select();
                else GetComponentsInChildren<UnityEngine.UI.Selectable>().FirstOrDefault(s => s.IsActive() && s.IsInteractable())?.Select();
            }
        }
        public void Cancel()
        {
            if (!IsBusy) return;
            var callback = cancelled; if (running != null) StopCoroutine(running);
            Release(); callback?.Invoke();
        }
        private void OnDisable()
        {
            // Disabling a panel is cancellation, but must not emit feedback onto the next screen.
            if (!IsBusy) return;
            if (running != null) StopCoroutine(running); Release();
        }
    }
}
