using System;
using System.Collections.Generic;

namespace Game.Domain.Simulation
{
    /// <summary>
    /// Nomes dos indicadores que condicoes e fatores de risco dos eventos podem ler.
    /// Alem destes, existem indicadores gerados por dado:
    ///   equipment.&lt;CATEGORIA&gt;.count | .working | .broken
    ///   location.&lt;id&gt; = 1, restaurant.&lt;tipo&gt; = 1, segment.&lt;id&gt; = 1
    ///   policy.&lt;categoria&gt;.&lt;opcao&gt; = 1
    /// </summary>
    public static class Indicator
    {
        public const string Satisfaction = "satisfaction";
        public const string SatisfactionToday = "satisfaction.today";
        public const string Reputation = "reputation";
        public const string Morale = "morale";
        public const string Utilization = "utilization";
        public const string Quality = "quality";
        public const string Training = "training";
        public const string Manager = "manager";
        public const string Preventive = "maintenance.preventive";
        public const string MarketingAwareness = "marketing.awareness";
        public const string MarketingSpend = "marketing.spend";
        public const string Loyalty = "loyalty";
        public const string SupplierQuality = "supplier.quality";
        public const string StockCoverage = "stock.coverage";
        public const string WasteRatio = "waste.ratio";
        public const string Cash = "cash";
        public const string CashRunwayDays = "cash.runway";
        public const string LoanBalance = "loan.balance";
        public const string LoanShortfall = "loan.shortfall";
        public const string Month = "month";
        /// <summary>Meses que faltam depois do atual (0 no ultimo mes).</summary>
        public const string MonthsRemaining = "month.remaining";
        public const string Day = "day";
        public const string AbsoluteDay = "day.absolute";
        public const string Competition = "competition";
        public const string LastMonthMargin = "profit.margin.last";
        public const string BrokenUnits = "equipment.broken";
        public const string Employees = "employees";
        public const string PricePosition = "price.position";
        public const string MenuPriceDrop = "menu.price.drop";
        public const string RecentPromotion = "promotion.recent";
        public const string WaitRatio = "wait.ratio";
        public const string Errors = "errors";
        public const string LostShare = "lost.share";
        /// <summary>Cargos obrigatorios cobertos / exigidos (0..1). Equivale ao TEAM_COVERAGE_RATIO dos eventos legados.</summary>
        public const string TeamCoverage = "team.coverage";

        public static string EquipmentCount(string category) => "equipment." + category + ".count";
        public static string EquipmentWorking(string category) => "equipment." + category + ".working";
        public static string EquipmentBroken(string category) => "equipment." + category + ".broken";
        public static string LocationFlag(string id) => "location." + id;
        public static string RestaurantFlag(string id) => "restaurant." + id;
        public static string SegmentFlag(string id) => "segment." + id;
        public static string PolicyFlag(string category, string option) => "policy." + category + "." + option;
    }

    /// <summary>Fotografia dos indicadores no inicio do dia. Indicador ausente vale 0.</summary>
    public sealed class IndicatorSet
    {
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>(StringComparer.Ordinal);

        public double this[string name]
        {
            get => _values.TryGetValue(name, out double value) ? value : 0d;
            set => _values[name] = value;
        }

        public bool Has(string name) => _values.ContainsKey(name);

        public IReadOnlyDictionary<string, double> Values => _values;

        public static bool Compare(double left, string op, double right)
        {
            const double tolerance = 1e-9;
            switch (op)
            {
                case ">": return left > right + tolerance;
                case ">=": return left >= right - tolerance;
                case "<": return left < right - tolerance;
                case "<=": return left <= right + tolerance;
                case "==": return Math.Abs(left - right) <= tolerance;
                case "!=": return Math.Abs(left - right) > tolerance;
                default: throw new InvalidOperationException("Operador de condicao desconhecido: " + op);
            }
        }

        public bool Satisfies(IEnumerable<EventCondition> conditions, out string failed)
        {
            failed = null;
            if (conditions == null) return true;
            foreach (var condition in conditions)
            {
                if (condition == null || string.IsNullOrEmpty(condition.indicator)) continue;
                if (!Compare(this[condition.indicator], condition.op, condition.value))
                {
                    failed = condition.indicator + " " + condition.op + " " + condition.value;
                    return false;
                }
            }
            return true;
        }
    }
}
