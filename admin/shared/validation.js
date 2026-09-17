/**
 * Motor generico de schema: cria itens vazios, normaliza e valida.
 * E usado pelo navegador (validacao ao vivo) e pelo servidor (validacao final),
 * garantindo que as duas pontas apliquem exatamente as mesmas regras.
 *
 * Tipos de campo suportados:
 *   string | text | number | integer | boolean | enum | enumList | object | list
 *
 * Caminhos (paths) usam ponto e indice: "options.1.effects.cashDelta".
 */
import { ENUMS, enumValues } from './enums.js';

export const SNAKE_CASE = /^[a-z0-9]+(_[a-z0-9]+)*$/;

/* ----------------------------------------------------------------------------
 * Utilitarios de caminho
 * ------------------------------------------------------------------------- */
export function joinPath(base, key) {
  return base === '' || base == null ? String(key) : `${base}.${key}`;
}

export function getPath(obj, path) {
  if (!path) return obj;
  return String(path)
    .split('.')
    .reduce((acc, key) => (acc == null ? undefined : acc[key]), obj);
}

export function setPath(obj, path, value) {
  const keys = String(path).split('.');
  let cur = obj;
  for (let i = 0; i < keys.length - 1; i++) {
    const key = keys[i];
    if (cur[key] == null || typeof cur[key] !== 'object') {
      cur[key] = /^\d+$/.test(keys[i + 1]) ? [] : {};
    }
    cur = cur[key];
  }
  cur[keys[keys.length - 1]] = value;
}

export function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

/** Todos os campos de nivel superior do schema (as secoes sao apenas visuais). */
export function rootFields(schema) {
  return schema.sections.flatMap((section) => section.fields);
}

/* ----------------------------------------------------------------------------
 * Criacao e normalizacao
 * ------------------------------------------------------------------------- */
function defaultFor(field) {
  if (field.default !== undefined) return clone(field.default);
  switch (field.type) {
    case 'string':
    case 'text':
      return '';
    case 'number':
    case 'integer':
      return 0;
    case 'boolean':
      return false;
    case 'enum':
      return enumValues(field.enum)[0];
    case 'enumList':
      return [];
    case 'object':
      return createEmptyObject(field.fields);
    case 'list': {
      const count = field.minItems ?? 0;
      return Array.from({ length: count }, () => createEmptyObject(field.item.fields));
    }
    default:
      return null;
  }
}

export function createEmptyObject(fields) {
  const obj = {};
  for (const field of fields) obj[field.key] = defaultFor(field);
  return obj;
}

export function createEmptyItem(schema) {
  return createEmptyObject(rootFields(schema));
}

function normalizeValue(field, value) {
  switch (field.type) {
    case 'string':
    case 'text':
      return value == null ? '' : String(value);
    case 'number':
    case 'integer':
      if (typeof value === 'number') return value;
      if (value == null || value === '') return null;
      if (typeof value === 'string' && !Number.isNaN(Number(value))) return Number(value);
      return value; // invalido: a validacao aponta o erro
    case 'boolean':
      return value === true || value === 'true';
    case 'enum':
      return value == null || value === '' ? defaultFor(field) : value;
    case 'enumList':
      return Array.isArray(value) ? [...new Set(value)] : [];
    case 'object':
      return normalizeObject(field.fields, value);
    case 'list':
      return Array.isArray(value) ? value.map((it) => normalizeObject(field.item.fields, it)) : [];
    default:
      return value;
  }
}

/** Mantem somente as chaves do schema, na ordem do schema (igual ao C#). */
export function normalizeObject(fields, raw) {
  const source = raw && typeof raw === 'object' ? raw : {};
  const out = {};
  for (const field of fields) {
    out[field.key] = field.key in source ? normalizeValue(field, source[field.key]) : defaultFor(field);
  }
  return out;
}

export function normalizeItem(schema, raw) {
  return normalizeObject(rootFields(schema), raw);
}

/* ----------------------------------------------------------------------------
 * Validacao
 * ------------------------------------------------------------------------- */
function issue(level, path, message) {
  return { level, path, message };
}

