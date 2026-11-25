// Game/Domain/Simulation/MonthSimulation.cs
// O orquestrador do mês. Ele não tem fórmula própria: chama os modelos na ordem certa,
// guarda os acumulados do mês e pausa quando um evento pede decisão.
//
// Cada dia tem DUAS fases:
//   1. Abertura  (OpenDay):       contas do dia, falhas, faltas, eventos. Pode pausar.
//   2. Operação  (RunOperations): capacidade, estoque, demanda, vendas, NPS, lançamentos.
// O evento é sorteado na abertura, com o estado de ONTEM, e a reação do aluno vale para a operação de HOJE.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    public enum StepKind { DayCompleted, AwaitingDecision }

    public sealed class DayResult
    {
        public int Day;
        public DemandBreakdown Demand;
        public CapacityResult Capacity;
        public ServeResult Serve;
        public NpsBreakdown Nps;
        public double Quality, Occupancy, ServiceTimeFactor, ErrorRate, AvgTicket, Revenue;
        public double Purchases, Spoilage, CashEnd, InventoryEnd, StockInOrders, Satisfaction, Reputation;
        public double NegativeReviewChanceTomorrow;   // exemplo de evento que "enxerga" o resultado do dia
        public List<string> Events = new List<string>();
        public DayTrace Trace = new DayTrace();       // entradas de cada fórmula, para a conferência
    }

    /// <summary>
    /// Tudo o que as fórmulas do dia usaram como entrada. Não participa de nenhum cálculo: existe para os
    /// testes de conferência e a planilha recalcularem cada número de forma independente.
    /// </summary>
    public sealed class DayTrace
    {
        // equipe e produtividade
        public double Morale, Experience, TrainingAdd, Productivity;
        public bool HasManager;
        public int AttendantsPresent, SpecialistsPresent;
        // capacidade
        public double AttendantCapacity, SpecialistCapacity, KitchenBonus, DiningBonus, PhysicalCapacity;
        public double ServiceAvailability, KitchenAvailability, DiningAvailability, ServiceCapFactor, KitchenCapFactor;
        // demanda
        public double ReputationBefore, SatisfactionBefore, BrandRecall, MarketingReach, MarketingMaxGain;
        public double LoyaltyStrength, CompetitionPressure, CategoryAttractiveness, WeekdayFactor, Seasonality, RegionalIndex;
        // qualidade e preço
        public double EquipmentQualityDelta, QualityLever, RefTicket;
        public Dictionary<string, double> ProductShare = new Dictionary<string, double>();
        // estoque
        public double SupplierCostFactor, CashDiscount, CoverDays, DeliveryFactor, StockUnitCost;
        public bool Installments, RefrigerationDown;
        public double RecentDemandBefore, RecentCapacityBefore, InventoryBefore, InventoryAdded, InventoryAfterSale, CmvSold;
        // experiência do cliente
        public double ErrorsFactor;
        public bool UnresolvedNegative, PremiumBonus;
        // caixa
        public double CashBeforeOperations, CashBeforeOverdraftInterest, OverdraftInterest;
        public double FailureStockLoss;          // perda de estoque das falhas sorteadas na abertura do dia
    }

    /// Contas do calendário registradas para a conferência do fechamento (não participam do cálculo).
    public sealed class MonthTrace
    {
        public Dictionary<RoleType, int> StaffOnSalaryDay = new Dictionary<RoleType, int>();
        public double PayrollFactor = 1;
        public double LoanBalanceBeforePayment, LoanRate, LoanInterest, LoanAmortization;
        public bool LoanPaidOnSchedule;
        public double SupplierInstallmentsPaid;
        public List<(int day, DreLine line, double amount, string what)> Reactions = new List<(int, DreLine, double, string)>();
    }

    public sealed class StepResult
    {
        public StepKind Kind;
        public int Day;
        public DayResult DayResult;                                  // preenchido quando Kind == DayCompleted
        public IReadOnlyList<EventOccurrence> PendingDecisions;      // preenchido quando Kind == AwaitingDecision
    }

    public sealed class ReactionOption
    {
        public ReactionDefinition Reaction;
        public double Cost;
        public bool Enabled;
        public string DisabledReason;
    }

    public sealed class MonthSimulation
    {
        // ---------------------------------------------------------------- dependências
        public SimulationCatalog Catalog { get; }
        public SessionSetup Setup { get; }
        public MonthPolicies Policies { get; }
        private readonly SimulationConstants _k;
        private readonly RegionProfile _region;
        private readonly RestaurantProfile _rest;
        private readonly DeterministicRandom _rng;
        private readonly EventEngine _events;
        private readonly List<PolicyOption> _policyOptions = new List<PolicyOption>();

        // ---------------------------------------------------------------- estado de trabalho
        public SimulationState State { get; }                        // cópia: o estado salvo não muda até o Commit
        public Accounting Books { get; }
        public int Month => State.Month;
        public int Day { get; private set; }                         // dia atual (aberto ou concluído)
        public int AbsDay => Days.Abs(Month, Day, _k.DaysPerMonth);
        public double CashAtStart { get; }
        public double ReputationAtStart { get; }
        public List<DayResult> DayResults { get; } = new List<DayResult>();
        public MonthTrace Trace { get; } = new MonthTrace();
        private double _failureLossToday;
        public List<EventOccurrence> Occurrences { get; } = new List<EventOccurrence>();
        public MonthlyEventPlan EventPlan { get; }

        private bool _dayOpened;
        private readonly List<EventOccurrence> _pending = new List<EventOccurrence>();
        // registro de cada ocorrência: o mesmo evento pode sair duas vezes no dia (duas unidades paradas, por exemplo)
        private readonly Dictionary<EventOccurrence, EventRecord> _records = new Dictionary<EventOccurrence, EventRecord>();
        private readonly HashSet<string> _absentToday = new HashSet<string>();
        private Observables _x;                                       // observáveis calculados ontem

        // ---------------------------------------------------------------- acumulados do mês
        public double SumDemand, SumServed, SumLostCapacity, SumLostStock, SumWaste, SumCmvSold, SumNps;
        public Dictionary<string, int> BottleneckDays { get; } = new Dictionary<string, int>();
        public double[] SumLogFactors { get; } = new double[8];      // para decompor a variação da demanda

        public bool IsAwaitingDecision => _pending.Count > 0;
        public bool IsFinished => Day >= _k.DaysPerMonth && !_dayOpened && !IsAwaitingDecision;
        public IReadOnlyList<EventOccurrence> PendingDecisions => _pending;
        public bool HasManager => State.Staff.Any(s => s.Role == RoleType.GERENTE);

        public MonthSimulation(SimulationCatalog catalog, SessionSetup setup, SimulationState start, MonthPolicies policies,
            MonthlyEventPlan eventPlan = null)
        {
            Catalog = catalog;
            Setup = setup;
            Policies = policies;
            _k = catalog.Constants;
            _region = catalog.Regions[setup.RegionId];
            _rest = catalog.Restaurants[setup.Restaurant];

            State = start.Clone();
            EventPlan = eventPlan;
            if (eventPlan != null && (eventPlan.Events == null || eventPlan.Events.Count > 5
                || eventPlan.Events.Any(e => e == null)
                || eventPlan.Events.Select(e => MonthlyEventPlanner.CanonicalId(e.EventId)).Distinct(StringComparer.Ordinal).Count() != eventPlan.Events.Count
                || eventPlan.Events.Any(e => e.Day < 1 || e.Day > _k.DaysPerMonth || catalog.Event(e.EventId) == null
                    || start.EventHistory.Any(h => MonthlyEventPlanner.CanonicalId(h.EventId) == MonthlyEventPlanner.CanonicalId(e.EventId)))))
                throw new ArgumentException("O plano mensal contém eventos inválidos ou repetidos.");
            Books = new Accounting(State, _k);
            _rng = new DeterministicRandom(setup.Seed);
            _events = new EventEngine(catalog, _rng);

            foreach (var (policy, option) in policies.All())
                _policyOptions.Add(catalog.PolicyOption(policy, option)
                                   ?? throw new ArgumentException($"Opção '{option}' não existe na política '{policy}'."));

            // Lembrança de marca: atualizada uma vez, no início do mês
            var monthLevers = BuildLevers(Days.Abs(Month, 1, _k.DaysPerMonth));
            State.BrandRecall = ExperienceModel.NextBrandRecall(State.BrandRecall, monthLevers.Add(LeverKeys.MarketingInvestment), _k.BrandRecallCarry);

            CashAtStart = State.Cash;
            ReputationAtStart = State.Reputation;
            _x = BuildObservables(CurrentQuality(monthLevers), 0.04, 1.0, false, monthLevers);
        }

        // ================================================================= API pública
        /// <summary>Avança o mês: abre o próximo dia ou, se o dia já está aberto, roda a operação dele.</summary>
        public StepResult AdvanceDay()
        {
            if (IsAwaitingDecision) throw new InvalidOperationException("Há evento esperando reação. Chame Choose antes.");
            if (IsFinished) throw new InvalidOperationException("O mês terminou. Chame MonthCloser.Close.");

            if (!_dayOpened)
            {
                Day++;
                _dayOpened = true;
                OpenDay();
                if (IsAwaitingDecision)
                    return new StepResult { Kind = StepKind.AwaitingDecision, Day = Day, PendingDecisions = _pending.ToList() };
            }

            var result = RunOperations();
            _dayOpened = false;
            return new StepResult { Kind = StepKind.DayCompleted, Day = Day, DayResult = result };
        }

        /// <summary>One weighted draw before day 1, without replacement across the whole campaign.</summary>
        public MonthlyEventPlan ReserveMonthlyEvents()
        {
            if (Day != 0) throw new InvalidOperationException("Reserve os eventos antes de iniciar o mês.");
            var levers = BuildLevers(Days.Abs(Month, 1, _k.DaysPerMonth));
            var ctx = NewEventContext(levers);
            ctx.Day = Month == 1 ? _k.GraceDaysWithoutNegative + 1 : 1;
            ctx.AbsDay = Days.Abs(Month, ctx.Day, _k.DaysPerMonth);
            var candidates = new List<MonthlyEventCandidate>();
            void Add(EventDefinition def, List<(string id, double weight)> targets, bool equipment)
            {
                double total = targets.Sum(t => t.weight);
                if (total <= 0) return;
                double cursor = _rng.Uniform(Month, 0, "monthly-target-" + def.Id) * total;
                var chosen = targets.Last();
                foreach (var target in targets) { if (cursor < target.weight) { chosen = target; break; } cursor -= target.weight; }
                candidates.Add(new MonthlyEventCandidate { Definition = def, Weight = total,
                    UnitId = equipment ? chosen.id : null, StaffId = equipment ? null : chosen.id });
            }
            foreach (var def in Catalog.Events)
            {
                if (Month < def.MinMonth || Month > def.MaxMonth || State.EventHistory.Any(h =>
                    MonthlyEventPlanner.CanonicalId(h.EventId) == MonthlyEventPlanner.CanonicalId(def.Id))) continue;
                // A real unpaid loan remains a financial decision, not a random narrative draw.
                if (def.Id == EventIds.CashShortfall) continue;
                if (def.Id == EventIds.EquipmentStillDown)
                {
                    Add(def, State.Equipment.Where(u => u.IsBroken).Select(u => (u.UnitId, 1.0)).ToList(), true);
                    continue;
                }
                if (!_events.IsEligible(def, ctx)) continue;
                if (def.Id == EventIds.StaffAbsent || def.Id == EventIds.StaffResigned)
                {
                    Add(def, State.Staff.Select(s => (s.StaffId, _k.DaysPerMonth * (def.Id == EventIds.StaffAbsent
                        ? AbsenceProbability(s) : ResignationProbability(levers)))).ToList(), false);
                }
                else if (def.Scope == EventScope.PerEquipmentUnit)
                {
                    var targets = new List<(string, double)>();
                    foreach (var unit in State.Equipment.Where(u => !u.IsBroken))
                    {
                        var category = Catalog.Categories[unit.Category];
                        if (category.FailureEventId != def.Id) continue;
                        var risk = ReliabilityModel.DailyHazard(category, Catalog.Levels[unit.Level], unit.AgeMonths,
                            State.LastOccupancy, unit.FailureAbsDays.Count(d => d > ctx.AbsDay - 60),
                            State.MonthsWithoutPreventive, levers.Factor(LeverKeys.FailureHr), HasManager ? _k.ManagerRiskHr : 1.0, _k);
                        targets.Add((unit.UnitId, risk.Probability * _k.DaysPerMonth));
                    }
                    Add(def, targets, true);
                }
                else if (def.Scope == EventScope.Global)
                    candidates.Add(new MonthlyEventCandidate { Definition = def, Weight = _events.DailyHazard(def, ctx) * _k.DaysPerMonth });
            }
            return MonthlyEventPlanner.Draw(Catalog, Month, _rng, candidates, State.EventHistory.Select(h => h.EventId), State.LoanBalance > .01);
        }

        /// <summary>Reações de um evento, com custo calculado e o motivo quando estão desabilitadas.</summary>
        public List<ReactionOption> ReactionOptions(EventOccurrence occ)
        {
            var list = new List<ReactionOption>();
            foreach (var r in occ.Definition.Reactions)
            {
                double cost = ReactionCost(occ, r);
                var option = new ReactionOption { Reaction = r, Cost = cost, Enabled = true };
                double headroom = State.Cash + _k.OverdraftLimit;       // caixa + cheque especial
                if (r.UsesOverdraft) cost = occ.PendingInterest + occ.PendingAmortization;
                if (r.Renegotiates && Month >= _k.MonthsPerMatch)
                { option.Enabled = false; option.DisabledReason = "Não há meses seguintes para renegociar."; }
                else if (!r.UsesOverdraft && cost > 0 && cost > headroom)          // reação sem custo (ou com ganho) nunca é bloqueada
                { option.Enabled = false; option.DisabledReason = $"Caixa insuficiente: custa R$ {cost:N0}, disponível R$ {headroom:N0}."; }
                list.Add(option);
            }
            return list;
        }

        /// <summary>Aplica a reação escolhida no popup. Depois disso, AdvanceDay continua o mesmo dia.</summary>
        public void Choose(string occurrenceId, string reactionId)
        {
            var occ = _pending.FirstOrDefault(o => o.OccurrenceId == occurrenceId)
                      ?? throw new ArgumentException("Ocorrência não está pendente: " + occurrenceId);
            var option = ReactionOptions(occ).FirstOrDefault(o => o.Reaction.Id == reactionId)
                         ?? throw new ArgumentException("Reação inexistente: " + reactionId);
            if (!option.Enabled) throw new InvalidOperationException(option.DisabledReason);
            var r = option.Reaction;

            // 1. custo da reação (vira DRE e caixa)
            if (r.UsesOverdraft)
            {
                Books.PayLoan(Day, occ.PendingInterest, occ.PendingAmortization);   // paga mesmo ficando negativo
                Trace.LoanInterest = occ.PendingInterest; Trace.LoanAmortization = occ.PendingAmortization;
            }
            else if (r.Renegotiates)
            {
                Books.PayLoan(Day, occ.PendingInterest, 0);                         // só juros agora
                Trace.LoanInterest = occ.PendingInterest; Trace.LoanAmortization = 0;
                double fee = State.LoanBalance * _k.RenegotiationFee;
                Books.PayExpense(Day, DreLine.Interest, CashLine.Interest, fee, "tarifa de renegociação");
                Trace.Reactions.Add((Day, DreLine.Interest, fee, "renegociação"));
            }
            else if (option.Cost < 0)
            {
                Books.ReceiveOtherIncome(Day, -option.Cost, occ.Definition.Title + ": " + r.Label);  // custo negativo = ganho
                Trace.Reactions.Add((Day, DreLine.OtherIncome, -option.Cost, occ.Definition.Id + "/" + r.Id));
            }
            else if (option.Cost > 0)
            {
                bool repair = r.RepairsUnit;
                Books.PayExpense(Day, repair ? DreLine.MaintenanceAndLosses : DreLine.OtherExpenses,
                                 repair ? CashLine.Maintenance : CashLine.Events, option.Cost,
                                 occ.Definition.Title + ": " + r.Label);
                Trace.Reactions.Add((Day, repair ? DreLine.MaintenanceAndLosses : DreLine.OtherExpenses, option.Cost, occ.Definition.Id + "/" + r.Id));
            }

            // 2. consequências diretas
            if (r.RepairsUnit && occ.UnitId != null) Repair(State.Equipment.First(u => u.UnitId == occ.UnitId));
            if (occ.Definition.Id == EventIds.StaffAbsent && r.KeepsStaff) _absentToday.Remove(occ.StaffId);
            if (occ.Definition.Id == EventIds.StaffResigned)
            {
                var person = State.Staff.First(s => s.StaffId == occ.StaffId);
                if (r.KeepsStaff) { person.Morale = _k.MoraleTarget; person.ExperienceBonus = 0; }  // substituto contratado
                else State.Staff.Remove(person);                                                   // vaga fica aberta
            }

            // 3. efeitos com duração (alavancas)
            ApplyEffects(r.Effects, occ);

            // 4. registro
            // a reação vai para o registro desta ocorrência (antes ia para o último registro do mesmo evento no dia,
            // e a primeira de duas ocorrências iguais ficava sem reação gravada)
            if (!_records.TryGetValue(occ, out var record))
                record = State.EventHistory.Last(e => e.EventId == occ.Definition.Id && e.AbsDay == AbsDay);
            _records.Remove(occ);
            record.ReactionId = r.Id;
            record.Cost = r.UsesOverdraft || r.Renegotiates ? 0 : option.Cost;
            occ.ChosenReactionId = r.Id;
            _pending.Remove(occ);
        }

        // ================================================================= FASE 1: abertura do dia
        private void OpenDay()
        {
            _absentToday.Clear();
            _failureLossToday = 0;
            var levers = BuildLevers(AbsDay);
            var ctx = NewEventContext(levers);
            var deterministic = new List<EventOccurrence>();

            PayScheduledBills(levers, ctx, deterministic);     // calendário de contas (e "Caixa insuficiente")

            if (EventPlan != null)
            {
                foreach (var occurrence in deterministic) Register(occurrence);
                foreach (var reservation in EventPlan.Events.Where(e => e.Day == Day))
                    RevealReservedEvent(reservation, ctx);
                return;
            }

            var drawn = new List<EventOccurrence>();
            DrawEquipmentFailures(levers, ctx, drawn, deterministic);
            drawn.AddRange(_events.DrawPerEmployee(ctx, EventIds.StaffAbsent, AbsenceProbability));
            drawn.AddRange(_events.DrawPerEmployee(ctx, EventIds.StaffResigned, p => ResignationProbability(levers)));
            drawn.AddRange(_events.DrawGlobal(ctx));

            int usedSlots = deterministic.Count(o => o.RequiresDecision);
            foreach (var occ in deterministic.Concat(_events.ApplyDailyLimits(drawn, usedSlots)))
                Register(occ);
        }

        private void RevealReservedEvent(MonthlyEventReservation reservation, EventContext ctx)
        {
            var definition = Catalog.Event(reservation.EventId);
            var occurrence = _events.NewOccurrence(definition, ctx, 1, new List<(string, double)>());
            if (reservation.StaffId != null)
            {
                var person = State.Staff.FirstOrDefault(s => s.StaffId == reservation.StaffId) ?? State.Staff.FirstOrDefault();
                if (person == null) return;
                occurrence.StaffId = person.StaffId;
                occurrence.OccurrenceId += "-" + person.StaffId;
            }
            if (reservation.UnitId != null)
            {
                var unit = State.Equipment.First(u => u.UnitId == reservation.UnitId);
                occurrence.UnitId = unit.UnitId;
                occurrence.OccurrenceId += "-" + unit.UnitId;
                if (definition.Id != EventIds.EquipmentStillDown && !unit.IsBroken)
                {
                    unit.IsBroken = true; unit.BrokenSinceAbsDay = AbsDay; unit.FailureAbsDays.Add(AbsDay);
                    var category = Catalog.Categories[unit.Category];
                    if (category.StockLossOnFailure > 0)
                    {
                        double before = State.InventoryValue;
                        Books.Spoil(Day, State.InventoryValue * category.StockLossOnFailure * Catalog.Levels[unit.Level].StockLossFactor,
                            "perda na falha: " + unit.ProfileId);
                        _failureLossToday += before - State.InventoryValue;
                    }
                }
            }
            Register(occurrence);
        }

        private void PayScheduledBills(LeverSet levers, EventContext ctx, List<EventOccurrence> deterministic)
        {
            int d = Day;
            if (d == _k.MarketingDay)
                foreach (var o in _policyOptions.Where(o => o.FixedMonthlyCost > 0 && o.AssetValueRate == 0))
                    Books.PayExpense(d, o.PolicyId == "training" ? DreLine.OtherExpenses : DreLine.Marketing,
                                     CashLine.Marketing, o.FixedMonthlyCost, o.Label);

            if (d == _k.RentDay)
                Books.PayExpense(d, DreLine.Rent, CashLine.Rent, _region.Rent, "aluguel");

            foreach (var inst in State.Installments.Where(i => i.DueAbsDay == AbsDay).ToList())
            {
                Trace.SupplierInstallmentsPaid += inst.Amount;
                Books.PaySupplierInstallment(d, inst);
                State.Installments.Remove(inst);
            }

            if (d == _k.EnergyDay)
            {
                double kwhCost = State.Equipment.Sum(u => Catalog.Equipment[u.ProfileId].KwhPerMonth
                                                          * Catalog.Levels[u.Level].EnergyFactor) * _k.EnergyPerKwh;
                Books.PayExpense(d, DreLine.Utilities, CashLine.Bills, _k.EnergyBase + kwhCost, "energia (fixa + equipamentos)");
                Books.PayBills(d, _k.EnergyPerOrder * ExpectedOrdersInMonth(), "energia (projeção por pedido)");
            }

            if (d == _k.MaintenanceDay)
                foreach (var o in _policyOptions.Where(o => o.AssetValueRate > 0))
                    Books.PayExpense(d, DreLine.MaintenanceAndLosses, CashLine.Maintenance,
                                     o.AssetValueRate * State.Equipment.Sum(u => u.PurchasePrice), o.Label);

            if (d == _k.LoanDay && State.LoanBalance > 0.01)
            {
                var line = Catalog.CreditLines[State.CreditLineId];
                int monthsLeft = Math.Max(1, _k.MonthsPerMatch - Month + 1);
                double interest = line.MonthlyRate * State.LoanBalance;
                double amortization = State.LoanBalance / monthsLeft;
                Trace.LoanBalanceBeforePayment = State.LoanBalance; Trace.LoanRate = line.MonthlyRate;
                if (State.Cash >= interest + amortization)
                {
                    Books.PayLoan(d, interest, amortization);
                    Trace.LoanInterest = interest; Trace.LoanAmortization = amortization; Trace.LoanPaidOnSchedule = true;
                }
                else
                {   // consequência determinística: o aluno escolhe cheque especial ou renegociação
                    var occ = _events.NewOccurrence(Catalog.Event(EventIds.CashShortfall), ctx, 1.0,
                        new List<(string, double)> { ("parcela", interest + amortization), ("caixa", State.Cash) });
                    occ.PendingInterest = interest;
                    occ.PendingAmortization = amortization;
                    deterministic.Add(occ);
                }
            }

            if (d == _k.WaterDay)
            {
                Books.PayExpense(d, DreLine.Utilities, CashLine.Bills, _k.WaterBase + _k.CleaningBase + _k.AdminFees, "água, limpeza e taxas (fixas)");
                Books.PayBills(d, (_k.WaterPerOrder + _k.CleaningPerOrder) * ExpectedOrdersInMonth(), "água e limpeza (projeção por pedido)");
            }

            if (d == _k.SalaryDay)
            {
                double payroll = State.Staff.Sum(s => Catalog.Roles[s.Role].Salary) * _k.PayrollCharges * levers.Factor(LeverKeys.PayrollFactor);
                foreach (var g in State.Staff.GroupBy(s => s.Role)) Trace.StaffOnSalaryDay[g.Key] = g.Count();
                Trace.PayrollFactor = levers.Factor(LeverKeys.PayrollFactor);
                Books.PayExpense(d, DreLine.Payroll, CashLine.Salaries, payroll, "salários e encargos");
            }

            if (d == _k.TaxDay) Books.PayTaxes(d);
        }

        private void DrawEquipmentFailures(LeverSet levers, EventContext ctx, List<EventOccurrence> drawn, List<EventOccurrence> deterministic)
        {
            for (int i = 0; i < State.Equipment.Count; i++)
            {
                var u = State.Equipment[i];
                var category = Catalog.Categories[u.Category];
                var level = Catalog.Levels[u.Level];

                if (u.IsBroken)
                {   // lembrete a cada 10 dias parado (determinístico)
                    int stopped = AbsDay - u.BrokenSinceAbsDay;
                    if (stopped > 0 && stopped % _k.StoppedReminderEveryDays == 0)
                    {
                        var reminder = _events.NewOccurrence(Catalog.Event(EventIds.EquipmentStillDown), ctx, 1.0,
                            new List<(string, double)> { ("dias parado", stopped) });
                        reminder.UnitId = u.UnitId;
                        reminder.OccurrenceId += "-" + u.UnitId;
                        deterministic.Add(reminder);
                    }
                    continue;                                            // unidade parada não falha de novo
                }

                var hz = ReliabilityModel.DailyHazard(category, level, u.AgeMonths + (Day - 1) / 30.0, State.LastOccupancy,
                    u.FailureAbsDays.Count(f => f > AbsDay - 60), State.MonthsWithoutPreventive,
                    levers.Factor(LeverKeys.FailureHr), HasManager ? _k.ManagerRiskHr : 1.0, _k);

                if (!_rng.Happens(hz.Probability, Month, Day, "failure", i)) continue;

                // A falha acontece fisicamente, mesmo que o popup seja descartado pelo limite do dia
                u.IsBroken = true;
                u.BrokenSinceAbsDay = AbsDay;
                u.FailureAbsDays.Add(AbsDay);
                if (category.StockLossOnFailure > 0)
                {
                    double before = State.InventoryValue;
                    Books.Spoil(Day, State.InventoryValue * category.StockLossOnFailure * level.StockLossFactor, "perda na falha: " + u.ProfileId);
                    _failureLossToday += before - State.InventoryValue;
                }

                var def = category.FailureEventId == null ? null : Catalog.Event(category.FailureEventId);
                if (def == null) continue;                                // Preparo e Salão: sem evento próprio
                var occ = _events.NewOccurrence(def, ctx, hz.Probability, new List<(string, double)>
                {
                    ("taxa base por dia", hz.BasePerDay), ("nível", hz.Level), ("idade", hz.Age), ("uso", hz.Usage),
                    ("falhas recentes", hz.History), ("meses sem preventiva", hz.MaintenanceGap),
                    ("política de manutenção", hz.Policy), ("gerente", hz.Management)
                });
                occ.UnitId = u.UnitId;
                occ.OccurrenceId += "-" + u.UnitId;
                drawn.Add(occ);
            }
        }

        private void Register(EventOccurrence occ)
        {
            Occurrences.Add(occ);
            var record = new EventRecord
            {
                EventId = occ.Definition.Id, AbsDay = AbsDay, Severity = occ.Definition.Severity, IsNegative = occ.Definition.IsNegative
            };
            State.EventHistory.Add(record);
            _records[occ] = record;
            if (occ.Definition.Id == EventIds.StaffAbsent) _absentToday.Add(occ.StaffId);
            ApplyEffects(occ.Definition.AutomaticEffects, occ);          // efeito que acontece com ou sem decisão
            if (occ.RequiresDecision) _pending.Add(occ);
        }

        // ================================================================= FASE 2: operação do dia
        private DayResult RunOperations()
        {
            var levers = BuildLevers(AbsDay);
            var dr = new DayResult { Day = Day };
            var t = dr.Trace;
            dr.Events.AddRange(Occurrences.Where(o => o.Day == Day).Select(o => o.Definition.Title));
            t.CashBeforeOperations = State.Cash;
            t.ReputationBefore = State.Reputation;
            t.SatisfactionBefore = State.Satisfaction;

            // --- qualidade e disponibilidade dos equipamentos
            double quality = CurrentQuality(levers);
            double kitchenAvail = 1, serviceAvail = 1, diningAvail = 1;
            bool refrigerationDown = false;
            foreach (var u in State.Equipment.Where(u => u.IsBroken))
            {
                var c = Catalog.Categories[u.Category];
                kitchenAvail *= c.KitchenFactorWhenDown;
                serviceAvail *= c.ServiceFactorWhenDown;
                diningAvail *= c.DiningFactorWhenDown;
                refrigerationDown |= u.Category == EquipmentReliabilityCategory.Refrigeration;
            }

            // --- equipe presente e produtividade
            var present = State.Staff.Where(s => !_absentToday.Contains(s.StaffId)).ToList();
            double morale = present.Count > 0 ? present.Average(s => s.Morale) : _k.MoraleTarget;
            double experience = present.Count > 0 ? present.Average(s => s.ExperienceBonus) : 0;
            double productivity = OperationsModel.Productivity(morale, experience, HasManager, levers.Add(LeverKeys.ProductivityAdd), _k);
            t.Morale = morale; t.Experience = experience; t.HasManager = HasManager;
            t.TrainingAdd = levers.Add(LeverKeys.ProductivityAdd); t.Productivity = productivity;
            t.ServiceAvailability = serviceAvail; t.KitchenAvailability = kitchenAvail; t.DiningAvailability = diningAvail;
            t.RefrigerationDown = refrigerationDown;

            // --- capacidade: o menor de três números
            var working = State.Equipment.Where(u => !u.IsBroken).Select(u => Catalog.Equipment[u.ProfileId]).ToList();
            int attendants = present.Count(s => s.Role == RoleType.ATENDENTE);
            int specialists = present.Count(s => s.Role == _rest.SpecialistRole);
            var cap = new CapacityResult
            {
                Service = attendants * _rest.AttendantCapacityPerDay * productivity * serviceAvail * levers.Factor(LeverKeys.ServiceCapFactor),
                Kitchen = (specialists * Catalog.Roles[_rest.SpecialistRole].KitchenCapacityPerDay * productivity
                           + working.Sum(e => e.KitchenCapacityBonus)) * kitchenAvail * levers.Factor(LeverKeys.KitchenCapFactor),
                Dining = (_region.PhysicalCapacityPerDay + working.Sum(e => e.DiningCapacityBonus)) * diningAvail
            };
            t.AttendantsPresent = attendants; t.SpecialistsPresent = specialists;
            t.AttendantCapacity = _rest.AttendantCapacityPerDay; t.SpecialistCapacity = Catalog.Roles[_rest.SpecialistRole].KitchenCapacityPerDay;
            t.KitchenBonus = working.Sum(e => e.KitchenCapacityBonus); t.DiningBonus = working.Sum(e => e.DiningCapacityBonus);
            t.PhysicalCapacity = _region.PhysicalCapacityPerDay;
            t.ServiceCapFactor = levers.Factor(LeverKeys.ServiceCapFactor); t.KitchenCapFactor = levers.Factor(LeverKeys.KitchenCapFactor);

            // --- encaixe de mercado e mix de pratos (depende de preço e qualidade de hoje)
            var fit = MarketModel.MarketFit(Catalog, _region, _rest, Setup.TargetSegment, Setup.Menu, quality, levers.Add(LeverKeys.MarketingReach));
            double avgTicket = 0, refTicket = 0, unitCost = 0;
            foreach (var p in _rest.Products)
            {
                double share = fit.ProductShare[p.Id];
                avgTicket += share * Setup.PriceOf(p.Id);
                refTicket += share * p.ReferencePrice;
                unitCost += share * p.UnitCost;
            }
            bool installments = levers.Add(LeverKeys.PayInInstallments) > 0;
            double costFactor = levers.Factor(LeverKeys.SuppliesCostFactor);
            double stockUnitCost = unitCost * costFactor * (installments ? 1 : 1 - _k.CashDiscountRate);
            t.EquipmentQualityDelta = State.Equipment.Count > 0 ? State.Equipment.Average(u => Catalog.Levels[u.Level].QualityDelta) : 0;
            t.QualityLever = levers.Add(LeverKeys.QualityAdd);
            t.MarketingReach = levers.Add(LeverKeys.MarketingReach);
            t.RefTicket = refTicket;
            foreach (var kv in fit.ProductShare) t.ProductShare[kv.Key] = kv.Value;
            t.SupplierCostFactor = costFactor; t.Installments = installments; t.CashDiscount = _k.CashDiscountRate;
            t.StockUnitCost = stockUnitCost;

            // --- estoque: compra do dia (cobre N dias do menor entre demanda esperada e capacidade recente)
            t.RecentDemandBefore = State.RecentDemand; t.RecentCapacityBefore = State.RecentCapacity;
            t.InventoryBefore = State.InventoryValue;
            t.CoverDays = levers.Add(LeverKeys.StockCoverDays); t.DeliveryFactor = levers.Factor(LeverKeys.DeliveryFactor);
            double expected = Math.Min(State.RecentDemand, State.RecentCapacity > 0 ? State.RecentCapacity : cap.Max);
            double targetValue = levers.Add(LeverKeys.StockCoverDays) * expected * stockUnitCost;
            double purchaseAtList = Math.Max(0, targetValue - State.InventoryValue) / (installments ? 1 : 1 - _k.CashDiscountRate);
            purchaseAtList *= levers.Factor(LeverKeys.DeliveryFactor);               // 0 = fornecedor atrasado
            Books.BuySupplies(Day, AbsDay, purchaseAtList, installments);
            dr.Purchases = purchaseAtList;
            double stockInOrders = State.InventoryValue / stockUnitCost;
            t.InventoryAdded = State.InventoryValue - t.InventoryBefore;

            // --- demanda potencial do dia (oito fatores)
            var dm = new DemandBreakdown
            {
                Base = _region.BaseDemandPerDay,
                Fit = fit.Fit,
                Competition = MarketModel.CompetitionFactor(fit.CategoryAttractiveness,
                                  _region.CompetitionPressure + levers.Add(LeverKeys.CompetitionAdd)),
                Reputation = MarketModel.ReputationFactor(State.Reputation),
                Loyalty = MarketModel.LoyaltyFactor(_k.LoyaltyBaseStrength + levers.Add(LeverKeys.LoyaltyStrength), State.Satisfaction),
                Marketing = MarketModel.MarketingFactor(levers.Add(LeverKeys.MarketingMaxGain), State.BrandRecall),
                Calendar = MarketModel.CalendarFactor(_region, _k, Month, Day),
                Events = levers.Factor(LeverKeys.DemandFactor),
                Noise = _rng.LogNormal(Month, Day, "demand.noise", _k.NoiseSigma)
            };
            double demand = dm.Potential;
            t.BrandRecall = State.BrandRecall; t.MarketingMaxGain = levers.Add(LeverKeys.MarketingMaxGain);
            t.LoyaltyStrength = _k.LoyaltyBaseStrength + levers.Add(LeverKeys.LoyaltyStrength);
            t.CompetitionPressure = _region.CompetitionPressure + levers.Add(LeverKeys.CompetitionAdd);
            t.CategoryAttractiveness = fit.CategoryAttractiveness;
            t.WeekdayFactor = _region.WeekdayProfile[(Day - 1) % 7];
            t.Seasonality = _k.MonthSeasonality[(Month - 1) % _k.MonthSeasonality.Length];
            t.RegionalIndex = _region.RegionalIndex;

            // --- atendimento
            var serve = OperationsModel.Serve(demand, cap.Max, stockInOrders);
            double revenue = serve.Served * avgTicket;
            Books.Sell(Day, revenue, serve.Served * stockUnitCost);
            t.CmvSold = Math.Min(serve.Served * stockUnitCost, t.InventoryBefore + t.InventoryAdded);
            t.InventoryAfterSale = State.InventoryValue;
            foreach (var o in _policyOptions.Where(o => o.CostPerOrder > 0))
                Books.PayExpense(Day, DreLine.Marketing, CashLine.Marketing, o.CostPerOrder * serve.Served, o.Label + " (por pedido)");
            Books.AccrueBill(Day, (_k.EnergyPerOrder + _k.WaterPerOrder + _k.CleaningPerOrder) * serve.Served, "contas por pedido");

            // --- perecibilidade (refrigeração parada multiplica por 4)
            double spoil = State.InventoryValue * _rest.PerishabilityPerDay * (refrigerationDown ? 4 : 1);
            Books.Spoil(Day, spoil, "perda por validade");

            // --- tempo, erros, NPS, reputação, moral
            double occupancy = cap.Max > 0 ? demand / cap.Max : 2.0;
            double timeFactor = OperationsModel.ServiceTimeFactor(occupancy);
            double errors = OperationsModel.ErrorRate(levers.Factor(LeverKeys.ErrorsFactor), occupancy);
            t.ErrorsFactor = levers.Factor(LeverKeys.ErrorsFactor);
            t.UnresolvedNegative = levers.Add(LeverKeys.UnresolvedNegative) > 0;
            t.PremiumBonus = HasManager && _rest.IsPremium;
            var nps = ExperienceModel.DailyNps(new NpsInput
            {
                ServiceTimeFactor = timeFactor,
                LostStockRatio = demand > 0 ? serve.LostStock / demand : 0,
                PriceLevel = refTicket > 0 ? avgTicket / refTicket : 1,
                Quality = quality,
                ErrorRate = errors,
                UnresolvedNegativeEvent = levers.Add(LeverKeys.UnresolvedNegative) > 0,
                PremiumManagerBonus = HasManager && _rest.IsPremium
            });
            double satisfactionToday = MarketModel.Clamp(nps.Total, 0, 100);
            State.Reputation = ExperienceModel.NextReputation(State.Reputation, satisfactionToday, _k.ReputationLagPerDay);
            State.Satisfaction += _k.SatisfactionMemory * (satisfactionToday - State.Satisfaction);
            foreach (var s in State.Staff)
            {
                s.Morale = ExperienceModel.NextMorale(s.Morale, levers.Add(LeverKeys.MoraleAdd), occupancy > 1.0, _k);
                s.ExperienceBonus = Math.Min(_k.MaxExperienceBonus, s.ExperienceBonus + _k.MaxExperienceBonus / 90.0);
            }

            t.FailureStockLoss = _failureLossToday;
            t.CashBeforeOverdraftInterest = State.Cash;
            Books.ChargeOverdraftInterest(Day);
            t.OverdraftInterest = t.CashBeforeOverdraftInterest - State.Cash;

            // --- memória para amanhã (compra de estoque, uso dos equipamentos)
            State.RecentDemand = State.RecentDemand <= 0 ? demand : 0.7 * State.RecentDemand + 0.3 * demand;
            State.RecentCapacity = cap.Max;
            State.LastOccupancy = occupancy;

            // --- acumulados do mês
            SumDemand += demand; SumServed += serve.Served;
            SumLostCapacity += serve.LostCapacity; SumLostStock += serve.LostStock;
            SumWaste += spoil; SumCmvSold += serve.Served * stockUnitCost; SumNps += nps.Total;
            BottleneckDays[cap.Bottleneck] = BottleneckDays.TryGetValue(cap.Bottleneck, out var n) ? n + 1 : 1;
            double[] f = { dm.Fit, dm.Competition, dm.Reputation, dm.Loyalty, dm.Marketing, dm.Calendar, dm.Events, dm.Noise };
            for (int i = 0; i < f.Length; i++) SumLogFactors[i] += Math.Log(f[i]);

            // --- observáveis que os eventos de AMANHÃ vão enxergar
            _x = BuildObservables(quality, errors, timeFactor, refrigerationDown, levers);

            dr.Demand = dm; dr.Capacity = cap; dr.Serve = serve; dr.Nps = nps;
            dr.Quality = quality; dr.Occupancy = occupancy; dr.ServiceTimeFactor = timeFactor; dr.ErrorRate = errors;
            dr.AvgTicket = avgTicket; dr.Revenue = revenue; dr.Spoilage = spoil; dr.CashEnd = State.Cash;
            dr.InventoryEnd = State.InventoryValue; dr.StockInOrders = stockInOrders;
            dr.Satisfaction = State.Satisfaction; dr.Reputation = State.Reputation;
            var review = Catalog.Event("negative_review");
            if (review != null)
                dr.NegativeReviewChanceTomorrow = 1 - Math.Exp(-_events.DailyHazard(review, new EventContext
                    { Month = Month, Day = Day + 1, AbsDay = AbsDay + 1, Setup = Setup, State = State, X = _x, Levers = levers, HasManager = HasManager }));
            DayResults.Add(dr);
            return dr;
        }

        // ================================================================= auxiliares
        /// Políticas do mês + efeitos ativos hoje = alavancas do dia
        public LeverSet BuildLevers(int absDay)
        {
            var set = new LeverSet();
            foreach (var o in _policyOptions) set.ApplyAll(o.Levers);
            foreach (var e in State.Effects.Where(e => e.ExpiresAfterAbsDay >= absDay))
                set.Apply(new Lever(e.LeverKey, e.LeverValue));
            return set;
        }

        private double CurrentQuality(LeverSet levers)
        {
            double equipmentDelta = State.Equipment.Count > 0 ? State.Equipment.Average(u => Catalog.Levels[u.Level].QualityDelta) : 0;
            return MarketModel.Clamp(0.5 + equipmentDelta + levers.Add(LeverKeys.QualityAdd), 0, 1);
        }

        private Observables BuildObservables(double quality, double errors, double waitFactor, bool refrigerationDown, LeverSet levers)
        {
            var x = new Observables();
            x[Observables.Quality] = quality;
            x[Observables.Satisfaction] = State.Satisfaction;
            x[Observables.Reputation] = State.Reputation;
            x[Observables.Morale] = State.Staff.Count > 0 ? State.Staff.Average(s => s.Morale) : _k.MoraleTarget;
            x[Observables.ErrorRate] = errors;
            x[Observables.WaitFactor] = waitFactor;
            x[Observables.LostRatio] = SumDemand > 0 ? (SumLostCapacity + SumLostStock) / SumDemand : 0;
            x[Observables.WasteRatio] = SumCmvSold + SumWaste > 0 ? SumWaste / (SumCmvSold + SumWaste) : 0;
            x[Observables.RefrigerationDown] = refrigerationDown ? 1 : 0;
            x[Observables.BrandRecall] = State.BrandRecall;
            x[Observables.CompetitionPressure] = _region.CompetitionPressure + levers.Add(LeverKeys.CompetitionAdd);
            x[Observables.PreviousMargin] = State.PreviousMonthMargin;
            x[Observables.MaintenanceGap] = State.MonthsWithoutPreventive;
            x[Observables.StockCoverDays] = levers.Add(LeverKeys.StockCoverDays);
            return x;
        }

        public Observables CurrentObservables => _x;

        private EventContext NewEventContext(LeverSet levers)
        {
            var ctx = new EventContext
            {
                Month = Month, Day = Day, AbsDay = AbsDay, Setup = Setup, State = State, X = _x, Levers = levers, HasManager = HasManager
            };
            foreach (var r in State.EventHistory)
            {
                var def = Catalog.Event(r.EventId);
                if (def?.Group != null && AbsDay - r.AbsDay < def.ActiveDays) ctx.ActiveGroups.Add(def.Group);
            }
            return ctx;
        }

        private double AbsenceProbability(StaffMember s)
            => _k.AbsencePerPersonDay * Math.Exp(0.02 * (_k.MoraleTarget - s.Morale)) * (HasManager ? _k.ManagerAbsenceHr : 1.0);

        private double ResignationProbability(LeverSet levers)
            => 1.0 - Math.Pow(1.0 - Math.Min(0.99, _k.ResignationPerPersonMonth * levers.Factor(LeverKeys.ResignationHr)), 1.0 / _k.DaysPerMonth);

        private double ExpectedOrdersInMonth() => Math.Min(State.RecentDemand, State.RecentCapacity) * _k.DaysPerMonth;

        private double ReactionCost(EventOccurrence occ, ReactionDefinition r)
        {
            var unit = occ.UnitId == null ? null : State.Equipment.First(u => u.UnitId == occ.UnitId);
            var person = occ.StaffId == null ? null : State.Staff.FirstOrDefault(s => s.StaffId == occ.StaffId);
            switch (r.CostKind)
            {
                case ReactionCostKind.Fixed: return r.CostValue;
                case ReactionCostKind.EquipmentRepair:
                    return unit == null ? 0 : r.CostExtra + r.CostValue * Catalog.Levels[unit.Level].RepairFraction * unit.PurchasePrice;
                case ReactionCostKind.EquipmentPriceFraction: return unit == null ? 0 : r.CostExtra + r.CostValue * unit.PurchasePrice;
                case ReactionCostKind.LoanBalanceFraction: return r.CostExtra + r.CostValue * State.LoanBalance;
                case ReactionCostKind.SalaryFraction:
                    return person == null ? 0 : r.CostExtra + r.CostValue * Catalog.Roles[person.Role].Salary;
                default: return 0;
            }
        }

        private void ApplyEffects(IEnumerable<EffectDefinition> effects, EventOccurrence occ)
        {
            int monthEnd = Days.Abs(Month, _k.DaysPerMonth, _k.DaysPerMonth);
            foreach (var e in effects)
            {
                State.Effects.Add(new ActiveEffect
                {
                    SourceId = occ.OccurrenceId,
                    LeverKey = e.Lever.Key,
                    LeverValue = e.Lever.Value,
                    ExpiresAfterAbsDay = e.Duration switch
                    {
                        EffectDuration.Days => AbsDay + e.Days - 1,
                        EffectDuration.EndOfMonth => monthEnd,
                        _ => int.MaxValue                                   // fim da partida ou até o conserto
                    },
                    UntilRepairUnitId = e.Duration == EffectDuration.UntilRepair ? occ.UnitId : null
                });
            }
        }

        private void Repair(EquipmentUnit unit)
        {
            unit.IsBroken = false;
            State.Effects.RemoveAll(e => e.UntilRepairUnitId == unit.UnitId);
        }
    }
}
