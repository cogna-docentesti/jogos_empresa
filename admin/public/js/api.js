/** Cliente HTTP da API do admin. */
async function request(method, url, body) {
  const res = await fetch(url, {
    method,
    headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let data = null;
  try { data = text ? JSON.parse(text) : null; } catch { data = { message: text }; }

  if (!res.ok) {
    const err = new Error(data?.message || `Erro ${res.status}`);
    err.status = res.status;
    err.issues = data?.issues || [];
    throw err;
  }
  return data;
}

const base = (name) => `/api/collections/${encodeURIComponent(name)}`;

export const api = {
  meta: () => request('GET', '/api/meta'),
  list: (name) => request('GET', base(name)),
  get: (name, id) => request('GET', `${base(name)}/items/${encodeURIComponent(id)}`),
  create: (name, item) => request('POST', `${base(name)}/items`, item),
  update: (name, id, item) => request('PUT', `${base(name)}/items/${encodeURIComponent(id)}`, item),
  remove: (name, id) => request('DELETE', `${base(name)}/items/${encodeURIComponent(id)}`),
  importAll: (name, doc) => request('POST', `${base(name)}/import`, doc),
  exportUrl: (name) => `${base(name)}/export`,
};
