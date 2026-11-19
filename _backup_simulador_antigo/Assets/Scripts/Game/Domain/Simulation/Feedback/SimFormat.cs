using System;
using System.Globalization;

namespace Game.Domain.Simulation
{
    /// <summary>
    /// Formatacao em portugues do Brasil sem depender da cultura instalada no aparelho
    /// (algumas builds Android nao trazem pt-BR). "R$ 1.234,56", "18%", "2,5 dias".
    /// </summary>
    public static class SimFormat
    {
        public static string Money(double value, bool decimals = true)
        {
            string sign = value < 0d ? "-" : string.Empty;
            string text = Math.Abs(value).ToString(decimals ? "#,##0.00" : "#,##0", CultureInfo.InvariantCulture);
            return sign + "R$ " + Swap(text);
        }

        public static string SignedMoney(double value)
        {
            return (value >= 0d ? "+ " : "- ") + Money(Math.Abs(value));
        }

        public static string Number(double value, int decimals = 0)
        {
            string format = decimals <= 0 ? "#,##0" : "#,##0." + new string('0', decimals);
            return Swap(value.ToString(format, CultureInfo.InvariantCulture));
        }

        public static string Percent(double percentValue, int decimals = 0)
        {
            return Number(percentValue, decimals) + "%";
        }

        /// <summary>Probabilidade 0..1 como percentual com uma casa quando pequena.</summary>
        public static string Probability(double probability)
        {
            double pct = probability * 100d;
            return pct < 10d ? Number(pct, 1) + "%" : Number(pct, 0) + "%";
        }

        private static readonly string[] MonthNames =
        {
            "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
            "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"
        };

        /// <summary>Nome do mes do calendario para o mes N da partida ("Março" para o mes 3 quando o ciclo comeca em janeiro).</summary>
        public static string CalendarMonthName(int gameMonth, int firstCalendarMonth = 1)
        {
            int index = ((Math.Max(1, firstCalendarMonth) - 1 + Math.Max(1, gameMonth) - 1) % 12 + 12) % 12;
            return MonthNames[index];
        }

        private static string Swap(string invariant)
        {
            var chars = invariant.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == ',') chars[i] = '.';
                else if (chars[i] == '.') chars[i] = ',';
            }
            return new string(chars);
        }
    }
}
