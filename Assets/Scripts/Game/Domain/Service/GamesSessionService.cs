using System;
using System.Collections.Generic;
using System.Linq;
using Game.Infrastructure.Session;
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

    /// <summary>
    /// Cria uma sessao nova SO em memoria. Nao grava nada no banco: a primeira
    /// gravacao acontece no confirm da D3, em CommitInitialDecisions().
    /// Se o jogador fechar o jogo antes disso, nenhuma linha fica para tras.
    /// </summary>
    public GameSessionEntity CreateNewSession()
    {
        // Save incompativel (Thaysla): so vira sessao nova pelo RestartSession,
        // depois que o jogador confirma no aviso. Evita apagar o save sem querer.
        if (GameSessionState.HasActiveSession || GameSessionState.HasIncompatibleSave)
            throw new Exception("Já existe uma sessão ativa.");

        var session = new GameSessionEntity
        {
            sessionId = Guid.NewGuid().ToString(),
            userId = _userId,
            professorId = _professorId,
            status = GameSessionStatus.IN_PROGRESS,
            currentRound = 1,

            // Estes dois sao [NotNull] na tabela, mas so recebem valor bem mais
            // adiante no fluxo: cityId em SetCity e coherenceRating em
            // SetAlignment. Sem inicializar, o INSERT morria com
            // "NOT NULL constraint failed: GameSessionEntity.cityId".
            //
            // String vazia e o "ainda nao definido" honesto - satisfaz a coluna
            // sem inventar um valor que o jogo depois trataria como real.
            cityId = string.Empty,
            coherenceRating = string.Empty,

            initialCapital = 0f,
            currentCash = 0f,
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

        // So memoria. Antes havia um GameSessionState.Save() aqui, que criava a
        // linha no banco no instante em que o jogo abria, antes de qualquer escolha.
        GameSessionState.Set(session);

        return session;
    }

    public GameSessionEntity RestartSession()
    {
        var session = GameSessionState.Current ?? GameSessionState.IncompatibleSession;
        var db = DatabaseInitializer.DatabaseService?.Connection;
        if (session == null || db == null)
            throw new InvalidOperationException("Sessao ou banco indisponivel para reiniciar.");

        GameSessionEntity replacement = null;
        try
        {
            db.RunInTransaction(() =>
            {
                new RoundResultRepository(db).DeleteBySessionId(session.sessionId);
                var historyRepository = new SessionEventHistoryRepository(db);
                foreach (var item in historyRepository.GetBySession(session.sessionId))
                    historyRepository.Delete(item);
                new GameSessionRepository(db).Delete(session);
                GameSessionState.Clear();
                replacement = new GameSessionService(session.userId, session.professorId).CreateNewSession();
            });
        }
        catch
        {
            GameSessionState.LoadActiveSession(session.userId);
            throw;
        }
        Game.Infrastructure.Session.PlayerSession.Clear();
        return replacement;
    }

    public void LoadActiveSession()
    {
        GameSessionState.Initialize(_userId);
    }

    // =============================
    // DECISOES INICIAIS (D1, D2, D3)
    // =============================
    // As telas D1, D2 e D3 escrevem so no rascunho PlayerSession.
    // Os metodos abaixo sao o unico caminho do rascunho para o banco.

    /// <summary>
    /// Unico ponto do fluxo inicial que escreve no SQLite.
    /// Le o rascunho (PlayerSession), copia tudo para a sessao em memoria e
    /// grava uma vez so. Nao troca de tela: quem decide a navegacao e o
    /// ConfirmInitialDecisions(), logo abaixo.
    /// </summary>
    public bool CommitInitialDecisions()
    {
        if (!GameSessionState.HasSession)
        {
            Debug.LogError("[GameSessionService] Não há sessão ativa para gravar.");
            return false;
        }

        if (!PlayerSession.IsComplete)
        {
            Debug.LogWarning(
                "[GameSessionService] Decisões incompletas: " +
                $"localização: {PlayerSession.HasLocation}, " +
                $"restaurante: {PlayerSession.HasRestaurant}, " +
                $"cardápio: {PlayerSession.HasMenu}, " +
                $"cardápio do restaurante certo: {PlayerSession.MenuRestaurantType == PlayerSession.SelectedRestaurantType}");
            return false;
        }

        if (GameSessionState.Current.currentRound > 1)
        {
            Debug.LogWarning("[GameSessionService] A campanha já começou. As decisões iniciais não podem mais ser alteradas.");
            return false;
        }

        // A identificacao fica em PlayerPrefs. Se a memoria foi limpa (por
        // exemplo pelo botao Resetar), recarrega de la antes de copiar.
        if (!PlayerSession.HasIdentification && !PlayerSession.TryLoadIdentification())
        {
            // So acontece quando o jogo e aberto direto pela GameScene no editor,
            // sem passar pela cena 0_Identification. Na build a identificacao e
            // sempre a primeira cena. A sessao e gravada mesmo assim, com os
            // tres campos nulos.
            Debug.LogWarning("[GameSessionService] Identificação do aluno não encontrada. " +
                             "studentName, studentRA e companyName serão gravados vazios.");
        }

        // Os setters do GameSessionState respeitam a trava currentRound > 1.
        GameSessionState.SetIdentification(
            PlayerSession.StudentName,
            PlayerSession.StudentRA,
            PlayerSession.RestaurantName);

        GameSessionState.SetLocation(PlayerSession.SelectedZone.Value);
        GameSessionState.SetRestaurant(PlayerSession.SelectedRestaurantType.Value);
        GameSessionState.SetTargetSegment(PlayerSession.SelectedTargetSegment.Value);
        GameSessionState.SetMenuPricingJson(MenuPricingHelper.ToJson(PlayerSession.GetMenu()));

        // coherenceRating continua vazio aqui. Ele e calculado pelo
        // AlignmentEngine no ConfirmInitialTeam, quando a equipe ja existe, e
        // depende da matriz que a Thaysla esta recalibrando (T-06).

        bool saved = GameSessionState.Persist();

        if (saved)
            Debug.Log($"[GameSessionService] Decisões iniciais gravadas. Sessão {GameSessionState.Current.sessionId}.");

        return saved;
    }

    /// <summary>
    /// Confirm da D3: grava e, se deu certo, sai das decisoes obrigatorias
    /// para o primeiro tutorial (InitialDecisionFlow.AfterCommit).
    /// Devolve false se nao conseguiu gravar; nesse caso o jogador continua na D3.
    /// </summary>
    public bool ConfirmInitialDecisions()
    {
        if (!CommitInitialDecisions())
            return false;

        var stateMachine = GameManager.Instance != null ? GameManager.Instance.StateMachine : null;

        if (stateMachine == null)
        {
            Debug.LogError("[GameSessionService] GameManager não encontrado. A sessão foi gravada, mas a tela não avançou.");
            return true;
        }

        if (stateMachine.CurrentState != InitialDecisionFlow.Review)
            stateMachine.TryChangeState(InitialDecisionFlow.Review);

        stateMachine.TryChangeState(InitialDecisionFlow.AfterCommit);
        return true;
    }

    public void ConfirmInitialEquipment(List<EquipmentData> equipments)
    {
        AddEquipments(equipments);

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Initial_Team);
    }

    public void ConfirmInitialTeam(List<RoleData> availableRoles)
    {
        if (!GameSessionState.HasSession)
            return;

        GameSessionState.Save();

        var session = GameSessionState.Current;
        var teamData = TeamSelectionHelper.FromJson(session.teamJson);
        var restaurantData = GetRestaurantData(session.restaurantType);
        var menuPricing = MenuPricingHelper.FromJson(session.menuPricingJson);

        AlignmentResult alignment = AlignmentEngine.Calculate(
            session.restaurantType,
            session.targetSegment,
            session.locationZone,
            restaurantData,
            menuPricing,
            teamData,
            availableRoles
        );

        GameSessionState.SetAlignment(alignment);
        GameSessionState.Save();

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Management_Hub);
    }

    public void ConfirmInitialCapital()
    {
        GameSessionState.Save();

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Initial_Equipment);
    }

    private RestaurantData GetRestaurantData(RestaurantType restaurantType)
    {
        return Resources.LoadAll<RestaurantData>("Restaurants")
            .FirstOrDefault(restaurant => restaurant != null && restaurant.type == restaurantType);
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
        if (!GameSessionState.HasActiveSession)
            return;

        var session = GameSessionState.Current;
        if (session.currentRound < 1 || session.currentRound > 3)
            return;

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Round_Start);

        GameManager.Instance.StateMachine
            .TryChangeState(GameState.Round_Sales);
    }

    public RoundResultEntity ProcessCurrentRound()
    {
        if (!GameSessionState.HasActiveSession || GameManager.Instance?.StateMachine == null)
            return null;

        var sm = GameManager.Instance.StateMachine;

        if (sm.CurrentState != GameState.Round_Sales)
        {
            Debug.LogWarning("[GameSessionService] The month is not in the sales state.");
            return null;
        }

        RoundResultEntity result;
        try
        {
            result = new RoundService().ProcessRound();
            if (result == null) return null;
        }
        catch (Exception exception)
        {
            Debug.LogError("[GameSessionService] Monthly settlement failed: " + exception.Message);
            return null;
        }

        sm.TryChangeState(GameState.Round_Costs);
        sm.TryChangeState(GameState.Round_Event);
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
