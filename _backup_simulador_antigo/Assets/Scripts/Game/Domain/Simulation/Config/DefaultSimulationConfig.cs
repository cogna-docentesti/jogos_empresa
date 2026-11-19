using System.Collections.Generic;

namespace Game.Domain.Simulation
{
    /// <summary>
    /// Valores padrao do simulador e um espelho dos assets do projeto (Resources/*), usado
    /// pelos testes e pelo balanceamento fora da Unity. No jogo, o adaptador le os assets de
    /// verdade e so pega daqui os perfis que nao existem em nenhum asset.
    ///
    /// Espelho conferido com os assets em 28/10/2026 (branch feature/reordenar-decisoes).
    /// </summary>
    public static class DefaultSimulationConfig
    {
        public static SimulationConfig Create()
        {
            var c = new SimulationConfig();
            c.segments.AddRange(Segments());
            c.locations.AddRange(Locations());
            c.restaurants.AddRange(Restaurants());
            c.roles.AddRange(Roles());
            c.equipmentCategories.AddRange(EquipmentCategories());
            c.equipmentTiers.AddRange(EquipmentTiers());
            c.equipment.AddRange(EquipmentProfiles());
            c.policies.AddRange(Policies());
            c.events.AddRange(DefaultEventCatalog.Create());
            return c;
        }

        // ---------------------------------------------------------------- Segmentos

        public static IEnumerable<SegmentProfile> Segments()
        {
            // Gastos de referencia por refeicao coerentes com as faixas de preco da Matriz de alinhamento
            // (popular 25-45, japones 65-120, frances acima de 120) e com a renda da planilha Parametros.
            yield return new SegmentProfile { id = "LOW", displayName = "Classe C", referenceSpend = 45d, cityShare = 0.35d, priceSensitivity = 1.15d };
            yield return new SegmentProfile { id = "MEDIUM", displayName = "Classe B", referenceSpend = 95d, cityShare = 0.53d, priceSensitivity = 1d };
            yield return new SegmentProfile { id = "HIGH", displayName = "Classe A", referenceSpend = 210d, cityShare = 0.12d, priceSensitivity = 0.85d };
        }

        private static LocationMarketProfile Location(string id, double low, double medium, double high, params double[] weekday)
        {
            var profile = new LocationMarketProfile { locationId = id };
            profile.segmentMix.Add(new SegmentShare("LOW", low));
            profile.segmentMix.Add(new SegmentShare("MEDIUM", medium));
            profile.segmentMix.Add(new SegmentShare("HIGH", high));
            profile.weekdayProfile = new List<double>(weekday);
            return profile;
        }

        public static IEnumerable<LocationMarketProfile> Locations()
        {
            // Composicao do fluxo (inclui quem trabalha na area). Perfil semanal: segunda a domingo.
            yield return Location("bank", 0.20, 0.35, 0.45, 1.15, 1.15, 1.15, 1.15, 1.10, 0.75, 0.55);
            yield return Location("university", 0.58, 0.34, 0.08, 1.15, 1.20, 1.20, 1.15, 1.05, 0.70, 0.55);
            yield return Location("store", 0.35, 0.50, 0.15, 0.90, 0.90, 0.95, 1.00, 1.10, 1.25, 0.90);
            yield return Location("condominium", 0.25, 0.55, 0.20, 0.85, 0.85, 0.90, 0.95, 1.10, 1.25, 1.10);
            yield return Location("marketing", 0.22, 0.43, 0.35, 1.15, 1.15, 1.15, 1.15, 1.10, 0.70, 0.60);
        }

        private static RestaurantProfile Restaurant(string id, double low, double medium, double high, double spoilage,
            double baseMinutes, double standardMinutes, double cmvMin, double cmvMax, bool premium)
        {
            var profile = new RestaurantProfile
            {
                typeId = id,
                spoilagePerDay = spoilage,
                baseServiceMinutes = baseMinutes,
                serviceStandardMinutes = standardMinutes,
                cmvStandardMin = cmvMin,
                cmvStandardMax = cmvMax,
                premium = premium
            };
            profile.appeal.Add(new SegmentShare("LOW", low));
            profile.appeal.Add(new SegmentShare("MEDIUM", medium));
            profile.appeal.Add(new SegmentShare("HIGH", high));
            return profile;
        }

