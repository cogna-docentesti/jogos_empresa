import { api } from '../api.js';
import { h, mount, icon, toast, confirmDialog, escapeSelector } from '../ui.js';
import { ENUMS } from '/shared/enums.js';
import {
  clone, getPath, setPath, joinPath, createEmptyItem, createEmptyObject,
  normalizeItem, validateItem, hasErrors,
} from '/shared/validation.js';

/* ----------------------------------------------------------------------------
 * Indicadores visuais ao lado dos campos numericos
 * ------------------------------------------------------------------------- */
function effectBadge(field, value) {
  if (typeof value !== 'number' || !Number.isFinite(value)) return { text: '', tone: '' };
  const signTone = (positive) => (field.invertTone ? !positive : positive) ? 'up' : 'down';

  if (field.display === 'multiplier') {
    if (value === 1) return { text: 'sem efeito', tone: '' };
    const pct = Math.round((value - 1) * 1000) / 10;
    return { text: `${pct > 0 ? '+' : ''}${pct.toLocaleString('pt-BR')}%`, tone: signTone(pct > 0) };
  }
  if (field.display === 'currency') {
    if (value === 0) return { text: 'sem efeito', tone: '' };
    const abs = Math.abs(value).toLocaleString('pt-BR', { maximumFractionDigits: 2 });
    return { text: `${value > 0 ? '+' : '-'}R$ ${abs}`, tone: signTone(value > 0) };
  }
  if (field.display === 'delta') {
    if (value === 0) return { text: 'sem efeito', tone: '' };
    return { text: `${value > 0 ? '+' : ''}${value}`, tone: signTone(value > 0) };
  }
  return { text: '', tone: '' };
}

const fieldId = (path) => `f-${String(path).replace(/[^\w-]/g, '-')}`;

/**
 * Tela de criacao/edicao generica.
 * @param ctx { schema, container, navigate, refreshNav, mode: 'new'|'edit', id, fromId, setGuard }
 */
