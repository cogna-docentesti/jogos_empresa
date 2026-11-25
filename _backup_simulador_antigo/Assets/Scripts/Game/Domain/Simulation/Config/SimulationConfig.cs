using System;
using System.Collections.Generic;

namespace Game.Domain.Simulation
{
    // =====================================================================
    //  CONFIGURACAO DO SIMULADOR
    //
    //  Todas as classes desta pasta usam [Serializable] e campos publicos,
    //  para que a Unity mostre e salve os valores dentro do ScriptableObject
    //  SimulationConfigAsset (Inspector) e para que o JsonUtility exporte e
    //  importe o mesmo conteudo. Nao ha dicionarios: a Unity nao serializa
    //  dicionarios, entao tudo e lista.
    //
    //  Cada parametro traz a premissa adotada. Os valores padrao estao em
    //  DefaultSimulationConfig e podem ser alterados pelo professor.
    // =====================================================================

    [Serializable]
    public class SimulationConfig
    {
        public int schemaVersion = 1;
        public string versionLabel = "padrao-2026-10";

        /// <summary>0 = semente derivada do id da sessao. Outro valor fixa a semente (testes e aula).</summary>
        public long seedOverride;

        public CalendarParameters calendar = new CalendarParameters();
        public MarketParameters market = new MarketParameters();
        public StaffParameters staff = new StaffParameters();
        public StockParameters stock = new StockParameters();
        public ReliabilityParameters reliability = new ReliabilityParameters();
        public ExperienceParameters experience = new ExperienceParameters();
        public FinanceParameters finance = new FinanceParameters();
        public EventEngineParameters eventEngine = new EventEngineParameters();
        public ScoringParameters scoring = new ScoringParameters();

        public List<SegmentProfile> segments = new List<SegmentProfile>();
        public List<LocationMarketProfile> locations = new List<LocationMarketProfile>();
        public List<RestaurantProfile> restaurants = new List<RestaurantProfile>();
        public List<RoleProfile> roles = new List<RoleProfile>();
        public List<EquipmentCategoryProfile> equipmentCategories = new List<EquipmentCategoryProfile>();
        public List<EquipmentTierProfile> equipmentTiers = new List<EquipmentTierProfile>();
        public List<EquipmentProfile> equipment = new List<EquipmentProfile>();
        public List<PolicyCategory> policies = new List<PolicyCategory>();
        public List<EventDefinition> events = new List<EventDefinition>();

        /// <summary>Copia rasa com calendario proprio (usada pela previsao, que estende o horizonte).</summary>
        public SimulationConfig WithCalendar(CalendarParameters calendar)
        {
            var copy = (SimulationConfig)MemberwiseClone();
            copy.calendar = calendar;
            return copy;
        }

        public SegmentProfile Segment(string id) => segments.Find(s => s.id == id);
        public LocationMarketProfile Location(string id) => locations.Find(l => l.locationId == id);
        public RestaurantProfile Restaurant(string id) => restaurants.Find(r => r.typeId == id);
        public RoleProfile Role(string id) => roles.Find(r => r.roleId == id);
        public EquipmentCategoryProfile EquipmentCategory(string id) => equipmentCategories.Find(c => c.id == id);
        public EquipmentTierProfile Tier(string id) => equipmentTiers.Find(t => t.id == id);
        public EquipmentProfile EquipmentProfileFor(string id) => equipment.Find(e => e.equipmentId == id);
        public PolicyCategory Policy(string id) => policies.Find(p => p.id == id);
        public EventDefinition Event(string id) => events.Find(e => e.id == id);
    }

    [Serializable]
    public class CalendarParameters
    {
        /// <summary>Dias de operacao por mes. O projeto ja usa 30 (MonthlySimulationSettings).</summary>
        public int operatingDays = 30;
        /// <summary>Meses da partida. O ciclo atual e trimestral.</summary>
        public int months = 3;
        /// <summary>Dia da semana do dia 1 do mes 1 (0 = segunda-feira, como no mockup do calendario).</summary>
        public int firstWeekday;
        /// <summary>Mes do calendario do mes 1 da partida (1 = janeiro). Usado so nos textos ("Mes 3 - Marco").</summary>
        public int firstCalendarMonth = 1;
        /// <summary>Sazonalidade por mes do ciclo (1 = neutro). Lista com um valor por mes.</summary>
        public List<double> monthlySeasonality = new List<double> { 1d, 1d, 1d };
    }

