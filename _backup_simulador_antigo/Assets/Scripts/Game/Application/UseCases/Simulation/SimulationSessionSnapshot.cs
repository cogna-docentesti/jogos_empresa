using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Simulation;

namespace Game.Application.UseCases.Simulation
{
    /// <summary>Estados da sessao com os mesmos nomes do enum GameSessionStatus.</summary>
    public static class SessionStatusName
    {
        public const string InProgress = "IN_PROGRESS";
        public const string Completed = "COMPLETED";
        public const string Bankrupt = "BANKRUPT";
    }

    /// <summary>
    /// Copia neutra da linha GameSessionEntity: so o que o simulador le.
    /// O adaptador SQLite preenche a partir da entidade (enums viram texto).
    /// </summary>
    public sealed class SimulationSessionSnapshot
    {
        public string SessionId;
        public string Status = SessionStatusName.InProgress;
        public int CurrentRound = 1;
        public string RestaurantType;
        public string LocationZone;
        public string TargetSegment;
        public Dictionary<string, double> MenuPrices = new Dictionary<string, double>(StringComparer.Ordinal);
        public Dictionary<string, int> Team = new Dictionary<string, int>(StringComparer.Ordinal);
        public List<string> EquipmentIds = new List<string>();
        public string CreditLineId;
        public double LoanBalance;
        public double CurrentCash;
        public int ReputationScore = 50;
        public double InitialCapital;
        public int ConsecutiveNegativeRounds;
    }

    /// <summary>O que o simulador guarda entre meses (tabela SimulationStateEntity).</summary>
    public sealed class StoredSimulationState
    {
        public SimulationState State;
        public Dictionary<string, string> Policies = new Dictionary<string, string>(StringComparer.Ordinal);
        public MonthlyReport PreviousReport;
    }

    /// <summary>Monta as entradas do mes a partir da sessao e do estado guardado. Sem Unity, sem banco.</summary>
    public static class SimulationInputsBuilder
    {
        public static MonthInputs Build(SimulationCatalog catalog, SimulationSessionSnapshot session, StoredSimulationState stored, out string error)
        {
            error = null;
            if (catalog == null) { error = "Catalogo do simulador ausente."; return null; }
            if (session == null) { error = "Nao ha sessao carregada."; return null; }
            if (session.Status != SessionStatusName.InProgress) { error = "A partida ja terminou (" + session.Status + ")."; return null; }
            int months = catalog.Config.calendar.months;
            if (session.CurrentRound < 1 || session.CurrentRound > months)
            {
                error = "Mes atual fora do ciclo: " + session.CurrentRound + " (ciclo de " + months + " meses).";
                return null;
            }
            if (string.IsNullOrEmpty(session.RestaurantType) || !catalog.Restaurants.ContainsKey(session.RestaurantType))
            {
                error = "Tipo de restaurante sem cadastro no simulador: " + (session.RestaurantType ?? "(vazio)");
                return null;
            }
            var location = catalog.Locations.Values.FirstOrDefault(l => l.ZoneId == session.LocationZone);
            if (location == null)
            {
                error = "Localizacao sem cadastro no simulador para a zona " + (session.LocationZone ?? "(vazia)");
                return null;
            }

            var state = stored?.State;
            if (state != null && state.lastClosedMonth >= session.CurrentRound)
            {
                error = "O mes " + session.CurrentRound + " ja foi fechado pelo simulador. Recarregue a partida.";
                return null;
            }

            var inputs = new MonthInputs
            {
                SessionId = session.SessionId,
                Month = session.CurrentRound,
                Seed = DeterministicRandom.FromSession(session.SessionId, catalog.Config.seedOverride).Seed,
                RestaurantTypeId = session.RestaurantType,
                LocationId = location.Id,
                TargetSegmentId = session.TargetSegment,
                CreditLineId = string.IsNullOrEmpty(session.CreditLineId) ? null : session.CreditLineId,
                LoanBalance = Math.Max(0d, session.LoanBalance),
                OpeningCash = session.CurrentCash,
                OpeningReputation = Math.Max(0, Math.Min(100, session.ReputationScore)),
                InitialCapital = session.InitialCapital,
                ConsecutiveNegativeMonths = Math.Max(0, session.ConsecutiveNegativeRounds),
                State = state?.Clone(),
                PreviousReport = stored?.PreviousReport
            };
            foreach (var pair in session.MenuPrices) inputs.MenuPrices[pair.Key] = pair.Value;
            foreach (var pair in session.Team) if (pair.Value > 0) inputs.Team[pair.Key] = pair.Value;
            inputs.EquipmentOwned.AddRange(session.EquipmentIds.Where(id => !string.IsNullOrEmpty(id) && catalog.Equipment.ContainsKey(id)).Distinct());
            if (stored?.Policies != null)
                foreach (var pair in stored.Policies) inputs.Policies[pair.Key] = pair.Value;
            return inputs;
        }
    }

    /// <summary>Valores da sessao depois do fechamento (o adaptador copia para a GameSessionEntity).</summary>
    public sealed class MonthSettlementPlan
    {
        public int NextRound;
        public string Status;
        public double CurrentCash;
        public int ReputationScore;
        public double LoanBalance;
        public int ConsecutiveNegativeRounds;
        public bool Ended => Status != SessionStatusName.InProgress;

        /// <summary>
        /// Mesma sequencia do MonthlySessionSettlement.Advance (falencia, depois fim do ciclo, depois
        /// proximo mes), mas a falencia vem da regra configurada no simulador (BankruptcyRule).
        /// </summary>
        public static MonthSettlementPlan From(MonthInputs inputs, MonthOutcome outcome, int cycleMonths)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            var plan = new MonthSettlementPlan
            {
                CurrentCash = outcome.Report.cashResult.closingCash,
                ReputationScore = outcome.Report.cashResult.reputationAtEnd,
                LoanBalance = Math.Max(0d, outcome.NextLoanBalance),
                ConsecutiveNegativeRounds = outcome.ConsecutiveNegativeMonths,
                NextRound = inputs.Month,
                Status = SessionStatusName.InProgress
            };
            if (outcome.Bankrupt) plan.Status = SessionStatusName.Bankrupt;
            else if (inputs.Month >= cycleMonths) plan.Status = SessionStatusName.Completed;
            else plan.NextRound = inputs.Month + 1;
            return plan;
        }
    }
}
