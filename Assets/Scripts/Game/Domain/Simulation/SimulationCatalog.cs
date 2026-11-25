// Game/Domain/Simulation/SimulationCatalog.cs
// O catálogo é a "tabela de números" do motor: tudo o que é configurável e não muda durante a partida.
// No Unity ele é montado pelo UnitySimulationCatalogProvider a partir do SimulationConfig.asset e
// dos ScriptableObjects (LocationData, RestaurantData, RoleData, EquipmentData, CreditLineData, EventData).
// O domínio só conhece estas classes simples; nunca o ScriptableObject.
using System.Collections.Generic;

namespace Game.Domain.Simulation
{
    public enum EquipmentReliabilityCategory { Refrigeration, Cooking, PointOfSale, Preparation, DiningRoom }
    public enum EquipmentLevel { Economy, Standard, Premium }
    public enum EventScope { Global, PerEmployee, PerEquipmentUnit }
    public enum ReactionCostKind { None, Fixed, EquipmentRepair, EquipmentPriceFraction, LoanBalanceFraction, SalaryFraction }
    public enum EffectDuration { Days, EndOfMonth, EndOfMatch, UntilRepair }

    public sealed class SegmentProfile
    {
        public Segment Segment;
        public double ReferenceSpend;   // T: R$ 45 / 95 / 210
        public double Sensitivity;      // 1,15 / 1,00 / 0,85
        public double CityShare;        // 35% / 53% / 12%
    }

    public sealed class RegionProfile                      // D1
    {
        public string Id;                                   // "bank", "university"...
        public string Name;
        public double Rent;                                 // aluguel/mês
        public double BaseDemandPerDay;                     // D_base
        public double PhysicalCapacityPerDay;               // capacidade do salão
        public double PriceFactor;                          // f_local
        public double CompetitionPressure;                  // 0,80 / 1,00 / 1,25
        public Dictionary<Segment, double> Mix = new Dictionary<Segment, double>(); // público da região (soma 1)
        public double[] WeekdayProfile = { 1, 1, 1, 1, 1, 1, 1 };                  // segunda..domingo
        public double RegionalIndex = 1.0;
    }

    public sealed class ProductProfile                     // D3
    {
        public string Id;
        public string Name;
        public double ReferencePrice;
        public double MinPrice;
        public double MaxPrice;
        public double UnitCost;                             // ficha técnica: não muda com o preço
    }

    public sealed class RestaurantProfile                  // D2
    {
        public RestaurantType Type;
        public string Name;
        public Segment[] AllowedSegments;
        public double InitialCapital;
        public RoleType SpecialistRole;
        public List<ProductProfile> Products = new List<ProductProfile>();
        public Dictionary<Segment, double> Attractiveness = new Dictionary<Segment, double>();
        public double AttendantCapacityPerDay;              // 35 / 30 / 25
        public double BaseServiceMinutes;                   // 15 / 25 / 40
        public double PerishabilityPerDay;                  // 1,2% / 3,5% / 2,2%
        public double CmvMin, CmvMax;                       // faixa de CMV para a pontuação
        public bool IsPremium;                              // para o +5 de NPS do gerente (ponto em aberto no GDD)
    }

    public sealed class RoleProfile                        // D6
    {
        public RoleType Role;
        public double Salary;
        public double KitchenCapacityPerDay;                // chapeiro 50, sushiman 45, chef 35 (0 para atendente/gerente)
    }

    public sealed class EquipmentProfile                   // D5
    {
        public string Id;
        public string Name;
        public double Price;
        public EquipmentReliabilityCategory Category;
        public EquipmentLevel Level;
        public double KitchenCapacityBonus;                 // chapa +8, forno combinado +8
        public double DiningCapacityBonus;                  // mobiliário de luxo +5
        public double KwhPerMonth;
    }

    public sealed class LevelProfile
    {
        public EquipmentLevel Level;
        public double HazardRatio;                          // 1,9 / 1,0 / 0,5
        public double EnergyFactor;                         // 1,30 / 1,00 / 0,78
        public double LifeMonths;                           // 36 / 60 / 96
        public double RepairFraction;                       // 25% / 20% / 15%
        public double QualityDelta;                         // -0,04 / 0 / +0,06
        public double StockLossFactor;                      // 1,3 / 1,0 / 0,7
    }

