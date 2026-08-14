using System;
using UnityEngine;

public static class GameSessionState
{
    public static GameSessionEntity Current { get; private set; }

    public static bool HasSession => Current != null;

    public static bool HasActiveSession =>
        Current != null && Current.status == GameSessionStatus.IN_PROGRESS;
/*
    private static GameSessionRepository Repository
    {
        get
        {
            var db = DatabaseInitializer.DatabaseService.Connection;
            return new GameSessionRepository(db);
        }
    }
*/
    private static GameSessionRepository Repository
    {
        get
        {
            var svc  = DatabaseInitializer.DatabaseService;
            if (svc == null)
            {
                Debug.LogError("[GameSessionState] DatabaseService não encontrado.");
                return null;
            }

            var conn = svc.Connection;
            if (conn == null)
            {
                Debug.LogError("[GameSessionState] Conexão com o banco está fechada.");
                return null;
            }

            return new GameSessionRepository(conn);
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
        var repository = Repository;

        // O getter acima ja avisa no Console quando o banco esta fora, mas
        // devolve null. Sem esta guarda o null virava NullReferenceException
        // aqui dentro, subia ate o GameManager.Start() e impedia o
        // SceneManager.LoadScene("GameScene") de rodar - o jogo travava na
        // Bootstrap com a tela vazia.
        if (repository == null)
        {
            Current = null;
            return;
        }

        Current = repository.GetActiveSessionByUserId(userId);
    }

    public static void Save()
    {
        if (Current == null)
            return;

        var repository = Repository;

        // Mesma historia do LoadActiveSession: sem banco, nao ha o que gravar.
        // A partida segue em memoria.
        if (repository == null)
            return;

        // InsertOrReplace, nao Update.
        //
        // O Update do SQLite so mexe numa linha que JA existe. Como a sessao
        // nova nascia direto em CreateNewSession -> Set -> Save, sem nenhum
        // Insert antes, o Update casava com zero linhas e ia embora em silencio:
        // o jogo nunca gravou uma sessao sequer, em nenhuma plataforma. Dava
        // para ver reabrindo o app - voltava sempre em "Nenhuma sessao
        // encontrada", com o game.db parado no tamanho do schema vazio.
        //
        // sessionId e [PrimaryKey], entao InsertOrReplace resolve os dois casos
        // (primeira gravacao e atualizacoes seguintes) e continua idempotente.
        repository.InsertOrReplace(Current);
    }

    // =============================
    // CONFIGURACA INICIAL
    // =============================

    public static void SetCity(string cityId)
    {
        if (Current == null)
            return;

        if (Current.currentRound > 1)
        {
            Debug.LogWarning("N�o � poss�vel alterar a cidade ap�s o in�cio da campanha.");
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
            Debug.LogWarning("N�o � poss�vel alterar o tipo de restaurante ap�s o in�cio da campanha.");
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
            Debug.LogWarning("Nao e possivel alterar a localizacao apos o inicio da campanha.");
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
            Debug.LogWarning("N�o � poss�vel alterar o segmento ap�s o in�cio da campanha.");
            return;
        }

        Current.targetSegment = targetSegment;
    }

    public static void SetSelectedPrice(float selectedPrice)
    {
        if (Current == null)
            return;

        Current.selectedPrice = Mathf.Max(0f, selectedPrice);
    }

    public static void SetMenuPricingJson(string json, bool save = false)
    {
        if (Current == null)
            return;

        Current.menuPricingJson = string.IsNullOrWhiteSpace(json)
            ? MenuPricingHelper.ToJson(new MenuPricingData())
            : json;

        if (save)
            Save();
    }

    public static void SetCoherence(string coherenceRating)
    {
        if (Current == null)
            return;

        if (Current.currentRound > 1)
        {
            Debug.LogWarning("N�o � poss�vel alterar a coer�ncia ap�s o in�cio da campanha.");
            return;
        }

        Current.coherenceRating = coherenceRating;
    }

    // =============================
    // DADOS DIN�MICOS 
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
    // ENCERRAMENTO DE UMA SESS�O (1 ANO)
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
