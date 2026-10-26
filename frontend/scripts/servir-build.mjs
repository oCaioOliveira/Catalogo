// Serve o BUILD do front (dist/catalogo-frontend/browser) com o mesmo proxy do `npm start`: /api/... vai para a API local.
// Usado pelos E2E (playwright.config.ts → webServer): cada rodada testa exatamente o código compilado agora, sem depender do
// watcher do `ng serve` (que, no WSL, às vezes perde alterações e serve código antigo). Sem dependências: só o Node.
//   PORTA=4300 CATALOGO_API=http://localhost:5080 node scripts/servir-build.mjs
import { createReadStream, existsSync, statSync } from 'node:fs';
import { createServer, request } from 'node:http';
import { extname, join, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';

const raiz = fileURLToPath(new URL('../dist/catalogo-frontend/browser/', import.meta.url));
const porta = Number(process.env.PORTA ?? 4300);
const api = new URL(process.env.CATALOGO_API ?? 'http://localhost:5080');

const tipos = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
  '.woff': 'font/woff',
  '.woff2': 'font/woff2',
  '.ttf': 'font/ttf',
  '.eot': 'application/vnd.ms-fontobject',
};

if (!existsSync(join(raiz, 'index.html'))) {
  console.error(`Build não encontrado em ${raiz}. Rode "npm run build" antes.`);
  process.exit(1);
}

/** /api/... vai para a API, com o caminho, o método, os cabeçalhos e o corpo como vieram. */
function encaminharParaApi(pedido, resposta) {
  const saida = request(
    {
      hostname: api.hostname,
      port: api.port,
      path: pedido.url,
      method: pedido.method,
      headers: { ...pedido.headers, host: api.host },
    },
    (daApi) => {
      resposta.writeHead(daApi.statusCode ?? 502, daApi.headers);
      daApi.pipe(resposta);
    },
  );
  saida.on('error', () => {
    resposta.writeHead(502, { 'Content-Type': 'text/plain; charset=utf-8' });
    resposta.end(`API fora do ar em ${api.origin}.`);
  });
  pedido.pipe(saida);
}

/** Arquivo do build, se existir; qualquer outra rota é do Angular (index.html), como no `ng serve`. */
function servirArquivo(pedido, resposta) {
  const caminho = decodeURIComponent(new URL(pedido.url, 'http://local').pathname);
  const arquivo = normalize(join(raiz, caminho));
  const existe = arquivo.startsWith(raiz) && existsSync(arquivo) && statSync(arquivo).isFile();
  const alvo = existe ? arquivo : join(raiz, 'index.html');
  resposta.writeHead(200, {
    'Content-Type': tipos[extname(alvo)] ?? 'application/octet-stream',
    // index.html nunca em cache: um build novo vale na hora; os demais têm hash no nome.
    'Cache-Control': existe && !alvo.endsWith('index.html') ? 'public, max-age=31536000, immutable' : 'no-store',
  });
  createReadStream(alvo).pipe(resposta);
}

createServer((pedido, resposta) => {
  if (pedido.url?.startsWith('/api/')) {
    encaminharParaApi(pedido, resposta);
  } else {
    servirArquivo(pedido, resposta);
  }
}).listen(porta, () => console.log(`Build do front em http://localhost:${porta} (API: ${api.origin})`));
