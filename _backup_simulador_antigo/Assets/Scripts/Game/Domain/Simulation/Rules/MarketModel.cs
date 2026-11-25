using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    /// <summary>Entradas do modelo de mercado para um dia.</summary>
    public sealed class MarketQuery
    {
        public RestaurantSpec Restaurant;
        public LocationSpec Location;
        public string TargetSegmentId;
        public IReadOnlyDictionary<string, double> MenuPrices;
        /// <summary>Indice de qualidade percebida, 0..1 (0,5 = neutro).</summary>
        public double Quality = 0.5d;
        public double TicketFactor = 1d;
        public double PricePerceptionFactor = 1d;
        /// <summary>Alcance de clientes de outras regioes (0..1), ja multiplicado pela consciencia de marca.</summary>
        public double Reach;
        public double Awareness;
        public double MarketingUplift;
        public double Reputation = 50d;
        public double Satisfaction = 50d;
        public double LoyaltyStrength;
        /// <summary>Pressao competitiva (1 = concorrencia media).</summary>
        public double CompetitionPressure = 1d;
        /// <summary>Dia da semana x sazonalidade x indice regional.</summary>
        public double CalendarFactor = 1d;
        public double EventFactor = 1d;
        public double Noise = 1d;
    }

    public sealed class SegmentDemand
    {
        public string SegmentId;
        public double LocalShare;
        public double OutsideShare;
        public double Appeal;
        public double Acceptance;
        /// <summary>Contribuicao do segmento para o encaixe (antes da normalizacao).</summary>
        public double FitContribution;
        public double Demand;
        public double Ticket;
        public double UnitCost;
    }

    public sealed class MarketResult
    {
        public double BaseDemand;
        public double PotentialDemand;
        public double Ticket;
        public double UnitCost;
        public double FitFactor;
        public double CompetitionFactor;
        public double ReputationFactor;
        public double LoyaltyFactor;
        public double MarketingFactor;
        public double PricePosition;
        public List<SegmentDemand> Segments = new List<SegmentDemand>();
    }

    /// <summary>
    /// Demanda potencial = demanda base da localizacao
    ///                    x encaixe de mercado (segmentos x atratividade x aceitacao de preco)
    ///                    x competitividade x reputacao x fidelizacao x marketing
    ///                    x calendario x eventos x ruido.
    ///
    /// O encaixe nao e uma tabela de pontos: ele emerge da composicao do publico da
    /// area, da preferencia de cada segmento pela categoria e da aceitacao do preco de
    /// cada prato. Por isso um restaurante popular em area nobre ainda vende para quem
    /// trabalha ali, e um restaurante premium em area universitaria pode melhorar com
    /// um prato acessivel, qualidade percebida ou marketing com alcance na cidade.
    /// </summary>
    public static class MarketModel
    {
        /// <summary>Aceitacao de um preco por um segmento: 1 / (1 + (p / T)^k).</summary>
        public static double Acceptance(double perceivedPrice, double referenceSpend, double steepness)
        {
            if (perceivedPrice <= 0d) return 1d;
            if (referenceSpend <= 0d) return 0d;
            return 1d / (1d + Math.Pow(perceivedPrice / referenceSpend, steepness));
        }

        public static double PerceivedPrice(double price, double ticketFactor, double perceptionFactor, double quality, double qualityPriceEffect)
        {
            double qualityRelief = 1d + qualityPriceEffect * (quality - 0.5d);
            if (qualityRelief < 0.2d) qualityRelief = 0.2d;
            return price * ticketFactor * perceptionFactor / qualityRelief;
        }

        public static MarketResult Evaluate(MarketQuery q, SimulationConfig config)
        {
            if (q == null) throw new ArgumentNullException(nameof(q));
            var market = config.market;
            var restaurant = q.Restaurant;
            var location = q.Location;
            var products = restaurant.Products;
            if (products.Count == 0) throw new InvalidOperationException("Restaurante sem produtos.");

            var prices = new double[products.Count];
            double positionSum = 0d;
            for (int i = 0; i < products.Count; i++)
            {
                var product = products[i];
                double price = product.ReferencePrice;
                if (q.MenuPrices != null && q.MenuPrices.TryGetValue(product.Id, out double saved)) price = saved;
                if (price <= 0d) throw new InvalidOperationException("Preco invalido para " + product.Id);
                prices[i] = price;
                double span = product.MaxPrice - product.MinPrice;
                positionSum += span > 0d ? SimMath.Clamp01((price - product.MinPrice) / span) : 0.5d;
            }

            var result = new MarketResult
            {
                BaseDemand = location.BaseDailyDemand,
                PricePosition = positionSum / products.Count
            };

            double mixTotal = location.Profile.segmentMix.Sum(s => Math.Max(0d, s.value));
            double cityTotal = config.segments.Sum(s => Math.Max(0d, s.cityShare));
            double fitRaw = 0d;
            foreach (var segment in config.segments)
            {
                double local = mixTotal > 0d ? Math.Max(0d, location.Profile.segmentMix.Find(s => s.segmentId == segment.id)?.value ?? 0d) / mixTotal : 0d;
                double outside = cityTotal > 0d ? q.Reach * market.cityFlowMultiplier * Math.Max(0d, segment.cityShare) / cityTotal : 0d;
                double appeal = restaurant.Profile.appeal.Find(a => a.segmentId == segment.id)?.value ?? 0d;
                if (!string.IsNullOrEmpty(q.TargetSegmentId))
                    appeal *= segment.id == q.TargetSegmentId ? 1d + market.targetFocusBonus : 1d - market.offTargetPenalty;

                double reference = segment.referenceSpend * location.ReferencePriceFactor;
                double steepness = market.priceSteepness * segment.priceSensitivity;
                double acceptanceSum = 0d, ticketSum = 0d, costSum = 0d;
                for (int i = 0; i < products.Count; i++)
                {
                    double perceived = PerceivedPrice(prices[i], q.TicketFactor, q.PricePerceptionFactor, q.Quality, market.qualityPriceEffect);
                    double a = Acceptance(perceived, reference, steepness);
                    acceptanceSum += a;
                    ticketSum += a * prices[i] * q.TicketFactor;
                    costSum += a * products[i].UnitCost;
                }
                double acceptance = acceptanceSum / products.Count;
                double contribution = (local + outside) * appeal * acceptance;
                fitRaw += contribution;
                result.Segments.Add(new SegmentDemand
                {
                    SegmentId = segment.id,
                    LocalShare = local,
                    OutsideShare = outside,
                    Appeal = appeal,
                    Acceptance = acceptance,
                    FitContribution = contribution,
                    Ticket = acceptanceSum > 0d ? ticketSum / acceptanceSum : prices.Average() * q.TicketFactor,
                    UnitCost = acceptanceSum > 0d ? costSum / acceptanceSum : products.Average(p => p.UnitCost)
                });
            }

            result.FitFactor = market.fitReference > 0d ? fitRaw / market.fitReference : fitRaw;
            double attractiveness = 1d + market.qualityAttractiveness * (q.Quality - 0.5d);
            double pressure = Math.Max(0.1d, q.CompetitionPressure);
            result.CompetitionFactor = Math.Pow(Math.Max(0.05d, attractiveness) / pressure, market.competitionElasticity);
            result.ReputationFactor = market.reputationDemandMin + (market.reputationDemandMax - market.reputationDemandMin) * SimMath.Clamp(q.Reputation, 0d, 100d) / 100d;
            result.LoyaltyFactor = Math.Max(0.5d, 1d + q.LoyaltyStrength * (q.Satisfaction - 50d) / 100d);
            result.MarketingFactor = 1d + Math.Max(0d, q.MarketingUplift) * SimMath.Clamp01(q.Awareness);

            double multiplier = result.FitFactor * result.CompetitionFactor * result.ReputationFactor * result.LoyaltyFactor
                                * result.MarketingFactor * q.CalendarFactor * q.EventFactor * q.Noise;
            result.PotentialDemand = Math.Max(0d, location.BaseDailyDemand * multiplier);

            double weight = 0d, ticket = 0d, cost = 0d;
            foreach (var segment in result.Segments)
            {
                segment.Demand = fitRaw > 0d ? result.PotentialDemand * segment.FitContribution / fitRaw : 0d;
                weight += segment.Demand;
                ticket += segment.Demand * segment.Ticket;
                cost += segment.Demand * segment.UnitCost;
            }
            result.Ticket = weight > 0d ? ticket / weight : prices.Average() * q.TicketFactor;
            result.UnitCost = weight > 0d ? cost / weight : products.Average(p => p.UnitCost);
            return result;
        }

        /// <summary>Pressao competitiva do LocationData (LOW/MEDIUM/HIGH) para indice numerico.</summary>
        public static double CompetitionIndex(string level, MarketParameters market)
        {
            switch (level)
            {
                case "LOW": return market.competitionLow;
                case "HIGH": return market.competitionHigh;
                default: return market.competitionMedium;
            }
        }

        /// <summary>Fator do dia da semana normalizado para media 1 na semana.</summary>
        public static double WeekdayFactor(LocationMarketProfile profile, int weekday)
        {
            var values = profile?.weekdayProfile;
            if (values == null || values.Count != 7) return 1d;
            double mean = values.Average();
            if (mean <= 0d) return 1d;
            return values[((weekday % 7) + 7) % 7] / mean;
        }
    }
}
