using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    // =====================================================================
    //  CATALOGO EM TEMPO DE EXECUCAO
    //
    //  Junta os dados que ja existem nos ScriptableObjects do projeto
    //  (LocationData, RestaurantData, ProductData, RoleData, EquipmentData,
    //  CreditLineData) com os perfis novos do SimulationConfig. O adaptador
    //  Unity monta este objeto; os testes e o balanceamento montam com
    //  DefaultSimulationConfig.CreateCatalog().
    // =====================================================================

    public sealed class ProductSpec
    {
        public string Id;
        public string Name;
        /// <summary>Preco de referencia do asset (ProductData.price).</summary>
        public double ReferencePrice;
        /// <summary>
        /// Custo variavel unitario (ingredientes + embalagem + perda tecnica). Vem de
        /// ProductData.price x ProductData.inputCostRatio, que reproduz a ficha tecnica do Bloco 3.
        /// </summary>
        public double UnitCost;
        public double MinPrice;
        public double MaxPrice;
    }

    public sealed class RestaurantSpec
    {
        public string TypeId;
        public string Name;
        public List<ProductSpec> Products = new List<ProductSpec>();
        public Dictionary<string, int> RequiredRoles = new Dictionary<string, int>(StringComparer.Ordinal);
        public List<string> RequiredEquipment = new List<string>();
        public RestaurantProfile Profile;
    }

    public sealed class LocationSpec
    {
        public string Id;
        public string ZoneId;
        public string Name;
        public double Rent;
        public double BaseDailyDemand;
        public double PhysicalCapacity;
        public double ReferencePriceFactor = 1d;
        public double CompetitionIndex = 1d;
        public string PrimarySegment;
        public LocationMarketProfile Profile;
    }

    public sealed class RoleSpec
    {
        public string Id;
        public string Name;
        public double Salary;
        public double QualityContribution;
        public RoleProfile Profile;
    }

    public sealed class EquipmentSpec
    {
        public string Id;
        public string Name;
        public double Cost;
        public List<string> ApplicableTypes = new List<string>();
        public string CategoryId;
        public string TierId = EquipmentTierId.Standard;
        public double QualityBonus;
        public double KitchenCapacityBonus;
        public double PhysicalCapacityBonus;

        public bool AppliesTo(string restaurantTypeId) => ApplicableTypes == null || ApplicableTypes.Count == 0 || ApplicableTypes.Contains(restaurantTypeId);
    }

    public sealed class CreditSpec
    {
        public string Id;
        public string Name;
        public double MaxAmount;
        public double MonthlyRate;
    }

    public sealed class SimulationCatalog
    {
        public SimulationConfig Config;
        public Dictionary<string, LocationSpec> Locations = new Dictionary<string, LocationSpec>(StringComparer.Ordinal);
        public Dictionary<string, RestaurantSpec> Restaurants = new Dictionary<string, RestaurantSpec>(StringComparer.Ordinal);
        public Dictionary<string, RoleSpec> Roles = new Dictionary<string, RoleSpec>(StringComparer.Ordinal);
        public Dictionary<string, EquipmentSpec> Equipment = new Dictionary<string, EquipmentSpec>(StringComparer.Ordinal);
        public Dictionary<string, CreditSpec> Credits = new Dictionary<string, CreditSpec>(StringComparer.Ordinal);

        public LocationSpec Location(string id) => Get(Locations, id, "localizacao");
        public RestaurantSpec Restaurant(string id) => Get(Restaurants, id, "restaurante");
        public RoleSpec Role(string id) => Get(Roles, id, "cargo");
        public EquipmentSpec EquipmentItem(string id) => Get(Equipment, id, "equipamento");

        public SegmentProfile Segment(string id)
        {
            var segment = Config.Segment(id);
            if (segment == null) throw new InvalidOperationException("Segmento sem perfil no SimulationConfig: " + id);
            return segment;
        }

        public EquipmentTierProfile Tier(string id)
        {
            return Config.Tier(id) ?? Config.Tier(EquipmentTierId.Standard)
                ?? throw new InvalidOperationException("Perfil de nivel de equipamento ausente: " + id);
        }

        public EquipmentCategoryProfile Category(string id)
        {
            return Config.EquipmentCategory(id) ?? throw new InvalidOperationException("Categoria de equipamento ausente: " + id);
        }

        private static T Get<T>(Dictionary<string, T> map, string id, string label)
        {
            if (string.IsNullOrEmpty(id) || !map.TryGetValue(id, out var value))
                throw new InvalidOperationException("Catalogo sem " + label + ": " + (id ?? "(vazio)"));
            return value;
        }

        /// <summary>Lista problemas de configuracao. Vazio = pronto para simular.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            if (Config == null) { problems.Add("SimulationConfig ausente."); return problems; }
            if (Config.calendar.operatingDays < 1 || Config.calendar.operatingDays > 31) problems.Add("operatingDays fora de 1..31.");
            if (Config.calendar.months < 1) problems.Add("months precisa ser ao menos 1.");
            foreach (var segment in Config.segments)
                if (segment.referenceSpend <= 0d) problems.Add("Segmento " + segment.id + " com gasto de referencia invalido.");
            foreach (var location in Locations.Values)
            {
                if (location.Profile == null) problems.Add("Localizacao " + location.Id + " sem LocationMarketProfile.");
                else if (location.Profile.segmentMix.Sum(s => s.value) <= 0d) problems.Add("Localizacao " + location.Id + " com segmentMix vazio.");
                if (location.BaseDailyDemand < 0 || location.PhysicalCapacity < 0) problems.Add("Localizacao " + location.Id + " com demanda ou capacidade negativa.");
            }
            foreach (var restaurant in Restaurants.Values)
            {
                if (restaurant.Profile == null) problems.Add("Restaurante " + restaurant.TypeId + " sem RestaurantProfile.");
                if (restaurant.Products.Count == 0) problems.Add("Restaurante " + restaurant.TypeId + " sem produtos.");
                foreach (var product in restaurant.Products)
                    if (product.UnitCost < 0 || product.MinPrice > product.MaxPrice) problems.Add("Produto " + product.Id + " com custo ou faixa invalida.");
            }
            foreach (var role in Roles.Values)
                if (role.Profile == null) problems.Add("Cargo " + role.Id + " sem RoleProfile.");
            foreach (var item in Equipment.Values)
            {
                if (Config.EquipmentCategory(item.CategoryId) == null) problems.Add("Equipamento " + item.Id + " com categoria desconhecida: " + item.CategoryId);
                if (Config.Tier(item.TierId) == null) problems.Add("Equipamento " + item.Id + " com nivel desconhecido: " + item.TierId);
            }
            foreach (var definition in Config.events)
            {
                if (string.IsNullOrEmpty(definition.id)) { problems.Add("Evento sem id."); continue; }
                if (definition.baseRatePerMonth < 0) problems.Add("Evento " + definition.id + " com taxa negativa.");
                if (definition.requiresDecision && definition.reactions.Count == 0) problems.Add("Evento " + definition.id + " pede decisao mas nao tem reacoes.");
                if (definition.requiresDecision && !definition.reactions.Any(r => r.cost <= 0d && r.costAsEquipmentPriceFraction <= 0d
                        && r.costAsLoanBalanceFraction <= 0d && r.conditions.Count == 0 && r.equipmentAction != EquipmentAction.Replace))
                    problems.Add("Evento " + definition.id + " precisa de ao menos uma reacao sem custo e sempre disponivel.");
                if (!string.IsNullOrEmpty(definition.defaultReactionId) && definition.Reaction(definition.defaultReactionId) == null)
                    problems.Add("Evento " + definition.id + " com reacao padrao inexistente.");
            }
            var duplicated = Config.events.GroupBy(e => e.id).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var id in duplicated) problems.Add("Evento duplicado: " + id);
            return problems;
        }
    }

    /// <summary>Entradas de um mes. O adaptador monta a partir do GameSessionEntity.</summary>
    public sealed class MonthInputs
    {
        public string SessionId;
        public int Month = 1;
        /// <summary>Semente base (DeterministicRandom.FromSession).</summary>
        public ulong Seed;
        public string RestaurantTypeId;
        public string LocationId;
        public string TargetSegmentId;
        public Dictionary<string, double> MenuPrices = new Dictionary<string, double>(StringComparer.Ordinal);
        public Dictionary<string, int> Team = new Dictionary<string, int>(StringComparer.Ordinal);
        public List<string> EquipmentOwned = new List<string>();
        public string CreditLineId;
        public double LoanBalance;
        public double OpeningCash;
        public int OpeningReputation = 50;
        public double InitialCapital;
        public int ConsecutiveNegativeMonths;
        /// <summary>categoria de politica -> opcao escolhida. Categoria ausente = opcao padrao.</summary>
        public Dictionary<string, string> Policies = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>null no primeiro mes.</summary>
        public SimulationState State;
        /// <summary>Relatorio do mes anterior, para o feedback comparar periodos (opcional).</summary>
        public MonthlyReport PreviousReport;

        public MonthInputs Clone()
        {
            var copy = (MonthInputs)MemberwiseClone();
            copy.MenuPrices = new Dictionary<string, double>(MenuPrices, StringComparer.Ordinal);
            copy.Team = new Dictionary<string, int>(Team, StringComparer.Ordinal);
            copy.EquipmentOwned = new List<string>(EquipmentOwned);
            copy.Policies = new Dictionary<string, string>(Policies, StringComparer.Ordinal);
            copy.State = State?.Clone();
            return copy;
        }
    }
}
