// Assets/Scripts/Game/Adapter/Out/Simulation/SqliteSimulationGateway.cs
// Implementa a porta ISimulationGateway com o SQLite do projeto (SQLite4Unity3d).
// Regra: nada é gravado durante o mês. O fechamento grava tudo de uma vez, numa transação.
using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application.Ports.Out;
using Game.Domain.Simulation;
using SQLite4Unity3d;
using UnityEngine;

namespace Game.Adapter.Out.Simulation
{
    public sealed class SqliteSimulationGateway : ISimulationGateway
    {
        private readonly SQLiteConnection _db;
        private readonly UnitySimulationCatalogProvider _catalogProvider;
        private SimulationCatalog _catalog;

        public SqliteSimulationGateway(SQLiteConnection db, UnitySimulationCatalogProvider catalogProvider)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
            _db.CreateTable<SimulationStateEntity>();      // CreateTable é idempotente; o DatabaseInitializer também cria
            _db.CreateTable<SimulationMonthEntity>();
        }

        public SimulationCatalog LoadCatalog() => _catalog ??= _catalogProvider.Build();

        // ------------------------------------------------------------ leitura do mês ativo
        public ActiveMonthData LoadActiveMonth(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return null;
            var session = _db.Table<GameSessionEntity>().FirstOrDefault(x => x.sessionId == sessionId);
            if (session == null) return null;                                    // D3 ainda não confirmada

            var catalog = LoadCatalog();
            var setup = SessionSetupMapper.FromEntity(session, catalog, _catalogProvider);
            var (errors, warnings) = SetupRules.Validate(catalog, setup);
            var data = new ActiveMonthData
            {
                Setup = setup,
                SetupErrors = errors,
                SetupWarnings = warnings,
                IsFinished = session.status != GameSessionStatus.IN_PROGRESS
            };

            var row = _db.Table<SimulationStateEntity>().FirstOrDefault(x => x.sessionId == sessionId);
            if (row != null && !string.IsNullOrEmpty(row.stateJson))
            {
                data.State = JsonUtility.FromJson<SimulationState>(row.stateJson);     // mês 2 em diante
                if (errors.Count == 0)
                    data.Changes.AddRange(SetupReconciler.Apply(catalog, setup, data.State));   // o que mudou no hub
            }
            else if (errors.Count == 0)
                data.State = InitialStateFactory.Create(catalog, setup);                // mês 1: o estado nasce de D1 a D6

            int month = data.State?.Month ?? Math.Max(1, session.currentRound);
            data.Month = month;
            data.Cash = data.State?.Cash ?? session.currentCash;
            data.Policies = LoadPolicies(sessionId, month);
            return data;
        }

        public MonthPolicies LoadPolicies(string sessionId, int month)
        {
            var row = _db.Table<SimulationStateEntity>().FirstOrDefault(x => x.sessionId == sessionId);
            if (row == null || string.IsNullOrEmpty(row.policiesJson)) return new MonthPolicies();   // padrões do GDD
            return JsonUtility.FromJson<MonthPolicies>(row.policiesJson);        // mês novo começa com as do anterior
        }

        public void SavePolicies(string sessionId, int month, MonthPolicies policies)
        {
            var row = _db.Table<SimulationStateEntity>().FirstOrDefault(x => x.sessionId == sessionId)
                      ?? new SimulationStateEntity { sessionId = sessionId, month = month };
            row.policiesMonth = month;
            row.policiesJson = JsonUtility.ToJson(policies);
            row.updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _db.InsertOrReplace(row);                                            // InsertOrReplace: cria se não existe
        }

