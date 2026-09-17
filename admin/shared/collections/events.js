/**
 * Colecao "events" -> ScriptableObject EventData do Unity.
 *
 * Espelha:
 *   Assets/Scripts/Data/ScriptableObjects/Event/EventData.cs
 *   Assets/Scripts/Data/ScriptableObjects/Event/EventOption.cs
 *   Assets/Scripts/Data/ScriptableObjects/Event/EventEffectData.cs
 *   Assets/Scripts/Data/ScriptableObjects/Event/EventConditionData.cs
 *
 * As regras de erro reproduzem a validacao do Assets/Editor/Events/EventAssetGenerator.cs.
 * As regras de distribuicao do catalogo (20 eventos, 10/10, 15/5) viram AVISOS aqui,
 * para que seja possivel criar eventos novos sem travar o cadastro.
 */
import { SNAKE_CASE } from '../validation.js';

const CURRENT_CYCLE_ROUNDS = 3; // ciclo do jogo reduzido de 12 para 3 meses

const snakeCase = {
  regex: SNAKE_CASE,
  message: 'use snake_case (letras minúsculas, números e "_" entre palavras). Ex.: quebra_do_freezer',
};

const effectFields = [
  {
    key: 'cashDelta', label: 'Caixa (R$)', type: 'number', default: 0, step: 50, display: 'currency', width: 'third',
    help: 'Valor somado ao caixa. Negativo = gasto. Ex.: -3500.',
  },
  {
    key: 'demandMultiplier', label: 'Demanda', type: 'number', default: 1, min: 0, step: 0.01, display: 'multiplier', width: 'third',
    help: '1 = sem efeito. 1.20 = +20%. 0.85 = -15%.',
  },
  {
    key: 'capacityMultiplier', label: 'Capacidade', type: 'number', default: 1, min: 0, step: 0.01, display: 'multiplier', width: 'third',
    help: 'Multiplica a capacidade de atendimento.',
  },
  {
    key: 'stockMultiplier', label: 'Estoque', type: 'number', default: 1, min: 0, step: 0.01, display: 'multiplier', width: 'third',
    help: 'Multiplica o estoque disponível.',
  },
  {
    key: 'ingredientCostMultiplier', label: 'Custo dos ingredientes', type: 'number', default: 1, min: 0, step: 0.01, display: 'multiplier', invertTone: true, width: 'third',
    help: 'Acima de 1 encarece os ingredientes.',
  },
  {
    key: 'averageTicketMultiplier', label: 'Ticket médio', type: 'number', default: 1, min: 0, step: 0.01, display: 'multiplier', width: 'third',
    help: 'Multiplica o valor médio gasto por cliente.',
  },
  {
    key: 'reputationDelta', label: 'Reputação (pontos)', type: 'integer', default: 0, step: 1, display: 'delta', width: 'third',
    help: 'Pontos somados à reputação. Ex.: -2 ou 3.',
  },
  {
    key: 'duration', label: 'Duração do efeito', type: 'enum', enum: 'EventEffectDuration', default: 'IMMEDIATE', width: 'third',
    help: 'Por quanto tempo os multiplicadores valem.',
  },
];

const NEUTRAL_EFFECTS = {
  cashDelta: 0, demandMultiplier: 1, capacityMultiplier: 1, stockMultiplier: 1,
  ingredientCostMultiplier: 1, averageTicketMultiplier: 1, reputationDelta: 0,
};

