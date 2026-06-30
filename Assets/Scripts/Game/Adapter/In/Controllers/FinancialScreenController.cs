using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.UI;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class FinancialScreenController : MonoBehaviour
    {
        [SerializeField] private FinancialScreenView view;

        private readonly List<BankCardView> _cards = new();
        private CreditLineData _selectedCreditLine;

        private void OnEnable()
        {
            if (view == null)
                view = GetComponent<FinancialScreenView>();

            BindActions();
            PopulateCreditLines();
        }

        private void OnDisable()
        {
            if (view != null)
            {
                view.BindConfirm(null);
                view.BindBack(null);
            }

            foreach (var card in _cards)
            {
                if (card != null)
                    card.Selected -= OnCardSelected;
            }
        }

        private void BindActions()
        {
            if (view == null)
                return;

            view.BindConfirm(OnConfirm);
            view.BindBack(OnBack);
        }

        private void PopulateCreditLines()
        {
            ClearCards();

            if (view == null || !view.HasRequiredReferences())
                return;

            view.SetTitle("Banco CogCred");
            view.SetHint("Selecione uma linha de credito para visualizar limite, juros e prazo.");
            view.SetConfirmEnabled(false);

            var creditLines = Resources
                .LoadAll<CreditLineData>("CreditLines")
                .Where(line => line != null)
                .OrderBy(line => line.maxAmount <= 0f ? 1 : 0)
                .ThenBy(line => line.maxAmount)
                .ToArray();

            if (creditLines.Length == 0)
            {
                view.SetHint("Nenhuma linha de credito encontrada em Resources/CreditLines.");
                return;
            }

           foreach (var creditLine in creditLines)
           {
               var card = view.CreateBankCard();

               int coinCount = GetCoinCountByRisk(creditLine, creditLines);

               card.Setup(creditLine, coinCount);
               card.Selected += OnCardSelected;

               _cards.Add(card);
           }
        }

     private void OnCardSelected(BankCardView selectedCard)
     {
         foreach (var card in _cards)
         {
             if (card != null)
                 card.SetSelected(card == selectedCard);
         }

         _selectedCreditLine = selectedCard != null ? selectedCard.CreditLine : null;

         if (_selectedCreditLine == null)
         {
             view.SetConfirmEnabled(false);
             return;
         }

         view.SetHint($"Selecionado: {_selectedCreditLine.displayName}");
         view.SetConfirmEnabled(true);
     }

        private void OnConfirm()
        {
            if (_selectedCreditLine == null || !GameSessionState.HasSession)
                return;

            float loanBalance = _selectedCreditLine.maxAmount <= 0f
                ? 0f
                : GameSessionState.Current.loanBalance;

            GameSessionState.SetLoan(_selectedCreditLine.id, loanBalance);

            PresentationMenuOverlay.Show();
        }

        private void OnBack()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.StateMachine.TryChangeState(GameState.Management_Hub);
        }

        private void ClearCards()
        {
            foreach (var card in _cards)
            {
                if (card != null)
                    card.Selected -= OnCardSelected;
            }

            _cards.Clear();
            _selectedCreditLine = null;

            if (view != null)
                view.ClearBankCards();
        }


        private static int GetCoinCountByRisk(
            CreditLineData currentCreditLine,
            IReadOnlyList<CreditLineData> allCreditLines
        )
        {
            if (currentCreditLine == null || allCreditLines == null || allCreditLines.Count == 0)
                return 1;

            var orderedRates = allCreditLines
                .Where(line => line != null)
                .Select(line => line.monthlyInterestRate)
                .Distinct()
                .OrderBy(rate => rate)
                .ToList();

            if (orderedRates.Count == 0)
                return 1;

            if (orderedRates.Count == 1)
                return 1;

            float currentRate = currentCreditLine.monthlyInterestRate;

            int rateIndex = orderedRates.FindIndex(rate =>
                Mathf.Approximately(rate, currentRate)
            );

            if (rateIndex < 0)
                rateIndex = orderedRates.Count(rate => rate < currentRate);

            if (orderedRates.Count == 2)
            {
                return rateIndex == 0 ? 1 : 3;
            }

            if (orderedRates.Count == 3)
            {
                return rateIndex switch
                {
                    0 => 1, // menor risco
                    1 => 2, // risco médio
                    _ => 3  // maior risco
                };
            }

            float normalizedPosition = rateIndex / (float)(orderedRates.Count - 1);

            return Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(1f, 3f, normalizedPosition)),
                1,
                3
            );
        }
    }
}