    [Serializable]
    public class MarketParameters
    {
        /// <summary>
        /// Inclinacao da curva de aceitacao de preco. Aceitacao de um prato pelo segmento s:
        /// a = 1 / (1 + (preco_percebido / gasto_de_referencia_s)^k). Com k = 3, um prato com
        /// preco igual ao gasto de referencia atrai metade do publico do segmento; com o dobro
        /// do preco, cerca de 11%.
        /// </summary>
        public double priceSteepness = 3d;
        /// <summary>
        /// Quanto a qualidade percebida "justifica" o preco. preco_percebido = preco / (1 + theta x (Q - 0,5)).
        /// Com theta = 0,6 e qualidade maxima (Q = 1), o preco parece 23% menor.
        /// </summary>
        public double qualityPriceEffect = 0.6d;
        /// <summary>Bonus de atratividade no segmento que o aluno escolheu como publico-alvo (posicionamento).</summary>
        public double targetFocusBonus = 0.15d;
        /// <summary>Perda de atratividade nos segmentos fora do publico-alvo (comunicacao menos dirigida).</summary>
        public double offTargetPenalty = 0.05d;
        /// <summary>
        /// Normalizador do encaixe de mercado. Encaixe = soma ponderada / referencia. Com 0,45 um
        /// restaurante de lanches em area educacional, com precos medios, fica perto de 1,2 e um
        /// frances na mesma area fica perto de 0,25.
        /// </summary>
        public double fitReference = 0.45d;
        /// <summary>Tamanho do mercado da cidade alcancavel por marketing, em multiplos da demanda base local.</summary>
        public double cityFlowMultiplier = 2d;
        /// <summary>Elasticidade da competicao: fator = (atratividade / pressao_competitiva)^gamma.</summary>
        public double competitionElasticity = 0.5d;
        /// <summary>Efeito da qualidade na atratividade frente aos concorrentes: A = 1 + beta x (Q - 0,5).</summary>
        public double qualityAttractiveness = 0.6d;
        /// <summary>Reputacao 0 -> fator minimo; 100 -> fator maximo; 50 -> 1.</summary>
        public double reputationDemandMin = 0.6d;
        public double reputationDemandMax = 1.4d;
        /// <summary>Forca basal da fidelizacao: fator = 1 + forca x (satisfacao - 50) / 100.</summary>
        public double loyaltyBaseStrength = 0.3d;
        /// <summary>Ruido diario lognormal (desvio do log). Representa a variacao aleatoria do movimento.</summary>
        public double demandNoiseSigma = 0.12d;
        /// <summary>Retencao diaria da consciencia de marca (adstock). 0,85 = meia-vida de cerca de 4 dias.</summary>
        public double awarenessRetention = 0.85d;
        /// <summary>Pressao competitiva por nivel de concorrencia do LocationData (LOW, MEDIUM, HIGH).</summary>
        public double competitionLow = 0.8d;
        public double competitionMedium = 1d;
        public double competitionHigh = 1.25d;
    }

    [Serializable]
    public class StaffParameters
    {
        /// <summary>Encargos sobre a folha. Especificacao (Bloco 6): folha = salarios x 1,35. Use 1 se o salario do asset ja incluir encargos.</summary>
        public double payrollChargesMultiplier = 1.35d;
        public double baseMorale = 65d;
        /// <summary>Velocidade diaria com que a moral se aproxima do alvo (0..1).</summary>
        public double moraleAdjustRate = 0.12d;
        /// <summary>Pontos de moral perdidos por 100% de sobrecarga acima da capacidade.</summary>
        public double overloadMoralePenalty = 20d;
        /// <summary>Produtividade com moral 0 e com moral 100 (interpolacao linear).</summary>
        public double productivityAtZeroMorale = 0.85d;
        public double productivityAtFullMorale = 1.15d;
        /// <summary>Ganho de produtividade por mes de experiencia acumulada, ate o limite.</summary>
        public double experienceGainPerMonth = 0.02d;
        public double experienceGainCap = 0.06d;
        /// <summary>Probabilidade diaria de falta por funcionario em moral neutra. Com 3 pessoas da ~5% ao dia (Bloco 7).</summary>
        public double absencePerEmployeePerDay = 0.017d;
        /// <summary>Efeito da moral na falta: HR = exp(beta x (0,65 - moral/100)).</summary>
        public double moraleAbsenceBeta = 2d;
        /// <summary>Probabilidade mensal de pedido de demissao por funcionario em moral neutra.</summary>
        public double turnoverPerEmployeePerMonth = 0.03d;
        public double moraleTurnoverBeta = 4d;
        /// <summary>Custo de recrutamento de um substituto, em salarios do cargo.</summary>
        public double recruitmentCostInSalaries = 0.5d;
        /// <summary>Taxa basal de erros em pedidos (fracao), antes de treinamento e sobrecarga.</summary>
        public double baseErrorRate = 0.04d;
        public double overloadErrorSlope = 0.12d;
        /// <summary>Capacidade perdida quando falta uma pessoa e ninguem cobre (fracao da capacidade dela).</summary>
        public double absenceCapacityLoss = 1d;
    }