        public static IEnumerable<RestaurantProfile> Restaurants()
        {
            // Perecibilidade (Bloco 1 e 3.7): lanches baixo risco, japones alto, frances medio.
            // Faixa de CMV sobre a receita liquida: ficha tecnica do Bloco 3 com precos na faixa media.
            yield return Restaurant("PODRAO", 1.00, 0.80, 0.45, 0.012, 10, 15, 0.30, 0.38, false);
            yield return Restaurant("JAPONES", 0.55, 1.00, 0.90, 0.035, 15, 25, 0.33, 0.42, false);
            yield return Restaurant("FRANCES", 0.25, 0.60, 1.00, 0.022, 22, 40, 0.32, 0.41, true);
        }

        public static IEnumerable<RoleProfile> Roles()
        {
            // Capacidades do Bloco 6.1 da especificacao (pedidos por dia por pessoa).
            var attendant = new RoleProfile { roleId = "attendant", function = RoleFunction.Front, dailyCapacity = 30 };
            attendant.capacityByRestaurant.Add(new RoleCapacityOverride { restaurantTypeId = "PODRAO", dailyCapacity = 35 });
            attendant.capacityByRestaurant.Add(new RoleCapacityOverride { restaurantTypeId = "JAPONES", dailyCapacity = 30 });
            attendant.capacityByRestaurant.Add(new RoleCapacityOverride { restaurantTypeId = "FRANCES", dailyCapacity = 25 });
            yield return attendant;
            yield return new RoleProfile { roleId = "grill_cook", function = RoleFunction.Kitchen, dailyCapacity = 50 };
            yield return new RoleProfile { roleId = "sushi_chef", function = RoleFunction.Kitchen, dailyCapacity = 45 };
            yield return new RoleProfile { roleId = "head_chef", function = RoleFunction.Kitchen, dailyCapacity = 35 };
            yield return new RoleProfile
            {
                roleId = "manager",
                function = RoleFunction.Management,
                dailyCapacity = 0,
                operationalRiskHazard = 0.85d,
                productivityBonus = 0.04d,
                premiumSatisfactionBonus = 5d
            };
        }

        public static IEnumerable<EquipmentCategoryProfile> EquipmentCategories()
        {
            yield return new EquipmentCategoryProfile
            {
                id = EquipmentCategoryId.Refrigeration, displayName = "Refrigeração", baseFailuresPerMonth = 0.07d, energyKwhPerDay = 7d,
                kitchenFactorWhenDown = 0.8d, kitchenFactorWhenMissing = 0.6d, stockLossOnFailure = 0.2d, failureEventId = "freezer_breakdown"
            };
            yield return new EquipmentCategoryProfile
            {
                id = EquipmentCategoryId.Cooking, displayName = "Cocção", baseFailuresPerMonth = 0.05d, energyKwhPerDay = 9d,
                kitchenFactorWhenDown = 0.45d, kitchenFactorWhenMissing = 0.3d, failureEventId = "kitchen_equipment_failure"
            };
            yield return new EquipmentCategoryProfile
            {
                id = EquipmentCategoryId.Preparation, displayName = "Preparo", baseFailuresPerMonth = 0.004d, energyKwhPerDay = 0.5d,
                kitchenFactorWhenDown = 0.9d, kitchenFactorWhenMissing = 0.85d
            };
            yield return new EquipmentCategoryProfile
            {
                id = EquipmentCategoryId.Service, displayName = "Salão", baseFailuresPerMonth = 0.003d, energyKwhPerDay = 0d,
                physicalFactorWhenDown = 0.8d, physicalFactorWhenMissing = 0.5d
            };
            yield return new EquipmentCategoryProfile
            {
                id = EquipmentCategoryId.PointOfSale, displayName = "Sistema de venda", baseFailuresPerMonth = 0.04d, energyKwhPerDay = 1d,
                frontFactorWhenDown = 0.85d, failureEventId = "payment_system_failure"
            };
        }

