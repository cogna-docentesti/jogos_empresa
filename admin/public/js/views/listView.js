import { api } from '../api.js';
import { h, mount, icon, toast, confirmDialog } from '../ui.js';
import { enumLabel, ENUMS } from '/shared/enums.js';

const filtersByCollection = new Map();

function getFilters(name) {
  if (!filtersByCollection.has(name)) filtersByCollection.set(name, { q: '', onlyIssues: false });
  return filtersByCollection.get(name);
}

function cellValue(col, item) {
  const raw = col.value ? col.value(item) : item[col.key];
  if (col.enum) {
    const tone = col.badge?.[raw] ?? 'gray';
    return h('span', { class: `badge ${tone}` }, enumLabel(col.enum, raw));
  }
  if (typeof raw === 'number') return raw.toLocaleString('pt-BR');
  return raw ?? '';
}

function statusCell(counts) {
  if (!counts || (counts.errors === 0 && counts.warnings === 0)) {
    return h('span', { class: 'status-ok', title: 'Sem pendências' }, icon('check'), h('span', { class: 'hide-sm' }, 'OK'));
  }
  return h('span', { class: 'actions', style: 'gap:4px' },
    counts.errors > 0 && h('span', { class: 'badge red' }, `${counts.errors} erro${counts.errors > 1 ? 's' : ''}`),
    counts.warnings > 0 && h('span', { class: 'badge amber' }, `${counts.warnings} aviso${counts.warnings > 1 ? 's' : ''}`),
  );
}

function readJsonFile(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      try { resolve(JSON.parse(reader.result)); } catch { reject(new Error('O arquivo não é um JSON válido.')); }
    };
    reader.onerror = () => reject(new Error('Não foi possível ler o arquivo.'));
    reader.readAsText(file, 'utf-8');
  });
}

/**
 * Tela de listagem generica de uma colecao.
 * @param ctx { schema, container, navigate, refreshNav }
 */