export async function renderFormView(ctx) {
  const { schema, container, navigate, refreshNav, mode, id, fromId, setGuard } = ctx;
  mount(container, h('div', { class: 'loading' }, 'Carregando...'));

  let listData;
  let draft;
  try {
    listData = await api.list(schema.name);
    const find = (key) => listData.items.find((it) => it[schema.idField] === key);

    if (mode === 'edit') {
      const found = find(id);
      if (!found) throw new Error(`${schema.singular} "${id}" não encontrado.`);
      draft = clone(found);
    } else if (fromId) {
      const source = find(fromId);
      if (!source) throw new Error(`${schema.singular} "${fromId}" não encontrado para duplicar.`);
      draft = clone(source);
      draft[schema.idField] = `${source[schema.idField]}_copia`;
      if (schema.titleField) draft[schema.titleField] = `${source[schema.titleField]} (cópia)`;
    } else {
      draft = createEmptyItem(schema);
    }
  } catch (err) {
    mount(container, 
      h('div', { class: 'card empty' }, err.message, h('div', { style: 'margin-top:12px' },
        h('a', { class: 'btn', href: `#/${schema.name}` }, icon('back'), `Voltar para ${schema.label}`))),
    );
    return;
  }

  const originalId = mode === 'edit' ? id : null;
  let dirty = Boolean(fromId);
  let saving = false;
  let validateTimer = null;
  let titleUpdaters = [];
  setGuard(() => dirty);

  /* ---------- Cabecalho ---------- */
  const titleEl = h('h1');
  const dirtyDot = h('span', { class: 'dirty-dot hidden', title: 'Alterações não salvas' });
  const saveBtn = h('button', { class: 'btn btn-primary', onclick: () => save() }, icon('save'), 'Salvar');

  function updateHeader() {
    const name = draft[schema.titleField] || draft[schema.idField] || '';
    mount(titleEl, 
      mode === 'edit' ? `Editar ${schema.singular}` : `Novo ${schema.singular}`,
      name ? h('span', { class: 'muted', style: 'font-weight:400' }, `: ${name}`) : '',
      dirtyDot,
    );
    dirtyDot.classList.toggle('hidden', !dirty);
    const crumb = document.querySelector('#breadcrumb .current');
    if (crumb) crumb.textContent = mode === 'edit' ? (draft[schema.idField] || id) : `Novo ${schema.singular}`;
  }

  const head = h('div', { class: 'page-head' },
    h('div', {},
      h('a', { href: `#/${schema.name}`, class: 'btn btn-sm', style: 'margin-bottom:10px' }, icon('back'), schema.label),
      titleEl,
    ),
    h('div', { class: 'actions' },
      mode === 'edit' && h('button', { class: 'btn', onclick: () => navigate(`#/${schema.name}/new?from=${encodeURIComponent(originalId)}`) }, icon('copy'), 'Duplicar'),
      mode === 'edit' && h('button', { class: 'btn btn-danger', onclick: () => removeItem() }, icon('trash'), 'Excluir'),
      h('a', { class: 'btn', href: `#/${schema.name}` }, 'Cancelar'),
      saveBtn,
    ),
  );

  /* ---------- Painel lateral ---------- */
  const issuesBox = h('div');
  const sidePanel = h('aside', { class: 'side-panel' },
    h('div', { class: 'card' }, h('h3', {}, 'Validação'), issuesBox),
    schema.unity && h('div', { class: 'card unity-hint' },
      h('h3', {}, 'No Unity'),
      h('div', {}, 'Asset gerado: ', h('code', { id: 'unity-asset-path' }, '')),
      h('div', { style: 'margin-top:8px' }, 'Depois de salvar, atualize o projeto (git pull, se o admin estiver publicado) e use o menu ', h('code', {}, schema.unity.importerMenu), '.'),
    ),
  );

  const formCol = h('div');
  const editor = h('div', { class: 'editor' }, formCol, sidePanel);

  /* ---------- Estado ---------- */
  function markDirty() {
    if (!dirty) { dirty = true; updateHeader(); }
    clearTimeout(validateTimer);
    validateTimer = setTimeout(runValidation, 250);
  }

  function setValue(path, value, { rerender = false } = {}) {
    setPath(draft, path, value);
    markDirty();
    titleUpdaters.forEach((fn) => fn());
    if (path === schema.titleField || path === schema.idField) updateHeader();
    if (path === schema.idField) updateAssetPath();
    if (rerender) renderForm();
  }

  function updateAssetPath() {
    const el = sidePanel.querySelector('#unity-asset-path');
    if (el && schema.unity) el.textContent = `${schema.unity.assetFolder}/${draft[schema.idField] || '<id>'}.asset`;
  }

  /* ---------- Renderizacao dos campos ---------- */
  function wrapField(field, path, control, { extraHelp } = {}) {
    return h('div', { class: `field w-${field.width ?? 'full'}`, dataset: { path } },
      field.type !== 'boolean' && h('label', { class: 'field-label', for: fieldId(path) }, field.label, field.required && h('span', { class: 'req', 'aria-hidden': 'true' }, '*')),
      control,
      (field.help || extraHelp) && h('div', { class: 'help' }, field.help || extraHelp),
    );
  }

  function renderField(field, path) {
    const value = getPath(draft, path);
    const idAttr = fieldId(path);

    switch (field.type) {
      case 'string': {
        const input = h('input', { id: idAttr, class: `input ${field.mono ? 'mono' : ''}`, type: 'text', value: value ?? '', placeholder: field.placeholder, maxlength: field.maxLength, autocomplete: 'off', spellcheck: field.mono ? 'false' : null });
        input.addEventListener('input', () => setValue(path, input.value));
        return wrapField(field, path, input);
      }
      case 'text': {
        const area = h('textarea', { id: idAttr, class: 'input', rows: field.rows ?? 3, placeholder: field.placeholder });
        area.value = value ?? '';
        area.addEventListener('input', () => setValue(path, area.value));
        return wrapField(field, path, area);
      }
      case 'number':
      case 'integer': {
        const input = h('input', { id: idAttr, class: 'input', type: 'number', step: field.step ?? (field.type === 'integer' ? 1 : 'any'), value: value ?? '', inputmode: 'decimal' });
        const badge = h('span', { class: 'fx-badge' });
        const paint = () => {
          const b = effectBadge(field, getPath(draft, path));
          badge.textContent = b.text;
          badge.className = `fx-badge ${b.tone}`;
          badge.classList.toggle('hidden', !b.text);
        };
        input.addEventListener('input', () => {
          setValue(path, input.value === '' ? null : Number(input.value));
          paint();
        });
        paint();
        const control = field.display ? h('div', { class: 'input-group' }, input, badge) : input;
        return wrapField(field, path, control);
      }
      case 'boolean': {
        const input = h('input', { id: idAttr, type: 'checkbox', checked: Boolean(value), role: 'switch' });
        input.addEventListener('change', () => setValue(path, input.checked, { rerender: field.rerender }));
        const control = h('label', { class: 'switch' }, input, h('span', { class: 'track' }), h('span', {}, field.label));
        return wrapField(field, path, control);
      }
      case 'enum': {
        const select = h('select', { id: idAttr, class: 'input' },
          ENUMS[field.enum].values.map((v) => h('option', { value: v.value, selected: v.value === value, title: v.value }, v.label)));
        select.addEventListener('change', () => setValue(path, select.value, { rerender: field.rerender }));
        return wrapField(field, path, select);
      }
      case 'enumList': {
        const selected = new Set(Array.isArray(value) ? value : []);
        const chips = h('div', { class: 'chips', id: idAttr, role: 'group' },
          ENUMS[field.enum].values.map((v) => {
            const cb = h('input', { type: 'checkbox', checked: selected.has(v.value) });
            cb.addEventListener('change', () => {
              const current = new Set(getPath(draft, path) ?? []);
              cb.checked ? current.add(v.value) : current.delete(v.value);
              // mantem a ordem do enum
              setValue(path, ENUMS[field.enum].values.map((x) => x.value).filter((x) => current.has(x)));
            });
            return h('label', { class: 'chip' }, cb, v.label);
          }));
        return wrapField(field, path, chips);
      }
      case 'object': {
        return h('div', { class: `field w-${field.width ?? 'full'}`, dataset: { path } },
          h('div', { class: 'subfields' },
            h('div', { class: 'subfields-title' }, field.label),
            h('div', { class: 'grid' }, field.fields.map((sub) => renderField(sub, joinPath(path, sub.key)))),
          ));
      }
      case 'list':
        return renderList(field, path);
      default:
        return h('div', {}, `Tipo de campo não suportado: ${field.type}`);
    }
  }

  function renderList(field, path) {
    const items = getPath(draft, path) ?? [];
    const atMax = field.maxItems !== undefined && items.length >= field.maxItems;
    const atMin = field.minItems !== undefined && items.length <= field.minItems;

    const move = (from, to) => {
      const arr = getPath(draft, path);
      const [moved] = arr.splice(from, 1);
      arr.splice(to, 0, moved);
      setValue(path, arr, { rerender: true });
    };

    const cards = items.map((item, index) => {
      const itemPath = joinPath(path, index);
      const titleNode = h('span', { class: 'list-item-title' });
      const updateTitle = () => { titleNode.textContent = field.itemLabel ? field.itemLabel(getPath(draft, itemPath) ?? {}, index) : `Item ${index + 1}`; };
      updateTitle();
      titleUpdaters.push(updateTitle);

      return h('div', { class: 'list-item', dataset: { path: itemPath } },
        h('div', { class: 'list-item-head' },
          titleNode,
          h('div', { class: 'row-actions' },
            field.reorderable && h('button', { type: 'button', class: 'icon-btn', title: 'Mover para cima', 'aria-label': 'Mover para cima', disabled: index === 0, onclick: () => move(index, index - 1) }, icon('up')),
            field.reorderable && h('button', { type: 'button', class: 'icon-btn', title: 'Mover para baixo', 'aria-label': 'Mover para baixo', disabled: index === items.length - 1, onclick: () => move(index, index + 1) }, icon('down')),
            h('button', {
              type: 'button', class: 'icon-btn danger', title: atMin ? `Mínimo de ${field.minItems}` : 'Remover', 'aria-label': 'Remover', disabled: atMin,
              onclick: () => { getPath(draft, path).splice(index, 1); setValue(path, getPath(draft, path), { rerender: true }); },
            }, icon('trash')),
          ),
        ),
        h('div', { class: 'list-item-body' },
          h('div', { class: 'grid' }, field.item.fields.map((sub) => renderField(sub, joinPath(itemPath, sub.key)))),
        ),
      );
    });

    return h('div', { class: `list-block w-${field.width ?? 'full'}`, dataset: { path } },
      h('div', { class: 'list-error', id: fieldId(path) }),
      cards.length ? h('div', { class: 'list-items' }, cards) : h('div', { class: 'muted', style: 'padding:4px 0' }, 'Nenhum item.'),
      h('div', { class: 'list-foot' },
        !(field.minItems !== undefined && field.minItems === field.maxItems && items.length === field.maxItems) &&
          h('button', {
            type: 'button', class: 'btn btn-sm', disabled: atMax,
            onclick: () => { const arr = getPath(draft, path) ?? []; arr.push(createEmptyObject(field.item.fields)); setValue(path, arr, { rerender: true }); },
          }, icon('plus'), field.addLabel ?? 'Adicionar'),
        field.help && h('span', { class: 'help' }, field.help),
      ),
    );
  }

  function renderForm() {
    const scrollY = window.scrollY;
    const activeId = document.activeElement?.id;
    titleUpdaters = [];

    mount(formCol, ...schema.sections.map((section) => {
      const visible = section.visibleWhen ? section.visibleWhen(draft) : true;
      if (!visible) {
        return h('section', { class: 'card section' },
          h('div', { class: 'section-head' }, h('h2', {}, section.title)),
          section.hiddenHint && h('div', { class: 'section-hint' }, section.hiddenHint));
      }
      return h('section', { class: 'card section' },
        h('div', { class: 'section-head' }, h('h2', {}, section.title)),
        h('div', { class: 'section-body' },
          h('div', { class: 'grid' }, section.fields.map((field) => renderField(field, field.key)))),
      );
    }));

    runValidation();
    window.scrollTo(0, scrollY);
    if (activeId) document.getElementById(activeId)?.focus({ preventScroll: true });
  }

  /* ---------- Validacao ---------- */
  function findTarget(path) {
    if (path == null) return null;
    let current = String(path);
    while (current) {
      const el = formCol.querySelector(`[data-path="${escapeSelector(current)}"]`);
      if (el) return el;
      current = current.includes('.') ? current.slice(0, current.lastIndexOf('.')) : '';
    }
    return null;
  }

  function currentIssues() {
    const item = normalizeItem(schema, draft);
    return validateItem(schema, item, { items: listData.items, originalId });
  }

  function focusIssue(target) {
    if (!target) return;
    target.scrollIntoView({ behavior: 'smooth', block: 'center' });
    target.classList.remove('flash');
    void target.offsetWidth;
    target.classList.add('flash');
    const focusable = target.querySelector('input, select, textarea, button:not([disabled])');
    setTimeout(() => focusable?.focus({ preventScroll: true }), 300);
  }

  function runValidation(issues = currentIssues()) {
    formCol.querySelectorAll('.has-error').forEach((el) => el.classList.remove('has-error'));
    formCol.querySelectorAll('.field > .error-msg, .field > .warn-msg').forEach((el) => el.remove());
    formCol.querySelectorAll('.list-error').forEach((el) => { el.textContent = ''; });

    for (const iss of issues) {
      const target = findTarget(iss.path);
      if (!target) continue;
      if (target.classList.contains('list-block')) {
        if (iss.level === 'error') {
          target.classList.add('has-error');
          const box = target.querySelector(':scope > .list-error');
          box.textContent = box.textContent ? `${box.textContent} ${iss.message}` : iss.message;
        }
      } else if (target.classList.contains('field')) {
        if (iss.level === 'error') target.classList.add('has-error');
        target.append(h('div', { class: iss.level === 'error' ? 'error-msg' : 'warn-msg' }, iss.message));
      }
    }

    if (issues.length === 0) {
      mount(issuesBox, h('div', { class: 'all-good' }, icon('check'), 'Tudo certo para salvar.'));
    } else {
      const sorted = [...issues].sort((a, b) => (a.level === b.level ? 0 : a.level === 'error' ? -1 : 1));
      mount(issuesBox, h('ul', { class: 'issues' }, sorted.map((iss) =>
        h('li', { class: iss.level },
          h('button', { type: 'button', onclick: () => focusIssue(findTarget(iss.path)) },
            h('span', { class: 'dot' }), h('span', {}, iss.message))))));
    }
    return issues;
  }

  /* ---------- Acoes ---------- */
  async function save() {
    if (saving) return;
    const issues = runValidation();
    if (hasErrors(issues)) {
      toast('Corrija os erros destacados antes de salvar.', 'error');
      focusIssue(findTarget(issues.find((i) => i.level === 'error').path));
      return;
    }

    saving = true;
    saveBtn.disabled = true;
    try {
      const payload = normalizeItem(schema, draft);
      const result = mode === 'edit'
        ? await api.update(schema.name, originalId, payload)
        : await api.create(schema.name, payload);
      dirty = false;
      const newId = result.item[schema.idField];
      toast(`"${newId}" salvo.`, 'success');
      refreshNav();
      navigate(`#/${schema.name}/edit/${encodeURIComponent(newId)}`, { force: true });
    } catch (err) {
      if (err.issues?.length) runValidation(err.issues);
      toast(err.message, 'error', 6000);
    } finally {
      saving = false;
      saveBtn.disabled = false;
    }
  }

  async function removeItem() {
    const ok = await confirmDialog({
      title: `Excluir ${schema.singular}`,
      message: `Excluir "${originalId}"? No Unity, o importador vai perguntar se o asset correspondente deve ser removido.`,
      confirmLabel: 'Excluir',
      danger: true,
    });
    if (!ok) return;
    try {
      await api.remove(schema.name, originalId);
      dirty = false;
      toast(`"${originalId}" excluído.`, 'success');
      refreshNav();
      navigate(`#/${schema.name}`, { force: true });
    } catch (err) {
      toast(err.message, 'error', 6000);
    }
  }

  const onKey = (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
      e.preventDefault();
      save();
    }
  };
  document.addEventListener('keydown', onKey);
  ctx.onLeave(() => document.removeEventListener('keydown', onKey));

  mount(container, head, editor);
  updateHeader();
  updateAssetPath();
  renderForm();
}
