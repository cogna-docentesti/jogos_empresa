import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
export const ADMIN_ROOT = path.resolve(here, '..');

// Carrega o arquivo .env (se existir) sem precisar de biblioteca externa.
const envFile = path.join(ADMIN_ROOT, '.env');
if (fs.existsSync(envFile)) process.loadEnvFile(envFile);

const env = process.env;

export const config = {
  port: Number(env.PORT) || 3000,

  auth: {
    user: env.ADMIN_USER || 'admin',
    password: env.ADMIN_PASSWORD || '',
  },

  storage: (env.STORAGE || 'file').toLowerCase(),

  file: {
    // Padrao: <projeto Unity>/admin-data, a mesma pasta que o importador do Unity le.
    dataDir: path.resolve(ADMIN_ROOT, env.DATA_DIR || '../admin-data'),
  },

  github: {
    token: env.GITHUB_TOKEN || '',
    repo: env.GITHUB_REPO || '',
    branch: env.GITHUB_BRANCH || 'main',
    dataDir: (env.GITHUB_DATA_DIR || 'admin-data').replace(/^\/+|\/+$/g, ''),
  },
};

export function assertConfig() {
  const problems = [];

  if (!['file', 'github'].includes(config.storage)) {
    problems.push(`STORAGE deve ser "file" ou "github" (recebido "${config.storage}").`);
  }
  if (config.storage === 'github') {
    if (!config.github.token) problems.push('GITHUB_TOKEN não definido.');
    if (!/^[\w.-]+\/[\w.-]+$/.test(config.github.repo)) problems.push('GITHUB_REPO deve estar no formato "dono/repositorio".');
    if (!config.auth.password) problems.push('ADMIN_PASSWORD é obrigatório quando o admin está publicado (STORAGE=github).');
  }

  if (problems.length) {
    console.error('\n[config] Não foi possível iniciar o admin:\n - ' + problems.join('\n - ') + '\n');
    process.exit(1);
  }
  if (!config.auth.password) {
    console.warn('[config] ADMIN_PASSWORD vazio: o painel está SEM senha. Use apenas no seu computador.');
  }
}
