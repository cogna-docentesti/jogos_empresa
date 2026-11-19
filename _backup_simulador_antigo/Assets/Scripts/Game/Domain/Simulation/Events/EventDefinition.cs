using System;
using System.Collections.Generic;

namespace Game.Domain.Simulation
{
    public static class EventCategory
    {
        public const string Equipment = "EQUIPMENT";
        public const string Staff = "STAFF";
        public const string Supplier = "SUPPLIER";
        public const string Market = "MARKET";
        public const string Regulatory = "REGULATORY";
        public const string Reputation = "REPUTATION";
        public const string Financial = "FINANCIAL";
        public const string Operations = "OPERATIONS";
    }

    public static class EventImpact
    {
        public const string Negative = "NEGATIVE";
        public const string Positive = "POSITIVE";
        public const string Neutral = "NEUTRAL";
    }

    public static class EventKind
    {
        /// <summary>Sorteado com probabilidade condicionada ao estado.</summary>
        public const string Probabilistic = "PROBABILISTIC";
        /// <summary>Consequencia certa quando as condicoes valem (ex.: parcela sem caixa). Nao usa sorteio.</summary>
        public const string Deterministic = "DETERMINISTIC";
    }

    public static class EventScope
    {
        /// <summary>Um sorteio por dia para o restaurante.</summary>
        public const string Global = "GLOBAL";
        /// <summary>Um sorteio por unidade de equipamento da categoria; o risco vem do modelo de confiabilidade.</summary>
        public const string EquipmentUnit = "EQUIPMENT_UNIT";
        /// <summary>Probabilidade combinada de todos os funcionarios: 1 - produto(1 - p_i).</summary>
        public const string Employee = "EMPLOYEE";
    }

    public static class EffectEnd
    {
        /// <summary>Termina depois de durationDays.</summary>
        public const string Days = "DAYS";
        /// <summary>Dura ate a unidade de equipamento ligada a ocorrencia voltar a funcionar.</summary>
        public const string UnitRepaired = "UNIT_REPAIRED";
        /// <summary>Dura ate o fim do mes corrente.</summary>
        public const string MonthEnd = "MONTH_END";
        /// <summary>Persistente ate o fim da partida.</summary>
        public const string GameEnd = "GAME_END";
    }

    public static class EquipmentAction
    {
        public const string None = "NONE";
        /// <summary>Conserto completo: unidade volta a funcionar.</summary>
        public const string Repair = "REPAIR";
        /// <summary>Reparo emergencial: volta a funcionar, mas com risco maior por um periodo.</summary>
        public const string TemporaryRepair = "TEMP_REPAIR";
        /// <summary>Troca por unidade nova do nivel indicado em replaceTier.</summary>
        public const string Replace = "REPLACE";
        /// <summary>Unidade continua parada; o aluno sera lembrado depois.</summary>
        public const string LeaveBroken = "LEAVE_BROKEN";
    }

    /// <summary>Condicao de elegibilidade: indicador, operador e valor. Todas as condicoes precisam valer (E).</summary>
    [Serializable]
    public class EventCondition
    {
        public string indicator;
        /// <summary>Um de: &gt;, &gt;=, &lt;, &lt;=, ==, !=</summary>
        public string op = ">=";
        public double value;

        public EventCondition() { }
        public EventCondition(string indicator, string op, double value)
        {
            this.indicator = indicator;
            this.op = op;
            this.value = value;
        }
    }

    /// <summary>
    /// Fator de risco: a taxa do evento e multiplicada por exp(coeficiente x (indicador - referencia)).
    /// exp(coeficiente) e o hazard ratio de uma unidade a mais do indicador: coeficiente 0,7 dobra o
    /// risco (e^0,7 = 2,0); coeficiente -0,7 reduz pela metade.
    /// </summary>
    [Serializable]
    public class ProbabilityModifier
    {
        public string indicator;
        public double coefficient;
        public double reference;
        /// <summary>Texto usado no feedback ("satisfacao baixa aumenta avaliacoes negativas").</summary>
        public string description;

        public ProbabilityModifier() { }
        public ProbabilityModifier(string indicator, double coefficient, double reference, string description)
        {
            this.indicator = indicator;
            this.coefficient = coefficient;
            this.reference = reference;
            this.description = description;
        }
    }

