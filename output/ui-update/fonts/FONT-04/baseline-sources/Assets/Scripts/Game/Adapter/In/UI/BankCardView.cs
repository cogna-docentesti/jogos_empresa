using System;
using TMPro;
using UnityEngine;
using Game.Adapter.In.UI.Theme;

namespace Game.Adapter.In.UI
{
    public sealed class BankCardView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI maxAmountText;
        [SerializeField] private TextMeshProUGUI interestRateText;
        [SerializeField] private TextMeshProUGUI termText;
        [SerializeField] private TextMeshProUGUI installmentText;
        [SerializeField] private TextMeshProUGUI availabilityText;
        [SerializeField] private UnityEngine.UI.Button selectButton;
        [SerializeField] private UnityEngine.UI.Image creditLineIcon;
        [SerializeField] private UnityEngine.UI.Image selectionMark;
        [SerializeField] private UnityEngine.UI.Image interactionOutline;
        public CreditLineData CreditLine { get; private set; }
        public event Action<BankCardView> Selected;
        private void OnEnable() { if (selectButton != null) selectButton.onClick.AddListener(OnSelected); }
        private void OnDisable() { if (selectButton != null) selectButton.onClick.RemoveListener(OnSelected); }
        private void OnSelected() => Selected?.Invoke(this);
        public void Setup(CreditLineData line)
        {
            CreditLine = line; nameText.text = line.displayName; descriptionText.text = line.cardDescription;
            creditLineIcon.sprite = line.icon; creditLineIcon.color = Color.white;
            creditLineIcon.type = UnityEngine.UI.Image.Type.Simple; creditLineIcon.preserveAspect = true; creditLineIcon.raycastTarget = false;
            bool noCredit = line.maxAmount <= 0; var session = GameSessionState.Current;
            bool contracted = !noCredit && LoanService.HasLoan(session) && session.creditLineId == line.id;
            int term = contracted ? LoanService.RemainingPaymentMonths(session.currentRound) : session != null ? LoanService.ContractTerm(session.currentRound) : 0;
            maxAmountText.text = noCredit ? "Capital próprio" : FinancialScreenView.Money(line.maxAmount);
            interestRateText.text = noCredit ? "Sem juros" : $"{(line.monthlyInterestRate * 100).ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))}% ao mês";
            termText.text = noCredit ? "Sem parcelas" : term > 0 ? $"{term} parcela(s) restante(s)" : "Contratação indisponível";
            installmentText.text = noCredit ? "Nenhum valor será adicionado ao caixa." : term > 0
                ? $"{(contracted ? "Parcela deste mês" : "Primeira parcela")}: {FinancialScreenView.Money(LoanService.Installment(contracted ? session.loanBalance : line.maxAmount, line, session.currentRound))}"
                : "Novos empréstimos indisponíveis.";
            bool available = GameSessionState.HasActiveSession && (noCredit ? !LoanService.HasLoan(session) : LoanService.CanContract);
            selectButton.interactable = available;
            availabilityText.text = contracted ? "Contratado • escolha mantida" : !GameSessionState.HasActiveSession ? "Sessão indisponível"
                : LoanService.HasLoan(session) ? "Outra linha já foi contratada" : noCredit ? "Disponível • sem empréstimo"
                : available ? "Disponível para contratação" : "Indisponível no mês atual";
            SetSelected(false);
        }
        public void SetSelected(bool selected)
        { GetComponent<SharedControlAppearance>()?.SetSelected(selected); if (selectionMark != null) selectionMark.enabled = selected; }
        private void LateUpdate()
        {
            var appearance = GetComponent<SharedControlAppearance>();
            if (interactionOutline != null && appearance != null)
            {
                bool pressed = appearance.CurrentState == "pressed";
                interactionOutline.enabled = pressed || appearance.CurrentState == "hover";
                var color = GamePalette.Primary; color.a = pressed ? .85f : .4f; interactionOutline.color = color;
            }
            // Long catalog copy expands the row; scrolling keeps fixed actions reachable.
            if (nameText == null || descriptionText == null) return;
            if (((RectTransform)transform).rect.width < 500)
            {
                float h = Mathf.Max(60, nameText.GetPreferredValues(nameText.text, nameText.rectTransform.rect.width, float.PositiveInfinity).y);
                nameText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
                float y = 190 + h + 12;
                maxAmountText.rectTransform.anchoredPosition = new Vector2(16, -y);
                interestRateText.rectTransform.anchoredPosition = new Vector2(16, -y - 50);
                termText.rectTransform.anchoredPosition = new Vector2(16, -y - 86);
                installmentText.rectTransform.anchoredPosition = new Vector2(16, -y - 132);
                descriptionText.rectTransform.anchoredPosition = new Vector2(16, -y - 202);
                float dh = Mathf.Max(64, descriptionText.GetPreferredValues(descriptionText.text, descriptionText.rectTransform.rect.width, float.PositiveInfinity).y);
                descriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, dh);
                availabilityText.rectTransform.anchoredPosition = new Vector2(16, -y - 214 - dh);
                var le = GetComponent<UnityEngine.UI.LayoutElement>();
                le.preferredHeight = Mathf.Max(640, y + 282 + dh);
                return;
            }
            float nameHeight = Mathf.Max(40, nameText.GetPreferredValues(nameText.text, nameText.rectTransform.rect.width, float.PositiveInfinity).y);
            float descriptionHeight = Mathf.Max(60, descriptionText.GetPreferredValues(descriptionText.text, descriptionText.rectTransform.rect.width, float.PositiveInfinity).y);
            nameText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, nameHeight);
            descriptionText.rectTransform.anchoredPosition = new Vector2(168, -24 - nameHeight - 8);
            descriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, descriptionHeight);
            availabilityText.rectTransform.anchoredPosition = new Vector2(168, -24 - nameHeight - 8 - descriptionHeight - 8);
            var layout = GetComponent<UnityEngine.UI.LayoutElement>();
            float height = Mathf.Max(212, 24 + nameHeight + 8 + descriptionHeight + 8 + 36 + 24);
            if (!Mathf.Approximately(layout.preferredHeight, height)) layout.preferredHeight = height;
        }
    }
}
