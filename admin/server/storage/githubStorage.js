import { StorageConflictError } from '../errors.js';

const API = process.env.GITHUB_API_URL || 'https://api.github.com';

/**
 * Adaptador de armazenamento que grava os JSON dentro de um repositorio GitHub
 * (API "Contents"). Cada gravacao vira um commit.
 *
 * Por que isso? Servidores gratuitos (ex.: Render Free) apagam o disco a cada
 * reinicio. Gravando no GitHub, os dados ficam persistidos, versionados e ja
 * disponiveis no repositorio para o importador do Unity.
 */
export class GitHubStorage {
  constructor({ token, repo, branch, dataDir }) {
    this.token = token;
    this.repo = repo;
    this.branch = branch;
    this.dataDir = dataDir;
    this.cacheTtlMs = 30_000; // evita uma chamada a API a cada clique
    this.shas = new Map(); // sha atual de cada arquivo (controle de concorrencia)
  }

  describe() {
    return { type: 'github', location: `${this.repo}@${this.branch}/${this.dataDir}` };
  }

  #path(collection) {
    return `${this.dataDir}/${collection}.json`;
  }

  async #request(method, url, body) {
    const res = await fetch(`${API}${url}`, {
      method,
      headers: {
        Authorization: `Bearer ${this.token}`,
        Accept: 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28',
        'User-Agent': 'jogos-empresa-admin',
        ...(body ? { 'Content-Type': 'application/json' } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
    });
    return res;
  }

  async read(collection) {
    const filePath = this.#path(collection);
    const res = await this.#request(
      'GET',
      `/repos/${this.repo}/contents/${encodeURI(filePath)}?ref=${encodeURIComponent(this.branch)}`,
    );

    if (res.status === 404) {
      this.shas.delete(collection);
      return null;
    }
    if (!res.ok) throw new Error(`GitHub respondeu ${res.status} ao ler ${filePath}: ${await res.text()}`);

    const data = await res.json();
    this.shas.set(collection, data.sha);
    return JSON.parse(Buffer.from(data.content, 'base64').toString('utf8'));
  }

  async write(collection, doc, message) {
    const filePath = this.#path(collection);
    const body = {
      message: message || `admin: atualiza ${filePath}`,
      content: Buffer.from(JSON.stringify(doc, null, 2) + '\n', 'utf8').toString('base64'),
      branch: this.branch,
    };
    const sha = this.shas.get(collection);
    if (sha) body.sha = sha;

    const res = await this.#request('PUT', `/repos/${this.repo}/contents/${encodeURI(filePath)}`, body);

    // 409/422: o arquivo mudou no GitHub desde a ultima leitura
    if (res.status === 409 || (res.status === 422 && /sha/i.test(await res.clone().text()))) {
      throw new StorageConflictError(`O arquivo ${filePath} foi alterado por outra gravação.`);
    }
    if (!res.ok) throw new Error(`GitHub respondeu ${res.status} ao gravar ${filePath}: ${await res.text()}`);

    const data = await res.json();
    this.shas.set(collection, data.content.sha);
  }
}
