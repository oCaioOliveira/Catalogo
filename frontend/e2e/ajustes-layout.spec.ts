import { Page, expect, test } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { GRUPO } from './apoio/api';
import { entrarComo } from './apoio/sessao';

/**
 * Medições de layout que já voltaram a quebrar (capturas em e2e/capturas/ajustes/):
 *   1. a nota da aba Pré-visualização do tipo de item não pode encostar na tabela (antes, .campo-dica.bloco subia 8 px por cima dela);
 *   2. o nome "Catálogo" no cabeçalho fica centralizado entre as duas barrinhas (antes: 8 px de um lado e 16 px do outro).
 */
type Tema = 'claro' | 'escuro';
const TEMAS: Tema[] = ['claro', 'escuro'];
const LARGURAS = [1024, 1366, 1920];
const pasta = join(__dirname, 'capturas', 'ajustes');

async function comTema(page: Page, tema: Tema): Promise<void> {
  await page.addInitScript((t) => localStorage.setItem('catalogo.tema', t), tema);
}

async function capturar(page: Page, nome: string, seletor?: string): Promise<void> {
  mkdirSync(pasta, { recursive: true });
  const caminho = join(pasta, `${nome}.png`);
  await (seletor ? page.locator(seletor).screenshot({ path: caminho }) : page.screenshot({ path: caminho }));
}

for (const tema of TEMAS) {
  for (const largura of LARGURAS) {
    test(`pré-visualização: a nota fica abaixo da tabela, sem sobrepor nada (${largura} px, tema ${tema})`, async ({
      page,
    }) => {
      await page.setViewportSize({ width: largura, height: 900 });
      await comTema(page, tema);
      // Relatório de Teste: o tipo de item com mais campos (e mais colunas padrão) da área de desenvolvimento.
      await entrarComo(page, 'admin', `/configuracoes/areas/${GRUPO}/entidades/relatorio_teste`);
      await page.getByRole('tab', { name: 'Pré-visualização' }).click();
      const nota = page.getByText('Textos longos não viram coluna');
      await expect(nota).toBeVisible();
      const colunas = await page.locator('app-aba-previa table th').count();
      expect(colunas).toBeGreaterThanOrEqual(6);

      const medida = await nota.evaluate((elNota) => {
        const raiz = elNota.closest('app-aba-previa')!;
        const n = elNota.getBoundingClientRect();
        const cruzam: string[] = [];
        for (const el of raiz.querySelectorAll('*')) {
          if (el === elNota || el.contains(elNota) || elNota.contains(el)) {
            continue;
          }
          const r = el.getBoundingClientRect();
          if (r.width === 0 || r.height === 0) {
            continue;
          }
          const largura = Math.min(n.right, r.right) - Math.max(n.left, r.left);
          const altura = Math.min(n.bottom, r.bottom) - Math.max(n.top, r.top);
          if (largura > 0.5 && altura > 0.5) {
            cruzam.push(`${el.tagName.toLowerCase()}.${el.className} (${Math.round(altura)} px)`);
          }
        }
        const tabela = raiz.querySelector('.p-datatable')!.getBoundingClientRect();
        return { cruzam, espaco: n.top - tabela.bottom };
      });
      expect(medida.cruzam, medida.cruzam.join('\n')).toEqual([]);
      // No fluxo, abaixo da tabela, com o espaçamento padrão (--espaco-4, 16 px).
      expect(medida.espaco).toBeGreaterThanOrEqual(15);
      expect(medida.espaco).toBeLessThanOrEqual(17);

      await nota.scrollIntoViewIfNeeded();
      await capturar(page, `depois-previa-${largura}-${tema}`);
    });
  }
}

const CENARIOS_CABECALHO: { nome: string; largura: number; tema: Tema; recolher: boolean }[] = [
  { nome: '1366-claro', largura: 1366, tema: 'claro', recolher: false },
  { nome: '1366-escuro', largura: 1366, tema: 'escuro', recolher: false },
  { nome: '1366-recolhido-claro', largura: 1366, tema: 'claro', recolher: true },
  { nome: '1366-recolhido-escuro', largura: 1366, tema: 'escuro', recolher: true },
  { nome: '1024-claro', largura: 1024, tema: 'claro', recolher: false },
  { nome: '1024-escuro', largura: 1024, tema: 'escuro', recolher: false },
];

for (const cenario of CENARIOS_CABECALHO) {
  test(`cabeçalho: "Catálogo" centralizado entre as barrinhas (${cenario.nome})`, async ({ page }) => {
    await page.setViewportSize({ width: cenario.largura, height: 800 });
    await comTema(page, cenario.tema);
    await entrarComo(page, 'dev.gestor', `/${GRUPO}`);
    if (cenario.recolher) {
      await page.getByRole('button', { name: 'Recolher menu' }).click();
      await expect(page.getByRole('button', { name: 'Expandir menu' })).toBeVisible();
    }
    const nome = page.getByRole('banner').getByText('Catálogo', { exact: true });
    await expect(nome).toBeVisible();

    // A caixa do TEXTO (não a do elemento): o Range dá o início e o fim reais das letras.
    const m = await nome.evaluate((el) => {
      const range = document.createRange();
      range.selectNodeContents(el);
      const texto = range.getBoundingClientRect();
      const [esquerda, direita] = [...document.querySelectorAll('.cabecalho-app .separador-cabecalho')].map((b) =>
        b.getBoundingClientRect(),
      );
      const centro = (r: DOMRect) => r.top + r.height / 2;
      return {
        distEsquerda: texto.left - esquerda.right,
        distDireita: direita.left - texto.right,
        centroTexto: centro(texto),
        centroEsquerda: centro(esquerda),
        centroDireita: centro(direita),
      };
    });
    console.log(`cabeçalho ${cenario.nome}: ${JSON.stringify(m)}`);
    expect(Math.abs(m.distEsquerda - m.distDireita), JSON.stringify(m)).toBeLessThanOrEqual(1);
    expect(m.distEsquerda).toBeGreaterThan(0);
    expect(Math.abs(m.centroTexto - m.centroEsquerda)).toBeLessThanOrEqual(1);
    expect(Math.abs(m.centroTexto - m.centroDireita)).toBeLessThanOrEqual(1);
    await capturar(page, `depois-cabecalho-${cenario.nome}`, '.cabecalho-app');
  });
}

test('abaixo de 1024 px, o nome sai e sobra uma barrinha só, entre a logo e o seletor', async ({ page }) => {
  await page.setViewportSize({ width: 900, height: 800 });
  await entrarComo(page, 'dev.gestor', `/${GRUPO}`);
  await expect(page.getByRole('banner').getByRole('img', { name: 'CAIXA' })).toBeVisible();
  await expect(page.getByRole('banner').getByText('Catálogo', { exact: true })).toBeHidden();
  await expect(page.locator('.cabecalho-app .separador-cabecalho:visible')).toHaveCount(1);
});

test('o nome do sistema aparece como "Catálogo" no título da aba e no login', async ({ page }) => {
  await page.goto('/login');
  await expect(page).toHaveTitle('Entrar · Catálogo');
  await expect(page.getByText('Catálogo', { exact: true })).toBeVisible();
  await expect(page.getByText('Catalogo', { exact: true })).toHaveCount(0);
});
