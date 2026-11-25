// Assets/Editor/Tests/Simulation/SampleCatalog.cs
// Usado pelos testes e pelo menu Game > Simulação > Criar ou atualizar SimulationConfig.
// Catálogo de EXEMPLO com os números do GDD. O que o GDD não informa foi preenchido
// com valores marcados "(exemplo)": mix completo de público por região, preços dos
// equipamentos, consumo de energia e os betas dos eventos. No jogo, tudo isso vem do
// SimulationConfig.asset e dos ScriptableObjects pelo UnitySimulationCatalogProvider.
using System.Collections.Generic;
using Game.Domain.Simulation;
using static Game.Domain.Simulation.LeverKeys;

public static class SampleCatalog
{
    public static SimulationCatalog Build()
    {
        var c = new SimulationCatalog { ConfigVersion = "gdd-2026-11" };

        // ---------------- segmentos (GDD)
        c.Segments[Segment.LOW] = new SegmentProfile { Segment = Segment.LOW, ReferenceSpend = 45, Sensitivity = 1.15, CityShare = 0.35 };
        c.Segments[Segment.MEDIUM] = new SegmentProfile { Segment = Segment.MEDIUM, ReferenceSpend = 95, Sensitivity = 1.00, CityShare = 0.53 };
        c.Segments[Segment.HIGH] = new SegmentProfile { Segment = Segment.HIGH, ReferenceSpend = 210, Sensitivity = 0.85, CityShare = 0.12 };

        // ---------------- regiões (GDD; mix das classes que não são a predominante = exemplo)
        Region(c, "bank", "Área Financeira", 11000, 70, 60, 1.15, 1.00, (Segment.HIGH, .45), (Segment.MEDIUM, .40), (Segment.LOW, .15),
               new[] { 1.15, 1.15, 1.15, 1.15, 1.0, 0.7, 0.55 });
        Region(c, "store", "Área Comercial", 8000, 80, 65, 1.00, 1.25, (Segment.MEDIUM, .50), (Segment.LOW, .30), (Segment.HIGH, .20),
               new[] { 0.95, 0.95, 1.0, 1.0, 1.1, 1.2, 0.8 });
        Region(c, "marketing", "Área Corporativa", 7000, 60, 58, 1.05, 1.00, (Segment.MEDIUM, .43), (Segment.HIGH, .32), (Segment.LOW, .25),
               new[] { 1.15, 1.15, 1.15, 1.15, 1.0, 0.6, 0.5 });
        Region(c, "university", "Área Educacional", 6000, 65, 55, 0.90, 1.25, (Segment.LOW, .58), (Segment.MEDIUM, .34), (Segment.HIGH, .08),
               new[] { 1.1, 1.1, 1.1, 1.1, 1.0, 0.7, 0.6 });
        Region(c, "condominium", "Área Residencial", 5500, 55, 50, 1.00, 0.80, (Segment.MEDIUM, .55), (Segment.LOW, .30), (Segment.HIGH, .15),
               new[] { 0.9, 0.9, 0.9, 0.95, 1.1, 1.3, 1.1 });

        // ---------------- restaurantes e pratos (GDD)
        c.Restaurants[RestaurantType.PODRAO] = new RestaurantProfile
        {
            Type = RestaurantType.PODRAO, Name = "Lanches", AllowedSegments = new[] { Segment.LOW, Segment.MEDIUM },
            InitialCapital = 65000, SpecialistRole = RoleType.CHAPEIRO, AttendantCapacityPerDay = 35, BaseServiceMinutes = 15,
            PerishabilityPerDay = 0.012, CmvMin = 0.30, CmvMax = 0.38, IsPremium = false,
            Attractiveness = { [Segment.LOW] = 1.00, [Segment.MEDIUM] = 0.80, [Segment.HIGH] = 0.45 },
            Products = { P("combo_simples", "Combo simples", 42, 35, 52, 14), P("combo_completo", "Combo completo", 58, 47.5, 70, 19), P("combo_especial", "Combo especial", 80, 67.5, 95, 27) }
        };
        c.Restaurants[RestaurantType.JAPONES] = new RestaurantProfile
        {
            Type = RestaurantType.JAPONES, Name = "Japonês", AllowedSegments = new[] { Segment.MEDIUM, Segment.HIGH },
            InitialCapital = 69000, SpecialistRole = RoleType.SUSHIMAN, AttendantCapacityPerDay = 30, BaseServiceMinutes = 25,
            PerishabilityPerDay = 0.035, CmvMin = 0.33, CmvMax = 0.42, IsPremium = true,
            Attractiveness = { [Segment.LOW] = 0.55, [Segment.MEDIUM] = 1.00, [Segment.HIGH] = 0.90 },
            Products = { P("sushi_executivo", "Sushi executivo", 90, 71, 110, 32), P("combinado_medio", "Combinado médio", 130, 107, 160, 48), P("combinado_premium", "Combinado premium", 175, 140, 210, 63) }
        };
        c.Restaurants[RestaurantType.FRANCES] = new RestaurantProfile
        {
            Type = RestaurantType.FRANCES, Name = "Francês", AllowedSegments = new[] { Segment.HIGH },
            InitialCapital = 72000, SpecialistRole = RoleType.CHEF, AttendantCapacityPerDay = 25, BaseServiceMinutes = 40,
            PerishabilityPerDay = 0.022, CmvMin = 0.32, CmvMax = 0.41, IsPremium = true,
            Attractiveness = { [Segment.LOW] = 0.25, [Segment.MEDIUM] = 0.60, [Segment.HIGH] = 1.00 },
            Products = { P("prato_executivo", "Prato executivo", 110, 84.5, 130, 38), P("menu_classico", "Menu clássico", 150, 122.5, 180, 55), P("menu_premium", "Menu premium", 215, 173.5, 260, 78) }
        };

        // ---------------- cargos (GDD)
        c.Roles[RoleType.ATENDENTE] = new RoleProfile { Role = RoleType.ATENDENTE, Salary = 2200 };
        c.Roles[RoleType.CHAPEIRO] = new RoleProfile { Role = RoleType.CHAPEIRO, Salary = 2500, KitchenCapacityPerDay = 50 };
        c.Roles[RoleType.SUSHIMAN] = new RoleProfile { Role = RoleType.SUSHIMAN, Salary = 4500, KitchenCapacityPerDay = 45 };
        c.Roles[RoleType.CHEF] = new RoleProfile { Role = RoleType.CHEF, Salary = 8000, KitchenCapacityPerDay = 35 };
        c.Roles[RoleType.GERENTE] = new RoleProfile { Role = RoleType.GERENTE, Salary = 5000 };

        // ---------------- níveis e categorias de equipamento (GDD)
        c.Levels[EquipmentLevel.Economy] = new LevelProfile { Level = EquipmentLevel.Economy, HazardRatio = 1.9, EnergyFactor = 1.30, LifeMonths = 36, RepairFraction = .25, QualityDelta = -.04, StockLossFactor = 1.3 };
        c.Levels[EquipmentLevel.Standard] = new LevelProfile { Level = EquipmentLevel.Standard, HazardRatio = 1.0, EnergyFactor = 1.00, LifeMonths = 60, RepairFraction = .20, QualityDelta = 0, StockLossFactor = 1.0 };
        c.Levels[EquipmentLevel.Premium] = new LevelProfile { Level = EquipmentLevel.Premium, HazardRatio = 0.5, EnergyFactor = 0.78, LifeMonths = 96, RepairFraction = .15, QualityDelta = .06, StockLossFactor = 0.7 };
        c.Categories[EquipmentReliabilityCategory.Refrigeration] = new CategoryProfile { Category = EquipmentReliabilityCategory.Refrigeration, FailuresPerMonth = 0.07, KitchenFactorWhenDown = 0.8, StockLossOnFailure = 0.20, FailureEventId = "freezer_breakdown" };
        c.Categories[EquipmentReliabilityCategory.Cooking] = new CategoryProfile { Category = EquipmentReliabilityCategory.Cooking, FailuresPerMonth = 0.05, KitchenFactorWhenDown = 0.45, FailureEventId = "kitchen_equipment_failure" };
        c.Categories[EquipmentReliabilityCategory.PointOfSale] = new CategoryProfile { Category = EquipmentReliabilityCategory.PointOfSale, FailuresPerMonth = 0.04, ServiceFactorWhenDown = 0.85, FailureEventId = "payment_system_failure" };
        c.Categories[EquipmentReliabilityCategory.Preparation] = new CategoryProfile { Category = EquipmentReliabilityCategory.Preparation, FailuresPerMonth = 0.004, KitchenFactorWhenDown = 0.9 };
        c.Categories[EquipmentReliabilityCategory.DiningRoom] = new CategoryProfile { Category = EquipmentReliabilityCategory.DiningRoom, FailuresPerMonth = 0.003, DiningFactorWhenDown = 0.8 };

        // ---------------- equipamentos (itens do GDD; preço e kWh = exemplo)
        E(c, "bancada", "Bancada", 2500, EquipmentReliabilityCategory.Preparation);
        E(c, "panelas", "Panelas", 1500, EquipmentReliabilityCategory.Preparation);
        E(c, "fogao6", "Fogão 6 bocas", 4000, EquipmentReliabilityCategory.Cooking);
        E(c, "freezer", "Freezer", 5000, EquipmentReliabilityCategory.Refrigeration, kwh: 120);
        E(c, "geladeira", "Geladeira dupla", 6000, EquipmentReliabilityCategory.Refrigeration, kwh: 150);
        E(c, "mobiliario", "Mobiliário básico", 8000, EquipmentReliabilityCategory.DiningRoom);
        E(c, "pia", "Pia", 1200, EquipmentReliabilityCategory.Preparation);
        E(c, "pdv", "PDV", 2500, EquipmentReliabilityCategory.PointOfSale, kwh: 15);
        E(c, "chapa", "Chapa", 3500, EquipmentReliabilityCategory.Cooking, kitchen: 8, kwh: 60);
        E(c, "fritadeira", "Fritadeira", 2800, EquipmentReliabilityCategory.Cooking, kwh: 50);
        E(c, "balcao_refrigerado", "Balcão refrigerado", 9000, EquipmentReliabilityCategory.Refrigeration, kwh: 180);
        E(c, "facas", "Facas", 1800, EquipmentReliabilityCategory.Preparation);
        E(c, "panela_arroz", "Panela de arroz", 1500, EquipmentReliabilityCategory.Cooking, kwh: 30);
        E(c, "adega", "Adega", 7000, EquipmentReliabilityCategory.Refrigeration, kwh: 90);
        E(c, "forno_combinado", "Forno combinado", 15000, EquipmentReliabilityCategory.Cooking, kitchen: 8, kwh: 200, level: EquipmentLevel.Premium);
        E(c, "mobiliario_luxo", "Mobiliário de luxo", 12000, EquipmentReliabilityCategory.DiningRoom, dining: 5, level: EquipmentLevel.Premium);

        // ---------------- crédito (GDD)
        c.CreditLines["micro"] = new CreditLineProfile { Id = "micro", Name = "Microcrédito Empresarial", Limit = 50000, MonthlyRate = 0.02 };
        c.CreditLines["padrao"] = new CreditLineProfile { Id = "padrao", Name = "Crédito Empresarial Padrão", Limit = 120000, MonthlyRate = 0.03 };
        c.CreditLines["investimento"] = new CreditLineProfile { Id = "investimento", Name = "Crédito de Investimento", Limit = 250000, MonthlyRate = 0.04 };

        // ---------------- políticas (GDD) como listas de alavancas
        Policy(c, "marketing", "none", "Sem divulgação", 0);
        Policy(c, "marketing", "low", "Marketing baixo", 1500, L(MarketingMaxGain, .05), L(MarketingReach, .03), L(MarketingInvestment, .35));
        Policy(c, "marketing", "medium", "Marketing médio", 4000, L(MarketingMaxGain, .15), L(MarketingReach, .08), L(MarketingInvestment, .35));
        Policy(c, "marketing", "high", "Marketing alto", 8000, L(MarketingMaxGain, .30), L(MarketingReach, .18), L(MarketingInvestment, .35));
        Policy(c, "loyalty", "none", "Sem programa", 0);
        Policy(c, "loyalty", "program", "Programa de fidelidade", 900, L(LoyaltyStrength, .4)).CostPerOrder = 0.50;
        Policy(c, "supplier", "economy", "Fornecedor econômico", 0, L(SuppliesCostFactor, .92), L(QualityAdd, -.07), L(EventHr("supplier_delay"), 1.8));
        Policy(c, "supplier", "standard", "Fornecedor padrão", 0);
        Policy(c, "supplier", "premium", "Fornecedor premium", 0, L(SuppliesCostFactor, 1.12), L(QualityAdd, .10), L(EventHr("supplier_delay"), 0.5));
        Policy(c, "supplierPayment", "cash", "À vista (2% de desconto)", 0);
        Policy(c, "supplierPayment", "installments", "Duas parcelas", 0, L(PayInInstallments, 1));
        Policy(c, "maintenance", "corrective", "Somente corretiva", 0);
        Policy(c, "maintenance", "preventive", "Manutenção preventiva", 0, L(FailureHr, .55), L(PreventiveFlag, 1)).AssetValueRate = 0.012;
        Policy(c, "maintenance", "intensive", "Manutenção intensiva", 0, L(FailureHr, .40), L(PreventiveFlag, 1)).AssetValueRate = 0.025;
        Policy(c, "stock", "low", "Estoque baixo (2 dias)", 0, L(StockCoverDays, 2));
        Policy(c, "stock", "medium", "Estoque médio (4 dias)", 0, L(StockCoverDays, 4));
        Policy(c, "stock", "high", "Estoque alto (6 dias)", 0, L(StockCoverDays, 6));
        Policy(c, "pay", "below", "Remuneração 10% abaixo", 0, L(PayrollFactor, .9), L(MoraleAdd, -12), L(ResignationHr, 1.8));
        Policy(c, "pay", "market", "Remuneração de mercado", 0);
        Policy(c, "pay", "above", "Remuneração 10% acima", 0, L(PayrollFactor, 1.1), L(MoraleAdd, 10), L(ResignationHr, .6));
        Policy(c, "training", "none", "Sem treinamento", 0);
        Policy(c, "training", "basic", "Treinamento básico", 700, L(ErrorsFactor, .75), L(ProductivityAdd, .04));
        Policy(c, "training", "advanced", "Treinamento avançado", 1800, L(ErrorsFactor, .55), L(ProductivityAdd, .08));

        // ---------------- eventos (subconjunto do GDD; betas = exemplo)
        // A ordem da lista é a prioridade no limite do dia: equipamento primeiro.
        c.Events.Add(EquipmentEvent("freezer_breakdown", "Problema na refrigeração"));
        c.Events.Add(EquipmentEvent("kitchen_equipment_failure", "Equipamento de cozinha quebrou"));
        c.Events.Add(new EventDefinition
        {
            Id = "payment_system_failure", Title = "Falha no sistema de pagamento", Category = "Equipamento", Scope = EventScope.PerEquipmentUnit,
            IsNegative = true, Severity = 1, RequiresDecision = true,
            Reactions =
            {
                new ReactionDefinition { Id = "support", Label = "Suporte emergencial", CostKind = ReactionCostKind.Fixed, CostValue = 400, RepairsUnit = true },
                new ReactionDefinition { Id = "alternative", Label = "Pagamentos alternativos", Effects = { new EffectDefinition(ServiceCapFactor, 1.1, EffectDuration.UntilRepair) } },
                new ReactionDefinition { Id = "wait", Label = "Aguardar" }
            }
        });
        c.Events.Add(new EventDefinition
        {
            Id = EventIds.EquipmentStillDown, Title = "Equipamento continua parado", Category = "Equipamento", Deterministic = true,
            RequiresDecision = true,
            Reactions =
            {
                new ReactionDefinition { Id = "repair", Label = "Consertar", CostKind = ReactionCostKind.EquipmentRepair, CostValue = 1.0, CostExtra = 300, RepairsUnit = true },
                new ReactionDefinition { Id = "postpone", Label = "Continuar adiando" }
            }
        });
        c.Events.Add(new EventDefinition
        {
            Id = EventIds.CashShortfall, Title = "Caixa insuficiente para a parcela", Category = "Financeiro", Deterministic = true,
            RequiresDecision = true, IsNegative = true, Severity = 1,
            Reactions =
            {
                new ReactionDefinition { Id = "overdraft", Label = "Usar cheque especial", UsesOverdraft = true },
                new ReactionDefinition { Id = "renegotiate", Label = "Renegociar", Renegotiates = true }
            }
        });
        c.Events.Add(new EventDefinition
        {
            Id = EventIds.StaffAbsent, Title = "Funcionário ausente", Category = "Equipe", Scope = EventScope.PerEmployee,
            IsNegative = true, Severity = 0.5, RequiresDecision = true,
            Reactions =
            {
                new ReactionDefinition { Id = "substitute", Label = "Substituto temporário", CostKind = ReactionCostKind.Fixed, CostValue = 350, KeepsStaff = true },
                new ReactionDefinition { Id = "reduced", Label = "Trabalhar com equipe reduzida" }
            }
        });
        c.Events.Add(new EventDefinition
        {
            Id = EventIds.StaffResigned, Title = "Funcionário pediu demissão", Category = "Equipe", Scope = EventScope.PerEmployee,
            IsNegative = true, Severity = 1, RequiresDecision = true,
            Reactions =
            {
                new ReactionDefinition { Id = "hire", Label = "Contratar substituto", CostKind = ReactionCostKind.SalaryFraction, CostValue = 0.5, KeepsStaff = true },
                new ReactionDefinition { Id = "leave", Label = "Deixar a vaga aberta" }
            }
        });
        c.Events.Add(new EventDefinition
        {
            Id = "supplier_delay", Title = "Atraso do fornecedor", Category = "Fornecedor", Group = "SUPPLIER", Scope = EventScope.Global,
            BaseRatePerMonth = 1.2, IsNegative = true, Severity = 1, RequiresDecision = true, ActiveDays = 2, MinIntervalDays = 3,
            Reactions =
            {
                new ReactionDefinition { Id = "emergency", Label = "Fornecedor emergencial", CostKind = ReactionCostKind.Fixed, CostValue = 500,
                                         Effects = { new EffectDefinition(SuppliesCostFactor, 1.15, EffectDuration.Days, 2) } },
                new ReactionDefinition { Id = "reduce_menu", Label = "Reduzir cardápio",
                                         Effects = { new EffectDefinition(DeliveryFactor, 0, EffectDuration.Days, 2), new EffectDefinition(DemandFactor, 0.92, EffectDuration.Days, 2) } },
                new ReactionDefinition { Id = "use_stock", Label = "Usar o estoque",
                                         Effects = { new EffectDefinition(DeliveryFactor, 0, EffectDuration.Days, 2) } }
            }
        });
        c.Events.Add(new EventDefinition
        {
            Id = "quality_complaint", Title = "Reclamação sobre a qualidade", Category = "Reputação", Group = "SOCIAL_REPUTATION", Scope = EventScope.Global,
            BaseRatePerMonth = 0.45, IsNegative = true, Severity = 1, RequiresDecision = true, MinIntervalDays = 5,
            Covariates = { new Covariate(Observables.Quality, 0.5, -4.0), new Covariate(Observables.ErrorRate, 0.04, 10.0) },
            Reactions =
            {
                new ReactionDefinition { Id = "refund", Label = "Reembolsar", CostKind = ReactionCostKind.Fixed, CostValue = 250 },
                new ReactionDefinition { Id = "apologize", Label = "Pedir desculpas", Effects = { new EffectDefinition(UnresolvedNegative, 1, EffectDuration.Days, 1) } },
                new ReactionDefinition { Id = "ignore", Label = "Ignorar", Effects = { new EffectDefinition(UnresolvedNegative, 1, EffectDuration.Days, 3) } }
            }
        });
        c.Events.Add(new EventDefinition
        {
            Id = "negative_review", Title = "Avaliação negativa nas redes", Category = "Reputação", Group = "SOCIAL_REPUTATION", Scope = EventScope.Global,
            BaseRatePerMonth = 0.25, IsNegative = true, Severity = 1, RequiresDecision = true, MinIntervalDays = 5,
            Covariates = { new Covariate(Observables.Satisfaction, 60, -0.05), new Covariate(Observables.WaitFactor, 1.0, 0.8), new Covariate(Observables.LostRatio, 0.05, 5.0) },
            Reactions =
            {
                new ReactionDefinition { Id = "compensate", Label = "Compensar o cliente", CostKind = ReactionCostKind.Fixed, CostValue = 300 },
                new ReactionDefinition { Id = "reply", Label = "Responder publicamente" },
                new ReactionDefinition { Id = "ignore", Label = "Ignorar", Effects = { new EffectDefinition(UnresolvedNegative, 1, EffectDuration.Days, 3), new EffectDefinition(DemandFactor, 0.95, EffectDuration.Days, 5) } }
            }
        });
        c.Events.Add(new EventDefinition
        {
            Id = "excessive_food_waste", Title = "Desperdício acima do esperado", Category = "Operação", Scope = EventScope.Global,
            BaseRatePerMonth = 3.0, IsNegative = true, Severity = 0.5, RequiresDecision = true, MinIntervalDays = 7,
            Conditions = { new Condition(Observables.WasteRatio, ">=", 0.07) },
            Reactions =
            {
                new ReactionDefinition { Id = "adjust", Label = "Ajustar o planejamento", Effects = { new EffectDefinition(StockCoverDays, -1, EffectDuration.EndOfMonth) } },
                new ReactionDefinition { Id = "promo", Label = "Promoção", Effects = { new EffectDefinition(DemandFactor, 1.08, EffectDuration.Days, 3), new EffectDefinition(QualityAdd, -0.02, EffectDuration.Days, 3) } },
                new ReactionDefinition { Id = "keep", Label = "Manter" }
            }
        });
        c.Events.Add(new EventDefinition
        {
            Id = "heavy_rain", Title = "Chuva forte", Category = "Mercado", Group = "DEMAND_SPIKE", Scope = EventScope.Global,
            BaseRatePerMonth = 1.8, IsNegative = true, Severity = 0.5, RequiresDecision = true,
            Reactions =
            {
                new ReactionDefinition { Id = "normal", Label = "Operação normal", Effects = { new EffectDefinition(DemandFactor, 0.70, EffectDuration.Days, 1) } },
                new ReactionDefinition { Id = "delivery", Label = "Pedidos externos", CostKind = ReactionCostKind.Fixed, CostValue = 300, Effects = { new EffectDefinition(DemandFactor, 0.90, EffectDuration.Days, 1) } }
            }
        });
        c.Events.Add(new EventDefinition
        {
            Id = "new_competitor", Title = "Novo concorrente na região", Category = "Mercado", Group = "COMPETITION", Scope = EventScope.Global,
            BaseRatePerMonth = 0.15, IsNegative = true, Severity = 1, RequiresDecision = true, MaxPerMatch = 1,
            Covariates = { new Covariate(Observables.PreviousMargin, 0.10, 3.0), new Covariate(Observables.CompetitionPressure, 1.0, -1.0) },
            AutomaticEffects = { new EffectDefinition(CompetitionAdd, 0.25, EffectDuration.EndOfMatch) },
            Reactions =
            {
                new ReactionDefinition { Id = "keep", Label = "Manter" },
                new ReactionDefinition { Id = "ads", Label = "Divulgação", CostKind = ReactionCostKind.Fixed, CostValue = 1500, Effects = { new EffectDefinition(DemandFactor, 1.05, EffectDuration.EndOfMonth) } },
                new ReactionDefinition { Id = "loyalty", Label = "Fidelização", CostKind = ReactionCostKind.Fixed, CostValue = 800, Effects = { new EffectDefinition(LoyaltyStrength, 0.2, EffectDuration.EndOfMonth) } }
            }
        });
        c.Events.Add(new EventDefinition
        {
            Id = "lunch_demand_peak", Title = "Pico de demanda no almoço", Category = "Mercado", Group = "DEMAND_SPIKE", Scope = EventScope.Global,
            BaseRatePerMonth = 0.5, RequiresDecision = true,
            Reactions =
            {
                new ReactionDefinition { Id = "current", Label = "Capacidade atual", Effects = { new EffectDefinition(DemandFactor, 1.15, EffectDuration.Days, 1) } },
                new ReactionDefinition { Id = "reinforce", Label = "Reforço", CostKind = ReactionCostKind.Fixed, CostValue = 400,
                                         Effects = { new EffectDefinition(DemandFactor, 1.15, EffectDuration.Days, 1), new EffectDefinition(ServiceCapFactor, 1.2, EffectDuration.Days, 1) } }
            }
        });
        c.Events.Add(new EventDefinition
        {
            Id = "supplier_discount", Title = "Desconto do fornecedor", Category = "Fornecedor", Group = "SUPPLIER", Scope = EventScope.Global,
            BaseRatePerMonth = 0.3, RequiresDecision = true,
            Reactions =
            {
                new ReactionDefinition { Id = "take", Label = "Aproveitar o desconto", Effects = { new EffectDefinition(SuppliesCostFactor, 0.90, EffectDuration.Days, 5) } },
                new ReactionDefinition { Id = "skip", Label = "Não aproveitar" }
            }
        });
        return c;
    }

