import path from 'node:path';
import express from 'express';
import { config, assertConfig, ADMIN_ROOT } from './config.js';
import { basicAuth } from './auth.js';
import { createStorage } from './storage/index.js';
import { CollectionService } from './application/collectionService.js';
import { collectionRoutes } from './http/collectionRoutes.js';
import { HttpError } from './errors.js';

assertConfig();

// Composicao: storage (adaptador de saida) -> servico (aplicacao) -> rotas (adaptador de entrada)
const storage = createStorage(config);
const service = new CollectionService(storage);

const app = express();
app.disable('x-powered-by');

// Health check sem senha (usado pelo servidor de hospedagem)
app.get('/healthz', (_req, res) => res.json({ ok: true }));

app.use(basicAuth(config.auth));
app.use(express.json({ limit: '5mb' }));

app.use('/api', collectionRoutes(service, storage.describe()));
app.use('/shared', express.static(path.join(ADMIN_ROOT, 'shared')));
app.use(express.static(path.join(ADMIN_ROOT, 'public')));

app.use('/api', (_req, res) => res.status(404).json({ message: 'Rota não encontrada.' }));

// Tratamento central de erros
app.use((err, _req, res, _next) => {
  if (err instanceof HttpError) {
    return res.status(err.status).json({ message: err.message, ...err.details });
  }
  if (err.type === 'entity.parse.failed') {
    return res.status(400).json({ message: 'JSON inválido.' });
  }
  console.error(err);
  res.status(500).json({ message: 'Erro interno no servidor.', detail: err.message });
});

app.listen(config.port, () => {
  const where = storage.describe();
  console.log(`\nAdmin rodando em http://localhost:${config.port}`);
  console.log(`Armazenamento: ${where.type} -> ${where.location}\n`);
});