    [Serializable]
    public class StockParameters
    {
        /// <summary>Multiplicador da deterioracao quando todos os equipamentos de refrigeracao estao parados.</summary>
        public double brokenRefrigerationSpoilage = 4d;
        /// <summary>Estoque maximo, em multiplos da cobertura-alvo, para evitar compras infinitas por evento.</summary>
        public double maxCoverageMultiple = 3d;
        /// <summary>Janela (dias) usada para medir o indice de desperdicio.</summary>
        public int wasteWindowDays = 7;
    }

    [Serializable]
    public class ReliabilityParameters
    {
        /// <summary>Hazard ratio por envelhecimento: HR = exp(beta x idade / vida_util). Fim da vida util = 4,5x.</summary>
        public double ageBeta = 1.5d;
        /// <summary>Hazard ratio por uso intenso: HR = exp(beta x max(0, utilizacao - limiar)).</summary>
        public double usageBeta = 1.2d;
        public double usageThreshold = 0.8d;
        /// <summary>
        /// Teto da intensidade de uso no calculo do risco. A intensidade e demanda / capacidade; a fila
        /// de quem nao foi atendido nao faz o equipamento trabalhar alem do limite fisico. Com 1,25 o
        /// risco por uso chega no maximo a e^(1,2 x 0,45) = 1,7x. 0 = sem teto.
        /// </summary>
        public double usageCap = 1.25d;
        /// <summary>Cada falha nos ultimos N dias multiplica o risco da unidade por este valor.</summary>
        public double recentFailureHazard = 1.3d;
        public int recentFailureWindowDays = 60;
        /// <summary>Acrescimo de risco por mes sem manutencao preventiva (limitado).</summary>
        public double maintenanceGapPerMonth = 0.1d;
        public double maintenanceGapCap = 0.5d;
        /// <summary>Limite inferior do hazard ratio combinado: nenhum equipamento fica imune.</summary>
        public double minimumHazardRatio = 0.15d;
        /// <summary>Dias entre lembretes quando o aluno adia um conserto.</summary>
        public int brokenReminderDays = 10;
    }

    [Serializable]
    public class ExperienceParameters
    {
        public double baseSatisfaction = 50d;
        public double waitWithinStandardBonus = 10d;
        public double waitOverStandardMaxPenalty = 15d;
        public double noStockoutBonus = 5d;
        public double stockoutPenalty = 15d;
        public double fairPriceBonus = 5d;
        public double highPriceWithoutQualityPenalty = 10d;
        /// <summary>Posicao do preco na faixa do produto (0 = minimo, 1 = maximo) a partir da qual e "preco alto".</summary>
        public double highPricePosition = 0.6d;
        public double qualityThresholdForHighPrice = 0.6d;
        public double unresolvedNegativeEventPenalty = 10d;
        public double premiumServiceBonus = 5d;
        /// <summary>Pontos por unidade de qualidade acima/abaixo de 0,5: (Q - 0,5) x escala.</summary>
        public double qualitySatisfactionScale = 20d;
        public double errorPenaltyScale = 120d;
        /// <summary>Suavizacao exponencial da satisfacao acumulada (peso do dia).</summary>
        public double satisfactionSmoothing = 0.25d;
        /// <summary>Velocidade diaria com que a reputacao segue a satisfacao.</summary>
        public double reputationAdjustRate = 0.035d;
        /// <summary>Reputacao inicial de uma sessao nova (o projeto usa 50).</summary>
        public int initialReputation = 50;
        public double initialSatisfaction = 60d;
    }

