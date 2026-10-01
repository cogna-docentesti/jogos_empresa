using System.Linq;
using UnityEngine;

public static class LoanService
{
    public const string LastMonthMessage = "Novos empréstimos não estão disponíveis no último mês do trimestre.";

    public static int RemainingPaymentMonths(int currentRound)
    {
        return currentRound >= 1 && currentRound <= 3 ? 4 - currentRound : 0;
    }

    public static int ContractTerm(int currentRound)
    {
        return currentRound == 1 || currentRound == 2 ? RemainingPaymentMonths(currentRound) : 0;
    }

    public static CreditLineData FindLine(string id)
    {
        return Resources.LoadAll<CreditLineData>("CreditLines")
            .FirstOrDefault(line => line != null && line.id == id);
    }

    public static bool HasLoan(GameSessionEntity session)
    {
        return session != null && (session.loanBalance > 0f || FindLine(session.creditLineId)?.maxAmount > 0f);
    }

    public static bool CanContract => GameSessionState.HasActiveSession
        && ContractTerm(GameSessionState.Current.currentRound) > 0 && !HasLoan(GameSessionState.Current);

    public static bool TryContract(CreditLineData line)
    {
        line = line != null ? FindLine(line.id) : null;
        if (line == null || line.maxAmount <= 0f || !CanContract)
            return false;

        // A tela atual contrata o valor disponivel integral da linha selecionada.
        GameSessionState.SetLoan(line.id, line.maxAmount, false);
        GameSessionState.AddCash(line.maxAmount, false);
        GameSessionState.Save();
        return true;
    }

    public static float PrincipalPayment(float balance, int currentRound)
    {
        int months = RemainingPaymentMonths(currentRound);
        return balance > 0f && months > 0 ? balance / months : 0f;
    }

    public static float Installment(float balance, CreditLineData line, int currentRound)
    {
        if (balance <= 0f || line == null || RemainingPaymentMonths(currentRound) == 0)
            return 0f;
        // Formula existente: principal / prazo + juros mensais sobre o saldo.
        return PrincipalPayment(balance, currentRound) + balance * line.monthlyInterestRate;
    }
}