    public sealed class CategoryProfile
    {
        public EquipmentReliabilityCategory Category;
        public double FailuresPerMonth;                     // 0,07 / 0,05 / 0,04 / 0,004 / 0,003
        public double KitchenFactorWhenDown = 1.0;
        public double ServiceFactorWhenDown = 1.0;
        public double DiningFactorWhenDown = 1.0;
        public double StockLossOnFailure;                   // refrigeração: 20%
        public string FailureEventId;                       // "freezer_breakdown"... ou null
    }

    public sealed class CreditLineProfile                  // D4
    {
        public string Id;
        public string Name;
        public double Limit;
        public double MonthlyRate;
    }

    public sealed class PolicyOption                       // Gestão mensal
    {
        public string PolicyId;                             // "marketing", "supplier"...
        public string OptionId;                             // "high", "economy"...
        public string Label;
        public double FixedMonthlyCost;                     // pago no dia do calendário da política
        public double CostPerOrder;                         // fidelização: R$ 0,50 por pedido
        public double AssetValueRate;                       // manutenção: 1,2% ou 2,5% do valor dos equipamentos
        public List<Lever> Levers = new List<Lever>();
    }

    public sealed class Covariate                          // um termo de beta*(x - ref)
    {
        public string Variable;                             // nome de um observável ("quality", "waste.ratio"...)
        public double Reference;
        public double Beta;
        public Covariate(string variable, double reference, double beta) { Variable = variable; Reference = reference; Beta = beta; }
    }

    public sealed class Condition                          // condição de elegibilidade
    {
        public string Variable;
        public string Operator;                             // ">=", "<=", ">", "<", "=="
        public double Value;
        public Condition(string variable, string op, double value) { Variable = variable; Operator = op; Value = value; }
    }

    public sealed class EffectDefinition
    {
        public Lever Lever;
        public EffectDuration Duration;
        public int Days;                                    // usado quando Duration == Days
        public EffectDefinition(string key, double value, EffectDuration duration, int days = 0)
        { Lever = new Lever(key, value); Duration = duration; Days = days; }
    }

    public sealed class ReactionDefinition
    {
        public string Id;
        public string Label;
        public ReactionCostKind CostKind;
        public double CostValue;                            // R$, fração do preço ou do saldo
        public double CostExtra;                            // parte fixa somada (ex.: R$ 300 + 1x conserto)
        public bool RepairsUnit;                            // conserta a unidade de equipamento do evento
        public bool KeepsStaff;                             // pedido de demissão: contratar substituto
        public bool UsesOverdraft;                          // "Caixa insuficiente": pagar com cheque especial
        public bool Renegotiates;                           // "Caixa insuficiente": renegociar
        public List<EffectDefinition> Effects = new List<EffectDefinition>();
    }

    public sealed class EventDefinition
    {
        public string Id;
        public string Title;
        public bool Announced;
        public int MinMonth = 1, MaxMonth = 3;
        public string Category;                             // Equipamento, Equipe, Fornecedor, Mercado...
        public string Group;                                // mesmo grupo não fica ativo junto (null = sem grupo)
        public EventScope Scope;
        public double BaseRatePerMonth;                     // taxa base (vezes por mês)
        public List<Covariate> Covariates = new List<Covariate>();
        public List<Condition> Conditions = new List<Condition>();
        public RestaurantType[] OnlyRestaurants;            // null = todos
        public string[] OnlyRegions;                        // null = todas
        public int MinIntervalDays;
        public int MaxPerMatch = int.MaxValue;
        public bool Repeats = true;
        public double Severity;                             // peso na proteção contra azar
        public bool IsNegative;
        public bool RequiresDecision;
        public bool Deterministic;                          // não é sorteado; o motor dispara quando a condição vale
        public int ActiveDays = 1;                          // quantos dias o evento conta como "ativo" no grupo
        public List<EffectDefinition> AutomaticEffects = new List<EffectDefinition>();
        public List<ReactionDefinition> Reactions = new List<ReactionDefinition>();
    }

