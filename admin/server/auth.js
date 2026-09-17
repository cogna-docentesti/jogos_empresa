import crypto from 'node:crypto';

function safeEqual(a, b) {
  const bufA = Buffer.from(String(a));
  const bufB = Buffer.from(String(b));
  return bufA.length === bufB.length && crypto.timingSafeEqual(bufA, bufB);
}

/**
 * Autenticacao basica (HTTP Basic Auth): o navegador mostra a janela de login.
 * Simples e suficiente para um painel interno servido por HTTPS.
 */
export function basicAuth({ user, password }) {
  if (!password) return (_req, _res, next) => next();

  return (req, res, next) => {
    const header = req.headers.authorization || '';
    const [scheme, encoded] = header.split(' ');
    if (scheme === 'Basic' && encoded) {
      const decoded = Buffer.from(encoded, 'base64').toString('utf8');
      const sep = decoded.indexOf(':');
      const u = decoded.slice(0, sep);
      const p = decoded.slice(sep + 1);
      if (sep >= 0 && safeEqual(u, user) && safeEqual(p, password)) return next();
    }
    res.set('WWW-Authenticate', 'Basic realm="Admin Jogo de Empresas", charset="UTF-8"');
    res.status(401).send('Autenticação necessária.');
  };
}
