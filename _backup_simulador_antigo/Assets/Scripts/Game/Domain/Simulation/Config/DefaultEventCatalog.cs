using System.Collections.Generic;

namespace Game.Domain.Simulation
{
    /// <summary>
    /// Catalogo padrao de eventos dinamicos. Os ids e as opcoes dos 20 eventos ja cadastrados
    /// pela Thaysla (Resources/Events e admin-data/events.json) foram mantidos; o adaptador
    /// Unity sobrepoe titulo, descricao e textos das opcoes vindos do EventData, entao o que
    /// for editado na pagina de administracao continua aparecendo no jogo.
    ///
    /// Taxas: "ocorrencias esperadas por 30 dias" nas condicoes de referencia. Fontes:
    /// Bloco 7 da especificacao (chuva 6% ao dia, insumos 10% ao mes, demanda extra 8% por
    /// semana, falta de funcionario 5% ao dia sem gerente) e premissas didaticas documentadas
    /// no tutorial para os demais.
    /// </summary>
    public static class DefaultEventCatalog
    {
        private static Modifier M(string key, double value) => new Modifier(key, value);

        private static EffectSpec Effect(int days, string end, params Modifier[] modifiers)
        {
            var effect = new EffectSpec { durationDays = days, end = end };
            effect.modifiers.AddRange(modifiers);
            return effect;
        }

        private static EffectSpec Days(int days, params Modifier[] modifiers) => Effect(days, EffectEnd.Days, modifiers);

        private static EventReaction R(string id, string title, string description, double cost, EffectSpec effect, string costLine = CostLine.Other)
            => new EventReaction { id = id, title = title, description = description, cost = cost, effect = effect ?? new EffectSpec(), costLine = costLine };

        private static ProbabilityModifier P(string indicator, double coefficient, double reference, string description)
            => new ProbabilityModifier(indicator, coefficient, reference, description);

        private static EventDefinition E(string id, string title, string description, string category, string impact, double ratePerMonth)
            => new EventDefinition { id = id, title = title, description = description, category = category, impact = impact, baseRatePerMonth = ratePerMonth };

