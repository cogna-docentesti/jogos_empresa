using System;
using System.Collections.Generic;
using SQLite4Unity3d;
using UnityEngine;

/// <summary>Atomically settles a month and publishes state only after SQLite commits.</summary>
public class RoundService
{
    private readonly SQLiteConnection _db;
    private readonly Func<GameSessionEntity, IReadOnlyList<SessionEventHistoryEntity>, MonthlySimulationContext> _contextFactory;
    private bool _processing;

    public RoundService() : this(DatabaseInitializer.DatabaseService?.Connection, MonthlySimulationCatalog.Load) { }

    public RoundService(SQLiteConnection connection,
        Func<GameSessionEntity, IReadOnlyList<SessionEventHistoryEntity>, MonthlySimulationContext> contextFactory)
    {
        _db = connection;
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public RoundResultEntity ProcessRound()
    {
        return GameSessionState.HasActiveSession ? ProcessRound(GameSessionState.Current.currentRound) : null;
    }

    /// <summary>Use an explicit month for safe retries: a settled month returns its original result.</summary>
    public RoundResultEntity ProcessRound(int expectedMonth)
    {
        var session = GameSessionState.Current;
        if (session == null || _db == null || !GameSessionState.IsPersisted || _processing)
            return null;
        if (expectedMonth < 1 || expectedMonth > MonthlySimulationEngine.CycleMonths)
            return null;

        var results = new RoundResultRepository(_db);
        var existing = results.GetBySessionAndRound(session.sessionId, expectedMonth);
        if (existing != null) return existing;
        if (!GameSessionState.HasActiveSession || session.currentRound != expectedMonth)
            return null;
        if (_db.IsInTransaction)
        {
            Debug.LogWarning("[RoundService] Settlement requires its own database transaction.");
            return null;
        }

        _processing = true;
        try
        {
            RoundResultEntity result = null;
            GameSessionEntity next = null;
            _db.RunInTransaction(() =>
            {
                var sessions = new GameSessionRepository(_db);
                var stored = sessions.GetById(session.sessionId);
                if (stored == null || stored.status != GameSessionStatus.IN_PROGRESS || stored.currentRound != expectedMonth)
                    throw new InvalidOperationException("The persisted session is missing or has already advanced.");
                if (results.GetBySessionAndRound(session.sessionId, expectedMonth) != null)
                    throw new InvalidOperationException("The month has already been settled.");

                var previous = results.GetBySessionId(session.sessionId);
                if (previous.Count != expectedMonth - 1)
                    throw new InvalidOperationException("The monthly history is incomplete or duplicated.");
                for (int index = 0; index < previous.Count; index++)
                    if (previous[index].round != index + 1)
                        throw new InvalidOperationException("Monthly results must be consecutive.");

                var history = new SessionEventHistoryRepository(_db).GetBySessionAndRound(session.sessionId, expectedMonth);
                result = MonthlySimulationEngine.Calculate(_contextFactory(session.Copy(), history));
                result.roundResultId = Guid.NewGuid().ToString();
                result.createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                next = MonthlySessionSettlement.Apply(session, result, result.createdAt);
                results.Insert(result);
                if (_db.Update(next) != 1)
                    throw new InvalidOperationException("The monthly session update was not persisted.");
            });

            GameSessionState.AcceptMonthlySettlement(next);
            Debug.Log($"[RoundService] Month {result.round} settled: customers={result.customers}, revenue={result.grossRevenue:F2}, cash={result.closingCash:F2}, reputation={result.reputationAtEnd}.");
            return result;
        }
        finally
        {
            _processing = false;
        }
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
