using System;

namespace Game.Domain.Simulation
{
    /// <summary>
    /// Regra de encerramento da empresa.
    ///
    /// LEGACY_CASH_RESULT: regra atual do MonthlySessionSettlement. Tres meses seguidos com
    /// resultado de caixa negativo. Como a amortizacao do emprestimo entra no resultado de caixa,
    /// uma empresa lucrativa que paga um emprestimo grande em tres parcelas "quebra" com caixa positivo.
    ///
    /// ECONOMIC (padrao do motor sistemico): tres meses seguidos com prejuizo liquido na DRE,
    /// ou caixa no fechamento abaixo do limite do cheque especial (insolvencia).
    /// </summary>
    public static class BankruptcyRule
    {
        public const string LegacyCashResult = "LEGACY_CASH_RESULT";
        public const string Economic = "ECONOMIC";

        public static int NextConsecutive(string rule, int previous, MonthlyReport report)
        {
            bool negative = rule == LegacyCashResult ? report.cashResult.netResult < 0d : report.income.netIncome < 0d;
            return negative ? previous + 1 : 0;
        }

        public static bool IsBankrupt(string rule, int consecutive, MonthlyReport report, FinanceParameters finance)
        {
            if (consecutive >= Math.Max(1, finance.bankruptcyConsecutiveMonths)) return true;
            return rule != LegacyCashResult && report.cashFlow.closingCash < -finance.overdraftLimit;
        }
    }
}
