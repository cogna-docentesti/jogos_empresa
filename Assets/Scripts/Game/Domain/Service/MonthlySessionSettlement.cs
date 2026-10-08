using System;

/// <summary>Builds the next session snapshot without changing the current session.</summary>
public static class MonthlySessionSettlement
{
    public static GameSessionEntity Apply(GameSessionEntity session, RoundResultEntity result, long closedAt)
    {
        if (session == null || result == null || result.sessionId != session.sessionId
            || result.round != session.currentRound || session.status != GameSessionStatus.IN_PROGRESS)
            throw new InvalidOperationException("The monthly result does not match the active session.");

        var next = session.Copy();
        next.currentCash = result.closingCash;
        next.reputationScore = result.reputationAtEnd;
        next.loanBalance = Math.Max(0f, session.loanBalance - result.loanPrincipalPayment);
        next.alignmentScore = result.alignmentScore;
        next.alignmentClassification = result.alignmentClassification;
        next.coherenceRating = result.alignmentClassification;
        next.alignmentFactor = result.coherenceFactor;
        next.consecutiveNegativeRounds = result.netResult < 0f ? session.consecutiveNegativeRounds + 1 : 0;
        Advance(next, closedAt);
        return next;
    }

    internal static void Advance(GameSessionEntity session, long closedAt)
    {
        if (session.consecutiveNegativeRounds >= MonthlySimulationEngine.CycleMonths)
            session.status = GameSessionStatus.BANKRUPT;
        else if (session.currentRound == MonthlySimulationEngine.CycleMonths)
            session.status = GameSessionStatus.COMPLETED;
        else
            session.currentRound++;

        if (session.status != GameSessionStatus.IN_PROGRESS) session.completedAt = closedAt;
    }
}
