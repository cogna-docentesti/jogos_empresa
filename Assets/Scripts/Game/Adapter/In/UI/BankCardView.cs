using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Adapter.In.UI
{
    public sealed class BankCardView : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI maxAmountText;
        [SerializeField] private TextMeshProUGUI interestRateText;
        [SerializeField] private TextMeshProUGUI termText;

        [Header("Action")]
        [SerializeField] private Button selectButton;

        public CreditLineData CreditLine { get; private set; }
        public event Action<BankCardView> Selected;

        private void OnDisable()
        {
            if (selectButton != null)
                selectButton.onClick.RemoveListener(OnSelected);
        }

        public void Setup(CreditLineData creditLine)
        {
            CreditLine = creditLine;

            if (nameText != null)
                nameText.text = creditLine.displayName;

            if (maxAmountText != null)
                maxAmountText.text = FormatCurrency(creditLine.maxAmount);

            if (interestRateText != null)
                interestRateText.text = FormatInterestRate(creditLine.monthlyInterestRate);

            if (termText != null)
                termText.text = $"{creditLine.termRounds} rodadas";

            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(OnSelected);
                selectButton.onClick.AddListener(OnSelected);
            }
        }

        private void OnSelected()
        {
            Selected?.Invoke(this);
        }

        private static string FormatCurrency(float value)
        {
            return $"R$ {value:0,0.00}";
        }

        private static string FormatInterestRate(float value)
        {
            return $"{value * 100f:0.##}% ao mes";
        }
    }
}
