using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    public static class EventAssetGenerator
    {
        private const string AssetDirectory = "Assets/Resources/Events";

        private static readonly IReadOnlyDictionary<string, string> Descriptions =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["freezer_breakdown"] = "O freezer apresenta uma falha e parte dos ingredientes armazenados fica em risco.",
                ["insufficient_team"] = "Um funcionário fica ausente e o restaurante precisa decidir como manter o atendimento.",
                ["health_inspection"] = "O restaurante recebe uma fiscalização sanitária e são identificados pontos que exigem adequação.",
                ["negative_review"] = "Um cliente publica uma avaliação negativa nas redes sociais, afetando a percepção do público.",
                ["excessive_food_waste"] = "O restaurante identifica excesso de alimentos em estoque e risco de perdas por desperdício.",
                ["ingredient_price_increase"] = "Os fornecedores reajustam os preços dos ingredientes, elevando os custos do restaurante durante o mês.",
                ["heavy_rain"] = "Uma chuva intensa reduz o movimento presencial esperado no restaurante.",
                ["payment_system_failure"] = "O sistema de pagamento apresenta instabilidade e pode dificultar as vendas.",
                ["supplier_delay"] = "Uma entrega de insumos atrasa e compromete parte da operação planejada.",
                ["new_competitor"] = "Um novo restaurante concorrente inicia as atividades na mesma região.",
                ["extra_local_demand"] = "Um aumento inesperado do fluxo de pessoas na região eleva a procura pelo restaurante.",
                ["positive_online_review"] = "Uma avaliação positiva ganha visibilidade nas redes sociais e aumenta o interesse pelo restaurante.",
                ["supplier_discount"] = "Um fornecedor oferece condições especiais de preço para uma compra.",
                ["lunch_demand_peak"] = "O restaurante recebe um volume de clientes acima do esperado durante o horário de almoço.",
                ["local_festival"] = "Um festival próximo ao restaurante aumenta significativamente o fluxo de pessoas na região.",
                ["competitor_temporary_closure"] = "Um concorrente próximo fecha temporariamente, aumentando a demanda potencial para o restaurante.",
                ["team_productivity_boost"] = "A equipe apresenta um desempenho acima do esperado, permitindo melhorar o ritmo de atendimento.",
                ["bulk_purchase_opportunity"] = "Surge uma oportunidade de adquirir ingredientes em maior quantidade com desconto.",
                ["influencer_mention"] = "Um influenciador local menciona espontaneamente o restaurante e amplia sua visibilidade.",
                ["corporate_order"] = "Uma empresa procura o restaurante para realizar um pedido de grande volume."
            };

        [MenuItem("Tools/Jogo/Eventos/Validar definições")]
        public static void ValidateDefinitionsMenu()
        {
            var definitions = BuildDefinitions();

            if (!ValidateDefinitions(definitions, out var errors))
            {
                LogErrors("As definições de eventos são inválidas", errors);
                return;
            }

            Debug.Log("[EventAssetGenerator] Validação concluída: 20 eventos válidos, com 3 opções cada.");
        }

        [MenuItem("Tools/Jogo/Eventos/Gerar ou atualizar eventos")]
        public static void GenerateOrUpdate()
        {
            var definitions = BuildDefinitions();

            if (!ValidateDefinitions(definitions, out var definitionErrors))
            {
                LogErrors("Geração cancelada: as definições de eventos são inválidas", definitionErrors);
                return;
            }

            if (!ValidateExistingAssets(definitions, out var assetErrors))
            {
                LogErrors("Geração cancelada: foram encontrados conflitos nos assets", assetErrors);
                return;
            }

            EnsureFolder(AssetDirectory);

            int created = 0;
            int updated = 0;

            foreach (var definition in definitions)
            {
                string path = GetAssetPath(definition.id);
                var asset = AssetDatabase.LoadAssetAtPath<EventData>(path);

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<EventData>();
                    ApplyDefinition(asset, definition);
                    AssetDatabase.CreateAsset(asset, path);
                    created++;
                }
                else
                {
                    ApplyDefinition(asset, definition);
                    EditorUtility.SetDirty(asset);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EventAssetGenerator] Concluído: {created} asset(s) criado(s) e {updated} atualizado(s) em {AssetDirectory}.");
        }

        private static bool ValidateDefinitions(IReadOnlyList<EventDefinition> definitions, out List<string> errors)
        {
            errors = new List<string>();

            if (definitions.Count != 20)
                errors.Add($"Esperados exatamente 20 eventos, mas foram encontrados {definitions.Count}.");

            foreach (var duplicate in definitions.GroupBy(x => x.id).Where(x => x.Count() > 1))
                errors.Add($"ID de evento duplicado: '{duplicate.Key}'.");

            foreach (var definition in definitions)
            {
                if (string.IsNullOrWhiteSpace(definition.id))
                    errors.Add("Existe um evento sem ID.");
                else if (!IsSnakeCase(definition.id))
                    errors.Add($"O ID de evento '{definition.id}' deve estar em snake_case.");

                if (string.IsNullOrWhiteSpace(definition.description))
                    errors.Add($"O evento '{definition.id}' deve possuir description.");

                if (definition.options == null || definition.options.Length != 3)
                    errors.Add($"O evento '{definition.id}' deve possuir exatamente 3 opções.");
                else
                {
                    foreach (var duplicate in definition.options.GroupBy(x => x.id).Where(x => x.Count() > 1))
                        errors.Add($"O evento '{definition.id}' possui o ID de opção duplicado '{duplicate.Key}'.");

                    if (definition.options.Any(x => string.IsNullOrWhiteSpace(x.id)))
                        errors.Add($"O evento '{definition.id}' possui uma opção sem ID.");
                }

                if (definition.baseWeight <= 0f)
                    errors.Add($"O evento '{definition.id}' deve possuir baseWeight positivo.");

                if (definition.minRound < 1 || definition.maxRound < definition.minRound)
                    errors.Add($"O evento '{definition.id}' possui intervalo de rodadas inválido ({definition.minRound}-{definition.maxRound}).");

                int conditionCount = definition.conditions?.Length ?? 0;
                if (definition.triggerType == EventTriggerType.CONDITIONAL && conditionCount == 0)
                    errors.Add($"O evento CONDITIONAL '{definition.id}' deve possuir ao menos uma condição.");

                if (definition.triggerType == EventTriggerType.RANDOM && conditionCount != 0)
                    errors.Add($"O evento RANDOM '{definition.id}' não pode possuir condições.");

                if (definition.canRepeat)
                    errors.Add($"O evento '{definition.id}' deve possuir canRepeat = false.");
            }

            int positiveCount = definitions.Count(x => x.polarity == EventPolarity.POSITIVE);
            int negativeCount = definitions.Count(x => x.polarity == EventPolarity.NEGATIVE);
            int randomCount = definitions.Count(x => x.triggerType == EventTriggerType.RANDOM);
            int conditionalCount = definitions.Count(x => x.triggerType == EventTriggerType.CONDITIONAL);
            int conditionalNegativeCount = definitions.Count(x =>
                x.triggerType == EventTriggerType.CONDITIONAL && x.polarity == EventPolarity.NEGATIVE);

            if (positiveCount != 10)
                errors.Add($"Esperados 10 eventos POSITIVE, mas foram encontrados {positiveCount}.");
            if (negativeCount != 10)
                errors.Add($"Esperados 10 eventos NEGATIVE, mas foram encontrados {negativeCount}.");
            if (randomCount != 15)
                errors.Add($"Esperados 15 eventos RANDOM, mas foram encontrados {randomCount}.");
            if (conditionalCount != 5)
                errors.Add($"Esperados 5 eventos CONDITIONAL, mas foram encontrados {conditionalCount}.");
            if (conditionalNegativeCount != 5)
                errors.Add($"Todos os 5 eventos CONDITIONAL devem ser NEGATIVE; encontrados {conditionalNegativeCount}.");

            return errors.Count == 0;
        }

        private static bool ValidateExistingAssets(
            IReadOnlyList<EventDefinition> definitions,
            out List<string> errors)
        {
            errors = new List<string>();

            if (!AssetDatabase.IsValidFolder(AssetDirectory))
                return true;

            var assetsById = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:EventData", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<EventData>(path);
                if (asset == null)
                    continue;

                if (string.IsNullOrWhiteSpace(asset.id))
                {
                    errors.Add($"O asset '{path}' não possui ID.");
                    continue;
                }

                if (assetsById.TryGetValue(asset.id, out string existingPath))
                    errors.Add($"ID '{asset.id}' duplicado nos assets '{existingPath}' e '{path}'.");
                else
                    assetsById.Add(asset.id, path);
            }

            foreach (var definition in definitions)
            {
                string expectedPath = GetAssetPath(definition.id);
                UnityEngine.Object objectAtPath = AssetDatabase.LoadMainAssetAtPath(expectedPath);

                if (objectAtPath != null && objectAtPath is not EventData)
                {
                    errors.Add($"O caminho '{expectedPath}' já está ocupado por um asset que não é EventData.");
                    continue;
                }

                if (objectAtPath is EventData eventAtPath &&
                    !string.Equals(eventAtPath.id, definition.id, StringComparison.Ordinal))
                {
                    errors.Add($"O caminho '{expectedPath}' contém EventData com ID '{eventAtPath.id}'.");
                }

                if (assetsById.TryGetValue(definition.id, out string actualPath) &&
                    !string.Equals(actualPath, expectedPath, StringComparison.Ordinal))
                {
                    errors.Add($"O ID '{definition.id}' já existe em '{actualPath}', mas o caminho esperado é '{expectedPath}'.");
                }
            }

            return errors.Count == 0;
        }

        private static void ApplyDefinition(EventData asset, EventDefinition definition)
        {
            asset.id = definition.id;
            asset.title = definition.title;
            asset.description = definition.description;
            asset.educationalConcept = string.Empty;
            asset.polarity = definition.polarity;
            asset.triggerType = definition.triggerType;
            asset.baseWeight = definition.baseWeight;
            asset.canRepeat = false;
            asset.applicableRestaurantTypes = Array.Empty<RestaurantType>();
            asset.minRound = definition.minRound;
            asset.maxRound = definition.maxRound;
            asset.conditions = definition.conditions ?? Array.Empty<EventConditionData>();
            asset.options = definition.options;
        }

        private static string GetAssetPath(string id) => $"{AssetDirectory}/{id}.asset";

        private static bool IsSnakeCase(string value)
        {
            if (value.Length == 0 || value[0] == '_' || value[value.Length - 1] == '_')
                return false;

            bool previousWasUnderscore = false;
            foreach (char character in value)
            {
                bool isUnderscore = character == '_';
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= '0' && character <= '9') &&
                    !isUnderscore)
                    return false;

                if (isUnderscore && previousWasUnderscore)
                    return false;

                previousWasUnderscore = isUnderscore;
            }

            return true;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private static void LogErrors(string heading, IEnumerable<string> errors)
        {
            Debug.LogError($"[EventAssetGenerator] {heading}:\n- {string.Join("\n- ", errors)}");
        }

        private static EventDefinition Event(
            string id,
            string title,
            EventPolarity polarity,
            EventTriggerType triggerType,
            float baseWeight,
            EventConditionData[] conditions,
            params EventOption[] options)
        {
            return new EventDefinition
            {
                id = id,
                title = title,
                description = Descriptions[id],
                polarity = polarity,
                triggerType = triggerType,
                baseWeight = baseWeight,
                canRepeat = false,
                minRound = 1,
                maxRound = 3,
                conditions = conditions ?? Array.Empty<EventConditionData>(),
                options = options
            };
        }

        private static EventConditionData Condition(
            EventConditionType type,
            EventConditionOperator comparison,
            float value)
        {
            return new EventConditionData { type = type, comparison = comparison, value = value };
        }

        private static EventOption Option(string id, string title, string description, EventEffectData effects)
        {
            return new EventOption { id = id, title = title, description = description, effects = effects };
        }

        private static EventEffectData Effects(
            float cashDelta = 0f,
            float demandMultiplier = 1f,
            float capacityMultiplier = 1f,
            float stockMultiplier = 1f,
            float ingredientCostMultiplier = 1f,
            float averageTicketMultiplier = 1f,
            int reputationDelta = 0,
            EventEffectDuration duration = EventEffectDuration.IMMEDIATE)
        {
            return new EventEffectData
            {
                cashDelta = cashDelta,
                demandMultiplier = demandMultiplier,
                capacityMultiplier = capacityMultiplier,
                stockMultiplier = stockMultiplier,
                ingredientCostMultiplier = ingredientCostMultiplier,
                averageTicketMultiplier = averageTicketMultiplier,
                reputationDelta = reputationDelta,
                duration = duration
            };
        }

        private static EventDefinition[] BuildDefinitions()
        {
            return new[]
            {
                Event("freezer_breakdown", "Quebra do freezer", EventPolarity.NEGATIVE, EventTriggerType.CONDITIONAL, 1f,
                    new[] { Condition(EventConditionType.EQUIPMENT_QUALITY_SCORE, EventConditionOperator.LESS_OR_EQUAL, 1f) },
                    Option("repair_now", "Consertar imediatamente", "Realizar o conserto completo do freezer.", Effects(cashDelta: -3500f)),
                    Option("temporary_repair", "Fazer um reparo emergencial", "Realizar um reparo temporário para manter a operação.", Effects(cashDelta: -1500f, capacityMultiplier: 0.90f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("postpone_repair", "Adiar o reparo", "Adiar o conserto e assumir o risco de perda de estoque.", Effects(stockMultiplier: 0.85f))),

                Event("insufficient_team", "Funcionário ausente", EventPolarity.NEGATIVE, EventTriggerType.CONDITIONAL, 1f,
                    new[] { Condition(EventConditionType.TEAM_COVERAGE_RATIO, EventConditionOperator.LESS_OR_EQUAL, 1f) },
                    Option("hire_temporary", "Contratar substituto temporário", "Contratar apoio temporário para manter a capacidade operacional.", Effects(cashDelta: -350f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("redistribute_team", "Redistribuir as funções", "Redistribuir as tarefas entre os funcionários disponíveis.", Effects(capacityMultiplier: 0.90f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("operate_reduced", "Trabalhar com equipe reduzida", "Manter a operação com menos funcionários.", Effects(capacityMultiplier: 0.75f, reputationDelta: -2, duration: EventEffectDuration.CURRENT_DAY))),

                Event("health_inspection", "Fiscalização sanitária", EventPolarity.NEGATIVE, EventTriggerType.CONDITIONAL, 0.8f,
                    new[] { Condition(EventConditionType.SANITARY_RISK_SCORE, EventConditionOperator.GREATER_OR_EQUAL, 0.70f) },
                    Option("full_compliance", "Fazer todas as adequações imediatamente", "Corrigir integralmente os problemas identificados.", Effects(cashDelta: -1500f, reputationDelta: 2)),
                    Option("minimum_compliance", "Corrigir apenas pontos obrigatórios", "Realizar somente as adequações mínimas exigidas.", Effects(cashDelta: -600f)),
                    Option("ignore_requirements", "Não realizar adequações", "Ignorar as exigências da fiscalização.", Effects(cashDelta: -2000f, reputationDelta: -5))),

                Event("negative_review", "Avaliação negativa nas redes", EventPolarity.NEGATIVE, EventTriggerType.CONDITIONAL, 0.9f,
                    new[] { Condition(EventConditionType.REPUTATION_SCORE, EventConditionOperator.LESS_OR_EQUAL, 50f) },
                    Option("compensate_customer", "Compensar o cliente", "Oferecer uma compensação ao cliente e responder publicamente.", Effects(cashDelta: -300f, demandMultiplier: 0.97f, reputationDelta: 3, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("reply_only", "Apenas responder", "Responder à avaliação sem oferecer compensação.", Effects(demandMultiplier: 0.94f, reputationDelta: 1, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("ignore_review", "Ignorar a avaliação", "Não responder à crítica publicada.", Effects(demandMultiplier: 0.90f, reputationDelta: -4, duration: EventEffectDuration.CURRENT_DAY))),

                Event("excessive_food_waste", "Desperdício acima do esperado", EventPolarity.NEGATIVE, EventTriggerType.CONDITIONAL, 0.9f,
                    new[] { Condition(EventConditionType.STOCK_COVERAGE_RATIO, EventConditionOperator.GREATER_OR_EQUAL, 1.30f) },
                    Option("adjust_planning", "Ajustar o planejamento", "Reduzir as compras futuras para adequar o estoque.", Effects(stockMultiplier: 0.95f)),
                    Option("create_promotion", "Criar uma promoção", "Criar uma promoção para aumentar a saída dos produtos em estoque.", Effects(demandMultiplier: 1.10f, stockMultiplier: 0.98f, averageTicketMultiplier: 0.90f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("keep_planning", "Manter o planejamento atual", "Não alterar o planejamento de compras.", Effects(stockMultiplier: 0.85f))),

                Event("ingredient_price_increase", "Aumento no preço dos ingredientes", EventPolarity.NEGATIVE, EventTriggerType.RANDOM, 1f, null,
                    Option("absorb_increase", "Absorver o aumento", "Manter os preços atuais e assumir o aumento do custo.", Effects(ingredientCostMultiplier: 1.08f, duration: EventEffectDuration.CURRENT_MONTH)),
                    Option("partial_price_pass", "Repassar parte do aumento", "Aumentar parcialmente os preços para compensar os custos.", Effects(demandMultiplier: 0.97f, ingredientCostMultiplier: 1.08f, averageTicketMultiplier: 1.05f, duration: EventEffectDuration.CURRENT_MONTH)),
                    Option("replace_ingredients", "Substituir ingredientes", "Utilizar alternativas mais econômicas.", Effects(ingredientCostMultiplier: 1.03f, reputationDelta: -2, duration: EventEffectDuration.CURRENT_MONTH))),

                Event("heavy_rain", "Chuva forte", EventPolarity.NEGATIVE, EventTriggerType.RANDOM, 1.1f, null,
                    Option("normal_operation", "Manter operação normal", "Não realizar nenhuma alteração específica.", Effects(demandMultiplier: 0.85f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("external_orders", "Incentivar pedidos externos", "Investir em pedidos para entrega e retirada.", Effects(cashDelta: -300f, demandMultiplier: 0.95f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("rain_promotion", "Criar promoção para o dia", "Reduzir preços para compensar a queda de movimento.", Effects(averageTicketMultiplier: 0.90f, duration: EventEffectDuration.CURRENT_DAY))),

                Event("payment_system_failure", "Falha no sistema de pagamento", EventPolarity.NEGATIVE, EventTriggerType.RANDOM, 0.8f, null,
                    Option("emergency_support", "Acionar suporte emergencial", "Pagar por atendimento técnico imediato.", Effects(cashDelta: -500f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("alternative_payments", "Aceitar pagamentos alternativos", "Operar temporariamente com meios de pagamento limitados.", Effects(demandMultiplier: 0.92f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("wait_normalization", "Aguardar normalização", "Aguardar o retorno do sistema sem realizar intervenção.", Effects(demandMultiplier: 0.85f, duration: EventEffectDuration.CURRENT_DAY))),

                Event("supplier_delay", "Atraso do fornecedor", EventPolarity.NEGATIVE, EventTriggerType.RANDOM, 0.9f, null,
                    Option("emergency_supplier", "Comprar de fornecedor emergencial", "Comprar os itens necessários de outro fornecedor por um preço maior.", Effects(stockMultiplier: 1f, ingredientCostMultiplier: 1.15f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("reduce_menu", "Reduzir temporariamente o cardápio", "Suspender alguns itens até a normalização do abastecimento.", Effects(demandMultiplier: 0.90f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("use_available_stock", "Trabalhar com o estoque disponível", "Manter a operação limitada aos insumos já disponíveis.", Effects(capacityMultiplier: 0.85f, stockMultiplier: 0.90f, duration: EventEffectDuration.CURRENT_DAY))),

                Event("new_competitor", "Novo concorrente na região", EventPolarity.NEGATIVE, EventTriggerType.RANDOM, 0.7f, null,
                    Option("keep_strategy", "Manter a estratégia atual", "Não alterar a operação diante do novo concorrente.", Effects(demandMultiplier: 0.90f, duration: EventEffectDuration.CURRENT_MONTH)),
                    Option("marketing_campaign", "Investir em divulgação", "Realizar uma campanha para preservar o movimento.", Effects(cashDelta: -700f, demandMultiplier: 0.97f, duration: EventEffectDuration.CURRENT_MONTH)),
                    Option("promotional_prices", "Adotar preços promocionais", "Reduzir temporariamente os preços para competir pelo público.", Effects(averageTicketMultiplier: 0.90f, duration: EventEffectDuration.CURRENT_MONTH))),

                Event("extra_local_demand", "Aumento inesperado da demanda local", EventPolarity.POSITIVE, EventTriggerType.RANDOM, 1f, null,
                    Option("normal_operation", "Manter operação normal", "Atender ao aumento de movimento com a estrutura atual.", Effects(demandMultiplier: 1.20f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("reinforce_operation", "Reforçar a operação", "Investir em apoio temporário para aumentar a capacidade.", Effects(cashDelta: -500f, demandMultiplier: 1.20f, capacityMultiplier: 1.15f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("special_promotion", "Criar promoção especial", "Aproveitar o movimento para atrair ainda mais clientes.", Effects(demandMultiplier: 1.30f, averageTicketMultiplier: 0.95f, duration: EventEffectDuration.CURRENT_DAY))),

                Event("positive_online_review", "Avaliação positiva nas redes", EventPolarity.POSITIVE, EventTriggerType.RANDOM, 0.9f, null,
                    Option("organic_exposure", "Aproveitar o alcance orgânico", "Deixar a avaliação gerar divulgação naturalmente.", Effects(demandMultiplier: 1.10f, reputationDelta: 3, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("boost_post", "Impulsionar a publicação", "Investir para ampliar o alcance da avaliação.", Effects(cashDelta: -250f, demandMultiplier: 1.15f, reputationDelta: 4, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("review_promotion", "Criar promoção vinculada à avaliação", "Usar a repercussão positiva para promover uma oferta.", Effects(demandMultiplier: 1.20f, averageTicketMultiplier: 0.92f, reputationDelta: 3, duration: EventEffectDuration.CURRENT_DAY))),

                Event("supplier_discount", "Desconto do fornecedor", EventPolarity.POSITIVE, EventTriggerType.RANDOM, 0.9f, null,
                    Option("normal_purchase", "Fazer a compra normal", "Aproveitar o desconto apenas na compra já planejada.", Effects(ingredientCostMultiplier: 0.95f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("larger_purchase", "Fazer uma compra maior", "Aproveitar o desconto para ampliar o estoque.", Effects(stockMultiplier: 1.15f, ingredientCostMultiplier: 0.90f)),
                    Option("decline_offer", "Não aproveitar a oferta", "Manter o planejamento atual sem compra adicional.", Effects())),

                Event("lunch_demand_peak", "Pico de demanda no horário de almoço", EventPolarity.POSITIVE, EventTriggerType.RANDOM, 1.1f, null,
                    Option("current_capacity", "Atender com a capacidade atual", "Utilizar a estrutura atual para atender o aumento de clientes.", Effects(demandMultiplier: 1.25f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("temporary_reinforcement", "Reforçar temporariamente a equipe", "Investir em apoio para ampliar o atendimento.", Effects(cashDelta: -400f, demandMultiplier: 1.25f, capacityMultiplier: 1.15f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("high_margin_items", "Destacar itens de maior margem", "Direcionar a demanda para produtos de maior valor.", Effects(demandMultiplier: 1.15f, averageTicketMultiplier: 1.08f, duration: EventEffectDuration.CURRENT_DAY))),

                Event("local_festival", "Festival na região", EventPolarity.POSITIVE, EventTriggerType.RANDOM, 0.7f, null,
                    Option("normal_operation", "Manter operação normal", "Atender o movimento adicional com a estrutura atual.", Effects(demandMultiplier: 1.30f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("reinforce_team", "Reforçar a equipe", "Contratar apoio temporário para aproveitar o festival.", Effects(cashDelta: -800f, demandMultiplier: 1.30f, capacityMultiplier: 1.20f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("festival_combo", "Criar combo especial", "Oferecer uma promoção temática para aumentar o volume de vendas.", Effects(demandMultiplier: 1.35f, averageTicketMultiplier: 0.95f, duration: EventEffectDuration.CURRENT_DAY))),

                Event("competitor_temporary_closure", "Fechamento temporário de concorrente", EventPolarity.POSITIVE, EventTriggerType.RANDOM, 0.6f, null,
                    Option("serve_extra_demand", "Atender à demanda adicional", "Operar normalmente aproveitando o aumento do movimento.", Effects(demandMultiplier: 1.15f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("increase_stock", "Reforçar o estoque", "Preparar mais produtos para atender o aumento esperado.", Effects(demandMultiplier: 1.15f, stockMultiplier: 1.10f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("local_advertising", "Fazer divulgação local", "Investir para atrair parte dos clientes do concorrente.", Effects(cashDelta: -400f, demandMultiplier: 1.20f, duration: EventEffectDuration.CURRENT_DAY))),

                Event("team_productivity_boost", "Aumento de produtividade da equipe", EventPolarity.POSITIVE, EventTriggerType.RANDOM, 0.8f, null,
                    Option("maintain_pace", "Manter o ritmo", "Aproveitar naturalmente o ganho de produtividade.", Effects(capacityMultiplier: 1.10f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("team_bonus", "Oferecer bônus à equipe", "Recompensar os funcionários pelo desempenho.", Effects(cashDelta: -500f, capacityMultiplier: 1.10f, reputationDelta: 3, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("increase_service_volume", "Aumentar o volume de atendimento", "Aproveitar o desempenho da equipe para atender mais clientes.", Effects(capacityMultiplier: 1.15f, reputationDelta: -1, duration: EventEffectDuration.CURRENT_DAY))),

                Event("bulk_purchase_opportunity", "Oportunidade de compra em volume", EventPolarity.POSITIVE, EventTriggerType.RANDOM, 0.8f, null,
                    Option("small_batch", "Comprar um pequeno lote", "Aproveitar parcialmente a oportunidade.", Effects(stockMultiplier: 1.05f, ingredientCostMultiplier: 0.95f)),
                    Option("large_batch", "Comprar um lote grande", "Aproveitar ao máximo o desconto e ampliar significativamente o estoque.", Effects(stockMultiplier: 1.25f, ingredientCostMultiplier: 0.88f)),
                    Option("decline_purchase", "Não realizar a compra", "Manter o estoque e planejamento atuais.", Effects())),

                Event("influencer_mention", "Menção de influenciador local", EventPolarity.POSITIVE, EventTriggerType.RANDOM, 0.6f, null,
                    Option("organic_reach", "Aproveitar o alcance orgânico", "Deixar a divulgação ocorrer naturalmente.", Effects(demandMultiplier: 1.15f, reputationDelta: 3, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("boost_mention", "Impulsionar a menção", "Investir em divulgação para ampliar o alcance.", Effects(cashDelta: -400f, demandMultiplier: 1.25f, reputationDelta: 4, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("new_customer_offer", "Criar oferta para novos clientes", "Criar uma promoção para converter o aumento de visibilidade em visitas.", Effects(demandMultiplier: 1.30f, averageTicketMultiplier: 0.90f, reputationDelta: 3, duration: EventEffectDuration.CURRENT_DAY))),

                Event("corporate_order", "Pedido corporativo", EventPolarity.POSITIVE, EventTriggerType.RANDOM, 0.7f, null,
                    Option("accept_current_capacity", "Aceitar com a capacidade atual", "Aceitar o pedido utilizando a estrutura existente.", Effects(demandMultiplier: 1.20f, capacityMultiplier: 0.90f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("temporary_support", "Contratar apoio temporário", "Investir em suporte adicional para atender o pedido.", Effects(cashDelta: -600f, demandMultiplier: 1.20f, duration: EventEffectDuration.CURRENT_DAY)),
                    Option("corporate_discount", "Oferecer desconto corporativo", "Conceder desconto para ampliar o volume do pedido.", Effects(demandMultiplier: 1.30f, capacityMultiplier: 0.90f, averageTicketMultiplier: 0.92f, duration: EventEffectDuration.CURRENT_DAY)))
            };
        }

        private sealed class EventDefinition
        {
            public string id;
            public string title;
            public string description;
            public EventPolarity polarity;
            public EventTriggerType triggerType;
            public float baseWeight;
            public bool canRepeat;
            public int minRound;
            public int maxRound;
            public EventConditionData[] conditions;
            public EventOption[] options;
        }
    }
}
