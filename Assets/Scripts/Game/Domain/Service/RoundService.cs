using System;
using SQLite4Unity3d;
using UnityEngine;

/// <summary>Atomically settles a month and publishes state only after SQLite commits.</summary>
public class RoundService
{
    private readonly SQLiteConnection _db;

    public RoundService() : this(DatabaseInitializer.DatabaseService?.Connection) { }

    public RoundService(SQLiteConnection connection)
    {
        _db = connection;
    }

    public RoundResultEntity ProcessRound()
    {
        return GameSessionState.HasActiveSession ? ProcessRound(GameSessionState.Current.currentRound) : null;
    }

    /// <summary>Use an explicit month for safe retries: a settled month returns its original result.</summary>
    public RoundResultEntity ProcessRound(int expectedMonth)
    {
        var session = GameSessionState.Current;
        if (session == null || _db == null || !GameSessionState.IsPersisted
            || expectedMonth < 1 || expectedMonth > MonthlySimulationEngine.CycleMonths)
            return null;

        RoundResultEntity result = null;
        _db.RunInDatabaseLock(() =>
        {
            if (_db.IsInTransaction)
                throw new InvalidOperationException("Settlement requires its own database transaction.");
            var results = new RoundResultRepository(_db);
            result = results.GetBySessionAndRound(session.sessionId, expectedMonth);
            if (result != null) return;
            GameSessionEntity next = null;
            _db.RunInTransaction(() =>
            {
                var stored = new GameSessionRepository(_db).GetById(session.sessionId);
                if (stored == null || stored.status != GameSessionStatus.IN_PROGRESS || stored.currentRound != expectedMonth
                    || !string.IsNullOrEmpty(stored.pendingSummaryResultId))
                    return;
                var calendar = MonthlyCalendarService.Find(_db, session.sessionId, expectedMonth);
                if (!MonthlyCalendarService.HasCalendar(calendar) || !MonthlyCalendarService.IsReadyToClose(calendar))
                    return;

                var previous = results.GetBySessionId(session.sessionId);
                if (previous.Count != expectedMonth - 1)
                    throw new InvalidOperationException("The monthly history is incomplete or duplicated.");
                for (int index = 0; index < previous.Count; index++)
                    if (previous[index].round != index + 1)
                        throw new InvalidOperationException("Monthly results must be consecutive.");

                var history = new SessionEventHistoryRepository(_db).GetBySessionAndRound(session.sessionId, expectedMonth);
                result = MonthlyCalendarService.Reconcile(calendar, stored, history, out var opening);
                result.roundResultId = Guid.NewGuid().ToString();
                result.createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var settlementBase = stored.Copy();
                settlementBase.currentCash = opening.currentCash;
                settlementBase.loanBalance = opening.loanBalance;
                next = MonthlySessionSettlement.Apply(settlementBase, result, result.createdAt);
                next.calendarMonthLocked = 0;
                next.pendingSummaryResultId = result.roundResultId;
                results.Insert(result);
                MonthlyCalendarService.MarkClosed(_db, calendar, result.roundResultId);
                if (_db.Update(next) != 1)
                    throw new InvalidOperationException("The monthly session update was not persisted.");
            });

            if (next == null) return;
            if (GameSessionState.Current?.sessionId == next.sessionId)
                GameSessionState.AcceptMonthlySettlement(next);
            Debug.Log($"[RoundService] Month {result.round} settled: customers={result.customers}, revenue={result.grossRevenue:F2}, cash={result.closingCash:F2}, reputation={result.reputationAtEnd}.");
        });
        return result;
    }

    // Retained for existing quarter-flow fixtures. Production uses the transaction above.
    internal static void EvaluateSessionEnd()
    {
        if (!GameSessionState.HasActiveSession) return;
        var next = GameSessionState.Current.Copy();
        MonthlySessionSettlement.Advance(next, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        GameSessionState.AcceptMonthlySettlement(next);
        GameSessionState.Save();
    }
}