    [Serializable]
    public class FinanceParameters
    {
        /// <summary>Aliquota didatica sobre a receita bruta (Bloco 3 e 5 da especificacao).</summary>
        public double salesTaxRate = 0.08d;
        /// <summary>
        /// SAME_MONTH: contas do mes vencem dentro do proprio mes, nos dias abaixo (calendario do mockup).
        /// NEXT_MONTH_DAY5: regra da especificacao, contas do mes M vencem no dia 5 do mes M+1.
        /// </summary>
        public string billTiming = BillTiming.SameMonth;
        public int marketingDueDay = 1;
        public int rentDueDay = 5;
        public int energyDueDay = 8;
        public int maintenanceDueDay = 15;
        public int loanDueDay = 20;
        public int waterDueDay = 22;
        public int payrollDueDay = 27;
        public int taxesDueDay = 29;
        public int nextMonthDueDay = 5;

        // Contas de consumo (Bloco 6.3). A parte fixa de energia vem do consumo dos equipamentos.
        public double energyBaseFixed = 600d;
        public double energyTariffPerKwh = 0.95d;
        public double energyPerMeal = 1.2d;
        public double waterFixed = 600d;
        public double waterPerMeal = 0.6d;
        public double cleaningFixed = 500d;
        public double cleaningPerMeal = 0.4d;
        public double adminFeesMonthly = 300d;

        /// <summary>ECONOMIC (padrao) ou LEGACY_CASH_RESULT. Ver BankruptcyRule.</summary>
        public string bankruptcyRule = BankruptcyRule.Economic;
        public int bankruptcyConsecutiveMonths = 3;

        /// <summary>Lucro mensal desejado, como fracao do capital inicial, para o ponto de equilibrio economico.</summary>
        public double desiredMonthlyReturnOnCapital = 0.05d;

        /// <summary>Limite do cheque especial que cobre caixa negativo.</summary>
        public double overdraftLimit = 15000d;
        /// <summary>Juros mensais sobre o saldo negativo (cobrados por dia proporcional).</summary>
        public double overdraftMonthlyRate = 0.08d;

        // Bonificacao diaria por meta (Bloco 8). Desligada por padrao: ver GDD, secao de divergencias.
        public bool dailyGoalBonusEnabled;
        public double dailyGoalCapacityShare = 0.8d;
        public double dailyGoalProgressionPerDay = 2d;
        public double bonusGoalReached = 300d;
        public double bonusGoalAndNps = 500d;
        public double bonusGoalNpsNoLost = 700d;
        public double bonusNpsThreshold = 70d;
    }

    public static class BillTiming
    {
        public const string SameMonth = "SAME_MONTH";
        public const string NextMonthDay5 = "NEXT_MONTH_DAY5";
    }

    [Serializable]
    public class EventEngineParameters
    {
        /// <summary>No maximo N eventos que pedem decisao por dia (os outros sorteados no mesmo dia sao descartados).</summary>
        public int maxDecisionEventsPerDay = 1;
        public int maxEventsPerDay = 2;
        /// <summary>Janela da protecao contra sequencias punitivas.</summary>
        public int mercyWindowDays = 10;
        /// <summary>Amortecimento: fator = exp(-forca x severidade_recente), nunca abaixo do minimo.</summary>
        public double mercyStrength = 0.35d;
        public double mercyMinimumFactor = 0.35d;
        /// <summary>Escalas globais para calibrar a frequencia de eventos sem editar cada um.</summary>
        public double negativeRateScale = 1d;
        public double positiveRateScale = 1d;
        /// <summary>Dias iniciais da partida sem eventos negativos aleatorios (abertura).</summary>
        public int openingGraceDays = 2;
        /// <summary>
        /// Eventos legados (EventData sem definicao propria no SimulationConfig): ocorrencias esperadas
        /// por mes para cada unidade de baseWeight. O motor antigo sorteava um evento por mes entre os
        /// elegiveis; 0,3 mantem a frequencia proxima disso quando ha 3 ou 4 eventos elegiveis.
        /// </summary>
        public double legacyRatePerWeight = 0.3d;
        /// <summary>Dias de demanda comprados a mais quando um evento legado tem stockMultiplier &gt; 1.</summary>
        public double legacyStockBoostDays = 3d;
    }

