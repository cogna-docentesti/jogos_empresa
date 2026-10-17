using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Adapter.In.UI
{
    /// <summary>Read-only explanation of a persisted ending, never a new bankruptcy calculation.</summary>
    public static class BankruptcyPresentation
    {
        public sealed class Report
        {
            public string Company, Reason, Counter, Period, Net, Cash, Reputation, History;
            public QuarterlyResultPresentation.Report Quarter;
            public int LastMonth;
        }
        public static Report Build(GameSessionEntity session, IReadOnlyList<RoundResultEntity> rows)
        {
            if (session == null || session.status != GameSessionStatus.BANKRUPT)
                throw new InvalidOperationException("A saved bankruptcy is required.");
            var quarter = QuarterlyResultPresentation.Build(session.sessionId, rows);
            var last = quarter.Months.LastOrDefault(r => r != null);
            bool confirmed = quarter.Count == 3 && quarter.Months.All(r => r.netResult < 0);
            return new Report
            {
                Company = string.IsNullOrWhiteSpace(session.companyName) ? "Nome do restaurante não registrado" : session.companyName,
                Reason = confirmed
                    ? "A campanha foi encerrada porque os três meses consecutivos tiveram prejuízo no resultado de caixa."
                    : "A campanha está registrada como encerrada por falência. A regra é ter três meses consecutivos de prejuízo no resultado de caixa; os resultados disponíveis não permitem conferir toda essa sequência.",
                Counter = session.consecutiveNegativeRounds + " meses consecutivos de prejuízo registrados",
                Period = "ENCERRAMENTO • MÊS " + session.currentRound + " DE 3",
                Net = last == null ? "Não disponível" : MonthlyResultPresentation.Money(last.netResult),
                Cash = last == null ? "Não disponível" : MonthlyResultPresentation.Money(last.closingCash),
                Reputation = last == null ? "Reputação final não disponível" : "Reputação no último mês salvo: " + last.reputationAtEnd + " / 100",
                History = confirmed ? "Os três resultados salvos confirmam a sequência. Consulte cada mês para rever receitas, saídas e efeitos dos eventos."
                    : "Histórico parcial ou divergente: " + quarter.Count + " de 3 meses salvos. Ausências não são valores zero. Nenhum resultado foi recalculado.",
                Quarter = quarter, LastMonth = last?.round ?? 0
            };
        }
    }
}
