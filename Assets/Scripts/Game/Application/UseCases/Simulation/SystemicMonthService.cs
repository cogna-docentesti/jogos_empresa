// Game/Application/UseCases/Simulation/SystemicMonthService.cs
// O caso de uso "jogar um mês". É a única classe que o Controller (Unity) chama.
// Ela monta o mês, repassa os passos para o domínio e pede a gravação pela porta.
using System;
using System.Collections.Generic;
using Game.Application.Ports.Out;
using Game.Domain.Simulation;

namespace Game.Application.UseCases.Simulation
{
    public sealed class SystemicMonthService
    {
        private readonly ISimulationGateway _gateway;
        private SimulationCatalog _catalog;
        private string _sessionId;
        private MonthSimulation _month;
        private SimulationState _startState;          // cópia do estado do dia 1, gravada com o mês
        private MonthCommit _pendingCommit;           // guardado para tentar de novo sem recalcular

        public SystemicMonthService(ISimulationGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public MonthSimulation Current => _month;     // a View lê daqui (somente leitura)
        public int MonthsPerMatch => (_catalog ??= _gateway.LoadCatalog()).Constants.MonthsPerMatch;
        public bool IsRunning => _month != null && _month.Day > 0 && !_month.IsFinished;

        // ------------------------------------------------------------ planejamento
        /// O que a tela de planejamento precisa: mês, políticas gravadas e as opções do catálogo.
        public PlanningData Planning(string sessionId)
        {
            _catalog ??= _gateway.LoadCatalog();
            var active = _gateway.LoadActiveMonth(sessionId);
            if (active == null) return null;
            return new PlanningData
            {
                Month = active.Month,
                MonthsPerMatch = _catalog.Constants.MonthsPerMatch,
                Cash = active.Cash,
                Current = (active.Policies ?? new MonthPolicies()).Clone(),
                Options = _catalog.Policies,
                IsFinished = active.IsFinished,
                Errors = active.SetupErrors,
                Warnings = active.SetupWarnings,
                Changes = active.Changes
            };
        }

        public SavePoliciesResult SavePolicies(string sessionId, MonthPolicies policies)
        {
            if (IsRunning) return SavePoliciesResult.Refused("O mês está rodando; as políticas ficam travadas até o fechamento.");
            var active = _gateway.LoadActiveMonth(sessionId);
            if (active == null) return SavePoliciesResult.Refused("A partida ainda não existe (confirme a D3 primeiro).");
            if (active.IsFinished) return SavePoliciesResult.Refused("A partida já terminou.");
            _gateway.SavePolicies(sessionId, active.Month, policies);
            return SavePoliciesResult.Saved();
        }

        // ------------------------------------------------------------ mês
        /// Monta o mês a partir do que está gravado. Sair no meio e chamar Begin de novo
        /// recomeça do dia 1, com a mesma semente: os mesmos acontecimentos.
        public MonthSimulation Begin(string sessionId)
        {
            _catalog ??= _gateway.LoadCatalog();
            var active = _gateway.LoadActiveMonth(sessionId)
                         ?? throw new InvalidOperationException("Partida não encontrada: " + sessionId);
            if (active.IsFinished) throw new InvalidOperationException("A partida já terminou.");
            if (active.SetupErrors.Count > 0) throw new InvalidOperationException(string.Join(" ", active.SetupErrors));
            _sessionId = sessionId;
            _pendingCommit = null;
            _startState = active.State.Clone();
            _month = new MonthSimulation(_catalog, active.Setup, active.State, active.Policies);
            return _month;
        }

        public StepResult Step() => Require().AdvanceDay();

        public IReadOnlyList<ReactionOption> ReactionsFor(EventOccurrence occurrence) => Require().ReactionOptions(occurrence);

        public void Choose(string occurrenceId, string reactionId) => Require().Choose(occurrenceId, reactionId);

        /// Dia 30: fecha o mês, calcula a previsão e grava tudo numa transação.
        public (CommitResult result, MonthReport report) Finish()
        {
            var month = Require();
            var outcome = MonthCloser.Close(month, _sessionId);
            outcome.Report.Forecast = Forecaster.Run(_catalog, month.Setup, outcome.NextState, month.Policies);

            int monthsPerMatch = _catalog.Constants.MonthsPerMatch;
            _pendingCommit = new MonthCommit
            {
                SessionId = _sessionId,
                Month = month.Month,
                ConfigVersion = _catalog.ConfigVersion,
                DaysSimulated = month.DayResults.Count,
                Report = outcome.Report,
                NextState = outcome.NextState,
                StartState = _startState,
                Events = outcome.Report.Events,
                CashAfterClose = outcome.NextState.Cash,
                Bankrupt = outcome.Report.Bankrupt,
                MatchFinished = outcome.Report.Bankrupt || month.Month >= monthsPerMatch
            };
            return (_gateway.Commit(_pendingCommit), outcome.Report);
        }

        /// Se o Commit falhou, a tela tenta de novo com o MESMO pacote (sem recalcular o mês).
        public CommitResult RetryCommit()
        {
            if (_pendingCommit == null) throw new InvalidOperationException("Não há fechamento pendente.");
            return _gateway.Commit(_pendingCommit);
        }

        public IReadOnlyList<MonthReport> Reports(string sessionId) => _gateway.LoadReports(sessionId);

        /// Descarta o mês em memória (o aluno saiu no meio). Nada foi gravado; o próximo Begin recomeça do dia 1.
        public void Abandon()
        {
            _month = null;
            _pendingCommit = null;
        }

        private MonthSimulation Require() => _month ?? throw new InvalidOperationException("Chame Begin antes.");
    }

    public sealed class PlanningData
    {
        public int Month;
        public int MonthsPerMatch;
        public double Cash;
        public MonthPolicies Current;
        public IReadOnlyDictionary<string, List<PolicyOption>> Options;   // policyId -> opções, na ordem do catálogo
        public bool IsFinished;
        public List<string> Errors = new List<string>();      // D1 a D6 impedem começar (ex.: sem equipe)
        public List<string> Warnings = new List<string>();
        public List<string> Changes = new List<string>();     // mudanças do hub que o mês vai aplicar
        public bool CanStart => !IsFinished && Errors.Count == 0;
    }

    public sealed class SavePoliciesResult
    {
        public bool Ok;
        public string Message;
        public static SavePoliciesResult Saved() => new SavePoliciesResult { Ok = true };
        public static SavePoliciesResult Refused(string why) => new SavePoliciesResult { Ok = false, Message = why };
    }
}
