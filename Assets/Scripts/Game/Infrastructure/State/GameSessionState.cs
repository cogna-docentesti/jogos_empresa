using System;
using UnityEngine;

/// <summary>
/// A sessao (partida) em memoria e a ponte com a tabela GameSessionEntity.
///
/// Regra de gravacao:
///  - Uma sessao nova nasce SO em memoria (CreateNewSession). Nao existe linha
///    no banco ate o confirm da D3.
///  - O confirm da D3 chama Persist(), que faz o primeiro INSERT. A partir dai
///    IsPersisted fica true.
///  - Depois disso, cada tela (Banco, Equipamentos, RH, rodadas) pode chamar
///    Save() por conta propria. Antes disso, Save() e ignorado de proposito:
///    e o que garante que nenhuma escolha das decisoes iniciais chegue ao
///    banco antes da hora, nem por acidente (por exemplo, o OnApplicationQuit
///    do GameManager).
/// </summary>
public static class GameSessionState
{
    public static GameSessionEntity Current { get; private set; }

    public static GameSessionEntity IncompatibleSession { get; private set; }
    public static bool HasIncompatibleSave => IncompatibleSession != null;

    public static bool HasSession => Current != null;

    public static bool HasActiveSession =>
        Current != null && Current.status == GameSessionStatus.IN_PROGRESS;

    /// <summary>
    /// True quando a sessao atual ja tem linha no banco: veio do
    /// LoadActiveSession ou ja passou pelo Persist() do confirm da D3.
    /// </summary>
    public static bool IsPersisted { get; private set; }

    /// <summary>
    /// True quando algo mudou na sessao em memoria e ainda nao foi gravado.
    /// Usado pelo dialogo Sair (E-05) para decidir se pergunta "salvar antes de sair?".
    /// </summary>
    public static bool HasUnsavedChanges { get; private set; }

    private static void MarkDirty() => HasUnsavedChanges = true;

