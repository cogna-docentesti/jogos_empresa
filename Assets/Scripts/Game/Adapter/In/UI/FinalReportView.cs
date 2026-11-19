using System;
using System.Collections.Generic;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class FinalReportView : MonoBehaviour
    {
        [Serializable] public sealed class BlockView { public TMP_Text title, text, source; public RectTransform root; }
        [SerializeField] private TMP_Text company, identity, conclusion, period, revenue, net, cash, reputation, alignment, stateTitle, stateMessage, newGameMessage;
        [SerializeField] private GameObject loadedBody, stateBody, newGameDialog;
        [SerializeField] private GameObject[] pages;
        [SerializeField] private UnityEngine.UI.Button[] tabs, monthButtons;
        [SerializeField] private UnityEngine.UI.Button quarterlyButton, retryButton, backButton, newGameButton, cancelNewGame, confirmNewGame;
        [SerializeField] private BlockView[] bmc, reflection;
        [SerializeField] private UnityEngine.UI.ScrollRect overviewScroll, bmcScroll, reflectionScroll;
        private int tab;
        public string DisplayState { get; private set; }
        public int Tab => tab;
        public IReadOnlyList<UnityEngine.UI.Button> MonthButtons => monthButtons;
        public UnityEngine.UI.Button QuarterlyButton => quarterlyButton;
        public UnityEngine.UI.Button BackButton => backButton;
        public UnityEngine.UI.Button RetryButton => retryButton;
        public UnityEngine.UI.Button NewGameButton => newGameButton;
        public UnityEngine.UI.Button ConfirmNewGame => confirmNewGame;
        public UnityEngine.UI.Button CancelNewGame => cancelNewGame;
        public IReadOnlyList<UnityEngine.UI.Button> Tabs => tabs;
        public IReadOnlyList<BlockView> Bmc => bmc;
        public IReadOnlyList<BlockView> Reflection => reflection;
        public UnityEngine.UI.ScrollRect CurrentScroll => tab == 0 ? overviewScroll : tab == 1 ? bmcScroll : reflectionScroll;
        private static void Bind(UnityEngine.UI.Button button, Action action)
        { button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => action()); }
        public void Actions(Action<int> month, Action quarter, Action retry, Action start)
        {
            for (int i = 0; i < 3; i++) { int index = i; Bind(monthButtons[i], () => month(index + 1)); Bind(tabs[i], () => ShowTab(index)); }
            Bind(quarterlyButton, quarter); Bind(retryButton, retry); Bind(backButton, () => { ShowTab(0); tabs[tab].Select(); });
            Bind(newGameButton, () => { newGameMessage.text = "Seu relatório e os três resultados mensais continuarão salvos. A nova campanha começa na identificação e só será gravada ao confirmar o cardápio. Deseja continuar?"; newGameDialog.SetActive(true); cancelNewGame.Select(); });
            Bind(cancelNewGame, CloseNewGame); Bind(confirmNewGame, start);
        }
        public void CloseNewGame() { newGameDialog.SetActive(false); }
        public void NewGameFailure(string message) { newGameMessage.text = message; }
        public void ShowTab(int index)
        {
            tab = index;
            for (int i = 0; i < pages.Length; i++) { pages[i].SetActive(i == tab); tabs[i].GetComponent<SharedControlAppearance>().SetSelected(i == tab); }
            backButton.interactable = tab != 0; Canvas.ForceUpdateCanvases();
        }
        private static void Fit(TMP_Text text, string value, float width, float minimum)
        { text.text = value; text.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = Mathf.Max(minimum, text.GetPreferredValues(value, width, 0).y + 8); }
        private static void Blocks(BlockView[] views, FinalReportPresentation.Block[] data, int columns)
        {
            for (int i = 0; i < views.Length; i++)
            {
                var v = views[i]; var d = data[i]; float width = columns == 3 ? 552 : 1336;
                v.title.text = d.Title; v.text.text = d.Text; v.source.text = d.Source;
                float heading = Mathf.Max(40, v.title.GetPreferredValues(d.Title, width - 64, 0).y + 4);
                float body = Mathf.Max(40, v.text.GetPreferredValues(d.Text, width, 0).y + 4);
                float sourceHeight = Mathf.Max(48, v.source.GetPreferredValues(d.Source, width, 0).y + 4);
                v.title.rectTransform.sizeDelta = new Vector2(width - 64, heading);
                v.text.rectTransform.anchoredPosition = new Vector2(24, -24 - heading - 16); v.text.rectTransform.sizeDelta = new Vector2(width, body);
                v.source.rectTransform.anchoredPosition = new Vector2(24, -24 - heading - 16 - body - 20); v.source.rectTransform.sizeDelta = new Vector2(width, sourceHeight);
                v.root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 24 + heading + 16 + body + 20 + sourceHeight + 24;
            }
            if (columns == 3)
                for (int i = 0; i < views.Length; i += 3)
                { float height = 0; for (int j = i; j < i + 3; j++) height = Mathf.Max(height, views[j].root.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight); views[i].root.parent.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height; }
        }
        public void Show(FinalReportPresentation.Report data)
        {
            DisplayState = "loaded"; loadedBody.SetActive(true); stateBody.SetActive(false); period.text = data.Period;
            Fit(company, data.Company, 1216, 44); Fit(identity, data.Identity, 1216, 32); Fit(conclusion, data.Conclusion, 1216, 76);
            revenue.text = data.Revenue; net.text = data.Net; cash.text = data.Cash;
            net.color = MonthlyResultView.Semantic((float)data.Quarter.Net); cash.color = MonthlyResultView.Semantic((float)data.Quarter.Closing);
            Fit(reputation, "Reputação final: " + data.Reputation, 1216, 32); Fit(alignment, "Alinhamento final: " + data.Alignment, 1216, 32);
            for (int i = 0; i < 3; i++)
            { monthButtons[i].interactable = data.Quarter.Months[i] != null; monthButtons[i].GetComponentInChildren<TMP_Text>().text = data.Quarter.Months[i] != null ? "DRE • mês " + (i + 1) : "Mês " + (i + 1) + " não salvo"; }
            Blocks(bmc, data.Bmc, 3); Blocks(reflection, data.Reflection, 1);
            newGameButton.interactable = true; foreach (var button in tabs) button.interactable = true;
            retryButton.gameObject.SetActive(false); ShowTab(tab);
        }
        public void State(string state, string title, string message)
        {
            DisplayState = state; loadedBody.SetActive(false); stateBody.SetActive(true); stateTitle.text = title; stateMessage.text = message;
            period.text = "RELATÓRIO FINAL"; retryButton.gameObject.SetActive(state == "error");
            newGameButton.interactable = false; backButton.interactable = false; foreach (var button in tabs) button.interactable = false;
        }
        private void OnDisable() { if (newGameDialog != null) newGameDialog.SetActive(false); }
    }
}
