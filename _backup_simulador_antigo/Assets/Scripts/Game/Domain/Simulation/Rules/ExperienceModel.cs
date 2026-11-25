using System;

namespace Game.Domain.Simulation
{
    public sealed class SatisfactionQuery
    {
        public double WaitMinutes;
        public double StandardMinutes;
        /// <summary>Fracao da demanda perdida por falta de estoque.</summary>
        public double StockoutShare;
        public double PricePosition;
        public double Quality;
        public double ErrorRate;
        public bool UnresolvedNegativeEvent;
        public bool PremiumServiceApplies;
        public double LeverDelta;
    }

    public sealed class SatisfactionResult
    {
        public double Value;
        public double WaitTerm;
        public double StockTerm;
        public double PriceTerm;
        public double QualityTerm;
        public double ErrorTerm;
        public double EventTerm;
        public double PremiumTerm;
        public double LeverTerm;
        /// <summary>NPS do dia como na especificacao: 50 + ajustes, limitado a -100..100.</summary>
        public double Nps;
    }

    /// <summary>
    /// NPS diario da especificacao (Bloco 8): 50 + tempo de atendimento + estoque + preco
    /// + qualidade - erros - evento nao resolvido + servico premium, limitado a -100..100.
    /// A satisfacao (0..100) e o mesmo valor limitado a 0..100: 50 = neutro.
    ///
    /// A reputacao segue a satisfacao com atraso: R(t+1) = R(t) + taxa x (S(t) - R(t)).
    /// Com taxa 0,035 a reputacao percorre cerca de 65% da distancia em um mes.
    /// </summary>
    public static class ExperienceModel
    {
        public static SatisfactionResult DailySatisfaction(SatisfactionQuery q, ExperienceParameters p)
        {
            var r = new SatisfactionResult();
            double standard = Math.Max(1d, q.StandardMinutes);
            if (q.WaitMinutes <= standard) r.WaitTerm = p.waitWithinStandardBonus;
            else
            {
                double over = SimMath.Clamp01((q.WaitMinutes - standard) / standard);
                r.WaitTerm = SimMath.Lerp(0d, -p.waitOverStandardMaxPenalty, over);
            }

            r.StockTerm = q.StockoutShare <= 0.0001d ? p.noStockoutBonus : -p.stockoutPenalty * SimMath.Clamp01(q.StockoutShare / 0.1d);

            bool highPrice = q.PricePosition > p.highPricePosition;
            if (!highPrice) r.PriceTerm = p.fairPriceBonus;
            else if (q.Quality < p.qualityThresholdForHighPrice) r.PriceTerm = -p.highPriceWithoutQualityPenalty;

            r.QualityTerm = (q.Quality - 0.5d) * p.qualitySatisfactionScale;
            r.ErrorTerm = -q.ErrorRate * p.errorPenaltyScale;
            r.EventTerm = q.UnresolvedNegativeEvent ? -p.unresolvedNegativeEventPenalty : 0d;
            r.PremiumTerm = q.PremiumServiceApplies ? p.premiumServiceBonus : 0d;
            r.LeverTerm = q.LeverDelta;
            double raw = p.baseSatisfaction + r.WaitTerm + r.StockTerm + r.PriceTerm + r.QualityTerm
                         + r.ErrorTerm + r.EventTerm + r.PremiumTerm + r.LeverTerm;
            r.Nps = SimMath.Clamp(raw, -100d, 100d);
            r.Value = SimMath.Clamp(raw, 0d, 100d);
            return r;
        }

        public static double Smooth(double previous, double today, double weight)
        {
            return previous + SimMath.Clamp01(weight) * (today - previous);
        }

        public static double ReputationStep(double reputation, double satisfactionToday, double rate)
        {
            return SimMath.Clamp(reputation + rate * (satisfactionToday - reputation), 0d, 100d);
        }
    }
}
