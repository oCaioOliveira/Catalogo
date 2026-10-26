import { Locator, Page, expect, test } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { GRUPO, chamar, criar, idPor, listar, prefixoUnico, tokenDe } from './apoio/api';
import { habilitarNaListagem, limparE2E } from './apoio/limpeza';
import { campo, digitarData, entrarComo, escolherReferencia } from './apoio/sessao';
import { UsuarioTeste, abrirComo, criarUsuarioDeTeste } from './apoio/usuarios-teste';

/**
 * Itens relacionados no detalhe de um item (RN94, ADR 0047). O arquivo cria a PRÓPRIA equipe ("E2E-… R", habilitada em Testes de
 * Desempenho) e um INTERNO só dela; o administrador cadastra, nela, um projeto com 12 relatórios e 2 endpoints, um relatório com
 * cenário e link e um RTC sem nada apontando para ele. Os relatórios usam o ambiente global "Homologação" dos dados de
 * desenvolvimento, que também tem relatórios de outras equipes: o INTERNO só conta os da equipe dele. No início e no fim, o helper
 * de limpeza desativa a equipe e o usuário. Capturas em e2e/capturas/relacionados-*.png.
 */
test.describe.configure({ mode: 'serial', timeout: 180_000 });

const prefixo = prefixoUnico();
const admin = () => tokenDe('admin');
const RELATORIOS = 12;

let equipe = '';
let interno: UsuarioTeste;
let projeto = '';
let rtc = '';
let rtcSolto = '';
let ambiente = '';
let status = '';
let relatorioComFilhos = '';
const nomeProjeto = `Projeto ${prefixo}`;

async function capturarTela(page: Page, nome: string): Promise<void> {
  for (const fechar of await page.locator('.p-toast-close-button').all()) {
    await fechar.click({ timeout: 1000 }).catch(() => undefined);
  }
  await page.waitForTimeout(600);
  const pasta = join(__dirname, 'capturas');
  mkdirSync(pasta, { recursive: true });
  await page.screenshot({ path: join(pasta, `${nome}.png`), fullPage: true });
}

/** A seção pelo começo do título ("Relatórios de Teste" acha "Relatórios de Teste (12)"). */
function secao(page: Page, titulo: string): Locator {
  return page
    .locator('app-secao-relacionados, app-secao-filhos')
    .filter({ has: page.getByRole('heading', { name: new RegExp(`^${titulo} \\(\\d+\\)$`) }) });
}

function relatorio(bateria: string): Record<string, unknown> {
  return {
    equipeId: equipe,
    projeto,
    rtc,
    ambiente,
    status_resultado: status,
    data_execucao: '2026-10-01T10:00:00-03:00',
    bateria_testes: bateria,
    tipo_teste: 'Carga',
    executado_por: 'Equipe E2E',
  };
}

test.beforeAll(async () => {
  test.setTimeout(240_000);
  await limparE2E(admin());
  const criada = await chamar<{ id: string }>(admin(), 'POST', '/equipes', { nome: `${prefixo} R` });
  expect(criada.status, 'POST /equipes').toBe(201);
  equipe = criada.corpo.id;
  await habilitarNaListagem(admin(), GRUPO, equipe);
  interno = await criarUsuarioDeTeste(admin(), prefixo, 'int', 'Interno de R', 'INTERNO', [equipe]);

  const sigla = (await listar(admin(), 'sigla', { tamanho: '1' })).itens[0].id;
  projeto = await criar(admin(), 'projeto', { equipeId: equipe, sigla, nome: nomeProjeto });
  rtc = await criar(admin(), 'rtc', { equipeId: equipe, rtc_externo_id: `${prefixo}A`.slice(0, 20) });
  rtcSolto = await criar(admin(), 'rtc', { equipeId: equipe, rtc_externo_id: `${prefixo}S`.slice(0, 20) });
  ambiente = await idPor(admin(), 'ambiente', 'nome', 'Homologação');
  status = await idPor(admin(), 'status_resultado', 'descricao', 'Aprovado');
  for (let i = 1; i <= RELATORIOS; i++) {
    const id = await criar(admin(), 'relatorio_teste', relatorio(`${prefixo} bateria ${String(i).padStart(2, '0')}`));
    if (i === 1) {
      relatorioComFilhos = id;
    }
  }
  const endpoint = await criar(admin(), 'endpoint_servico', {
    equipeId: equipe,
    projeto,
    nome_servico: `${prefixo} consulta`,
    metodo: 'GET',
    endpoint_url_fila: '/consulta',
    ativo: true,
  });
  await criar(admin(), 'endpoint_servico', {
    equipeId: equipe,
    projeto,
    nome_servico: `${prefixo} envio`,
    metodo: 'POST',
    endpoint_url_fila: '/envio',
    ativo: true,
  });
  await criar(admin(), 'cenario_teste', {
    equipeId: equipe,
    relatorio_teste: relatorioComFilhos,
    endpoint_servico: endpoint,
    numero_cenario: 1,
    usuarios_virtuais: 100,
    tempo_medio_resposta_ms: 200,
    percentil_95_ms: 400,
    vazao_media_tps: 50,
    taxa_erro_percentual: 0.1,
  });
});