export default {
  name: 'events',
  label: 'Eventos',
  singular: 'evento',
  gender: 'm',
  idField: 'id',
  titleField: 'title',
  description:
    'Situações que acontecem durante as rodadas. Cada evento oferece 3 opções ao jogador, e cada opção aplica efeitos sobre caixa, demanda, capacidade, estoque, custos, ticket e reputação.',

  unity: {
    scriptableObject: 'EventData',
    assetFolder: 'Assets/Resources/Events',
    importerMenu: 'Tools > Jogo > Eventos > Importar JSON do Admin',
  },

  sections: [
    {
      title: 'Identificação',
      fields: [
        {
          key: 'id', label: 'ID', type: 'string', required: true, pattern: snakeCase, mono: true, width: 'half',
          placeholder: 'ex.: quebra_do_freezer',
          help: 'Também vira o nome do arquivo .asset e é gravado no histórico da sessão. Evite alterar depois de publicado.',
        },
        { key: 'title', label: 'Título', type: 'string', required: true, maxLength: 80, width: 'half', placeholder: 'ex.: Quebra do freezer' },
        {
          key: 'description', label: 'Descrição', type: 'text', required: true, rows: 3, width: 'full',
          help: 'Texto mostrado ao jogador explicando a situação.',
        },
      ],
    },
    {
      title: 'Educacional',
      fields: [
        {
          key: 'educationalConcept', label: 'Conceito educacional', type: 'text', rows: 2, width: 'full',
          placeholder: 'ex.: Gestão de riscos operacionais e manutenção preventiva',
          help: 'Opcional. Conceito de Administração que o evento trabalha.',
        },
      ],
    },
    {
      title: 'Classificação e sorteio',
      fields: [
        { key: 'polarity', label: 'Polaridade', type: 'enum', enum: 'EventPolarity', default: 'NEGATIVE', width: 'third', rerender: true },
        {
          key: 'triggerType', label: 'Gatilho', type: 'enum', enum: 'EventTriggerType', default: 'RANDOM', width: 'third', rerender: true,
          help: 'Aleatório: sorteado sem condições. Condicional: só ocorre se as condições forem atendidas.',
        },
        {
          key: 'baseWeight', label: 'Peso no sorteio', type: 'number', default: 1, exclusiveMin: 0, step: 0.1, width: 'third',
          help: 'Peso relativo. 2 tem o dobro de chance de 1.',
        },
        {
          key: 'canRepeat', label: 'Pode se repetir na mesma sessão', type: 'boolean', default: false, width: 'full', rerender: true,
        },
        {
          key: 'applicableRestaurantTypes', label: 'Tipos de restaurante', type: 'enumList', enum: 'RestaurantType', width: 'full',
          help: 'Nenhum marcado = vale para todos os tipos.',
        },
      ],
    },
    {
      title: 'Rodadas',
      fields: [
        {
          key: 'minRound', label: 'Rodada mínima', type: 'integer', default: 1, min: 1, step: 1, width: 'half',
          help: 'Cada rodada corresponde a um mês do ciclo.',
        },
        {
          key: 'maxRound', label: 'Rodada máxima', type: 'integer', default: CURRENT_CYCLE_ROUNDS, min: 1, step: 1, width: 'half',
          help: `O ciclo atual tem ${CURRENT_CYCLE_ROUNDS} rodadas.`,
        },
      ],
    },
    {
      title: 'Condições',
      visibleWhen: (item) => item.triggerType === 'CONDITIONAL' || (item.conditions?.length ?? 0) > 0,
      hiddenHint: 'Eventos aleatórios não possuem condições. Mude o gatilho para "Condicional" para configurar.',
      fields: [
        {
          key: 'conditions', label: 'Condições', type: 'list', width: 'full',
          itemLabel: (_item, index) => `Condição ${index + 1}`,
          addLabel: 'Adicionar condição',
          help: 'Todas as condições são comparadas com o estado atual do restaurante.',
          item: {
            fields: [
              { key: 'type', label: 'Indicador', type: 'enum', enum: 'EventConditionType', width: 'third' },
              { key: 'comparison', label: 'Comparação', type: 'enum', enum: 'EventConditionOperator', default: 'LESS_OR_EQUAL', width: 'third' },
              { key: 'value', label: 'Valor', type: 'number', default: 0, step: 0.01, width: 'third' },
            ],
          },
        },
      ],
    },
    {
      title: 'Opções do jogador',
      fields: [
        {
          key: 'options', label: 'Opções', type: 'list', minItems: 3, maxItems: 3, uniqueBy: 'id', width: 'full', reorderable: true,
          itemLabel: (item, index) => `Opção ${String.fromCharCode(65 + index)}${item.title ? ': ' + item.title : ''}`,
          addLabel: 'Adicionar opção',
          item: {
            fields: [
              { key: 'id', label: 'ID da opção', type: 'string', required: true, pattern: snakeCase, mono: true, width: 'half', placeholder: 'ex.: repair_now' },
              { key: 'title', label: 'Título', type: 'string', required: true, maxLength: 80, width: 'half' },
              { key: 'description', label: 'Descrição', type: 'text', rows: 2, width: 'full' },
              {
                key: 'effects', label: 'Efeitos', type: 'object', width: 'full', fields: effectFields,
                default: { ...NEUTRAL_EFFECTS, duration: 'IMMEDIATE' },
              },
            ],
          },
        },
      ],
    },
  ],

  /** Colunas da tabela de listagem. */
  columns: [
    { key: 'id', label: 'ID', mono: true },
    { key: 'title', label: 'Título' },
    { key: 'polarity', label: 'Polaridade', enum: 'EventPolarity', badge: { POSITIVE: 'green', NEGATIVE: 'red' } },
    { key: 'triggerType', label: 'Gatilho', enum: 'EventTriggerType', badge: { RANDOM: 'gray', CONDITIONAL: 'amber' } },
    { key: 'baseWeight', label: 'Peso', align: 'right' },
    { key: 'rounds', label: 'Rodadas', align: 'center', value: (item) => `${item.minRound}–${item.maxRound}` },
  ],

  filters: [
    { key: 'polarity', label: 'Polaridade', enum: 'EventPolarity' },
    { key: 'triggerType', label: 'Gatilho', enum: 'EventTriggerType' },
  ],

  searchKeys: ['id', 'title', 'description'],

  /** Regras especificas de eventos (alem das regras de tipo definidas nos campos). */
  rules(item) {
    const issues = [];

    if (Number.isInteger(item.minRound) && Number.isInteger(item.maxRound) && item.maxRound < item.minRound) {
      issues.push({ level: 'error', path: 'maxRound', message: `Intervalo de rodadas inválido (${item.minRound}–${item.maxRound}).` });
    }
    if (Number.isInteger(item.maxRound) && item.maxRound > CURRENT_CYCLE_ROUNDS) {
      issues.push({ level: 'warning', path: 'maxRound', message: `O ciclo atual do jogo tem ${CURRENT_CYCLE_ROUNDS} rodadas (meses). Rodadas acima disso nunca ocorrem.` });
    }

    const conditionCount = item.conditions?.length ?? 0;
    if (item.triggerType === 'CONDITIONAL' && conditionCount === 0) {
      issues.push({ level: 'error', path: 'conditions', message: 'Evento condicional precisa de ao menos uma condição.' });
    }
    if (item.triggerType === 'RANDOM' && conditionCount > 0) {
      issues.push({ level: 'error', path: 'conditions', message: 'Evento aleatório não pode ter condições. Remova as condições ou mude o gatilho.' });
    }
    if (item.triggerType === 'CONDITIONAL' && item.polarity === 'POSITIVE') {
      issues.push({ level: 'warning', path: 'polarity', message: 'No catálogo atual, todos os eventos condicionais são negativos.' });
    }

    if (item.canRepeat) {
      issues.push({ level: 'warning', path: 'canRepeat', message: 'O EventAssetGenerator atual exige canRepeat = false. Confirme se o sorteio já trata repetição.' });
    }

    (item.options ?? []).forEach((option, index) => {
      const fx = option.effects ?? {};
      ['demandMultiplier', 'capacityMultiplier', 'stockMultiplier', 'ingredientCostMultiplier', 'averageTicketMultiplier'].forEach((key) => {
        if (fx[key] === 0) {
          issues.push({ level: 'warning', path: `options.${index}.effects.${key}`, message: `Opção ${String.fromCharCode(65 + index)}: multiplicador 0 zera o valor por completo.` });
        }
      });
    });

    return issues;
  },

  /** Distribuicao esperada pelo EventAssetGenerator. Aqui sao apenas avisos. */
  summary(items) {
    const count = (fn) => items.filter(fn).length;
    return [
      { label: 'Total', value: items.length, expected: 20 },
      { label: 'Positivos', value: count((e) => e.polarity === 'POSITIVE'), expected: 10 },
      { label: 'Negativos', value: count((e) => e.polarity === 'NEGATIVE'), expected: 10 },
      { label: 'Aleatórios', value: count((e) => e.triggerType === 'RANDOM'), expected: 15 },
      { label: 'Condicionais', value: count((e) => e.triggerType === 'CONDITIONAL'), expected: 5 },
    ];
  },

  catalogRules(items) {
    return this.summary(items)
      .filter((s) => s.value !== s.expected)
      .map((s) => ({
        level: 'warning',
        path: null,
        message: `${s.label}: ${s.value} (o EventAssetGenerator espera ${s.expected}).`,
      }));
  },
};
