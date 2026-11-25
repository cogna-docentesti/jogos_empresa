using System;

namespace Game.Domain.Simulation
{
    /// <summary>Funcoes matematicas pequenas usadas pelos modelos. Todas puras.</summary>
    public static class SimMath
    {
        public static double Clamp(double value, double min, double max) => value < min ? min : (value > max ? max : value);

        public static double Clamp01(double value) => Clamp(value, 0d, 1d);

        /// <summary>
        /// Converte uma taxa de risco (hazard, ocorrencias esperadas por dia) em
        /// probabilidade de ao menos uma ocorrencia no intervalo: p = 1 - e^(-taxa x dias).
        /// Fica sempre entre 0 e 1, qualquer que seja a taxa nao negativa.
        /// </summary>
        public static double HazardToProbability(double ratePerDay, double days = 1d)
        {
            if (ratePerDay <= 0d || days <= 0d) return 0d;
            return 1d - Math.Exp(-ratePerDay * days);
        }

        /// <summary>Inverso: taxa diaria que produz a probabilidade p em "days" dias.</summary>
        public static double ProbabilityToHazard(double probability, double days = 1d)
        {
            probability = Clamp(probability, 0d, 0.999999d);
            if (probability <= 0d || days <= 0d) return 0d;
            return -Math.Log(1d - probability) / days;
        }

        public static double Logistic(double x) => 1d / (1d + Math.Exp(-x));

        public static double Lerp(double a, double b, double t) => a + (b - a) * Clamp01(t);

        public static double SafeDivide(double numerator, double denominator, double fallback = 0d)
            => Math.Abs(denominator) < 1e-12 ? fallback : numerator / denominator;

        public static double Money(double value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        public static void RequireFinite(double value, string label)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException(label + " precisa ser um numero finito.");
        }
    }
}