test.afterAll(async () => {
  await limparE2E(admin());
});

test('1. o detalhe de um Projeto mostra os Relatórios e os Endpoints dele, sem a coluna Projeto', async ({
  browser,
}) => {
  const pagina = await abrirComo(browser, interno.token, `/${GRUPO}/projeto/${projeto}`);
  const relatorios = secao(pagina, 'Relatórios de Teste');
  const endpoints = secao(pagina, 'Endpoints de Serviço');
  await expect(relatorios.getByRole('heading')).toHaveText(`Relatórios de Teste (${RELATORIOS})`);
  await expect(endpoints.getByRole('heading')).toHaveText('Endpoints de Serviço (2)');
  await expect(relatorios.locator('tbody tr')).toHaveCount(10);
  await expect(endpoints.locator('tbody tr')).toHaveCount(2);
  await expect(endpoints.locator('tbody')).toContainText(`${prefixo} consulta`);
  // A coluna do campo que aponta para o projeto seria sempre ele: não aparece.
  await expect(relatorios.getByRole('columnheader', { name: /^Projeto/ })).toHaveCount(0);
  await expect(endpoints.getByRole('columnheader', { name: /^Projeto/ })).toHaveCount(0);
  await expect(relatorios.getByRole('columnheader', { name: /^Bateria de testes/ })).toBeVisible();
  await capturarTela(pagina, 'relacionados-01-detalhe-projeto');

  // Cada linha leva ao detalhe do item.
  await endpoints
    .locator('tbody tr')
    .filter({ hasText: `${prefixo} envio` })
    .locator('td')
    .first()
    .click();
  await expect(pagina.getByRole('heading', { level: 1, name: `${prefixo} envio` })).toBeVisible();
  await pagina.context().close();
});

test('2. "Ver todos" abre os relatórios filtrados pelo projeto, com o filtro visível e o mesmo total', async ({
  browser,
}) => {
  const pagina = await abrirComo(browser, interno.token, `/${GRUPO}/projeto/${projeto}`);
  await secao(pagina, 'Relatórios de Teste')
    .getByRole('button', { name: /^Ver todos/ })
    .click();
  await expect(pagina).toHaveURL(new RegExp(`/${GRUPO}/relatorio_teste\\?.*projeto=${projeto}`));
  await expect(pagina.getByRole('heading', { level: 1, name: 'Relatórios de Teste' })).toBeVisible();
  // O painel de filtros vem aberto, com o projeto escolhido.
  await expect(pagina.getByRole('combobox', { name: /^Projeto/ })).toHaveValue(nomeProjeto);
  await expect(pagina.getByRole('button', { name: 'Filtros 1', exact: true })).toBeVisible();
  await expect(pagina.locator('.p-paginator')).toContainText(`de ${RELATORIOS}`);
  await capturarTela(pagina, 'relacionados-02-ver-todos');
  await pagina.context().close();
});

test('3. "Adicionar relatório de teste" a partir do projeto vem com o projeto preenchido; ao salvar, volta ao projeto e a contagem sobe', async ({
  browser,
}) => {
  const pagina = await abrirComo(browser, interno.token, `/${GRUPO}/projeto/${projeto}`);
  await secao(pagina, 'Relatórios de Teste').getByRole('button', { name: 'Adicionar relatório de teste' }).click();
  await expect(pagina.getByRole('heading', { level: 1, name: 'Novo: Relatório de Teste' })).toBeVisible();
  const projetoNoFormulario = pagina.getByRole('combobox', { name: /^Projeto/ });
  await expect(projetoNoFormulario).toHaveValue(nomeProjeto);
  await expect(projetoNoFormulario).toBeEnabled();
  await escolherReferencia(pagina, /^RTC/, prefixo.slice(0, 8), `${prefixo}A`.slice(0, 20));
  await escolherReferencia(pagina, /^Ambiente/, 'Homolog', 'Homologação');
  await escolherReferencia(pagina, /^Status/, 'Aprovado', /^Aprovado$/);
  await digitarData(pagina, /^Data de execução/, '20/10/2026 09:00');
  await campo(pagina, /^Bateria de testes/).fill(`${prefixo} criado pelo projeto`);
  await campo(pagina, /^Tipo de teste/).fill('Carga');
  await campo(pagina, /^Executado por/).fill('Equipe E2E');
  await capturarTela(pagina, 'relacionados-03-novo-com-projeto');
  await pagina.getByRole('button', { name: /^Salvar\b/ }).click();

  await expect(pagina).toHaveURL(new RegExp(`/${GRUPO}/projeto/${projeto}$`));
  await expect(secao(pagina, 'Relatórios de Teste').getByRole('heading')).toHaveText(
    `Relatórios de Teste (${RELATORIOS + 1})`,
  );
  await pagina.context().close();
});