    [System.Serializable]   // editável no Inspector dentro do SimulationConfig.asset
    public sealed class SimulationConstants
    {
        public int DaysPerMonth = 30;
        public int MonthsPerMatch = 3;
        public double TaxRate = 0.08;
        public double PayrollCharges = 1.35;
        public double FitReference = 0.45;
        public double TargetSegmentBonus = 1.15, OtherSegmentPenalty = 0.95;
        public double CityReachFlow = 2.0;
        public double LoyaltyBaseStrength = 0.3;
        public double SatisfactionMemory = 0.2;            // peso do dia na satisfação acumulada
        public double RenegotiationFee = 0.01;
        public double NoiseSigma = 0.12;
        public double BrandRecallCarry = 0.85;               // sobra 85% da lembrança de um mês para o outro
        public double ReputationLagPerDay = 0.035;
        public double MoraleTarget = 65, MoraleDriftPerDay = 0.10, OverloadMoralePenalty = 20;
        public double AbsencePerPersonDay = 0.017, ManagerAbsenceHr = 0.6;
        public double ResignationPerPersonMonth = 0.03;
        public double ManagerRiskHr = 0.85, ManagerProductivity = 0.04, ManagerPremiumNps = 5;
        public double MaxExperienceBonus = 0.06;
        public double OverdraftLimit = 15000, OverdraftMonthlyRate = 0.08;
        public double CashDiscountRate = 0.02;
        public int MaxEventsPerDay = 2, MaxDecisionEventsPerDay = 1, GraceDaysWithoutNegative = 2;
        public double AntiStreakK = 0.35, AntiStreakFloor = 0.35;
        public double HazardFloorFactor = 0.15;
        // Teto e piso do termo e^(soma beta*(x - ref)) dos eventos. Não está no GDD: sem ele, um restaurante
        // com 100% de pedidos perdidos multiplica o risco de avaliação negativa por mais de 1.000 (chance diária ~100%).
        public double CovariateFactorMin = 0.1, CovariateFactorMax = 10.0;
        public int StoppedReminderEveryDays = 10;
        // Contas de consumo
        public double EnergyBase = 600, EnergyPerKwh = 0.95, EnergyPerOrder = 1.20;
        public double WaterBase = 600, WaterPerOrder = 0.60;
        public double CleaningBase = 500, CleaningPerOrder = 0.40;
        public double AdminFees = 300;
        // Calendário SAME_MONTH (dia do mês)
        public int MarketingDay = 1, RentDay = 5, SupplierInstallmentDay = 5, EnergyDay = 8, MaintenanceDay = 15,
                   LoanDay = 20, WaterDay = 22, SalaryDay = 27, TaxDay = 29;
        public double[] MonthSeasonality = { 1.0, 1.0, 1.0 };
    }

    public sealed class SimulationCatalog
    {
        public string ConfigVersion = "ref-1";
        public SimulationConstants Constants = new SimulationConstants();
        public Dictionary<Segment, SegmentProfile> Segments = new Dictionary<Segment, SegmentProfile>();
        public Dictionary<string, RegionProfile> Regions = new Dictionary<string, RegionProfile>();
        public Dictionary<RestaurantType, RestaurantProfile> Restaurants = new Dictionary<RestaurantType, RestaurantProfile>();
        public Dictionary<RoleType, RoleProfile> Roles = new Dictionary<RoleType, RoleProfile>();
        public Dictionary<string, EquipmentProfile> Equipment = new Dictionary<string, EquipmentProfile>();
        public Dictionary<EquipmentLevel, LevelProfile> Levels = new Dictionary<EquipmentLevel, LevelProfile>();
        public Dictionary<EquipmentReliabilityCategory, CategoryProfile> Categories = new Dictionary<EquipmentReliabilityCategory, CategoryProfile>();
        public Dictionary<string, CreditLineProfile> CreditLines = new Dictionary<string, CreditLineProfile>();
        public Dictionary<string, List<PolicyOption>> Policies = new Dictionary<string, List<PolicyOption>>();
        public List<EventDefinition> Events = new List<EventDefinition>();

        public EventDefinition Event(string id) => Events.Find(e => e.Id == id);

        public PolicyOption PolicyOption(string policyId, string optionId)
            => Policies.TryGetValue(policyId, out var list) ? list.Find(o => o.OptionId == optionId) : null;
    }
}
