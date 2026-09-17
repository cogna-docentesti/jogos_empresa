import express from 'express';
import { COLLECTIONS, PLANNED } from '../../shared/collections/index.js';

/** Adaptador HTTP: traduz requisicoes REST em chamadas ao CollectionService. */
export function collectionRoutes(service, storageInfo) {
  const router = express.Router();

  router.get('/meta', (_req, res) => {
    res.json({
      storage: storageInfo,
      collections: COLLECTIONS.map((c) => ({ name: c.name, label: c.label })),
      planned: PLANNED,
    });
  });

  router.get('/collections/:name', async (req, res) => {
    res.json(await service.list(req.params.name));
  });

  router.get('/collections/:name/export', async (req, res) => {
    const { report, ...doc } = await service.list(req.params.name);
    res.set('Content-Disposition', `attachment; filename="${req.params.name}.json"`);
    res.type('application/json').send(JSON.stringify(doc, null, 2) + '\n');
  });

  router.post('/collections/:name/import', async (req, res) => {
    res.json(await service.replaceAll(req.params.name, req.body));
  });

  router.get('/collections/:name/items/:id', async (req, res) => {
    res.json(await service.get(req.params.name, req.params.id));
  });

  router.post('/collections/:name/items', async (req, res) => {
    res.status(201).json(await service.create(req.params.name, req.body));
  });

  router.put('/collections/:name/items/:id', async (req, res) => {
    res.json(await service.update(req.params.name, req.params.id, req.body));
  });

  router.delete('/collections/:name/items/:id', async (req, res) => {
    res.json(await service.remove(req.params.name, req.params.id));
  });

  return router;
}
