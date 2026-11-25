using System;
using System.Collections.Generic;
using System.Linq;
using SQLite4Unity3d;
using UnityEngine;

/// <summary>Reserves the existing selector's monthly draw and records choices without applying effects.</summary>
public sealed class MonthlyEventDecisionService
{
    [Serializable] public sealed class Entry { public string eventId; public int day; }
    [Serializable] private sealed class Plan { public List<Entry> entries = new(); }
    private readonly SQLiteConnection db;
    private readonly MonthlyEventSelector selector;
    public MonthlyEventDecisionService(SQLiteConnection connection, MonthlyEventSelector eventSelector = null)
    { db = connection ?? throw new ArgumentNullException(nameof(connection)); selector = eventSelector ?? new MonthlyEventSelector(); }

    public IReadOnlyList<Entry> OpenMonth(GameSessionEntity session, IReadOnlyList<EventData> catalog,
        RestaurantData restaurant, IReadOnlyList<EquipmentData> equipment, int operatingDays)
    {
        if (session == null || catalog == null || operatingDays < 1 || operatingDays > 31)
            throw new InvalidOperationException("Event context is unavailable.");
        db.CreateTable<MonthlyEventPlanEntity>();
        IReadOnlyList<Entry> entries = null;
        db.RunInTransaction(() =>
        {
            var stored = RequireActiveMonth(session.sessionId, session.currentRound);
            if (!GameSessionService.HasMinimumTeam(stored)
                || (stored.teamJson != session.teamJson && !GameSessionService.HasMinimumTeam(session)))
                throw new InvalidOperationException("The month requires the restaurant's minimum staffing.");
            if (stored.calendarMonthLocked != 0)
                throw new InvalidOperationException("Read active commitments through the calendar projection.");
            entries = Reserve(stored, catalog, restaurant, equipment, operatingDays);
        });
        return entries;
    }

    internal static string PlanKey(string sessionId, int month) => sessionId + ":" + month;

    // The caller owns the transaction so reservation and calendar opening commit together.
    internal IReadOnlyList<Entry> Reserve(GameSessionEntity session, IReadOnlyList<EventData> catalog,
        RestaurantData restaurant, IReadOnlyList<EquipmentData> equipment, int operatingDays)
    {
        string key = PlanKey(session.sessionId, session.currentRound);
        var saved = db.Find<MonthlyEventPlanEntity>(key);
        if (saved != null)
        {
            var plan = JsonUtility.FromJson<Plan>(saved.entriesJson);
            if (plan?.entries == null || plan.entries.Any(e => e == null || e.day < 1 || e.day > operatingDays || string.IsNullOrWhiteSpace(e.eventId))
                || plan.entries.Select(e => e.eventId).Distinct().Count() != plan.entries.Count)
                throw new InvalidOperationException("The saved event plan is invalid.");
            return plan.entries.AsReadOnly();
        }
        var history = new SessionEventHistoryRepository(db).GetBySession(session.sessionId);
        var month = history.Where(h => h.round == session.currentRound).ToList();
        var used = catalog.Where(e => e != null && month.Any(h => h.eventId == e.id)).Select(e => e.exclusionGroup);
        var selection = selector.SelectEventsForMonth(new MonthlyEventSelectionContext
        {
            Session = session, Month = session.currentRound, Restaurant = restaurant,
            EquipmentCatalog = equipment, EventCatalog = catalog,
            OccurredEventIds = history.Select(h => h.eventId), ReservedEventIds = month.Select(h => h.eventId),
            UsedGroupsInMonth = used, AlreadySelectedCountInMonth = month.Count
        });
        if (!selection.IsValid) throw new InvalidOperationException(string.Join(" ", selection.Diagnostics));
        var reserved = new Plan();
        for (int i = 0; i < selection.Events.Count; i++)
            reserved.entries.Add(new Entry { eventId = selection.Events[i].id, day = Math.Max(1, (i + 1) * operatingDays / (selection.Events.Count + 1)) });
        db.Insert(new MonthlyEventPlanEntity { planId = key, sessionId = session.sessionId, month = session.currentRound, entriesJson = JsonUtility.ToJson(reserved) });
        return reserved.entries.AsReadOnly();
    }

    public SessionEventHistoryEntity Confirm(string sessionId, int month, int day, EventData data, EventOption option)
    {
        if (data == null || string.IsNullOrWhiteSpace(data.id) || data.options == null || option == null ||
            !data.options.Contains(option) || string.IsNullOrWhiteSpace(option.id) || option.effects == null || day < 1 || day > 31)
            throw new InvalidOperationException("Event or choice is unavailable.");
        SessionEventHistoryEntity record = null;
        db.RunInDatabaseLock(() =>
        {
            if (db.IsInTransaction) throw new InvalidOperationException("Event confirmation requires its own transaction.");
            db.RunInTransaction(() =>
            {
                // An occurrence is identified by session/month/event/day, including a retry after settlement.
                var repository = new SessionEventHistoryRepository(db);
                record = repository.GetBySessionAndRound(sessionId, month).SingleOrDefault(h => h.eventId == data.id && h.day == day);
                if (record != null)
                {
                    if (record.selectedChoiceId != option.id) throw new InvalidOperationException("This occurrence has already been confirmed.");
                    return;
                }
                var stored = RequireActiveMonth(sessionId, month);
                if (stored.calendarMonthLocked != 0)
                {
                    record = MonthlyCalendarService.ConfirmPending(db, stored, day, data, option, repository);
                    return;
                }
                record = repository.RecordFromOption(sessionId, month, day, data, option);
            });
        });
        return record;
    }

    internal GameSessionEntity RequireActiveMonth(string sessionId, int month)
    {
        var stored = new GameSessionRepository(db).GetById(sessionId);
        if (stored == null || stored.status != GameSessionStatus.IN_PROGRESS || stored.currentRound != month || !string.IsNullOrEmpty(stored.pendingSummaryResultId) || month < 1 || month > 3
            || new RoundResultRepository(db).GetBySessionAndRound(sessionId, month) != null)
            throw new InvalidOperationException("Events require a persisted, unsettled active month.");
        return stored;
    }

}
