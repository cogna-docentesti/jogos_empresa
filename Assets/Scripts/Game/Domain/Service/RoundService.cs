using System;
using UnityEngine;

public class RoundService
{
    private readonly RoundResultRepository _roundRepository;

    public RoundService()
    {
        var db = DatabaseInitializer.DatabaseService.Connection;
        _roundRepository = new RoundResultRepository(db);
    }

    public RoundResultEntity ProcessRound()
    {
        if (!GameSessionState.HasSession)
        {
            Debug.LogWarning("Não há sessão ativa para processar rodada.");
            return null;
        }

        var session = GameSessionState.Current;

        if (!GameSessionState.HasActiveSession)
        {
            Debug.LogWarning("A sessao ja esta encerrada. Nenhuma rodada sera processada.");
            return null;
        }

        if (session.currentRound < 1 || session.currentRound > 3)
        {
            Debug.LogWarning("Rodada fora do intervalo trimestral de 1 a 3. Nenhuma rodada sera processada.");
            return null;
        }

        float openingCash = session.currentCash;

        // Valores temporários/simulados.
        // Depois podemos substituir pelo FinancialEngine.
        float grossRevenue = 30000f;
        float supplyCost = grossRevenue * 0.30f;
        float rent = 9000f;
        float salaries = 8000f;
        float utilities = 1500f;
        var creditLine = LoanService.FindLine(session.creditLineId);
        float loanPayment = LoanService.Installment(session.loanBalance, creditLine, session.currentRound);
        float thirteenthSalary = 0f;
        float eventCashImpact = 0f;
        float coherenceFactor = GetCoherenceFactor(session.coherenceRating);

        float totalCosts =
            supplyCost +
            rent +
            salaries +
            utilities +
            loanPayment +
            thirteenthSalary +
            eventCashImpact;

        float netResult = grossRevenue - totalCosts;
        float closingCash = openingCash + netResult;

        var result = new RoundResultEntity
        {
            roundResultId = Guid.NewGuid().ToString(),
            sessionId = session.sessionId,
            round = session.currentRound,

            grossRevenue = grossRevenue,
            supplyCost = supplyCost,
            rent = rent,
            salaries = salaries,
            utilities = utilities,
            loanPayment = loanPayment,
            thirteenthSalary = thirteenthSalary,
            eventCashImpact = eventCashImpact,

            netResult = netResult,

            openingCash = openingCash,
            closingCash = closingCash,

            eventId = null,
            eventChoice = -1,

            reputationDelta = 0,
            coherenceFactor = session.alignmentFactor,

            createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _roundRepository.Insert(result);

        if (creditLine != null && session.loanBalance > 0f)
            GameSessionState.SetLoan(session.creditLineId,
                Mathf.Max(0f, session.loanBalance - LoanService.PrincipalPayment(session.loanBalance, session.currentRound)), false);

        GameSessionState.SetCash(closingCash, false);
        GameSessionState.RegisterNegativeRound(netResult < 0, false);

        EvaluateSessionEnd();

        return result;
    }

    internal static void EvaluateSessionEnd()
    {
        var session = GameSessionState.Current;

        if (session.consecutiveNegativeRounds >= 3)
        {
            GameSessionState.BankruptSession();
        }
        else if (session.currentRound == 3)
        {
            GameSessionState.CompleteSession();
        }
        else
        {
            GameSessionState.AdvanceRound(true);
        }
    }

    private float GetCoherenceFactor(string coherenceRating)
    {
        return coherenceRating switch
        {
            "IDEAL" => 1.0f,
            "REGULAR" => 0.7f,
            "INCOHERENT" => 0.35f,
            "BANKRUPT" => 0.0f,
            _ => 1.0f
        };
    }
}