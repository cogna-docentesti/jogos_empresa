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

    // =============================
    // CONFIGURAÇÃO INICIAL
    // =============================

    public static void SetCity(string cityId)
    {
        if (Current == null)
            return;

        if (Current.currentRound > 1)
        {
            Debug.LogWarning("Não é possível alterar a cidade após o início da campanha.");
            return;
        }

        Current.cityId = cityId;
    }

    public static void SetRestaurant(RestaurantType restaurantType)
    {
        if (Current == null)
            return;

        if (Current.currentRound > 1)
        {
            Debug.LogWarning("Não é possível alterar o tipo de restaurante após o início da campanha.");
            return;
        }

        Current.restaurantType = restaurantType;
    }

    public static void SetLocation(LocationZone locationZone)
    {
        if (Current == null)
            return;

        if (Current.currentRound > 1)
        {
            Debug.LogWarning("Não é possível alterar a localização após o início da campanha.");
            return;
        }

        Current.locationZone = locationZone;
    }

    public static void SetTargetSegment(Segment targetSegment)
    {
        if (Current == null)
            return;

        if (Current.currentRound > 1)
        {
            Debug.LogWarning("Não é possível alterar o segmento após o início da campanha.");
            return;
        }

        Current.targetSegment = targetSegment;
    }

    public static void SetPriceStrategy(PriceStrategy priceStrategy)
    {
        if (Current == null)
            return;

        Current.priceStrategy = priceStrategy;
    }

    public static void SetCoherence(string coherenceRating)
    {
        if (Current == null)
            return;

        if (Current.currentRound > 1)
        {
            Debug.LogWarning("Não é possível alterar a coerência após o início da campanha.");
            return;
        }

        Current.coherenceRating = coherenceRating;
    }

    // =============================
    // DADOS DINÂMICOS 
    // =============================

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

    public static void SetLoan(string creditLineId, float loanBalance, bool save = true)
    {
        if (Current == null)
            return;

        Current.creditLineId = creditLineId;
        Current.loanBalance = loanBalance;

        if (save)
            Save();
    }

    public static void SetReputation(int value, bool save = true)
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

    public static void RegisterNegativeRound(bool isNegative, bool save = true)
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

    public static void SetAlignment(AlignmentResult result)
    {
        if (Current == null || result == null)
            return;

        Current.alignmentScore = result.totalScore;
        Current.alignmentClassification = result.classification.ToString();
        Current.alignmentFactor = result.alignmentFactor;
        Current.coherenceRating = result.classification.ToString();
    }

    public static void AdvanceRound(bool save = true)
    {
        if (Current == null)
            return;

        Current.currentRound += 1;

        if (save)
            Save();
    }

    // =============================
    // ENCERRAMENTO DE UMA SESSÃO (1 ANO)
    // =============================

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