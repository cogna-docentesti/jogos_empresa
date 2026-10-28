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
        private TMPro.TMP_Text message;
        private float minimumHeight, minimumMessageHeight, initialTop, initialDismissTop;
        private bool stacked;
        private void Reflow()
        {
            message ??= panel.GetComponentInChildren<TMPro.TMP_Text>(true);
            if(message==null || panel.transform is not RectTransform surface || dismiss==null)return;
            var text=message.rectTransform;var action=(RectTransform)dismiss.transform;
            if(minimumHeight<=0)
            {
                minimumHeight=surface.rect.height;minimumMessageHeight=text.rect.height;
                initialTop=-surface.anchoredPosition.y;initialDismissTop=-action.anchoredPosition.y;
                stacked=initialDismissTop>=-text.anchoredPosition.y+minimumMessageHeight;
            }
            float height=Mathf.Max(minimumMessageHeight,Mathf.Ceil(message.GetPreferredValues(message.text,text.rect.width,Mathf.Infinity).y)+2);
            text.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            if(stacked)action.anchoredPosition=new Vector2(action.anchoredPosition.x,-(initialDismissTop+height-minimumMessageHeight));
            float surfaceHeight=Mathf.Max(minimumHeight,Mathf.Max(-text.anchoredPosition.y+height,-action.anchoredPosition.y+action.rect.height)+16);
            surface.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,surfaceHeight);
            if(surface.parent is RectTransform parent && initialTop>parent.rect.height/2)
                surface.anchoredPosition=new Vector2(surface.anchoredPosition.x,-Mathf.Max(0,initialTop+minimumHeight-surfaceHeight));
        }
        private void LateUpdate(){if(IsVisible && panel.activeInHierarchy)Reflow();}
        private void Awake() { dismiss?.onClick.AddListener(Complete); }
        public void Refresh(bool available, bool alreadyDone)
        {
            bool show = available && !alreadyDone && !understood.Contains(Key)
                && GameManager.Instance?.StateMachine?.CurrentState == stage;
            if (panel != null) panel.SetActive(show);
            if(show)Reflow();
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
