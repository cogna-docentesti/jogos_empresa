using System;
using System.Collections.Generic;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class BankruptcyView : MonoBehaviour
    {
        [SerializeField] private TMP_Text screenTitle, company, reason, counter, period, net, cash, reputation, history, stateTitle, stateMessage, newGameMessage;
        [SerializeField] private TMP_Text[] monthValues;
        [SerializeField] private GameObject loadedBody, stateBody, newGameDialog;
        [SerializeField] private UnityEngine.UI.Button[] monthButtons;
        [SerializeField] private UnityEngine.UI.Button reviewButton, quarterlyButton, retryButton, newGameButton, cancelNewGame, confirmNewGame;
        [SerializeField] private UnityEngine.UI.ScrollRect scroll;
        public string DisplayState { get; private set; }
        public IReadOnlyList<UnityEngine.UI.Button> MonthButtons => monthButtons;
        public UnityEngine.UI.Button ReviewButton => reviewButton;
        public UnityEngine.UI.Button QuarterlyButton => quarterlyButton;
        public UnityEngine.UI.Button RetryButton => retryButton;
        public UnityEngine.UI.Button NewGameButton => newGameButton;
        public UnityEngine.UI.Button ConfirmNewGame => confirmNewGame;
        public UnityEngine.UI.Button CancelNewGame => cancelNewGame;
        public UnityEngine.UI.ScrollRect Scroll => scroll;
        private static void Bind(UnityEngine.UI.Button b, Action action) { b.onClick.RemoveAllListeners(); b.onClick.AddListener(() => action()); }
        public void Actions(Action<int> month, Action last, Action quarter, Action retry, Action start)
        {
            for (int i = 0; i < 3; i++) { int index = i; Bind(monthButtons[i], () => month(index + 1)); }
            Bind(reviewButton, last); Bind(quarterlyButton, quarter); Bind(retryButton, retry); Bind(confirmNewGame, start);
            Bind(newGameButton, () =>
            {
                newGameMessage.text = "A campanha encerrada, seus resultados e suas escolhas de eventos continuarão salvos. O novo jogo começa na identificação, nesta mesma tela de jogo, e só será gravado ao confirmar o cardápio. Deseja continuar?";
                newGameDialog.SetActive(true); cancelNewGame.Select();
            });
            Bind(cancelNewGame, CloseNewGame);
        }
        public void CloseNewGame() { newGameDialog.SetActive(false); }
        public void NewGameFailure(string message) { newGameMessage.text = message; }
        private static void Fit(TMP_Text text, string value)
        { text.text = value; text.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = Mathf.Max(40, text.GetPreferredValues(value, 1192, 0).y + 8); }
        public void Show(BankruptcyPresentation.Report data)
        {
            DisplayState = "loaded"; loadedBody.SetActive(true); stateBody.SetActive(false); period.text = data.Period; screenTitle.text = "Seu negócio encerrou esta campanha";
            Fit(company, data.Company); Fit(reason, data.Reason); Fit(counter, data.Counter); Fit(history, data.History); Fit(reputation, data.Reputation);
            net.text = data.Net; cash.text = data.Cash;
            var last = data.LastMonth > 0 ? data.Quarter.Months[data.LastMonth - 1] : null;
            net.color = last == null ? GamePalette.Ink : MonthlyResultView.Semantic(last.netResult);
            cash.color = last == null ? GamePalette.Ink : MonthlyResultView.Semantic(last.closingCash);
            for (int i = 0; i < 3; i++)
            {
                var r = data.Quarter.Months[i]; monthButtons[i].interactable = r != null;
                monthValues[i].text = r == null ? "Não salvo" : MonthlyResultPresentation.Money(r.netResult);
                monthValues[i].color = r == null ? GamePalette.Muted : MonthlyResultView.Semantic(r.netResult);
            }
            reviewButton.interactable = last != null; quarterlyButton.interactable = data.Quarter.Count > 0;
            newGameButton.interactable = true; retryButton.gameObject.SetActive(false); Canvas.ForceUpdateCanvases();
        }
        public void State(string state, string heading, string message)
        {
            DisplayState = state; loadedBody.SetActive(false); stateBody.SetActive(true); stateTitle.text = heading; stateMessage.text = message;
            period.text = "ENCERRAMENTO DA CAMPANHA"; screenTitle.text = "Encerramento da campanha"; reviewButton.interactable = newGameButton.interactable = false;
            retryButton.gameObject.SetActive(state == "error");
        }
        private void OnDisable() { if (newGameDialog != null) newGameDialog.SetActive(false); }
    }
}