        public static List<EventDefinition> Create()
        {
            var list = new List<EventDefinition>();

            // =============================== EQUIPAMENTOS ===============================
            var freezer = E("freezer_breakdown", "Problema na refrigeração",
                "Um equipamento de refrigeração apresentou falha e parte dos alimentos começou a descongelar.",
                EventCategory.Equipment, EventImpact.Negative, 1d);
            freezer.scope = EventScope.EquipmentUnit;
            freezer.equipmentCategory = EquipmentCategoryId.Refrigeration;
            freezer.educationalConcept = "Risco operacional, manutenção e custo total de propriedade";
            freezer.cooldownDays = 0;
            freezer.severity = 1.5d;
            freezer.defaultReactionId = "repair_now";
            freezer.reactions.Add(new EventReaction
            {
                id = "repair_now", title = "Consertar imediatamente", description = "Chamar técnico agora. Custo mais alto, mas reduz impactos.",
                cost = 300, costAsEquipmentPriceFraction = 1d, costLine = CostLine.Maintenance, equipmentAction = EquipmentAction.Repair,
                effect = Days(60, M(Lever.UnitHazard, 0.85)), outcome = "Equipamento consertado por técnico; risco de nova falha um pouco menor nos próximos 60 dias."
            });
            freezer.reactions.Add(new EventReaction
            {
                id = "temporary_repair", title = "Fazer um reparo emergencial", description = "Reparo rápido e barato. A chance de falhar de novo dobra por 30 dias.",
                cost = 150, costAsEquipmentPriceFraction = 0.4d, costLine = CostLine.Maintenance, equipmentAction = EquipmentAction.TemporaryRepair,
                effect = Days(30, M(Lever.UnitHazard, 2.2)), outcome = "Reparo provisório: funciona, mas com risco maior por 30 dias."
            });
            var spare = new EventReaction
            {
                id = "use_spare", title = "Usar refrigeração reserva", description = "Transferir produtos para outro equipamento. Custo moderado; o conserto fica para depois.",
                cost = 600, costLine = CostLine.Maintenance, equipmentAction = EquipmentAction.LeaveBroken,
                effect = Effect(1, EffectEnd.UnitRepaired, M(Lever.SpoilageFactor, 0.6), M(Lever.KitchenCapacityFactor, 0.95)),
                outcome = "Produtos transferidos; perdas contidas enquanto o equipamento segue parado."
            };
            spare.conditions.Add(new EventCondition(Indicator.EquipmentWorking(EquipmentCategoryId.Refrigeration), ">=", 1));
            freezer.reactions.Add(spare);
            var postpone = new EventReaction
            {
                id = "postpone_repair", title = "Adiar o conserto", description = "Continuar operando até piorar. Risco alto de perdas de insumos e de autuação sanitária.",
                cost = 0, equipmentAction = EquipmentAction.LeaveBroken,
                effect = Effect(1, EffectEnd.UnitRepaired, M(Lever.SanitaryHazard, 1.6), M(Lever.EventHazard("storage_loss"), 2.5)),
                outcome = "Conserto adiado: o equipamento continua parado."
            };
            postpone.effect.unresolved = true;
            freezer.reactions.Add(postpone);
            freezer.reactions.Add(new EventReaction
            {
                id = "replace_premium", title = "Substituir por modelo premium", description = "Comprar um equipamento novo de nível premium: investimento imediato, menor risco e menor consumo.",
                equipmentAction = EquipmentAction.Replace, replaceTier = EquipmentTierId.Premium, costLine = CostLine.Capex,
                outcome = "Equipamento substituído por um modelo premium novo."
            });
            list.Add(freezer);

            var kitchen = E("kitchen_equipment_failure", "Equipamento de cozinha quebrou",
                "Um equipamento de cocção parou durante o expediente e a produção caiu.",
                EventCategory.Equipment, EventImpact.Negative, 1d);
            kitchen.scope = EventScope.EquipmentUnit;
            kitchen.equipmentCategory = EquipmentCategoryId.Cooking;
            kitchen.cooldownDays = 0;
            kitchen.severity = 1.5d;
            kitchen.defaultReactionId = "repair_now";
            kitchen.reactions.Add(new EventReaction
            {
                id = "repair_now", title = "Consertar imediatamente", description = "Técnico especializado hoje.",
                cost = 300, costAsEquipmentPriceFraction = 1d, costLine = CostLine.Maintenance, equipmentAction = EquipmentAction.Repair,
                effect = Days(60, M(Lever.UnitHazard, 0.85))
            });
            kitchen.reactions.Add(new EventReaction
            {
                id = "temporary_repair", title = "Fazer um reparo emergencial", description = "Volta a funcionar, com risco dobrado por 30 dias.",
                cost = 150, costAsEquipmentPriceFraction = 0.4d, costLine = CostLine.Maintenance, equipmentAction = EquipmentAction.TemporaryRepair,
                effect = Days(30, M(Lever.UnitHazard, 2.2))
            });
            var kitchenPostpone = new EventReaction
            {
                id = "postpone_repair", title = "Adiar o conserto", description = "Operar com a cozinha reduzida até decidir consertar.",
                equipmentAction = EquipmentAction.LeaveBroken, effect = Effect(1, EffectEnd.UnitRepaired)
            };
            kitchenPostpone.effect.unresolved = true;
            kitchen.reactions.Add(kitchenPostpone);
            kitchen.reactions.Add(new EventReaction
            {
                id = "replace_premium", title = "Substituir por modelo premium", description = "Investimento imediato em um equipamento novo e mais confiável.",
                equipmentAction = EquipmentAction.Replace, replaceTier = EquipmentTierId.Premium, costLine = CostLine.Capex
            });
            list.Add(kitchen);

            var pos = E("payment_system_failure", "Falha no sistema de pagamento",
                "O sistema de pagamento apresenta instabilidade e pode dificultar as vendas.",
                EventCategory.Equipment, EventImpact.Negative, 1d);
            pos.scope = EventScope.EquipmentUnit;
            pos.equipmentCategory = EquipmentCategoryId.PointOfSale;
            pos.cooldownDays = 0;
            pos.defaultReactionId = "emergency_support";
            pos.reactions.Add(new EventReaction
            {
                id = "emergency_support", title = "Acionar suporte emergencial", description = "Suporte resolve hoje.",
                cost = 200, costAsEquipmentPriceFraction = 0.75d, costLine = CostLine.Maintenance, equipmentAction = EquipmentAction.Repair
            });
            pos.reactions.Add(new EventReaction
            {
                id = "alternative_payments", title = "Aceitar pagamentos alternativos", description = "Pix e dinheiro por três dias até o sistema normalizar.",
                equipmentAction = EquipmentAction.TemporaryRepair, effect = Days(3, M(Lever.DemandFactor, 0.94))
            });
            pos.reactions.Add(new EventReaction
            {
                id = "wait_normalization", title = "Aguardar normalização", description = "O sistema volta sozinho, mas o dia de hoje perde vendas.",
                equipmentAction = EquipmentAction.TemporaryRepair, effect = Days(1, M(Lever.DemandFactor, 0.85))
            });
            list.Add(pos);

            var reminder = E(MonthSimulation.ReminderEventId, "Equipamento continua parado",
                "Um equipamento está parado há dias. O conserto tardio custa mais, e o problema continua afetando a operação.",
                EventCategory.Equipment, EventImpact.Negative, 0d);
            reminder.scope = EventScope.EquipmentUnit;
            reminder.kind = EventKind.Deterministic;
            reminder.cooldownDays = 0;
            reminder.defaultReactionId = "keep_waiting";
            reminder.reactions.Add(new EventReaction
            {
                id = "repair_now", title = "Consertar agora", description = "Conserto tardio, 15% mais caro.",
                cost = 300, costAsEquipmentPriceFraction = 1.15d, costLine = CostLine.Maintenance, equipmentAction = EquipmentAction.Repair
            });
            reminder.reactions.Add(new EventReaction
            {
                id = "replace_premium", title = "Substituir por modelo premium", description = "Trocar o equipamento por um novo de nível premium.",
                equipmentAction = EquipmentAction.Replace, replaceTier = EquipmentTierId.Premium, costLine = CostLine.Capex
            });
            reminder.reactions.Add(new EventReaction { id = "keep_waiting", title = "Continuar adiando", description = "Seguir sem o equipamento.", equipmentAction = EquipmentAction.LeaveBroken });
            list.Add(reminder);

            var save = E(MonthSimulation.PreventiveSaveEventId, "Manutenção preventiva evitou uma falha",
                "Na revisão programada, o técnico encontrou e corrigiu um defeito antes que o equipamento parasse.",
                EventCategory.Equipment, EventImpact.Positive, 0d);
            save.scope = EventScope.EquipmentUnit;
            save.kind = EventKind.Deterministic;
            save.requiresDecision = false;
            save.cooldownDays = 10;
            list.Add(save);

            // =============================== EQUIPE ===============================
            var absence = E("insufficient_team", "Funcionário ausente",
                "Um funcionário faltou e o restaurante precisa decidir como manter o atendimento.",
                EventCategory.Staff, EventImpact.Negative, 0.017d * 30d);
            absence.scope = EventScope.Employee;
            absence.cooldownDays = 1;
            absence.severity = 0.5d;
            // Frequente (5% ao dia sem gerente, Bloco 7): resolvido automaticamente para nao interromper o jogo.
            // Mude para true para o aluno escolher a reacao a cada falta.
            absence.requiresDecision = false;
            absence.defaultReactionId = "redistribute_team";
            absence.modifiers.Add(P(Indicator.Morale, -0.02d, 65d, "moral da equipe"));
            absence.modifiers.Add(P(Indicator.Manager, -0.51d, 0d, "presença de gerente"));
            absence.baseEffect = Days(1, M(Lever.AbsentInstant, 1));
            absence.reactions.Add(R("hire_temporary", "Contratar substituto temporário", "Diária de um profissional avulso; capacidade preservada.", 350, Days(1, M(Lever.AbsenceCover, 1)), CostLine.Staff));
            absence.reactions.Add(R("redistribute_team", "Redistribuir as funções", "A equipe cobre parte do trabalho e se cansa um pouco.", 0, Days(1, M(Lever.AbsenceCover, 0.6), M(Lever.MoraleDelta, -2))));
            absence.reactions.Add(R("operate_reduced", "Trabalhar com equipe reduzida", "Menos capacidade hoje e atendimento mais lento.", 0, Days(1, M(Lever.SatisfactionDelta, -3))));
            list.Add(absence);

            var resignation = E("employee_resignation", "Funcionário pediu demissão",
                "Um funcionário pediu demissão. A vaga fica aberta até ser preenchida.",
                EventCategory.Staff, EventImpact.Negative, 0.03d);
            resignation.scope = EventScope.Employee;
            resignation.cooldownDays = 20;
            resignation.severity = 1.5d;
            resignation.defaultReactionId = "hire_replacement";
            resignation.modifiers.Add(P(Indicator.Morale, -0.04d, 65d, "moral da equipe"));
            resignation.baseEffect = Days(0, M(Lever.VacancyInstant, 1));
            var hire = R("hire_replacement", "Contratar substituto", "Recrutamento custa meio salário; a pessoa nova rende menos nas primeiras semanas.", 0,
                Days(14, M(Lever.VacancyInstant, -1), M(Lever.ProductivityFactor, 0.96)), CostLine.Staff);
            hire.chargesRecruitment = true;
            resignation.reactions.Add(hire);
            resignation.reactions.Add(R("counter_offer", "Fazer contraproposta", "Aumento para a equipe toda (+3% na folha até o fim da partida); a vaga não abre.", 0,
                Effect(1, EffectEnd.GameEnd, M(Lever.VacancyInstant, -1), M(Lever.PayrollFactor, 1.03), M(Lever.MoraleDelta, 4)), CostLine.Staff));
            resignation.reactions.Add(R("leave_vacant", "Deixar a vaga aberta", "Economiza o salário, mas reduz a capacidade. A vaga pode ser preenchida depois no RH.", 0, null));
            list.Add(resignation);

            var productivity = E("team_productivity_boost", "Aumento de produtividade da equipe",
                "A equipe apresenta um desempenho acima do esperado, permitindo melhorar o ritmo de atendimento.",
                EventCategory.Staff, EventImpact.Positive, 0.25d);
            productivity.cooldownDays = 15;
            productivity.defaultReactionId = "maintain_pace";
            productivity.modifiers.Add(P(Indicator.Morale, 0.05d, 65d, "moral alta"));
            productivity.modifiers.Add(P(Indicator.Training, 0.5d, 0d, "treinamento"));
            productivity.baseEffect = Days(5, M(Lever.ProductivityFactor, 1.08));
            productivity.reactions.Add(R("maintain_pace", "Manter o ritmo", "Aproveitar sem custo.", 0, null));
            productivity.reactions.Add(R("team_bonus", "Oferecer bônus à equipe", "Reconhecimento eleva a moral por duas semanas.", 500, Days(14, M(Lever.MoraleDelta, 6)), CostLine.Staff));
            productivity.reactions.Add(R("increase_service_volume", "Aumentar o volume de atendimento", "Mais capacidade, com algum desgaste.", 0, Days(5, M(Lever.FrontCapacityFactor, 1.06), M(Lever.KitchenCapacityFactor, 1.06), M(Lever.MoraleDelta, -3))));
            list.Add(productivity);

            var training = E("training_payoff", "Treinamento melhorou o atendimento",
                "O investimento em treinamento começou a aparecer no atendimento e nos pedidos sem erro.",
                EventCategory.Staff, EventImpact.Positive, 0.6d);
            training.requiresDecision = false;
            training.cooldownDays = 20;
            training.conditions.Add(new EventCondition(Indicator.Training, ">=", 1));
            training.baseEffect = Days(14, M(Lever.SatisfactionDelta, 4), M(Lever.ErrorRateFactor, 0.85));
            list.Add(training);

            // =============================== FORNECEDORES ===============================
            var delay = E("supplier_delay", "Atraso do fornecedor",
                "A entrega de insumos de hoje atrasou. Sem estoque suficiente, faltam produtos.",
                EventCategory.Supplier, EventImpact.Negative, 1.2d);
            delay.exclusionGroup = "SUPPLIER";
            delay.cooldownDays = 6;
            delay.defaultReactionId = "use_available_stock";
            delay.baseEffect = Days(1, M(Lever.DeliveryBlocked, 1));
            delay.reactions.Add(R("emergency_supplier", "Comprar de fornecedor emergencial", "A entrega chega hoje, com insumos 15% mais caros.", 0, Days(1, M(Lever.DeliveryBlocked, -1), M(Lever.IngredientCostFactor, 1.15))));
            delay.reactions.Add(R("reduce_menu", "Reduzir temporariamente o cardápio", "Menos itens hoje: demanda 10% menor, estoque dura mais.", 0, Days(1, M(Lever.DemandFactor, 0.9))));
            delay.reactions.Add(R("use_available_stock", "Trabalhar com o estoque disponível", "Vender só o que há em estoque.", 0, null));
            list.Add(delay);

            var price = E("ingredient_price_increase", "Aumento no preço dos ingredientes",
                "Os fornecedores reajustam os preços dos ingredientes em 8% até o fim do mês.",
                EventCategory.Market, EventImpact.Negative, 0.105d);
            price.exclusionGroup = "SUPPLIER";
            price.cooldownDays = 30;
            price.defaultReactionId = "absorb_increase";
            price.educationalConcept = "Margem de contribuição e repasse de custos";
            price.baseEffect = Effect(1, EffectEnd.MonthEnd, M(Lever.IngredientCostFactor, 1.08));
            price.reactions.Add(R("absorb_increase", "Absorver o aumento", "Manter preços; a margem cai.", 0, null));
            price.reactions.Add(R("partial_price_pass", "Repassar parte do aumento", "Preços 5% maiores até o fim do mês; um pouco menos de demanda.", 0, Effect(1, EffectEnd.MonthEnd, M(Lever.TicketFactor, 1.05), M(Lever.DemandFactor, 0.97))));
            price.reactions.Add(new EventReaction
            {
                id = "replace_ingredients", title = "Substituir ingredientes", description = "Insumos alternativos: o aumento cai para 3%, mas a qualidade percebida diminui.",
                effect = Effect(1, EffectEnd.MonthEnd, M(Lever.IngredientCostFactor, 0.954), M(Lever.QualityBonus, -0.04), M(Lever.ReputationInstant, -1))
            });
            list.Add(price);

            var discount = E("supplier_discount", "Desconto do fornecedor",
                "Um fornecedor oferece condições especiais de preço por uma semana.",
                EventCategory.Supplier, EventImpact.Positive, 0.3d);
            discount.exclusionGroup = "SUPPLIER";
            discount.cooldownDays = 20;
            discount.defaultReactionId = "normal_purchase";
            discount.reactions.Add(R("normal_purchase", "Fazer a compra normal", "Insumos 5% mais baratos por 7 dias.", 0, Days(7, M(Lever.IngredientCostFactor, 0.95))));
            discount.reactions.Add(R("larger_purchase", "Fazer uma compra maior", "Comprar 3 dias extras com 10% de desconto. Mais estoque também significa mais perdas se o produto for perecível.", 0,
                Days(3, M(Lever.IngredientCostFactor, 0.9), M(Lever.StockBoostDays, 3))));
            discount.reactions.Add(R("decline_offer", "Não aproveitar a oferta", "Manter as compras como estão.", 0, null));
            list.Add(discount);

            var bulk = E("bulk_purchase_opportunity", "Oportunidade de compra em volume",
                "Surge uma oportunidade de adquirir ingredientes em maior quantidade com desconto.",
                EventCategory.Supplier, EventImpact.Positive, 0.2d);
            bulk.exclusionGroup = "SUPPLIER";
            bulk.cooldownDays = 20;
            bulk.defaultReactionId = "decline_purchase";
            bulk.reactions.Add(R("small_batch", "Comprar um pequeno lote", "1 dia extra de estoque com 5% de desconto.", 0, Days(2, M(Lever.IngredientCostFactor, 0.95), M(Lever.StockBoostDays, 1))));
            bulk.reactions.Add(R("large_batch", "Comprar um lote grande", "4 dias extras com 12% de desconto; risco de perdas em perecíveis.", 0, Days(5, M(Lever.IngredientCostFactor, 0.88), M(Lever.StockBoostDays, 4))));
            bulk.reactions.Add(R("decline_purchase", "Não realizar a compra", "Manter o caixa livre.", 0, null));
            list.Add(bulk);

            // =============================== OPERACAO E ESTOQUE ===============================
            var storage = E("storage_loss", "Problemas de armazenamento",
                "Parte dos insumos estragou no armazenamento e precisou ser descartada.",
                EventCategory.Operations, EventImpact.Negative, 0.15d);
            storage.cooldownDays = 10;
            storage.defaultReactionId = "accept_loss";
            storage.modifiers.Add(P(Indicator.EquipmentBroken(EquipmentCategoryId.Refrigeration), 1.2d, 0d, "refrigeração parada"));
            storage.modifiers.Add(P(Indicator.StockCoverage, 0.25d, 4d, "estoque alto"));
            storage.modifiers.Add(P(Indicator.SupplierQuality, -3d, 0d, "qualidade do fornecedor"));
            storage.modifiers.Add(P(Indicator.RestaurantFlag("JAPONES"), 0.6d, 0d, "insumos muito perecíveis"));
            storage.baseEffect = Days(0, M(Lever.StockLossInstant, 0.12));
            storage.reactions.Add(R("review_storage", "Revisar o armazenamento", "Treinar a equipe e reorganizar a câmara: perdas menores por 3 semanas.", 400, Days(21, M(Lever.SpoilageFactor, 0.8)), CostLine.Maintenance));
            storage.reactions.Add(R("accept_loss", "Registrar a perda e seguir", "Sem custo adicional.", 0, null));
            list.Add(storage);

            var waste = E("excessive_food_waste", "Desperdício acima do esperado",
                "O desperdício da última semana passou de 7% dos insumos e está comprimindo a margem.",
                EventCategory.Operations, EventImpact.Negative, 3d);
            waste.cooldownDays = 10;
            waste.severity = 0.5d;
            waste.defaultReactionId = "keep_planning";
            waste.conditions.Add(new EventCondition(Indicator.WasteRatio, ">=", 0.07));
            waste.reactions.Add(R("adjust_planning", "Ajustar o planejamento", "Comprar para 1 dia a menos de estoque nas próximas 3 semanas.", 0, Days(21, M(Lever.StockCoverageDays, -1))));
            waste.reactions.Add(R("create_promotion", "Criar uma promoção", "Girar o estoque em 2 dias com desconto.", 0, Days(2, M(Lever.DemandFactor, 1.08), M(Lever.TicketFactor, 0.92), M(Lever.PricePerceptionFactor, 0.9))));
            waste.reactions.Add(R("keep_planning", "Manter o planejamento atual", "Seguir como está.", 0, null));
            list.Add(waste);

            // =============================== REGULATORIO ===============================
            var inspection = E("health_inspection", "Fiscalização sanitária",
                "O restaurante recebe uma fiscalização sanitária e são identificados pontos que exigem adequação.",
                EventCategory.Regulatory, EventImpact.Negative, 0.3d);
            inspection.cooldownDays = 20;
            inspection.severity = 1.2d;
            inspection.defaultReactionId = "minimum_compliance";
            inspection.modifiers.Add(P(Indicator.EquipmentBroken(EquipmentCategoryId.Refrigeration), 0.9d, 0d, "refrigeração parada"));
            inspection.modifiers.Add(P(Indicator.WasteRatio, 4d, 0.05d, "desperdício e armazenamento"));
            inspection.modifiers.Add(P(Indicator.Quality, -1d, 0.5d, "qualidade da operação"));
            inspection.reactions.Add(R("full_compliance", "Fazer todas as adequações imediatamente", "Investimento agora; menor chance de nova autuação por 60 dias.", 1500,
                Days(60, M(Lever.ReputationInstant, 2), M(Lever.SanitaryHazard, 0.7)), CostLine.Maintenance));
            inspection.reactions.Add(R("minimum_compliance", "Corrigir apenas pontos obrigatórios", "Custo menor, sem benefício adicional.", 600, null, CostLine.Maintenance));
            inspection.reactions.Add(R("ignore_requirements", "Não realizar adequações", "Multa de R$ 2.000, reputação menor e nova fiscalização provável.", 0,
                Days(30, M(Lever.CashInstant, -2000), M(Lever.ReputationInstant, -5), M(Lever.EventHazard("health_inspection"), 2.5)), CostLine.Fines));
            list.Add(inspection);

            // =============================== REPUTACAO ===============================
            var negativeReview = E("negative_review", "Avaliação negativa nas redes",
                "Um cliente publica uma avaliação negativa nas redes sociais, afetando a percepção do público.",
                EventCategory.Reputation, EventImpact.Negative, 0.25d);
            negativeReview.exclusionGroup = "SOCIAL_REPUTATION";
            negativeReview.cooldownDays = 12;
            negativeReview.defaultReactionId = "reply_only";
            negativeReview.modifiers.Add(P(Indicator.SatisfactionToday, -0.05d, 55d, "satisfação do dia"));
            negativeReview.modifiers.Add(P(Indicator.WaitRatio, 0.8d, 1d, "tempo de espera acima do padrão"));
            negativeReview.modifiers.Add(P(Indicator.Errors, 8d, 0.04d, "erros nos pedidos"));
            negativeReview.modifiers.Add(P(Indicator.LostShare, 2d, 0d, "clientes que não foram atendidos"));
            negativeReview.baseEffect = Days(0, M(Lever.ReputationInstant, -3));
            negativeReview.reactions.Add(R("compensate_customer", "Compensar o cliente", "Cortesia e resposta pública: recupera parte da reputação.", 300, Days(3, M(Lever.ReputationInstant, 2), M(Lever.DemandFactor, 0.98))));
            negativeReview.reactions.Add(R("reply_only", "Apenas responder", "Resposta educada; efeito pequeno na demanda por 3 dias.", 0, Days(3, M(Lever.DemandFactor, 0.95))));
            negativeReview.reactions.Add(R("ignore_review", "Ignorar a avaliação", "A crítica circula: demanda menor por 5 dias e reputação ainda mais baixa.", 0, Days(5, M(Lever.DemandFactor, 0.9), M(Lever.ReputationInstant, -2))));
            list.Add(negativeReview);

            var complaint = E("quality_complaint", "Reclamação sobre a qualidade da refeição",
                "Um cliente reclamou da qualidade do prato servido.",
                EventCategory.Reputation, EventImpact.Negative, 0.45d);
            complaint.cooldownDays = 7;
            complaint.defaultReactionId = "apologize";
            complaint.modifiers.Add(P(Indicator.Quality, -3d, 0.5d, "qualidade percebida"));
            complaint.modifiers.Add(P(Indicator.Errors, 10d, 0.04d, "erros nos pedidos"));
            complaint.modifiers.Add(P(Indicator.SupplierQuality, -4d, 0d, "qualidade dos insumos"));
            complaint.modifiers.Add(P(Indicator.Training, -0.3d, 0d, "treinamento"));
            complaint.baseEffect = Days(0, M(Lever.ReputationInstant, -1));
            complaint.reactions.Add(R("refund_and_review", "Reembolsar e revisar o preparo", "Devolve o valor e ajusta a ficha técnica: menos chance de avaliação negativa por 2 semanas.", 250,
                Days(14, M(Lever.ReputationInstant, 2), M(Lever.EventHazard("negative_review"), 0.7))));
            complaint.reactions.Add(R("apologize", "Pedir desculpas", "Sem custo; efeito pequeno.", 0, Days(1, M(Lever.ReputationInstant, 0.5))));
            complaint.reactions.Add(R("ignore", "Ignorar a reclamação", "A insatisfação aumenta a chance de avaliação negativa.", 0, Days(14, M(Lever.EventHazard("negative_review"), 1.8), M(Lever.SatisfactionDelta, -2))));
            list.Add(complaint);

            var positiveReview = E("positive_online_review", "Avaliação positiva nas redes",
                "Uma avaliação positiva ganha visibilidade nas redes sociais e aumenta o interesse pelo restaurante.",
                EventCategory.Reputation, EventImpact.Positive, 0.4d);
            positiveReview.exclusionGroup = "SOCIAL_REPUTATION";
            positiveReview.cooldownDays = 10;
            positiveReview.defaultReactionId = "organic_exposure";
            positiveReview.modifiers.Add(P(Indicator.SatisfactionToday, 0.07d, 55d, "satisfação do dia"));
            positiveReview.modifiers.Add(P(Indicator.Quality, 2d, 0.5d, "qualidade percebida"));
            positiveReview.baseEffect = Days(5, M(Lever.ReputationInstant, 2), M(Lever.DemandFactor, 1.08));
            positiveReview.reactions.Add(R("organic_exposure", "Aproveitar o alcance orgânico", "Sem custo.", 0, null));
            positiveReview.reactions.Add(R("boost_post", "Impulsionar a publicação", "Mais alcance por 5 dias.", 250, Days(5, M(Lever.DemandFactor, 1.06), M(Lever.ReputationInstant, 1)), CostLine.Marketing));
            positiveReview.reactions.Add(R("review_promotion", "Criar promoção vinculada à avaliação", "Mais clientes por 3 dias, com desconto.", 0, Days(3, M(Lever.DemandFactor, 1.1), M(Lever.TicketFactor, 0.94), M(Lever.PricePerceptionFactor, 0.92))));
            list.Add(positiveReview);

            var influencer = E("influencer_mention", "Menção de influenciador local",
                "Um influenciador local menciona espontaneamente o restaurante e amplia sua visibilidade.",
                EventCategory.Reputation, EventImpact.Positive, 0.15d);
            influencer.exclusionGroup = "SOCIAL_REPUTATION";
            influencer.cooldownDays = 20;
            influencer.defaultReactionId = "organic_reach";
            influencer.modifiers.Add(P(Indicator.MarketingAwareness, 1.2d, 0d, "marketing ativo"));
            influencer.modifiers.Add(P(Indicator.Quality, 2d, 0.5d, "qualidade percebida"));
            influencer.modifiers.Add(P(Indicator.Satisfaction, 0.03d, 55d, "clientes satisfeitos"));
            influencer.baseEffect = Days(4, M(Lever.DemandFactor, 1.15), M(Lever.ReputationInstant, 2));
            influencer.reactions.Add(R("organic_reach", "Aproveitar o alcance orgânico", "Sem custo.", 0, null));
            influencer.reactions.Add(R("boost_mention", "Impulsionar a menção", "Mais alcance por 4 dias.", 400, Days(4, M(Lever.DemandFactor, 1.1)), CostLine.Marketing));
            influencer.reactions.Add(R("new_customer_offer", "Criar oferta para novos clientes", "Mais clientes com desconto por 4 dias.", 0, Days(4, M(Lever.DemandFactor, 1.12), M(Lever.TicketFactor, 0.9), M(Lever.PricePerceptionFactor, 0.88))));
            list.Add(influencer);

            // =============================== MERCADO E DEMANDA ===============================
            var rain = E("heavy_rain", "Chuva forte",
                "Uma chuva intensa reduz o movimento presencial esperado no restaurante.",
                EventCategory.Market, EventImpact.Negative, 1.8d);
            rain.cooldownDays = 3;
            rain.severity = 0.3d;
            rain.requiresDecision = false;
            rain.defaultReactionId = "normal_operation";
            rain.reactions.Add(R("normal_operation", "Manter operação normal", "Movimento 15% menor hoje.", 0, Days(1, M(Lever.DemandFactor, 0.85))));
            rain.reactions.Add(R("external_orders", "Incentivar pedidos externos", "Divulgação de entrega reduz a queda para 5%.", 300, Days(1, M(Lever.DemandFactor, 0.95)), CostLine.Marketing));
            rain.reactions.Add(R("rain_promotion", "Criar promoção para o dia", "Desconto de 10% mantém o movimento.", 0, Days(1, M(Lever.DemandFactor, 0.97), M(Lever.TicketFactor, 0.9))));
            list.Add(rain);

            var drop = E("demand_drop", "Queda inesperada na demanda",
                "O movimento da região caiu nos últimos dias (obras na rua, feriado prolongado ou mudança de rotina dos clientes).",
                EventCategory.Market, EventImpact.Negative, 0.25d);
            drop.cooldownDays = 25;
            drop.defaultReactionId = "wait";
            drop.baseEffect = Days(7, M(Lever.DemandFactor, 0.85));
            drop.reactions.Add(R("promotion", "Fazer uma promoção", "Preço 8% menor e percepção de preço 15% menor por 7 dias.", 0, Days(7, M(Lever.TicketFactor, 0.92), M(Lever.PricePerceptionFactor, 0.85))));
            drop.reactions.Add(R("marketing_push", "Reforçar a divulgação", "Campanha pontual recupera parte do movimento.", 1200, Days(7, M(Lever.DemandFactor, 1.08)), CostLine.Marketing));
            drop.reactions.Add(R("wait", "Aguardar a normalização", "Sem custo; o movimento volta em uma semana.", 0, null));
            list.Add(drop);

            var competitor = E("new_competitor", "Novo concorrente na região",
                "Um novo restaurante concorrente inicia as atividades na mesma região e disputa o mesmo público.",
                EventCategory.Market, EventImpact.Negative, 0.15d);
            competitor.exclusionGroup = "COMPETITION";
            competitor.canRecur = false;
            competitor.severity = 2d;
            competitor.defaultReactionId = "keep_strategy";
            competitor.educationalConcept = "Barreiras de entrada e atratividade do mercado";
            competitor.modifiers.Add(P(Indicator.Competition, 1d, 1d, "região já disputada"));
            competitor.modifiers.Add(P(Indicator.LastMonthMargin, 2d, 0.15d, "região lucrativa atrai concorrentes"));
            competitor.modifiers.Add(P(Indicator.Month, 0.2d, 1d, "tempo de mercado"));
            competitor.baseEffect = Effect(1, EffectEnd.GameEnd, M(Lever.CompetitionDelta, 0.25));
            competitor.reactions.Add(R("keep_strategy", "Manter a estratégia atual", "Sem custo; a concorrência maior permanece.", 0, null));
            competitor.reactions.Add(R("marketing_campaign", "Investir em divulgação", "Campanha de R$ 1.500 sustenta a demanda por 30 dias.", 1500, Days(30, M(Lever.DemandFactor, 1.06)), CostLine.Marketing));
            competitor.reactions.Add(R("promotional_prices", "Adotar preços promocionais", "Preços 7% menores por 21 dias.", 0, Days(21, M(Lever.TicketFactor, 0.93), M(Lever.PricePerceptionFactor, 0.88))));
            competitor.reactions.Add(R("loyalty_focus", "Reforçar a fidelização", "Ações com clientes atuais por 30 dias.", 800, Days(30, M(Lever.LoyaltyStrength, 0.15)), CostLine.Marketing));
            list.Add(competitor);

            var energy = E("energy_tariff_increase", "Aumento da tarifa de energia",
                "A distribuidora reajustou a tarifa de energia em 15% até o fim da partida.",
                EventCategory.Market, EventImpact.Negative, 0.12d);
            energy.maxPerGame = 1;
            energy.severity = 0.8d;
            energy.defaultReactionId = "absorb";
            energy.baseEffect = Effect(1, EffectEnd.GameEnd, M(Lever.EnergyTariffFactor, 1.15));
            energy.reactions.Add(R("absorb", "Absorver o aumento", "Sem investimento.", 0, null));
            energy.reactions.Add(R("efficiency", "Investir em eficiência energética", "R$ 2.500 em vedação, iluminação LED e ajustes: consumo 10% menor.", 2500,
                Effect(1, EffectEnd.GameEnd, M(Lever.EnergyTariffFactor, 0.9)), CostLine.Maintenance));
            list.Add(energy);

            var lunch = E("lunch_demand_peak", "Pico de demanda no horário de almoço",
                "O restaurante recebe um volume de clientes acima do esperado durante o horário de almoço.",
                EventCategory.Market, EventImpact.Positive, 0.5d);
            lunch.exclusionGroup = "DEMAND_SPIKE";
            lunch.cooldownDays = 5;
            lunch.defaultReactionId = "current_capacity";
            lunch.locations.AddRange(new[] { "bank", "marketing", "store", "university" });
            lunch.baseEffect = Days(1, M(Lever.DemandFactor, 1.25));
            lunch.reactions.Add(R("current_capacity", "Atender com a capacidade atual", "Sem custo; parte dos clientes pode não ser atendida.", 0, null));
            lunch.reactions.Add(R("temporary_reinforcement", "Reforçar temporariamente a equipe", "Diárias extras: +15% de capacidade hoje.", 400, Days(1, M(Lever.FrontCapacityFactor, 1.15), M(Lever.KitchenCapacityFactor, 1.15)), CostLine.Staff));
            lunch.reactions.Add(R("high_margin_items", "Destacar itens de maior margem", "Ticket 8% maior, com um pouco menos de clientes.", 0, Days(1, M(Lever.DemandFactor, 0.92), M(Lever.TicketFactor, 1.08))));
            list.Add(lunch);

            var extra = E("extra_local_demand", "Aumento inesperado da demanda local",
                "Um aumento inesperado do fluxo de pessoas na região eleva a procura pelo restaurante.",
                EventCategory.Market, EventImpact.Positive, 0.35d);
            extra.exclusionGroup = "DEMAND_SPIKE";
            extra.cooldownDays = 7;
            extra.defaultReactionId = "normal_operation";
            extra.baseEffect = Days(1, M(Lever.DemandFactor, 1.2));
            extra.reactions.Add(R("normal_operation", "Manter operação normal", "Sem custo.", 0, null));
            extra.reactions.Add(R("reinforce_operation", "Reforçar a operação", "+15% de capacidade hoje.", 500, Days(1, M(Lever.FrontCapacityFactor, 1.15), M(Lever.KitchenCapacityFactor, 1.15)), CostLine.Staff));
            extra.reactions.Add(R("special_promotion", "Criar promoção especial", "Ainda mais clientes, com desconto de 5%.", 0, Days(1, M(Lever.DemandFactor, 1.08), M(Lever.TicketFactor, 0.95))));
            list.Add(extra);

            var festival = E("local_festival", "Festival na região",
                "Um festival próximo ao restaurante aumenta significativamente o fluxo de pessoas por dois dias.",
                EventCategory.Market, EventImpact.Positive, 0.3d);
            festival.exclusionGroup = "DEMAND_SPIKE";
            festival.cooldownDays = 25;
            festival.defaultReactionId = "normal_operation";
            festival.baseEffect = Days(2, M(Lever.DemandFactor, 1.3));
            festival.reactions.Add(R("normal_operation", "Manter operação normal", "Sem custo.", 0, null));
            festival.reactions.Add(R("reinforce_team", "Reforçar a equipe", "+20% de capacidade nos dois dias.", 800, Days(2, M(Lever.FrontCapacityFactor, 1.2), M(Lever.KitchenCapacityFactor, 1.2)), CostLine.Staff));
            festival.reactions.Add(R("festival_combo", "Criar combo especial", "Mais clientes com desconto de 5%.", 0, Days(2, M(Lever.DemandFactor, 1.05), M(Lever.TicketFactor, 0.95))));
            list.Add(festival);

            var corporate = E("corporate_order", "Pedido corporativo",
                "Uma empresa procura o restaurante para realizar um pedido de grande volume.",
                EventCategory.Market, EventImpact.Positive, 0.2d);
            corporate.exclusionGroup = "DEMAND_SPIKE";
            corporate.cooldownDays = 10;
            corporate.defaultReactionId = "accept_current_capacity";
            corporate.modifiers.Add(P(Indicator.LocationFlag("bank"), 0.9d, 0d, "região de escritórios"));
            corporate.modifiers.Add(P(Indicator.LocationFlag("marketing"), 0.9d, 0d, "região de escritórios"));
            corporate.baseEffect = Days(1, M(Lever.DemandFactor, 1.2));
            corporate.reactions.Add(R("accept_current_capacity", "Aceitar com a capacidade atual", "Sem custo; a operação fica apertada.", 0, Days(1, M(Lever.KitchenCapacityFactor, 0.95))));
            corporate.reactions.Add(R("temporary_support", "Contratar apoio temporário", "Mantém a capacidade normal.", 600, null, CostLine.Staff));
            corporate.reactions.Add(R("corporate_discount", "Oferecer desconto corporativo", "Fechar um volume maior com 8% de desconto.", 0, Days(1, M(Lever.DemandFactor, 1.08), M(Lever.TicketFactor, 0.92))));
            list.Add(corporate);

            var closure = E("competitor_temporary_closure", "Fechamento temporário de concorrente",
                "Um concorrente próximo fecha por alguns dias, aumentando a demanda potencial para o restaurante.",
                EventCategory.Market, EventImpact.Positive, 0.12d);
            closure.exclusionGroup = "COMPETITION";
            closure.cooldownDays = 30;
            closure.defaultReactionId = "serve_extra_demand";
            closure.modifiers.Add(P(Indicator.Competition, 1d, 1d, "região disputada"));
            closure.baseEffect = Days(5, M(Lever.DemandFactor, 1.15));
            closure.reactions.Add(R("serve_extra_demand", "Atender à demanda adicional", "Sem custo.", 0, null));
            closure.reactions.Add(R("increase_stock", "Reforçar o estoque", "Comprar 1 dia extra de insumos.", 0, Days(1, M(Lever.StockBoostDays, 1))));
            closure.reactions.Add(R("local_advertising", "Fazer divulgação local", "Conquistar parte desses clientes.", 400, Days(5, M(Lever.DemandFactor, 1.05), M(Lever.LoyaltyStrength, 0.05)), CostLine.Marketing));
            list.Add(closure);

            var growth = E("regional_growth", "Crescimento da região",
                "Novos empreendimentos aumentaram o movimento da região de forma duradoura.",
                EventCategory.Market, EventImpact.Positive, 0.08d);
            growth.maxPerGame = 1;
            growth.requiresDecision = false;
            growth.baseEffect = Effect(1, EffectEnd.GameEnd, M(Lever.RegionalFactor, 1.05));
            list.Add(growth);

            var menu = E("menu_change_new_audience", "Mudança no cardápio atraiu novo público",
                "A redução de preço de um prato chamou a atenção de clientes que antes não vinham.",
                EventCategory.Market, EventImpact.Positive, 1.5d);
            menu.requiresDecision = false;
            menu.cooldownDays = 30;
            menu.conditions.Add(new EventCondition(Indicator.MenuPriceDrop, ">=", 1));
            menu.baseEffect = Days(14, M(Lever.DemandFactor, 1.06));
            list.Add(menu);

            var loyaltySuccess = E("loyalty_program_success", "Programa de fidelização aumentou o retorno",
                "Clientes do programa de fidelidade estão voltando com mais frequência.",
                EventCategory.Reputation, EventImpact.Positive, 0.5d);
            loyaltySuccess.requiresDecision = false;
            loyaltySuccess.cooldownDays = 20;
            loyaltySuccess.conditions.Add(new EventCondition(Indicator.Loyalty, ">=", 1));
            loyaltySuccess.conditions.Add(new EventCondition(Indicator.Satisfaction, ">=", 52));
            loyaltySuccess.baseEffect = Days(14, M(Lever.DemandFactor, 1.06));
            list.Add(loyaltySuccess);

            var promoSuccess = E("promotion_success", "Promoção gerou clientes recorrentes",
                "Parte dos clientes que vieram na promoção voltou a comprar.",
                EventCategory.Market, EventImpact.Positive, 0.8d);
            promoSuccess.requiresDecision = false;
            promoSuccess.cooldownDays = 20;
            promoSuccess.conditions.Add(new EventCondition(Indicator.RecentPromotion, ">=", 1));
            promoSuccess.conditions.Add(new EventCondition(Indicator.Satisfaction, ">=", 50));
            promoSuccess.baseEffect = Days(14, M(Lever.DemandFactor, 1.04), M(Lever.LoyaltyStrength, 0.05));
            list.Add(promoSuccess);

            // =============================== FINANCEIRO ===============================
            var loan = E("loan_payment_difficulty", "Caixa insuficiente para a parcela",
                "Hoje vence a parcela do empréstimo e o caixa não cobre o valor.",
                EventCategory.Financial, EventImpact.Negative, 0d);
            loan.kind = EventKind.Deterministic;
            loan.cooldownDays = 25;
            loan.defaultReactionId = "pay_with_overdraft";
            loan.educationalConcept = "Fluxo de caixa x lucro; custo do capital de terceiros";
            loan.conditions.Add(new EventCondition(Indicator.LoanShortfall, ">", 0));
            loan.reactions.Add(R("pay_with_overdraft", "Pagar com cheque especial", "A parcela é paga; o saldo negativo cobra 8% ao mês.", 0, null));
            var renegotiate = new EventReaction
            {
                id = "renegotiate", title = "Renegociar com o banco", description = "Adiar metade do principal deste mês para os meses seguintes. Tarifa de 2% do saldo.",
                costAsLoanBalanceFraction = 0.02d, costLine = CostLine.Other,
                effect = Days(1, M(Lever.LoanPrincipalFactor, 0.5))
            };
            renegotiate.conditions.Add(new EventCondition(Indicator.MonthsRemaining, ">=", 1));
            loan.reactions.Add(renegotiate);
            list.Add(loan);

            return list;
        }
    }
}
