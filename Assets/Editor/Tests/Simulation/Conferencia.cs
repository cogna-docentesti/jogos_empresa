// Assets/Editor/Tests/Simulation/Conferencia.cs
// Conferência independente do motor. Cada fórmula do GDD é escrita de novo aqui, a partir das
// entradas que o motor registrou no DayTrace/MonthTrace, SEM chamar MarketModel, OperationsModel,
// ExperienceModel nem Accounting. O resultado é comparado com o número que o motor produziu.
// A planilha Conferencia_Motor.xlsx faz exatamente as mesmas contas, em fórmulas.
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Simulation;

public sealed class CheckLine
{
    public string Item;
    public int Day;                       // 0 = fechamento do mês
    public double Motor, Expected;
    public bool Ok => Math.Abs(Motor - Expected) <= Math.Max(1e-6, 1e-7 * Math.Abs(Expected)) + 0.005;
    public override string ToString() => $"{(Day == 0 ? "mês" : "dia " + Day)} · {Item}: motor {Motor:0.######} esperado {Expected:0.######}";
}

public static class Conferencia
{
    /// Confere todos os dias e o fechamento. 'start' é o estado entregue ao MonthSimulation (antes do dia 1).
    public static List<CheckLine> Month(MonthSimulation sim, MonthReport report, SimulationState start)
    {
        var checks = new List<CheckLine>();
        var cat = sim.Catalog;
        var k = cat.Constants;
        var region = cat.Regions[sim.Setup.RegionId];
        var rest = cat.Restaurants[sim.Setup.Restaurant];
        void C(string item, int day, double motor, double expected) => checks.Add(new CheckLine { Item = item, Day = day, Motor = motor, Expected = expected });

        double recentDemand = start.RecentDemand;
        double recentCapacity = start.RecentCapacity;
        double reputation = start.Reputation, satisfaction = start.Satisfaction;
        double loyaltyPerOrder = PolicyOptions(sim).Sum(o => o.CostPerOrder);

        foreach (var d in sim.DayResults)
        {
            var t = d.Trace;
            int day = d.Day;

            // ------------------------------------------------ estado que vem do dia anterior
            C("Reputação antes da operação", day, t.ReputationBefore, reputation);
            C("Satisfação antes da operação", day, t.SatisfactionBefore, satisfaction);
            C("Memória de demanda (média móvel)", day, t.RecentDemandBefore, recentDemand);
            C("Capacidade recente", day, t.RecentCapacityBefore, recentCapacity);

            // ------------------------------------------------ qualidade, encaixe e mix de pratos
            double equipmentDelta = start.Equipment.Count > 0 ? start.Equipment.Average(u => cat.Levels[u.Level].QualityDelta) : 0;
            C("Qualidade dos equipamentos (média do nível)", day, t.EquipmentQualityDelta, equipmentDelta);
            double quality = Clamp(0.5 + equipmentDelta + t.QualityLever, 0, 1);
            C("Qualidade", day, d.Quality, quality);
            double perceivedDiv = 1 + 0.6 * (quality - 0.5);
            double fitSum = 0, attr = 0;
            var weightByProduct = rest.Products.ToDictionary(p => p.Id, p => 0.0);
            foreach (var seg in cat.Segments.Values)
            {
                double mix = region.Mix.TryGetValue(seg.Segment, out var m) ? m : 0;
                double adj = rest.Attractiveness[seg.Segment] * (seg.Segment == sim.Setup.TargetSegment ? 1.15 : 0.95);
                double weight = (mix + t.MarketingReach * seg.CityShare * 2.0) * adj;
                double accSum = 0;
                foreach (var p in rest.Products)
                {
                    double acc = 1 / (1 + Math.Pow(sim.Setup.PriceOf(p.Id) / perceivedDiv / (seg.ReferenceSpend * region.PriceFactor), 3 * seg.Sensitivity));
                    accSum += acc;
                    weightByProduct[p.Id] += weight * acc;
                }
                fitSum += weight * accSum / rest.Products.Count;
                attr += mix * adj;
            }
            double fit = fitSum / 0.45;
            C("Encaixe de mercado", day, d.Demand.Fit, fit);
            double totalWeight = weightByProduct.Values.Sum();
            double ticket = 0, refTicket = 0, unitCost = 0;
            foreach (var p in rest.Products)
            {
                double share = totalWeight > 0 ? weightByProduct[p.Id] / totalWeight : 1.0 / rest.Products.Count;
                ticket += share * sim.Setup.PriceOf(p.Id);
                refTicket += share * p.ReferencePrice;
                unitCost += share * p.UnitCost;
            }
            C("Ticket médio", day, d.AvgTicket, ticket);

            // ------------------------------------------------ oito fatores da demanda
            C("Fator concorrência", day, d.Demand.Competition, Math.Sqrt(attr / t.CompetitionPressure));
            C("Fator reputação", day, d.Demand.Reputation, 0.6 + 0.8 * Clamp(reputation, 0, 100) / 100);
            C("Fator fidelização", day, d.Demand.Loyalty, 1 + t.LoyaltyStrength * (satisfaction - 50) / 100);
            C("Fator marketing", day, d.Demand.Marketing, 1 + t.MarketingMaxGain * t.BrandRecall);
            C("Fator calendário", day, d.Demand.Calendar,
              region.WeekdayProfile[(day - 1) % 7] * k.MonthSeasonality[(sim.Month - 1) % k.MonthSeasonality.Length] * region.RegionalIndex);
            double demand = region.BaseDemandPerDay * fit * d.Demand.Competition * d.Demand.Reputation * d.Demand.Loyalty
                          * d.Demand.Marketing * d.Demand.Calendar * d.Demand.Events * d.Demand.Noise;
            C("Demanda potencial", day, d.Demand.Potential, demand);

            // ------------------------------------------------ capacidade
            double productivity = 0.85 + 0.30 * Clamp(t.Morale, 0, 100) / 100 + t.Experience + (t.HasManager ? 0.04 : 0) + t.TrainingAdd;
            C("Produtividade", day, t.Productivity, productivity);
            double service = t.AttendantsPresent * rest.AttendantCapacityPerDay * productivity * t.ServiceAvailability * t.ServiceCapFactor;
            double kitchen = (t.SpecialistsPresent * cat.Roles[rest.SpecialistRole].KitchenCapacityPerDay * productivity + t.KitchenBonus)
                           * t.KitchenAvailability * t.KitchenCapFactor;
            double dining = (region.PhysicalCapacityPerDay + t.DiningBonus) * t.DiningAvailability;
            C("Capacidade do atendimento", day, d.Capacity.Service, service);
            C("Capacidade da cozinha", day, d.Capacity.Kitchen, kitchen);
            C("Capacidade do salão", day, d.Capacity.Dining, dining);
            double capMax = Math.Min(service, Math.Min(kitchen, dining));

            // ------------------------------------------------ estoque e compra
            double stockUnitCost = unitCost * t.SupplierCostFactor * (t.Installments ? 1 : 1 - k.CashDiscountRate);
            C("Custo unitário do estoque", day, t.StockUnitCost, stockUnitCost);
            double expected = Math.Min(recentDemand, recentCapacity > 0 ? recentCapacity : capMax);
            double purchase = Math.Max(0, t.CoverDays * expected * stockUnitCost - t.InventoryBefore)
                            / (t.Installments ? 1 : 1 - k.CashDiscountRate) * t.DeliveryFactor;
            C("Compra do dia (preço de tabela)", day, d.Purchases, purchase);
            double added = t.Installments ? purchase : purchase * (1 - k.CashDiscountRate);
            C("Estoque que entrou", day, t.InventoryAdded, added);
            double inventory = t.InventoryBefore + added;
            double stockInOrders = inventory / stockUnitCost;

            // ------------------------------------------------ atendimento
            double afterCapacity = Math.Min(demand, capMax);
            double served = Math.Min(afterCapacity, Math.Max(0, stockInOrders));
            C("Pedidos atendidos", day, d.Serve.Served, served);
            C("Perdidos por capacidade", day, d.Serve.LostCapacity, demand - afterCapacity);
            C("Perdidos por estoque", day, d.Serve.LostStock, afterCapacity - served);
            double revenue = served * ticket;
            C("Receita do dia", day, d.Revenue, revenue);
            double cmvSold = Math.Min(served * stockUnitCost, inventory);
            C("CMV das vendas", day, t.CmvSold, cmvSold);
            double afterSale = inventory - cmvSold;
            double spoil = Math.Min(afterSale, afterSale * rest.PerishabilityPerDay * (t.RefrigerationDown ? 4 : 1));
            C("Perda por validade", day, d.Spoilage, spoil);
            C("Estoque no fim do dia", day, d.InventoryEnd, afterSale - spoil);

            // ------------------------------------------------ tempo, erros, NPS, reputação
            double occupancy = capMax > 0 ? demand / capMax : 2.0;
            C("Ocupação", day, d.Occupancy, occupancy);
            double time = occupancy <= 0.75 ? 1 : occupancy <= 1 ? 1 + (occupancy - 0.75) / 0.25 * 0.8
                        : occupancy <= 1.25 ? 1.8 + (occupancy - 1) / 0.25 * 0.8 : 2.6;
            C("Fator de tempo de atendimento", day, d.ServiceTimeFactor, time);
            double errors = 0.04 * t.ErrorsFactor + 0.12 * Math.Max(0, Math.Min(occupancy, 1.25) - 1);
            C("Taxa de erros", day, d.ErrorRate, errors);
            double lostStockRatio = demand > 0 ? (afterCapacity - served) / demand : 0;
            double priceLevel = refTicket > 0 ? ticket / refTicket : 1;
            double nps = 50
                + (time <= 1 ? 10 : -15 * Math.Min(1, (time - 1) / 1.6))
                + (lostStockRatio <= 0 ? 5 : -15 * Math.Min(1, lostStockRatio / 0.2))
                + (priceLevel <= 1 ? 5 : quality < 0.55 ? -10 * Math.Min(1, (priceLevel - 1) / 0.2) : 0)
                + Clamp(20 * (quality - 0.5), -10, 10)
                - 120 * errors
                - (t.UnresolvedNegative ? 10 : 0)
                + (t.PremiumBonus ? 5 : 0);
            nps = Clamp(nps, -100, 100);
            C("NPS do dia", day, d.Nps.Total, nps);
            double satisfactionToday = Clamp(nps, 0, 100);
            reputation += 0.035 * (satisfactionToday - reputation);
            satisfaction += k.SatisfactionMemory * (satisfactionToday - satisfaction);
            C("Reputação no fim do dia", day, d.Reputation, reputation);
            C("Satisfação acumulada no fim do dia", day, d.Satisfaction, satisfaction);

            // ------------------------------------------------ caixa da operação
            double overdraft = Math.Max(0, -t.CashBeforeOverdraftInterest) * k.OverdraftMonthlyRate / k.DaysPerMonth;
            C("Juros do cheque especial", day, t.OverdraftInterest, overdraft);
            double cashEnd = t.CashBeforeOperations + revenue - (t.Installments ? 0 : added) - loyaltyPerOrder * served - overdraft;
            C("Caixa no fim do dia", day, d.CashEnd, cashEnd);

            recentDemand = recentDemand <= 0 ? demand : 0.7 * recentDemand + 0.3 * demand;
            recentCapacity = capMax;
        }

        // ================================================= fechamento do mês
        var dre = report.Dre.ToDictionary(v => v.Name, v => v.Value);
        var cash = report.CashFlow.ToDictionary(v => v.Name, v => v.Value);
        var tr = sim.Trace;
        var options = PolicyOptions(sim);

        double grossRevenue = sim.DayResults.Sum(d => d.Revenue);
        double servedTotal = sim.DayResults.Sum(d => d.Serve.Served);
        double otherIncome = tr.Reactions.Where(r => r.line == DreLine.OtherIncome).Sum(r => r.amount);
        double taxes = grossRevenue * k.TaxRate;
        double cmv = sim.DayResults.Sum(d => d.Trace.CmvSold + d.Spoilage + d.Trace.FailureStockLoss);
        double payroll = tr.StaffOnSalaryDay.Sum(kv => kv.Value * cat.Roles[kv.Key].Salary) * k.PayrollCharges * tr.PayrollFactor;
        double rent = region.Rent;
        double marketing = options.Where(o => o.PolicyId != "training" && o.AssetValueRate == 0).Sum(o => o.FixedMonthlyCost)
                         + loyaltyPerOrder * servedTotal;
        double training = options.Where(o => o.PolicyId == "training").Sum(o => o.FixedMonthlyCost);
        double kwh = start.Equipment.Sum(u => cat.Equipment[u.ProfileId].KwhPerMonth * cat.Levels[u.Level].EnergyFactor) * k.EnergyPerKwh;
        double utilities = k.EnergyBase + kwh + k.WaterBase + k.CleaningBase + k.AdminFees
                         + servedTotal * (k.EnergyPerOrder + k.WaterPerOrder + k.CleaningPerOrder);
        double maintenancePolicy = options.Sum(o => o.AssetValueRate) * start.Equipment.Sum(u => u.PurchasePrice);
        double repairs = tr.Reactions.Where(r => r.line == DreLine.MaintenanceAndLosses).Sum(r => r.amount);
        double otherReactions = tr.Reactions.Where(r => r.line == DreLine.OtherExpenses).Sum(r => r.amount);
        double depreciation = start.Equipment.Sum(u => Math.Min(u.BookValue, u.PurchasePrice / cat.Levels[u.Level].LifeMonths));
        double overdraftTotal = sim.DayResults.Sum(d => d.Trace.OverdraftInterest);
        double fees = tr.Reactions.Where(r => r.line == DreLine.Interest).Sum(r => r.amount);
        double interest = tr.LoanInterest + overdraftTotal + fees;

        C("DRE · Receita bruta", 0, dre["GrossRevenue"], grossRevenue);
        C("DRE · Impostos (8%)", 0, dre["Taxes"], taxes);
        C("DRE · CMV (vendas + perdas)", 0, dre["Cmv"], cmv);
        C("DRE · Folha e encargos", 0, dre["Payroll"], payroll);
        C("DRE · Aluguel", 0, dre["Rent"], rent);
        C("DRE · Marketing e fidelização", 0, dre["Marketing"], marketing);
        C("DRE · Energia, água e taxas", 0, dre["Utilities"], utilities);
        C("DRE · Manutenção e reparos", 0, dre["MaintenanceAndLosses"], maintenancePolicy + repairs);
        C("DRE · Outras despesas (treinamento + reações)", 0, dre["OtherExpenses"], training + otherReactions);
        C("DRE · Outras receitas", 0, dre["OtherIncome"], otherIncome);
        C("DRE · Depreciação", 0, dre["Depreciation"], depreciation);
        C("DRE · Juros", 0, dre["Interest"], interest);
        double netIncome = grossRevenue + otherIncome - taxes - cmv - payroll - rent - marketing - utilities
                         - maintenancePolicy - repairs - training - otherReactions - depreciation - interest;
        C("DRE · Lucro líquido", 0, report.NetIncome, netIncome);

        double cashPurchases = sim.DayResults.Where(d => !d.Trace.Installments).Sum(d => d.Trace.InventoryAdded);
        C("Caixa · Vendas", 0, cash["Sales"], grossRevenue);
        C("Caixa · Outras entradas", 0, cash["OtherInflows"], otherIncome);
        C("Caixa · Fornecedores", 0, cash["Suppliers"], -(cashPurchases + tr.SupplierInstallmentsPaid));
        C("Caixa · Salários", 0, cash["Salaries"], -payroll);
        C("Caixa · Aluguel", 0, cash["Rent"], -rent);
        C("Caixa · Contas de consumo", 0, cash["Bills"], -utilities);
        C("Caixa · Impostos", 0, cash["Taxes"], -taxes);
        C("Caixa · Marketing, fidelização e treinamento", 0, cash["Marketing"], -(marketing + training));
        C("Caixa · Manutenção", 0, cash["Maintenance"], -(maintenancePolicy + repairs));
        C("Caixa · Reações a eventos", 0, cash["Events"], -otherReactions);
        C("Caixa · Juros", 0, cash["Interest"], -interest);
        C("Caixa · Amortização", 0, cash["Amortization"], -tr.LoanAmortization);
        double cashResult = grossRevenue + otherIncome - cashPurchases - tr.SupplierInstallmentsPaid - payroll - rent - utilities - taxes
                          - marketing - training - maintenancePolicy - repairs - otherReactions - interest - tr.LoanAmortization;
        C("Caixa · Resultado do mês", 0, report.CashResult, cashResult);
        double cashEndMonth = sim.CashAtStart + cashResult;
        C("Caixa no fim do mês", 0, sim.State.Cash, cashEndMonth);

        // balanço reconstruído a partir do estado inicial
        double inventoryEnd = sim.DayResults.Last().InventoryEnd;
        double equipmentNet = start.Equipment.Sum(u => u.BookValue) - depreciation;
        double installmentPurchases = sim.DayResults.Where(d => d.Trace.Installments).Sum(d => d.Trace.InventoryAdded);
        double payables = start.SupplierPayables + installmentPurchases - tr.SupplierInstallmentsPaid;
        double loan = start.LoanBalance - tr.LoanAmortization;
        var balance = report.Balance.ToDictionary(v => v.Name, v => v.Value);
        C("Balanço · Caixa", 0, balance["Caixa"], Math.Max(0, cashEndMonth));
        C("Balanço · Estoque", 0, balance["Estoque"], inventoryEnd);
        C("Balanço · Equipamentos líquidos", 0, balance["Equipamentos líquidos"], equipmentNet);
        C("Balanço · Cheque especial", 0, balance["Cheque especial"], Math.Max(0, -cashEndMonth));
        C("Balanço · Fornecedores", 0, balance["Fornecedores"], payables);
        C("Balanço · Contas a pagar", 0, balance["Contas a pagar"], 0);
        C("Balanço · Empréstimos", 0, balance["Empréstimos"], loan);
        C("Balanço · Lucros acumulados", 0, balance["Lucros acumulados"], start.RetainedEarnings + netIncome);
        double assets = Math.Max(0, cashEndMonth) + inventoryEnd + equipmentNet;
        double liabilities = Math.Max(0, -cashEndMonth) + payables + loan;
        C("Balanço · Ativo − passivo − patrimônio", 0, report.BalanceDifference, assets - liabilities - (start.Capital + start.RetainedEarnings + netIncome));

        // indicadores e pontuação (Bloco 10)
        double demandTotal = sim.DayResults.Sum(d => d.Demand.Potential);
        double lost = sim.DayResults.Sum(d => d.Serve.LostCapacity + d.Serve.LostStock);
        double npsAvg = sim.DayResults.Sum(d => d.Nps.Total) / k.DaysPerMonth;
        double cmvRatio = grossRevenue > 0 ? cmv / grossRevenue : 0;
        double liquidity = liabilities > 0 ? (Math.Max(0, cashEndMonth) + inventoryEnd) / liabilities : 99;
        C("Indicador · Pedidos atendidos", 0, report.Orders, servedTotal);
        C("Indicador · Pedidos perdidos (fração)", 0, report.LostRatio, demandTotal > 0 ? lost / demandTotal : 0);
        C("Indicador · NPS médio", 0, report.NpsAverage, npsAvg);
        C("Indicador · CMV / receita", 0, report.CmvRatio, cmvRatio);
        C("Indicador · Ticket médio", 0, report.AvgTicket, servedTotal > 0 ? grossRevenue / servedTotal : 0);
        C("Indicador · Liquidez corrente", 0, report.CurrentLiquidity, liquidity);
        bool lateBill = sim.Occurrences.Any(o => o.Definition.Id == EventIds.CashShortfall);
        int score = (netIncome > 0 ? 20 : 0) + (cashEndMonth - sim.CashAtStart > 0 ? 20 : 0) + (npsAvg > 70 ? 15 : 0)
                  + (cmvRatio >= rest.CmvMin && cmvRatio <= rest.CmvMax ? 15 : 0) + (liquidity > 1.2 ? 10 : 0)
                  + ((demandTotal > 0 ? lost / demandTotal : 0) < 0.05 ? 10 : 0) + (!lateBill ? 10 : 0)
                  - (netIncome < 0 ? 20 : 0) - (cashEndMonth < 0 ? 25 : 0) - (npsAvg < 30 ? 15 : 0);
        C("Pontuação do mês", 0, report.Score, score);
        return checks;
    }

    private static List<PolicyOption> PolicyOptions(MonthSimulation sim)
        => sim.Policies.All().Select(p => sim.Catalog.PolicyOption(p.policy, p.option)).ToList();

    private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
}
