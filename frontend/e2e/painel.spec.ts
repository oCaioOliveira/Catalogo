import { Page, expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { capturar, entrarComo } from './apoio/sessao';

/**
 * Painel gerencial (docs/frontend.md → "Painel gerencial"), com os dados do npm run dados-dev (inclusive o histórico de 12
 * meses). Capturas em e2e/capturas/painel-*.png.
 */

const PAINEL = '/painel/testes_desempenho';
const WIDGETS = [
  'medias-gerais',
  'relatorios-por-mes-e-status',
  'relatorios-por-tipo',
  'vazao-por-ambiente',
  'evolucao-tempos-de-resposta',
  'taxa-de-erro-por-projeto',
  'endpoints-maior-percentil-95',
];

function widget(page: Page, id: string) {
  return page.locator(`app-widget[data-widget="${id}"]`);
}

/** Espera o widget sair do carregamento e terminar desenhado: cartões, tabela ou o canvas do gráfico. */
async function esperarDesenhado(page: Page, id: string): Promise<void> {
  const cartao = widget(page, id);
  await expect(cartao.locator('[aria-busy="true"]')).toHaveCount(0);
  await expect(cartao.getByText('Não foi possível carregar')).toHaveCount(0);
  await expect(cartao.getByText('Configuração inválida')).toHaveCount(0);
  await expect(cartao.locator('canvas, table, .cartao-kpi').first()).toBeVisible();
}

async function escolher(page: Page, rotulo: string | RegExp, opcao: string | RegExp): Promise<void> {
  await page.getByRole('combobox', { name: rotulo }).click();
  await page.getByRole('option', { name: opcao }).first().click();
}

test('o GESTOR vê o "Painel" no menu; INTERNO e EXTERNO não, e a rota direta mostra "sem permissão"', async ({
  page,
  browser,
}) => {
  await entrarComo(page, 'dev.gestor');
  const menu = page.getByRole('navigation', { name: 'Menu da área de gestão' });
  await expect(menu.getByRole('link', { name: 'Painel', exact: true })).toBeVisible();

  for (const perfil of ['dev.interno', 'dev.externo'] as const) {
    const contexto = await browser.newContext({
      locale: 'pt-BR',
      timezoneId: 'America/Sao_Paulo',
      reducedMotion: 'reduce',
    });
    const outra = await contexto.newPage();
    await entrarComo(outra, perfil);
    const menuOutra = outra.getByRole('navigation', { name: 'Menu da área de gestão' });
    await expect(menuOutra.getByRole('link', { name: 'Relatórios de Teste' })).toBeVisible();
    await expect(menuOutra.getByRole('link', { name: 'Painel', exact: true })).toHaveCount(0);

    await outra.goto(PAINEL);
    await expect(outra.getByRole('heading', { name: 'Sem permissão' })).toBeVisible();
    await expect(outra).toHaveURL(new RegExp(`${PAINEL}$`));
    if (perfil === 'dev.interno') {
      await capturar(outra, 'painel-05-sem-permissao-interno');
    }
    await contexto.close();
  }
});

test('os 7 widgets pré-configurados renderizam com os dados de desenvolvimento, e "Ver dados" mostra a tabela', async ({
  page,
}) => {
  await entrarComo(page, 'dev.gestor', PAINEL);
  await expect(page.getByRole('heading', { name: 'Painel gerencial' })).toBeVisible();
  await expect(page.locator('app-widget')).toHaveCount(WIDGETS.length);
  for (const id of WIDGETS) {
    await esperarDesenhado(page, id);
  }
  // Os KPIs da visão geral e os das médias, com a unidade.
  await expect(page.locator('[data-kpi="total"] .kpi-valor')).not.toHaveText('0');
  await expect(widget(page, 'medias-gerais').locator('.kpi-valor').first()).toContainText('TPS');
  await capturar(page, 'painel-01-gestor');

  const vazao = widget(page, 'vazao-por-ambiente');
  await vazao.getByRole('button', { name: 'Ver dados' }).click();
  const tabela = vazao.getByRole('table');
  await expect(tabela).toBeVisible();
  await expect(tabela.getByRole('columnheader', { name: 'Média de Vazão média (TPS)' })).toBeVisible();
  await expect(tabela.getByRole('row')).not.toHaveCount(1);
  await vazao.scrollIntoViewIfNeeded();
  await vazao.screenshot({ path: `${__dirname}/capturas/painel-03-ver-dados.png` });
  await vazao.getByRole('button', { name: 'Ver gráfico' }).click();
  await expect(vazao.locator('canvas')).toBeVisible();
});

test('mudar o período e a equipe atualiza os blocos e a URL, e recarregar mantém o estado', async ({ page }) => {
  await entrarComo(page, 'admin', PAINEL);
  for (const id of WIDGETS) {
    await esperarDesenhado(page, id);
  }
  // O ADMINISTRADOR escolhe "Todas" (o padrão) ou uma equipe.
  await expect(page.getByRole('combobox', { name: 'Equipe' })).toContainText('Todas');
  await capturar(page, 'painel-02-administrador-todas');

  const novoPeriodo = page.waitForRequest(
    (r) => r.url().includes('/indicadores?') && r.url().includes('relatorio_teste.data_execucao.de='),
  );
  await escolher(page, 'Período', 'Últimos 3 meses');
  await expect(page).toHaveURL(/periodo=3m/);
  const de = new URL((await novoPeriodo).url()).searchParams.get('relatorio_teste.data_execucao.de');
  const hoje = new Date();
  const inicio = new Date(hoje.getFullYear(), hoje.getMonth() - 2, 1);
  expect(de).toBe(`${inicio.getFullYear()}-${String(inicio.getMonth() + 1).padStart(2, '0')}-01`);

  const comEquipe = page.waitForRequest((r) => r.url().includes('/visao-geral?') && r.url().includes('equipeId='));
  await escolher(page, 'Equipe', 'Equipe Alfa');
  await expect(page).toHaveURL(/equipeId=/);
  await comEquipe;
  for (const id of WIDGETS) {
    await esperarDesenhado(page, id);
  }

  await page.reload();
  await expect(page.getByRole('combobox', { name: 'Período' })).toContainText('Últimos 3 meses');
  await expect(page.getByRole('combobox', { name: 'Equipe' })).toContainText('Equipe Alfa');
  await esperarDesenhado(page, 'vazao-por-ambiente');
});

test('análise personalizada: "Vazão média por Relatório › Ambiente", exportar CSV e a URL reproduz a análise', async ({
  page,
}) => {
  await entrarComo(page, 'dev.gestor', `${PAINEL}/analise`);
  await escolher(page, 'Tipo de item', 'Cenário de Teste');
  await page.getByRole('button', { name: 'Adicionar dimensão' }).click();
  await escolher(page, 'Dimensão 1', 'Relatório › Ambiente');
  await escolher(page, 'Métrica 1', 'Média de Vazão média (TPS)');
  await escolher(page, 'Visualização', 'Colunas');
  await escolher(page, 'O período do painel vale para', 'Relatório › Data de execução');

  await expect(page).toHaveURL(/agruparPor=relatorio_teste\.ambiente/);
  await expect(page).toHaveURL(/metricas=media(:|%3A)vazao_media_tps/);
  const resultado = page.locator('app-widget');
  await expect(resultado.locator('canvas')).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('listbox')).toHaveCount(0);
  await page.evaluate(() => window.scrollTo(0, 0));
  await capturar(page, 'painel-04-analise');

  await resultado.getByRole('button', { name: /^Exportar/ }).click();
  const download = page.waitForEvent('download');
  await page.getByRole('menuitem', { name: 'Arquivo CSV (.csv)', exact: true }).click();
  const arquivo = await download;
  expect(arquivo.suggestedFilename()).toMatch(/^cenario_teste_indicadores_\d{4}-\d{2}-\d{2}\.csv$/);
  const linhas = readFileSync(await arquivo.path(), 'utf8')
    .replace(/^﻿/, '')
    .trim()
    .split(/\r?\n/);
  expect(linhas[0]).toBe('Ambiente (Relatório);Contagem;Média de Vazão média (TPS)');
  expect(linhas.length).toBeGreaterThan(2);

  // O mesmo link, numa aba nova (o token é por aba, ADR 0035: entra de novo e abre o endereço): a mesma análise.
  const endereco = new URL(page.url());
  const outra = await page.context().newPage();
  await entrarComo(outra, 'dev.gestor', `${endereco.pathname}${endereco.search}`);
  await expect(outra.getByRole('combobox', { name: 'Dimensão 1' })).toContainText('Relatório › Ambiente');
  await expect(outra.getByRole('combobox', { name: 'Métrica 1' })).toContainText('Média de Vazão média (TPS)');
  await expect(outra.locator('app-widget canvas')).toBeVisible();
});

