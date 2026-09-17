import fs from 'node:fs/promises';
import path from 'node:path';

/**
 * Adaptador de armazenamento em arquivos JSON locais.
 * Cada colecao vira um arquivo: <dataDir>/<colecao>.json
 */
export class FileStorage {
  constructor({ dataDir }) {
    this.dataDir = dataDir;
    this.cacheTtlMs = 0; // disco local: sempre le o arquivo atualizado
  }

  describe() {
    return { type: 'file', location: this.dataDir };
  }

  #file(collection) {
    return path.join(this.dataDir, `${collection}.json`);
  }

  async read(collection) {
    try {
      const text = await fs.readFile(this.#file(collection), 'utf8');
      return JSON.parse(text);
    } catch (err) {
      if (err.code === 'ENOENT') return null;
      throw err;
    }
  }

  async write(collection, doc) {
    await fs.mkdir(this.dataDir, { recursive: true });
    const target = this.#file(collection);
    const temp = `${target}.tmp`;
    // grava em arquivo temporario e renomeia: evita JSON corrompido se o processo cair no meio
    await fs.writeFile(temp, JSON.stringify(doc, null, 2) + '\n', 'utf8');
    await fs.rename(temp, target);
  }
}