        public static IEnumerable<EquipmentTierProfile> EquipmentTiers()
        {
            yield return new EquipmentTierProfile { id = EquipmentTierId.Economic, displayName = "Econômico", hazardFactor = 1.9d, energyFactor = 1.3d, lifespanMonths = 36d, repairCostFraction = 0.25d, qualityBonus = -0.04d, stockLossFactor = 1.3d, priceFactor = 0.65d };
            yield return new EquipmentTierProfile { id = EquipmentTierId.Standard, displayName = "Intermediário", hazardFactor = 1d, energyFactor = 1d, lifespanMonths = 60d, repairCostFraction = 0.2d, qualityBonus = 0d, stockLossFactor = 1d, priceFactor = 1d };
            yield return new EquipmentTierProfile { id = EquipmentTierId.Premium, displayName = "Premium", hazardFactor = 0.5d, energyFactor = 0.78d, lifespanMonths = 96d, repairCostFraction = 0.15d, qualityBonus = 0.06d, stockLossFactor = 0.7d, priceFactor = 1.6d };
        }

        private static EquipmentProfile Equip(string id, string category, string tier = EquipmentTierId.Standard, double quality = 0d, double kitchen = 0d, double physical = 0d)
            => new EquipmentProfile { equipmentId = id, categoryId = category, tierId = tier, qualityBonus = quality, kitchenCapacityBonus = kitchen, physicalCapacityBonus = physical };

        public static IEnumerable<EquipmentProfile> EquipmentProfiles()
        {
            yield return Equip("adega_climatizada", EquipmentCategoryId.Refrigeration, quality: 0.04d);
            yield return Equip("balcao_refrigerado_sushi", EquipmentCategoryId.Refrigeration, quality: 0.03d);
            yield return Equip("bancada_inox_preparo", EquipmentCategoryId.Preparation);
            yield return Equip("chapa_profissional_podrao", EquipmentCategoryId.Cooking, quality: 0.02d, kitchen: 8d);
            yield return Equip("conjunto_panelas_industriais", EquipmentCategoryId.Preparation);
            yield return Equip("facas_especiais_japones", EquipmentCategoryId.Preparation, quality: 0.04d);
            yield return Equip("fogao_industrial_6_bocas", EquipmentCategoryId.Cooking);
            yield return Equip("forno_combinado_inteligente", EquipmentCategoryId.Cooking, EquipmentTierId.Premium, quality: 0.05d, kitchen: 8d);
            yield return Equip("freezer_horizontal", EquipmentCategoryId.Refrigeration);
            yield return Equip("fritadeira_eletrica_dupla_podrao", EquipmentCategoryId.Cooking, quality: 0.01d, kitchen: 6d);
            yield return Equip("geladeira_comercial_dupla", EquipmentCategoryId.Refrigeration);
            yield return Equip("mobiliario_basico", EquipmentCategoryId.Service);
            yield return Equip("mobiliario_luxo", EquipmentCategoryId.Service, EquipmentTierId.Premium, quality: 0.08d, physical: 5d);
            yield return Equip("panela_eletrica_arroz", EquipmentCategoryId.Cooking, kitchen: 4d);
            yield return Equip("pia_industrial_inox", EquipmentCategoryId.Preparation);
            yield return Equip("sistema_pdv_computador", EquipmentCategoryId.PointOfSale);
        }

        // ---------------------------------------------------------------- Politicas

        private static PolicyOption Option(string id, string name, string description, double monthlyCost, string costLine, params Modifier[] modifiers)
        {
            var option = new PolicyOption { id = id, displayName = name, description = description, monthlyCost = monthlyCost, costLine = costLine };
            option.modifiers.AddRange(modifiers);
            return option;
        }

        private static Modifier M(string key, double value) => new Modifier(key, value);

