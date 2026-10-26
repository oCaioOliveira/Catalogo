import { defineConfig, devices } from '@playwright/test';

/**
 * Testes E2E contra a API e o banco LOCAIS, com os dados do script tools/dados-dev (npm run dados-dev). Veja frontend/README.md →
 * "Testes E2E".
 *
 * Por padrão, o front testado é um BUILD novo (npm run build), servido na porta 4300 por scripts/servir-build.mjs, com o mesmo
 * proxy de /api do `npm start`: cada rodada testa exatamente o código atual (o watcher do `ng serve` no WSL às vezes perde
 * alterações e serve código antigo).
 *   CATALOGO_ADMIN_SENHA=... CATALOGO_SENHA_DEV=... npm run e2e            # build + servidor estático (4300)
 *   CATALOGO_ADMIN_SENHA=... CATALOGO_SENHA_DEV=... npm run e2e:ng-serve   # contra o `npm start` (4200), no desenvolvimento
 * E2E_SEM_BUILD=1 reaproveita o build que já está em dist/ (rodadas seguidas de um arquivo só, sem mudança no front).
 */
const comNgServe = !!process.env['E2E_NG_SERVE'];
const semBuild = !!process.env['E2E_SEM_BUILD'];
const enderecoFront = comNgServe ? 'http://localhost:4200' : 'http://localhost:4300';

export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  // Um de cada vez: os testes compartilham o banco local e o limite de logins da API (10 por minuto).
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  globalSetup: './e2e/apoio/preparacao-global.ts',
  // Falha a execução se sobrar equipe ou usuário de E2E ativo (e2e/apoio/limpeza.ts).
  globalTeardown: './e2e/apoio/verificacao-final.ts',
  use: {
    baseURL: enderecoFront,
    locale: 'pt-BR',
    timezoneId: 'America/Sao_Paulo',
    // Gráficos sem animação (o painel respeita prefers-reduced-motion): capturas de tela estáveis.
    contextOptions: { reducedMotion: 'reduce' },
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1400, height: 900 } } }],
  webServer: comNgServe
    ? { command: 'npm start', url: enderecoFront, reuseExistingServer: true, timeout: 120_000 }
    : {
        command: semBuild ? 'node scripts/servir-build.mjs' : 'npm run build && node scripts/servir-build.mjs',
        url: enderecoFront,
        // Nunca reaproveita um servidor que já está na porta: ele poderia ter um build antigo.
        reuseExistingServer: false,
        timeout: 300_000,
        env: { PORTA: '4300' },
      },
});
