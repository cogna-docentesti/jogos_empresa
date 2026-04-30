using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    // CREATE SESSION/ LOAD SESSION
    // =============================

    public GameSessionEntity CreateNewSession()
    {
        if (GameSessionState.HasActiveSession)
            throw new Exception("J� existe uma sess�o ativa.");

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
            teamJson = TeamSelectionHelper.ToJson(new TeamSelectionData()),
            equipmentJson = EquipmentSelectionHelper.ToJson(new EquipmentSelectionData()),
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
    // CONFIGURACAO (USADO PELAS TELAS)
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

    // =============================
    // FINALIZA CONFIGURA��O DAS DECIS�ES INICIAIS
    // =============================

    public void ConfirmConfiguration()
    {
        if (!GameSessionState.HasSession)
            return;

       
        GameSessionState.Save();
    }

    // =============================
    // EQUIPAMENTOS
    // =============================

    public void AddEquipments(List<EquipmentData> newEquipments)
    {
        Debug.Log("Qtd equipamentos: " + (newEquipments?.Count ?? -1));
        Debug.Log(GameSessionState.HasSession);
        if (!GameSessionState.HasSession || newEquipments == null || newEquipments.Count == 0)
            return;

        var currentData = EquipmentSelectionHelper.FromJson(GameSessionState.Current.equipmentJson);

        var newIds = newEquipments
            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.id))
            .Select(e => e.id);

        EquipmentSelectionHelper.AddUniqueIds(currentData, newIds);

        string updatedJson = EquipmentSelectionHelper.ToJson(currentData);
        Debug.Log("JSON Equip atualizado: " + updatedJson);
        GameSessionState.SetEquipmentJson(updatedJson);
    }

    public void RemoveEquipments(List<EquipmentData> equipmentsToRemove)
    {
        if (!GameSessionState.HasSession || equipmentsToRemove == null || equipmentsToRemove.Count == 0)
            return;

        var currentData = EquipmentSelectionHelper.FromJson(GameSessionState.Current.equipmentJson);

        var idsToRemove = equipmentsToRemove
            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.id))
            .Select(e => e.id);

        EquipmentSelectionHelper.RemoveIds(currentData, idsToRemove);

        string updatedJson = EquipmentSelectionHelper.ToJson(currentData);
        GameSessionState.SetEquipmentJson(updatedJson);
    }

    public bool HasEquipment(EquipmentData equipment)
    {
        if (!GameSessionState.HasSession || equipment == null || string.IsNullOrWhiteSpace(equipment.id))
            return false;

        var currentData = EquipmentSelectionHelper.FromJson(GameSessionState.Current.equipmentJson);
        return EquipmentSelectionHelper.ContainsId(currentData, equipment.id);
    }

    public List<string> GetCurrentEquipmentIds()
    {
        if (!GameSessionState.HasSession)
            return new List<string>();

        var currentData = EquipmentSelectionHelper.FromJson(GameSessionState.Current.equipmentJson);
        return EquipmentSelectionHelper.GetIds(currentData);
    }

    // =============================
    // EQUIPE
    // =============================

    public void AddTeamMember(RoleData role, int quantity = 1)
    {

        if (!GameSessionState.HasSession || role == null || string.IsNullOrWhiteSpace(role.id) || quantity <= 0)
            return;

        var currentData = TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson);

        TeamSelectionHelper.AddMember(currentData, role.id, quantity);

        string updatedJson = TeamSelectionHelper.ToJson(currentData);
        Debug.Log("JSON team atualizado: " + updatedJson);
        GameSessionState.SetTeamJson(updatedJson);
    }

    public void RemoveTeamMember(RoleData role, int quantity = 1)
    {
        if (!GameSessionState.HasSession || role == null || string.IsNullOrWhiteSpace(role.id) || quantity <= 0)
            return;

        var currentData = TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson);

        TeamSelectionHelper.RemoveMember(currentData, role.id, quantity);

        string updatedJson = TeamSelectionHelper.ToJson(currentData);
        GameSessionState.SetTeamJson(updatedJson);
    }

    public void SetTeamMemberQuantity(RoleData role, int quantity)
    {
        if (!GameSessionState.HasSession || role == null || string.IsNullOrWhiteSpace(role.id))
            return;

        var currentData = TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson);

        TeamSelectionHelper.SetMemberQuantity(currentData, role.id, quantity);

        string updatedJson = TeamSelectionHelper.ToJson(currentData);
        GameSessionState.SetTeamJson(updatedJson);
    }

    public int GetTeamMemberQuantity(RoleData role)
    {
        if (!GameSessionState.HasSession || role == null || string.IsNullOrWhiteSpace(role.id))
            return 0;

        var currentData = TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson);
        return TeamSelectionHelper.GetMemberQuantity(currentData, role.id);
    }

    public bool HasTeamMember(RoleData role)
    {
        return GetTeamMemberQuantity(role) > 0;
    }

    public List<TeamSelectionItem> GetCurrentTeam()
    {
        if (!GameSessionState.HasSession)
            return new List<TeamSelectionItem>();

        var currentData = TeamSelectionHelper.FromJson(GameSessionState.Current.teamJson);
        return TeamSelectionHelper.GetMembers(currentData);
    }
}