export async function renderListView({ schema, container, navigate, refreshNav }) {
  mount(container, h('div', { class: 'loading' }, 'Carregando...'));

  let data;
  try {
    data = await api.list(schema.name);
  } catch (err) {
    mount(container, h('div', { class: 'card empty' }, `Não foi possível carregar: ${err.message}`));
    return;
  }

  const filters = getFilters(schema.name);
  const items = data.items;
  const perItem = data.report?.perItem ?? {};

  /* ---------- Cabecalho ---------- */
  const fileInput = h('input', { type: 'file', accept: '.json,application/json', class: 'hidden' });
  fileInput.addEventListener('change', async () => {
    const file = fileInput.files?.[0];
    fileInput.value = '';
    if (!file) return;
    try {
      const doc = await readJsonFile(file);
      const count = Array.isArray(doc) ? doc.length : doc?.items?.length ?? 0;
      const ok = await confirmDialog({
        title: `Importar ${schema.label.toLowerCase()}`,
        message: `O arquivo "${file.name}" tem ${count} item(ns). Todos os ${items.length} itens atuais serão substituídos. Deseja continuar?`,
        confirmLabel: 'Substituir tudo',
        danger: true,
      });
      if (!ok) return;
      const result = await api.importAll(schema.name, doc);
      toast(`${result.count} item(ns) importado(s).`, 'success');
      refreshNav();
      renderListView({ schema, container, navigate, refreshNav });
    } catch (err) {
      if (err.issues?.length) {
        await confirmDialog({
          title: 'Importação cancelada',
          message: err.message,
          details: err.issues.filter((i) => i.level === 'error').slice(0, 30).map((i) => `${i.item ?? ''}: ${i.message}`),
          confirmLabel: 'Entendi',
          cancelLabel: null,
        });
      } else {
        toast(err.message, 'error', 6000);
      }
    }
  });

  const head = h('div', { class: 'page-head' },
    h('div', {},
      h('h1', {}, schema.label),
      h('p', {}, schema.description),
    ),
    h('div', { class: 'actions' },
      h('button', { class: 'btn', onclick: () => fileInput.click(), title: 'Substitui a coleção pelo conteúdo de um arquivo JSON' }, icon('upload'), 'Importar JSON'),
      h('a', { class: 'btn', href: api.exportUrl(schema.name), download: `${schema.name}.json` }, icon('download'), 'Exportar JSON'),
      h('button', { class: 'btn btn-primary', onclick: () => navigate(`#/${schema.name}/new`) }, icon('plus'), `Novo ${schema.singular}`),
      fileInput,
    ),
  );

  /* ---------- Resumo ---------- */
  const summary = typeof schema.summary === 'function'
    ? h('div', { class: 'summary' }, schema.summary(items).map((s) =>
        h('div', { class: `card stat ${s.expected !== undefined && s.value !== s.expected ? 'off' : ''}`, title: s.expected !== undefined ? `Esperado pelo EventAssetGenerator: ${s.expected}` : '' },
          h('div', { class: 'stat-label' }, s.label),
          h('div', { class: 'stat-value' }, s.value, s.expected !== undefined && h('small', {}, ` / ${s.expected}`)),
        )))
    : null;

  /* ---------- Filtros ---------- */
  const searchInput = h('input', { class: 'input', type: 'search', placeholder: 'Buscar por ID, título ou descrição', value: filters.q, 'aria-label': 'Buscar' });
  const filterSelects = (schema.filters ?? []).map((f) => {
    const select = h('select', { class: 'input', style: 'width:auto', 'aria-label': f.label },
      h('option', { value: '' }, `${f.label}: todos`),
      ENUMS[f.enum].values.map((v) => h('option', { value: v.value, selected: filters[f.key] === v.value }, v.label)),
    );
    select.addEventListener('change', () => { filters[f.key] = select.value; renderRows(); });
    return select;
  });
  const onlyIssues = h('input', { type: 'checkbox', checked: filters.onlyIssues });
  onlyIssues.addEventListener('change', () => { filters.onlyIssues = onlyIssues.checked; renderRows(); });
  searchInput.addEventListener('input', () => { filters.q = searchInput.value; renderRows(); });

  const toolbar = h('div', { class: 'toolbar' },
    h('div', { class: 'search' }, icon('search'), searchInput),
    filterSelects,
    h('label', { class: 'check-inline' }, onlyIssues, 'Somente com pendências'),
  );

  /* ---------- Tabela ---------- */
  const tbody = h('tbody');
  const countLabel = h('div', { class: 'muted', style: 'padding:10px 12px; font-size:12px; border-top:1px solid var(--border)' });

  async function removeItem(item) {
    const id = item[schema.idField];
    const ok = await confirmDialog({
      title: `Excluir ${schema.singular}`,
      message: `Excluir "${item[schema.titleField] || id}" (${id})? No Unity, o importador vai perguntar se o asset correspondente deve ser removido.`,
      confirmLabel: 'Excluir',
      danger: true,
    });
    if (!ok) return;
    try {
      await api.remove(schema.name, id);
      toast(`"${id}" excluído.`, 'success');
      refreshNav();
      renderListView({ schema, container, navigate, refreshNav });
    } catch (err) {
      toast(err.message, 'error', 6000);
    }
  }

  function renderRows() {
    const q = filters.q.trim().toLowerCase();
    const visible = items.filter((item) => {
      if (q && !(schema.searchKeys ?? [schema.idField]).some((k) => String(item[k] ?? '').toLowerCase().includes(q))) return false;
      for (const f of schema.filters ?? []) {
        if (filters[f.key] && item[f.key] !== filters[f.key]) return false;
      }
      if (filters.onlyIssues) {
        const c = perItem[item[schema.idField]];
        if (!c || (c.errors === 0 && c.warnings === 0)) return false;
      }
      return true;
    });

    if (visible.length === 0) {
      mount(tbody, h('tr', {}, h('td', { colspan: schema.columns.length + 2, class: 'empty' },
        items.length === 0 ? `Nenhum ${schema.singular} cadastrado ainda.` : 'Nenhum resultado para os filtros aplicados.')));
    } else {
      mount(tbody, ...visible.map((item) => {
        const id = item[schema.idField];
        const open = () => navigate(`#/${schema.name}/edit/${encodeURIComponent(id)}`);
        return h('tr', { tabindex: '0', onclick: open, onkeydown: (e) => { if (e.key === 'Enter') open(); } },
          schema.columns.map((col) => h('td', { class: [col.mono && 'mono', col.align && `align-${col.align}`].filter(Boolean).join(' ') }, cellValue(col, item))),
          h('td', {}, statusCell(perItem[id])),
          h('td', {},
            h('div', { class: 'row-actions' },
              h('button', { class: 'icon-btn', title: 'Duplicar', 'aria-label': `Duplicar ${id}`, onclick: (e) => { e.stopPropagation(); navigate(`#/${schema.name}/new?from=${encodeURIComponent(id)}`); } }, icon('copy')),
              h('button', { class: 'icon-btn danger', title: 'Excluir', 'aria-label': `Excluir ${id}`, onclick: (e) => { e.stopPropagation(); removeItem(item); } }, icon('trash')),
            )),
        );
      }));
    }
    countLabel.textContent = `Mostrando ${visible.length} de ${items.length}${data.updatedAt ? ` · última alteração em ${new Date(data.updatedAt).toLocaleString('pt-BR')}` : ''}`;
  }

  const table = h('div', { class: 'card' },
    toolbar,
    h('div', { class: 'table-wrap' },
      h('table', { class: 'data' },
        h('thead', {}, h('tr', {},
          schema.columns.map((col) => h('th', { class: col.align ? `align-${col.align}` : '' }, col.label)),
          h('th', {}, 'Validação'),
          h('th', { class: 'align-right' }, h('span', { class: 'hide-sm' }, 'Ações')),
        )),
        tbody,
      ),
    ),
    countLabel,
  );

  renderRows();
  mount(container, head, summary, table);
}
