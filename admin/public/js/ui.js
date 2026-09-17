/** Utilitarios de interface: criacao de elementos, icones, toasts e modais. */

export function h(tag, attrs = {}, ...children) {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs ?? {})) {
    if (value == null || value === false) continue;
    if (key === 'class') el.className = value;
    else if (key === 'dataset') Object.assign(el.dataset, value);
    else if (key.startsWith('on') && typeof value === 'function') el.addEventListener(key.slice(2).toLowerCase(), value);
    else if (key === 'html') el.innerHTML = value;
    else if (value === true) el.setAttribute(key, '');
    else el.setAttribute(key, value);
  }
  for (const child of children.flat(Infinity)) {
    if (child == null || child === false) continue;
    el.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return el;
}

const PATHS = {
  plus: '<path d="M12 5v14M5 12h14"/>',
  download: '<path d="M12 4v11M7 10l5 5 5-5M5 20h14"/>',
  upload: '<path d="M12 20V9M7 14l5-5 5 5M5 4h14"/>',
  search: '<circle cx="11" cy="11" r="6.5"/><path d="M20 20l-4-4"/>',
  copy: '<rect x="9" y="9" width="11" height="11" rx="2"/><path d="M5 15V5a1 1 0 0 1 1-1h9"/>',
  trash: '<path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3"/>',
  up: '<path d="M12 19V5M6 11l6-6 6 6"/>',
  down: '<path d="M12 5v14M6 13l6 6 6-6"/>',
  check: '<path d="M5 12.5l4.5 4.5L19 7.5"/>',
  back: '<path d="M19 12H5M11 6l-6 6 6 6"/>',
  save: '<path d="M5 4h11l3 3v13H5zM8 4v5h8V4M8 20v-6h8v6"/>',
};

export function icon(name) {
  const span = document.createElement('span');
  span.style.display = 'contents';
  span.innerHTML = `<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${PATHS[name] ?? ''}</svg>`;
  return span.firstChild;
}

export function toast(message, type = 'info', ms = 3500) {
  const el = h('div', { class: `toast ${type}`, role: type === 'error' ? 'alert' : 'status' }, message);
  document.getElementById('toasts').append(el);
  setTimeout(() => el.remove(), ms);
}

/**
 * Modal de confirmacao. Retorna uma Promise<boolean>.
 * @param opts { title, message, confirmLabel, danger, details: string[] }
 */
export function confirmDialog({ title, message, confirmLabel = 'Confirmar', cancelLabel = 'Cancelar', danger = false, details = [] }) {
  return new Promise((resolve) => {
    const previous = document.activeElement;
    const close = (result) => {
      backdrop.remove();
      document.removeEventListener('keydown', onKey);
      previous?.focus?.();
      resolve(result);
    };
    const onKey = (e) => { if (e.key === 'Escape') close(false); };

    const confirmBtn = h('button', { class: `btn ${danger ? 'btn-danger-solid' : 'btn-primary'}`, onclick: () => close(true) }, confirmLabel);
    const backdrop = h('div', { class: 'modal-backdrop', onclick: (e) => { if (e.target === backdrop) close(false); } },
      h('div', { class: 'modal card', role: 'dialog', 'aria-modal': 'true', 'aria-labelledby': 'modal-title' },
        h('h2', { id: 'modal-title' }, title),
        message && h('p', {}, message),
        details.length > 0 && h('ul', {}, details.map((d) => h('li', {}, d))),
        h('div', { class: 'actions' },
          cancelLabel && h('button', { class: 'btn', onclick: () => close(false) }, cancelLabel),
          confirmBtn,
        ),
      ),
    );
    document.body.append(backdrop);
    document.addEventListener('keydown', onKey);
    confirmBtn.focus();
  });
}

export function formatNumber(value, options = {}) {
  if (typeof value !== 'number' || !Number.isFinite(value)) return '';
  return value.toLocaleString('pt-BR', options);
}

export function escapeSelector(value) {
  return window.CSS?.escape ? CSS.escape(value) : String(value).replace(/"/g, '\\"');
}

/** Substitui o conteudo de um elemento. Aceita listas aninhadas e ignora null/false. */
export function mount(el, ...children) {
  const nodes = children.flat(Infinity)
    .filter((c) => c != null && c !== false)
    .map((c) => (c instanceof Node ? c : document.createTextNode(String(c))));
  el.replaceChildren(...nodes);
  return el;
}