        // ------------------------------------------------------------ fechamento (dia 30)
        public CommitResult Commit(MonthCommit c)
        {
            var session = _db.Table<GameSessionEntity>().FirstOrDefault(x => x.sessionId == c.SessionId);
            if (session == null) return Fail(CommitStatus.StorageError, "Sessão não encontrada.");

            string monthId = c.SessionId + ":" + c.Month;
            if (_db.Table<SimulationMonthEntity>().FirstOrDefault(x => x.id == monthId) != null)
                return Fail(CommitStatus.AlreadyClosed, "Este mês já foi fechado.");
            if (session.currentRound != c.Month)
                return Fail(CommitStatus.WrongMonth, $"A sessão gravada está no mês {session.currentRound}, não no {c.Month}.");
            var catalog = LoadCatalog();
            int daysPerMonth = catalog.Constants.DaysPerMonth;
            if (c.DaysSimulated != daysPerMonth)
                return Fail(CommitStatus.IncompleteHistory, $"O mês não tem os {daysPerMonth} dias simulados.");

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var updated = session.Copy();
            try
            {
                _db.RunInTransaction(() =>
                {
                    int i = 0;
                    foreach (var e in c.Events)
                        _db.InsertOrReplace(ToHistory(c, e, i++, daysPerMonth, catalog, now));

                    var roundResult = ToRoundResult(c, session, now);
                    _db.InsertOrReplace(roundResult);

                    _db.InsertOrReplace(new SimulationMonthEntity
                    {
                        id = monthId, sessionId = c.SessionId, month = c.Month,
                        reportJson = JsonUtility.ToJson(c.Report), configVersion = c.ConfigVersion, closedAt = now,
                        startStateJson = c.StartState != null ? JsonUtility.ToJson(c.StartState) : null
                    });

                    var stateRow = _db.Table<SimulationStateEntity>().FirstOrDefault(x => x.sessionId == c.SessionId)
                                   ?? new SimulationStateEntity { sessionId = c.SessionId };
                    stateRow.month = c.NextState.Month;
                    stateRow.stateJson = JsonUtility.ToJson(c.NextState);
                    stateRow.configVersion = c.ConfigVersion;
                    stateRow.updatedAt = now;
                    _db.InsertOrReplace(stateRow);

                    updated.currentRound = c.MatchFinished ? c.Month : c.Month + 1;
                    updated.currentCash = (float)c.CashAfterClose;
                    updated.loanBalance = (float)c.NextState.LoanBalance;
                    updated.reputationScore = (int)Math.Round(c.NextState.Reputation);
                    updated.consecutiveNegativeRounds = c.NextState.ConsecutiveLossMonths;
                    updated.teamJson = TeamJsonFrom(c.NextState);                       // demissões aceitas no mês
                    updated.pendingSummaryResultId = roundResult.roundResultId;          // a tela de resultado ainda não foi lida
                    updated.finalSummaryAcknowledged = false;
                    if (c.MatchFinished)
                    {
                        updated.status = c.Bankrupt ? GameSessionStatus.BANKRUPT : GameSessionStatus.COMPLETED;
                        updated.completedAt = now;
                    }
                    _db.InsertOrReplace(updated);
                });
            }
            catch (Exception ex)
            {
                Debug.LogError("[SqliteSimulationGateway] Falha no Commit: " + ex.Message);
                return Fail(CommitStatus.StorageError, ex.Message);              // a transação desfez tudo
            }

            SyncInMemorySession(updated);
            return new CommitResult { Status = CommitStatus.Ok };
        }

        public IReadOnlyList<MonthReport> LoadReports(string sessionId)
            => _db.Table<SimulationMonthEntity>().Where(x => x.sessionId == sessionId)
                  .ToList()
                  .OrderBy(x => x.month)
                  .Select(x => JsonUtility.FromJson<MonthReport>(x.reportJson))
                  .ToList();

        // ------------------------------------------------------------ auxiliares
        private static CommitResult Fail(CommitStatus status, string message) => new CommitResult { Status = status, Message = message };

        /// A sessão em memória (GameSessionState) é a que o hub e as telas leem. Só é atualizada depois do Commit.
        /// Equipe do estado do motor no formato da sessão (TeamSelectionData), para o hub enxergar quem saiu.
        private string TeamJsonFrom(SimulationState state)
        {
            var data = new TeamSelectionData();
            foreach (var group in state.Staff.GroupBy(s => s.Role))
            {
                string roleId = _catalogProvider.RoleTypeById.FirstOrDefault(kv => kv.Value == group.Key).Key;
                if (roleId != null) data.members.Add(new TeamSelectionItem { roleId = roleId, quantity = group.Count() });
            }
            return TeamSelectionHelper.ToJson(data);
        }

