using UnityEngine;

namespace Game.Adapter.In.UI.Navigation
{
    /// <summary>History action above revisitable panels; initial setup keeps its own footer.</summary>
    public sealed class HubBackButton : MonoBehaviour
    {
        [SerializeField] private GameObject buttonRoot;
        [SerializeField] private RectTransform exitButton;
        [SerializeField] private GameObject menuPanel;
        private Vector2 anchorMin,anchorMax,pivot,position,size;
        private bool? positionedForHub;
        public void Configure(GameObject button,RectTransform exit,GameObject menu)
        { buttonRoot=button; exitButton=exit; menuPanel=menu; }
        private void Awake()
        {
            if(exitButton==null) return;
            anchorMin=exitButton.anchorMin; anchorMax=exitButton.anchorMax; pivot=exitButton.pivot;
            position=exitButton.anchoredPosition; size=exitButton.sizeDelta;
        }
        private void Update()
        {
            bool visible=MenuNavigator.HubAvailable && MenuNavigator.Instance!=null
                && MenuNavigator.Instance.Current!=PanelId.None && MenuNavigator.Instance.Current!=PanelId.Menu;
            if(buttonRoot!=null && buttonRoot.activeSelf!=visible) buttonRoot.SetActive(visible);
            bool hub=menuPanel!=null && menuPanel.activeInHierarchy;
            if(exitButton==null || positionedForHub==hub) return;
            positionedForHub=hub;
            exitButton.anchorMin=hub?new Vector2(0,1):anchorMin;
            exitButton.anchorMax=hub?new Vector2(0,1):anchorMax;
            exitButton.pivot=hub?new Vector2(0,1):pivot;
            exitButton.anchoredPosition=hub?new Vector2(1632,-998):position;
            exitButton.sizeDelta=hub?new Vector2(264,68):size;
        }
    }
}
