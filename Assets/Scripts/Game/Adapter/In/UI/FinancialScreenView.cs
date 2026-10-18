using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;

namespace Game.Adapter.In.UI
{
    public sealed class FinancialScreenView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI stepText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private TextMeshProUGUI capitalValueText;
        [SerializeField] private TextMeshProUGUI cashValueText;
        [SerializeField] private TextMeshProUGUI summaryText;
        [SerializeField] private Transform bankCardsContainer;
        [SerializeField] private BankCardView bankCardPrefab;
        [SerializeField] private UnityEngine.UI.ScrollRect cardsScrollRect;
        [SerializeField] private UnityEngine.UI.Button confirmButton;
        [SerializeField] private TextMeshProUGUI confirmLabel;
        [SerializeField] private TextMeshProUGUI footerHintText;
        public bool HasRequiredReferences() => bankCardsContainer != null && bankCardPrefab != null && confirmButton != null;
        public void SetContext(bool management)
        {
            if (stepText != null) stepText.text = management
                ? GameSessionState.HasSession ? $"GESTÃO FINANCEIRA  •  MÊS {GameSessionState.Current.currentRound} DE 3" : "GESTÃO FINANCEIRA  •  SESSÃO INDISPONÍVEL"
                : "PASSO 5 DE 7  •  BANCO E CAPITAL";
        }
        public void SetInitialCapital(float? value) { if (capitalValueText != null) capitalValueText.text = value.HasValue ? Money(value.Value) : "Indisponível"; }
        public void SetCash(float? value) { if (cashValueText != null) cashValueText.text = value.HasValue ? Money(value.Value) : "Indisponível"; }
        public void SetTitle(string value) { if (titleText != null) titleText.text = value; }
        public void SetHint(string value, bool error = false)
        { if (hintText != null) { hintText.text = value; hintText.color = error ? GamePalette.ErrorText : GamePalette.Ink; } }
        public void SetSummary(string value) { if (summaryText != null) summaryText.text = value; }
        public void SetConfirmEnabled(bool value) { if (confirmButton != null) confirmButton.interactable = value; }
        public void SetConfirmLabel(string value) { if (confirmLabel != null) confirmLabel.text = value; }
        public void SetFooterHint(string value) { if (footerHintText != null) footerHintText.text = value; }
        public void BindConfirm(UnityEngine.Events.UnityAction action)
        { if (confirmButton == null) return; confirmButton.onClick.RemoveAllListeners(); if (action != null) confirmButton.onClick.AddListener(action); }
        private void LateUpdate()
        {
            // The four-column composition must scroll through native long-copy heights too.
            var grid = bankCardsContainer != null ? bankCardsContainer.GetComponent<UnityEngine.UI.GridLayoutGroup>() : null;
            if (grid == null) return;
            float height = 660;
            foreach (var card in bankCardsContainer.GetComponentsInChildren<BankCardView>())
            {
                var layout = card.GetComponent<UnityEngine.UI.LayoutElement>();
                if (layout != null) height = Mathf.Max(height, layout.preferredHeight);
            }
            if (!Mathf.Approximately(grid.cellSize.y, height)) grid.cellSize = new Vector2(grid.cellSize.x, height);
        }
        public BankCardView CreateBankCard() => Instantiate(bankCardPrefab, bankCardsContainer);
        public void ClearBankCards()
        {
            if (bankCardsContainer == null) return;
            for (int i = bankCardsContainer.childCount - 1; i >= 0; i--)
            { var child = bankCardsContainer.GetChild(i).gameObject; child.SetActive(false); Destroy(child); }
            if (cardsScrollRect != null) cardsScrollRect.verticalNormalizedPosition = 1;
        }
        public static string Money(float value) => "R$ " + value.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
    }
}
