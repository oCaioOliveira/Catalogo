import { expect, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { criarRelatorio, prefixoUnico, tokenDe } from './apoio/api';
import { capturar, entrarComo } from './apoio/sessao';

test('EXTERNO não vê os botões de criar, editar e excluir (RN26)', async ({ page }) => {
  await criarRelatorio(tokenDe('dev.gestor'), `${prefixoUnico()} externo`);
  await entrarComo(page, 'dev.externo', '/testes_desempenho/relatorio_teste');
  await expect(page.locator('tbody tr').first()).toBeVisible();
  await expect(page.getByRole('button', { name: 'Novo' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Editar' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Excluir' })).toHaveCount(0);
  // Sem nenhuma ação permitida, a linha nem mostra o "⋯".
  await expect(page.getByRole('button', { name: /^Ações de/ })).toHaveCount(0);
  await capturar(page, '06-listagem-externo');

  // A coluna principal (o campo de exibição) é o link do item.
  await page.locator('tbody tr').first().locator('.link-linha').click();
  await expect(page.getByRole('tab', { name: 'Dados' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Editar' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: /Adicionar/ })).toHaveCount(0);
});

test('exporta a listagem em CSV com os filtros atuais', async ({ page }) => {
  const prefixo = prefixoUnico();
  await criarRelatorio(tokenDe('dev.gestor'), `${prefixo} exportação`);
  await entrarComo(page, 'dev.gestor', `/testes_desempenho/relatorio_teste?bateria_testes.contem=${prefixo}`);
  await expect(page.locator('tbody tr')).toHaveCount(1);

  await page.getByRole('button', { name: 'Exportar' }).click();
  const download = page.waitForEvent('download');
  await page.getByRole('menuitem', { name: 'Arquivo CSV (.csv)', exact: true }).click();
  const arquivo = await download;

  expect(arquivo.suggestedFilename()).toMatch(/^relatorio_teste_\d{4}-\d{2}-\d{2}\.csv$/);
  const conteudo = readFileSync(await arquivo.path(), 'utf8');
  const linhas = conteudo.replace(/^﻿/, '').trim().split(/\r?\n/);
  expect(linhas[0]).toContain('Bateria de testes');
  expect(linhas).toHaveLength(2);
  expect(linhas[1]).toContain(`${prefixo} exportação`);
});
