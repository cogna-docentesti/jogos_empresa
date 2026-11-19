using System;
using System.Collections.Generic;
using Game.Application.Ports.Out;
using Game.Domain.Simulation;

namespace Game.Application.UseCases.Simulation
{
    /// <summary>
    /// Caso de uso "simular o mes". Liga a tela ao motor sem conhecer Unity nem SQLite.
    ///
    /// Regra de persistencia: nada e gravado durante o mes. O fechamento grava tudo de uma vez.
    /// Se o jogo fechar no meio do mes, o mes recomeca no proximo acesso; a semente e fixa por
    /// sessao, entao os mesmos sorteios se repetem e so as decisoes do aluno mudam o resultado.
    /// </summary>
    public sealed class SystemicMonthService
    {
        private readonly ISimulationGateway _gateway;
        private SimulationCatalog _catalog;
        private MonthInputs _inputs;

        public MonthSimulation Simulation { get; private set; }
        public MonthOutcome LastOutcome { get; private set; }
        public MonthCommitResult LastCommit { get; private set; }
        public string LastError { get; private set; }
        public int OutlookRuns { get; set; } = 40;

        public SystemicMonthService(ISimulationGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public bool IsRunning => Simulation != null && Simulation.Status != SimulationStatus.Finished;
        public bool IsAwaitingDecision => Simulation != null && Simulation.Status == SimulationStatus.AwaitingDecision;
        public bool IsFinished => Simulation != null && Simulation.Status == SimulationStatus.Finished;

        /// <summary>Monta a simulacao do mes ativo. Retorna false (com LastError) se nao for possivel.</summary>
        public bool Begin()
        {
            LastError = null;
            LastOutcome = null;
            LastCommit = null;
            try
            {
                _catalog = _gateway.LoadCatalog();
                var problems = _catalog.Validate();
                if (problems.Count > 0)
                {
                    LastError = "Configuracao invalida: " + string.Join(" | ", problems);
                    return false;
                }
                _inputs = _gateway.LoadActiveMonth(_catalog, out string error);
                if (_inputs == null)
                {
                    LastError = error ?? "Nao ha mes ativo para simular.";
                    return false;
                }
                Simulation = new MonthSimulation(_catalog, _inputs);
                return true;
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Simulation = null;
                return false;
            }
        }

        /// <summary>Avanca um dia (ou ate a proxima decisao). Retorna o dia concluido, ou null se parou para decidir.</summary>
        public DayResult Step()
        {
            if (Simulation == null) throw new InvalidOperationException("Chame Begin antes de Step.");
            if (Simulation.Status == SimulationStatus.Finished) return null;
            return Simulation.Advance();
        }

        public void Choose(string occurrenceId, string reactionId)
        {
            if (Simulation == null) throw new InvalidOperationException("Nao ha simulacao em andamento.");
            Simulation.Resolve(occurrenceId, reactionId);
        }

        /// <summary>Fecha o mes, calcula a previsao e grava. Retorna o relatorio mesmo se a gravacao falhar.</summary>
        public MonthlyReport Finish(bool withOutlook = true)
        {
            if (Simulation == null || Simulation.Status != SimulationStatus.Finished)
                throw new InvalidOperationException("O mes ainda nao terminou.");
            if (LastOutcome != null) return LastOutcome.Report;

            var outcome = Simulation.Close();
            if (withOutlook && OutlookRuns > 0)
            {
                try
                {
                    var next = _inputs.Clone();
                    next.Month = _inputs.Month + 1;
                    next.State = outcome.NextState.Clone();
                    next.OpeningCash = outcome.Report.cashResult.closingCash;
                    next.OpeningReputation = outcome.Report.cashResult.reputationAtEnd;
                    next.LoanBalance = outcome.NextLoanBalance;
                    next.ConsecutiveNegativeMonths = outcome.ConsecutiveNegativeMonths;
                    next.PreviousReport = outcome.Report;
                    outcome.Report.outlook = OutlookEstimator.Estimate(_catalog, next, outcome.Report.balance.Equity, OutlookRuns);
                }
                catch (Exception exception)
                {
                    outcome.Report.diagnostics.Add("Previsao indisponivel: " + exception.Message);
                }
            }
            LastOutcome = outcome;
            LastCommit = _gateway.Commit(_inputs, outcome);
            if (!LastCommit.Success) LastError = LastCommit.Error;
            return outcome.Report;
        }

        /// <summary>Tenta gravar de novo um fechamento que falhou (ex.: banco ocupado).</summary>
        public MonthCommitResult RetryCommit()
        {
            if (LastOutcome == null) throw new InvalidOperationException("Nao ha fechamento pendente.");
            if (LastCommit != null && LastCommit.Success) return LastCommit;
            LastCommit = _gateway.Commit(_inputs, LastOutcome);
            LastError = LastCommit.Success ? null : LastCommit.Error;
            return LastCommit;
        }

        /// <summary>Descarta o mes em memoria (ex.: aluno saiu no meio do mes).</summary>
        public void Abandon()
        {
            Simulation = null;
            LastOutcome = null;
            LastCommit = null;
        }

        public SimulationCatalog Catalog => _catalog ??= _gateway.LoadCatalog();

        /// <summary>Politicas de gestao disponiveis no hub (marketing, manutencao, compras...).</summary>
        public IReadOnlyList<PolicyCategory> PolicyCategories() => Catalog.Config.policies;

        /// <summary>Escolhas atuais: o que foi salvo, completando com a opcao padrao de cada categoria.</summary>
        public Dictionary<string, string> CurrentPolicies(string sessionId)
        {
            var saved = _gateway.LoadPolicies(sessionId) ?? new Dictionary<string, string>();
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var category in Catalog.Config.policies)
            {
                string option = saved.TryGetValue(category.id, out string chosen) && category.Option(chosen) != null
                    ? chosen
                    : category.defaultOptionId;
                if (!string.IsNullOrEmpty(option)) result[category.id] = option;
            }
            return result;
        }