        private static void SyncInMemorySession(GameSessionEntity saved)
        {
            var current = GameSessionState.Current;
            if (current == null || current.sessionId != saved.sessionId) return;
            current.currentRound = saved.currentRound;
            current.currentCash = saved.currentCash;
            current.loanBalance = saved.loanBalance;
            current.reputationScore = saved.reputationScore;
            current.consecutiveNegativeRounds = saved.consecutiveNegativeRounds;
            current.teamJson = saved.teamJson;
            current.status = saved.status;
            current.completedAt = saved.completedAt;
            current.pendingSummaryResultId = saved.pendingSummaryResultId;
            current.finalSummaryAcknowledged = saved.finalSummaryAcknowledged;
        }

        private static double Line(List<NamedValue> lines, string name) => lines.FirstOrDefault(l => l.Name == name)?.Value ?? 0;

        private static SessionEventHistoryEntity ToHistory(MonthCommit c, EventRecord e, int index, int daysPerMonth, SimulationCatalog catalog, long now)
        {
            var def = catalog.Event(e.EventId);
            var reaction = def?.Reactions.FirstOrDefault(r => r.Id == e.ReactionId);
            return new SessionEventHistoryEntity
            {
                historyId = $"{c.SessionId}:{c.Month}:{e.AbsDay}:{e.EventId}:{index}",
                sessionId = c.SessionId,
                eventId = e.EventId,
                round = c.Month,
                day = e.AbsDay - (c.Month - 1) * daysPerMonth,
                eventName = def?.Title ?? e.EventId,
                polarity = e.IsNegative ? EventPolarity.NEGATIVE.ToString() : EventPolarity.POSITIVE.ToString(),
                selectedChoiceId = e.ReactionId,
                selectedChoiceTitle = reaction?.Label,
                occurredAt = now,
                cashChange = (float)-e.Cost
            };
        }