        public static IEnumerable<PolicyCategory> Policies()
        {
            var marketing = new PolicyCategory { id = "marketing", displayName = "Marketing", description = "Investimento mensal em divulgação (Bloco 6.2).", defaultOptionId = "none" };
            marketing.options.Add(Option("none", "Sem divulgação", "Nenhum gasto; a lembrança da marca cai com o tempo.", 0, CostLine.Marketing, M(Lever.MarketingAwareness, 0)));
            marketing.options.Add(Option("low", "Baixo", "R$ 1.500/mês. Até +5% de demanda; pouco alcance fora da região.", 1500, CostLine.Marketing,
                M(Lever.MarketingAwareness, 1), M(Lever.MarketingUplift, 0.05), M(Lever.MarketReach, 0.03)));
            marketing.options.Add(Option("medium", "Médio", "R$ 4.000/mês. Até +15%; pode gerar fila se a estrutura for limitada.", 4000, CostLine.Marketing,
                M(Lever.MarketingAwareness, 1), M(Lever.MarketingUplift, 0.15), M(Lever.MarketReach, 0.08)));
            marketing.options.Add(Option("high", "Alto", "R$ 8.000/mês. Até +30% e alcance na cidade; sem capacidade, vira pedidos perdidos.", 8000, CostLine.Marketing,
                M(Lever.MarketingAwareness, 1), M(Lever.MarketingUplift, 0.30), M(Lever.MarketReach, 0.18)));
            yield return marketing;

            var loyalty = new PolicyCategory { id = "loyalty", displayName = "Fidelização", description = "Programa de pontos e relacionamento.", defaultOptionId = "off" };
            loyalty.options.Add(Option("off", "Sem programa", "Clientes voltam conforme a satisfação.", 0, CostLine.Marketing));
            loyalty.options.Add(Option("on", "Programa de fidelidade", "R$ 900/mês + R$ 0,50 por pedido. A satisfação acumulada pesa mais no retorno.", 900, CostLine.Marketing,
                M(Lever.LoyaltyStrength, 0.25), M(Lever.PerMealCost, 0.5)));
            yield return loyalty;

            var supplier = new PolicyCategory { id = "supplier", displayName = "Fornecedor", description = "Preço, qualidade e confiabilidade dos insumos (Bloco 2).", defaultOptionId = "standard" };
            supplier.options.Add(Option("economic", "Econômico", "Insumos 8% mais baratos, qualidade menor e mais atrasos.", 0, CostLine.Other,
                M(Lever.IngredientCostFactor, 0.92), M(Lever.QualityBonus, -0.07), M(Lever.SupplierDelayHazard, 1.8), M(Lever.SpoilageFactor, 1.15), M(Lever.SanitaryHazard, 1.25)));
            supplier.options.Add(Option("standard", "Padrão", "Preço e qualidade de referência.", 0, CostLine.Other));
            supplier.options.Add(Option("premium", "Premium", "Insumos 12% mais caros, melhor qualidade, entregas confiáveis.", 0, CostLine.Other,
                M(Lever.IngredientCostFactor, 1.12), M(Lever.QualityBonus, 0.10), M(Lever.SupplierDelayHazard, 0.5), M(Lever.SpoilageFactor, 0.85), M(Lever.SanitaryHazard, 0.85)));
            yield return supplier;

            var payment = new PolicyCategory { id = "supplier_payment", displayName = "Pagamento a fornecedores", description = "À vista com 2% de desconto ou em duas parcelas (Bloco 2).", defaultOptionId = "cash" };
            payment.options.Add(Option("cash", "À vista", "2% de desconto; o caixa sai no dia da compra.", 0, CostLine.Other, M(Lever.SupplierCashDiscount, 0.02)));
            payment.options.Add(Option("installments", "Duas parcelas", "Sem desconto; parcelas no dia 5 dos dois meses seguintes.", 0, CostLine.Other, M(Lever.SupplierInstallments, 1)));
            yield return payment;

            var maintenance = new PolicyCategory { id = "maintenance", displayName = "Manutenção", description = "Política de manutenção dos equipamentos.", defaultOptionId = "corrective" };
            maintenance.options.Add(Option("corrective", "Somente corretiva", "Sem custo fixo; conserta quando quebra.", 0, CostLine.Maintenance, M(Lever.EquipmentHazard, 1)));
            maintenance.options.Add(Option("preventive", "Preventiva mensal", "1,2% do valor dos equipamentos por mês. Risco de falha cerca de 45% menor.", 0, CostLine.Maintenance,
                M(Lever.EquipmentHazard, 0.55), M(Lever.MaintenanceCostRate, 0.012), M(Lever.PreventiveMaintenance, 1), M(Lever.SanitaryHazard, 0.9)));
            maintenance.options.Add(Option("intensive", "Preventiva + inspeção semanal", "2,5% do valor por mês. Risco 60% menor, nunca zero.", 0, CostLine.Maintenance,
                M(Lever.EquipmentHazard, 0.4), M(Lever.MaintenanceCostRate, 0.025), M(Lever.PreventiveMaintenance, 1), M(Lever.SanitaryHazard, 0.8)));
            yield return maintenance;

            var stock = new PolicyCategory { id = "stock", displayName = "Estoque", description = "Quantos dias de demanda manter em estoque (Bloco 1.7).", defaultOptionId = "medium" };
            stock.options.Add(Option("low", "Baixo (2 dias)", "Menos perdas e menos caixa parado; mais risco de faltar produto.", 0, CostLine.Other, M(Lever.StockCoverageDays, 2)));
            stock.options.Add(Option("medium", "Médio (4 dias)", "Equilíbrio entre perdas e falta de produto.", 0, CostLine.Other, M(Lever.StockCoverageDays, 4)));
            stock.options.Add(Option("high", "Alto (6 dias)", "Protege contra atrasos; mais perdas em produtos perecíveis.", 0, CostLine.Other, M(Lever.StockCoverageDays, 6)));
            yield return stock;

            var salary = new PolicyCategory { id = "salary", displayName = "Remuneração", description = "Salários em relação ao mercado.", defaultOptionId = "market" };
            salary.options.Add(Option("below", "10% abaixo do mercado", "Folha menor; moral mais baixa e mais pedidos de demissão.", 0, CostLine.Staff,
                M(Lever.PayrollFactor, 0.9), M(Lever.MoraleDelta, -12), M(Lever.TurnoverHazard, 1.8)));
            salary.options.Add(Option("market", "Na média do mercado", "Referência.", 0, CostLine.Staff));
            salary.options.Add(Option("above", "10% acima do mercado", "Folha maior; moral alta e equipe estável.", 0, CostLine.Staff,
                M(Lever.PayrollFactor, 1.1), M(Lever.MoraleDelta, 10), M(Lever.TurnoverHazard, 0.6), M(Lever.ProductivityFactor, 1.02)));
            yield return salary;

            var training = new PolicyCategory { id = "training", displayName = "Treinamento", description = "Capacitação contínua da equipe.", defaultOptionId = "none" };
            training.options.Add(Option("none", "Sem treinamento", "Sem custo.", 0, CostLine.Staff));
            training.options.Add(Option("basic", "Básico", "R$ 700/mês. Menos erros e um pouco mais de produtividade.", 700, CostLine.Staff,
                M(Lever.TrainingLevel, 1), M(Lever.ErrorRateFactor, 0.75), M(Lever.ProductivityFactor, 1.04), M(Lever.MoraleDelta, 3)));
            training.options.Add(Option("advanced", "Avançado", "R$ 1.800/mês. Erros caem quase pela metade; qualidade percebida sobe.", 1800, CostLine.Staff,
                M(Lever.TrainingLevel, 2), M(Lever.ErrorRateFactor, 0.55), M(Lever.ProductivityFactor, 1.08), M(Lever.MoraleDelta, 6), M(Lever.QualityBonus, 0.03)));
            yield return training;
        }

