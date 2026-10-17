using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Navigation;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class FinancialScreenController : MonoBehaviour
    {
        [SerializeField] private FinancialScreenView view;
        private readonly List<BankCardView> _cards = new();
        private CreditLineData _selectedCreditLine;
        private bool _selectionLocked;
        private static bool Management => MenuNavigator.Instance != null && MenuNavigator.Instance.Current == PanelId.Financial;
        private void OnEnable()
        { if (view == null) view = GetComponent<FinancialScreenView>(); view?.BindConfirm(OnConfirm); PopulateCreditLines(); }
        private void OnDisable()
        { view?.BindConfirm(null); foreach (var card in _cards) if (card != null) card.Selected -= OnCardSelected; }
        private void PopulateCreditLines()
        {
            foreach (var card in _cards) if (card != null) card.Selected -= OnCardSelected;
            _cards.Clear(); _selectedCreditLine = null; _selectionLocked = false;
            if (view == null) return;
            view.ClearBankCards();
            if (!view.HasRequiredReferences()) { view.SetHint("Não foi possível carregar as opções do banco.", true); view.SetSummary("Opções indisponíveis. Nenhuma contratação será confirmada."); view.SetFooterHint("Consulte a disponibilidade do banco."); view.SetConfirmEnabled(false); return; }
            view.SetTitle("Banco CogCred"); view.SetContext(Management);
            view.SetInitialCapital(GameSessionState.Current?.initialCapital);
            view.SetCash(GameSessionState.Current?.currentCash);
            var lines = Resources.LoadAll<CreditLineData>("CreditLines").Where(l => l != null).OrderBy(l => l.maxAmount).ToArray();
            foreach (var line in lines)
            { var card = view.CreateBankCard(); card.Setup(line); card.Selected += OnCardSelected; _cards.Add(card); }
            var saved = _cards.FirstOrDefault(c => c.CreditLine.id == GameSessionState.Current?.creditLineId);
            if (saved != null) { _selectedCreditLine = saved.CreditLine; saved.SetSelected(true); }
            _selectionLocked = LoanService.HasLoan(GameSessionState.Current); RefreshDetails();
            GetComponent<ContextualHelp>()?.Refresh(GameSessionState.HasActiveSession && lines.Length > 0, _selectedCreditLine != null || _selectionLocked);
            if (lines.Length == 0) view.SetHint("Nenhuma linha de crédito disponível no catálogo.", true);
        }
        private void OnCardSelected(BankCardView card)
        {
            if (!GameSessionState.HasActiveSession || _selectionLocked || card?.CreditLine == null) return;
            if (card.CreditLine.maxAmount > 0 && !LoanService.CanContract) { RefreshDetails(); return; }
            _selectedCreditLine = card.CreditLine;
            GetComponent<ContextualHelp>()?.Complete();
            foreach (var item in _cards) item.SetSelected(item == card);
            RefreshDetails();
        }
        private void RefreshDetails()
        {
            var session = GameSessionState.Current; bool active = GameSessionState.HasActiveSession;
            bool canConfirm = active && _selectedCreditLine != null && (_selectionLocked || _selectedCreditLine.maxAmount <= 0 || LoanService.CanContract);
            view.SetConfirmEnabled(canConfirm);
            view.SetFooterHint(!active ? "Consulta indisponível nesta sessão."
                : _selectionLocked ? Management ? "Confira o contrato e conclua a consulta." : "Confira o contrato e continue."
                : session.currentRound == 3 ? "Consulte as condições e volte ao concluir."
                : "Selecione, confira e confirme.");
            view.SetConfirmLabel(_selectionLocked ? Management ? "Concluir consulta" : "Continuar para equipamentos"
                : _selectedCreditLine?.maxAmount <= 0 ? Management ? "Manter sem crédito" : "Continuar sem crédito" : "Confirmar crédito");
            if (!active) { view.SetHint("Sessão indisponível para alterações.", true); view.SetSummary("Nenhuma contratação pode ser confirmada."); return; }
            if (_selectionLocked)
            {
                var line = LoanService.FindLine(session.creditLineId);
                view.SetHint(line != null ? $"Contrato mantido: {line.displayName}. Esta escolha não pode ser alterada." : "Há um empréstimo registrado. A linha não está disponível no catálogo.", line == null);
                view.SetSummary(line != null ? $"Saldo devedor: {FinancialScreenView.Money(session.loanBalance)}  •  Parcela deste mês: {FinancialScreenView.Money(LoanService.Installment(session.loanBalance, line, session.currentRound))}"
                    : $"Saldo devedor: {FinancialScreenView.Money(session.loanBalance)}. Parcela indisponível: linha não encontrada no catálogo."); return;
            }
            bool lastMonth = !LoanService.CanContract && session.currentRound == 3;
            view.SetHint(lastMonth ? LoanService.LastMonthMessage : _selectedCreditLine == null
                ? "Selecione uma das quatro opções. Confira os valores antes de confirmar."
                : $"Selecionado: {_selectedCreditLine.displayName}. Confira os valores antes de confirmar.");
            view.SetSummary(_selectedCreditLine == null ? "Nenhuma opção selecionada. A confirmação fica disponível após a seleção."
                : _selectedCreditLine.maxAmount <= 0 ? "Sem empréstimo: seu caixa será mantido, sem juros e sem parcelas."
                : $"Entrada no caixa: {FinancialScreenView.Money(_selectedCreditLine.maxAmount)}  •  Primeira parcela: {FinancialScreenView.Money(LoanService.Installment(_selectedCreditLine.maxAmount, _selectedCreditLine, session.currentRound))}\nAmortização do saldo + juros mensais; as parcelas seguintes acompanham o saldo devedor.");
        }
        private void OnConfirm()
        {
            try { ConfirmSelection(); }
            catch (System.Exception)
            {
                view.SetHint("Não foi possível confirmar a escolha. Confira a sessão e tente novamente.", true);
                FeedbackToast.Show("A escolha no banco não foi confirmada. Tente novamente.", FeedbackKind.Error);
            }
        }
        private void ConfirmSelection()
        {
            if (_selectedCreditLine == null || !GameSessionState.HasActiveSession) return;
            GetComponent<ContextualHelp>()?.Complete();
            bool contractedNow = false;
            if (!_selectionLocked)
            {
                if (_selectedCreditLine.maxAmount > 0)
                {
                    if (!LoanService.TryContract(_selectedCreditLine)) { PopulateCreditLines(); view.SetHint(GameSessionState.Current.currentRound == 3 ? LoanService.LastMonthMessage : "Não foi possível contratar o crédito. Confira a disponibilidade da sessão.", true); FeedbackToast.Show("Crédito não contratado. Confira a disponibilidade da sessão.", FeedbackKind.Error); view.SetConfirmEnabled(false); return; }
                    _selectionLocked = true;
                    contractedNow = true;
                }
                else if (!LoanService.HasLoan(GameSessionState.Current)) GameSessionState.SetLoan(_selectedCreditLine.id, 0);
                else { PopulateCreditLines(); return; }
            }
            FeedbackToast.Show(contractedNow ? "Crédito contratado. O valor foi adicionado ao caixa." : _selectionLocked ? "Contrato mantido. Confira o saldo e as parcelas no banco." : "Escolha sem crédito salva. Seu caixa foi mantido.", FeedbackKind.Success);
            if (Management) { MenuNavigator.Instance.Back(); return; }
            if (GameManager.Instance?.StateMachine.CurrentState == GameState.Initial_Capital)
                GameManager.Instance.StateMachine.TryChangeState(GameState.Initial_Equipment);
        }
    }
}
