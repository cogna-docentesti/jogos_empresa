// Game/Application/Ports/Out/ISimulationGateway.cs
// A porta de saída do simulador. O serviço de aplicação só conhece esta interface.
// No jogo, quem implementa é o SqliteSimulationGateway (Adapter/Out).
// Nos testes, quem implementa é o InMemorySimulationGateway.
using System.Collections.Generic;
using Game.Domain.Simulation;

namespace Game.Application.Ports.Out
{
    public interface ISimulationGateway
    {
        /// Catálogo de números (SimulationConfig.asset + ScriptableObjects)
        SimulationCatalog LoadCatalog();

        /// Tudo o que o mês ativo precisa: decisões D1 a D6, estado salvo e políticas do mês.
        /// Retorna null se a partida ainda não existe (antes do Confirmar da D3).
        ActiveMonthData LoadActiveMonth(string sessionId);

        MonthPolicies LoadPolicies(string sessionId, int month);
        void SavePolicies(string sessionId, int month, MonthPolicies policies);

        /// Grava o fechamento inteiro numa transação. Recusa se o mês já foi fechado,
        /// se a sessão gravada não está nesse mês ou se o histórico está incompleto.
        CommitResult Commit(MonthCommit commit);

        IReadOnlyList<MonthReport> LoadReports(string sessionId);
    }

    public sealed class ActiveMonthData
    {
        public SessionSetup Setup;
        public SimulationState State;          // estado no início do mês (dia 1); null quando há erro em D1 a D6
        public MonthPolicies Policies;
        public MonthlyEventPlan EventPlan;
        public bool LegacyDailyEvents;          // only for replaying existing pre-fix journals
        public int Month;
        public double Cash;
        public bool IsFinished;                // partida encerrada (mês 3 fechado ou falência)
        public List<string> SetupErrors = new List<string>();     // impedem começar o mês
        public List<string> SetupWarnings = new List<string>();   // aparecem na tela de planejamento
        public List<string> Changes = new List<string>();         // o que o hub mudou desde o fechamento anterior
    }

    public sealed class MonthCommit
    {
        public string SessionId;
        public int Month;
        public string ConfigVersion;
        public int DaysSimulated;
        public MonthReport Report;
        public SimulationState NextState;
        public SimulationState StartState;          // estado entregue ao dia 1 (reprodução e conferência)
        public List<EventRecord> Events;
        public double CashAfterClose;
        public bool Bankrupt;
        public bool MatchFinished;
    }

    public enum CommitStatus { Ok, AlreadyClosed, WrongMonth, IncompleteHistory, StorageError }

    public sealed class CommitResult
    {
        public CommitStatus Status;
        public string Message;
        public bool Ok => Status == CommitStatus.Ok;
    }
}