        // ---------------------------------------------------------------- Espelho dos assets

        /// <summary>Capital inicial por tipo, como em GameSessionState.SetRestaurant.</summary>
        public static double InitialCapital(string restaurantTypeId)
        {
            switch (restaurantTypeId)
            {
                case "PODRAO": return 65000d;
                case "JAPONES": return 69000d;
                case "FRANCES": return 72000d;
                default: return 65000d;
            }
        }

        public static SimulationCatalog CreateCatalog(SimulationConfig config = null)
        {
            var cfg = config ?? Create();
            var catalog = new SimulationCatalog { Config = cfg };

            void AddLocation(string id, string zone, string name, double rent, string primary, string competition, double priceFactor, double capacity, double demand)
            {
                catalog.Locations[id] = new LocationSpec
                {
                    Id = id, ZoneId = zone, Name = name, Rent = rent, PrimarySegment = primary,
                    CompetitionIndex = MarketModel.CompetitionIndex(competition, cfg.market),
                    ReferencePriceFactor = priceFactor, PhysicalCapacity = capacity, BaseDailyDemand = demand,
                    Profile = cfg.Location(id)
                };
            }
            AddLocation("bank", "Financas", "Área Financeira", 11000, "HIGH", "MEDIUM", 1.15, 60, 70);
            AddLocation("condominium", "Residencial", "Área Residencial", 5500, "MEDIUM", "LOW", 1.0, 50, 55);
            AddLocation("marketing", "Servicos", "Área Corporativa", 7000, "HIGH", "MEDIUM", 1.05, 58, 60);
            AddLocation("store", "Comercio", "Área Comercial", 8000, "MEDIUM", "HIGH", 1.0, 65, 80);
            AddLocation("university", "Educacao", "Área Educacional", 6000, "LOW", "HIGH", 0.9, 55, 65);

            ProductSpec Product(string id, string name, double price, double ratio, double min, double max)
                => new ProductSpec { Id = id, Name = name, ReferencePrice = price, UnitCost = price * ratio, MinPrice = min, MaxPrice = max };

            void AddRestaurant(string type, string name, string specialist, params ProductSpec[] products)
            {
                var spec = new RestaurantSpec { TypeId = type, Name = name, Profile = cfg.Restaurant(type) };
                spec.Products.AddRange(products);
                spec.RequiredRoles["attendant"] = 2;
                spec.RequiredRoles[specialist] = 1;
                catalog.Restaurants[type] = spec;
            }
            AddRestaurant("PODRAO", "Lanches", "grill_cook",
                Product("LAN_COMBO_SIMPLES", "Combo simples", 42, 0.33333334, 35, 52),
                Product("LAN_COMBO_COMPLETO", "Combo completo", 58, 0.3275862, 47.5, 70),
                Product("LAN_COMBO_ESPECIAL", "Combo especial", 80, 0.3375, 67.5, 95));
            AddRestaurant("JAPONES", "Japonês", "sushi_chef",
                Product("JAP_SUSHI_EXECUTIVO", "Sushi executivo", 90, 0.35555556, 71, 110),
                Product("JAP_COMBINADO_MEDIO", "Combinado médio", 130, 0.36923078, 107, 160),
                Product("JAP_COMBINADO_PREMIUM", "Combinado premium", 175, 0.36, 140, 210));
            AddRestaurant("FRANCES", "Francês", "head_chef",
                Product("FRA_PRATO_EXECUTIVO", "Prato executivo", 110, 0.34545454, 84.5, 130),
                Product("FRA_MENU_CLASSICO", "Menu clássico", 150, 0.36666667, 122.5, 180),
                Product("FRA_MENU_PREMIUM", "Menu premium", 215, 0.3627907, 173.5, 260));

            void AddRole(string id, string name, double salary, double quality)
                => catalog.Roles[id] = new RoleSpec { Id = id, Name = name, Salary = salary, QualityContribution = quality, Profile = cfg.Role(id) };
            AddRole("attendant", "Atendente", 2200, 0.02);
            AddRole("grill_cook", "Chapeiro", 2500, 0.05);
            AddRole("sushi_chef", "Sushiman", 4500, 0.10);
            AddRole("head_chef", "Chef de cozinha", 8000, 0.15);
            AddRole("manager", "Gerente", 5000, 0.10);

            void AddEquipment(string id, string name, double cost, params string[] types)
            {
                var profile = cfg.EquipmentProfileFor(id);
                var spec = new EquipmentSpec
                {
                    Id = id, Name = name, Cost = cost,
                    CategoryId = profile?.categoryId ?? EquipmentCategoryId.Preparation,
                    TierId = profile?.tierId ?? EquipmentTierId.Standard,
                    QualityBonus = profile?.qualityBonus ?? 0d,
                    KitchenCapacityBonus = profile?.kitchenCapacityBonus ?? 0d,
                    PhysicalCapacityBonus = profile?.physicalCapacityBonus ?? 0d
                };
                spec.ApplicableTypes.AddRange(types);
                catalog.Equipment[id] = spec;
            }
            AddEquipment("adega_climatizada", "Adega Climatizada", 4500, "FRANCES");
            AddEquipment("balcao_refrigerado_sushi", "Balcão Refrigerado p/ Sushi", 5500, "JAPONES");
            AddEquipment("bancada_inox_preparo", "Bancada de Inox para preparo", 1500);
            AddEquipment("chapa_profissional_podrao", "Chapa Profissional", 1500, "PODRAO");
            AddEquipment("conjunto_panelas_industriais", "Conjunto de Panelas Industriais", 1200);
            AddEquipment("facas_especiais_japones", "Facas Especiais", 1200, "JAPONES");
            AddEquipment("fogao_industrial_6_bocas", "Fogão Industrial 6 bocas", 2500);
            AddEquipment("forno_combinado_inteligente", "Forno Combinado Inteligente", 18000, "FRANCES");
            AddEquipment("freezer_horizontal", "Freezer Horizontal", 3000);
            AddEquipment("fritadeira_eletrica_dupla_podrao", "Fritadeira Elétrica Dupla", 1800, "PODRAO");
            AddEquipment("geladeira_comercial_dupla", "Geladeira Comercial Dupla", 4500);
            AddEquipment("mobiliario_basico", "Mobiliário Básico (10 mesas/40 cadeiras)", 6000);
            AddEquipment("mobiliario_luxo", "Mobiliário de Luxo", 12000, "FRANCES");
            AddEquipment("panela_eletrica_arroz", "Panela Elétrica de Arroz", 800, "JAPONES");
            AddEquipment("pia_industrial_inox", "Pia Industrial de Inox", 1800);
            AddEquipment("sistema_pdv_computador", "Sistema PDV + Computador", 3500);

            void AddCredit(string id, string name, double max, double rate) => catalog.Credits[id] = new CreditSpec { Id = id, Name = name, MaxAmount = max, MonthlyRate = rate };
            AddCredit("sem_credito", "Sem empréstimo", 0, 0);
            AddCredit("microcredito_empresarial", "Microcrédito Empresarial", 50000, 0.02);
            AddCredit("credito_empresarial_padrao", "Crédito Empresarial Padrão", 120000, 0.03);
            AddCredit("credito_de_investimento", "Crédito de Investimento", 250000, 0.04);
            return catalog;
        }

        /// <summary>Equipamentos basicos (categoria BASIC no asset) + especificos do tipo.</summary>
        public static List<string> BasicEquipment()
        {
            return new List<string>
            {
                "bancada_inox_preparo", "conjunto_panelas_industriais", "fogao_industrial_6_bocas", "freezer_horizontal",
                "geladeira_comercial_dupla", "mobiliario_basico", "pia_industrial_inox", "sistema_pdv_computador"
            };
        }

        public static List<string> SpecificEquipment(string restaurantTypeId)
        {
            switch (restaurantTypeId)
            {
                case "PODRAO": return new List<string> { "chapa_profissional_podrao", "fritadeira_eletrica_dupla_podrao" };
                case "JAPONES": return new List<string> { "balcao_refrigerado_sushi", "facas_especiais_japones", "panela_eletrica_arroz" };
                case "FRANCES": return new List<string> { "adega_climatizada", "forno_combinado_inteligente", "mobiliario_luxo" };
                default: return new List<string>();
            }
        }

        public static string Specialist(string restaurantTypeId)
        {
            switch (restaurantTypeId)
            {
                case "JAPONES": return "sushi_chef";
                case "FRANCES": return "head_chef";
                default: return "grill_cook";
            }
        }
    }
}
