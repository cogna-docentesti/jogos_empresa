import { api } from './api.js';
import { h, mount, confirmDialog } from './ui.js';
import { COLLECTIONS, PLANNED, getCollection } from '/shared/collections/index.js';
import { renderListView } from './views/listView.js';
import { renderFormView } from './views/formView.js';

/**
 * Roteador baseado em hash:
 *   #/<colecao>                  listagem
 *   #/<colecao>/new              novo item
 *   #/<colecao>/new?from=<id>    duplicar item
 *   #/<colecao>/edit/<id>        editar item
 */
const view = document.getElementById('view');
const nav = document.getElementById('nav');
const breadcrumb = document.getElementById('breadcrumb');
const sidebar = document.getElementById('sidebar');

let guard = () => false;       // retorna true quando ha alteracoes nao salvas
let leaveHandlers = [];
let lastHash = location.hash;
let skipNextHashChange = false;

function parseRoute(hash) {
  const [pathPart, query = ''] = hash.replace(/^#\/?/, '').split('?');
  const parts = pathPart.split('/').filter(Boolean).map(decodeURIComponent);
  const params = new URLSearchParams(query);
  return { collection: parts[0], action: parts[1] ?? 'list', id: parts[2], from: params.get('from') };
}

export function navigate(hash, { force = false } = {}) {
  if (force) guard = () => false;
  if (location.hash === hash) route();
  else location.hash = hash;
}

async function refreshNav() {
  let meta = null;
  const counts = {};
  try {
    meta = await api.meta();
    await Promise.all(COLLECTIONS.map(async (c) => {
      try { counts[c.name] = (await api.list(c.name)).items.length; } catch { counts[c.name] = '?'; }
    }));
  } catch { /* segue sem contagem */ }

  const { collection } = parseRoute(location.hash);
  mount(nav, 
    h('div', { class: 'nav-group' }, 'Catálogos'),
    COLLECTIONS.map((c) => h('a', { class: `nav-link ${c.name === collection ? 'active' : ''}`, href: `#/${c.name}` },
      h('span', {}, c.label), h('span', { class: 'count' }, counts[c.name] ?? ''))),
    h('div', { class: 'nav-group' }, 'Próximos cadastros'),
    PLANNED.map((p) => h('span', { class: 'nav-link disabled', title: p.unity ? `ScriptableObject ${p.unity}` : '' },
      h('span', {}, p.label), h('span', { class: 'soon' }, 'em breve'))),
  );

  if (meta?.storage) {
    mount(document.getElementById('storage-info'), 
      h('div', {}, 'Dados salvos em'),
      h('strong', {}, meta.storage.type === 'github' ? 'GitHub' : 'Arquivo local'),
      h('div', { class: 'mono', style: 'font-size:11px;margin-top:2px' }, meta.storage.location),
    );
  }
}

function setBreadcrumb(schema, label) {
  mount(breadcrumb, 
    h('a', { href: `#/${schema.name}` }, schema.label),
    label && h('span', {}, '/'),
    label && h('span', { class: 'current' }, label),
  );
}

async function route() {
  leaveHandlers.forEach((fn) => fn());
  leaveHandlers = [];
  guard = () => false;
  sidebar.classList.remove('open');

  const { collection, action, id, from } = parseRoute(location.hash);
  const schema = getCollection(collection);
  if (!schema) {
    location.replace(`#/${COLLECTIONS[0].name}`);
    return;
  }

  nav.querySelectorAll('.nav-link').forEach((a) => a.classList.toggle('active', a.getAttribute('href') === `#/${schema.name}`));
  document.title = `${schema.label} · Admin Jogo de Empresas`;

  const ctx = {
    schema,
    container: view,
    navigate,
    refreshNav,
    setGuard: (fn) => { guard = fn; },
    onLeave: (fn) => leaveHandlers.push(fn),
  };

  if (action === 'new') {
    setBreadcrumb(schema, `Novo ${schema.singular}`);
    await renderFormView({ ...ctx, mode: 'new', fromId: from });
  } else if (action === 'edit' && id) {
    setBreadcrumb(schema, id);
    await renderFormView({ ...ctx, mode: 'edit', id });
  } else {
    setBreadcrumb(schema, null);
    await renderListView(ctx);
  }
  view.focus({ preventScroll: true });
}

window.addEventListener('hashchange', async () => {
  if (skipNextHashChange) { skipNextHashChange = false; return; }
  if (guard()) {
    const target = location.hash;
    skipNextHashChange = true;
    history.replaceState(null, '', lastHash);   // volta a URL enquanto pergunta
    skipNextHashChange = false;
    const leave = await confirmDialog({
      title: 'Descartar alterações?',
      message: 'Existem alterações que ainda não foram salvas.',
      confirmLabel: 'Descartar',
      cancelLabel: 'Continuar editando',
      danger: true,
    });
    if (!leave) return;
    guard = () => false;
    history.replaceState(null, '', target);
  }
  lastHash = location.hash;
  route();
});

window.addEventListener('beforeunload', (e) => {
  if (guard()) { e.preventDefault(); e.returnValue = ''; }
});

document.getElementById('menu-toggle').addEventListener('click', () => sidebar.classList.toggle('open'));

refreshNav();
if (!location.hash) history.replaceState(null, '', `#/${COLLECTIONS[0].name}`);
lastHash = location.hash;
route();
