// Game/Domain/Simulation/Models.cs
// Modelos de cálculo. Todos são funções puras: recebem números e devolvem números.
// Não guardam estado, não sorteiam (o sorteio vem de fora) e não sabem que Unity existe.
// Isso permite testar cada fórmula sozinha, com um número na entrada e outro na saída.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    // ================================================================== MERCADO
    public sealed class FitResult
    {
        public double Fit;                                         // F_encaixe
        public double CategoryAttractiveness;                      // usado na concorrência
        public Dictionary<string, double> ProductShare = new Dictionary<string, double>(); // fração dos pedidos por prato
        public Dictionary<Segment, double> SegmentContribution = new Dictionary<Segment, double>();
    }

    public sealed class DemandBreakdown
    {
        public double Base, Fit, Competition, Reputation, Loyalty, Marketing, Calendar, Events, Noise;
        public double Potential => Base * Fit * Competition * Reputation * Loyalty * Marketing * Calendar * Events * Noise;
    }

    public static class MarketModel
    {
        /// p_perc = preço / (1 + 0,6 * (qualidade - 0,5))
        public static double PerceivedPrice(double price, double quality)
            => price / (1.0 + 0.6 * (quality - 0.5));

        /// aceitação = 1 / (1 + (p_perc / (T * f_local))^(3 * sens))
        public static double Acceptance(double perceivedPrice, double referenceSpend, double localPriceFactor, double sensitivity)
            => 1.0 / (1.0 + Math.Pow(perceivedPrice / (referenceSpend * localPriceFactor), 3.0 * sensitivity));

        /// atrat_s ajustada: +15% no público-alvo, -5% nos outros
        public static double AdjustedAttractiveness(RestaurantProfile r, Segment s, Segment target, SimulationConstants k)
            => r.Attractiveness[s] * (s == target ? k.TargetSegmentBonus : k.OtherSegmentPenalty);

        /// F_encaixe = (1/0,45) * soma_s[(mix_s + alcance_s) * atrat_s * média_i(aceitação)]
        public static FitResult MarketFit(SimulationCatalog cat, RegionProfile region, RestaurantProfile rest,
                                          Segment target, IReadOnlyList<MenuPrice> menu, double quality, double marketingReach)
        {
            var k = cat.Constants;
            var result = new FitResult();
            var productWeight = rest.Products.ToDictionary(p => p.Id, p => 0.0);
            double sum = 0, attractivenessSum = 0;

            foreach (var seg in cat.Segments.Values)
            {
                double mix = region.Mix.TryGetValue(seg.Segment, out var m) ? m : 0.0;
                double reach = marketingReach * seg.CityShare * k.CityReachFlow;     // alcance_s
                double attr = AdjustedAttractiveness(rest, seg.Segment, target, k);  // atrat_s
                double weight = (mix + reach) * attr;

                double accSum = 0;
                foreach (var product in rest.Products)
                {
                    double price = menu.First(x => x.ProductId == product.Id).Price;
                    double acc = Acceptance(PerceivedPrice(price, quality), seg.ReferenceSpend, region.PriceFactor, seg.Sensitivity);
                    accSum += acc;
                    productWeight[product.Id] += weight * acc;                       // quem compra o quê
                }
                double contribution = weight * accSum / rest.Products.Count;
                result.SegmentContribution[seg.Segment] = contribution / k.FitReference;
                sum += contribution;
                attractivenessSum += mix * attr;
            }

            result.Fit = sum / k.FitReference;
            result.CategoryAttractiveness = attractivenessSum;
            double total = productWeight.Values.Sum();
            foreach (var kv in productWeight)
                result.ProductShare[kv.Key] = total > 0 ? kv.Value / total : 1.0 / rest.Products.Count;
            return result;
        }

        /// F_concorr = (atratividade / pressão)^0,5
        public static double CompetitionFactor(double attractiveness, double pressure)
            => Math.Sqrt(attractiveness / pressure);

        /// F_reput: 0,6 (reputação 0) a 1,4 (reputação 100)
        public static double ReputationFactor(double reputation)
            => 0.6 + 0.8 * Clamp(reputation, 0, 100) / 100.0;

        /// F_fidel = 1 + força * (satisfação - 50) / 100
        public static double LoyaltyFactor(double strength, double satisfaction)
            => 1.0 + strength * (satisfaction - 50.0) / 100.0;

        /// F_mkt = 1 + ganho máximo * lembrança de marca
        public static double MarketingFactor(double maxGain, double brandRecall)
            => 1.0 + maxGain * brandRecall;

        /// F_calend = perfil do dia da semana * sazonalidade do mês * índice regional (dia 1 = segunda)
        public static double CalendarFactor(RegionProfile region, SimulationConstants k, int month, int day)
            => region.WeekdayProfile[(day - 1) % 7]
             * k.MonthSeasonality[(month - 1) % k.MonthSeasonality.Length]
             * region.RegionalIndex;

        public static double Clamp(double v, double min, double max) => v < min ? min : (v > max ? max : v);
    }

    // ================================================================== OPERAÇÃO
    public sealed class CapacityResult
    {
        public double Service, Kitchen, Dining;
        public double Max => Math.Min(Service, Math.Min(Kitchen, Dining));
        public string Bottleneck => Max == Dining ? "salão" : (Max == Kitchen ? "cozinha" : "atendimento");
    }

    public sealed class ServeResult
    {
        public double Served, LostCapacity, LostStock;
        public double Lost => LostCapacity + LostStock;
    }

    public static class OperationsModel
    {
        /// 0,85 (moral 0) a 1,15 (moral 100) + experiência + gestão + treinamento
        public static double Productivity(double morale, double experience, bool hasManager, double trainingAdd, SimulationConstants k)
            => 0.85 + 0.30 * MarketModel.Clamp(morale, 0, 100) / 100.0
             + experience + (hasManager ? k.ManagerProductivity : 0) + trainingAdd;

        /// pedidos atendidos = mínimo(demanda, capacidade, estoque). Primeiro a capacidade corta, depois o estoque.
        public static ServeResult Serve(double demand, double capacity, double stockInOrders)
        {
            double afterCapacity = Math.Min(demand, capacity);
            double served = Math.Min(afterCapacity, Math.Max(0, stockInOrders));
            return new ServeResult
            {
                Served = served,
                LostCapacity = demand - afterCapacity,
                LostStock = afterCapacity - served
            };
        }

        /// Tempo de atendimento: igual ao tempo-base até 75% de ocupação; 1,8x em 100%; 2,6x em 125%.
        public static double ServiceTimeFactor(double occupancy)
        {
            if (occupancy <= 0.75) return 1.0;
            if (occupancy <= 1.00) return 1.0 + (occupancy - 0.75) / 0.25 * 0.8;
            if (occupancy <= 1.25) return 1.8 + (occupancy - 1.00) / 0.25 * 0.8;
            return 2.6;
        }

        /// Erros = 4% * fator do treinamento + 12% por ponto de sobrecarga (ocupação acima de 100%).
        /// A ocupação entra limitada a 125%, como no tempo de atendimento e no desgaste dos equipamentos:
        /// sem esse teto, um dia com a cozinha quase parada geraria taxa de erro acima de 50%.
        public static double ErrorRate(double trainingFactor, double occupancy)
            => 0.04 * trainingFactor + 0.12 * Math.Max(0, Math.Min(occupancy, 1.25) - 1.0);
    }

    // ================================================================== EXPERIÊNCIA (NPS, reputação, moral)
    public sealed class NpsInput
    {
        public double ServiceTimeFactor;   // 1 = dentro do padrão
        public double LostStockRatio;      // pedidos perdidos por falta de estoque / demanda
        public double PriceLevel;          // ticket médio / ticket de referência
        public double Quality;             // 0..1
        public double ErrorRate;
        public bool UnresolvedNegativeEvent;
        public bool PremiumManagerBonus;
    }

    public sealed class NpsBreakdown
    {
        public double Time, Stock, Price, Quality, Errors, Event, Premium;
        public double Total => MarketModel.Clamp(50 + Time + Stock + Price + Quality - Errors - Event + Premium, -100, 100);
    }

    public static class ExperienceModel
    {
        public static NpsBreakdown DailyNps(NpsInput i)
        {
            var b = new NpsBreakdown();
            b.Time = i.ServiceTimeFactor <= 1.0 ? 10 : -15 * Math.Min(1.0, (i.ServiceTimeFactor - 1.0) / 1.6);
            b.Stock = i.LostStockRatio <= 0 ? 5 : -15 * Math.Min(1.0, i.LostStockRatio / 0.2);
            b.Price = i.PriceLevel <= 1.0 ? 5 : (i.Quality < 0.55 ? -10 * Math.Min(1.0, (i.PriceLevel - 1.0) / 0.2) : 0);
            b.Quality = MarketModel.Clamp(20 * (i.Quality - 0.5), -10, 10);
            b.Errors = 120 * i.ErrorRate;
            b.Event = i.UnresolvedNegativeEvent ? 10 : 0;
            b.Premium = i.PremiumManagerBonus ? 5 : 0;
            return b;
        }

        /// A reputação anda 3,5% da distância até a satisfação, por dia
        public static double NextReputation(double reputation, double satisfactionToday, double lag)
            => reputation + lag * (satisfactionToday - reputation);

        /// A moral tende a 65 (+ ajuste da remuneração) e cai 20 pontos de alvo em dia de sobrecarga
        public static double NextMorale(double morale, double payAdjust, bool overloaded, SimulationConstants k)
        {
            double target = k.MoraleTarget + payAdjust - (overloaded ? k.OverloadMoralePenalty : 0);
            return MarketModel.Clamp(morale + k.MoraleDriftPerDay * (target - morale), 0, 100);
        }

        /// Lembrança de marca no início do mês: sobra 85% do mês anterior + o que a política investe
        public static double NextBrandRecall(double recall, double investment, double carry)
            => MarketModel.Clamp(carry * recall + investment, 0, 1);
    }

    // ================================================================== CONFIABILIDADE (equipamentos)
    public sealed class HazardBreakdown
    {
        public double BasePerDay, Level, Age, Usage, History, MaintenanceGap, Policy, Management, Floor;
        public double Hazard => Math.Max(Floor, BasePerDay * Level * Age * Usage * History * MaintenanceGap * Policy * Management);
        public double Probability => 1.0 - Math.Exp(-Hazard);
    }

    public static class ReliabilityModel
    {
        /// h_dia = falhas/30 * HR_nível * e^(1,5*idade/vida) * e^(1,2*max(0, min(uso,1,25)-0,8)) * 1,3^falhas60 * HR_lacuna * HR_política * HR_gestão
        public static HazardBreakdown DailyHazard(CategoryProfile category, LevelProfile level, double ageMonths,
                                                  double usage, int failuresLast60Days, int monthsWithoutPreventive,
                                                  double policyHr, double managementHr, SimulationConstants k)
        {
            double basePerDay = category.FailuresPerMonth / k.DaysPerMonth;
            return new HazardBreakdown
            {
                BasePerDay = basePerDay,
                Level = level.HazardRatio,
                Age = Math.Exp(1.5 * ageMonths / level.LifeMonths),
                Usage = Math.Exp(1.2 * Math.Max(0, Math.Min(usage, 1.25) - 0.8)),
                History = Math.Pow(1.3, failuresLast60Days),
                MaintenanceGap = 1.0 + Math.Min(0.5, 0.10 * monthsWithoutPreventive),
                Policy = policyHr,
                Management = managementHr,
                Floor = k.HazardFloorFactor * basePerDay
            };
        }
    }
}
