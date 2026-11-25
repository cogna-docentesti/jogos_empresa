// Assets/Editor/Simulation/ConferenciaJogada.cs
// Menu Game > Simulação > Conferir partida jogada (game.db) e exportar CSV.
//
// Lê do game.db os meses que foram jogados no Play, reproduz cada mês no motor com as mesmas
// decisões (estado do dia 1, políticas e reações gravadas; a semente vem da sessão) e:
//   1. passa o mês reproduzido pela Conferencia (todas as fórmulas do dia e do fechamento);
//   2. compara o mês reproduzido com o que ficou gravado (relatório e tela de resultado);
//   3. grava Conferencia/P1_Partida_jogada_no_Play.csv, no mesmo layout dos cenários, para a planilha.
// Funciona com o jogo parado (abre o game.db só para leitura) ou durante o Play (usa a conexão do jogo).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Adapter.Out.Simulation;
using Game.Domain.Simulation;
using SQLite4Unity3d;
using UnityEditor;
using UnityEngine;

public static class ConferenciaJogada
{
    public const string FileName = "P1_Partida_jogada_no_Play.csv";

    [MenuItem("Game/Simulação/Conferir partida jogada (game.db) e exportar CSV")]
    public static void FromMenu()
    {
        var config = AssetDatabase.LoadAssetAtPath<SimulationConfigAsset>(SimulationConfigBuilder.AssetPath);
        if (config == null) { Debug.LogError("[Conferência] Crie o SimulationConfig antes."); return; }
        var provider = new UnitySimulationCatalogProvider(config);
        var catalog = provider.Build();

        SQLiteConnection db;
        bool own = false;
        if (EditorApplication.isPlaying && DatabaseInitializer.DatabaseService?.Connection != null)
            db = DatabaseInitializer.DatabaseService.Connection;
        else
        {
            string path = Path.Combine(UnityEngine.Application.persistentDataPath, "game.db");
            if (!File.Exists(path)) { Debug.LogError("[Conferência] game.db não encontrado em " + path); return; }
            db = new SQLiteConnection(path, SQLiteOpenFlags.ReadOnly);
            own = true;
        }
        try
        {
            string result = Run(db, catalog, provider, ConferenciaExport.Folder, GameSessionState.Current?.sessionId);
            Debug.Log("[Conferência] " + result);
        }
        catch (Exception ex) { Debug.LogError("[Conferência] Falha ao conferir a partida jogada: " + ex); }
        finally { if (own) db.Close(); }
    }

