using System;
using System.Collections.Generic;

namespace Game.Domain.Simulation
{
    /// <summary>
    /// "Alavancas" que politicas de gestao, reacoes e efeitos de eventos podem mover.
    ///
    /// Regra de composicao, pelo sufixo da chave:
    ///  - termina em ".factor" ou ".hr" (hazard ratio): MULTIPLICA. Valor neutro = 1.
    ///  - qualquer outra chave: SOMA. Valor neutro = 0.
    ///
    /// Assim um professor pode criar uma nova opcao de politica ou um novo evento
    /// combinando alavancas existentes, sem escrever codigo.
    /// </summary>
    public static class Lever
    {
        // ---------------- Demanda ----------------
        /// <summary>Multiplicador direto da demanda potencial do dia.</summary>
        public const string DemandFactor = "demand.factor";
        /// <summary>Ganho maximo de demanda do marketing (0,15 = +15% com consciencia de marca plena).</summary>
        public const string MarketingUplift = "marketing.uplift";
        /// <summary>Alvo de consciencia de marca (0..1) que o investimento sustenta.</summary>
        public const string MarketingAwareness = "marketing.awareness";
        /// <summary>Fracao do mercado da cidade (fora da regiao) que o restaurante alcanca.</summary>
        public const string MarketReach = "market.reach";
        /// <summary>Multiplica o preco percebido pelo cliente (promocao percebida, por exemplo).</summary>
        public const string PricePerceptionFactor = "price.perception.factor";
        /// <summary>Multiplica o preco efetivamente cobrado (desconto real).</summary>
        public const string TicketFactor = "ticket.factor";
        /// <summary>Pontos de qualidade percebida (escala 0..1).</summary>
        public const string QualityBonus = "quality.bonus";
        /// <summary>Pressao competitiva adicional (1 = um concorrente medio a mais).</summary>
        public const string CompetitionDelta = "competition.delta";
        /// <summary>Forca da fidelizacao: quanto a satisfacao acumulada aumenta o retorno dos clientes.</summary>
        public const string LoyaltyStrength = "loyalty.strength";
        /// <summary>Indice regional de demanda (crescimento ou retracao da regiao).</summary>
        public const string RegionalFactor = "region.factor";

        // ---------------- Capacidade e equipe ----------------
        public const string FrontCapacityFactor = "capacity.front.factor";
        public const string KitchenCapacityFactor = "capacity.kitchen.factor";
        public const string PhysicalCapacityFactor = "capacity.physical.factor";
        public const string ProductivityFactor = "staff.productivity.factor";
        /// <summary>Desloca a moral-alvo da equipe (pontos 0..100).</summary>
        public const string MoraleDelta = "staff.morale";
        /// <summary>Hazard ratio de faltas (mesma chave do evento insufficient_team).</summary>
        public const string AbsenceHazard = "event.insufficient_team.hr";
        /// <summary>Hazard ratio de pedidos de demissao (mesma chave do evento employee_resignation).</summary>
        public const string TurnoverHazard = "event.employee_resignation.hr";
        /// <summary>Fracao da capacidade de quem faltou que a equipe consegue cobrir hoje (0..1).</summary>
        public const string AbsenceCover = "staff.absence.cover";
        /// <summary>Pessoas do cargo ligado a ocorrencia que faltam hoje (instantaneo).</summary>
        public const string AbsentInstant = "staff.absent.instant";
        /// <summary>Abre (+1) ou fecha (-1) uma vaga no cargo ligado a ocorrencia.</summary>
        public const string VacancyInstant = "staff.vacancy.instant";
        public const string ErrorRateFactor = "staff.errors.factor";
        /// <summary>Multiplica a folha (politica de remuneracao acima ou abaixo do mercado).</summary>
        public const string PayrollFactor = "cost.payroll.factor";
        /// <summary>Nivel de treinamento (0 = nenhum, 1 = basico, 2 = avancado).</summary>
        public const string TrainingLevel = "staff.training";

        // ---------------- Custos e estoque ----------------
        public const string IngredientCostFactor = "cost.ingredient.factor";
        /// <summary>Custo fixo mensal adicional (R$/mes), como fidelizacao ou treinamento.</summary>
        public const string MonthlyFixedCost = "cost.monthly";
        /// <summary>Custo adicional por refeicao vendida (R$).</summary>
        public const string PerMealCost = "cost.meal";
        public const string EnergyTariffFactor = "energy.tariff.factor";
        /// <summary>Dias de cobertura de estoque que a politica de compras mira.</summary>
        public const string StockCoverageDays = "stock.coverage";
        public const string SpoilageFactor = "stock.spoilage.factor";
        /// <summary>Perda imediata de uma fracao do estoque (0,15 = 15%).</summary>
        public const string StockLossInstant = "stock.loss.instant";
        /// <summary>Entrega de insumos do dia nao chega (1 = bloqueia a reposicao do dia).</summary>
        public const string DeliveryBlocked = "stock.delivery.blocked";
        /// <summary>Compra extra imediata, em dias de demanda (2 = dois dias a mais de estoque).</summary>
        public const string StockBoostDays = "stock.boost.instant";

        // ---------------- Fornecedor ----------------
        /// <summary>Hazard ratio de atrasos de entrega (mesma chave do evento supplier_delay).</summary>
        public const string SupplierDelayHazard = "event.supplier_delay.hr";
        /// <summary>Desconto de pagamento a vista (0,02 = 2%).</summary>
        public const string SupplierCashDiscount = "supplier.discount";
        /// <summary>1 = compras parceladas em duas vezes (dia 5 dos meses seguintes).</summary>
        public const string SupplierInstallments = "supplier.installments";

