using UnityEngine;

namespace Game.Adapter.In.UI.Navigation
{
    /// <summary>History action above revisitable panels; initial setup keeps its own footer.</summary>
    public sealed class HubBackButton : MonoBehaviour
    {
        [SerializeField] private GameObject buttonRoot;
        [SerializeField] private RectTransform exitButton;
        [SerializeField] private GameObject menuPanel;
        [SerializeField] private GameObject eventPanel;
        [SerializeField] private GameObject monthlyResultPanel;
        [SerializeField] private GameObject quarterlyResultPanel;
        [SerializeField] private GameObject finalReportPanel;
        [SerializeField] private GameObject bankruptcyPanel;
        [SerializeField] private RectTransform eventMenuAccess;
        private Vector2 anchorMin,anchorMax,pivot,position,size;
        private Vector2 menuAnchorMin,menuAnchorMax,menuPivot,menuPosition,menuSize;
        private bool? positionedForHub;
        private bool? positionedForEvents;
        private RectTransform menuCanvas;
        public void Configure(GameObject button,RectTransform exit,GameObject menu)
        { buttonRoot=button; exitButton=exit; menuPanel=menu; }
        public void ConfigureEvents(GameObject panel,RectTransform menu)
        { eventPanel=panel; eventMenuAccess=menu; }
        public void ConfigureMonthlyResults(GameObject panel) { monthlyResultPanel=panel; }
        public void ConfigureQuarterlyResults(GameObject panel) { quarterlyResultPanel=panel; }
        public void ConfigureFinalReport(GameObject panel) { finalReportPanel=panel; }
        public void ConfigureBankruptcy(GameObject panel) { bankruptcyPanel=panel; }
        private void Awake()
        {
            if(eventMenuAccess!=null)
            {
                menuCanvas=(RectTransform)eventMenuAccess.GetComponentInParent<Canvas>().rootCanvas.transform;
                menuAnchorMin=eventMenuAccess.anchorMin;menuAnchorMax=eventMenuAccess.anchorMax;menuPivot=eventMenuAccess.pivot;
                menuPosition=eventMenuAccess.anchoredPosition;menuSize=eventMenuAccess.sizeDelta;
            }
            if(exitButton==null) return;
            anchorMin=exitButton.anchorMin; anchorMax=exitButton.anchorMax; pivot=exitButton.pivot;
            position=exitButton.anchoredPosition; size=exitButton.sizeDelta;
        }
        private void Update()
        {
            bool visible=MenuNavigator.HubAvailable && MenuNavigator.Instance!=null
                && MenuNavigator.Instance.Current!=PanelId.None && MenuNavigator.Instance.Current!=PanelId.Menu;
            if(buttonRoot!=null && buttonRoot.activeSelf!=visible) buttonRoot.SetActive(visible);
            bool events=(eventPanel!=null && eventPanel.activeInHierarchy) || (monthlyResultPanel!=null && monthlyResultPanel.activeInHierarchy) || (quarterlyResultPanel!=null && quarterlyResultPanel.activeInHierarchy) || (finalReportPanel!=null && finalReportPanel.activeInHierarchy) || (bankruptcyPanel!=null && bankruptcyPanel.activeInHierarchy);
            if(eventMenuAccess!=null && positionedForEvents!=events)
            {
                positionedForEvents=events;
                eventMenuAccess.anchorMin=events?new Vector2(0,1):menuAnchorMin;
                eventMenuAccess.anchorMax=events?new Vector2(0,1):menuAnchorMax;
                eventMenuAccess.pivot=events?new Vector2(0,1):menuPivot;
                eventMenuAccess.anchoredPosition=events?new Vector2(968,-998):menuPosition;
                eventMenuAccess.sizeDelta=events?new Vector2(252,68):menuSize;
            }
            if(events && eventMenuAccess!=null && menuCanvas!=null)
                eventMenuAccess.position=menuCanvas.TransformPoint(new Vector3(menuCanvas.rect.xMin+968,menuCanvas.rect.yMax-998,0));
            bool hub=(menuPanel!=null && menuPanel.activeInHierarchy) || events;
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
