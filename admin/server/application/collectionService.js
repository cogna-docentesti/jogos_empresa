import {
  normalizeItem, validateItem, validateCatalog, hasErrors, countByLevel,
} from '../../shared/validation.js';
import { getCollection } from '../../shared/collections/index.js';
import { notFound, conflict, unprocessable, StorageConflictError } from '../errors.js';

const SCHEMA_VERSION = 1;

/**
 * Casos de uso do admin (camada de aplicacao).
 * Nao conhece HTTP nem o formato de armazenamento: recebe um "storage"
 * com read(colecao) e write(colecao, documento, mensagem).
 */
export class CollectionService {
  constructor(storage) {
    this.storage = storage;
    this.cache = new Map(); // colecao -> { doc, loadedAt }
    this.queue = Promise.resolve(); // serializa gravacoes
  }

  schema(name) {
    const schema = getCollection(name);
    if (!schema) throw notFound(`Coleção "${name}" não existe.`);
    return schema;
  }

  emptyDoc(name) {
    return { schemaVersion: SCHEMA_VERSION, collection: name, updatedAt: null, items: [] };
  }

  async load(name, { fresh = false } = {}) {
    const cached = this.cache.get(name);
    const ttl = this.storage.cacheTtlMs ?? 0;
    if (!fresh && cached && Date.now() - cached.loadedAt < ttl) return cached.doc;

    const doc = (await this.storage.read(name)) ?? this.emptyDoc(name);
    if (!Array.isArray(doc.items)) doc.items = [];
    this.cache.set(name, { doc, loadedAt: Date.now() });
    return doc;
  }

  report(schema, items) {
    const perItem = {};
    for (const item of items) {
      perItem[item[schema.idField]] = countByLevel(
        validateItem(schema, item, { items, originalId: item[schema.idField] }),
      );
    }
    return { perItem, catalog: validateCatalog(schema, items) };
  }

  async list(name) {
    const schema = this.schema(name);
    const doc = await this.load(name);
    return { ...doc, report: this.report(schema, doc.items) };
  }

  async get(name, id) {
    const schema = this.schema(name);
    const doc = await this.load(name);
    const item = doc.items.find((it) => it[schema.idField] === id);
    if (!item) throw notFound(`${schema.singular} "${id}" não encontrado.`);
    return item;
  }

  /**
   * Aplica uma alteracao no documento e grava.
   * Em caso de conflito (outra gravacao no meio), rele os dados e tenta de novo.
   */
  mutate(name, change, message) {
    const run = async () => {
      for (let attempt = 1; attempt <= 3; attempt++) {
        const current = await this.load(name, { fresh: attempt > 1 || this.storage.cacheTtlMs > 0 });
        const next = { ...current, schemaVersion: SCHEMA_VERSION, collection: name, items: [...current.items] };
        const result = change(next);
        next.updatedAt = new Date().toISOString();
        try {
          await this.storage.write(name, next, message);
          this.cache.set(name, { doc: next, loadedAt: Date.now() });
          return result;
        } catch (err) {
          if (!(err instanceof StorageConflictError) || attempt === 3) throw err;
        }
      }
    };
    const task = this.queue.then(run, run);
    this.queue = task.catch(() => {});
    return task;
  }

  #prepare(schema, raw, items, originalId) {
    const item = normalizeItem(schema, raw);
    const issues = validateItem(schema, item, { items, originalId });
    if (hasErrors(issues)) throw unprocessable('Existem erros de validação.', issues);
    return { item, issues };
  }

  async create(name, raw) {
    const schema = this.schema(name);
    return this.mutate(name, (doc) => {
      const { item, issues } = this.#prepare(schema, raw, doc.items, null);
      doc.items.push(item);
      return { item, issues };
    }, `admin: cria ${schema.singular} ${raw?.[schema.idField] ?? ''}`.trim());
  }

  async update(name, id, raw) {
    const schema = this.schema(name);
    return this.mutate(name, (doc) => {
      const index = doc.items.findIndex((it) => it[schema.idField] === id);
      if (index < 0) throw notFound(`${schema.singular} "${id}" não encontrado.`);
      const { item, issues } = this.#prepare(schema, raw, doc.items, id);
      doc.items[index] = item;
      return { item, issues };
    }, `admin: atualiza ${schema.singular} ${id}`);
  }

  async remove(name, id) {
    const schema = this.schema(name);
    return this.mutate(name, (doc) => {
      const index = doc.items.findIndex((it) => it[schema.idField] === id);
      if (index < 0) throw notFound(`${schema.singular} "${id}" não encontrado.`);
      doc.items.splice(index, 1);
      return { removed: id };
    }, `admin: remove ${schema.singular} ${id}`);
  }

  /** Substitui a colecao inteira (importacao de arquivo JSON). */
  async replaceAll(name, payload) {
    const schema = this.schema(name);
    const rawItems = Array.isArray(payload) ? payload : payload?.items;
    if (!Array.isArray(rawItems)) throw unprocessable('O arquivo deve conter uma lista "items".', []);

    const items = rawItems.map((raw) => normalizeItem(schema, raw));
    const issues = [];
    items.forEach((item, index) => {
      const itemIssues = validateItem(schema, item, { items: items.filter((_, i) => i !== index), originalId: null });
      itemIssues.forEach((iss) => issues.push({ ...iss, item: item[schema.idField] || `#${index + 1}` }));
    });
    if (hasErrors(issues)) throw unprocessable('O arquivo possui erros de validação. Nada foi importado.', issues);

    const ids = new Set(items.map((it) => it[schema.idField]));
    if (ids.size !== items.length) throw conflict('O arquivo possui IDs repetidos.');

    return this.mutate(name, (doc) => {
      doc.items = items;
      return { count: items.length, issues };
    }, `admin: importa ${items.length} ${schema.label.toLowerCase()}`);
  }
}
