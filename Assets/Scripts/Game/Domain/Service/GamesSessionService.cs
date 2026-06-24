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
    // CREATE SESSION / LOAD SESSION
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
            menuPricingJson = MenuPricingHelper.ToJson(new MenuPricingData()),
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
    // CONFIGURA��O E STATE MACHINE
    // =============================

    public void ConfirmLocation(LocationZone locationZone)
    {
        if (!GameSessionState.HasSession)
            return;

        GameSessionState.SetLocation(locationZone);

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Config_Restaurant);
    }

    public void ConfirmRestaurant(RestaurantType type)
    {
        if (!GameSessionState.HasSession)
            return;

        GameSessionState.SetRestaurant(type);

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Config_TargetSegment);
    }

    public void ConfirmTargetSegmentAndPrice(Segment targetSegment, float selectedPrice)
    {
        if (!GameSessionState.HasSession)
            return;

        GameSessionState.SetTargetSegment(targetSegment);
        GameSessionState.SetSelectedPrice(selectedPrice);

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Config_Review);
    }

    public void ConfirmMenuPricing(MenuPricingData menuPricing)
    {
        if (!GameSessionState.HasSession)
            return;

        GameSessionState.SetMenuPricingJson(MenuPricingHelper.ToJson(menuPricing));
        GameSessionState.Save();

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Config_Review);
    }

    public void ConfirmStructuralConfiguration(string coherenceRating)
    {
        if (!GameSessionState.HasSession)
            return;

        GameSessionState.SetCoherence(coherenceRating);
        GameSessionState.Save();

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Initial_Equipment);
    }

    public void ConfirmInitialEquipment(List<EquipmentData> equipments)
    {
        AddEquipments(equipments);

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Initial_Team);
    }

    public void ConfirmInitialTeam()
    {
        GameSessionState.Save();

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Initial_Capital);
    }

    public void ConfirmInitialCapital()
    {
        GameSessionState.Save();

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Management_Hub);
    }

    // =============================
    // EQUIPAMENTOS
    // =============================

    public void AddEquipments(List<EquipmentData> newEquipments)
    {
        if (!GameSessionState.HasSession || newEquipments == null || newEquipments.Count == 0)
            return;

        var currentData = EquipmentSelectionHelper.FromJson(GameSessionState.Current.equipmentJson);

        var newIds = newEquipments
            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.id))
            .Select(e => e.id);

        EquipmentSelectionHelper.AddUniqueIds(currentData, newIds);

        string updatedJson = EquipmentSelectionHelper.ToJson(currentData);
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

    // =============================
    // ROUND FLOW
    // =============================

    public void StartRound()
    {
        if (!GameSessionState.HasSession)
            return;

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Round_Start);

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Round_Sales);
    }

    public RoundResultEntity ProcessCurrentRound()
    {
        if (!GameSessionState.HasSession)
            return null;

        var sm = GameManager.Instance.StateMachine;

        if (sm.CurrentState != GameState.Round_Sales)
        {
            Debug.LogWarning("A rodada n�o est� no estado correto.");
            return null;
        }

        sm.TryChangeState(GameState.Round_Costs);
        sm.TryChangeState(GameState.Round_Event);

        var roundService = new RoundService();

        RoundResultEntity result = roundService.ProcessRound();

        sm.TryChangeState(GameState.Round_Summary);

        EvaluateRoundEnd(result);

        return result;
    }

    private void EvaluateRoundEnd(RoundResultEntity result)
    {
        var session = GameSessionState.Current;

        if (session == null)
            return;

        if (session.status == GameSessionStatus.BANKRUPT)
        {
            GameManager.Instance.StateMachine
                .TryChangeState(GameState.GameOver_Bankruptcy);

            return;
        }

        if (session.status == GameSessionStatus.COMPLETED)
        {
            GameManager.Instance.StateMachine
                .TryChangeState(GameState.FinalReport);

            return;
        }

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Management_Hub);
    }
}
