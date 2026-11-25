using System;
using System.Linq;

namespace Game.Domain.Simulation
{
    /// <summary>Decomposicao do risco de falha de uma unidade, usada nos testes e no feedback.</summary>
    public sealed class HazardBreakdown
    {
        public double BaseRatePerDay;
        public double TierRatio = 1d;
        public double AgeRatio = 1d;
        public double UsageRatio = 1d;
        public double HistoryRatio = 1d;
        public double MaintenanceGapRatio = 1d;
        public double PolicyRatio = 1d;
        public double UnitRatio = 1d;
        public double ManagementRatio = 1d;
        public double CombinedRatio = 1d;
        public double RatePerDay;
        public double DailyProbability;
        public double MonthlyProbability;
    }

    /// <summary>
    /// Confiabilidade de equipamentos por riscos proporcionais (modelo de hazard):
    ///
    ///   taxa_dia = (falhas_mes_categoria / 30) x HR_nivel x HR_idade x HR_uso x HR_historico
    ///              x HR_lacuna_manutencao x HR_politica x HR_unidade x HR_gestao
    ///   P(falha no dia) = 1 - e^(-taxa_dia)
    ///
    /// Cada HR (hazard ratio) multiplica o risco e e facil de explicar: "a geladeira
    /// economica tem 1,9x o risco da padrao". A probabilidade fica sempre entre 0 e 1.
    /// O HR combinado tem piso (minimumHazardRatio): manutencao e qualidade reduzem o
    /// risco, mas nenhum equipamento fica imune.
    /// </summary>
    public static class ReliabilityModel
    {
        public static HazardBreakdown Hazard(
            EquipmentCategoryProfile category,
            EquipmentTierProfile tier,
            ReliabilityParameters p,
            double ageMonths,
            double utilization,
            int recentFailures,
            int monthsWithoutPreventive,
            bool preventiveActive,
            double policyRatio,
            double unitRatio,
            double managementRatio)
        {
            // Fila acima da capacidade nao desgasta mais o equipamento: a intensidade tem teto (usageCap).
            double usage = p.usageCap > 0d ? Math.Min(utilization, p.usageCap) : utilization;
            var h = new HazardBreakdown
            {
                BaseRatePerDay = Math.Max(0d, category.baseFailuresPerMonth) / 30d,
                TierRatio = Math.Max(0d, tier.hazardFactor),
                AgeRatio = Math.Exp(p.ageBeta * Math.Max(0d, ageMonths) / Math.Max(1d, tier.lifespanMonths)),
                UsageRatio = Math.Exp(p.usageBeta * Math.Max(0d, usage - p.usageThreshold)),
                HistoryRatio = Math.Pow(Math.Max(1d, p.recentFailureHazard), Math.Max(0, recentFailures)),
                MaintenanceGapRatio = preventiveActive ? 1d : 1d + Math.Min(p.maintenanceGapCap, p.maintenanceGapPerMonth * Math.Max(0, monthsWithoutPreventive)),
                PolicyRatio = Math.Max(0d, policyRatio),
                UnitRatio = Math.Max(0d, unitRatio),
                ManagementRatio = Math.Max(0d, managementRatio)
            };
            double combined = h.TierRatio * h.AgeRatio * h.UsageRatio * h.HistoryRatio * h.MaintenanceGapRatio
                              * h.PolicyRatio * h.UnitRatio * h.ManagementRatio;
            h.CombinedRatio = Math.Max(p.minimumHazardRatio, combined);
            h.RatePerDay = h.BaseRatePerDay * h.CombinedRatio;
            h.DailyProbability = SimMath.HazardToProbability(h.RatePerDay);
            h.MonthlyProbability = SimMath.HazardToProbability(h.RatePerDay, 30d);
            return h;
        }

        public static int RecentFailures(EquipmentUnitState unit, int absDay, int windowDays)
        {
            if (unit.failureDays == null) return 0;
            return unit.failureDays.Count(d => absDay - d >= 0 && absDay - d <= windowDays);
        }

        /// <summary>Depreciacao linear mensal = custo de compra / vida util (Bloco 9: ativos / 60 meses para o nivel padrao).</summary>
        public static double MonthlyDepreciation(EquipmentUnitState unit, EquipmentTierProfile tier)
        {
            double remaining = Math.Max(0d, unit.purchaseCost - unit.accumulatedDepreciation);
            return Math.Min(remaining, unit.purchaseCost / Math.Max(1d, tier.lifespanMonths));
        }
    }
}