    [Serializable]
    public class EffectSpec
    {
        public List<Modifier> modifiers = new List<Modifier>();
        public int durationDays = 1;
        public string end = EffectEnd.Days;
        /// <summary>Linha da DRE do caixa imediato (cash.instant): MAINTENANCE, FINES, OTHER, OTHER_INCOME...</summary>
        public string costLine = CostLine.Other;
        /// <summary>Penaliza a satisfacao enquanto ativo, como "evento negativo nao resolvido" (Bloco 8).</summary>
        public bool unresolved;

        public static EffectSpec Of(int days, params Modifier[] modifiers)
        {
            var spec = new EffectSpec { durationDays = days };
            spec.modifiers.AddRange(modifiers);
            return spec;
        }
    }

    [Serializable]
    public class EventReaction
    {
        public string id;
        public string title;
        public string description;
        /// <summary>Desembolso imediato da reacao (R$). Valor positivo = saida de caixa.</summary>
        public double cost;
        /// <summary>Custo como fracao do saldo do emprestimo (renegociacao).</summary>
        public double costAsLoanBalanceFraction;
        /// <summary>Custo como multiplo do conserto padrao do equipamento afetado (nivel x preco de compra).</summary>
        public double costAsEquipmentPriceFraction;
        /// <summary>Se verdadeiro, pode ser paga com cheque especial quando o caixa nao basta.</summary>
        public bool financeable = true;
        /// <summary>Cobra recrutamento (StaffParameters.recruitmentCostInSalaries x salario do cargo envolvido).</summary>
        public bool chargesRecruitment;
        public string costLine = CostLine.Other;
        public string equipmentAction = EquipmentAction.None;
        public string replaceTier;
        /// <summary>Condicoes para a reacao aparecer habilitada (ex.: ter refrigeracao reserva).</summary>
        public List<EventCondition> conditions = new List<EventCondition>();
        public EffectSpec effect = new EffectSpec();
        /// <summary>Frase do feedback depois da escolha.</summary>
        public string outcome;
    }

    [Serializable]
    public class EventDefinition
    {
        public string id;
        public string title;
        public string description;
        public string educationalConcept;
        public string category = EventCategory.Operations;
        public string impact = EventImpact.Negative;
        public string kind = EventKind.Probabilistic;
        public string scope = EventScope.Global;
        /// <summary>Categoria de equipamento quando scope = EQUIPMENT_UNIT.</summary>
        public string equipmentCategory;
        /// <summary>
        /// Ocorrencias esperadas por 30 dias nas condicoes de referencia (todos os indicadores na referencia).
        /// Para EQUIPMENT_UNIT a taxa vem do modelo de confiabilidade e este valor multiplica (1 = neutro).
        /// </summary>
        public double baseRatePerMonth = 1d;
        public List<ProbabilityModifier> modifiers = new List<ProbabilityModifier>();
        public List<EventCondition> conditions = new List<EventCondition>();
        public List<string> restaurantTypes = new List<string>();
        public List<string> locations = new List<string>();
        public int minMonth = 1;
        public int maxMonth = 99;
        public int minDayOfMonth = 1;
        public bool canRecur = true;
        /// <summary>Intervalo minimo entre ocorrencias do mesmo evento (dias corridos de jogo).</summary>
        public int cooldownDays = 10;
        /// <summary>0 = sem limite por partida.</summary>
        public int maxPerGame;
        /// <summary>Eventos do mesmo grupo nao ficam ativos ao mesmo tempo (mutuamente exclusivos).</summary>
        public string exclusionGroup;
        /// <summary>Peso do evento na protecao contra sequencias punitivas.</summary>
        public double severity = 1d;
        public bool requiresDecision = true;
        public string defaultReactionId;
        /// <summary>Consequencia automatica da ocorrencia, antes (e independente) da reacao.</summary>
        public EffectSpec baseEffect = new EffectSpec { durationDays = 0 };
        public List<EventReaction> reactions = new List<EventReaction>();

        public EventReaction Reaction(string reactionId) => reactions.Find(r => r.id == reactionId);
        public bool IsNegative => impact == EventImpact.Negative;
    }
}
