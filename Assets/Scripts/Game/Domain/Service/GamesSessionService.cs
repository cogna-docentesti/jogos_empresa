using System;

public class GameSessionService
{
    private readonly string _userId;
    private readonly string _professorId;

    public GameSessionService(string userId, string professorId)
    {
        _userId = userId;
        _professorId = professorId;
    }

    // =============================
    // CRIAÇÃO / CARREGAMENTO
    // =============================

    public GameSessionEntity CreateNewSession()
    {
        if (GameSessionState.HasActiveSession)
            throw new Exception("Já existe uma sessão ativa.");

        var session = new GameSessionEntity
        {
            sessionId = Guid.NewGuid().ToString(),
            userId = _userId,
            professorId = _professorId,
            status = GameSessionStatus.IN_PROGRESS,
            currentRound = 1,
            initialCapital = 150000f,
            currentCash = 150000f,
            loanBalance = 0f,
            creditLineId = null,
            reputationScore = 50,
            teamJson = "{}",
            equipmentJson = "{}",
            consecutiveNegativeRounds = 0,
            startedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            completedAt = null,
            syncedAt = 0
        };

        GameSessionState.Set(session);
        GameSessionState.Save();

        return session;
    }

    public void LoadActiveSession()
    {
        GameSessionState.Initialize(_userId);
    }

    // =============================
    // CONFIGURAÇÃO (USADO PELAS TELAS)
    // =============================

    public void SetCity(string cityId)
    {
        if (!GameSessionState.HasSession)
            return;

        GameSessionState.Current.cityId = cityId;
        GameSessionState.Save();
    }

    public void SetRestaurant(RestaurantType type, Segment targetSegment)
    {
        if (!GameSessionState.HasSession)
            return;

        GameSessionState.Current.restaurantType = type;
        GameSessionState.Current.targetSegment = targetSegment;

        GameSessionState.Save();
    }

    public void SetLocation(LocationZone locationZone)
    {
        if (!GameSessionState.HasSession)
            return;

        GameSessionState.Current.locationZone = locationZone;

        GameSessionState.Save();
    }

    public void SetCoherence(string coherenceRating)
    {
        if (!GameSessionState.HasSession)
            return;

        GameSessionState.Current.coherenceRating = coherenceRating;

        GameSessionState.Save();
    }

    public void SetEquipment(string equipmentJson)
    {
        GameSessionState.SetEquipmentJson(equipmentJson);
    }

    public void SetTeam(string teamJson)
    {
        GameSessionState.SetTeamJson(teamJson);
    }

    // =============================
    // FINALIZA CONFIGURAÇÃO
    // =============================

    public void ConfirmConfiguration()
    {
        if (!GameSessionState.HasSession)
            return;

        // aqui você pode validar depois se quiser
        GameSessionState.Save();
    }
}
