import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import events from '../shared/collections/events.js';
import { createEmptyItem, normalizeItem, validateItem, validateCatalog, hasErrors } from '../shared/validation.js';

const seed = JSON.parse(fs.readFileSync(new URL('../../admin-data/events.json', import.meta.url), 'utf8'));

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