    private static GameSessionRepository Repository
    {
        get
        {
            var svc  = DatabaseInitializer.DatabaseService;
            if (svc == null)
            {
                Debug.LogError("[GameSessionState] DatabaseService não encontrado. "
                             + "Confirme que o prefab Database está na GameScene.");
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

    /// <summary>
    /// Coloca uma sessao NOVA em memoria. Ela ainda nao existe no banco, entao
    /// IsPersisted volta a ser false.
    /// </summary>
    public static void Set(GameSessionEntity session)
    {
        Current = session;
        IncompatibleSession = null;
        IsPersisted = false;
        HasUnsavedChanges = session != null;
    }

    public static void Clear()
    {
        Current = null;
        IsPersisted = false;
        HasUnsavedChanges = false;
        IncompatibleSession = null;
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
            Clear();
            return;
        }

        // Comeca do zero: sem sessao, sem save incompativel, nada gravado.
        Clear();

        var session = repository.GetActiveSessionByUserId(userId);
        if (session == null)
            return;

        // Save do ciclo antigo de 12 meses (Thaysla): a partida existe no banco
        // mas nao pode ser jogada no ciclo de 3 meses. Fica separada em
        // IncompatibleSession ate o jogador confirmar o reinicio.
        var db = DatabaseInitializer.DatabaseService.Connection;
        var results = new RoundResultRepository(db).GetBySessionId(session.sessionId);
        var history = new SessionEventHistoryRepository(db).GetBySession(session.sessionId);
        if (session.currentRound > 3 || results.Exists(result => result.round > 3)
            || history.Exists(item => item.round > 3))
        {
            IncompatibleSession = session;
            return;
        }

        // Veio do banco, entao ja esta gravada: os Save() seguintes valem.
        Current = session;
        IsPersisted = true;
        HasUnsavedChanges = false;
    }

    /// <summary>
    /// Primeira gravacao da sessao. Chamado uma unica vez no fluxo inicial, por
    /// GameSessionService.CommitInitialDecisions() (confirm da D3).
    /// Devolve false se nao houver sessao, se o banco estiver fora ou se o
    /// SQLite recusar a linha. Nesse caso IsPersisted continua false e o
    /// jogador pode tentar de novo.
    /// </summary>
    public static bool Persist()
    {
        if (Current == null)
        {
            Debug.LogError("[GameSessionState] Persist chamado sem sessao em memoria.");
            return false;
        }

        var repository = Repository;
        if (repository == null)
            return false;

        try
        {
            // InsertOrReplace: cria a linha se nao existe, sobrescreve se existe.
            // sessionId e a chave primaria, entao confirmar a D3 duas vezes
            // continua deixando UMA linha so.
            repository.InsertOrReplace(Current);
            IsPersisted = true;
            HasUnsavedChanges = false;
            Debug.Log($"[Banco] PRIMEIRA GRAVACAO da sessao {Current.sessionId} (confirm da D3). "
                    + $"Linhas na tabela GameSessionEntity: {repository.Table().Count()}.");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[GameSessionState] Falha ao gravar a sessao: " + e.Message + "\n" + e);
            return false;
        }
    }

    /// <summary>
    /// Regrava a sessao que JA existe no banco. Antes do confirm da D3 nao faz
    /// nada (ver comentario no topo da classe).
    /// </summary>
    public static void Save()
    {
        if (Current == null)
            return;

        if (!IsPersisted)
        {
            // Log simples (nao warning): o GameManager chama Save() no
            // OnApplicationPause/Quit, e sair do jogo no meio da D2 e normal.
            Debug.Log("[Banco] Save ignorado: a sessao ainda nao foi gravada (isso so acontece no confirm da D3).");
            return;
        }

        var repository = Repository;

        // Mesma historia do LoadActiveSession: sem banco, nao ha o que gravar.
        // A partida segue em memoria.
        if (repository == null)
            return;

        // InsertOrReplace, nao Update.
        //
        // O Update do SQLite so mexe numa linha que JA existe. Como a sessao
        // nova nascia direto em CreateNewSession -> Set -> Save, sem nenhum
        // Insert antes, o Update casava com zero linhas e ia embora em silencio.
        // sessionId e [PrimaryKey], entao InsertOrReplace resolve os dois casos
        // e continua idempotente.
        repository.InsertOrReplace(Current);
        HasUnsavedChanges = false;
        Debug.Log($"[Banco] Sessao {Current.sessionId} atualizada (rodada {Current.currentRound}, caixa {Current.currentCash:N0}).");
    }

    // =============================
    // CONFIGURACAO INICIAL
    // =============================
    // Estes setters so mexem no objeto em memoria. Quem grava e o Persist().
    // A trava currentRound > 1 impede mudar a estrategia depois que a
    // campanha comecou.

    private static bool IsLockedForSetup(string what)
    {
        if (Current.currentRound <= 1)
            return false;

        Debug.LogWarning($"Não é possível alterar {what} após o início da campanha.");
        return true;
    }

    public static void SetCity(string cityId)
    {
        if (Current == null || IsLockedForSetup("a cidade"))
            return;

        Current.cityId = cityId;
        MarkDirty();
    }

    public static void SetRestaurant(RestaurantType restaurantType)
    {
        if (Current == null || IsLockedForSetup("o tipo de restaurante"))
            return;

        float initialCapital = restaurantType switch
        {
            RestaurantType.PODRAO => 65000f,
            RestaurantType.JAPONES => 69000f,
            RestaurantType.FRANCES => 72000f,
            _ => throw new ArgumentOutOfRangeException(nameof(restaurantType), restaurantType, null)
        };

        // Replace the initial contribution without duplicating it or erasing expenses.
        Current.currentCash += initialCapital - Current.initialCapital;
        Current.initialCapital = initialCapital;
        Current.restaurantType = restaurantType;
        MarkDirty();
        Save();
    }

    public static void SetLocation(LocationZone locationZone)
    {
        if (Current == null || IsLockedForSetup("a localização"))
            return;

        Current.locationZone = locationZone;
        MarkDirty();
    }

    public static void SetTargetSegment(Segment targetSegment)
    {
        if (Current == null || IsLockedForSetup("o segmento"))
            return;

        Current.targetSegment = targetSegment;
        MarkDirty();
    }

    /// <summary>Dados da cena 0_Identification (tela do Renan, R-02).</summary>
    public static void SetIdentification(string studentName, string studentRA, string companyName)
    {
        if (Current == null || IsLockedForSetup("a identificação"))
            return;

        Current.studentName = studentName;
        Current.studentRA = studentRA;
        Current.companyName = companyName;
        MarkDirty();
    }

    public static void SetSelectedPrice(float selectedPrice)
    {
        if (Current == null)
            return;

        Current.selectedPrice = Mathf.Max(0f, selectedPrice);
        MarkDirty();
    }

    public static void SetMenuPricingJson(string json, bool save = false)
    {
        if (Current == null)
            return;

        Current.menuPricingJson = string.IsNullOrWhiteSpace(json)
            ? MenuPricingHelper.ToJson(new MenuPricingData())
            : json;

        MarkDirty();

        if (save)
            Save();
    }

    public static void SetCoherence(string coherenceRating)
    {
        if (Current == null || IsLockedForSetup("a coerência"))
            return;

        Current.coherenceRating = coherenceRating;
        MarkDirty();
    }

    // =============================
    // DADOS DINAMICOS
    // =============================
    // Usados depois do confirm da D3 (Banco, Equipamentos, RH, rodadas).
    // O save = true padrao continua valendo: essas telas gravam por conta
    // propria. Antes da D3 o Save() e ignorado, entao nao ha risco.

    public static void SetCash(float value, bool save = true)
    {
        if (Current == null)
            return;

        Current.currentCash = value;

        MarkDirty();

        if (save)
            Save();
    }

    public static void AddCash(float value, bool save = true)
    {
        if (Current == null)
            return;

        Current.currentCash += value;

        MarkDirty();

        if (save)
            Save();
    }

    public static void SetLoan(string creditLineId, float loanBalance, bool save = true)
    {
        if (Current == null)
            return;

        Current.creditLineId = creditLineId;
        Current.loanBalance = loanBalance;

        MarkDirty();

        if (save)
            Save();
    }

    public static void SetReputation(int value, bool save = true)
    {
        if (Current == null)
            return;

        Current.reputationScore = Mathf.Clamp(value, 0, 100);

        MarkDirty();

        if (save)
            Save();
    }

    public static void SetTeamJson(string json, bool save = true)
    {
        if (Current == null)
            return;

        Current.teamJson = json;

        MarkDirty();

        if (save)
            Save();
    }

    public static void SetEquipmentJson(string json, bool save = true)
    {
        if (Current == null)
            return;

        Current.equipmentJson = json;

        MarkDirty();

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

        MarkDirty();

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
        MarkDirty();
    }

    public static void AdvanceRound(bool save = true)
    {
        if (!HasActiveSession)
            return;

        if (Current.currentRound < 1 || Current.currentRound >= 3)
            return;

        Current.currentRound += 1;

        MarkDirty();

        if (save)
            Save();
    }

    // =============================
    // ENCERRAMENTO DE UMA SESSAO
    // =============================

    public static void CompleteSession()
    {
        if (Current == null)
            return;

        Current.status = GameSessionStatus.COMPLETED;
        MarkDirty();
        Current.completedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Save();
    }

    public static void BankruptSession()
    {
        if (Current == null)
            return;

        Current.status = GameSessionStatus.BANKRUPT;
        MarkDirty();
        Current.completedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Save();
    }
}
