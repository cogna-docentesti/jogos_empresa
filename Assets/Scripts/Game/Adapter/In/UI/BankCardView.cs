using System;
using TMPro;
using UnityEngine;
using Game.Adapter.In.UI.Theme;

namespace Game.Adapter.In.UI
{
    public sealed class BankCardView : MonoBehaviour
    {
        private void ApplyTypography()
        {
            GameTypography.KeepSize(nameText,TypographyRole.SectionTitle);
            GameTypography.KeepSize(descriptionText,TypographyRole.Body);
            GameTypography.KeepSize(maxAmountText,TypographyRole.Table);
            GameTypography.KeepSize(interestRateText,TypographyRole.Table);
            GameTypography.KeepSize(termText,TypographyRole.Table);
            GameTypography.KeepSize(installmentText,TypographyRole.Table);
            GameTypography.KeepSize(availabilityText,TypographyRole.Caption);
        }
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI maxAmountText;
        [SerializeField] private TextMeshProUGUI interestRateText;
        [SerializeField] private TextMeshProUGUI termText;
        [SerializeField] private TextMeshProUGUI installmentText;
        [SerializeField] private TextMeshProUGUI availabilityText;
        [SerializeField] private UnityEngine.UI.Button selectButton;
        [SerializeField] private UnityEngine.UI.Image creditLineIcon;
        public CreditLineData CreditLine { get; private set; }
        public event Action<BankCardView> Selected;
        private void OnEnable() { ApplyTypography(); if (selectButton != null) selectButton.onClick.AddListener(OnSelected); }
        private void OnDisable() { if (selectButton != null) selectButton.onClick.RemoveListener(OnSelected); }
        private void OnSelected() => Selected?.Invoke(this);
        public void Setup(CreditLineData line)
        {
            ApplyTypography();
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
        public void SetSelected(bool selected) => GetComponent<SharedControlAppearance>()?.SetSelected(selected);
    }
}
