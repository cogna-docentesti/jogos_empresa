using System;
using UnityEngine;

public static class GameSessionState
{
    public static GameSessionEntity Current { get; private set; }

    public static bool HasSession => Current != null;

    public static bool HasActiveSession =>
        Current != null && Current.status == GameSessionStatus.IN_PROGRESS;

    private static GameSessionRepository Repository
    {
        get
        {
            var db = DatabaseInitializer.DatabaseService.Connection;
            return new GameSessionRepository(db);
        }
    }

    public static void Initialize(string userId)
    {
        LoadActiveSession(userId);
    }

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
        Current = Repository.GetActiveSessionByUserId(userId);
    }

    public static void Save()
    {
        if (Current == null)
            return;

        Repository.Update(Current);
    }

    public static void ConfigureBusiness(
        string cityId,
        RestaurantType restaurantType,
        LocationZone locationZone,
        Segment targetSegment,
        string coherenceRating,
        bool save = true
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

        if (save)
            Save();
    }

    public static void SetCash(float value, bool save = true)
    {
        if (Current == null)
            return;

        Current.currentCash = value;

        if (save)
            Save();
    }

    public static void AddCash(float value, bool save = true)
    {
        if (Current == null)
            return;

        Current.currentCash += value;

        if (save)
            Save();
    }

    public static void SetLoan(string creditLineId, float loanBalance, bool save = false)
    {
        if (Current == null)
            return;

        Current.creditLineId = creditLineId;
        Current.loanBalance = loanBalance;

        if (save)
            Save();
    }

    public static void SetReputation(int value, bool save = false)
    {
        if (Current == null)
            return;

        Current.reputationScore = Mathf.Clamp(value, 0, 100);

        if (save)
            Save();
    }

    public static void SetTeamJson(string json, bool save = true)
    {
        if (Current == null)
            return;

        Current.teamJson = json;

        if (save)
            Save();
    }

    public static void SetEquipmentJson(string json, bool save = true)
    {
        if (Current == null)
            return;

        Current.equipmentJson = json;

        if (save)
            Save();
    }

    public static void RegisterNegativeRound(bool isNegative, bool save = false)
    {
        if (Current == null)
            return;

        if (isNegative)
            Current.consecutiveNegativeRounds += 1;
        else
            Current.consecutiveNegativeRounds = 0;

        if (save)
            Save();
    }

    public static void AdvanceRoundAndSave()
    {
        if (Current == null)
            return;

        Current.currentRound += 1;
        Save();
    }

    public static void CompleteSession()
    {
        if (Current == null)
            return;

        Current.status = GameSessionStatus.COMPLETED;
        Current.completedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Save();
    }

    public static void BankruptSession()
    {
        if (Current == null)
            return;

        Current.status = GameSessionStatus.BANKRUPT;
        Current.completedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Save();
    }
}