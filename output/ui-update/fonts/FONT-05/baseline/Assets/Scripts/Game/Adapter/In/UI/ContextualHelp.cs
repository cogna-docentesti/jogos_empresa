using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Adapter.In.UI
{
    /// <summary>Optional guidance scoped to the current campaign; saved actions also suppress it after reload.</summary>
    public sealed class ContextualHelp : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject regularHint;
        [SerializeField] private UnityEngine.UI.Button dismiss;
        [SerializeField] private GameState stage;
        private static readonly HashSet<string> understood = new();
        private string Key => GameSessionState.Current?.sessionId + ":" + stage;
        public bool IsVisible => panel != null && panel.activeSelf;
        private void Awake() { dismiss?.onClick.AddListener(Complete); }
        public void Refresh(bool available, bool alreadyDone)
        {
            bool show = available && !alreadyDone && !understood.Contains(Key)
                && GameManager.Instance?.StateMachine?.CurrentState == stage;
            if (panel != null) panel.SetActive(show);
            if (regularHint != null) regularHint.SetActive(!show);
        }
        public void Complete()
        {
            bool restore = panel != null && EventSystem.current?.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(panel.transform);
            if (GameSessionState.HasSession) understood.Add(Key);
            if (panel != null) panel.SetActive(false);
            if (regularHint != null) regularHint.SetActive(true);
            if (restore) GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(b => b.IsInteractable())?.Select();
        }
    }
}