test('4. o detalhe de um Relatório continua com Cenários e Links editáveis, agora com a quantidade no título', async ({
  browser,
}) => {
  const pagina = await abrirComo(browser, interno.token, `/${GRUPO}/relatorio_teste/${relatorioComFilhos}`);
  await expect(secao(pagina, 'Cenários de Teste').getByRole('heading')).toHaveText('Cenários de Teste (1)');
  await expect(secao(pagina, 'Links').getByRole('heading')).toHaveText('Links (0)');
  await expect(pagina.getByRole('button', { name: 'Adicionar cenário de teste' })).toBeVisible();
  await expect(pagina.getByRole('button', { name: 'Adicionar link' })).toBeVisible();
  await expect(secao(pagina, 'Cenários de Teste').locator('tbody tr')).toHaveCount(1);

  await pagina.getByRole('button', { name: 'Adicionar link' }).click();
  await campo(pagina.getByRole('dialog'), /^Texto de exibição/).fill('Painel do teste');
  await campo(pagina.getByRole('dialog'), /^URL/).fill('https://grafana.interno/d/e2e');
  await pagina
    .getByRole('dialog')
    .getByRole('button', { name: /^Salvar\b/ })
    .click();
  await expect(pagina.getByRole('dialog')).toBeHidden();
  await expect(secao(pagina, 'Links').getByRole('heading')).toHaveText('Links (1)');
  await capturarTela(pagina, 'relacionados-04-detalhe-relatorio');
  await pagina.context().close();
});

test('5. o ambiente global Homologação mostra só os relatórios que o usuário de uma equipe vê', async ({ browser }) => {
  const contar = async (token: string) =>
    (
      await chamar<{ campo: { chave: string }; entidade: { chave: string }; quantidade: number }[]>(
        token,
        'GET',
        `/grupos/${GRUPO}/entidades/ambiente/registros/${ambiente}/relacionados`,
      )
    ).corpo.find((r) => r.entidade.chave === 'relatorio_teste' && r.campo.chave === 'ambiente')!.quantidade;
  const doInterno = await contar(interno.token);
  const doAdmin = await contar(admin());
  expect(doInterno).toBe(RELATORIOS + 1);
  expect(doAdmin).toBeGreaterThan(doInterno);

  const pagina = await abrirComo(browser, interno.token, `/${GRUPO}/ambiente/${ambiente}`);
  const relatorios = secao(pagina, 'Relatórios de Teste');
  await expect(relatorios.getByRole('heading')).toHaveText(`Relatórios de Teste (${doInterno})`);
  for (const linha of await relatorios.locator('tbody tr').all()) {
    await expect(linha).toContainText(prefixo);
  }
  await capturarTela(pagina, 'relacionados-05-ambiente-homologacao');
  await pagina.context().close();
});

test('6. um item sem relacionados mostra só a linha "Sem itens relacionados em: …"', async ({ browser }) => {
  const pagina = await abrirComo(browser, interno.token, `/${GRUPO}/rtc/${rtcSolto}`);
  await expect(pagina.locator('.sem-relacionados')).toHaveText('Sem itens relacionados em: Relatórios de Teste, Links');
  await expect(pagina.locator('app-secao-relacionados, app-secao-filhos')).toHaveCount(0);
  await pagina.context().close();
});

test('7. paginação e ordenação dentro de uma seção; recolher uma seção e recarregar mantém o estado', async ({
  page,
}) => {
  await entrarComo(page, 'admin', `/${GRUPO}/projeto/${projeto}`);
  const relatorios = secao(page, 'Relatórios de Teste');
  const linhas = relatorios.locator('tbody tr');
  await expect(linhas).toHaveCount(10);

  await relatorios.getByRole('button', { name: /^Page 2|Página 2|^2$/ }).click();
  await expect(linhas).toHaveCount(RELATORIOS + 1 - 10);

  await relatorios.getByRole('columnheader', { name: /^Bateria de testes/ }).click();
  await expect(linhas).toHaveCount(10);
  await expect(linhas.first()).toContainText(`${prefixo} bateria 01`);
  await relatorios.getByRole('columnheader', { name: /^Bateria de testes/ }).click();
  await expect(linhas.first()).toContainText(`${prefixo} criado pelo projeto`);

  const endpoints = secao(page, 'Endpoints de Serviço');
  const alternar = endpoints.getByRole('button', { name: /^Endpoints de Serviço \(2\)$/ });
  await expect(alternar).toHaveAttribute('aria-expanded', 'true');
  await alternar.click();
  await expect(alternar).toHaveAttribute('aria-expanded', 'false');
  await expect(endpoints.locator('table')).toHaveCount(0);
  await page.reload();
  await expect(
    secao(page, 'Endpoints de Serviço').getByRole('button', { name: /^Endpoints de Serviço/ }),
  ).toHaveAttribute('aria-expanded', 'false');
  await expect(secao(page, 'Endpoints de Serviço').locator('table')).toHaveCount(0);
  await expect(secao(page, 'Relatórios de Teste').locator('tbody tr')).toHaveCount(10);
  // Abre de novo (e deixa assim para as próximas execuções).
  await secao(page, 'Endpoints de Serviço')
    .getByRole('button', { name: /^Endpoints de Serviço/ })
    .click();
  await expect(secao(page, 'Endpoints de Serviço').locator('tbody tr')).toHaveCount(2);
});
