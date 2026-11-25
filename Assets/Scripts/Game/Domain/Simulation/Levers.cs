// Game/Domain/Simulation/Levers.cs
using System;
using System.Collections.Generic;

namespace Game.Domain.Simulation
{
    /// <summary>
    /// Uma alavanca: (chave, valor). Chave terminada em ".factor" ou ".hr" MULTIPLICA;
    /// qualquer outra chave SOMA. Políticas, reações e efeitos de eventos são listas de alavancas.
    /// </summary>
    [Serializable]
    public sealed class Lever
    {
        public string Key;
        public double Value;

        public Lever() { }
        public Lever(string key, double value) { Key = key; Value = value; }

        public bool IsMultiplicative => IsMultiplicativeKey(Key);

        public static bool IsMultiplicativeKey(string key)
            => key.EndsWith(".factor", StringComparison.Ordinal) || key.EndsWith(".hr", StringComparison.Ordinal);
    }

    /// <summary>
    /// O conjunto de alavancas valendo em um dia. Os modelos só leem daqui;
    /// nenhum modelo precisa saber se o valor veio de política, reação ou evento.
    /// </summary>
    public sealed class LeverSet
    {
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>();

        public void Apply(Lever lever)
        {
            if (lever.IsMultiplicative)
                _values[lever.Key] = Factor(lever.Key) * lever.Value;
            else
                _values[lever.Key] = Add(lever.Key) + lever.Value;
        }

        public void ApplyAll(IEnumerable<Lever> levers)
        {
            foreach (var l in levers) Apply(l);
        }

        /// <summary>Valor de uma chave multiplicativa. Neutro = 1.</summary>
        public double Factor(string key) => _values.TryGetValue(key, out var v) ? v : 1.0;

        /// <summary>Valor de uma chave aditiva. Neutro = 0.</summary>
        public double Add(string key) => _values.TryGetValue(key, out var v) ? v : 0.0;

        public IReadOnlyDictionary<string, double> Values => _values;
    }

    /// <summary>
    /// Nomes das chaves usadas por este motor de referência. Ficam num lugar só para
    /// evitar erro de digitação. As chaves reais do projeto estão no SimulationConfig.
    /// </summary>
    public static class LeverKeys
    {
        // Mercado
        public const string DemandFactor        = "demand.factor";           // eventos de demanda (chuva, festival...)
        public const string CompetitionAdd      = "competition.pressure";    // soma na pressão (novo concorrente +0,25)
        public const string MarketingMaxGain    = "marketing.maxGain";       // 0,05 / 0,15 / 0,30
        public const string MarketingReach      = "marketing.reach";         // 0,03 / 0,08 / 0,18
        public const string MarketingInvestment = "marketing.investment";    // 0 a 1: quanto a política alimenta a lembrança
        public const string LoyaltyStrength     = "loyalty.strength";        // força da fidelização
        // Qualidade e insumos
        public const string QualityAdd          = "quality";                 // soma na qualidade (fornecedor)
        public const string SuppliesCostFactor  = "supplies.cost.factor";    // custo dos insumos
        public const string DeliveryFactor      = "supplies.delivery.factor";// 0 = entrega bloqueada (atraso do fornecedor)
        public const string StockCoverDays      = "stock.coverDays";         // 2 / 4 / 6
        public const string PayInInstallments   = "supplier.installments";   // 1 = duas parcelas
        // Equipe
        public const string MoraleAdd           = "morale";                  // remuneração: -12 / +10
        public const string ResignationHr       = "resignation.hr";
        public const string ErrorsFactor        = "errors.factor";           // treinamento
        public const string ProductivityAdd     = "productivity";            // treinamento: +0,04 / +0,08
        public const string StaffPresentAdd     = "staff.present";           // substituto temporário
        public const string ServiceCapFactor    = "capacity.service.factor";
        public const string KitchenCapFactor    = "capacity.kitchen.factor";
        // Equipamentos
        public const string FailureHr           = "failure.hr";              // manutenção: 0,55 / 0,40
        public const string PreventiveFlag      = "maintenance.preventive";  // 1 = tem preventiva
        // Eventos: "event.<id>.hr" multiplica o risco de um evento específico
        public static string EventHr(string eventId) => "event." + eventId + ".hr";
        // NPS
        public const string UnresolvedNegative  = "nps.unresolvedNegative";  // 1 = evento negativo ignorado
        // Folha
        public const string PayrollFactor       = "payroll.factor";          // remuneração: 0,9 / 1,1
    }
}
