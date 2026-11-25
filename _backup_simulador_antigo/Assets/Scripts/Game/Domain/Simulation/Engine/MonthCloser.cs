using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    /// <summary>
    /// Fechamento mensal (Bloco 9): DRE, fluxo de caixa, balanco, ponto de equilibrio,
    /// indicadores, pontuacao (Bloco 10), atribuicao de fatores da demanda e feedback.
    /// Todos os numeros sao lidos dos lancamentos do mes; nada e preenchido a mao.
    /// </summary>
    internal sealed class MonthCloser
    {
        private readonly SimulationCatalog _catalog;
        private readonly SimulationConfig _cfg;
        private readonly MonthInputs _in;
        private readonly SimulationState _state;
        private readonly MonthlyReport _report;
        private readonly Ledger _ledger;
        private readonly double _loanBalanceEnd;
        private readonly double _reputationEnd;
        private readonly int _reputationStart;
        private readonly double _principalPaid;

        public MonthCloser(SimulationCatalog catalog, MonthInputs inputs, SimulationState state, MonthlyReport report, Ledger ledger,
            double loanBalanceEnd, double reputationEnd, int reputationStart, double principalPaid)
        {
            _catalog = catalog;
            _cfg = catalog.Config;
            _in = inputs;
            _state = state;
            _report = report;
            _ledger = ledger;
            _loanBalanceEnd = loanBalanceEnd;
            _reputationEnd = reputationEnd;
            _reputationStart = reputationStart;
            _principalPaid = principalPaid;
        }

        public void Run()
        {
            FinishIncomeStatement();
            BuildCashResult();
            BuildBalanceSheet();
            BuildIndicators();
            BuildDemandAttribution();
            BuildScorecard();
            Round();
            _report.feedback = FeedbackComposer.Compose(_report, _catalog, _in, _in.PreviousReport, _cfg);
        }

        private void FinishIncomeStatement() => CompleteTotals(_report.income);

        /// <summary>Calcula as linhas de subtotal da DRE. Chamar uma unica vez por objeto (soma as multas em outras despesas).</summary>
        internal static void CompleteTotals(IncomeStatement i)
        {
            i.netRevenue = i.grossRevenue - i.salesTaxes;
            i.grossProfit = i.netRevenue - i.cmv;
            i.maintenanceAndLosses = i.preventiveMaintenance + i.repairs + i.spoilageLoss + i.eventStockLoss + i.assetWriteOffs;
            i.otherExpenses += i.fines;
            i.financialExpenses = i.loanInterest + i.overdraftInterest;
            i.operatingResult = i.grossProfit - i.payroll - i.rent - i.marketing - i.utilities - i.maintenanceAndLosses
                                - i.otherExpenses + i.otherIncome - i.depreciation;
            i.netIncome = i.operatingResult - i.financialExpenses;
        }

        private void BuildCashResult()
        {
            var flow = _report.cashFlow;
            var days = _report.days;
            double served = days.Sum(d => d.served);
            var r = _report.cashResult;
            r.grossRevenue = flow.salesReceipts;
            r.supplyCost = flow.suppliers;
            r.rent = flow.rent;
            r.salaries = flow.payroll;
            r.utilities = flow.utilities;
            r.loanPayment = flow.loanInterest + flow.loanPrincipal + flow.overdraftInterest;
            r.loanPrincipalPayment = flow.loanPrincipal;
            r.otherOperatingCosts = flow.taxes + flow.marketing + flow.maintenance + flow.otherOperating;
            r.eventCashImpact = flow.otherReceipts - flow.eventPayments;
            r.openingCash = flow.openingCash;
            r.closingCash = flow.closingCash;
            r.netResult = flow.closingCash - flow.openingCash;
            r.customers = (int)Math.Floor(served + 1e-7d);
            r.baseDemand = (int)Math.Floor(_catalog.Location(_in.LocationId).BaseDailyDemand * _cfg.calendar.operatingDays);
            r.potentialDemand = (int)Math.Floor(days.Sum(d => d.potentialDemand) + 1e-7d);
            r.serviceCapacity = (int)Math.Floor(days.Sum(d => d.capacity) + 1e-7d);
            r.averageTicket = served > 0d ? flow.salesReceipts / served : 0d;
            r.reputationAtStart = _reputationStart;
            r.reputationAtEnd = (int)Math.Round(_reputationEnd, MidpointRounding.AwayFromZero);
            r.fitFactor = GeoMean(days.Select(d => d.fFit));
            var events = _report.events;
            r.singleEventId = events.Count == 1 ? events[0].eventId : null;

            double check = r.grossRevenue - (r.supplyCost + r.rent + r.salaries + r.utilities + r.loanPayment + r.otherOperatingCosts) + r.eventCashImpact;
            if (Math.Abs(check - r.netResult) > 0.05d)
                throw new InvalidOperationException("Fluxo de caixa inconsistente: " + check + " x " + r.netResult);
        }

        private void BuildBalanceSheet()
        {
            var b = _report.balance;
            double cash = _ledger.Cash;
            b.cash = Math.Max(0d, cash);
            b.overdraft = Math.Max(0d, -cash);
            b.inventory = _state.stockValue;
            b.equipmentNet = _state.units.Sum(u => u.BookValue);
            b.suppliersPayable = _state.payables.Where(p => p.kind == PayableKind.Supplier).Sum(p => p.amount);
            b.billsPayable = _state.payables.Where(p => p.kind == PayableKind.Bill).Sum(p => p.amount);
            b.loans = _loanBalanceEnd;
            b.capital = _in.InitialCapital;
            b.capitalAdjustment = _state.capitalAdjustment;
            b.retainedEarnings = _state.retainedEarnings + _report.income.netIncome;
            if (Math.Abs(b.Difference) > 1d)
                _report.diagnostics.Add("Balanco com diferenca de " + b.Difference.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void BuildIndicators()
        {
            var i = _report.income;
            var b = _report.balance;
            var x = _report.indicators;
            var days = _report.days;
            var f = _cfg.finance;
            var restaurant = _catalog.Restaurant(_in.RestaurantTypeId);
            double served = days.Sum(d => d.served);
            double demand = days.Sum(d => d.potentialDemand);
            double capacity = days.Sum(d => d.capacity);

            x.averageTicket = served > 0d ? i.grossRevenue / served : 0d;
            x.cmvPercent = i.netRevenue > 0d ? i.cmv / i.netRevenue * 100d : 0d;
            double perMealUtilities = (f.energyPerMeal + f.waterPerMeal + f.cleaningPerMeal) * served;
            double variable = i.cmv + perMealUtilities;
            x.contributionMarginPercent = i.netRevenue > 0d ? (i.netRevenue - variable) / i.netRevenue * 100d : 0d;
            x.unitContributionMargin = served > 0d ? (i.netRevenue - variable) / served : 0d;
            x.capacityOccupancy = capacity > 0d ? served / capacity * 100d : 0d;
            x.lostOrders = Math.Max(0d, demand - served);
            x.lostOrdersShare = demand > 0d ? x.lostOrders / demand * 100d : 0d;
            x.lostToCapacity = days.Sum(d => d.lostCapacity);
            x.lostToStock = days.Sum(d => d.lostStock);
            var open = days.Where(d => d.served > 0d).ToList();
            x.npsAverage = open.Count > 0 ? open.Average(d => d.nps) : -100d;
            x.satisfactionAverage = open.Count > 0 ? open.Average(d => d.satisfaction) : 0d;
            x.waitAverage = served > 0d ? days.Sum(d => d.waitMinutes * d.served) / served : 0d;
            double currentLiabilities = b.CurrentLiabilities;
            x.currentLiquidity = currentLiabilities > 0.01d ? b.CurrentAssets / currentLiabilities : 99d;
            x.quickLiquidity = currentLiabilities > 0.01d ? (b.CurrentAssets - b.inventory) / currentLiabilities : 99d;
            x.immediateLiquidity = currentLiabilities > 0.01d ? b.cash / currentLiabilities : 99d;
            x.indebtedness = b.TotalAssets > 0.01d ? b.TotalLiabilities / b.TotalAssets * 100d : 0d;

            double fixedUtilities = i.utilities - perMealUtilities;
            double policyOther = i.otherExpenses - i.fines;
            double fixedCash = i.payroll + i.rent + i.marketing + Math.Max(0d, fixedUtilities) + i.preventiveMaintenance + Math.Max(0d, policyOther) + i.loanInterest;
            double fixedTotal = fixedCash + i.depreciation;
            double desired = f.desiredMonthlyReturnOnCapital * Math.Max(0d, _in.InitialCapital);
            if (x.unitContributionMargin > 0d)
            {
                x.breakEvenAccounting = fixedTotal / x.unitContributionMargin;
                x.breakEvenFinancial = fixedCash / x.unitContributionMargin;
                x.breakEvenEconomic = (fixedTotal + desired) / x.unitContributionMargin;
            }
            else
            {
                // -1 = nao ha ponto de equilibrio (margem de contribuicao nao positiva). Evita "Infinity" no JSON.
                x.breakEvenAccounting = x.breakEvenFinancial = x.breakEvenEconomic = -1d;
            }
            x.workingCapitalNeed = b.inventory - b.suppliersPayable - b.billsPayable;
            x.recommendedCashReserve = fixedCash + _report.cashFlow.loanPrincipal;
            int teamSize = _in.Team?.Values.Where(v => v > 0).Sum() ?? 0;
            x.teamProductivity = teamSize > 0 ? served / (teamSize * (double)_cfg.calendar.operatingDays) : 0d;
            x.moraleEnd = _state.morale;
            double losses = i.spoilageLoss + i.eventStockLoss;
            x.wasteShare = i.cmv + losses > 0d ? losses / (i.cmv + losses) * 100d : 0d;
            x.interestCoverage = i.financialExpenses > 0.01d ? i.operatingResult / i.financialExpenses : 99d;
            x.overdueBills = _ledger.OverdueBills;
            x.overdraftDays = _ledger.OverdraftDays;
            x.fitIndex = GeoMean(days.Select(d => d.fFit));
            x.competitiveness = 100d * GeoMean(days.Select(d => d.fFit * d.fCompetition));
            x.operationalRisk = OperationalRisk();
        }

        /// <summary>
        /// Indice de risco operacional 0..100 = 100 x E / (E + 5), em que E e o numero esperado de
        /// eventos negativos no proximo mes, ponderado pela severidade, nas condicoes do ultimo dia.
        /// 50 = cerca de cinco eventos negativos de severidade 1 esperados por mes.
        /// </summary>
        private double OperationalRisk()
        {
            var last = _report.days.LastOrDefault();
            if (last == null) return 0d;
            var indicators = new IndicatorSet();
            indicators[Indicator.Satisfaction] = _state.satisfaction;
            indicators[Indicator.SatisfactionToday] = last.satisfaction;
            indicators[Indicator.Reputation] = _reputationEnd;
            indicators[Indicator.Morale] = _state.morale;
            indicators[Indicator.Utilization] = last.utilization;
            indicators[Indicator.Quality] = last.quality;
            indicators[Indicator.WaitRatio] = last.waitMinutes / Math.Max(1d, _catalog.Restaurant(_in.RestaurantTypeId).Profile.serviceStandardMinutes);
            indicators[Indicator.Errors] = last.errorRate;
            indicators[Indicator.Competition] = _catalog.Location(_in.LocationId).CompetitionIndex;
            var ctx = new EventContext { Config = _cfg, Indicators = indicators, State = _state, Levers = new LeverSet(), Month = _in.Month, Day = 1, AbsDay = 9999 };
            double expected = 0d;
            foreach (var d in _cfg.events.Where(e => e.IsNegative && e.kind == EventKind.Probabilistic && e.scope == EventScope.Global))
                expected += EventEngine.RatePerDay(d, ctx, null) * 30d * d.severity;
            bool preventive = _report.policies.Any(p => p.StartsWith("maintenance=", StringComparison.Ordinal) && !p.EndsWith("=corrective", StringComparison.Ordinal));
            foreach (var unit in _state.units)
            {
                var category = _cfg.EquipmentCategory(unit.categoryId);
                if (category == null || string.IsNullOrEmpty(category.failureEventId)) continue;
                var hazard = ReliabilityModel.Hazard(category, _catalog.Tier(unit.tierId), _cfg.reliability, unit.ageMonths, last.utilization,
                    0, _state.monthsWithoutPreventive, preventive, preventive ? 0.55d : 1d, unit.hazardRatio, 1d);
                expected += hazard.RatePerDay * 30d * 1.5d;
            }
            return 100d * expected / (expected + 5d);
        }

        private void BuildDemandAttribution()
        {
            var days = _report.days;
            if (days.Count == 0) return;
            var factors = new List<(string id, string label, double value)>
            {
                ("fit", "encaixe entre restaurante, público da região e preços", GeoMean(days.Select(d => d.fFit))),
                ("competition", "concorrência local frente à qualidade percebida", GeoMean(days.Select(d => d.fCompetition))),
                ("reputation", "reputação", GeoMean(days.Select(d => d.fReputation))),
                ("loyalty", "satisfação acumulada (retorno de clientes)", GeoMean(days.Select(d => d.fLoyalty))),
                ("marketing", "marketing", GeoMean(days.Select(d => d.fMarketing))),
                ("calendar", "dias da semana, sazonalidade e região", GeoMean(days.Select(d => d.fCalendar))),
                ("events", "eventos do mês", GeoMean(days.Select(d => d.fEvents))),
                ("noise", "variação aleatória do movimento", GeoMean(days.Select(d => d.fNoise)))
            };
            double baseDaily = days[0].fBase;
            double avgDemand = days.Average(d => d.potentialDemand);
            double change = baseDaily > 0d ? avgDemand / baseDaily - 1d : 0d;
            _report.demandChangePercent = change * 100d;
            double totalLog = factors.Sum(fx => Math.Log(Math.Max(1e-9d, fx.value)));
            foreach (var fx in factors)
            {
                double log = Math.Log(Math.Max(1e-9d, fx.value));
                double points = Math.Abs(totalLog) > 1e-6d ? log / totalLog * change * 100d : log * 100d;
                _report.demandFactors.Add(new FactorContribution { factor = fx.id, label = fx.label, currentValue = fx.value, baselineValue = 1d, contributionPoints = points });
            }
        }

        private void BuildScorecard()
        {
            var s = _cfg.scoring;
            var x = _report.indicators;
            var i = _report.income;
            var card = _report.scorecard;
            var restaurant = _catalog.Restaurant(_in.RestaurantTypeId).Profile;
            void Add(bool condition, string criterion, int points)
            {
                if (!condition) return;
                card.lines.Add(new ScorecardLine { criterion = criterion, points = points });
                card.total += points;
            }
            double cmvShare = x.cmvPercent / 100d;
            bool negativeCash = _report.cashFlow.closingCash < 0d || x.overdraftDays > 0;
            Add(i.netIncome > 0d, "Lucro líquido positivo", s.netProfitPositive);
            Add(_report.cashResult.netResult > 0d, "Fluxo de caixa do mês positivo", s.cashFlowPositive);
            Add(x.npsAverage > 70d, "NPS médio acima de 70", s.npsAbove70);
            Add(cmvShare >= restaurant.cmvStandardMin && cmvShare <= restaurant.cmvStandardMax, "CMV dentro do padrão do restaurante", s.cmvWithinStandard);
            Add(x.currentLiquidity > 1.2d, "Liquidez corrente maior que 1,2", s.currentLiquidityAbove12);
            Add(x.lostOrdersShare < 5d, "Pedidos perdidos abaixo de 5% da demanda", s.lostOrdersBelow5Percent);
            Add(x.overdueBills == 0, "Nenhuma conta paga além do limite de crédito", s.noOverdueBills);
            Add(i.netIncome < 0d, "Prejuízo líquido", s.netLoss);
            Add(negativeCash, "Caixa negativo", s.negativeCash);
            Add(x.npsAverage < 30d, "NPS abaixo de 30", s.npsBelow30);
            card.classification = card.total >= 80 ? "Gestão excelente"
                : card.total >= 60 ? "Gestão adequada, com pontos de atenção"
                : card.total >= 40 ? "Gestão instável"
                : card.total >= 0 ? "Gestão em risco"
                : "Crise financeira e operacional";
        }

        private void Round()
        {
            var i = _report.income;
            i.grossRevenue = SimMath.Money(i.grossRevenue);
            i.salesTaxes = SimMath.Money(i.salesTaxes);
            i.netRevenue = SimMath.Money(i.netRevenue);
            i.cmv = SimMath.Money(i.cmv);
            i.grossProfit = SimMath.Money(i.grossProfit);
            i.payroll = SimMath.Money(i.payroll);
            i.rent = SimMath.Money(i.rent);
            i.marketing = SimMath.Money(i.marketing);
            i.utilities = SimMath.Money(i.utilities);
            i.maintenanceAndLosses = SimMath.Money(i.maintenanceAndLosses);
            i.otherExpenses = SimMath.Money(i.otherExpenses);
            i.otherIncome = SimMath.Money(i.otherIncome);
            i.depreciation = SimMath.Money(i.depreciation);
            i.operatingResult = SimMath.Money(i.operatingResult);
            i.financialExpenses = SimMath.Money(i.financialExpenses);
            i.netIncome = SimMath.Money(i.netIncome);
        }

        internal static double GeoMean(IEnumerable<double> values)
        {
            double sum = 0d;
            int n = 0;
            foreach (double v in values)
            {
                sum += Math.Log(Math.Max(1e-9d, v));
                n++;
            }
            return n > 0 ? Math.Exp(sum / n) : 1d;
        }
    }
}