    public static string Run(SQLiteConnection db, SimulationCatalog cat, UnitySimulationCatalogProvider provider, string folder, string preferredSessionId)
    {
        var months = db.Table<SimulationMonthEntity>().ToList();
        string sessionId = preferredSessionId != null && months.Any(m => m.sessionId == preferredSessionId)
            ? preferredSessionId
            : months.OrderByDescending(m => m.closedAt).Select(m => m.sessionId).FirstOrDefault();
        if (sessionId == null) return "Nenhum mês fechado no game.db. Jogue um mês no Play antes.";

        var session = db.Table<GameSessionEntity>().FirstOrDefault(s => s.sessionId == sessionId);
        if (session == null) return "Sessão " + sessionId + " não encontrada.";
        var setup = SessionSetupMapper.FromEntity(session, cat, provider);
        var stateRow = db.Table<SimulationStateEntity>().FirstOrDefault(s => s.sessionId == sessionId);
        var savedPolicies = stateRow != null && !string.IsNullOrEmpty(stateRow.policiesJson)
            ? JsonUtility.FromJson<MonthPolicies>(stateRow.policiesJson) : new MonthPolicies();
        var screens = db.Table<RoundResultEntity>().Where(r => r.sessionId == sessionId).ToList();

        var notes = new List<string>();
        var runs = new List<ConferenciaRunner.MonthRun>();
        SimulationState carried = null;
        foreach (var row in months.Where(m => m.sessionId == sessionId).OrderBy(m => m.month))
        {
            var stored = JsonUtility.FromJson<MonthReport>(row.reportJson);

            // estado do dia 1: gravado com o mês; em meses antigos, reconstruído
            SimulationState start;
            if (!string.IsNullOrEmpty(row.startStateJson)) start = JsonUtility.FromJson<SimulationState>(row.startStateJson);
            else if (row.month == 1)
            {
                double endLoan = stored.Balance.FirstOrDefault(v => v.Name == "Empréstimos")?.Value ?? 0;
                double amortization = -(stored.CashFlow.FirstOrDefault(v => v.Name == nameof(CashLine.Amortization))?.Value ?? 0);
                setup.LoanAmount = endLoan + amortization;                // o banco guarda o saldo de hoje, não o contratado
                start = InitialStateFactory.Create(cat, setup);
                notes.Add("mês 1: estado do dia 1 reconstruído das decisões D1 a D6 (mês gravado antes do campo startStateJson)");
            }
            else if (carried != null)
            {
                start = carried.Clone();
                setup.LoanAmount = start.LoanBalance;                     // sem empréstimo novo: o hub não mostra esse histórico
                SetupReconciler.Apply(cat, setup, start);
                notes.Add($"mês {row.month}: estado do dia 1 = fechamento reproduzido do mês anterior + mudanças do hub de hoje");
            }
            else { notes.Add($"mês {row.month}: sem estado do dia 1, não reproduzido"); continue; }

            MonthPolicies policies;
            if (stored.HasPolicies && stored.Policies != null) policies = stored.Policies;
            else if (stateRow != null && stateRow.policiesMonth == row.month) { policies = savedPolicies; notes.Add($"mês {row.month}: políticas do planejamento gravado"); }
            else
            {
                policies = FindPolicies(cat, setup, start, stored);
                if (policies == null) { notes.Add($"mês {row.month}: políticas não gravadas e nenhuma combinação reproduz o mês; não conferido"); carried = null; continue; }
                notes.Add($"mês {row.month}: políticas não gravadas (mês antigo), encontradas pela reprodução dia a dia: " + Describe(policies));
            }

            var sim = new MonthSimulation(cat, setup, start.Clone(), policies, stored.EventPlan);
            Replay(sim, stored, cat, 0, msg => notes.Add($"mês {row.month} {msg}"));
            var outcome = MonthCloser.Close(sim, sessionId);
            var report = outcome.Report;
            var checks = Conferencia.Month(sim, report, start);

            // o mês reproduzido tem de ser o mesmo que ficou gravado no Play
            void G(string item, double gravado, double reproduzido, double extra = 0)
                => checks.Add(new CheckLine { Item = "Gravado no Play · " + item, Motor = gravado, Expected = reproduzido, ExtraTolerance = extra });
            foreach (var line in stored.Dre) G("DRE " + line.Name, line.Value, report.Dre.First(v => v.Name == line.Name).Value);
            foreach (var line in stored.CashFlow) G("Caixa " + line.Name, line.Value, report.CashFlow.First(v => v.Name == line.Name).Value);
            G("Lucro líquido", stored.NetIncome, report.NetIncome);
            G("Resultado de caixa", stored.CashResult, report.CashResult);
            G("Pedidos", stored.Orders, report.Orders);
            G("Pontuação", stored.Score, report.Score);
            var screen = screens.FirstOrDefault(r => r.round == row.month);
            if (screen != null)
            {
                var again = RoundResultMapper.From(report, outcome.NextState.Cash, sessionId, null, 0);
                G("Tela · Receita de vendas", screen.grossRevenue, again.grossRevenue, 0.02);
                G("Tela · Insumos", screen.supplyCost, again.supplyCost, 0.02);
                G("Tela · Aluguel", screen.rent, again.rent, 0.02);
                G("Tela · Folha", screen.salaries, again.salaries, 0.02);
                G("Tela · Despesas operacionais", screen.utilities, again.utilities, 0.02);
                G("Tela · Parcela", screen.loanPayment, again.loanPayment, 0.02);
                G("Tela · Impacto dos eventos", screen.eventCashImpact, again.eventCashImpact, 0.02);
                G("Tela · Resultado do mês", screen.netResult, again.netResult, 0.02);
                G("Tela · Caixa no início", screen.openingCash, again.openingCash, 0.02);
                G("Tela · Caixa no fechamento", screen.closingCash, again.closingCash, 0.02);
            }
            else notes.Add($"mês {row.month}: RoundResultEntity não encontrado");

            runs.Add(new ConferenciaRunner.MonthRun { Month = row.month, Sim = sim, Report = report, Start = start, Checks = checks });
            carried = outcome.NextState;
        }
        if (runs.Count == 0) return "Nenhum mês pôde ser reproduzido. " + string.Join(" | ", notes);

        var cenario = new ConferenciaExport.Cenario
        {
            nome = $"Partida jogada no Play ({setup.Restaurant}, {setup.RegionId})", semente = setup.Seed, regiao = setup.RegionId,
            restaurante = setup.Restaurant.ToString(), publico = setup.TargetSegment.ToString(), meses = runs.Count
        };
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, FileName), ConferenciaExport.Csv(cat, cenario, setup, runs), new UTF8Encoding(true));
        int total = runs.Sum(r => r.Checks.Count), bad = runs.Sum(r => r.Checks.Count(c => !c.Ok));
        foreach (var c in runs.SelectMany(r => r.Checks.Where(x => !x.Ok).Select(x => $"mês {r.Month} {x}")).Take(15)) Debug.LogWarning("[Conferência] " + c);
        return $"Partida {sessionId}: {runs.Count} mês(es) reproduzido(s), {total} conferências, {bad} divergências → {FileName}"
               + (notes.Count > 0 ? ". Observações: " + string.Join(" | ", notes) : "");
    }

    /// Roda o mês escolhendo as reações gravadas. maxDay > 0: para no primeiro dia diferente do gravado (busca).
    private static bool Replay(MonthSimulation sim, MonthReport stored, SimulationCatalog cat, int checkDays, Action<string> note)
    {
        var recorded = stored.Events.GroupBy(e => (e.EventId, e.AbsDay)).ToDictionary(g => g.Key, g => new Queue<EventRecord>(g));
        while (!sim.IsFinished)
        {
            sim.AdvanceDay();
            while (sim.IsAwaitingDecision)
            {
                var occ = sim.PendingDecisions[0];
                int abs = Days.Abs(occ.Month, occ.Day, cat.Constants.DaysPerMonth);
                var enabled = sim.ReactionOptions(occ).Where(o => o.Enabled).ToList();
                string reaction = recorded.TryGetValue((occ.Definition.Id, abs), out var q) && q.Count > 0 ? q.Dequeue().ReactionId : null;
                if (reaction == null || enabled.All(o => o.Reaction.Id != reaction))
                {
                    if (checkDays > 0) return false;
                    note?.Invoke($"dia {occ.Day}: evento {occ.Definition.Id} não bate com o gravado");
                    reaction = enabled[0].Reaction.Id;
                }
                sim.Choose(occ.OccurrenceId, reaction);
            }
            if (checkDays > 0 && sim.DayResults.Count > 0)
            {
                var d = sim.DayResults[sim.DayResults.Count - 1];
                var s = stored.Days.FirstOrDefault(x => x.Day == d.Day);
                if (s == null || Differs(s.Served, d.Serve.Served) || Differs(s.Revenue, d.Revenue) || Differs(s.CashEnd, d.CashEnd))
                    return false;
            }
        }
        return true;
    }

    /// Mês gravado antes de o relatório guardar as políticas: procura a combinação que reproduz os 30 dias gravados.
    private static MonthPolicies FindPolicies(SimulationCatalog cat, SessionSetup setup, SimulationState start, MonthReport stored)
    {
        var ids = MonthPolicies.PolicyIds;
        var options = ids.Select(id => cat.Policies[id].Select(o => o.OptionId).ToList()).ToList();
        var index = new int[ids.Length];
        while (true)
        {
            var candidate = new MonthPolicies();
            for (int i = 0; i < ids.Length; i++) candidate.Set(ids[i], options[i][index[i]]);
            var sim = new MonthSimulation(cat, setup, start.Clone(), candidate, stored.EventPlan);
            if (Replay(sim, stored, cat, 1, null)) return candidate;
            int k = 0;
            while (k < ids.Length && ++index[k] == options[k].Count) { index[k] = 0; k++; }
            if (k == ids.Length) return null;
        }
    }

    private static bool Differs(double gravado, double reproduzido) => Math.Abs(gravado - reproduzido) > 1e-4 + 1e-9 * Math.Abs(reproduzido);

    private static string Describe(MonthPolicies p) => string.Join(", ", p.All().Select(x => x.policy + "=" + x.option));
}
