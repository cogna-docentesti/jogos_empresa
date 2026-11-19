using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Game.Domain.Simulation
{
    /// <summary>Escolhe reacoes automaticamente (testes, balanceamento, previsao).</summary>
    public interface IReactionPolicy
    {
        string Choose(PendingDecision decision, IndicatorSet indicators);
    }

    /// <summary>
    /// Simula um mes dia a dia. Nao acessa banco, nao usa UnityEngine e nao altera as entradas.
    ///
    /// Uso pela tela:
    ///   var sim = new MonthSimulation(catalog, inputs);
    ///   while (sim.Status != SimulationStatus.Finished) {
    ///       sim.Advance();                       // roda ate o fim do dia ou ate uma decisao
    ///       if (sim.Status == SimulationStatus.AwaitingDecision)
    ///           sim.Resolve(sim.Pending.OccurrenceId, reacaoEscolhida);
    ///   }
    ///   var outcome = sim.Close();
    ///
    /// Ordem do dia: (1) efeitos expiram, (2) equipamentos podem falhar, (3) eventos sao
    /// sorteados, (4) o aluno reage, (5) operacao: demanda, capacidade, estoque, vendas,
    /// custos, satisfacao, reputacao, contas do dia.
    /// </summary>
    public sealed class MonthSimulation
    {
        public const string ReminderEventId = "equipment_still_broken";
        public const string PreventiveSaveEventId = "preventive_maintenance_save";

        private sealed class Occurrence
        {
            public string Id;
            public EventDefinition Definition;
            public int Day;
            public int AbsDay;
            public string UnitKey;
            public string RoleId;
            public double Probability;
            public List<string> Drivers = new List<string>();
            public bool Resolved;
            public string ReactionId;
            public string ReactionTitle;
            public double CashImpact;
            public double ReputationImpact;
            public bool Automatic;
        }

        private readonly SimulationCatalog _catalog;
        private readonly SimulationConfig _cfg;
        private readonly MonthInputs _in;
        private readonly SimulationState _state;
        private readonly DeterministicRandom _rng;
        private readonly RestaurantSpec _restaurant;
        private readonly LocationSpec _location;
        private readonly LeverSet _policyLevers = new LeverSet();
        private readonly Ledger _ledger;
        private readonly MonthlyReport _report = new MonthlyReport();
        private readonly List<Occurrence> _occurrences = new List<Occurrence>();
        private readonly List<Occurrence> _pending = new List<Occurrence>();
        private readonly Dictionary<string, int> _team = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _absentToday = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly List<CalendarEntry> _todayEntries = new List<CalendarEntry>();
        private readonly List<string> _policyLabels = new List<string>();

        private LeverSet _levers = new LeverSet();
        private IndicatorSet _indicators = new IndicatorSet();
        private int _day = 1;
        private bool _dayStarted;
        private int _occurrenceCounter;

        private double _reputation;
        private readonly int _reputationStart;
        private double _loanBalance;
        private double _loanPrincipalDue;
        private double _loanInterestDue;
        private double _maintenanceMonthly;
        private double _supplierQuality;
        private double _menuMinPrice;
        private double _dailyGoal;
        private double _mixUnitCost;

        // Valores do dia anterior, lidos pelos indicadores do dia seguinte.
        private double _yUtilization = 0.8d;
        private double _ySatisfaction;
        private double _yQuality = 0.5d;
        private double _yWaitRatio = 1d;
        private double _yErrors;
        private double _yLostShare;

        public string Status { get; private set; } = SimulationStatus.Running;
        public int CurrentDay => _day;
        public int Month => _in.Month;
        public int OperatingDays => _cfg.calendar.operatingDays;
        public double Cash => _ledger.Cash;
        public IReactionPolicy AutoPolicy { get; set; }
        public IReadOnlyList<DayResult> Days => _report.days;
        public IndicatorSet CurrentIndicators => _indicators;
        public SimulationCatalog Catalog => _catalog;
        public MonthInputs Inputs => _in;

        public PendingDecision Pending => _pending.Count > 0 ? BuildPending(_pending[0]) : null;

        /// <summary>Fluxo de caixa acumulado ate agora (copia; a tela so le).</summary>
        public CashFlowStatement CashFlowSoFar() => _ledger.Flow.Clone();

        /// <summary>
        /// DRE parcial ate o dia atual, para o "Ver DRE detalhada" durante o mes. Contas mensais
        /// entram pelo valor ja incorrido (competencia diaria); a depreciacao so entra no fechamento.
        /// </summary>
        public IncomeStatement PartialIncome()
        {
            var partial = _report.income.Clone();
            MonthCloser.CompleteTotals(partial);
            return partial;
        }

        /// <summary>Ocorrencias do mes ate agora (para a lista "Ultimos acontecimentos").</summary>
        public IReadOnlyList<EventOccurrenceRecord> OccurrencesSoFar() => _occurrences.Select(ToRecord).ToList();

        private EventOccurrenceRecord ToRecord(Occurrence occurrence)
        {
            return new EventOccurrenceRecord
            {
                occurrenceId = occurrence.Id,
                eventId = occurrence.Definition.id,
                title = occurrence.Definition.title,
                impact = occurrence.Definition.impact,
                category = occurrence.Definition.category,
                month = _in.Month,
                day = occurrence.Day,
                unitKey = occurrence.UnitKey,
                reactionId = occurrence.ReactionId,
                reactionTitle = occurrence.ReactionTitle,
                cashImpact = SimMath.Money(occurrence.CashImpact),
                reputationImpact = (int)Math.Round(occurrence.ReputationImpact),
                probability = occurrence.Probability,
                automatic = occurrence.Automatic,
                explanation = occurrence.Drivers.Count > 0 ? string.Join("; ", occurrence.Drivers) : null
            };
        }

        public MonthSimulation(SimulationCatalog catalog, MonthInputs inputs, IReactionPolicy autoPolicy = null)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _in = inputs ?? throw new ArgumentNullException(nameof(inputs));
            _cfg = catalog.Config ?? throw new InvalidOperationException("Catalogo sem SimulationConfig.");
            AutoPolicy = autoPolicy;
            if (_in.Month < 1 || _in.Month > _cfg.calendar.months)
                throw new InvalidOperationException("Mes fora do ciclo: " + _in.Month);
            if (string.IsNullOrEmpty(_in.SessionId)) throw new InvalidOperationException("Sessao sem id.");
            SimMath.RequireFinite(_in.OpeningCash, "Caixa inicial");
            if (_in.LoanBalance < 0d) throw new InvalidOperationException("Saldo de emprestimo negativo.");

            _restaurant = catalog.Restaurant(_in.RestaurantTypeId);
            _location = catalog.Location(_in.LocationId);
            _state = _in.State != null ? _in.State.Clone() : CreateInitialState();
            if (_state.lastClosedMonth >= _in.Month)
                throw new InvalidOperationException("O mes " + _in.Month + " ja foi fechado nesta sessao.");
            _rng = new DeterministicRandom(_in.Seed);
            _reputation = SimMath.Clamp(_in.OpeningReputation, 0, 100);
            _reputationStart = _in.OpeningReputation;
            _loanBalance = _in.LoanBalance;
            _ledger = new Ledger(_in.OpeningCash, _cfg.finance.overdraftLimit);
            _ySatisfaction = _state.satisfaction;

            _report.sessionId = _in.SessionId;
            _report.month = _in.Month;
            _report.configVersion = _cfg.versionLabel;
            _report.restaurantTypeId = _in.RestaurantTypeId;
            _report.locationId = _in.LocationId;
            _report.targetSegmentId = _in.TargetSegmentId;
            _report.cashFlow = _ledger.Flow;

            ValidatePrices();
            SyncEquipment();
            SyncTeam();
            ApplyPolicies();
            ReconcileOpeningEquity();
            ScheduleMonth();
            RebuildLevers();
            if (_state.expectedDailyDemand <= 0d) _state.expectedDailyDemand = EstimateDailyDemand();
            _dailyGoal = 0d;
        }

        // =================================================================
        //  CONTROLE DO TEMPO
        // =================================================================

        /// <summary>Avanca ate terminar o dia atual ou ate surgir uma decisao.</summary>
        public DayResult Advance()
        {
            if (Status == SimulationStatus.Finished) throw new InvalidOperationException("O mes ja terminou.");
            if (!_dayStarted)
            {
                BeginDay();
                _dayStarted = true;
            }
            ResolveAutomatically();
            if (_pending.Count > 0)
            {
                Status = SimulationStatus.AwaitingDecision;
                return null;
            }
            var result = OperateDay();
            _dayStarted = false;
            _day++;
            Status = _day > OperatingDays ? SimulationStatus.Finished : SimulationStatus.Running;
            return result;
        }

        /// <summary>Roda o mes inteiro com a politica automatica (exige AutoPolicy).</summary>
        public MonthOutcome RunToEnd()
        {
            if (AutoPolicy == null) AutoPolicy = new DefaultReactionPolicy();
            while (Status != SimulationStatus.Finished) Advance();
            return Close();
        }

        public IReadOnlyList<ReactionOption> OptionsFor(string occurrenceId)
        {
            var occurrence = _pending.Find(o => o.Id == occurrenceId) ?? throw new InvalidOperationException("Ocorrencia nao esta pendente: " + occurrenceId);
            return BuildPending(occurrence).Options;
        }

        public void Resolve(string occurrenceId, string reactionId)
        {
            var occurrence = _pending.Find(o => o.Id == occurrenceId) ?? throw new InvalidOperationException("Ocorrencia nao esta pendente: " + occurrenceId);
            var option = BuildPending(occurrence).Options.Find(o => o.ReactionId == reactionId)
                         ?? throw new InvalidOperationException("Reacao inexistente: " + reactionId);
            if (!option.Enabled) throw new InvalidOperationException("Reacao indisponivel: " + option.DisabledReason);
            ApplyReaction(occurrence, occurrence.Definition.Reaction(reactionId), option.Cost, automatic: false);
            _pending.Remove(occurrence);
            Status = _pending.Count > 0 ? SimulationStatus.AwaitingDecision : SimulationStatus.Running;
        }

        private void ResolveAutomatically()
        {
            foreach (var occurrence in _pending.ToList())
            {
                bool auto = !occurrence.Definition.requiresDecision || AutoPolicy != null;
                if (!auto) continue;
                var pending = BuildPending(occurrence);
                string choice = !occurrence.Definition.requiresDecision
                    ? DefaultChoice(pending)
                    : AutoPolicy.Choose(pending, _indicators);
                var option = pending.Options.Find(o => o.ReactionId == choice && o.Enabled)
                             ?? pending.Options.Where(o => o.Enabled).OrderBy(o => o.Cost).FirstOrDefault();
                if (option == null)
                {
                    occurrence.Resolved = true;
                    occurrence.Automatic = true;
                    _pending.Remove(occurrence);
                    continue;
                }
                ApplyReaction(occurrence, occurrence.Definition.Reaction(option.ReactionId), option.Cost, automatic: !occurrence.Definition.requiresDecision);
                _pending.Remove(occurrence);
            }
        }

        private static string DefaultChoice(PendingDecision pending)
        {
            string id = pending.Definition.defaultReactionId;
            if (!string.IsNullOrEmpty(id) && pending.Options.Any(o => o.ReactionId == id && o.Enabled)) return id;
            return pending.Options.Where(o => o.Enabled).OrderBy(o => o.Cost).Select(o => o.ReactionId).FirstOrDefault();
        }

        // =================================================================
        //  PREPARACAO DO MES
        // =================================================================

        private SimulationState CreateInitialState()
        {
            return new SimulationState
            {
                satisfaction = _cfg.experience.initialSatisfaction,
                morale = _cfg.staff.baseMorale,
                lastMonthMinMenuPrice = 0d
            };
        }

        private void ValidatePrices()
        {
            double min = double.MaxValue;
            foreach (var product in _restaurant.Products)
            {
                double price = product.ReferencePrice;
                if (_in.MenuPrices != null && _in.MenuPrices.TryGetValue(product.Id, out double saved)) price = saved;
                else _report.diagnostics.Add("Produto sem preco salvo, usando o de referencia: " + product.Id);
                if (price < product.MinPrice - 0.005d || price > product.MaxPrice + 0.005d)
                    _report.diagnostics.Add("Preco fora da faixa do produto " + product.Id + ": " + price.ToString("0.00", CultureInfo.InvariantCulture));
                min = Math.Min(min, price);
            }
            _menuMinPrice = min == double.MaxValue ? 0d : min;
        }

        private int AbsDay(int day) => (_in.Month - 1) * OperatingDays + day;

        /// <summary>Dia da semana (0 = segunda) de um dia do mes atual, para a tela montar o calendario.</summary>
        public int WeekdayOf(int day) => (_cfg.calendar.firstWeekday + AbsDay(day) - 1) % 7;

        private void SyncEquipment()
        {
            var owned = new List<string>();
            foreach (var id in _in.EquipmentOwned ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(id)) owned.Add(id);

            // Unidades vendidas/removidas no hub: baixa do valor contabil.
            foreach (var unit in _state.units.ToList())
            {
                int keep = owned.Count(id => id == unit.equipmentId);
                int have = _state.units.Count(u => u.equipmentId == unit.equipmentId);
                if (have > keep)
                {
                    _report.income.assetWriteOffs += unit.BookValue;
                    _report.diagnostics.Add("Equipamento removido no hub: " + unit.equipmentId + " (baixa de " + SimMath.Money(unit.BookValue).ToString("0.00", CultureInfo.InvariantCulture) + ").");
                    _state.units.Remove(unit);
                }
            }

            // Compras feitas no hub: novas unidades pelo preco do catalogo.
            foreach (var group in owned.GroupBy(id => id))
            {
                if (!_catalog.Equipment.TryGetValue(group.Key, out var spec))
                {
                    _report.diagnostics.Add("Equipamento fora do catalogo ignorado: " + group.Key);
                    continue;
                }
                int have = _state.units.Count(u => u.equipmentId == group.Key);
                for (int n = have; n < group.Count(); n++)
                {
                    _state.units.Add(new EquipmentUnitState
                    {
                        unitKey = group.Key + "#" + (n + 1).ToString(CultureInfo.InvariantCulture),
                        equipmentId = group.Key,
                        tierId = spec.TierId,
                        categoryId = spec.CategoryId,
                        purchaseCost = spec.Cost,
                        hazardRatio = 1d
                    });
                }
            }
        }

        private void SyncTeam()
        {
            foreach (var pair in _in.Team ?? new Dictionary<string, int>())
            {
                if (pair.Value < 0) throw new InvalidOperationException("Quantidade negativa no cargo " + pair.Key);
                if (pair.Value == 0) continue;
                if (!_catalog.Roles.ContainsKey(pair.Key)) { _report.diagnostics.Add("Cargo fora do catalogo ignorado: " + pair.Key); continue; }
                int last = _state.lastTeam.Find(t => t.roleId == pair.Key)?.count ?? -1;
                // Se o aluno mudou a quantidade no RH, as vagas abertas por demissao daquele cargo foram resolvidas por ele.
                if (last >= 0 && last != pair.Value) _state.SetVacancies(pair.Key, 0);
                _team[pair.Key] = pair.Value;
            }
            foreach (var vacancy in _state.vacancies.ToList())
            {
                int qty = _team.TryGetValue(vacancy.roleId, out int q) ? q : 0;
                if (vacancy.count > qty) _state.SetVacancies(vacancy.roleId, qty);
            }
        }

        private void ApplyPolicies()
        {
            foreach (var category in _cfg.policies)
            {
                string chosen = _in.Policies != null && _in.Policies.TryGetValue(category.id, out string id) ? id : category.defaultOptionId;
                var option = category.Option(chosen) ?? category.Option(category.defaultOptionId) ?? category.options.FirstOrDefault();
                if (option == null) continue;
                string source = category.displayName + ": " + option.displayName;
                _policyLevers.ApplyAll(option.modifiers, source);
                _policyLabels.Add(category.id + "=" + option.id);
                _report.decisions.Add(new DecisionRecord { month = _in.Month, day = 1, category = category.id, optionId = option.id, description = source });
                if (category.id == "supplier")
                    _supplierQuality = option.modifiers.Where(m => m.key == Lever.QualityBonus).Sum(m => m.value);
            }
            _report.policies = _policyLabels.ToList();
            bool preventive = _policyLevers.Sum(Lever.PreventiveMaintenance) > 0d;
            if (preventive) _state.monthsWithoutPreventive = 0;
        }

        private double EquityNow(double cash)
        {
            double assets = cash + _state.stockValue + _state.units.Sum(u => u.BookValue);
            double liabilities = _loanBalance + _state.payables.Sum(p => p.amount);
            return assets - liabilities;
        }

        private void ReconcileOpeningEquity()
        {
            double actual = EquityNow(_in.OpeningCash);
            if (!_state.hasOpeningEquity)
            {
                _state.capitalAdjustment = actual - _in.InitialCapital;
                if (Math.Abs(_state.capitalAdjustment) > 0.5d)
                    _report.diagnostics.Add("Ajuste de abertura (gastos antes do mes 1 fora da simulacao): " + SimMath.Money(_state.capitalAdjustment).ToString("0.00", CultureInfo.InvariantCulture));
                _state.hasOpeningEquity = true;
            }
            else
            {
                // Baixas de equipamentos removidos ja entram na DRE do mes; o restante e movimento externo.
                double expected = _state.expectedOpeningEquity - _report.income.assetWriteOffs;
                double difference = actual - expected;
                if (Math.Abs(difference) > 0.5d)
                {
                    _state.capitalAdjustment += difference;
                    _report.diagnostics.Add("Movimentos no hub fora da simulacao ajustaram o patrimonio em " + SimMath.Money(difference).ToString("0.00", CultureInfo.InvariantCulture));
                }
            }
        }

        private void ScheduleMonth()
        {
            var f = _cfg.finance;
            _ledger.Bill("RENT", "Aluguel", CashLine.Rent, f.rentDueDay, "rent");
            _ledger.Bill("PAYROLL", "Salários", CashLine.Payroll, f.payrollDueDay, "payroll");
            _ledger.Bill("ENERGY", "Energia elétrica", CashLine.Utilities, f.energyDueDay, "energy");
            _ledger.Bill("WATER", "Água", CashLine.Utilities, f.waterDueDay, "water");
            _ledger.Bill("CLEANING", "Limpeza e taxas", CashLine.Utilities, f.taxesDueDay, "cleaning");
            _ledger.Bill("TAXES", "Impostos sobre vendas", CashLine.Taxes, f.taxesDueDay, "tax");
            _ledger.Bill("MARKETING", "Marketing", CashLine.Marketing, f.marketingDueDay, "marketing");
            _ledger.Bill("MAINTENANCE", "Manutenção preventiva", CashLine.Maintenance, f.maintenanceDueDay, "maintenance");
            _ledger.Bill("STAFF_PROGRAMS", "Treinamento", CashLine.OtherOperating, f.payrollDueDay, "training");
            _ledger.Bill("OTHER_POLICIES", "Programas de gestão", CashLine.OtherOperating, f.taxesDueDay, "info");

            double maintenanceRate = _policyLevers.Sum(Lever.MaintenanceCostRate);
            _maintenanceMonthly = maintenanceRate * _state.units.Sum(u => u.purchaseCost);

            // Emprestimo: regra existente (LoanService): principal = saldo / meses restantes, juros sobre o saldo.
            if (_loanBalance > 0d)
            {
                if (string.IsNullOrEmpty(_in.CreditLineId) || !_catalog.Credits.TryGetValue(_in.CreditLineId, out var credit))
                    throw new InvalidOperationException("Ha saldo de emprestimo, mas a linha de credito nao esta no catalogo.");
                int remaining = Math.Max(1, _cfg.calendar.months - _in.Month + 1);
                _loanPrincipalDue = _in.Month >= _cfg.calendar.months ? _loanBalance : _loanBalance / remaining;
                _loanInterestDue = _loanBalance * credit.MonthlyRate;
            }
        }

        private double EstimateDailyDemand()
        {
            var market = MarketModel.Evaluate(BuildMarketQuery(1d, 1d, 1d, QualityIndex(includeLevers: true)), _cfg);
            if (_mixUnitCost <= 0d) _mixUnitCost = market.UnitCost;
            return Math.Max(1d, market.PotentialDemand);
        }

        // =================================================================
        //  INICIO DO DIA: EFEITOS, EQUIPAMENTOS E EVENTOS
        // =================================================================

        private void BeginDay()
        {
            int absDay = AbsDay(_day);
            _todayEntries.Clear();
            _absentToday.Clear();
            ExpireEffects(absDay);
            RebuildLevers();
            _indicators = BuildIndicators(absDay);

            DrawEquipment(absDay);
            var ctx = BuildEventContext(absDay);
            foreach (var fire in EventEngine.DrawDay(_cfg.events, ctx, _report.diagnostics))
                CreateOccurrence(fire.Definition, fire.Probability, fire.Drivers, unitKey: null, roleId: fire.RoleId);
            RebuildLevers();
            // Indicadores depois das falhas e eventos: as condicoes das reacoes (ex.: refrigeracao reserva) leem o estado atual.
            _indicators = BuildIndicators(absDay);
        }

        private void ExpireEffects(int absDay)
        {
            _state.effects.RemoveAll(e =>
                (e.end == EffectEnd.Days && absDay > e.endAbsDay)
                || (e.end == EffectEnd.UnitRepaired && !UnitIsBroken(e.unitKey)));
            foreach (var unit in _state.units)
                if (unit.hazardRatio != 1d && absDay > unit.hazardRatioUntilAbsDay) unit.hazardRatio = 1d;
        }

        private bool UnitIsBroken(string unitKey) => _state.units.Any(u => u.unitKey == unitKey && u.broken);

        private void RebuildLevers()
        {
            var levers = new LeverSet();
            foreach (var c in _policyLevers.Contributions) levers.Apply(new Modifier(c.Key, c.Value), c.Source);
            foreach (var effect in _state.effects)
                levers.ApplyAll(effect.modifiers, effect.label);
            _levers = levers;
        }

        private IEnumerable<EquipmentUnitState> ApplicableUnits()
        {
            foreach (var unit in _state.units)
            {
                if (!_catalog.Equipment.TryGetValue(unit.equipmentId, out var spec)) continue;
                if (!spec.AppliesTo(_restaurant.TypeId)) continue;
                yield return unit;
            }
        }

        private void DrawEquipment(int absDay)
        {
            bool preventive = _levers.Sum(Lever.PreventiveMaintenance) > 0d;
            double policyRatio = _levers.Factor(Lever.EquipmentHazard);
            double management = ManagementRiskRatio();
            var reminder = _cfg.Event(ReminderEventId);

            foreach (var unit in ApplicableUnits().OrderBy(u => u.unitKey, StringComparer.Ordinal).ToList())
            {
                var category = _catalog.Config.EquipmentCategory(unit.categoryId);
                if (category == null) continue;
                var tier = _catalog.Tier(unit.tierId);

                if (unit.broken)
                {
                    int since = Math.Max(unit.brokenSinceAbsDay, unit.lastReminderAbsDay);
                    if (reminder != null && absDay - since >= _cfg.reliability.brokenReminderDays)
                    {
                        unit.lastReminderAbsDay = absDay;
                        CreateOccurrence(reminder, 1d, new List<string> { "equipamento parado há " + (absDay - unit.brokenSinceAbsDay) + " dias" }, unit.unitKey, null);
                    }
                    continue;
                }
                if (category.baseFailuresPerMonth <= 0d) continue;

                var hazard = ReliabilityModel.Hazard(category, tier, _cfg.reliability, unit.ageMonths, _yUtilization,
                    ReliabilityModel.RecentFailures(unit, absDay, _cfg.reliability.recentFailureWindowDays),
                    _state.monthsWithoutPreventive, preventive, policyRatio, unit.hazardRatio, management);
                double u = _rng.Uniform(_in.Month, _day, "equipment", unit.unitKey);
                if (u < hazard.DailyProbability)
                {
                    FailUnit(unit, category, tier, hazard, absDay);
                }
                else if (preventive && policyRatio > 0d && policyRatio < 1d)
                {
                    // Contrafactual com o mesmo sorteio: sem a manutencao preventiva, a unidade teria falhado hoje.
                    double withoutPolicy = SimMath.HazardToProbability(hazard.RatePerDay / policyRatio * (1d + Math.Min(_cfg.reliability.maintenanceGapCap, _cfg.reliability.maintenanceGapPerMonth)));
                    var save = _cfg.Event(PreventiveSaveEventId);
                    if (save != null && u < withoutPolicy)
                    {
                        var counter = _state.eventCounters.Find(c => c.eventId == save.id);
                        if (counter == null || absDay - counter.lastAbsDay >= save.cooldownDays)
                            CreateOccurrence(save, withoutPolicy - hazard.DailyProbability, new List<string> { unit.equipmentId }, unit.unitKey, null);
                    }
                }
            }
        }

        private void FailUnit(EquipmentUnitState unit, EquipmentCategoryProfile category, EquipmentTierProfile tier, HazardBreakdown hazard, int absDay)
        {
            unit.broken = true;
            unit.brokenSinceAbsDay = absDay;
            unit.lastReminderAbsDay = absDay;
            unit.failures++;
            unit.failureDays.Add(absDay);

            double loss = category.stockLossOnFailure * tier.stockLossFactor;
            if (loss > 0d) LoseStock(loss, "Perda de insumos na falha");

            var definition = string.IsNullOrEmpty(category.failureEventId) ? null : _cfg.Event(category.failureEventId);
            var drivers = new List<string>();
            if (hazard.TierRatio > 1.05d) drivers.Add("modelo " + tier.displayName + " (x" + hazard.TierRatio.ToString("0.0", CultureInfo.InvariantCulture) + ")");
            if (hazard.UsageRatio > 1.05d) drivers.Add("uso intenso (x" + hazard.UsageRatio.ToString("0.0", CultureInfo.InvariantCulture) + ")");
            if (hazard.MaintenanceGapRatio > 1.05d) drivers.Add("sem manutenção preventiva (x" + hazard.MaintenanceGapRatio.ToString("0.0", CultureInfo.InvariantCulture) + ")");
            if (hazard.HistoryRatio > 1.05d) drivers.Add("falhas recentes (x" + hazard.HistoryRatio.ToString("0.0", CultureInfo.InvariantCulture) + ")");
            if (hazard.UnitRatio > 1.05d) drivers.Add("reparo provisório anterior (x" + hazard.UnitRatio.ToString("0.0", CultureInfo.InvariantCulture) + ")");
            if (definition == null)
            {
                // Falha sem evento: conserto automatico, pago no dia.
                double cost = tier.repairCostFraction * unit.purchaseCost;
                PayEventCost(cost, CostLine.Maintenance, "Conserto: " + EquipmentName(unit));
                unit.broken = false;
                return;
            }
            CreateOccurrence(definition, hazard.DailyProbability, drivers, unit.unitKey, null);
        }

        private EventContext BuildEventContext(int absDay)
        {
            var ctx = new EventContext
            {
                Config = _cfg,
                Indicators = _indicators,
                State = _state,
                Levers = _levers,
                Rng = _rng,
                Month = _in.Month,
                Day = _day,
                AbsDay = absDay,
                RestaurantTypeId = _restaurant.TypeId,
                LocationId = _location.Id,
                ManagementRiskRatio = ManagementRiskRatio()
            };
            foreach (var pair in _team)
            {
                double present = pair.Value - _state.Vacancies(pair.Key);
                if (present > 0d) ctx.Employees.Add(new EmployeeSlot { RoleId = pair.Key, Count = present });
            }
            foreach (var effect in _state.effects)
                if (!string.IsNullOrEmpty(effect.exclusionGroup)) ctx.ActiveGroups.Add(effect.exclusionGroup);
            foreach (var occurrence in _occurrences.Where(o => o.AbsDay == absDay))
                if (!string.IsNullOrEmpty(occurrence.Definition.exclusionGroup)) ctx.ActiveGroups.Add(occurrence.Definition.exclusionGroup);
            return ctx;
        }

        private double ManagementRiskRatio()
        {
            double ratio = 1d;
            foreach (var pair in _team)
            {
                if (pair.Value - _state.Vacancies(pair.Key) <= 0) continue;
                var role = _catalog.Roles[pair.Key];
                if (role.Profile != null && role.Profile.operationalRiskHazard > 0d) ratio *= role.Profile.operationalRiskHazard;
            }
            return ratio;
        }

        private void CreateOccurrence(EventDefinition definition, double probability, List<string> drivers, string unitKey, string roleId)
        {
            int absDay = AbsDay(_day);
            var occurrence = new Occurrence
            {
                Id = _in.Month.ToString(CultureInfo.InvariantCulture) + "-" + _day.ToString(CultureInfo.InvariantCulture) + "-" + definition.id + "-" + (++_occurrenceCounter).ToString(CultureInfo.InvariantCulture),
                Definition = definition,
                Day = _day,
                AbsDay = absDay,
                UnitKey = unitKey,
                RoleId = roleId,
                Probability = probability,
                Drivers = drivers ?? new List<string>()
            };
            _occurrences.Add(occurrence);

            var counter = _state.Counter(definition.id);
            counter.occurrences++;
            counter.lastAbsDay = absDay;
            if (definition.IsNegative && definition.kind == EventKind.Probabilistic)
                _state.recentNegatives.Add(new RecentEventState { absDay = absDay, severity = definition.severity });

            ApplyEffect(occurrence, definition.baseEffect, definition.title);
            _todayEntries.Add(new CalendarEntry { kind = EntryKind.Event, label = definition.title, amount = 0d, icon = definition.IsNegative ? "event" : "event_positive" });

            if (definition.reactions.Count == 0)
            {
                occurrence.Resolved = true;
                occurrence.Automatic = true;
                return;
            }
            _pending.Add(occurrence);
        }

        // =================================================================
        //  REACOES E EFEITOS
        // =================================================================

        private PendingDecision BuildPending(Occurrence occurrence)
        {
            var definition = occurrence.Definition;
            var unit = occurrence.UnitKey != null ? _state.units.Find(u => u.unitKey == occurrence.UnitKey) : null;
            var pending = new PendingDecision
            {
                OccurrenceId = occurrence.Id,
                Definition = definition,
                Month = _in.Month,
                Day = occurrence.Day,
                UnitKey = occurrence.UnitKey,
                UnitName = unit != null ? EquipmentName(unit) : null,
                Probability = occurrence.Probability,
                ImpactSummary = occurrence.Drivers.Count > 0 ? "Fatores: " + string.Join("; ", occurrence.Drivers) : null
            };
            foreach (var reaction in definition.reactions)
            {
                double cost = ReactionCost(reaction, unit, occurrence.RoleId);
                var option = new ReactionOption
                {
                    ReactionId = reaction.id,
                    Title = reaction.title,
                    Description = reaction.description,
                    Cost = cost,
                    Enabled = true
                };
                if (!_indicators.Satisfies(reaction.conditions, out string failed))
                {
                    option.Enabled = false;
                    option.DisabledReason = "Indisponível: " + failed;
                }
                else if (cost > 0d && cost > _ledger.Available(reaction.financeable) + 0.005d)
                {
                    option.Enabled = false;
                    option.DisabledReason = reaction.financeable ? "Caixa e cheque especial insuficientes" : "Caixa insuficiente (não pode ser financiada)";
                }
                else if (cost > 0d && cost > _ledger.Cash)
                {
                    option.UsesOverdraft = true;
                }
                pending.Options.Add(option);
            }
            return pending;
        }

        private double ReactionCost(EventReaction reaction, EquipmentUnitState unit, string roleId)
        {
            double cost = Math.Max(0d, reaction.cost);
            if (reaction.costAsLoanBalanceFraction > 0d) cost += reaction.costAsLoanBalanceFraction * _loanBalance;
            if (unit != null)
            {
                var tier = _catalog.Tier(unit.tierId);
                if (reaction.equipmentAction == EquipmentAction.Replace)
                {
                    var targetTier = _catalog.Tier(string.IsNullOrEmpty(reaction.replaceTier) ? unit.tierId : reaction.replaceTier);
                    double standardPrice = unit.purchaseCost / Math.Max(0.01d, tier.priceFactor);
                    cost += standardPrice * targetTier.priceFactor;
                }
                else if (reaction.costAsEquipmentPriceFraction > 0d)
                {
                    cost += reaction.costAsEquipmentPriceFraction * tier.repairCostFraction * unit.purchaseCost;
                }
            }
            // Recrutamento: fechar uma vaga custa uma fracao do salario do cargo envolvido.
            if (roleId != null && reaction.chargesRecruitment && _catalog.Roles.TryGetValue(roleId, out var role))
                cost += role.Salary * _cfg.staff.recruitmentCostInSalaries;
            return SimMath.Money(cost);
        }

        private void ApplyReaction(Occurrence occurrence, EventReaction reaction, double cost, bool automatic)
        {
            if (reaction == null) return;
            var unit = occurrence.UnitKey != null ? _state.units.Find(u => u.unitKey == occurrence.UnitKey) : null;
            if (cost > 0d)
            {
                string line = reaction.costLine;
                if (reaction.equipmentAction == EquipmentAction.Replace) line = CostLine.Capex;
                PayEventCost(cost, line, reaction.title);
                occurrence.CashImpact -= cost;
            }

            if (unit != null)
            {
                switch (reaction.equipmentAction)
                {
                    case EquipmentAction.Repair:
                    case EquipmentAction.TemporaryRepair:
                        unit.broken = false;
                        break;
                    case EquipmentAction.Replace:
                        _report.income.assetWriteOffs += unit.BookValue;
                        unit.tierId = string.IsNullOrEmpty(reaction.replaceTier) ? unit.tierId : reaction.replaceTier;
                        unit.purchaseCost = cost;
                        unit.accumulatedDepreciation = 0d;
                        unit.ageMonths = 0d;
                        unit.broken = false;
                        unit.failures = 0;
                        unit.failureDays.Clear();
                        unit.hazardRatio = 1d;
                        break;
                }
            }

            ApplyEffect(occurrence, reaction.effect, occurrence.Definition.title + " (" + reaction.title + ")");
            occurrence.Resolved = true;
            occurrence.ReactionId = reaction.id;
            occurrence.ReactionTitle = reaction.title;
            occurrence.Automatic = automatic;
            if (reaction.id.Contains("promotion") || reaction.id.Contains("promotional") || reaction.id.Contains("combo") || reaction.id.Contains("offer"))
                _state.lastPromotionAbsDay = AbsDay(_day);
            _report.decisions.Add(new DecisionRecord
            {
                month = _in.Month,
                day = _day,
                category = "event:" + occurrence.Definition.id,
                optionId = reaction.id,
                description = occurrence.Definition.title + " -> " + reaction.title + (automatic ? " (automático)" : string.Empty)
            });
            RebuildLevers();
        }

        private void ApplyEffect(Occurrence occurrence, EffectSpec effect, string label)
        {
            if (effect?.modifiers == null || effect.modifiers.Count == 0) return;
            int absDay = AbsDay(_day);
            var durable = effect.modifiers.Where(x => x != null && !Lever.IsInstant(x.key) && x.key != Lever.UnitHazard).Select(x => new Modifier(x.key, x.value)).ToList();
            var unitHazard = effect.modifiers.Where(x => x != null && x.key == Lever.UnitHazard).ToList();
            if (unitHazard.Count > 0 && occurrence.UnitKey != null)
            {
                var unit = _state.units.Find(u => u.unitKey == occurrence.UnitKey);
                if (unit != null)
                {
                    unit.hazardRatio = unitHazard.Aggregate(1d, (acc, m) => acc * Math.Max(0d, m.value));
                    unit.hazardRatioUntilAbsDay = absDay + Math.Max(1, effect.durationDays) - 1;
                }
            }
            if (durable.Count > 0 || effect.unresolved)
            {
                var active = new ActiveEffectState
                {
                    occurrenceId = occurrence.Id,
                    eventId = occurrence.Definition.id,
                    label = label,
                    modifiers = durable,
                    startAbsDay = absDay,
                    end = string.IsNullOrEmpty(effect.end) ? EffectEnd.Days : effect.end,
                    endAbsDay = absDay + Math.Max(1, effect.durationDays) - 1,
                    unitKey = occurrence.UnitKey,
                    unresolved = effect.unresolved,
                    exclusionGroup = occurrence.Definition.exclusionGroup
                };
                if (active.end == EffectEnd.UnitRepaired && occurrence.UnitKey == null) active.end = EffectEnd.Days;
                _state.effects.Add(active);
                RebuildLevers();
            }
            // Instantaneos depois dos duraveis: uma compra extra com desconto ja usa o preco com desconto.
            foreach (var m in effect.modifiers.Where(x => x != null && Lever.IsInstant(x.key)))
                ApplyInstant(occurrence, m, effect.costLine, label);
        }

        private void ApplyInstant(Occurrence occurrence, Modifier m, string costLine, string label)
        {
            switch (m.key)
            {
                case Lever.CashInstant:
                    if (m.value >= 0d)
                    {
                        _ledger.Receive(CashLine.OtherReceipts, m.value);
                        _report.income.otherIncome += m.value;
                        _todayEntries.Add(new CalendarEntry { kind = EntryKind.Revenue, label = label, amount = m.value, icon = "event_positive" });
                    }
                    else PayEventCost(-m.value, costLine, label);
                    occurrence.CashImpact += m.value;
                    break;
                case Lever.ReputationInstant:
                    _reputation = SimMath.Clamp(_reputation + m.value, 0d, 100d);
                    occurrence.ReputationImpact += m.value;
                    break;
                case Lever.StockLossInstant:
                    LoseStock(m.value, label);
                    break;
                case Lever.StockBoostDays:
                    BuyStock(Math.Max(0d, m.value) * PurchaseBasis(), label, force: true);
                    break;
                case Lever.AbsentInstant:
                    if (occurrence.RoleId != null)
                    {
                        _absentToday.TryGetValue(occurrence.RoleId, out double absent);
                        _absentToday[occurrence.RoleId] = absent + Math.Max(0d, m.value);
                    }
                    break;
                case Lever.VacancyInstant:
                    if (occurrence.RoleId != null)
                    {
                        int qty = _team.TryGetValue(occurrence.RoleId, out int q) ? q : 0;
                        int next = (int)Math.Round(_state.Vacancies(occurrence.RoleId) + m.value);
                        _state.SetVacancies(occurrence.RoleId, Math.Max(0, Math.Min(qty, next)));
                    }
                    break;
            }
        }

        private void PayEventCost(double amount, string costLine, string label)
        {
            if (amount <= 0d) return;
            _ledger.Pay(CashLine.Events, amount);
            var dre = _report.income;
            switch (costLine)
            {
                case CostLine.Maintenance: dre.repairs += amount; break;
                case CostLine.Fines: dre.fines += amount; break;
                case CostLine.Marketing: dre.marketing += amount; break;
                case CostLine.Staff: dre.payroll += amount; break;
                case CostLine.Capex: break;
                default: dre.otherExpenses += amount; break;
            }
            _todayEntries.Add(new CalendarEntry { kind = EntryKind.Expense, label = label, amount = -amount, icon = "event" });
        }

        // =================================================================
        //  ESTOQUE
        // =================================================================

        private void LoseStock(double fraction, string label)
        {
            fraction = SimMath.Clamp01(fraction);
            if (fraction <= 0d || _state.stockMeals <= 0d) return;
            double meals = _state.stockMeals * fraction;
            double value = _state.stockValue * fraction;
            _state.stockMeals -= meals;
            _state.stockValue -= value;
            _report.income.eventStockLoss += value;
            _todayEntries.Add(new CalendarEntry { kind = EntryKind.Expense, label = label + " (estoque)", amount = -value, icon = "stock" });
        }

        /// <summary>Custo de uma refeicao media do mix vendido (ficha tecnica x participacao de cada prato) x alavancas de custo.</summary>
        private double CurrentUnitCost()
        {
            if (_mixUnitCost <= 0d)
                _mixUnitCost = MarketModel.Evaluate(BuildMarketQuery(1d, 1d, 1d, QualityIndex(includeLevers: true)), _cfg).UnitCost;
            return _mixUnitCost * _levers.Factor(Lever.IngredientCostFactor);
        }

        /// <summary>
        /// Vendas diarias esperadas = menor valor entre a demanda esperada e a capacidade recente.
        /// E a base das compras: comprar para a demanda que a cozinha nao consegue atender so gera perda.
        /// </summary>
        private double PurchaseBasis()
        {
            double demand = Math.Max(0d, _state.expectedDailyDemand);
            return _state.expectedDailyCapacity > 0d ? Math.Min(demand, _state.expectedDailyCapacity) : demand;
        }

        private double BuyStock(double meals, string label, bool force)
        {
            if (meals <= 0.0001d) return 0d;
            double coverage = Math.Max(0.5d, _levers.Sum(Lever.StockCoverageDays));
            double cap = coverage * _cfg.stock.maxCoverageMultiple * Math.Max(1d, PurchaseBasis());
            if (!force) meals = Math.Min(meals, Math.Max(0d, cap - _state.stockMeals));
            if (meals <= 0.0001d) return 0d;
            double gross = meals * CurrentUnitCost();
            bool installments = _levers.Sum(Lever.SupplierInstallments) > 0d;
            if (installments)
            {
                int nextDue = _in.Month * OperatingDays + _cfg.finance.nextMonthDueDay;
                _state.payables.Add(new PayableState { kind = PayableKind.Supplier, label = "Fornecedor (1/2)", cashLine = CashLine.Suppliers, amount = gross / 2d, dueAbsDay = nextDue });
                _state.payables.Add(new PayableState { kind = PayableKind.Supplier, label = "Fornecedor (2/2)", cashLine = CashLine.Suppliers, amount = gross / 2d, dueAbsDay = nextDue + OperatingDays });
                _state.stockValue += gross;
            }
            else
            {
                double net = gross * (1d - SimMath.Clamp01(_levers.Sum(Lever.SupplierCashDiscount)));
                _ledger.Pay(CashLine.Suppliers, net);
                _state.stockValue += net;
                gross = net;
            }
            _state.stockMeals += meals;
            return gross;
        }

        // =================================================================
        //  OPERACAO DO DIA
        // =================================================================

        private double QualityIndex(bool includeLevers)
        {
            double quality = 0.4d;
            double fromRoles = 0d;
            foreach (var pair in _team)
            {
                double present = pair.Value - _state.Vacancies(pair.Key);
                if (present <= 0d) continue;
                fromRoles += _catalog.Roles[pair.Key].QualityContribution * present;
            }
            quality += Math.Min(0.3d, fromRoles);
            double fromEquipment = 0d;
            foreach (var unit in ApplicableUnits())
            {
                if (unit.broken) continue;
                var spec = _catalog.Equipment[unit.equipmentId];
                fromEquipment += spec.QualityBonus + _catalog.Tier(unit.tierId).qualityBonus;
            }
            quality += SimMath.Clamp(fromEquipment, -0.15d, 0.25d);
            if (includeLevers) quality += _levers.Sum(Lever.QualityBonus);
            return SimMath.Clamp01(quality);
        }

        private MarketQuery BuildMarketQuery(double calendar, double events, double noise, double quality)
        {
            double awareness = _state.awareness;
            return new MarketQuery
            {
                Restaurant = _restaurant,
                Location = _location,
                TargetSegmentId = _in.TargetSegmentId,
                MenuPrices = _in.MenuPrices,
                Quality = quality,
                TicketFactor = _levers.Factor(Lever.TicketFactor),
                PricePerceptionFactor = _levers.Factor(Lever.PricePerceptionFactor),
                Reach = Math.Max(0d, _levers.Sum(Lever.MarketReach)) * awareness,
                Awareness = awareness,
                MarketingUplift = _levers.Sum(Lever.MarketingUplift),
                Reputation = _reputation,
                Satisfaction = _state.satisfaction,
                LoyaltyStrength = _cfg.market.loyaltyBaseStrength + _levers.Sum(Lever.LoyaltyStrength),
                CompetitionPressure = _location.CompetitionIndex + _levers.Sum(Lever.CompetitionDelta),
                CalendarFactor = calendar,
                EventFactor = events,
                Noise = noise
            };
        }

        private DayResult OperateDay()
        {
            int absDay = AbsDay(_day);
            int weekday = WeekdayOf(_day);
            var result = new DayResult { month = _in.Month, day = _day, absDay = absDay, weekday = weekday, cashStart = _ledger.Cash };

            // Pagamentos agendados de meses anteriores (fornecedores parcelados, contas do mes anterior).
            foreach (var payable in _state.payables.Where(p => p.dueAbsDay == absDay).ToList())
            {
                _ledger.Pay(payable.cashLine, payable.amount);
                _todayEntries.Add(new CalendarEntry { kind = EntryKind.Expense, label = payable.label, amount = -payable.amount, icon = payable.cashLine == CashLine.Suppliers ? "stock" : "info" });
                _state.payables.Remove(payable);
            }

            double quality = QualityIndex(includeLevers: true);
            double season = _in.Month - 1 < _cfg.calendar.monthlySeasonality.Count ? _cfg.calendar.monthlySeasonality[_in.Month - 1] : 1d;
            double calendar = MarketModel.WeekdayFactor(_location.Profile, weekday) * season * _levers.Factor(Lever.RegionalFactor);
            double eventFactor = _levers.Factor(Lever.DemandFactor);
            double noise = _rng.LogNormalNoise(_cfg.market.demandNoiseSigma, _in.Month, _day, "demand");
            var market = MarketModel.Evaluate(BuildMarketQuery(calendar, eventFactor, noise, quality), _cfg);
            _mixUnitCost = market.UnitCost;

            // ---------- Capacidade ----------
            double cover = SimMath.Clamp01(_levers.Sum(Lever.AbsenceCover));
            var working = new Dictionary<string, double>(StringComparer.Ordinal);
            double managerBonus = 0d;
            bool managerPresent = false;
            foreach (var pair in _team)
            {
                double present = pair.Value - _state.Vacancies(pair.Key);
                if (_absentToday.TryGetValue(pair.Key, out double absent)) present -= absent * (1d - cover) * _cfg.staff.absenceCapacityLoss;
                present = Math.Max(0d, present);
                working[pair.Key] = present;
                var profile = _catalog.Roles[pair.Key].Profile;
                if (present > 0d && profile != null && profile.function == RoleFunction.Management)
                {
                    managerPresent = true;
                    managerBonus = Math.Max(managerBonus, profile.productivityBonus);
                }
            }
            double productivity = OperationsModel.Productivity(_cfg.staff, _state.morale, _state.experienceMonths, managerBonus, _levers.Factor(Lever.ProductivityFactor));
            var availability = EquipmentAvailability();
            var capacity = OperationsModel.Capacity(new CapacityQuery
            {
                Restaurant = _restaurant,
                Location = _location,
                WorkingPeople = working,
                Catalog = _catalog,
                Productivity = productivity,
                FrontFactor = _levers.Factor(Lever.FrontCapacityFactor),
                KitchenFactor = _levers.Factor(Lever.KitchenCapacityFactor),
                PhysicalFactor = _levers.Factor(Lever.PhysicalCapacityFactor),
                EquipmentFrontFactor = availability.Front,
                EquipmentKitchenFactor = availability.Kitchen,
                EquipmentPhysicalFactor = availability.Physical,
                EquipmentKitchenBonus = availability.KitchenBonus,
                EquipmentPhysicalBonus = availability.PhysicalBonus
            });

            // ---------- Estoque: reposicao da manha ----------
            // A capacidade do dia (equipe presente e equipamentos) ja e conhecida pela manha; a media suavizada
            // evita que uma falta isolada derrube a compra.
            _state.expectedDailyCapacity = _state.expectedDailyCapacity > 0d
                ? 0.8d * _state.expectedDailyCapacity + 0.2d * capacity.Capacity
                : capacity.Capacity;
            bool blocked = _levers.Sum(Lever.DeliveryBlocked) > 0.5d;
            double coverage = Math.Max(0.5d, _levers.Sum(Lever.StockCoverageDays));
            double target = coverage * PurchaseBasis();
            double purchases = 0d;
            if (!blocked) purchases = BuyStock(Math.Max(0d, target - _state.stockMeals), "Reposição de insumos", force: false);
            else _todayEntries.Add(new CalendarEntry { kind = EntryKind.Info, label = "Entrega de insumos não chegou", icon = "stock" });
            if (purchases > 0.005d && (_day == 1 || purchases > 2d * PurchaseBasis() * CurrentUnitCost()))
                _todayEntries.Add(new CalendarEntry { kind = EntryKind.Expense, label = "Reposição de insumos", amount = -purchases, icon = "stock", routine = true });

            // ---------- Vendas ----------
            double demand = market.PotentialDemand;
            double served = Math.Min(demand, Math.Min(capacity.Capacity, _state.stockMeals));
            served = Math.Max(0d, served);
            double lost = Math.Max(0d, demand - served);
            double lostStock = 0d, lostCapacity = 0d;
            string bottleneck = Bottleneck.None;
            if (lost > 0d)
            {
                if (_state.stockMeals < capacity.Capacity) { lostStock = Math.Max(0d, Math.Min(demand, capacity.Capacity) - served); lostCapacity = lost - lostStock; bottleneck = lostStock > lostCapacity ? Bottleneck.Stock : capacity.Bottleneck; }
                else { lostCapacity = lost; bottleneck = capacity.Bottleneck; }
            }

            double revenue = served * market.Ticket;
            double tax = revenue * _cfg.finance.salesTaxRate;
            double avgCost = _state.stockMeals > 0d ? _state.stockValue / _state.stockMeals : CurrentUnitCost();
            double cmv = served * avgCost;
            _state.stockMeals -= served;
            _state.stockValue = Math.Max(0d, _state.stockValue - cmv);
            _ledger.Receive(CashLine.Sales, revenue);
            _ledger.Accrue("TAXES", tax);

            // ---------- Deterioracao ----------
            double spoilageRate = _restaurant.Profile.spoilagePerDay * _levers.Factor(Lever.SpoilageFactor) * availability.Spoilage;
            double spoiledMeals = _state.stockMeals * SimMath.Clamp01(spoilageRate);
            double spoiledValue = _state.stockMeals > 0d ? _state.stockValue * spoiledMeals / _state.stockMeals : 0d;
            _state.stockMeals -= spoiledMeals;
            _state.stockValue = Math.Max(0d, _state.stockValue - spoiledValue);
            if (_state.stockMeals < 1e-6d) { _state.stockMeals = 0d; _state.stockValue = 0d; }
            _state.wasteWindow.Add(new DailyWasteState { absDay = absDay, wasted = spoiledMeals, consumed = served });
            _state.wasteWindow.RemoveAll(w => absDay - w.absDay >= _cfg.stock.wasteWindowDays);

            // ---------- Custos do dia (competencia) ----------
            var f = _cfg.finance;
            double tariff = f.energyTariffPerKwh * _levers.Factor(Lever.EnergyTariffFactor);
            double energy = f.energyBaseFixed / 30d * _levers.Factor(Lever.EnergyTariffFactor) + availability.EnergyKwh * tariff + f.energyPerMeal * served * _levers.Factor(Lever.EnergyTariffFactor);
            double water = f.waterFixed / 30d + f.waterPerMeal * served;
            double cleaning = (f.cleaningFixed + f.adminFeesMonthly) / 30d + f.cleaningPerMeal * served;
            double rent = _location.Rent / (double)OperatingDays;
            double payroll = DailyPayroll(working);
            double marketing = PolicyCost(CostLine.Marketing) / OperatingDays + _levers.Sum(Lever.PerMealCost) * served;
            double maintenance = _maintenanceMonthly / OperatingDays;
            double staffPrograms = PolicyCost(CostLine.Staff) / OperatingDays;
            double otherPolicies = (PolicyCost(CostLine.Other) + Math.Max(0d, _levers.Sum(Lever.MonthlyFixedCost))) / OperatingDays;
            double perDayCash = _levers.Sum(Lever.CashPerDay);

            _ledger.Accrue("ENERGY", energy);
            _ledger.Accrue("WATER", water);
            _ledger.Accrue("CLEANING", cleaning);
            _ledger.Accrue("RENT", rent);
            _ledger.Accrue("PAYROLL", payroll);
            _ledger.Accrue("MARKETING", marketing);
            _ledger.Accrue("MAINTENANCE", maintenance);
            _ledger.Accrue("STAFF_PROGRAMS", staffPrograms);
            _ledger.Accrue("OTHER_POLICIES", otherPolicies);
            if (perDayCash > 0d) { _ledger.Receive(CashLine.OtherReceipts, perDayCash); _report.income.otherIncome += perDayCash; }
            else if (perDayCash < 0d) PayEventCost(-perDayCash, CostLine.Other, "Custo diário de evento");

            var dre = _report.income;
            dre.grossRevenue += revenue;
            dre.salesTaxes += tax;
            dre.cmv += cmv;
            dre.spoilageLoss += spoiledValue;
            dre.payroll += payroll + staffPrograms;
            dre.rent += rent;
            dre.marketing += marketing;
            dre.utilities += energy + water + cleaning;
            dre.preventiveMaintenance += maintenance;
            dre.otherExpenses += otherPolicies;

            // ---------- Experiencia do cliente ----------
            double utilization = capacity.Capacity > 0d ? demand / capacity.Capacity : (demand > 0d ? 2d : 0d);
            double wait = OperationsModel.WaitMinutes(_restaurant.Profile.baseServiceMinutes, utilization);
            double errors = OperationsModel.ErrorRate(_cfg.staff, _levers.Factor(Lever.ErrorRateFactor), utilization);
            bool unresolved = _state.effects.Any(e => e.unresolved);
            bool premiumService = _restaurant.Profile.premium && (managerPresent || ApplicableUnits().Any(u => !u.broken && u.categoryId == EquipmentCategoryId.Service && u.tierId == EquipmentTierId.Premium));
            var satisfaction = ExperienceModel.DailySatisfaction(new SatisfactionQuery
            {
                WaitMinutes = wait,
                StandardMinutes = _restaurant.Profile.serviceStandardMinutes,
                StockoutShare = demand > 0d ? lostStock / demand : 0d,
                PricePosition = market.PricePosition,
                Quality = quality,
                ErrorRate = errors,
                UnresolvedNegativeEvent = unresolved,
                PremiumServiceApplies = premiumService,
                LeverDelta = _levers.Sum(Lever.SatisfactionDelta)
            }, _cfg.experience);
            if (served > 0d)
            {
                _state.satisfaction = ExperienceModel.Smooth(_state.satisfaction, satisfaction.Value, _cfg.experience.satisfactionSmoothing);
                _reputation = ExperienceModel.ReputationStep(_reputation, satisfaction.Value, _cfg.experience.reputationAdjustRate);
            }

            // ---------- Equipe e marketing ----------
            double moraleTarget = _cfg.staff.baseMorale + _levers.Sum(Lever.MoraleDelta) - _cfg.staff.overloadMoralePenalty * Math.Max(0d, utilization - 1d);
            _state.morale = SimMath.Clamp(_state.morale + _cfg.staff.moraleAdjustRate * (moraleTarget - _state.morale), 0d, 100d);
            double awarenessTarget = SimMath.Clamp01(_levers.Sum(Lever.MarketingAwareness));
            _state.awareness = _cfg.market.awarenessRetention * _state.awareness + (1d - _cfg.market.awarenessRetention) * awarenessTarget;

            // ---------- Meta diaria e bonificacao (opcional, Bloco 8) ----------
            if (f.dailyGoalBonusEnabled && capacity.Capacity > 0d)
            {
                _dailyGoal = _dailyGoal <= 0d ? capacity.Capacity * f.dailyGoalCapacityShare : Math.Min(capacity.Capacity, _dailyGoal + f.dailyGoalProgressionPerDay);
                if (served + 1e-6d >= _dailyGoal)
                {
                    double bonus = f.bonusGoalReached;
                    int repBonus = 1;
                    if (satisfaction.Nps >= f.bonusNpsThreshold) { bonus = f.bonusGoalAndNps; repBonus = 2; }
                    if (satisfaction.Nps >= f.bonusNpsThreshold && lost <= 0.5d) { bonus = f.bonusGoalNpsNoLost; repBonus = 3; }
                    _ledger.Receive(CashLine.OtherReceipts, bonus);
                    dre.otherIncome += bonus;
                    _reputation = SimMath.Clamp(_reputation + repBonus * 0.25d, 0d, 100d);
                    _todayEntries.Add(new CalendarEntry { kind = EntryKind.Revenue, label = "Bônus de meta", amount = bonus, icon = "bonus" });
                }
                else _reputation = SimMath.Clamp(_reputation - (lostStock > 0d ? 0.5d : 0.25d), 0d, 100d);
            }

            // ---------- Contas que vencem hoje ----------
            // Na regra da especificacao (NEXT_MONTH_DAY5) nada vence dentro do mes: tudo vira conta a pagar no fechamento.
            if (_cfg.finance.billTiming != BillTiming.NextMonthDay5)
                _todayEntries.AddRange(_ledger.PayDueBills(_day, OperatingDays));
            if (_day == _cfg.finance.loanDueDay && _loanBalance > 0d)
            {
                double interest = _loanInterestDue;
                double principal = Math.Min(_loanBalance, _loanPrincipalDue * _levers.Factor(Lever.LoanPrincipalFactor));
                _ledger.Pay(CashLine.LoanInterest, interest);
                _ledger.Pay(CashLine.LoanPrincipal, principal);
                dre.loanInterest += interest;
                _loanBalance -= principal;
                _todayEntries.Add(new CalendarEntry { kind = EntryKind.Expense, label = "Parcela do empréstimo", amount = -(interest + principal), icon = "loan" });
            }
            double overdraftInterest = _ledger.ChargeOverdraftInterest(_cfg.finance.overdraftMonthlyRate, OperatingDays);
            dre.overdraftInterest += overdraftInterest;

            // ---------- Registro ----------
            result.potentialDemand = demand;
            result.served = served;
            result.lostCapacity = lostCapacity;
            result.lostStock = lostStock;
            result.bottleneck = bottleneck;
            result.capacity = capacity.Capacity;
            result.frontCapacity = capacity.Front;
            result.kitchenCapacity = capacity.Kitchen;
            result.physicalCapacity = capacity.Physical;
            result.utilization = utilization;
            result.revenue = revenue;
            result.ticket = market.Ticket;
            result.salesTax = tax;
            result.cmv = cmv;
            result.spoilage = spoiledValue;
            result.purchases = purchases;
            result.waitMinutes = wait;
            result.errorRate = errors;
            result.satisfaction = satisfaction.Value;
            result.nps = satisfaction.Nps;
            result.reputation = _reputation;
            result.morale = _state.morale;
            result.quality = quality;
            result.cashEnd = _ledger.Cash;
            result.stockMealsEnd = _state.stockMeals;
            result.fBase = _location.BaseDailyDemand;
            result.fFit = market.FitFactor;
            result.fCompetition = market.CompetitionFactor;
            result.fReputation = market.ReputationFactor;
            result.fLoyalty = market.LoyaltyFactor;
            result.fMarketing = market.MarketingFactor;
            result.fCalendar = calendar;
            result.fEvents = eventFactor;
            result.fNoise = noise;
            // "Vendas altas/medias/baixas" compara com as vendas esperadas (demanda limitada pela capacidade).
            double expected = PurchaseBasis();
            result.salesBand = served >= expected * 1.1d ? "HIGH" : (served <= expected * 0.85d ? "LOW" : "MEDIUM");
            result.entries.Add(new CalendarEntry
            {
                kind = EntryKind.Revenue,
                label = result.salesBand == "HIGH" ? "Vendas altas" : (result.salesBand == "LOW" ? "Vendas baixas" : "Vendas médias"),
                amount = revenue,
                icon = result.salesBand == "LOW" ? "sales_down" : "sales_up",
                routine = result.salesBand == "MEDIUM"
            });
            result.entries.AddRange(_todayEntries);
            _report.days.Add(result);

            // Valores do dia para os indicadores de amanha.
            _yUtilization = utilization;
            _ySatisfaction = satisfaction.Value;
            _yQuality = quality;
            _yWaitRatio = wait / Math.Max(1d, _restaurant.Profile.serviceStandardMinutes);
            _yErrors = errors;
            _yLostShare = demand > 0d ? lost / demand : 0d;
            _state.expectedDailyDemand = 0.8d * _state.expectedDailyDemand + 0.2d * demand / Math.Max(0.2d, calendar * noise);

            foreach (var unit in _state.units) unit.ageMonths += 1d / OperatingDays;
            return result;
        }

        private double DailyPayroll(Dictionary<string, double> working)
        {
            double monthly = 0d;
            foreach (var pair in _team)
            {
                double paid = pair.Value - _state.Vacancies(pair.Key);
                if (paid <= 0d) continue;
                monthly += _catalog.Roles[pair.Key].Salary * paid;
            }
            return monthly * _cfg.staff.payrollChargesMultiplier * _levers.Factor(Lever.PayrollFactor) / OperatingDays;
        }

        private double PolicyCost(string costLine)
        {
            double total = 0d;
            foreach (var category in _cfg.policies)
            {
                string chosen = _in.Policies != null && _in.Policies.TryGetValue(category.id, out string id) ? id : category.defaultOptionId;
                var option = category.Option(chosen) ?? category.Option(category.defaultOptionId);
                if (option != null && option.costLine == costLine) total += Math.Max(0d, option.monthlyCost);
            }
            return total;
        }

        private sealed class Availability
        {
            public double Front = 1d, Kitchen = 1d, Physical = 1d, Spoilage = 1d;
            public double KitchenBonus, PhysicalBonus, EnergyKwh;
        }

        private Availability EquipmentAvailability()
        {
            var result = new Availability();
            var units = ApplicableUnits().ToList();
            foreach (var category in _cfg.equipmentCategories)
            {
                var ofCategory = units.Where(u => u.categoryId == category.id).ToList();
                int owned = ofCategory.Count;
                int workingCount = ofCategory.Count(u => !u.broken);
                result.Kitchen *= OperationsModel.AvailabilityFactor(owned, workingCount, category.kitchenFactorWhenDown, category.kitchenFactorWhenMissing);
                result.Front *= OperationsModel.AvailabilityFactor(owned, workingCount, category.frontFactorWhenDown, 1d);
                result.Physical *= OperationsModel.AvailabilityFactor(owned, workingCount, category.physicalFactorWhenDown, category.physicalFactorWhenMissing);
                if (category.id == EquipmentCategoryId.Refrigeration)
                {
                    double brokenShare = owned > 0 ? (owned - workingCount) / (double)owned : 1d;
                    result.Spoilage = 1d + (_cfg.stock.brokenRefrigerationSpoilage - 1d) * brokenShare;
                }
            }
            foreach (var unit in units.Where(u => !u.broken))
            {
                var spec = _catalog.Equipment[unit.equipmentId];
                result.KitchenBonus += spec.KitchenCapacityBonus;
                result.PhysicalBonus += spec.PhysicalCapacityBonus;
                var category = _cfg.EquipmentCategory(unit.categoryId);
                if (category != null) result.EnergyKwh += category.energyKwhPerDay * _catalog.Tier(unit.tierId).energyFactor;
            }
            return result;
        }

        // =================================================================
        //  INDICADORES
        // =================================================================

        private IndicatorSet BuildIndicators(int absDay)
        {
            var ind = new IndicatorSet();
            ind[Indicator.Satisfaction] = _state.satisfaction;
            ind[Indicator.SatisfactionToday] = _ySatisfaction;
            ind[Indicator.Reputation] = _reputation;
            ind[Indicator.Morale] = _state.morale;
            ind[Indicator.Utilization] = _yUtilization;
            ind[Indicator.Quality] = _yQuality;
            ind[Indicator.Training] = _levers.Sum(Lever.TrainingLevel);
            ind[Indicator.Manager] = _team.Any(p => p.Value - _state.Vacancies(p.Key) > 0 && _catalog.Roles[p.Key].Profile?.function == RoleFunction.Management) ? 1d : 0d;
            ind[Indicator.Preventive] = _levers.Sum(Lever.PreventiveMaintenance) > 0d ? 1d : 0d;
            ind[Indicator.MarketingAwareness] = _state.awareness;
            ind[Indicator.MarketingSpend] = PolicyCost(CostLine.Marketing) / 1000d;
            ind[Indicator.Loyalty] = _levers.Sum(Lever.LoyaltyStrength) > 0.05d ? 1d : 0d;
            ind[Indicator.SupplierQuality] = _supplierQuality;
            double basis = PurchaseBasis();
            ind[Indicator.StockCoverage] = basis > 0d ? _state.stockMeals / basis : 0d;
            double consumed = _state.wasteWindow.Sum(w => w.consumed), wasted = _state.wasteWindow.Sum(w => w.wasted);
            ind[Indicator.WasteRatio] = consumed + wasted > 0d ? wasted / (consumed + wasted) : 0d;
            ind[Indicator.Cash] = _ledger.Cash;
            double fixedDaily = (_location.Rent + OperationsModel.MonthlyPayroll(_catalog, _team, _cfg.staff.payrollChargesMultiplier, 1d)) / OperatingDays;
            ind[Indicator.CashRunwayDays] = fixedDaily > 0d ? _ledger.Cash / fixedDaily : 999d;
            ind[Indicator.LoanBalance] = _loanBalance;
            double installment = _loanBalance > 0d ? _loanInterestDue + Math.Min(_loanBalance, _loanPrincipalDue) : 0d;
            ind[Indicator.LoanShortfall] = _day == _cfg.finance.loanDueDay && installment > 0d ? Math.Max(0d, installment - Math.Max(0d, _ledger.Cash)) : 0d;
            ind[Indicator.Month] = _in.Month;
            ind[Indicator.MonthsRemaining] = _cfg.calendar.months - _in.Month;
            ind[Indicator.Day] = _day;
            ind[Indicator.AbsoluteDay] = absDay;
            ind[Indicator.Competition] = _location.CompetitionIndex + _levers.Sum(Lever.CompetitionDelta);
            ind[Indicator.LastMonthMargin] = _state.lastMonthNetMargin;
            ind[Indicator.Employees] = _team.Sum(p => Math.Max(0, p.Value - _state.Vacancies(p.Key)));
            ind[Indicator.TeamCoverage] = TeamCoverage();
            ind[Indicator.PricePosition] = PricePosition();
            ind[Indicator.MenuPriceDrop] = _state.lastMonthMinMenuPrice > 0d && _menuMinPrice < _state.lastMonthMinMenuPrice * 0.97d ? 1d : 0d;
            ind[Indicator.RecentPromotion] = absDay - _state.lastPromotionAbsDay <= 14 ? 1d : 0d;
            ind[Indicator.WaitRatio] = _yWaitRatio;
            ind[Indicator.Errors] = _yErrors;
            ind[Indicator.LostShare] = _yLostShare;
            int broken = 0;
            var units = ApplicableUnits().ToList();
            foreach (var category in _cfg.equipmentCategories)
            {
                var ofCategory = units.Where(u => u.categoryId == category.id).ToList();
                ind[Indicator.EquipmentCount(category.id)] = ofCategory.Count;
                ind[Indicator.EquipmentWorking(category.id)] = ofCategory.Count(u => !u.broken);
                ind[Indicator.EquipmentBroken(category.id)] = ofCategory.Count(u => u.broken);
                broken += ofCategory.Count(u => u.broken);
            }
            ind[Indicator.BrokenUnits] = broken;
            ind[Indicator.LocationFlag(_location.Id)] = 1d;
            ind[Indicator.RestaurantFlag(_restaurant.TypeId)] = 1d;
            if (!string.IsNullOrEmpty(_in.TargetSegmentId)) ind[Indicator.SegmentFlag(_in.TargetSegmentId)] = 1d;
            foreach (var label in _policyLabels)
            {
                var parts = label.Split('=');
                if (parts.Length == 2) ind[Indicator.PolicyFlag(parts[0], parts[1])] = 1d;
            }
            return ind;
        }

        /// <summary>Fracao dos cargos obrigatorios coberta por quem esta presente (mesma regra do EventIndicatorCalculator legado).</summary>
        private double TeamCoverage()
        {
            int required = 0, covered = 0;
            foreach (var pair in _restaurant.RequiredRoles)
            {
                if (pair.Value <= 0) continue;
                required += pair.Value;
                int hired = _team.TryGetValue(pair.Key, out int count) ? count : 0;
                covered += Math.Min(pair.Value, Math.Max(0, hired - _state.Vacancies(pair.Key)));
            }
            return required > 0 ? covered / (double)required : 1d;
        }

        private double PricePosition()
        {
            double sum = 0d;
            foreach (var product in _restaurant.Products)
            {
                double price = _in.MenuPrices != null && _in.MenuPrices.TryGetValue(product.Id, out double saved) ? saved : product.ReferencePrice;
                double span = product.MaxPrice - product.MinPrice;
                sum += span > 0d ? SimMath.Clamp01((price - product.MinPrice) / span) : 0.5d;
            }
            return _restaurant.Products.Count > 0 ? sum / _restaurant.Products.Count : 0.5d;
        }

        private string EquipmentName(EquipmentUnitState unit)
        {
            string name = _catalog.Equipment.TryGetValue(unit.equipmentId, out var spec) ? spec.Name : unit.equipmentId;
            var tier = _catalog.Config.Tier(unit.tierId);
            return tier != null && unit.tierId != EquipmentTierId.Standard ? name + " (" + tier.displayName + ")" : name;
        }

        // =================================================================
        //  FECHAMENTO
        // =================================================================

        public MonthOutcome Close()
        {
            if (Status != SimulationStatus.Finished) throw new InvalidOperationException("O mes ainda nao terminou (dia " + _day + ").");
            int nextDue = _in.Month * OperatingDays + _cfg.finance.nextMonthDueDay;
            var closingEntries = new List<CalendarEntry>();
            var billPayables = _ledger.CloseBills(_cfg.finance.billTiming, nextDue, closingEntries);
            _state.payables.AddRange(billPayables);
            if (_report.days.Count > 0) _report.days[_report.days.Count - 1].entries.AddRange(closingEntries);
            _ledger.Finish();
            if (_report.days.Count > 0) _report.days[_report.days.Count - 1].cashEnd = _ledger.Cash;

            // Depreciacao do mes (competencia, sem caixa).
            double depreciation = 0d;
            foreach (var unit in _state.units)
            {
                double value = ReliabilityModel.MonthlyDepreciation(unit, _catalog.Tier(unit.tierId));
                unit.accumulatedDepreciation += value;
                depreciation += value;
            }
            _report.income.depreciation = depreciation;

            foreach (var occurrence in _occurrences) _report.events.Add(ToRecord(occurrence));

            var closer = new MonthCloser(_catalog, _in, _state, _report, _ledger, _loanBalance, _reputation, _reputationStart, _ledger.Flow.loanPrincipal);
            closer.Run();

            // Estado do proximo mes.
            var next = _state;
            next.lastClosedMonth = _in.Month;
            next.experienceMonths += 1d;
            if (_policyLevers.Sum(Lever.PreventiveMaintenance) <= 0d) next.monthsWithoutPreventive++;
            next.effects.RemoveAll(e => e.end == EffectEnd.MonthEnd);
            next.recentNegatives.RemoveAll(r => AbsDay(OperatingDays) - r.absDay >= _cfg.eventEngine.mercyWindowDays);
            next.lastTeam = _team.Select(p => new RoleCountState { roleId = p.Key, count = p.Value }).ToList();
            next.lastMonthNetMargin = _report.income.netRevenue > 0d ? _report.income.netIncome / _report.income.netRevenue : -1d;
            next.lastMonthMinMenuPrice = _menuMinPrice;
            next.lastMonthDemand = _report.days.Sum(d => d.potentialDemand);
            next.retainedEarnings += _report.income.netIncome;
            next.expectedOpeningEquity = _report.balance.Equity;

            int consecutive = BankruptcyRule.NextConsecutive(_cfg.finance.bankruptcyRule, _in.ConsecutiveNegativeMonths, _report);
            return new MonthOutcome
            {
                Report = _report,
                NextState = next,
                NextLoanBalance = Math.Max(0d, _loanBalance),
                ConsecutiveNegativeMonths = consecutive,
                Bankrupt = BankruptcyRule.IsBankrupt(_cfg.finance.bankruptcyRule, consecutive, _report, _cfg.finance)
            };
        }
    }

    /// <summary>Escolhe a reacao padrao do evento; se indisponivel, a mais barata habilitada.</summary>
    public sealed class DefaultReactionPolicy : IReactionPolicy
    {
        public string Choose(PendingDecision decision, IndicatorSet indicators)
        {
            string id = decision.Definition.defaultReactionId;
            if (!string.IsNullOrEmpty(id) && decision.Options.Any(o => o.ReactionId == id && o.Enabled)) return id;
            return decision.Options.Where(o => o.Enabled).OrderBy(o => o.Cost).Select(o => o.ReactionId).FirstOrDefault();
        }
    }

    /// <summary>Sempre a reacao mais barata (estrategia de economia maxima).</summary>
    public sealed class CheapestReactionPolicy : IReactionPolicy
    {
        public string Choose(PendingDecision decision, IndicatorSet indicators)
            => decision.Options.Where(o => o.Enabled).OrderBy(o => o.Cost).ThenBy(o => o.ReactionId, StringComparer.Ordinal).Select(o => o.ReactionId).FirstOrDefault();
    }

    /// <summary>Resolve problemas por completo quando o caixa permite (primeira reacao listada habilitada sem cheque especial).</summary>
    public sealed class ThoroughReactionPolicy : IReactionPolicy
    {
        public string Choose(PendingDecision decision, IndicatorSet indicators)
        {
            var first = decision.Options.FirstOrDefault(o => o.Enabled && !o.UsesOverdraft);
            return first?.ReactionId ?? new CheapestReactionPolicy().Choose(decision, indicators);
        }
    }

    /// <summary>Reproduz escolhas gravadas (id do evento + dia); sem registro, usa a padrao.</summary>
    public sealed class RecordedReactionPolicy : IReactionPolicy
    {
        private readonly Dictionary<string, string> _choices;
        private readonly IReactionPolicy _fallback;

        public RecordedReactionPolicy(Dictionary<string, string> choicesByEventAndDay, IReactionPolicy fallback = null)
        {
            _choices = choicesByEventAndDay ?? new Dictionary<string, string>();
            _fallback = fallback ?? new DefaultReactionPolicy();
        }

        public static string Key(string eventId, int day) => eventId + "@" + day.ToString(CultureInfo.InvariantCulture);

        public string Choose(PendingDecision decision, IndicatorSet indicators)
        {
            return _choices.TryGetValue(Key(decision.Definition.id, decision.Day), out string id) ? id : _fallback.Choose(decision, indicators);
        }
    }
}