    // ---------------------------------------------------------------- auxiliares
    private static void Region(SimulationCatalog c, string id, string name, double rent, double demand, double capacity, double priceFactor,
                               double pressure, (Segment, double) a, (Segment, double) b, (Segment, double) d, double[] weekday)
    {
        c.Regions[id] = new RegionProfile
        {
            Id = id, Name = name, Rent = rent, BaseDemandPerDay = demand, PhysicalCapacityPerDay = capacity, PriceFactor = priceFactor,
            CompetitionPressure = pressure, Mix = { [a.Item1] = a.Item2, [b.Item1] = b.Item2, [d.Item1] = d.Item2 }, WeekdayProfile = weekday
        };
    }

    private static ProductProfile P(string id, string name, double reference, double min, double max, double cost)
        => new ProductProfile { Id = id, Name = name, ReferencePrice = reference, MinPrice = min, MaxPrice = max, UnitCost = cost };

    private static void E(SimulationCatalog c, string id, string name, double price, EquipmentReliabilityCategory category,
                          double kitchen = 0, double dining = 0, double kwh = 0, EquipmentLevel level = EquipmentLevel.Standard)
        => c.Equipment[id] = new EquipmentProfile { Id = id, Name = name, Price = price, Category = category, Level = level, KitchenCapacityBonus = kitchen, DiningCapacityBonus = dining, KwhPerMonth = kwh };

