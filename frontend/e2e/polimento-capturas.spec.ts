import { Page, expect, test } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { chamar, idPor, tokenDe } from './apoio/api';
import { entrarComo } from './apoio/sessao';

/**
 * Capturas "antes e depois" do polimento da interface (e2e/capturas/polimento/). A fase vem de CAPTURA_FASE ("antes" ou
 * "depois", o padrão). Usa um relatório dos dados de desenvolvimento que tenha cenários E links (o primeiro, pela bateria).
 *   CAPTURA_FASE=antes npx playwright test e2e/polimento-capturas.spec.ts
 */
const fase = process.env['CAPTURA_FASE'] === 'antes' ? 'antes' : 'depois';

async function capturarPolimento(page: Page, nome: string): Promise<void> {
  const pasta = join(__dirname, 'capturas', 'polimento');
  mkdirSync(pasta, { recursive: true });
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: join(pasta, `${fase}-${nome}.png`), fullPage: true });
}

/** Um relatório "Bateria NN Alfa" (dados de desenvolvimento) com pelo menos um cenário e um link. */
async function relatorioComCenariosELinks(): Promise<string> {
  const admin = tokenDe('admin');
  const links = await chamar<{ itens: { dados: { relatorio_teste: { id: string; rotulo: string } } }[] }>(
    admin,
    'GET',
    '/grupos/testes_desempenho/entidades/link/registros?tamanho=100&texto_exibicao.contem=Painel de monitora',
  );
  const candidatos = links.corpo.itens
    .map((l) => l.dados.relatorio_teste)
    .filter((r) => /^Bateria \d+ Alfa$/.test(r.rotulo ?? ''))
    .sort((a, b) => a.rotulo.localeCompare(b.rotulo));
  for (const relatorio of candidatos) {
    const cenarios = await chamar<{ total: number }>(
      admin,
      'GET',
      `/grupos/testes_desempenho/entidades/cenario_teste/registros?relatorio_teste=${relatorio.id}&tamanho=1`,
    );
    if (cenarios.corpo.total > 0) {
      return relatorio.id;
    }
  }
  throw new Error('Nenhum relatório "Bateria NN Alfa" com cenários e links. Rode o npm run dados-dev.');
}

test(`capturas do polimento (${fase})`, async ({ page }) => {
  const relatorio = await relatorioComCenariosELinks();
  await idPor(tokenDe('admin'), 'sigla', 'codigo_sigla', 'PIX'); // os cadastros de apoio existem

  await entrarComo(page, 'dev.gestor', '/testes_desempenho/relatorio_teste');
  await expect(page.locator('tbody tr').first()).toBeVisible();
  await capturarPolimento(page, '01-listagem-relatorios');

  await page.goto(`/testes_desempenho/relatorio_teste/${relatorio}`);
  await expect(page.getByRole('tab', { name: 'Dados' })).toBeVisible();
  await expect(page.locator('.secao-filhos').first()).toBeVisible();
  await expect(page.locator('.secao-filhos tbody tr').first()).toBeVisible();
  await capturarPolimento(page, '02-detalhe-relatorio');

  await page.goto(`/testes_desempenho/relatorio_teste/${relatorio}/editar`);
  await expect(page.getByRole('heading', { name: /^Editar/ })).toBeVisible();
  await expect(page.getByRole('button', { name: /^Salvar/ })).toBeVisible();
  await capturarPolimento(page, '03-formulario-edicao-relatorio');

  await page.goto('/testes_desempenho/sigla');
  await expect(page.locator('tbody tr').first()).toBeVisible();
  await capturarPolimento(page, '04-listagem-siglas');
});
