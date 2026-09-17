/**
 * Espelho dos enums C# do projeto Unity.
 *
 * IMPORTANTE: mantenha estes valores em sincronia com
 *   Assets/Scripts/Game/Domain/Enums/*.cs
 *
 * O JSON exportado usa o NOME do valor (ex.: "NEGATIVE"), e nao o indice.
 * Assim, reordenar um enum no C# nao corrompe os dados do admin.
 * O importador do Unity converte o nome para o enum correspondente.
 */
export const ENUMS = {
  EventPolarity: {
    csharp: 'Assets/Scripts/Game/Domain/Enums/EventPolarity.cs',
    values: [
      { value: 'POSITIVE', label: 'Positivo' },
      { value: 'NEGATIVE', label: 'Negativo' },
    ],
  },
  EventTriggerType: {
    csharp: 'Assets/Scripts/Game/Domain/Enums/EventTriggerType.cs',
    values: [
      { value: 'RANDOM', label: 'Aleatório' },
      { value: 'CONDITIONAL', label: 'Condicional' },
    ],
  },
  EventConditionType: {
    csharp: 'Assets/Scripts/Game/Domain/Enums/EventConditionType.cs',
    values: [
      { value: 'EQUIPMENT_QUALITY_SCORE', label: 'Qualidade dos equipamentos' },
      { value: 'TEAM_COVERAGE_RATIO', label: 'Cobertura da equipe' },
      { value: 'SANITARY_RISK_SCORE', label: 'Risco sanitário' },
      { value: 'REPUTATION_SCORE', label: 'Reputação' },
      { value: 'STOCK_COVERAGE_RATIO', label: 'Cobertura de estoque' },
    ],
  },
  EventConditionOperator: {
    csharp: 'Assets/Scripts/Game/Domain/Enums/EventConditionOperator.cs',
    values: [
      { value: 'EQUALS', label: '= igual a' },
      { value: 'NOT_EQUALS', label: '≠ diferente de' },
      { value: 'GREATER_THAN', label: '> maior que' },
      { value: 'GREATER_OR_EQUAL', label: '≥ maior ou igual a' },
      { value: 'LESS_THAN', label: '< menor que' },
      { value: 'LESS_OR_EQUAL', label: '≤ menor ou igual a' },
    ],
  },
  EventEffectDuration: {
    csharp: 'Assets/Scripts/Game/Domain/Enums/EventEffectDuration.cs',
    values: [
      { value: 'IMMEDIATE', label: 'Imediato' },
      { value: 'CURRENT_DAY', label: 'Dia atual' },
      { value: 'CURRENT_MONTH', label: 'Mês atual' },
    ],
  },
  RestaurantType: {
    csharp: 'Assets/Scripts/Game/Domain/Enums/Enums.cs',
    values: [
      { value: 'PODRAO', label: 'Podrão' },
      { value: 'JAPONES', label: 'Japonês' },
      { value: 'FRANCES', label: 'Francês' },
    ],
  },

  // Enums ja existentes no jogo, prontos para os proximos cadastros.
  Segment: {
    csharp: 'Assets/Scripts/Game/Domain/Enums/Enums.cs',
    values: [
      { value: 'LOW', label: 'Baixo' },
      { value: 'MEDIUM', label: 'Médio' },
      { value: 'HIGH', label: 'Alto' },
    ],
  },
  EquipmentCategory: {
    csharp: 'Assets/Scripts/Game/Domain/Enums/Enums.cs',
    values: [
      { value: 'BASIC', label: 'Básico' },
      { value: 'SPECIFIC', label: 'Específico' },
    ],
  },
  RoleType: {
    csharp: 'Assets/Scripts/Game/Domain/Enums/Enums.cs',
    values: [
      { value: 'ATENDENTE', label: 'Atendente' },
      { value: 'GARCOM', label: 'Garçom' },
      { value: 'CHAPEIRO', label: 'Chapeiro' },
      { value: 'CHEF', label: 'Chef' },
      { value: 'SUSHIMAN', label: 'Sushiman' },
      { value: 'GERENTE', label: 'Gerente' },
    ],
  },
};

export function enumValues(name) {
  const def = ENUMS[name];
  if (!def) throw new Error(`Enum desconhecido: ${name}`);
  return def.values.map((v) => v.value);
}

export function enumLabel(name, value) {
  const def = ENUMS[name];
  const found = def?.values.find((v) => v.value === value);
  return found ? found.label : String(value ?? '');
}
