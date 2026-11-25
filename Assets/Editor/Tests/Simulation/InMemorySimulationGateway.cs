// Assets/Editor/Tests/Simulation/InMemorySimulationGateway.cs
// Implementação da porta ISimulationGateway em memória. Serve para os testes EditMode:
// o motor inteiro roda sem cena, sem SQLite e sem ScriptableObject.
// Ela aplica as MESMAS regras de recusa do Commit que o SqliteSimulationGateway aplica.
using System.Collections.Generic;
using System.Linq;
using Game.Application.Ports.Out;
using Game.Domain.Simulation;

public sealed class InMemorySimulationGateway : ISimulationGateway
{
    private readonly SimulationCatalog _catalog;
    private readonly Dictionary<string, SessionSetup> _setups = new Dictionary<string, SessionSetup>();
    private readonly Dictionary<string, SimulationState> _states = new Dictionary<string, SimulationState>();
    private readonly Dictionary<(string, int), MonthPolicies> _policies = new Dictionary<(string, int), MonthPolicies>();
    private readonly Dictionary<string, List<MonthReport>> _reports = new Dictionary<string, List<MonthReport>>();
    private readonly HashSet<string> _finished = new HashSet<string>();
    public bool FailNextCommit;                                   // para testar o RetryCommit

    public InMemorySimulationGateway(SimulationCatalog catalog) { _catalog = catalog; }

    /// Equivale ao Confirmar da D3 + telas D4 a D6 já gravadas
    public void CreateSession(SessionSetup setup)
    {
        _setups[setup.SessionId] = setup;
        _states[setup.SessionId] = InitialStateFactory.Create(_catalog, setup);
        _reports[setup.SessionId] = new List<MonthReport>();
    }

    public SimulationCatalog LoadCatalog() => _catalog;

    /// Simula uma mudança feita no hub entre os meses (RH, Loja, Banco): altera as decisões gravadas.
    public void ChangeSetup(string sessionId, System.Action<SessionSetup> change) => change(_setups[sessionId]);

    public ActiveMonthData LoadActiveMonth(string sessionId)
    {
        if (!_setups.TryGetValue(sessionId, out var setup)) return null;
        var (errors, warnings) = SetupRules.Validate(_catalog, setup);
        var data = new ActiveMonthData
        {
            Setup = setup, SetupErrors = errors, SetupWarnings = warnings, IsFinished = _finished.Contains(sessionId)
        };
        if (errors.Count == 0)
        {
            data.State = _states[sessionId].Clone();
            if (_reports[sessionId].Count > 0)                       // mesmas regras do SqliteSimulationGateway
                data.Changes.AddRange(SetupReconciler.Apply(_catalog, setup, data.State));
        }
        data.Month = data.State?.Month ?? _reports[sessionId].Count + 1;
        data.Cash = data.State?.Cash ?? 0;
        data.Policies = LoadPolicies(sessionId, data.Month);
        return data;
    }

    public MonthPolicies LoadPolicies(string sessionId, int month)
        => _policies.TryGetValue((sessionId, month), out var p) ? p.Clone()
         : month > 1 && _policies.TryGetValue((sessionId, month - 1), out var prev) ? prev.Clone()   // repete o mês anterior
         : new MonthPolicies();                                                                     // padrões do GDD

    public void SavePolicies(string sessionId, int month, MonthPolicies policies) => _policies[(sessionId, month)] = policies.Clone();

    public CommitResult Commit(MonthCommit c)
    {
        if (FailNextCommit) { FailNextCommit = false; return new CommitResult { Status = CommitStatus.StorageError, Message = "falha simulada" }; }
        if (_reports[c.SessionId].Any(r => r.Month == c.Month)) return new CommitResult { Status = CommitStatus.AlreadyClosed };
        if (_states[c.SessionId].Month != c.Month) return new CommitResult { Status = CommitStatus.WrongMonth };
        if (c.DaysSimulated != _catalog.Constants.DaysPerMonth) return new CommitResult { Status = CommitStatus.IncompleteHistory };

        // "transação": só troca tudo depois de validar tudo
        _reports[c.SessionId].Add(c.Report);
        _states[c.SessionId] = c.NextState.Clone();
        if (c.MatchFinished) _finished.Add(c.SessionId);
        return new CommitResult { Status = CommitStatus.Ok };
    }

    public IReadOnlyList<MonthReport> LoadReports(string sessionId) => _reports[sessionId];
}
