using System;
using System.Collections.Generic;

namespace Game.Domain.Simulation
{
    // =====================================================================
    //  RESULTADOS. Tudo [Serializable] para virar JSON no SimulationMonthEntity.
    // =====================================================================

    public static class EntryKind
    {
        public const string Revenue = "REVENUE";
        public const string Expense = "EXPENSE";
        public const string Event = "EVENT";
        public const string Info = "INFO";
    }

    /// <summary>Uma linha do calendario do mes (mockup "Rodada / Mes").</summary>
    [Serializable]
    public class CalendarEntry
    {
        public string kind;
        public string label;
        public double amount;
        /// <summary>Nome curto do icone sugerido para a UI (sales_up, sales_down, energy, water, payroll, loan, tax, stock, event, info).</summary>
        public string icon;
        /// <summary>Lancamento rotineiro (venda media do dia, reposicao normal de insumos). A lista "Ultimos acontecimentos" pode omitir.</summary>
        public bool routine;
    }

    [Serializable]
    public class DayResult
    {
        public int month;
        public int day;
        public int absDay;
        public int weekday;

        public double potentialDemand;
        public double served;
        public double lostCapacity;
        public double lostStock;
        public string bottleneck;
        public double capacity;
        public double frontCapacity;
        public double kitchenCapacity;
        public double physicalCapacity;
        public double utilization;

        public double revenue;
        public double ticket;
        public double salesTax;
        public double cmv;
        public double spoilage;
        public double eventStockLoss;
        public double purchases;

        public double waitMinutes;
        public double errorRate;
        public double satisfaction;
        public double nps;
        public double reputation;
        public double morale;
        public double quality;

        public double cashStart;
        public double cashEnd;
        public double stockMealsEnd;

        // Fatores multiplicativos da demanda do dia (base para a atribuicao no feedback).
        public double fBase;
        public double fFit;
        public double fCompetition;
        public double fReputation;
        public double fLoyalty;
        public double fMarketing;
        public double fCalendar;
        public double fEvents;
        public double fNoise;

        public string salesBand;
        public List<CalendarEntry> entries = new List<CalendarEntry>();
    }

    [Serializable]
    public class IncomeStatement
    {
        public double grossRevenue;
        public double salesTaxes;
        public double netRevenue;
        public double cmv;
        public double grossProfit;
        public double payroll;
        public double rent;
        public double marketing;
        public double utilities;
        public double maintenanceAndLosses;
        public double otherExpenses;
        public double otherIncome;
        public double depreciation;
        public double operatingResult;
        public double financialExpenses;
        public double netIncome;

        // Detalhes que compoem as linhas acima.
        public double spoilageLoss;
        public double eventStockLoss;
        public double repairs;
        public double preventiveMaintenance;
        public double assetWriteOffs;
        public double fines;
        public double loanInterest;
        public double overdraftInterest;

        public IncomeStatement Clone() => (IncomeStatement)MemberwiseClone();
    }

    [Serializable]
    public class CashFlowStatement
    {
        public double openingCash;
        public double salesReceipts;
        public double otherReceipts;
        public double suppliers;
        public double payroll;
        public double rent;
        public double utilities;
        public double taxes;
        public double marketing;
        public double maintenance;
        public double otherOperating;
        public double eventPayments;
        public double loanInterest;
        public double loanPrincipal;
        public double overdraftInterest;
        public double closingCash;

        public CashFlowStatement Clone() => (CashFlowStatement)MemberwiseClone();

        public double TotalInflows => salesReceipts + otherReceipts;
        public double TotalOutflows => suppliers + payroll + rent + utilities + taxes + marketing + maintenance
                                       + otherOperating + eventPayments + loanInterest + loanPrincipal + overdraftInterest;
    }

    [Serializable]
    public class BalanceSheet
    {
        public double cash;
        public double inventory;
        public double equipmentNet;
        public double overdraft;
        public double suppliersPayable;
        public double billsPayable;
        public double loans;
        public double capital;
        public double capitalAdjustment;
        public double retainedEarnings;

        public double CurrentAssets => cash + inventory;
        public double TotalAssets => CurrentAssets + equipmentNet;
        public double CurrentLiabilities => overdraft + suppliersPayable + billsPayable + loans;
        public double TotalLiabilities => CurrentLiabilities;
        public double Equity => capital + capitalAdjustment + retainedEarnings;
        public double Difference => TotalAssets - TotalLiabilities - Equity;
    }

    [Serializable]
    public class MonthlyIndicators
    {
        public double averageTicket;
        public double cmvPercent;
        public double contributionMarginPercent;
        public double capacityOccupancy;
        public double lostOrders;
        public double lostOrdersShare;
        public double lostToCapacity;
        public double lostToStock;
        public double npsAverage;
        public double satisfactionAverage;
        public double waitAverage;
        public double currentLiquidity;
        public double quickLiquidity;
        public double immediateLiquidity;
        public double indebtedness;
        /// <summary>Pedidos no mes. -1 quando a margem de contribuicao nao e positiva.</summary>
        public double breakEvenAccounting;
        public double breakEvenFinancial;
        public double breakEvenEconomic;
        public double unitContributionMargin;
        public double workingCapitalNeed;
        public double recommendedCashReserve;
        public double teamProductivity;
        public double moraleEnd;
        public double operationalRisk;
        public double competitiveness;
        public double wasteShare;
        public double interestCoverage;
        public int overdueBills;
        public int overdraftDays;
        public double fitIndex;
    }

