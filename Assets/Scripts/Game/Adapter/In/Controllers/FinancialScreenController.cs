using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class FinancialScreenController : MonoBehaviour
    {
        private const string NoLoanCreditLineId = "sem_emprestimo";

        [SerializeField] private FinancialScreenView view;

        private readonly List<BankCardView> _cards = new();
        private CreditLineData _selectedCreditLine;
        private bool _selectionLocked;

        private void OnEnable()
        {
            if (view == null)
                view = GetComponent<FinancialScreenView>();

            view?.SetFooterVisible(!WasOpenedFromMenu());
            BindActions();
            PopulateCreditLines();
        }

        private static bool WasOpenedFromMenu()
        {
            return MenuNavigator.Instance != null
                && MenuNavigator.Instance.Current == PanelId.Financial;
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

               int coinCount = GetCoinCountByCreditLimit(creditLine, creditLines);

               card.Setup(creditLine, coinCount);
               card.Selected += OnCardSelected;

               _cards.Add(card);
           }

           RestoreSavedSelection();
        }

        private void RestoreSavedSelection()
        {
            if (!GameSessionState.HasSession
                || string.IsNullOrWhiteSpace(GameSessionState.Current.creditLineId))
                return;

            string savedCreditLineId = GameSessionState.Current.creditLineId;
            BankCardView savedCard = _cards.FirstOrDefault(card =>
                card != null
                && card.CreditLine != null
                && card.CreditLine.id == savedCreditLineId
            );

            if (savedCard == null)
            {
                Debug.LogWarning($"[FinancialScreenController] Linha de credito salva nao encontrada: {savedCreditLineId}");
                return;
            }

            foreach (BankCardView card in _cards)
                card?.SetSelected(card == savedCard);

            _selectedCreditLine = savedCard.CreditLine;
            _selectionLocked = savedCreditLineId != NoLoanCreditLineId;
            view.SetHint(_selectionLocked
                ? $"Linha de credito contratada: {_selectedCreditLine.displayName}. Esta escolha nao pode ser alterada."
                : $"Selecionado: {_selectedCreditLine.displayName}");
            view.SetConfirmEnabled(true);
        }

     private void OnCardSelected(BankCardView selectedCard)
     {
         if (_selectionLocked)
         {
             view.SetHint($"Linha de credito contratada: {_selectedCreditLine.displayName}. Esta escolha nao pode ser alterada.");
             return;
         }

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

            if (GameManager.Instance != null)
                GameManager.Instance.StateMachine.TryChangeState(GameState.Initial_Equipment);
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
            _selectionLocked = false;

            if (view != null)
                view.ClearBankCards();
        }


        private static int GetCoinCountByCreditLimit(
            CreditLineData currentCreditLine,
            IReadOnlyList<CreditLineData> allCreditLines
        )
        {
            if (currentCreditLine == null || allCreditLines == null || allCreditLines.Count == 0)
                return 0;

            if (currentCreditLine.maxAmount <= 0f)
                return 0;

            var orderedCreditLimits = allCreditLines
                .Where(line => line != null)
                .Select(line => line.maxAmount)
                .Where(maxAmount => maxAmount > 0f)
                .Distinct()
                .OrderBy(maxAmount => maxAmount)
                .ToList();

            int limitIndex = orderedCreditLimits.FindIndex(maxAmount =>
                Mathf.Approximately(maxAmount, currentCreditLine.maxAmount)
            );

            return limitIndex >= 0 ? limitIndex + 1 : 0;
        }
    }
}

