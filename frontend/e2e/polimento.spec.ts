import { expect, test } from '@playwright/test';
import { cadastrosAlfa, criar, criarRelatorio, prefixoUnico, tokenDe } from './apoio/api';
import { campo, entrarComo } from './apoio/sessao';

// Polimento da interface: tipo URL (ADR 0042), plural e ordem do menu, ordenação por referência (RN19), layout dos textos longos e
// densidade das tabelas.

test('a seção de Links do relatório mostra a URL como link para uma nova aba', async ({ page }) => {
  const gestor = tokenDe('dev.gestor');
  const prefixo = prefixoUnico();
  const relatorio = await criarRelatorio(gestor, `${prefixo} links`);
  const { equipe } = await cadastrosAlfa(tokenDe('admin'));
  const url = `https://grafana.interno/d/${prefixo.toLowerCase()}`;
  await criar(gestor, 'link', { equipeId: equipe, relatorio_teste: relatorio, texto_exibicao: 'Painel E2E', url });

  await entrarComo(page, 'dev.gestor', `/testes_desempenho/relatorio_teste/${relatorio}`);
  const links = page.locator('app-secao-filhos').filter({ has: page.getByRole('heading', { name: 'Links' }) });
  await expect(links.getByRole('columnheader', { name: 'URL' })).toBeVisible();
  const link = links.getByRole('link', { name: new RegExp(url) });
  await expect(link).toHaveAttribute('href', url);
  await expect(link).toHaveAttribute('target', '_blank');
  await expect(link).toHaveAttribute('rel', 'noopener noreferrer');
});

test('cadastrar um link com URL inválida mostra o erro no campo', async ({ page }) => {
  const relatorio = await criarRelatorio(tokenDe('dev.gestor'), `${prefixoUnico()} url inválida`);

  await entrarComo(page, 'dev.gestor', `/testes_desempenho/relatorio_teste/${relatorio}`);
  await page.getByRole('button', { name: 'Adicionar link' }).click();
  const dialogo = page.getByRole('dialog');
  await campo(dialogo, /^Texto de exibição/).fill('Painel sem esquema');
  await campo(dialogo, /^URL/).fill('grafana.interno/d/painel');
  await dialogo.getByRole('button', { name: /^Salvar\b/ }).click();

  await expect(dialogo.locator('#dialogo-link-url-erros')).toContainText('http://');
  await expect(campo(dialogo, /^URL/)).toHaveAttribute('aria-invalid', 'true');
  await expect(dialogo.getByRole('button', { name: /^Abrir URL/ })).toBeDisabled();

  // Corrigida, o botão de abrir vira um link e o link é salvo.
  await campo(dialogo, /^URL/).fill('https://grafana.interno/d/painel');
  await expect(dialogo.getByRole('link', { name: /^Abrir URL/ })).toHaveAttribute('target', '_blank');
  await dialogo.getByRole('button', { name: /^Salvar\b/ }).click();
  await expect(dialogo).toBeHidden();
  await expect(page.getByRole('link', { name: /grafana\.interno\/d\/painel/ })).toBeVisible();
});

test('ordena os relatórios por Projeto (pelo nome do projeto), nos dois sentidos', async ({ page }) => {
  const gestor = tokenDe('dev.gestor');
  const c = await cadastrosAlfa(tokenDe('admin'));
  const prefixo = prefixoUnico();
  await criarRelatorio(gestor, `${prefixo} T`, { projeto: c.projeto }); // Transferências Alfa
  await criarRelatorio(gestor, `${prefixo} C`, { projeto: c.outroProjeto }); // Consultas Alfa

  await entrarComo(page, 'dev.gestor', `/testes_desempenho/relatorio_teste?bateria_testes.contem=${prefixo}`);
  const linhas = page.locator('tbody tr');
  await expect(linhas).toHaveCount(2);

  await page.getByRole('columnheader', { name: /^Projeto/ }).click();
  await expect(page).toHaveURL(/ordenacao=projeto(&|$)/);
  await expect(linhas.nth(0)).toContainText('Consultas Alfa');
  await expect(linhas.nth(1)).toContainText('Transferências Alfa');

  await page.getByRole('columnheader', { name: /^Projeto/ }).click();
  await expect(page).toHaveURL(/ordenacao=-projeto/);
  await expect(linhas.nth(0)).toContainText('Transferências Alfa');
  await expect(linhas.nth(1)).toContainText('Consultas Alfa');
});