test('"Abrir na análise" leva a configuração do widget', async ({ page }) => {
  await entrarComo(page, 'dev.gestor', PAINEL);
  await esperarDesenhado(page, 'taxa-de-erro-por-projeto');
  await widget(page, 'taxa-de-erro-por-projeto')
    .getByRole('link', { name: /Abrir na análise/ })
    .click();
  await expect(page).toHaveURL(/\/analise\?/);
  await expect(page.getByRole('combobox', { name: 'Dimensão 1' })).toContainText('Relatório › Projeto');
  await expect(page.getByRole('combobox', { name: 'Visualização' })).toContainText('Barras horizontais');
  await expect(page.locator('app-widget canvas')).toBeVisible();
});

test('um widget com erro simulado não afeta os outros', async ({ page }) => {
  // Só a consulta do widget "Relatórios por tipo de teste" falha (500).
  await page.route(/\/indicadores\?.*agruparPor=tipo_teste/, (rota) =>
    rota.fulfill({
      status: 500,
      contentType: 'application/problem+json',
      body: JSON.stringify({ status: 500, codigo: 'ERRO_INTERNO', title: 'Erro simulado', traceId: '00-e2e-00' }),
    }),
  );
  await entrarComo(page, 'dev.gestor', PAINEL);

  const comErro = widget(page, 'relatorios-por-tipo');
  await expect(comErro.getByText('Não foi possível carregar este bloco.')).toBeVisible();
  await expect(comErro.getByRole('button', { name: 'Tentar de novo' })).toBeVisible();
  for (const id of WIDGETS.filter((w) => w !== 'relatorios-por-tipo')) {
    await esperarDesenhado(page, id);
  }

  // Sem a falha, "Tentar de novo" recupera o widget.
  await page.unroute(/\/indicadores\?.*agruparPor=tipo_teste/);
  await comErro.getByRole('button', { name: 'Tentar de novo' }).click();
  await esperarDesenhado(page, 'relatorios-por-tipo');
});
