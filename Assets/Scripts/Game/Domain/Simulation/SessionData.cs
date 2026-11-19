// Game/Domain/Simulation/SessionData.cs
// Os dados que ENTRAM no motor (decisões D1 a D6 e políticas do mês) e o ESTADO que
// atravessa de um mês para o outro. Todas as classes usam campos públicos e [Serializable]
// porque o adaptador de banco grava com JsonUtility, que só lê campos públicos e listas.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    // ------------------------------------------------------------------ decisões D1 a D6
    [Serializable]
    public sealed class SessionSetup
    {
        public string SessionId;
        public int Seed;                                    // fixa por sessão
        public string RegionId;                             // D1
        public RestaurantType Restaurant;                   // D2
        public Segment TargetSegment;                       // D2
        public List<MenuPrice> Menu = new List<MenuPrice>();// D3
        public string CreditLineId;                         // D4 (null = sem empréstimo)
        public double LoanAmount;                           // D4
        public List<string> EquipmentIds = new List<string>(); // D5
        public List<RoleType> Team = new List<RoleType>();  // D6

        public double PriceOf(string productId) => Menu.First(m => m.ProductId == productId).Price;

        /// <summary>Cópia com outra semente (usada só pela previsão).</summary>
        public SessionSetup WithSeed(int seed)
        {
            var c = (SessionSetup)MemberwiseClone();
            c.Seed = seed;
            return c;
        }
    }

    [Serializable]
    public sealed class MenuPrice
    {
        public string ProductId;
        public double Price;
        public MenuPrice() { }
        public MenuPrice(string productId, double price) { ProductId = productId; Price = price; }
    }

    // ------------------------------------------------------------------ 8 políticas do mês
    [Serializable]
    public sealed class MonthPolicies
    {
        public string Marketing = "none";        // none, low, medium, high
        public string Loyalty = "none";          // none, program
        public string Supplier = "standard";     // economy, standard, premium
        public string SupplierPayment = "cash";  // cash, installments
        public string Maintenance = "corrective";// corrective, preventive, intensive
        public string Stock = "medium";          // low, medium, high
        public string Pay = "market";            // below, market, above
        public string Training = "none";         // none, basic, advanced

        public IEnumerable<(string policy, string option)> All()
        {
            yield return ("marketing", Marketing);
            yield return ("loyalty", Loyalty);
            yield return ("supplier", Supplier);
            yield return ("supplierPayment", SupplierPayment);
            yield return ("maintenance", Maintenance);
            yield return ("stock", Stock);
            yield return ("pay", Pay);
            yield return ("training", Training);
        }

        public static readonly string[] PolicyIds =
            { "marketing", "loyalty", "supplier", "supplierPayment", "maintenance", "stock", "pay", "training" };

        public string Get(string policyId)
        {
            foreach (var (policy, option) in All())
                if (policy == policyId) return option;
            throw new ArgumentException("Política desconhecida: " + policyId);
        }

        public void Set(string policyId, string optionId)
        {
            switch (policyId)
            {
                case "marketing": Marketing = optionId; break;
                case "loyalty": Loyalty = optionId; break;
                case "supplier": Supplier = optionId; break;
                case "supplierPayment": SupplierPayment = optionId; break;
                case "maintenance": Maintenance = optionId; break;
                case "stock": Stock = optionId; break;
                case "pay": Pay = optionId; break;
                case "training": Training = optionId; break;
                default: throw new ArgumentException("Política desconhecida: " + policyId);
            }
        }

        public MonthPolicies Clone() => (MonthPolicies)MemberwiseClone();
    }

    // ------------------------------------------------------------------ estado entre meses
    [Serializable]
    public sealed class SimulationState
    {
        public int Month = 1;                    // 1..3 (a previsão pode passar de 3)
        public bool Bankrupt;

        // Dinheiro e balanço
        public double Cash;
        public double Capital;
        public double RetainedEarnings;
        public double LoanBalance;
        public string CreditLineId;
        public double InventoryValue;            // estoque a custo (R$)
        public double SupplierPayables;          // fornecedores a pagar
        public double TaxesPayable;
        public double BillsPayable;              // contas de consumo a pagar
        public List<SupplierInstallment> Installments = new List<SupplierInstallment>();

        // Mercado e experiência
        public double Reputation = 50;
        public double Satisfaction = 50;
        public double BrandRecall;               // 0..1
        public double PreviousMonthMargin;
        public int ConsecutiveLossMonths;

        // Operação
        public double RecentCapacity;
        public double RecentDemand;
        public double LastOccupancy = 0.8;
        public int MonthsWithoutPreventive;
        public List<EquipmentUnit> Equipment = new List<EquipmentUnit>();
        public List<StaffMember> Staff = new List<StaffMember>();

        // Eventos
        public List<ActiveEffect> Effects = new List<ActiveEffect>();
        public List<EventRecord> EventHistory = new List<EventRecord>();

        public SimulationState Clone()
        {
            var c = (SimulationState)MemberwiseClone();
            c.Installments = Installments.Select(i => i.Clone()).ToList();
            c.Equipment = Equipment.Select(e => e.Clone()).ToList();
            c.Staff = Staff.Select(s => s.Clone()).ToList();
            c.Effects = Effects.Select(e => e.Clone()).ToList();
            c.EventHistory = EventHistory.Select(e => e.Clone()).ToList();
            return c;
        }
    }

    [Serializable]
    public sealed class SupplierInstallment
    {
        public int DueAbsDay;                    // dia absoluto: (mês - 1) * 30 + dia
        public double Amount;
        public SupplierInstallment Clone() => (SupplierInstallment)MemberwiseClone();
    }

    [Serializable]
    public sealed class EquipmentUnit
    {
        public string UnitId;
        public string ProfileId;
        public EquipmentReliabilityCategory Category;
        public EquipmentLevel Level;
        public double PurchasePrice;
        public double BookValue;                 // valor contábil (preço - depreciação)
        public int AgeMonths;
        public bool IsBroken;
        public int BrokenSinceAbsDay;
        public List<int> FailureAbsDays = new List<int>();

        public EquipmentUnit Clone()
        {
            var c = (EquipmentUnit)MemberwiseClone();
            c.FailureAbsDays = new List<int>(FailureAbsDays);
            return c;
        }
    }

    [Serializable]
    public sealed class StaffMember
    {
        public string StaffId;
        public RoleType Role;
        public double Morale = 65;
        public double ExperienceBonus;           // até +6%
        public StaffMember Clone() => (StaffMember)MemberwiseClone();
    }

    [Serializable]
    public sealed class ActiveEffect
    {
        public string SourceId;                  // evento ou reação que criou o efeito
        public string LeverKey;
        public double LeverValue;
        public int ExpiresAfterAbsDay;           // int.MaxValue = até o fim da partida
        public string UntilRepairUnitId;         // efeito que some quando a unidade é consertada
        public ActiveEffect Clone() => (ActiveEffect)MemberwiseClone();
    }

    [Serializable]
    public sealed class EventRecord
    {
        public string EventId;
        public int AbsDay;
        public double Severity;
        public bool IsNegative;
        public string ReactionId;
        public double Cost;
        public EventRecord Clone() => (EventRecord)MemberwiseClone();
    }

    public static class Days
    {
        public static int Abs(int month, int day, int daysPerMonth = 30) => (month - 1) * daysPerMonth + day;
    }
}
