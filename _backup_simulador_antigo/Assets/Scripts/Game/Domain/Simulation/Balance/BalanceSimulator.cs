using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Game.Domain.Simulation
{
    /// <summary>Uma estrategia arquetipica de jogador para o balanceamento automatizado.</summary>
    public sealed class StrategyProfile
    {
        public string Id;
        public string Name;
        /// <summary>Posicao dos precos na faixa de cada produto (0 = minimo, 1 = maximo).</summary>
        public double PricePosition = 0.5d;
        public bool BuySpecificEquipment = true;
        public int ExtraAttendants;
        public int ExtraSpecialists;
        public bool HireManager;
        public string CreditLineId;
        public Dictionary<string, string> Policies = new Dictionary<string, string>();
        public IReactionPolicy Reactions = new DefaultReactionPolicy();

        public static List<StrategyProfile> Archetypes()
        {
            return new List<StrategyProfile>
            {
                new StrategyProfile
                {
                    Id = "equilibrado", Name = "Equilibrado", PricePosition = 0.45d,
                    Policies = P(("marketing", "low"), ("maintenance", "preventive"), ("supplier", "standard"), ("stock", "medium"), ("salary", "market"), ("training", "basic"))
                },
                new StrategyProfile
                {
                    Id = "economico", Name = "Econômico", PricePosition = 0.2d, BuySpecificEquipment = false,
                    Policies = P(("marketing", "none"), ("maintenance", "corrective"), ("supplier", "economic"), ("stock", "low"), ("salary", "below"), ("training", "none")),
                    Reactions = new CheapestReactionPolicy()
                },
                new StrategyProfile
                {
                    Id = "premium", Name = "Premium", PricePosition = 0.75d, HireManager = true,
                    Policies = P(("marketing", "medium"), ("maintenance", "intensive"), ("supplier", "premium"), ("stock", "medium"), ("salary", "above"), ("training", "advanced"), ("loyalty", "on")),
                    Reactions = new ThoroughReactionPolicy()
                },
                new StrategyProfile
                {
                    Id = "expansao_credito", Name = "Expansão com crédito", PricePosition = 0.5d, ExtraSpecialists = 1, ExtraAttendants = 1, HireManager = true,
                    CreditLineId = "credito_empresarial_padrao",
                    Policies = P(("marketing", "high"), ("maintenance", "preventive"), ("supplier", "standard"), ("stock", "high"), ("salary", "market"), ("training", "basic"))
                },
                new StrategyProfile
                {
                    Id = "volume", Name = "Volume com preço baixo", PricePosition = 0.1d, ExtraSpecialists = 1, ExtraAttendants = 1,
                    Policies = P(("marketing", "medium"), ("maintenance", "corrective"), ("supplier", "economic"), ("stock", "medium"), ("salary", "market"), ("training", "none")),
                    Reactions = new CheapestReactionPolicy()
                }
            };
        }

        private static Dictionary<string, string> P(params (string category, string option)[] items)
        {
            var map = new Dictionary<string, string>();
            foreach (var item in items) map[item.category] = item.option;
            return map;
        }
    }

    public sealed class BalanceCell
    {
        public string RestaurantTypeId;
        public string LocationId;
        public string SegmentId;
        public string StrategyId;
        public int Runs;
        public double MeanNetIncome;
        public double MedianEquity;
        public double P10Equity;
        public double P90Equity;
        public double BankruptcyRate;
        public double NegativeCashRate;
        public double MeanNps;
        public double DecisionEventsPerMonth;
        public double MeanRevenue;
        public double NetMarginPercent;
    }

    public sealed class BalanceReport
    {
        public List<BalanceCell> Cells = new List<BalanceCell>();
        public Dictionary<string, double> EventsPerMonth = new Dictionary<string, double>();
        public Dictionary<string, int> StrategyWins = new Dictionary<string, int>();
        public List<string> Warnings = new List<string>();
        public int Contexts;

        public string ToCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("restaurante;localizacao;segmento;estrategia;rodadas;lucro_medio_trimestre;patrimonio_mediano;patrimonio_p10;patrimonio_p90;falencia;caixa_negativo;nps;decisoes_mes;receita_media_mes;margem_liquida");
            foreach (var c in Cells)
            {
                sb.AppendLine(string.Join(";", c.RestaurantTypeId, c.LocationId, c.SegmentId, c.StrategyId, c.Runs.ToString(CultureInfo.InvariantCulture),
                    F(c.MeanNetIncome), F(c.MedianEquity), F(c.P10Equity), F(c.P90Equity), F(c.BankruptcyRate), F(c.NegativeCashRate), F(c.MeanNps),
                    F(c.DecisionEventsPerMonth), F(c.MeanRevenue), F(c.NetMarginPercent)));
            }
            return sb.ToString();
        }

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Contextos (restaurante x localizacao x publico): " + Contexts);
            foreach (var group in Cells.GroupBy(c => c.StrategyId))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-18} vitorias {1,3} | lucro medio {2,10:0} | margem {3,6:0.0}% | falencia {4,5:0.0}% | caixa negativo {5,5:0.0}% | NPS {6,5:0} | decisoes/mes {7,4:0.0}",
                    group.Key, StrategyWins.TryGetValue(group.Key, out int w) ? w : 0, group.Average(c => c.MeanNetIncome), group.Average(c => c.NetMarginPercent),
                    group.Average(c => c.BankruptcyRate) * 100d, group.Average(c => c.NegativeCashRate) * 100d, group.Average(c => c.MeanNps), group.Average(c => c.DecisionEventsPerMonth)));
            }
            foreach (var warning in Warnings) sb.AppendLine("ALERTA: " + warning);
            return sb.ToString();
        }

        private static string F(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Balanceamento automatizado (Monte Carlo): roda partidas completas para cada combinacao de
    /// restaurante, localizacao, publico permitido e estrategia, com varias sementes. Procura
    /// estrategias dominantes (melhor em quase todos os contextos), eventos frequentes demais
    /// ou que nunca ocorrem, e combinacoes que sempre quebram.
    /// </summary>
    public static class BalanceSimulator
    {
        public static readonly Dictionary<string, string[]> AllowedSegments = new Dictionary<string, string[]>
        {
            { "PODRAO", new[] { "LOW", "MEDIUM" } },
            { "JAPONES", new[] { "MEDIUM", "HIGH" } },
            { "FRANCES", new[] { "HIGH" } }
        };

        public static MonthInputs BuildInputs(SimulationCatalog catalog, string restaurantTypeId, string locationId, string segmentId, StrategyProfile strategy, ulong seed)
        {
            var restaurant = catalog.Restaurant(restaurantTypeId);
            var inputs = new MonthInputs
            {
                SessionId = "balance-" + restaurantTypeId + "-" + locationId + "-" + segmentId + "-" + strategy.Id + "-" + seed.ToString(CultureInfo.InvariantCulture),
                Month = 1,
                Seed = seed,
                RestaurantTypeId = restaurantTypeId,
                LocationId = locationId,
                TargetSegmentId = segmentId,
                OpeningReputation = catalog.Config.experience.initialReputation,
                InitialCapital = DefaultSimulationConfig.InitialCapital(restaurantTypeId)
            };
            foreach (var product in restaurant.Products)
                inputs.MenuPrices[product.Id] = Math.Round(product.MinPrice + (product.MaxPrice - product.MinPrice) * strategy.PricePosition, 2);
            foreach (var role in restaurant.RequiredRoles) inputs.Team[role.Key] = role.Value;
            inputs.Team["attendant"] = inputs.Team.TryGetValue("attendant", out int a) ? a + strategy.ExtraAttendants : strategy.ExtraAttendants;
            string specialist = DefaultSimulationConfig.Specialist(restaurantTypeId);
            inputs.Team[specialist] = (inputs.Team.TryGetValue(specialist, out int s) ? s : 0) + strategy.ExtraSpecialists;
            if (strategy.HireManager) inputs.Team["manager"] = 1;
            inputs.EquipmentOwned.AddRange(DefaultSimulationConfig.BasicEquipment());
            if (strategy.BuySpecificEquipment) inputs.EquipmentOwned.AddRange(DefaultSimulationConfig.SpecificEquipment(restaurantTypeId));
            double equipmentCost = inputs.EquipmentOwned.Sum(id => catalog.Equipment[id].Cost);
            double cash = inputs.InitialCapital - equipmentCost;
            if (!string.IsNullOrEmpty(strategy.CreditLineId) && catalog.Credits.TryGetValue(strategy.CreditLineId, out var credit) && credit.MaxAmount > 0d)
            {
                inputs.CreditLineId = credit.Id;
                inputs.LoanBalance = credit.MaxAmount;
                cash += credit.MaxAmount;
            }
            inputs.OpeningCash = cash;
            foreach (var policy in strategy.Policies) inputs.Policies[policy.Key] = policy.Value;
            return inputs;
        }

        public static BalanceReport Run(SimulationCatalog catalog, int seedsPerCell = 30, IList<StrategyProfile> strategies = null, Action<string> progress = null)
        {
            strategies = strategies ?? StrategyProfile.Archetypes();
            var report = new BalanceReport();
            var eventCounts = new Dictionary<string, double>();
            double totalMonths = 0d;
            foreach (var restaurant in catalog.Restaurants.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var segments = AllowedSegments.TryGetValue(restaurant, out var allowed) ? allowed : catalog.Config.segments.Select(s => s.id).ToArray();
                foreach (var location in catalog.Locations.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    foreach (var segment in segments)
                    {
                        report.Contexts++;
                        var cellsOfContext = new List<BalanceCell>();
                        foreach (var strategy in strategies)
                        {
                            var equities = new List<double>();
                            double net = 0d, nps = 0d, decisions = 0d, revenue = 0d, netRevenue = 0d;
                            int bankrupt = 0, negativeCash = 0, months = 0;
                            for (int k = 0; k < seedsPerCell; k++)
                            {
                                ulong seed = (ulong)(1000003 * (k + 1)) ^ 0x5DEECE66DUL;
                                var inputs = BuildInputs(catalog, restaurant, location, segment, strategy, seed);
                                var campaign = CampaignRunner.Run(catalog, inputs, strategy.Reactions);
                                equities.Add(campaign.FinalEquity);
                                net += campaign.TotalNetIncome;
                                nps += campaign.AverageNps;
                                decisions += campaign.DecisionEvents;
                                months += campaign.Months.Count;
                                revenue += campaign.Months.Sum(m => m.income.grossRevenue);
                                netRevenue += campaign.Months.Sum(m => m.income.netRevenue);
                                if (campaign.Bankrupt) bankrupt++;
                                if (campaign.Months.Any(m => m.indicators.overdraftDays > 0)) negativeCash++;
                                foreach (var month in campaign.Months)
                                {
                                    totalMonths++;
                                    foreach (var e in month.events)
                                    {
                                        eventCounts.TryGetValue(e.eventId, out double count);
                                        eventCounts[e.eventId] = count + 1d;
                                    }
                                }
                            }
                            equities.Sort();
                            var cell = new BalanceCell
                            {
                                RestaurantTypeId = restaurant,
                                LocationId = location,
                                SegmentId = segment,
                                StrategyId = strategy.Id,
                                Runs = seedsPerCell,
                                MeanNetIncome = net / seedsPerCell,
                                MedianEquity = OutlookEstimator.Percentile(equities, 0.5d),
                                P10Equity = OutlookEstimator.Percentile(equities, 0.1d),
                                P90Equity = OutlookEstimator.Percentile(equities, 0.9d),
                                BankruptcyRate = bankrupt / (double)seedsPerCell,
                                NegativeCashRate = negativeCash / (double)seedsPerCell,
                                MeanNps = nps / seedsPerCell,
                                DecisionEventsPerMonth = months > 0 ? decisions / months : 0d,
                                MeanRevenue = months > 0 ? revenue / months : 0d,
                                NetMarginPercent = netRevenue > 0d ? net / netRevenue * 100d : 0d
                            };
                            report.Cells.Add(cell);
                            cellsOfContext.Add(cell);
                        }
                        var winner = cellsOfContext.OrderByDescending(c => c.MedianEquity - 0.5d * Math.Max(0d, c.MedianEquity - c.P10Equity)).First();
                        report.StrategyWins.TryGetValue(winner.StrategyId, out int wins);
                        report.StrategyWins[winner.StrategyId] = wins + 1;
                        progress?.Invoke(restaurant + "/" + location + "/" + segment + " -> " + winner.StrategyId);
                    }
                }
            }
            foreach (var pair in eventCounts) report.EventsPerMonth[pair.Key] = totalMonths > 0 ? pair.Value / totalMonths : 0d;

            foreach (var pair in report.StrategyWins)
                if (pair.Value >= 0.8d * report.Contexts)
                    report.Warnings.Add("Estrategia possivelmente dominante: " + pair.Key + " vence em " + pair.Value + " de " + report.Contexts + " contextos.");
            foreach (var definition in catalog.Config.events)
            {
                report.EventsPerMonth.TryGetValue(definition.id, out double perMonth);
                if (perMonth > 2d) report.Warnings.Add("Evento muito frequente: " + definition.id + " (" + perMonth.ToString("0.00", CultureInfo.InvariantCulture) + " por mes).");
                if (perMonth <= 0d && definition.kind == EventKind.Probabilistic && definition.baseRatePerMonth > 0d)
                    report.Warnings.Add("Evento que nunca ocorreu: " + definition.id + ".");
            }
            foreach (var context in report.Cells.GroupBy(c => c.RestaurantTypeId + "/" + c.LocationId + "/" + c.SegmentId))
                if (context.All(c => c.BankruptcyRate >= 0.9d))
                    report.Warnings.Add("Combinacao que quebra com qualquer estrategia: " + context.Key + ".");
            return report;
        }
    }
}
