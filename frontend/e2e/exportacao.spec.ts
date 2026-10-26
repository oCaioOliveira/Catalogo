import { Locator, Page, expect, test } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { GRUPO } from './apoio/api';
import { entrarComo } from './apoio/sessao';

/**
 * Os menus "Exportar" (listagem e widgets do painel) têm os mesmos rótulos, na mesma ordem (shared/exportacao/opcoes-exportacao.ts):
 * a planilha primeiro, e a dica no CSV. Capturas do menu aberto em e2e/capturas/exportacao/, nos dois temas.
 */
type Tema = 'claro' | 'escuro';
const PLANILHA = 'Planilha Excel (.xlsx)';
const CSV = 'Arquivo CSV (.csv)';
const DICA_CSV = 'Separado por ponto e vírgula; abre direto no Excel';
const pasta = join(__dirname, 'capturas', 'exportacao');

test.use({ viewport: { width: 1366, height: 768 } });

async function conferirMenu(page: Page, botao: Locator): Promise<Locator> {
  await botao.click();
  const itens = page.getByRole('menuitem');
  await expect(itens).toHaveCount(2);
  // Rótulos exatos, nesta ordem.
  expect((await itens.allInnerTexts()).map((t) => t.trim())).toEqual([PLANILHA, CSV]);
  await expect(page.getByRole('menuitem', { name: PLANILHA, exact: true })).toBeVisible();
  const csv = page.getByRole('menuitem', { name: CSV, exact: true });
  // A dica aparece ao passar o mouse no CSV.
  await csv.hover();
  const dica = page.getByRole('tooltip');
  await expect(dica).toHaveText(DICA_CSV);
  // A dica entra com uma animação de opacidade: só captura depois de ela estar inteira.
  await expect(dica).toHaveCSS('opacity', '1');
  return csv;
}

for (const tema of ['claro', 'escuro'] as Tema[]) {
  test(`menus "Exportar" da listagem e do painel: rótulos exatos e a dica do CSV (tema ${tema})`, async ({ page }) => {
    await page.addInitScript((t) => localStorage.setItem('catalogo.tema', t), tema);
    mkdirSync(pasta, { recursive: true });

    await entrarComo(page, 'dev.gestor', `/${GRUPO}/relatorio_teste`);
    await expect(page.locator('tbody tr').first()).toBeVisible();
    await conferirMenu(page, page.getByRole('button', { name: 'Exportar', exact: true }));
    await page.screenshot({ path: join(pasta, `menu-listagem-${tema}.png`) });
    await page.keyboard.press('Escape');

    await page.goto(`/painel/${GRUPO}`);
    const exportarWidget = page.getByRole('button', { name: /^Exportar: / }).first();
    await expect(exportarWidget).toBeVisible();
    await exportarWidget.scrollIntoViewIfNeeded();
    await conferirMenu(page, exportarWidget);
    await page.screenshot({ path: join(pasta, `menu-painel-${tema}.png`) });
  });
}