    [Serializable]
    public class ScoringParameters
    {
        public int netProfitPositive = 20;
        public int cashFlowPositive = 20;
        public int npsAbove70 = 15;
        public int cmvWithinStandard = 15;
        public int currentLiquidityAbove12 = 10;
        public int lostOrdersBelow5Percent = 10;
        public int noOverdueBills = 10;
        public int netLoss = -20;
        public int negativeCash = -25;
        public int npsBelow30 = -15;
    }

    // ------------------------------------------------------------------
    //  Perfis de mercado e operacao
    // ------------------------------------------------------------------

    [Serializable]
    public class SegmentShare
    {
        public string segmentId;
        public double value;

        public SegmentShare() { }
        public SegmentShare(string segmentId, double value) { this.segmentId = segmentId; this.value = value; }
    }

    [Serializable]
    public class SegmentProfile
    {
        /// <summary>Mesmo nome do enum Segment do projeto: LOW, MEDIUM, HIGH.</summary>
        public string id;
        public string displayName;
        /// <summary>Gasto de referencia por refeicao do segmento (R$). Base da curva de aceitacao de preco.</summary>
        public double referenceSpend;
        /// <summary>Participacao do segmento na cidade (CityData do plano de arquitetura: 35/53/12).</summary>
        public double cityShare;
        /// <summary>Multiplica a inclinacao da curva de preco: acima de 1 = mais sensivel a preco.</summary>
        public double priceSensitivity = 1d;
    }

    [Serializable]
    public class LocationMarketProfile
    {
        /// <summary>id do LocationData (bank, university, store, condominium, marketing).</summary>
        public string locationId;
        /// <summary>Composicao do fluxo de pessoas da area por segmento (inclui quem trabalha ali).</summary>
        public List<SegmentShare> segmentMix = new List<SegmentShare>();
        /// <summary>Perfil por dia da semana, segunda a domingo. Normalizado para media 1 na leitura.</summary>
        public List<double> weekdayProfile = new List<double> { 1, 1, 1, 1, 1, 1, 1 };
        /// <summary>0 = usar o competitionLevel do LocationData.</summary>
        public double competitionOverride;
    }

    [Serializable]
    public class RestaurantProfile
    {
        /// <summary>Mesmo nome do enum RestaurantType: PODRAO, JAPONES, FRANCES.</summary>
        public string typeId;
        /// <summary>Preferencia de cada segmento pela categoria, sem considerar preco (0..1).</summary>
        public List<SegmentShare> appeal = new List<SegmentShare>();
        /// <summary>Fracao do estoque perdida por dia (perecibilidade). Japones e o mais sensivel (Bloco 1).</summary>
        public double spoilagePerDay = 0.02d;
        /// <summary>Tempo medio de atendimento sem fila (minutos).</summary>
        public double baseServiceMinutes = 12d;
        /// <summary>Padrao de atendimento esperado pelo publico (minutos).</summary>
        public double serviceStandardMinutes = 18d;
        /// <summary>Faixa de CMV considerada padrao (fracao da receita liquida).</summary>
        public double cmvStandardMin = 0.28d;
        public double cmvStandardMax = 0.38d;
        /// <summary>Proposta premium: salao premium ou gerente/maitre somam satisfacao (Bloco 8).</summary>
        public bool premium;
    }

    public static class RoleFunction
    {
        public const string Front = "FRONT";
        public const string Kitchen = "KITCHEN";
        public const string Management = "MANAGEMENT";
    }

    [Serializable]
    public class RoleCapacityOverride
    {
        public string restaurantTypeId;
        public double dailyCapacity;
    }

    [Serializable]
    public class RoleProfile
    {
        public string roleId;
        /// <summary>FRONT (atendimento), KITCHEN (producao) ou MANAGEMENT.</summary>
        public string function = RoleFunction.Front;
        /// <summary>Pedidos por dia que uma pessoa do cargo atende ou produz (Bloco 6.1).</summary>
        public double dailyCapacity;
        public List<RoleCapacityOverride> capacityByRestaurant = new List<RoleCapacityOverride>();
        // O efeito do gerente nas faltas (5% ao dia sem gerente, 3% com gerente, Bloco 7) fica no
        // modificador "manager" do evento insufficient_team, e nao aqui.
        /// <summary>Hazard ratio de eventos operacionais negativos (gerente: -15%).</summary>
        public double operationalRiskHazard = 1d;
        public double productivityBonus;
        /// <summary>Pontos de satisfacao em restaurante premium (maitre).</summary>
        public double premiumSatisfactionBonus;

