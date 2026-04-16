using System;
using UnityEngine;

public static class GameSessionState
{
    public static GameSessionEntity Current { get; private set; }

    public static bool HasSession => Current != null;

    public static void Set(GameSessionEntity session)
    {
        Current = session;
    }

    public static void Clear()
    {
        Current = null;
    }

    public static void LoadActiveSession(string userId)
    {
        var db = DatabaseInitializer.DatabaseService.Connection;
        var repo = new GameSessionRepository(db);

        Current = repo.GetActiveSessionByUserId(userId);
    }

    public static void Save()
    {
        if (Current == null)
            return;

        var db = DatabaseInitializer.DatabaseService.Connection;
        var repo = new GameSessionRepository(db);

        repo.Update(Current);
    }

    public static void ConfigureBusiness(
        string cityId,
        RestaurantType restaurantType,
        LocationZone locationZone,
        Segment targetSegment,
        string coherenceRating
    )
    {
        if (Current == null)
            return;

        if (Current.currentRound > 1)
        {
            Debug.LogWarning("Não é possível alterar a configuração inicial após o início da campanha.");
            return;
        }

        Current.cityId = cityId;
        Current.restaurantType = restaurantType;
        Current.locationZone = locationZone;
        Current.targetSegment = targetSegment;
        Current.coherenceRating = coherenceRating;
    }

    public static void SetCash(float value)
    {
        if (Current == null)
            return;

        Current.currentCash = value;
    }

    public static void AddCash(float value)
    {
        if (Current == null)
            return;

        Current.currentCash += value;
    }

    public static void SetLoan(string creditLineId, float loanBalance)
    {
        if (Current == null)
            return;

        Current.creditLineId = creditLineId;
        Current.loanBalance = loanBalance;
    }

    public static void SetReputation(int value)
    {
        if (Current == null)
            return;

        Current.reputationScore = Mathf.Clamp(value, 0, 100);
    }

    public static void SetTeamJson(string json)
    {
        if (Current == null)
            return;

        Current.teamJson = json;
    }

    public static void SetEquipmentJson(string json)
    {
        if (Current == null)
            return;

        Current.equipmentJson = json;
    }

    public static void AdvanceRound()
    {
        if (Current == null)
            return;

        Current.currentRound += 1;
    }

    public static void RegisterNegativeRound(bool isNegative)
    {
        if (Current == null)
            return;

        if (isNegative)
            Current.consecutiveNegativeRounds += 1;
        else
            Current.consecutiveNegativeRounds = 0;
    }

    public static void CompleteSession()
    {
        if (Current == null)
            return;

        Current.status = "COMPLETED";
        Current.completedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static void BankruptSession()
    {
        if (Current == null)
            return;

        Current.status = "BANKRUPT";
        Current.completedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}