function validateValue(field, value, path, issues) {
  const label = field.label;

  switch (field.type) {
    case 'string':
    case 'text': {
      const text = typeof value === 'string' ? value : '';
      if (field.required && text.trim() === '') {
        issues.push(issue('error', path, `Preencha o campo ${label}.`));
        return;
      }
      if (text !== '' && field.pattern && !field.pattern.regex.test(text)) {
        issues.push(issue('error', path, `${label}: ${field.pattern.message}`));
      }
      if (field.maxLength && text.length > field.maxLength) {
        issues.push(issue('error', path, `${label} deve ter no máximo ${field.maxLength} caracteres.`));
      }
      return;
    }

    case 'number':
    case 'integer': {
      if (typeof value !== 'number' || !Number.isFinite(value)) {
        issues.push(issue('error', path, `${label}: informe um número.`));
        return;
      }
      if (field.type === 'integer' && !Number.isInteger(value)) {
        issues.push(issue('error', path, `${label} deve ser um número inteiro.`));
      }
      if (field.min !== undefined && value < field.min) {
        issues.push(issue('error', path, `${label} deve ser maior ou igual a ${field.min}.`));
      }
      if (field.exclusiveMin !== undefined && value <= field.exclusiveMin) {
        issues.push(issue('error', path, `${label} deve ser maior que ${field.exclusiveMin}.`));
      }
      if (field.max !== undefined && value > field.max) {
        issues.push(issue('error', path, `${label} deve ser menor ou igual a ${field.max}.`));
      }
      return;
    }

    case 'boolean':
      if (typeof value !== 'boolean') issues.push(issue('error', path, `${label}: valor inválido.`));
      return;

    case 'enum':
      if (!enumValues(field.enum).includes(value)) {
        issues.push(issue('error', path, `${label}: valor "${value}" não existe em ${field.enum}.`));
      }
      return;

    case 'enumList': {
      if (!Array.isArray(value)) {
        issues.push(issue('error', path, `${label}: deve ser uma lista.`));
        return;
      }
      const allowed = enumValues(field.enum);
      for (const v of value) {
        if (!allowed.includes(v)) issues.push(issue('error', path, `${label}: valor "${v}" não existe em ${field.enum}.`));
      }
      return;
    }

    case 'object':
      validateObject(field.fields, value ?? {}, path, issues);
      return;

    case 'list': {
      if (!Array.isArray(value)) {
        issues.push(issue('error', path, `${label}: deve ser uma lista.`));
        return;
      }
      const { minItems, maxItems } = field;
      if (minItems !== undefined && maxItems !== undefined && minItems === maxItems && value.length !== minItems) {
        issues.push(issue('error', path, `${label}: são necessários exatamente ${minItems} itens (há ${value.length}).`));
      } else {
        if (minItems !== undefined && value.length < minItems) {
          issues.push(issue('error', path, `${label}: mínimo de ${minItems} item(ns).`));
        }
        if (maxItems !== undefined && value.length > maxItems) {
          issues.push(issue('error', path, `${label}: máximo de ${maxItems} item(ns).`));
        }
      }
      value.forEach((item, index) => validateObject(field.item.fields, item ?? {}, joinPath(path, index), issues));

      if (field.uniqueBy) {
        const seen = new Map();
        value.forEach((item, index) => {
          const key = item?.[field.uniqueBy];
          if (key === '' || key == null) return;
          if (seen.has(key)) {
            issues.push(issue('error', joinPath(joinPath(path, index), field.uniqueBy), `${label}: o ID "${key}" está repetido.`));
          } else {
            seen.set(key, index);
          }
        });
      }
      return;
    }

    default:
      return;
  }
}

function validateObject(fields, obj, basePath, issues) {
  for (const field of fields) {
    validateValue(field, obj[field.key], joinPath(basePath, field.key), issues);
  }
}

/**
 * Valida um item completo.
 * @param schema    definicao da colecao
 * @param item      item ja normalizado
 * @param context   { items: todos os itens salvos, originalId: id antes da edicao (ou null) }
 */
export function validateItem(schema, item, context = {}) {
  const issues = [];
  validateObject(rootFields(schema), item, '', issues);

  // Unicidade do ID dentro da colecao
  const idField = schema.idField;
  const id = item[idField];
  const others = (context.items ?? []).filter((other) => other[idField] !== context.originalId);
  if (id && others.some((other) => other[idField] === id)) {
    issues.push(issue('error', idField, `Já existe um ${schema.singular} com o ID "${id}".`));
  }

  if (typeof schema.rules === 'function') {
    issues.push(...schema.rules(item, context));
  }
  return issues;
}

/** Regras que olham o catalogo inteiro (ex.: distribuicao esperada). */
export function validateCatalog(schema, items) {
  return typeof schema.catalogRules === 'function' ? schema.catalogRules(items) : [];
}

export function hasErrors(issues) {
  return issues.some((i) => i.level === 'error');
}

export function countByLevel(issues) {
  return {
    errors: issues.filter((i) => i.level === 'error').length,
    warnings: issues.filter((i) => i.level === 'warning').length,
  };
}

export { ENUMS };