test('no formulário, os textos longos ficam embaixo, na largura toda, e crescem ao digitar', async ({ page }) => {
  await entrarComo(page, 'dev.gestor', '/testes_desempenho/relatorio_teste/novo');
  await expect(page.getByRole('heading', { name: 'Novo: Relatório de Teste' })).toBeVisible();

  const formulario = await page.locator('form.formulario').boundingBox();
  const contexto = campo(page, /^Contexto/);
  const objetivo = campo(page, /^Objetivo/);
  const bateria = await campo(page, /^Bateria de testes/).boundingBox();
  const caixaContexto = (await contexto.boundingBox())!;
  const caixaObjetivo = (await objetivo.boundingBox())!;

  // Embaixo dos curtos, um por linha, ocupando (quase) a largura do formulário.
  expect(caixaContexto.y).toBeGreaterThan(bateria!.y);
  expect(caixaObjetivo.y).toBeGreaterThan(caixaContexto.y + caixaContexto.height);
  expect(caixaContexto.width).toBeGreaterThan(formulario!.width * 0.9);

  // Cresce com o texto; o contador continua.
  const texto = Array.from({ length: 10 }, (_, i) => `Linha ${i + 1} do contexto.`).join('\n');
  await contexto.fill(texto);
  await expect.poll(async () => (await contexto.boundingBox())!.height).toBeGreaterThan(caixaContexto.height * 1.5);
  await expect(page.getByText(`${texto.length} caracteres`)).toBeVisible();
});

test('o menu segue a ordem dos metadados, com os nomes no plural, e a área abre no Início', async ({ page }) => {
  await entrarComo(page, 'dev.gestor', '/');
  // "/" abre o Início da área; o menu lateral tem só o trabalho do dia a dia.
  await expect(page).toHaveURL(/\/testes_desempenho$/);
  await expect(page.getByRole('heading', { level: 1 })).toContainText(/^(Bom dia|Boa tarde|Boa noite)/);

  const menu = page.getByRole('navigation', { name: 'Menu da área de gestão' });
  // Os cadastros de apoio ficam num grupo recolhido por padrão.
  const apoio = menu.getByRole('button', { name: 'Cadastros de apoio' });
  await expect(apoio).toHaveAttribute('aria-expanded', 'false');
  await expect(menu.getByRole('link')).toHaveText([
    'Início',
    'Painel',
    'Relatórios de Teste',
    'Projetos',
    'Endpoints de Serviço',
    'RTCs',
  ]);
  await apoio.click();
  await expect(menu.getByRole('link')).toHaveText([
    'Início',
    'Painel',
    'Relatórios de Teste',
    'Projetos',
    'Endpoints de Serviço',
    'RTCs',
    'Comunidades',
    'Siglas',
    'Ambientes',
    'Status de Resultado',
  ]);
  // Volta ao padrão (recolhido), para não afetar os outros testes.
  await apoio.click();
  await expect(apoio).toHaveAttribute('aria-expanded', 'false');
});

test('a densidade da tabela alterna entre Compacto e Confortável, e a escolha fica ao recarregar', async ({ page }) => {
  await entrarComo(page, 'dev.gestor', '/testes_desempenho/sigla');
  const tabela = page.locator('.tabela-registros');
  const compacto = page.getByRole('button', { name: 'Compacto' });
  const confortavel = page.getByRole('button', { name: 'Confortável' });

  await expect(page.locator('tbody tr').first()).toBeVisible();
  await expect(compacto).toHaveAttribute('aria-pressed', 'true');
  await expect(tabela).toHaveClass(/p-datatable-sm/);

  await confortavel.click();
  await expect(confortavel).toHaveAttribute('aria-pressed', 'true');
  await expect(tabela).not.toHaveClass(/p-datatable-sm/);

  await page.reload();
  await expect(page.getByRole('button', { name: 'Confortável' })).toHaveAttribute('aria-pressed', 'true');
  await expect(page.locator('.tabela-registros')).not.toHaveClass(/p-datatable-sm/);

  // Volta ao padrão, para não afetar as capturas e os outros testes.
  await page.getByRole('button', { name: 'Compacto' }).click();
  await expect(page.locator('.tabela-registros')).toHaveClass(/p-datatable-sm/);
});
