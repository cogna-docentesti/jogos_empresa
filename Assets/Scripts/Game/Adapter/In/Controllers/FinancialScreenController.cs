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
                .OrderBy(line => line.maxAmount)
                .ToArray();

            if (creditLines.Length == 0)
            {
                view.SetHint("Nenhuma linha de credito encontrada em Resources/CreditLines.");
                return;
            }

            foreach (var creditLine in creditLines)
            {
                var card = view.CreateBankCard();
                card.Setup(creditLine);
                card.Selected += OnCardSelected;
                _cards.Add(card);
            }
        }

        private void OnCardSelected(BankCardView card)
        {
            _selectedCreditLine = card != null ? card.CreditLine : null;

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

            GameSessionState.SetLoan(_selectedCreditLine.id, GameSessionState.Current.loanBalance);
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
    }
}