    private static Lever L(string key, double value) => new Lever(key, value);

    private static PolicyOption Policy(SimulationCatalog c, string policy, string option, string label, double cost, params Lever[] levers)
    {
        if (!c.Policies.TryGetValue(policy, out var list)) c.Policies[policy] = list = new List<PolicyOption>();
        var o = new PolicyOption { PolicyId = policy, OptionId = option, Label = label, FixedMonthlyCost = cost, Levers = new List<Lever>(levers) };
        list.Add(o);
        return o;
    }

    private static EventDefinition EquipmentEvent(string id, string title) => new EventDefinition
    {
        Id = id, Title = title, Category = "Equipamento", Scope = EventScope.PerEquipmentUnit, IsNegative = true, Severity = 1.5, RequiresDecision = true,
        Reactions =
        {
            new ReactionDefinition { Id = "repair", Label = "Consertar", CostKind = ReactionCostKind.EquipmentRepair, CostValue = 1.0, CostExtra = 300, RepairsUnit = true },
            new ReactionDefinition { Id = "emergency", Label = "Reparo emergencial", CostKind = ReactionCostKind.EquipmentRepair, CostValue = 0.4, CostExtra = 150, RepairsUnit = true },
            new ReactionDefinition { Id = "postpone", Label = "Adiar o conserto" }
        }
    };
}