        private static RoundResultEntity ToRoundResult(MonthCommit c, GameSessionEntity session, long now)
            => RoundResultMapper.From(c.Report, c.CashAfterClose, c.SessionId, session, now);
    }

    /// RoundResultEntity (tabela que as telas do layout novo leem) a partir do relatório do motor.
    /// Usado pelo gateway no Commit e pela exportação da conferência (planilha).
    public static class RoundResultMapper
    {
        /// Resumo no formato que as telas do layout novo leem (MonthlyResultPresentation, trimestral, relatório final).
        /// Essas telas usam o MODELO DE CAIXA: caixa inicial + resultado do mês = caixa final, e
        /// resultado do mês = receita - saídas (TotalCosts, que inclui a parcela) + impacto dos eventos.
        /// Por isso cada campo recebe a linha do FLUXO DE CAIXA do motor, não a da DRE (competência).
        ///   grossRevenue        vendas recebidas no mês
        ///   supplyCost          pagamentos a fornecedores (à vista e parcelas)
        ///   rent, salaries      aluguel e folha pagos
        ///   utilities           contas de consumo + impostos + marketing/fidelização/treinamento + manutenção
        ///                       + juros do cheque especial e tarifas (tudo o que a tela chama de despesas operacionais)
        ///   loanPayment         juros + amortização do empréstimo pagos no mês
        ///   eventCashImpact     reações a eventos (saída) e outras entradas, com sinal
        public static RoundResultEntity From(MonthReport r, double cashAfterClose, string sessionId, GameSessionEntity session, long now)
        {
            var cash = r.CashFlow;
            double Line(string name) => cash.FirstOrDefault(l => l.Name == name)?.Value ?? 0;
            double Out(CashLine line) => -Line(line.ToString());             // saídas vêm negativas no fluxo
            double principal = Out(CashLine.Amortization);
            double allInterest = Out(CashLine.Interest);
            double loanInterest = Math.Min(Math.Max(0, r.LoanInterestPaid), allInterest);
            double operating = Out(CashLine.Bills) + Out(CashLine.Taxes) + Out(CashLine.Marketing)
                             + Out(CashLine.Maintenance) + (allInterest - loanInterest);
            int repStart = (int)Math.Round(r.ReputationStart), repEnd = (int)Math.Round(r.ReputationEnd);
            return new RoundResultEntity
            {
                roundResultId = sessionId + ":" + r.Month,
                sessionId = sessionId,
                round = r.Month,
                grossRevenue = (float)Line(nameof(CashLine.Sales)),
                supplyCost = (float)Out(CashLine.Suppliers),
                rent = (float)Out(CashLine.Rent),
                salaries = (float)Out(CashLine.Salaries),
                utilities = (float)operating,
                thirteenthSalary = 0,
                loanPayment = (float)(loanInterest + principal),
                loanPrincipalPayment = (float)principal,
                otherOperatingCosts = 0,
                eventCashImpact = (float)(Line(nameof(CashLine.Events)) + Line(nameof(CashLine.OtherInflows))),
                netResult = (float)r.CashResult,
                openingCash = (float)(cashAfterClose - r.CashResult),
                closingCash = (float)cashAfterClose,
                createdAt = now,
                customers = (int)Math.Round(r.Orders),
                reputationAtStart = repStart,
                reputationAtEnd = repEnd,
                reputationDelta = repEnd - repStart,
                baseDemand = (int)Math.Round(r.BaseDemandPerDay * r.Days.Count),
                potentialDemand = (int)Math.Round(r.Days.Sum(d => d.Demand)),
                serviceCapacity = (int)Math.Round(r.Days.Sum(d => d.Capacity)),
                averageTicket = (float)r.AvgTicket,
                alignmentScore = session?.alignmentScore ?? 0,
                alignmentClassification = session?.alignmentClassification,
                coherenceFactor = session?.alignmentFactor ?? 0
            };
        }
    }

    /// Converte a linha do banco (D1 a D6 gravadas) nas decisões que o motor entende.
    public static class SessionSetupMapper
    {
        private const string NoCreditId = "sem_credito";

        public static SessionSetup FromEntity(GameSessionEntity e, SimulationCatalog catalog, UnitySimulationCatalogProvider provider)
        {
            var setup = new SessionSetup
            {
                SessionId = e.sessionId,
                Seed = DeterministicRandom.SeedFromSessionId(e.sessionId),      // fixa por sessão
                RegionId = provider.RegionIdByZone.TryGetValue(e.locationZone, out var regionId) ? regionId : null,
                Restaurant = e.restaurantType,
                TargetSegment = e.targetSegment
            };

            // D3: preço escolhido de cada prato; prato sem preço gravado usa o preço de referência
            var pricing = MenuPricingHelper.FromJson(e.menuPricingJson);
            if (catalog.Restaurants.TryGetValue(e.restaurantType, out var restaurant))
                foreach (var p in restaurant.Products)
                {
                    var item = pricing?.items?.FirstOrDefault(i => i.productId == p.Id);
                    double price = item != null && item.selectedPrice > 0 ? item.selectedPrice : p.ReferencePrice;
                    setup.Menu.Add(new MenuPrice(p.Id, Math.Min(p.MaxPrice, Math.Max(p.MinPrice, price))));
                }

            // D4
            if (!string.IsNullOrEmpty(e.creditLineId) && e.creditLineId != NoCreditId && catalog.CreditLines.ContainsKey(e.creditLineId))
            {
                setup.CreditLineId = e.creditLineId;
                setup.LoanAmount = e.loanBalance;                                // usado só para criar o estado do mês 1
            }

            // D5
            var equipment = EquipmentSelectionHelper.FromJson(e.equipmentJson);
            if (equipment?.equipmentIds != null)
                setup.EquipmentIds.AddRange(equipment.equipmentIds.Where(id => catalog.Equipment.ContainsKey(id)));

            // D6
            var team = TeamSelectionHelper.FromJson(e.teamJson);
            if (team?.members != null)
                foreach (var m in team.members)
                    if (provider.RoleTypeById.TryGetValue(m.roleId, out var role))
                        for (int i = 0; i < m.quantity; i++) setup.Team.Add(role);

            return setup;
        }
    }
}
