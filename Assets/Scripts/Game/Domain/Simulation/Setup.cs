// Game/Domain/Simulation/Setup.cs
// Montagem: regras de validação de D1 a D6 e a criação do estado inicial do mês 1.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    public static class SetupRules
    {
        /// <summary>Erros impedem confirmar. Avisos aparecem no Canvas ("equipe abaixo do mínimo").</summary>
        public static (List<string> errors, List<string> warnings) Validate(SimulationCatalog cat, SessionSetup s)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            if (!cat.Regions.ContainsKey(s.RegionId ?? "")) errors.Add("D1: região inválida.");
            if (!cat.Restaurants.TryGetValue(s.Restaurant, out var rest)) { errors.Add("D2: restaurante inválido."); return (errors, warnings); }
            if (!rest.AllowedSegments.Contains(s.TargetSegment)) errors.Add("D2: público não liberado para este restaurante.");
            foreach (var p in rest.Products)
            {
                var price = s.Menu.FirstOrDefault(m => m.ProductId == p.Id);
                if (price == null) errors.Add($"D3: falta o preço de {p.Name}.");
                else if (price.Price < p.MinPrice || price.Price > p.MaxPrice) errors.Add($"D3: {p.Name} fora da faixa {p.MinPrice} a {p.MaxPrice}.");
            }
            if (s.CreditLineId != null)
            {
                if (!cat.CreditLines.TryGetValue(s.CreditLineId, out var line)) errors.Add("D4: linha de crédito inválida.");
                else if (s.LoanAmount > line.Limit) errors.Add("D4: valor acima do limite da linha.");
            }
            int attendants = s.Team.Count(r => r == RoleType.ATENDENTE);
            int specialists = s.Team.Count(r => r == rest.SpecialistRole);
            if (attendants == 0) errors.Add("D6: nenhum atendente contratado. Sem atendente o restaurante não vende.");
            if (specialists == 0) errors.Add("D6: nenhum especialista de cozinha contratado. Sem ele a cozinha não produz.");
            if (attendants == 1)
                warnings.Add("D6: equipe abaixo do mínimo (2 atendentes + 1 especialista).");
            return (errors, warnings);
        }
    }

    public static class InitialStateFactory
    {
        /// <summary>Estado do início do mês 1, a partir das decisões D1 a D6.</summary>
        public static SimulationState Create(SimulationCatalog cat, SessionSetup setup)
        {
            var rest = cat.Restaurants[setup.Restaurant];
            var region = cat.Regions[setup.RegionId];
            var s = new SimulationState
            {
                Month = 1,
                Capital = rest.InitialCapital,                 // D2
                Cash = rest.InitialCapital,
                CreditLineId = setup.CreditLineId,
                RecentDemand = region.BaseDemandPerDay * 0.8,
                RecentCapacity = 0                             // 0 = a primeira compra usa a capacidade do próprio dia
            };
            if (setup.CreditLineId != null && setup.LoanAmount > 0)
            {                                                 // D4: o empréstimo entra no caixa no início
                s.LoanBalance = setup.LoanAmount;
                s.Cash += setup.LoanAmount;
            }
            int n = 0;
            foreach (var id in setup.EquipmentIds)            // D5: cada item vira uma unidade
            {
                var p = cat.Equipment[id];
                s.Equipment.Add(new EquipmentUnit
                {
                    UnitId = $"u{n++}-{id}", ProfileId = id, Category = p.Category, Level = p.Level,
                    PurchasePrice = p.Price, BookValue = p.Price
                });
                s.Cash -= p.Price;                            // compra paga com o capital
            }
            n = 0;
            foreach (var role in setup.Team)                  // D6
                s.Staff.Add(new StaffMember { StaffId = $"s{n++}-{role}", Role = role, Morale = cat.Constants.MoraleTarget });
            return s;
        }
    }

    /// <summary>
    /// Leva para o estado salvo as decisões que o aluno mudou no hub ENTRE os meses (D4, D5 e D6).
    /// O estado do mês 2 em diante vem do banco; sem isto, uma contratação na Agência de RH, uma compra
    /// na Loja de Equipamentos ou um empréstimo novo no Banco não chegariam ao motor.
    /// Função pura: devolve a lista do que mudou, para aparecer na tela de planejamento.
    /// </summary>
    public static class SetupReconciler
    {
        public static List<string> Apply(SimulationCatalog cat, SessionSetup setup, SimulationState s)
        {
            var changes = new List<string>();

            // D6: contratações e desligamentos (a pessoa nova entra com a moral-alvo e sem experiência)
            foreach (var role in setup.Team.Concat(s.Staff.Select(m => m.Role)).Distinct().ToList())
            {
                int wanted = setup.Team.Count(r => r == role);
                var current = s.Staff.Where(m => m.Role == role).ToList();
                for (int i = current.Count; i < wanted; i++)
                {
                    string id;
                    int n = s.Staff.Count;
                    do id = $"h{s.Month}-{n++}-{role}"; while (s.Staff.Any(m => m.StaffId == id));
                    s.Staff.Add(new StaffMember { StaffId = id, Role = role, Morale = cat.Constants.MoraleTarget });
                    changes.Add($"Contratação: {role}");
                }
                for (int i = current.Count - 1; i >= wanted; i--)
                {
                    s.Staff.Remove(current[i]);                           // sai o mais recente
                    changes.Add($"Desligamento: {role}");
                }
            }

            // D5: equipamento comprado no hub vira unidade nova; o caixa paga (troca de caixa por ativo)
            foreach (var group in setup.EquipmentIds.GroupBy(id => id))
            {
                if (!cat.Equipment.TryGetValue(group.Key, out var profile)) continue;
                int owned = s.Equipment.Count(u => u.ProfileId == group.Key);
                for (int i = owned; i < group.Count(); i++)
                {
                    s.Equipment.Add(new EquipmentUnit
                    {
                        UnitId = $"u{s.Month}-{s.Equipment.Count}-{group.Key}", ProfileId = group.Key, Category = profile.Category,
                        Level = profile.Level, PurchasePrice = profile.Price, BookValue = profile.Price
                    });
                    s.Cash -= profile.Price;
                    changes.Add($"Compra de equipamento: {profile.Name} (R$ {profile.Price:N0})");
                }
            }

            // D4: empréstimo tomado no hub depois do mês 1 (o saldo da sessão passou do saldo do motor)
            if (setup.CreditLineId != null && setup.LoanAmount > s.LoanBalance + 0.01)
            {
                double extra = setup.LoanAmount - s.LoanBalance;
                s.LoanBalance += extra;
                s.Cash += extra;
                s.CreditLineId = setup.CreditLineId;
                changes.Add($"Empréstimo novo: R$ {extra:N0}");
            }
            return changes;
        }
    }

    public static class Forecaster
    {
        /// <summary>
        /// Previsão "se nada mudar": roda o resto da partida N vezes, com sementes diferentes,
        /// as mesmas políticas e a reação sem custo em todo evento. Não altera a partida real.
        /// </summary>
        public static ForecastResult Run(SimulationCatalog cat, SessionSetup setup, SimulationState afterClose,
                                         MonthPolicies policies, int scenarios = 40, bool plannedEvents = false)
        {
            var k = cat.Constants;
            int closedMonth = afterClose.Month - 1;
            int monthsToRun = closedMonth < k.MonthsPerMatch ? k.MonthsPerMatch - closedMonth : 2;
            double equityNow = afterClose.Capital + afterClose.RetainedEarnings;
            int growth = 0, closure = 0;
            var finalCash = new List<double>();

            for (int i = 0; i < scenarios; i++)
            {
                var scenarioSetup = setup.WithSeed(unchecked(setup.Seed * 7919 + i + 1));
                var state = afterClose.Clone();
                double lastNet = 0;
                bool closed = false;
                for (int m = 0; m < monthsToRun && !closed; m++)
                {
                    var plan = plannedEvents ? new MonthSimulation(cat, scenarioSetup, state, policies).ReserveMonthlyEvents() : null;
                    var sim = new MonthSimulation(cat, scenarioSetup, state, policies, plan);
                    Autopilot.RunToEnd(sim);
                    var outcome = MonthCloser.Close(sim);
                    state = outcome.NextState;
                    lastNet = outcome.Report.NetIncome;
                    closed = state.Bankrupt || state.Cash < -k.OverdraftLimit;
                }
                double equityEnd = state.Capital + state.RetainedEarnings;
                if (closed) closure++;
                else if (equityEnd > 1.05 * equityNow && lastNet > 0) growth++;
                finalCash.Add(state.Cash);
            }

            finalCash.Sort();
            double Pct(double q) => finalCash[(int)Math.Round(q * (finalCash.Count - 1))];
            return new ForecastResult
            {
                Scenarios = scenarios,
                Growth = (double)growth / scenarios,
                Closure = (double)closure / scenarios,
                Stagnation = (double)(scenarios - growth - closure) / scenarios,
                CashP10 = Pct(0.10), CashMedian = Pct(0.50), CashP90 = Pct(0.90)
            };
        }
    }

    public static class Autopilot
    {
        /// <summary>Roda o mês até o fim escolhendo a primeira reação sem custo (ou a primeira habilitada).</summary>
        public static void RunToEnd(MonthSimulation sim)
        {
            while (!sim.IsFinished)
            {
                sim.AdvanceDay();
                while (sim.IsAwaitingDecision)
                {
                    var occ = sim.PendingDecisions[0];
                    var options = sim.ReactionOptions(occ);
                    var pick = options.FirstOrDefault(o => o.Enabled && o.Cost == 0 && !o.Reaction.Renegotiates)
                               ?? options.First(o => o.Enabled);
                    sim.Choose(occ.OccurrenceId, pick.Reaction.Id);
                }
            }
        }
    }
}
