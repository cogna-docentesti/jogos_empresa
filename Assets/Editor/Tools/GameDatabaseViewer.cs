using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite4Unity3d;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Janela que mostra o que esta gravado no game.db, atualizando sozinha.
///
/// Abra em: Tools > Jogos de Empresa > Ver banco (game.db)
///
/// Funciona com o jogo parado ou rodando. Durante o Play, deixe a janela
/// aberta ao lado do Simulator e acompanhe: enquanto voce passa pela D1, D2 e
/// D3, a tabela GameSessionEntity fica vazia; no Confirmar da D3 aparece uma linha.
///
/// So LE o banco (abre em modo somente leitura e fecha a cada leitura),
/// entao nao interfere no jogo.
/// </summary>
public sealed class GameDatabaseViewer : EditorWindow
{
    private const double RefreshSeconds = 1.0;

    private bool _autoRefresh = true;
    private double _lastRefresh;
    private Vector2 _scroll;

    private string _error;
    private DateTime _fileTime;
    private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
    private List<GameSessionEntity> _sessions = new List<GameSessionEntity>();
    private List<RoundResultEntity> _rounds = new List<RoundResultEntity>();

    private static string DbPath => Path.Combine(Application.persistentDataPath, "game.db");

    [MenuItem("Tools/Jogos de Empresa/Ver banco (game.db)")]
    public static void Open()
    {
        var window = GetWindow<GameDatabaseViewer>("Banco (game.db)");
        window.minSize = new Vector2(420, 300);
        window.Reload();
    }

    private void OnEnable() => EditorApplication.update += Tick;
    private void OnDisable() => EditorApplication.update -= Tick;

    private void Tick()
    {
        if (!_autoRefresh || EditorApplication.timeSinceStartup - _lastRefresh < RefreshSeconds)
            return;

        Reload();
        Repaint();
    }

    private void Reload()
    {
        _lastRefresh = EditorApplication.timeSinceStartup;
        _error = null;
        _counts.Clear();
        _sessions = new List<GameSessionEntity>();
        _rounds = new List<RoundResultEntity>();

        if (!File.Exists(DbPath))
            return;

        _fileTime = File.GetLastWriteTime(DbPath);

        try
        {
            using (var db = new SQLiteConnection(DbPath, SQLiteOpenFlags.ReadOnly))
            {
                foreach (var table in new[] { "GameSessionEntity", "RoundResultEntity", "SessionEventHistoryEntity", "LocationEntity" })
                    _counts[table] = TableExists(db, table)
                        ? db.ExecuteScalar<int>($"select count(*) from \"{table}\"")
                        : -1;

                if (_counts["GameSessionEntity"] > 0)
                    _sessions = db.Query<GameSessionEntity>("select * from GameSessionEntity");

                if (_counts["RoundResultEntity"] > 0)
                    _rounds = db.Query<RoundResultEntity>("select * from RoundResultEntity order by sessionId, round");
            }
        }
        catch (Exception e)
        {
            // Pode acontecer por um instante se o jogo estiver gravando
            // exatamente agora. A proxima leitura (1 s depois) resolve.
            _error = e.Message;
        }
    }

    private static bool TableExists(SQLiteConnection db, string table) =>
        db.ExecuteScalar<int>("select count(*) from sqlite_master where type='table' and name=?", table) > 0;

