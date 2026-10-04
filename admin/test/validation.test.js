import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import events from '../shared/collections/events.js';
import { createEmptyItem, normalizeItem, validateItem, validateCatalog, hasErrors } from '../shared/validation.js';

const seed = JSON.parse(fs.readFileSync(new URL('../../admin-data/events.json', import.meta.url), 'utf8'));

test('grupos dos 20 eventos correspondem ao catalogo e NONE e permitido', () => {
  const groups = {
    SOCIAL_REPUTATION: ['negative_review', 'positive_online_review', 'influencer_mention'],
    SUPPLIER: ['ingredient_price_increase', 'supplier_delay', 'supplier_discount', 'bulk_purchase_opportunity'],
    COMPETITION: ['new_competitor', 'competitor_temporary_closure'],
    DEMAND_SPIKE: ['extra_local_demand', 'lunch_demand_peak', 'local_festival', 'corporate_order'],
    NONE: ['freezer_breakdown', 'insufficient_team', 'health_inspection', 'excessive_food_waste', 'heavy_rain', 'payment_system_failure', 'team_productivity_boost'],
  };
  assert.equal(Object.values(groups).flat().length, seed.items.length);
  for (const [group, ids] of Object.entries(groups)) {
    for (const id of ids) assert.equal(seed.items.find(e => e.id === id).exclusionGroup, group);
  }
});

test('admin rejeita grupo desconhecido e preserva exclusionGroup na normalizacao', () => {
  const item = structuredClone(seed.items[0]);
  item.exclusionGroup = 'UNKNOWN';
  const normalized = normalizeItem(events, item);
  assert.equal(normalized.exclusionGroup, 'UNKNOWN');
  assert.ok(validateItem(events, normalized).some(i => i.level === 'error' && i.path === 'exclusionGroup'));
  item.exclusionGroup = 'NONE';
  assert.ok(!validateItem(events, normalizeItem(events, item)).some(i => i.path === 'exclusionGroup'));
});

test('catalogo possui 17 aleatorios e 3 condicionais negativos, com cobertura inferior a 1', () => {
  assert.equal(seed.items.filter(e => e.triggerType === 'RANDOM').length, 17);
  const conditional = seed.items.filter(e => e.triggerType === 'CONDITIONAL');
  assert.equal(conditional.length, 3);
  assert.ok(conditional.every(e => e.polarity === 'NEGATIVE'));
  for (const id of ['health_inspection', 'excessive_food_waste']) {
    const event = seed.items.find(e => e.id === id);
    assert.equal(event.triggerType, 'RANDOM');
    assert.deepEqual(event.conditions, []);
  }
  assert.deepEqual(seed.items.find(e => e.id === 'insufficient_team').conditions,
    [{ type: 'TEAM_COVERAGE_RATIO', comparison: 'LESS_THAN', value: 1 }]);
});

test('indicadores descontinuados nao podem ser salvos', () => {
  for (const type of ['SANITARY_RISK_SCORE', 'STOCK_COVERAGE_RATIO']) {
    const item = structuredClone(seed.items.find(e => e.id === 'negative_review'));
    item.conditions[0].type = type;
    const issues = validateItem(events, normalizeItem(events, item), { items: seed.items, originalId: item.id });
    assert.ok(issues.some(i => i.level === 'error' && i.path === 'conditions.0.type'));
  }
});

test('os 20 eventos atuais do jogo passam sem erros nem avisos', () => {
  for (const item of seed.items) {
    const issues = validateItem(events, normalizeItem(events, item), { items: seed.items, originalId: item.id });
    assert.deepEqual(issues, [], `${item.id}: ${JSON.stringify(issues)}`);
  }
  assert.deepEqual(validateCatalog(events, seed.items), []);
});

test('normalizeItem nao altera os dados atuais (ordem e valores)', () => {
  for (const item of seed.items) {
    assert.equal(JSON.stringify(normalizeItem(events, item)), JSON.stringify(item));
  }
});

test('item vazio tem 3 opcoes com efeitos neutros e falha por campos obrigatorios', () => {
  const empty = createEmptyItem(events);
  assert.equal(empty.options.length, 3);
  assert.equal(empty.options[0].effects.demandMultiplier, 1);
  assert.ok(hasErrors(validateItem(events, empty, { items: seed.items })));
});

test('regras espelhadas do EventAssetGenerator', () => {
  const base = structuredClone(seed.items.find((e) => e.id === 'heavy_rain'));
  const check = (mutate, expectedPath) => {
    const item = structuredClone(base);
    mutate(item);
    const issues = validateItem(events, normalizeItem(events, item), { items: seed.items, originalId: 'heavy_rain' });
    assert.ok(issues.some((i) => i.level === 'error' && i.path === expectedPath), `${expectedPath}: ${JSON.stringify(issues)}`);
  };
  check((e) => { e.id = 'Chuva Forte'; }, 'id');
  check((e) => { e.id = 'freezer_breakdown'; }, 'id');
  check((e) => { e.options.pop(); }, 'options');
  check((e) => { e.options[1].id = e.options[0].id; }, 'options.1.id');
  check((e) => { e.baseWeight = 0; }, 'baseWeight');
  check((e) => { e.minRound = 3; e.maxRound = 2; }, 'maxRound');
  check((e) => { e.triggerType = 'CONDITIONAL'; }, 'conditions');
  check((e) => { e.conditions = [{ type: 'REPUTATION_SCORE', comparison: 'LESS_OR_EQUAL', value: 50 }]; }, 'conditions');
  check((e) => { e.polarity = 'NEUTRO'; }, 'polarity');
});