        // ---------------- Riscos ----------------
        /// <summary>Hazard ratio global de falha de equipamentos (politica de manutencao).</summary>
        public const string EquipmentHazard = "equipment.hazard.hr";
        /// <summary>1 = manutencao preventiva ativa.</summary>
        public const string PreventiveMaintenance = "equipment.preventive";
        /// <summary>Custo mensal de manutencao como fracao do valor de compra dos equipamentos.</summary>
        public const string MaintenanceCostRate = "equipment.maintenance.rate";
        /// <summary>Hazard ratio de fiscalizacao sanitaria (mesma chave do evento health_inspection).</summary>
        public const string SanitaryHazard = "event.health_inspection.hr";
        /// <summary>Prefixo para hazard ratio de um evento especifico: "event.&lt;id&gt;.hr".</summary>
        public const string EventHazardPrefix = "event.";
        public const string EventHazardSuffix = ".hr";
        /// <summary>Hazard ratio aplicado apenas a unidade de equipamento afetada pela ocorrencia.</summary>
        public const string UnitHazard = "unit.hazard.hr";

        // ---------------- Financeiro ----------------
        /// <summary>Multiplica o principal da parcela do mes (renegociacao: 0,5 adia metade).</summary>
        public const string LoanPrincipalFactor = "loan.principal.factor";

        // ---------------- Satisfacao e reputacao ----------------
        /// <summary>Pontos somados a satisfacao diaria (escala 0..100).</summary>
        public const string SatisfactionDelta = "satisfaction";
        /// <summary>Variacao imediata de reputacao (pontos 0..100).</summary>
        public const string ReputationInstant = "reputation.instant";

        // ---------------- Caixa ----------------
        /// <summary>Entrada (+) ou saida (-) imediata de caixa, em R$.</summary>
        public const string CashInstant = "cash.instant";
        /// <summary>Entrada (+) ou saida (-) diaria de caixa enquanto o efeito durar.</summary>
        public const string CashPerDay = "cash.daily";

        public static string EventHazard(string eventId) => EventHazardPrefix + eventId + EventHazardSuffix;

        public static bool IsMultiplicative(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return key.EndsWith(".factor", StringComparison.Ordinal) || key.EndsWith(".hr", StringComparison.Ordinal);
        }

        /// <summary>Chaves aplicadas uma unica vez, no dia em que o efeito comeca.</summary>
        public static bool IsInstant(string key)
        {
            return key == CashInstant || key == ReputationInstant || key == StockLossInstant || key == StockBoostDays
                   || key == VacancyInstant || key == AbsentInstant;
        }
    }

    /// <summary>Um ajuste de alavanca. Serializavel pela Unity (campos publicos).</summary>
    [Serializable]
    public class Modifier
    {
        public string key;
        public double value;

        public Modifier() { }

        public Modifier(string key, double value)
        {
            this.key = key;
            this.value = value;
        }

        public override string ToString() => key + "=" + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Contribuicao de uma fonte para uma alavanca, guardada para o feedback.</summary>
    public sealed class LeverContribution
    {
        public string Key;
        public double Value;
        public string Source;
    }

    /// <summary>
    /// Soma as alavancas de varias fontes (politicas, reacoes, efeitos ativos)
    /// e lembra quem contribuiu com o que.
    /// </summary>
    public sealed class LeverSet
    {
        private readonly Dictionary<string, double> _values = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly List<LeverContribution> _contributions = new List<LeverContribution>();

        public IReadOnlyList<LeverContribution> Contributions => _contributions;

        public void Apply(Modifier modifier, string source)
        {
            if (modifier == null || string.IsNullOrEmpty(modifier.key)) return;
            if (double.IsNaN(modifier.value) || double.IsInfinity(modifier.value))
                throw new InvalidOperationException("Alavanca com valor invalido: " + modifier.key);
            bool multiply = Lever.IsMultiplicative(modifier.key);
            if (multiply && modifier.value < 0d)
                throw new InvalidOperationException("Alavanca multiplicativa nao pode ser negativa: " + modifier.key);
            if (_values.TryGetValue(modifier.key, out double current))
                _values[modifier.key] = multiply ? current * modifier.value : current + modifier.value;
            else
                _values[modifier.key] = modifier.value;
            _contributions.Add(new LeverContribution { Key = modifier.key, Value = modifier.value, Source = source });
        }

        public void ApplyAll(IEnumerable<Modifier> modifiers, string source, bool skipInstant = true)
        {
            if (modifiers == null) return;
            foreach (var modifier in modifiers)
            {
                if (modifier == null) continue;
                if (skipInstant && Lever.IsInstant(modifier.key)) continue;
                Apply(modifier, source);
            }
        }

        /// <summary>Valor multiplicativo (neutro 1).</summary>
        public double Factor(string key) => _values.TryGetValue(key, out double value) ? value : 1d;

        /// <summary>Valor aditivo (neutro 0).</summary>
        public double Sum(string key) => _values.TryGetValue(key, out double value) ? value : 0d;

        public bool Has(string key) => _values.ContainsKey(key);

        public IEnumerable<LeverContribution> SourcesOf(string key)
        {
            foreach (var item in _contributions)
                if (item.Key == key) yield return item;
        }
    }
}