        public double CapacityFor(string restaurantTypeId)
        {
            var match = capacityByRestaurant?.Find(c => c.restaurantTypeId == restaurantTypeId);
            return match != null ? match.dailyCapacity : dailyCapacity;
        }
    }

    public static class EquipmentCategoryId
    {
        public const string Refrigeration = "REFRIGERATION";
        public const string Cooking = "COOKING";
        public const string Preparation = "PREPARATION";
        public const string Service = "SERVICE";
        public const string PointOfSale = "POS";
    }

    [Serializable]
    public class EquipmentCategoryProfile
    {
        public string id;
        public string displayName;
        /// <summary>Falhas esperadas por mes de uma unidade padrao, nova, sem uso intenso.</summary>
        public double baseFailuresPerMonth;
        /// <summary>Consumo medio em kWh por dia de uma unidade padrao.</summary>
        public double energyKwhPerDay;
        /// <summary>Capacidade de cozinha quando todas as unidades da categoria estao paradas (1 = sem efeito).</summary>
        public double kitchenFactorWhenDown = 1d;
        public double frontFactorWhenDown = 1d;
        public double physicalFactorWhenDown = 1d;
        /// <summary>Capacidade quando a categoria nao existe no restaurante (nunca comprada).</summary>
        public double kitchenFactorWhenMissing = 1d;
        public double physicalFactorWhenMissing = 1d;
        /// <summary>Fracao do estoque perdida no momento da falha (unidade padrao).</summary>
        public double stockLossOnFailure;
        /// <summary>Evento disparado quando uma unidade falha (vazio = falha silenciosa).</summary>
        public string failureEventId;
    }

    public static class EquipmentTierId
    {
        public const string Economic = "ECONOMIC";
        public const string Standard = "STANDARD";
        public const string Premium = "PREMIUM";
    }

    [Serializable]
    public class EquipmentTierProfile
    {
        public string id;
        public string displayName;
        /// <summary>Multiplica a taxa de falhas da categoria.</summary>
        public double hazardFactor = 1d;
        public double energyFactor = 1d;
        public double lifespanMonths = 60d;
        /// <summary>Custo de um conserto completo como fracao do preco de compra.</summary>
        public double repairCostFraction = 0.2d;
        public double qualityBonus;
        /// <summary>Multiplica a perda de estoque na falha (isolamento termico pior ou melhor).</summary>
        public double stockLossFactor = 1d;
        /// <summary>Preco relativo ao modelo padrao, usado na reacao "substituir".</summary>
        public double priceFactor = 1d;
    }

    [Serializable]
    public class EquipmentProfile
    {
        /// <summary>id do EquipmentData.</summary>
        public string equipmentId;
        public string categoryId;
        public string tierId = EquipmentTierId.Standard;
        /// <summary>Qualidade somada ao indice de qualidade (o qualityBonus do asset e somado tambem).</summary>
        public double qualityBonus;
        /// <summary>Pedidos por dia somados a capacidade de cozinha (o capacityBonus do asset e mensal e e somado tambem).</summary>
        public double kitchenCapacityBonus;
        public double physicalCapacityBonus;
    }

    // ------------------------------------------------------------------
    //  Politicas de gestao (decisoes mensais extensiveis)
    // ------------------------------------------------------------------

    [Serializable]
    public class PolicyOption
    {
        public string id;
        public string displayName;
        public string description;
        /// <summary>Custo fixo mensal da opcao (R$).</summary>
        public double monthlyCost;
        /// <summary>Linha da DRE onde o custo mensal entra (MARKETING, MAINTENANCE, STAFF, OTHER).</summary>
        public string costLine = CostLine.Other;
        public List<Modifier> modifiers = new List<Modifier>();
    }

    [Serializable]
    public class PolicyCategory
    {
        public string id;
        public string displayName;
        public string description;
        public string defaultOptionId;
        public List<PolicyOption> options = new List<PolicyOption>();

        public PolicyOption Option(string optionId) => options.Find(o => o.id == optionId);
    }

    public static class CostLine
    {
        public const string Marketing = "MARKETING";
        public const string Maintenance = "MAINTENANCE";
        public const string Staff = "STAFF";
        public const string Other = "OTHER";
        public const string Fines = "FINES";
        public const string Revenue = "OTHER_INCOME";
        /// <summary>Compra de ativo (substituicao de equipamento): sai do caixa, nao e despesa.</summary>
        public const string Capex = "CAPEX";
    }
}
