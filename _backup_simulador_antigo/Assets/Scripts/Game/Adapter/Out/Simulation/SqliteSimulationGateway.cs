using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application.Ports.Out;
using Game.Application.UseCases.Simulation;
using Game.Domain.Simulation;
using SQLite4Unity3d;
using UnityEngine;

namespace Game.Adapter.Out.Simulation
{
    [Serializable]
    public class PolicyChoice
    {
        public string category;
        public string option;
    }

    [Serializable]
    public class PolicyChoiceList
    {
        public List<PolicyChoice> items = new List<PolicyChoice>();

        public static string ToJson(IDictionary<string, string> policies)
        {
            var list = new PolicyChoiceList();
            if (policies != null)
                foreach (var pair in policies.OrderBy(p => p.Key, StringComparer.Ordinal))
                    list.items.Add(new PolicyChoice { category = pair.Key, option = pair.Value });
            return JsonUtility.ToJson(list);
        }

        public static Dictionary<string, string> FromJson(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json)) return result;
            var list = JsonUtility.FromJson<PolicyChoiceList>(json);
            if (list?.items == null) return result;
            foreach (var item in list.items)
                if (item != null && !string.IsNullOrEmpty(item.category) && !string.IsNullOrEmpty(item.option))
                    result[item.category] = item.option;
            return result;
        }
    }

    /// <summary>
    /// Adaptador de saida: liga o caso de uso SystemicMonthService ao SQLite e ao GameSessionState.
    ///
    /// Regras (as mesmas do RoundService do Renan):
    ///  - Le a sessao em memoria (GameSessionState.Current), que so existe no banco depois do confirm da D3.
    ///  - Um fechamento = uma transacao. Se qualquer gravacao falhar, nada fica no banco e a sessao
    ///    em memoria nao muda; a tela pode tentar de novo (RetryCommit).
    ///  - So depois do COMMIT a sessao em memoria recebe os novos valores (AcceptMonthlySettlement).
    ///  - Ordem dentro da transacao: historico de eventos, RoundResultEntity, relatorio, estado, sessao.
    ///    O historico vem antes do RoundResultEntity porque o SessionEventHistoryRepository recusa
    ///    eventos de um mes ja fechado.
    /// </summary>
    public sealed class SqliteSimulationGateway : ISimulationGateway
    {
        private readonly SQLiteConnection _db;
        private readonly UnitySimulationCatalogProvider _catalogs;
        private SimulationCatalog _lastCatalog;

        public SqliteSimulationGateway()
            : this(DatabaseInitializer.DatabaseService?.Connection, new UnitySimulationCatalogProvider()) { }

        public SqliteSimulationGateway(SQLiteConnection connection, UnitySimulationCatalogProvider catalogs)
        {
            _db = connection;
            _catalogs = catalogs ?? new UnitySimulationCatalogProvider();
            if (_db != null) EnsureTables(_db);
        }

        public int OutlookRuns => _catalogs.OutlookRuns;

        /// <summary>Cria as duas tabelas novas (ou acrescenta colunas novas). Seguro de chamar sempre.</summary>
        public static void EnsureTables(SQLiteConnection db)
        {
            db.CreateTable<SimulationStateEntity>();
            db.CreateTable<SimulationMonthEntity>();
        }

        /// <summary>Apaga os dados do simulador de uma sessao (para o Resetar).</summary>
        public static void DeleteSession(SQLiteConnection db, string sessionId)
        {
            EnsureTables(db);
            new SimulationStateRepository(db).DeleteBySessionId(sessionId);
            new SimulationMonthRepository(db).DeleteBySessionId(sessionId);
        }

        public SimulationCatalog LoadCatalog() => _lastCatalog = _catalogs.Load();

        public MonthInputs LoadActiveMonth(SimulationCatalog catalog, out string error)
        {
            error = null;
            if (_db == null) { error = "Banco indisponivel: o mes nao pode ser simulado sem persistencia."; return null; }
            if (!GameSessionState.HasActiveSession) { error = "Nao ha partida em andamento."; return null; }
            if (!GameSessionState.IsPersisted) { error = "A partida ainda nao foi gravada (confirme as decisoes iniciais)."; return null; }

            var session = GameSessionState.Current;
            if (new RoundResultRepository(_db).GetBySessionAndRound(session.sessionId, session.currentRound) != null)
            {
                error = "O mes " + session.currentRound + " ja foi fechado. Recarregue a partida.";
                return null;
            }
            var stored = LoadStored(session.sessionId, session.currentRound);
            return SimulationInputsBuilder.Build(catalog, Snapshot(session), stored, out error);
        }

        public IDictionary<string, string> LoadPolicies(string sessionId)
        {
            if (_db == null || string.IsNullOrEmpty(sessionId)) return new Dictionary<string, string>(StringComparer.Ordinal);
            return PolicyChoiceList.FromJson(new SimulationStateRepository(_db).GetBySessionId(sessionId)?.policiesJson);
        }

        /// <summary>
        /// Grava as politicas escolhidas no hub. Segue a regra do GameSessionState: antes do confirm
        /// da D3 nada e gravado. Depois disso, a tela grava quando o aluno confirma.
        /// </summary>
        public void SavePolicies(string sessionId, IDictionary<string, string> policies)
        {
            if (_db == null || !GameSessionState.IsPersisted || GameSessionState.Current?.sessionId != sessionId)
            {
                Debug.Log("[Simulacao] Politicas nao gravadas: a partida ainda nao existe no banco.");
                return;
            }
            var repository = new SimulationStateRepository(_db);
            var row = repository.GetBySessionId(sessionId) ?? new SimulationStateEntity { sessionId = sessionId };
            row.policiesJson = PolicyChoiceList.ToJson(policies);
            row.updatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            repository.InsertOrReplace(row);
        }

        public IReadOnlyList<MonthlyReport> LoadReports(string sessionId)
        {
            var reports = new List<MonthlyReport>();
            if (_db == null || string.IsNullOrEmpty(sessionId)) return reports;
            foreach (var row in new SimulationMonthRepository(_db).GetBySessionId(sessionId))
            {
                if (string.IsNullOrWhiteSpace(row.reportJson)) continue;
                var report = JsonUtility.FromJson<MonthlyReport>(row.reportJson);
                if (report != null) reports.Add(report);
            }
            return reports;
        }

        public MonthCommitResult Commit(MonthInputs inputs, MonthOutcome outcome)
        {
            if (_db == null) return Fail("Banco indisponivel.");
            if (_db.IsInTransaction) return Fail("O fechamento precisa de uma transacao propria.");
            var session = GameSessionState.Current;
            if (session == null || session.sessionId != inputs.SessionId)
                return Fail("A sessao em memoria nao e a mesma do mes simulado.");

            int cycleMonths = (_lastCatalog ?? LoadCatalog()).Config.calendar.months;
            var plan = MonthSettlementPlan.From(inputs, outcome, cycleMonths);
            long closedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            GameSessionEntity next = null;
            try
            {
                _db.RunInTransaction(() =>
                {
                    var stored = new GameSessionRepository(_db).GetById(session.sessionId);
                    if (stored == null || stored.status != GameSessionStatus.IN_PROGRESS || stored.currentRound != inputs.Month)
                        throw new InvalidOperationException("A partida gravada nao esta no mes " + inputs.Month + ".");
                    var results = new RoundResultRepository(_db);
                    if (results.GetBySessionAndRound(session.sessionId, inputs.Month) != null)
                        throw new InvalidOperationException("O mes " + inputs.Month + " ja foi fechado.");
                    var previous = results.GetBySessionId(session.sessionId);
                    if (previous.Count != inputs.Month - 1)
                        throw new InvalidOperationException("O historico mensal esta incompleto ou duplicado.");

                    foreach (var record in outcome.Report.events)
                        _db.Insert(ToHistory(session.sessionId, record, closedAt));

                    results.Insert(ToRoundResult(session, outcome.Report, closedAt));

                    new SimulationMonthRepository(_db).Insert(new SimulationMonthEntity
                    {
                        simulationMonthId = Guid.NewGuid().ToString(),
                        sessionId = session.sessionId,
                        month = inputs.Month,
                        reportJson = JsonUtility.ToJson(outcome.Report),
                        scoreTotal = outcome.Report.scorecard.total,
                        scoreClassification = outcome.Report.scorecard.classification,
                        netIncome = (float)outcome.Report.income.netIncome,
                        configVersion = outcome.Report.configVersion,
                        createdAt = closedAt
                    });

                    var states = new SimulationStateRepository(_db);
                    var row = states.GetBySessionId(session.sessionId) ?? new SimulationStateEntity { sessionId = session.sessionId };
                    if (string.IsNullOrEmpty(row.policiesJson)) row.policiesJson = PolicyChoiceList.ToJson(inputs.Policies);
                    row.stateJson = JsonUtility.ToJson(outcome.NextState);
                    row.lastClosedMonth = inputs.Month;
                    row.configVersion = outcome.Report.configVersion;
                    row.updatedAt = closedAt;
                    states.InsertOrReplace(row);

                    next = SystemicSessionSettlement.Apply(stored, plan, closedAt);
                    if (_db.Update(next) != 1)
                        throw new InvalidOperationException("A sessao do mes nao foi gravada.");
                });
            }
            catch (Exception exception)
            {
                Debug.LogError("[Simulacao] Fechamento do mes " + inputs.Month + " nao gravado: " + exception.Message);
                return Fail(exception.Message);
            }

            GameSessionState.AcceptMonthlySettlement(next);
            Debug.Log($"[Simulacao] Mes {inputs.Month} gravado: caixa {next.currentCash:N2}, reputacao {next.reputationScore}, status {next.status}.");
            return new MonthCommitResult
            {
                Success = true,
                Bankrupt = next.status == GameSessionStatus.BANKRUPT,
                Completed = next.status == GameSessionStatus.COMPLETED,
                NextMonth = next.currentRound
            };
        }

        // -----------------------------------------------------------------

        private static MonthCommitResult Fail(string error) => new MonthCommitResult { Success = false, Error = error };

        private StoredSimulationState LoadStored(string sessionId, int month)
        {
            var stored = new StoredSimulationState();
            var row = new SimulationStateRepository(_db).GetBySessionId(sessionId);
            if (row != null)
            {
                if (!string.IsNullOrWhiteSpace(row.stateJson)) stored.State = JsonUtility.FromJson<SimulationState>(row.stateJson);
                stored.Policies = PolicyChoiceList.FromJson(row.policiesJson);
            }
            if (month > 1)
            {
                var previous = new SimulationMonthRepository(_db).GetBySessionAndMonth(sessionId, month - 1);
                if (previous != null && !string.IsNullOrWhiteSpace(previous.reportJson))
                    stored.PreviousReport = JsonUtility.FromJson<MonthlyReport>(previous.reportJson);
            }
            return stored;
        }

        public static SimulationSessionSnapshot Snapshot(GameSessionEntity session)
        {
            var snapshot = new SimulationSessionSnapshot
            {
                SessionId = session.sessionId,
                Status = session.status.ToString(),
                CurrentRound = session.currentRound,
                RestaurantType = session.restaurantType.ToString(),
                LocationZone = session.locationZone.ToString(),
                TargetSegment = session.targetSegment.ToString(),
                CreditLineId = session.creditLineId,
                LoanBalance = session.loanBalance,
                CurrentCash = session.currentCash,
                ReputationScore = session.reputationScore,
                InitialCapital = session.initialCapital,
                ConsecutiveNegativeRounds = session.consecutiveNegativeRounds
            };
            foreach (var item in MenuPricingHelper.FromJson(session.menuPricingJson).items)
                if (item != null && !string.IsNullOrEmpty(item.productId)) snapshot.MenuPrices[item.productId] = item.selectedPrice;
            foreach (var member in TeamSelectionHelper.GetMembers(TeamSelectionHelper.FromJson(session.teamJson)))
                if (member.quantity > 0) snapshot.Team[member.roleId] = member.quantity;
            snapshot.EquipmentIds.AddRange(EquipmentSelectionHelper.GetIds(EquipmentSelectionHelper.FromJson(session.equipmentJson)));
            return snapshot;
        }

        public static RoundResultEntity ToRoundResult(GameSessionEntity session, MonthlyReport report, long createdAt)
        {
            var r = report.cashResult;
            return new RoundResultEntity
            {
                roundResultId = Guid.NewGuid().ToString(),
                sessionId = session.sessionId,
                round = report.month,
                grossRevenue = (float)r.grossRevenue,
                supplyCost = (float)r.supplyCost,
                rent = (float)r.rent,
                salaries = (float)r.salaries,
                utilities = (float)r.utilities,
                loanPayment = (float)r.loanPayment,
                thirteenthSalary = 0f,
                otherOperatingCosts = (float)r.otherOperatingCosts,
                eventCashImpact = (float)r.eventCashImpact,
                netResult = (float)r.netResult,
                openingCash = (float)r.openingCash,
                closingCash = (float)r.closingCash,
                eventId = r.singleEventId,
                eventChoice = 0,
                reputationDelta = r.reputationAtEnd - r.reputationAtStart,
                // O alinhamento das decisoes iniciais nao muda no mes: copia o da sessao.
                coherenceFactor = session.alignmentFactor,
                alignmentScore = session.alignmentScore,
                alignmentClassification = session.alignmentClassification,
                createdAt = createdAt,
                customers = r.customers,
                reputationAtEnd = r.reputationAtEnd,
                reputationAtStart = r.reputationAtStart,
                baseDemand = r.baseDemand,
                potentialDemand = r.potentialDemand,
                serviceCapacity = r.serviceCapacity,
                averageTicket = (float)r.averageTicket,
                loanPrincipalPayment = (float)r.loanPrincipalPayment
            };
        }

        public static SessionEventHistoryEntity ToHistory(string sessionId, EventOccurrenceRecord record, long occurredAt)
        {
            return new SessionEventHistoryEntity
            {
                historyId = Guid.NewGuid().ToString(),
                sessionId = sessionId,
                eventId = record.eventId,
                eventName = record.title,
                polarity = record.impact == EventImpact.Positive ? "POSITIVE" : record.impact == EventImpact.Negative ? "NEGATIVE" : record.impact,
                round = record.month,
                day = record.day,
                selectedChoiceId = record.reactionId,
                selectedChoiceTitle = record.reactionTitle,
                selectedChoiceText = record.explanation,
                cashChange = (float)record.cashImpact,
                reputationChange = record.reputationImpact,
                clientsChange = 0,
                // Marca de origem: o motor diario ja aplicou estes impactos no fechamento.
                effectsJson = "{\"source\":\"systemic\",\"automatic\":" + (record.automatic ? "true" : "false") + "}",
                occurredAt = occurredAt
            };
        }
    }

    /// <summary>Aplica o MonthSettlementPlan a uma copia da sessao (nao mexe na sessao em memoria).</summary>
    public static class SystemicSessionSettlement
    {
        public static GameSessionEntity Apply(GameSessionEntity session, MonthSettlementPlan plan, long closedAt)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var next = session.Copy();
            next.currentCash = (float)plan.CurrentCash;
            next.reputationScore = Mathf.Clamp(plan.ReputationScore, 0, 100);
            next.loanBalance = (float)Math.Max(0d, plan.LoanBalance);
            next.consecutiveNegativeRounds = plan.ConsecutiveNegativeRounds;
            next.currentRound = plan.NextRound;
            next.status = plan.Status == SessionStatusName.Bankrupt ? GameSessionStatus.BANKRUPT
                : plan.Status == SessionStatusName.Completed ? GameSessionStatus.COMPLETED
                : GameSessionStatus.IN_PROGRESS;
            if (next.status != GameSessionStatus.IN_PROGRESS) next.completedAt = closedAt;
            return next;
        }
    }
}
