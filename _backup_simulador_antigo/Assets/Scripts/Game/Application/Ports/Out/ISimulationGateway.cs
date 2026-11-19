using System.Collections.Generic;
using Game.Domain.Simulation;

namespace Game.Application.Ports.Out
{
    /// <summary>Resultado da gravacao do fechamento.</summary>
    public sealed class MonthCommitResult
    {
        public bool Success;
        public string Error;
        public bool Bankrupt;
        public bool Completed;
        public int NextMonth;
    }

    /// <summary>
    /// Porta de saida (hexagonal): tudo que o caso de uso precisa do mundo de fora.
    /// O adaptador SQLite/Unity implementa; os testes usam uma versao em memoria.
    /// </summary>
    public interface ISimulationGateway
    {
        SimulationCatalog LoadCatalog();

        /// <summary>Entradas do mes ativo da sessao atual (caixa, decisoes, estado dinamico). null se nao houver mes ativo.</summary>
        MonthInputs LoadActiveMonth(SimulationCatalog catalog, out string error);

        /// <summary>Grava, numa unica transacao, o resultado do mes e a sessao avancada.</summary>
        MonthCommitResult Commit(MonthInputs inputs, MonthOutcome outcome);

        IDictionary<string, string> LoadPolicies(string sessionId);

        /// <summary>Grava as politicas de gestao escolhidas no hub (vale a partir do proximo mes simulado).</summary>
        void SavePolicies(string sessionId, IDictionary<string, string> policies);

        /// <summary>Relatorios ja gravados da sessao, em ordem de mes (relatorio final e historico).</summary>
        IReadOnlyList<MonthlyReport> LoadReports(string sessionId);
    }
}