    // ─────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Arquivo", DbPath, EditorStyles.wordWrappedMiniLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            _autoRefresh = GUILayout.Toggle(_autoRefresh, "Atualizar sozinho (1 s)", GUILayout.Width(170));
            if (GUILayout.Button("Atualizar agora", GUILayout.Width(120))) Reload();
            if (GUILayout.Button("Abrir pasta", GUILayout.Width(90))) EditorUtility.RevealInFinder(DbPath);
        }

        EditorGUILayout.Space(6);
        DrawGameStatus();
        EditorGUILayout.Space(6);

        if (!File.Exists(DbPath))
        {
            EditorGUILayout.HelpBox("O arquivo game.db ainda nao existe. Ele e criado quando o jogo abre a GameScene.", MessageType.Info);
            return;
        }

        if (!string.IsNullOrEmpty(_error))
            EditorGUILayout.HelpBox("Leitura falhou agora (o jogo pode estar gravando): " + _error, MessageType.Warning);

        EditorGUILayout.LabelField($"Ultima alteracao do arquivo: {_fileTime:dd/MM HH:mm:ss}", EditorStyles.miniLabel);

        int sessions = _counts.TryGetValue("GameSessionEntity", out var s) ? s : 0;
        if (sessions == 0)
            EditorGUILayout.HelpBox("GameSessionEntity: 0 linhas. Nenhuma partida gravada.\n"
                                  + "E o esperado enquanto o jogador esta na identificacao, D1, D2 ou D3.", MessageType.Info);
        else
            EditorGUILayout.HelpBox($"GameSessionEntity: {sessions} linha(s) gravada(s).", MessageType.None);

        EditorGUILayout.LabelField("Linhas por tabela", EditorStyles.boldLabel);
        foreach (var pair in _counts)
            EditorGUILayout.LabelField("   " + pair.Key, pair.Value < 0 ? "(tabela nao existe)" : pair.Value.ToString());

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (var row in _sessions)
            DrawSession(row);

        if (_rounds.Count > 0)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("RoundResultEntity (meses)", EditorStyles.boldLabel);
            foreach (var r in _rounds)
                EditorGUILayout.LabelField($"   mes {r.round}", $"receita {r.grossRevenue:N0} · resultado {r.netResult:N0} · caixa final {r.closingCash:N0}");
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawGameStatus()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.LabelField("Jogo", "parado");
            return;
        }

        string state = GameManager.Instance != null && GameManager.Instance.StateMachine != null
            ? GameManager.Instance.StateMachine.CurrentState.ToString()
            : "(GameManager ainda nao existe nesta cena)";

        EditorGUILayout.LabelField("Jogo", "rodando · estado " + state);
        EditorGUILayout.LabelField("Sessao em memoria",
            GameSessionState.HasIncompatibleSave ? "save de versao anterior (aguardando reinicio)"
            : !GameSessionState.HasSession ? "nenhuma"
            : GameSessionState.IsPersisted ? "ja gravada no banco"
            : "so em memoria (ainda nao gravada)");
        if (GameSessionState.HasSession && GameSessionState.IsPersisted)
        {
            EditorGUILayout.LabelField("Alteracoes pendentes",
                GameSessionState.HasUnsavedChanges ? "sim (o dialogo Sair vai perguntar)" : "nao");

            // Hoje todas as telas gravam logo depois de mudar algo, entao jogando
            // normalmente o dialogo Sair nunca fica no modo "alteracoes pendentes".
            // Este botao existe so para testar esse modo a mao: regrava o mesmo
            // caixa SEM salvar. Nenhum valor muda; so a marca "pendente" liga.
            if (GUILayout.Button("Teste E-05: marcar alteracao pendente (nao muda valores)"))
                GameSessionState.SetCash(GameSessionState.Current.currentCash, false);
        }
    }

    private static void DrawSession(GameSessionEntity row)
    {
        EditorGUILayout.Space(6);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Sessao", row.sessionId, EditorStyles.boldLabel);
            Field("Aluno", row.studentName);
            Field("RA", row.studentRA);
            Field("Restaurante (nome)", row.companyName);
            Field("D1 Localizacao", row.locationZone.ToString());
            Field("D2 Restaurante", $"{row.restaurantType} · {SegmentName(row.targetSegment)}");
            Field("D3 Cardapio", MenuText(row.menuPricingJson));
            Field("Rodada / status", $"{row.currentRound} · {row.status}");
            Field("Caixa", row.currentCash.ToString("N0"));
            Field("Linha de credito", row.creditLineId);
            Field("Equipamentos", EquipmentText(row.equipmentJson));
            Field("Coerencia", string.IsNullOrEmpty(row.coherenceRating) ? "(calculada depois da equipe)" : row.coherenceRating);
            Field("Criada em", DateTimeOffset.FromUnixTimeSeconds(row.startedAt).ToLocalTime().ToString("dd/MM HH:mm:ss"));
        }
    }

    private static void Field(string label, string value) =>
        EditorGUILayout.LabelField(label, string.IsNullOrEmpty(value) ? "(vazio)" : value, EditorStyles.wordWrappedLabel);

    private static string SegmentName(Segment segment) => segment switch
    {
        Segment.LOW => "Classe C",
        Segment.MEDIUM => "Classe B",
        Segment.HIGH => "Classe A",
        _ => segment.ToString()
    };

    private static string EquipmentText(string json)
    {
        var ids = EquipmentSelectionHelper.GetIds(EquipmentSelectionHelper.FromJson(json));
        return ids.Count == 0 ? null : string.Join(", ", ids);
    }

    // ─────────────────────────────────────────────────────────────
    //  E-06: levar mudancas dos assets LOC_* para um banco que ja existe
    // ─────────────────────────────────────────────────────────────

    [MenuItem("Tools/Jogos de Empresa/Recarregar localizacoes no banco (E-06)")]
    public static void ReloadLocations()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("[Banco] Saia do Play Mode antes de recarregar as localizacoes.");
            return;
        }

        if (!File.Exists(DbPath))
        {
            Debug.Log("[Banco] game.db ainda nao existe. As localizacoes serao copiadas na primeira vez que o jogo abrir.");
            return;
        }

        using (var db = new SQLiteConnection(DbPath, SQLiteOpenFlags.ReadWrite))
        {
            db.CreateTable<LocationEntity>();
            int count = Game.Adapter.Out.Persistence.SqliteLocationRepository.CopyAssetsToDatabase(db);
            Debug.Log($"[Banco] {count} localizacoes copiadas dos assets (Resources/Locations) para o game.db.");
        }
    }

    private static string MenuText(string json)
    {
        var menu = MenuPricingHelper.FromJson(json);
        if (menu.items.Count == 0)
            return null;

        return string.Join(", ", menu.items.Select(i => $"{i.productId} R$ {i.selectedPrice:N2}"));
    }
}