    [Serializable]
    public class ScorecardLine
    {
        public string criterion;
        public int points;
    }

    [Serializable]
    public class Scorecard
    {
        public int total;
        public string classification;
        public List<ScorecardLine> lines = new List<ScorecardLine>();
    }

    /// <summary>Contribuicao de um fator para a variacao de um indicador (pontos percentuais).</summary>
    [Serializable]
    public class FactorContribution
    {
        public string factor;
        public string label;
        public double currentValue;
        public double baselineValue;
        /// <summary>Participacao na variacao total, em pontos percentuais.</summary>
        public double contributionPoints;
    }

    public static class FeedbackTone
    {
        public const string Positive = "POSITIVE";
        public const string Negative = "NEGATIVE";
        public const string Neutral = "NEUTRAL";
        public const string Warning = "WARNING";
    }

    [Serializable]
    public class FeedbackMessage
    {
        public string topic;
        public string tone = FeedbackTone.Neutral;
        public string text;
        /// <summary>Valores numericos citados no texto, para a UI destacar.</summary>
        public List<double> values = new List<double>();
    }

    [Serializable]
    public class EventOccurrenceRecord
    {
        public string occurrenceId;
        public string eventId;
        public string title;
        public string impact;
        public string category;
        public int month;
        public int day;
        public string unitKey;
        public string reactionId;
        public string reactionTitle;
        public double cashImpact;
        public int reputationImpact;
        public double probability;
        public bool automatic;
        public string explanation;
    }

    [Serializable]
    public class DecisionRecord
    {
        public int month;
        public int day;
        public string category;
        public string optionId;
        public string description;
    }

    [Serializable]
    public class OutlookEstimate
    {
        public int runs;
        public double growthProbability;
        public double stagnationProbability;
        public double closureProbability;
        public double medianCashEnd;
        public double p10CashEnd;
        public double p90CashEnd;
    }

    /// <summary>Resultado de caixa do mes, nos mesmos termos do RoundResultEntity existente.</summary>
    [Serializable]
    public class CashResultSummary
    {
        public double grossRevenue;
        public double supplyCost;
        public double rent;
        public double salaries;
        public double utilities;
        public double loanPayment;
        public double loanPrincipalPayment;
        public double otherOperatingCosts;
        public double eventCashImpact;
        public double openingCash;
        public double closingCash;
        public double netResult;
        public int customers;
        public int baseDemand;
        public int potentialDemand;
        public int serviceCapacity;
        public double averageTicket;
        public int reputationAtStart;
        public int reputationAtEnd;
        public double fitFactor;
        public string singleEventId;
    }

    [Serializable]
    public class MonthlyReport
    {
        public int schemaVersion = 1;
        public string sessionId;
        public int month;
        public string configVersion;
        public string restaurantTypeId;
        public string locationId;
        public string targetSegmentId;
        public List<string> policies = new List<string>();
        public IncomeStatement income = new IncomeStatement();
        public CashFlowStatement cashFlow = new CashFlowStatement();
        public BalanceSheet balance = new BalanceSheet();
        public MonthlyIndicators indicators = new MonthlyIndicators();
        public Scorecard scorecard = new Scorecard();
        public CashResultSummary cashResult = new CashResultSummary();
        public List<FactorContribution> demandFactors = new List<FactorContribution>();
        public double demandChangePercent;
        public List<FeedbackMessage> feedback = new List<FeedbackMessage>();
        public List<EventOccurrenceRecord> events = new List<EventOccurrenceRecord>();
        public List<DecisionRecord> decisions = new List<DecisionRecord>();
        public List<DayResult> days = new List<DayResult>();
        public OutlookEstimate outlook;
        public List<string> diagnostics = new List<string>();
    }

    /// <summary>Resultado completo do fechamento: relatorio + estado do proximo mes.</summary>
    public sealed class MonthOutcome
    {
        public MonthlyReport Report;
        public SimulationState NextState;
        public double NextLoanBalance;
        public int ConsecutiveNegativeMonths;
        public bool Bankrupt;
    }

    public static class SimulationStatus
    {
        public const string Running = "RUNNING";
        public const string AwaitingDecision = "AWAITING_DECISION";
        public const string Finished = "FINISHED";
    }

    /// <summary>Uma opcao de reacao como a tela deve mostrar.</summary>
    public sealed class ReactionOption
    {
        public string ReactionId;
        public string Title;
        public string Description;
        public double Cost;
        public bool Enabled;
        public string DisabledReason;
        public bool UsesOverdraft;
    }

    /// <summary>Evento aguardando a escolha do aluno. A simulacao fica parada ate Resolve.</summary>
    public sealed class PendingDecision
    {
        public string OccurrenceId;
        public EventDefinition Definition;
        public int Month;
        public int Day;
        public string UnitKey;
        public string UnitName;
        public double Probability;
        public string ImpactSummary;
        public List<ReactionOption> Options = new List<ReactionOption>();
    }
}