        /// <summary>Valida e grava as politicas. Valem a partir do proximo mes simulado.</summary>
        public void SavePolicies(string sessionId, IDictionary<string, string> policies)
        {
            if (string.IsNullOrEmpty(sessionId)) throw new InvalidOperationException("Sessao sem id.");
            if (IsRunning) throw new InvalidOperationException("As politicas so mudam entre um mes e outro.");
            var clean = new Dictionary<string, string>(StringComparer.Ordinal);
            if (policies != null)
            {
                foreach (var pair in policies)
                {
                    var category = Catalog.Config.Policy(pair.Key)
                                   ?? throw new InvalidOperationException("Politica desconhecida: " + pair.Key);
                    if (category.Option(pair.Value) == null)
                        throw new InvalidOperationException("Opcao " + pair.Value + " nao existe na politica " + category.displayName + ".");
                    clean[pair.Key] = pair.Value;
                }
            }
            _gateway.SavePolicies(sessionId, clean);
        }

        /// <summary>Custo mensal fixo das politicas escolhidas (para a tela mostrar antes de simular).</summary>
        public double MonthlyPolicyCost(IDictionary<string, string> policies)
        {
            double total = 0d;
            if (policies == null) return total;
            foreach (var pair in policies)
                total += Catalog.Config.Policy(pair.Key)?.Option(pair.Value)?.monthlyCost ?? 0d;
            return total;
        }

        public IReadOnlyList<MonthlyReport> Reports(string sessionId) => _gateway.LoadReports(sessionId) ?? new List<MonthlyReport>();

        public BusinessModelCanvas Canvas()
        {
            var inputs = _inputs ?? _gateway.LoadActiveMonth(Catalog, out _);
            return inputs == null ? null : BusinessModelCanvasBuilder.Build(Catalog, inputs, inputs.PreviousReport);
        }
    }
}
