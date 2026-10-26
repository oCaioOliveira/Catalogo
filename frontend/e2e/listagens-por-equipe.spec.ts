import { Locator, Page, expect, test } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { GRUPO as GRUPO_TESTES, chamar, criar, idPor, listar, prefixoUnico, tokenDe, UsuarioDev } from './apoio/api';
import { habilitarNaListagem, limparE2E, mudarEquipesDaListagem } from './apoio/limpeza';
import { campo, campoSelecao, digitarData, entrarComo } from './apoio/sessao';
import { UsuarioTeste, abrirComo, criarUsuarioDeTeste } from './apoio/usuarios-teste';

/**
 * Listagens habilitadas por equipe (RN90 a RN93, ADR 0046). O arquivo cria as PRÓPRIAS equipes ("E2E-… T", de testes, habilitada
 * em Testes de Desempenho; "E2E-… D", de desenvolvimento), a listagem "Tasks de Desenvolvimento E2E-…" (pela interface, habilitada
 * só para D) e três usuários: um INTERNO só de T, um GESTOR de T e D e um EXTERNO só de D. As equipes e os usuários dev.* nunca são
 * alterados (o cenário 7 confere). Os cenários seguem uma história, em série. No início e no fim, o helper compartilhado
 * (e2e/apoio/limpeza.ts) desmonta as equipes, os usuários e as listagens de teste. Capturas em e2e/capturas/equipes-*.png.
 */
test.describe.configure({ mode: 'serial', timeout: 240_000 });

const prefixo = prefixoUnico();
const NOME_LISTAGEM = `Tasks de Desenvolvimento ${prefixo}`;
const GRUPO = `tasks_de_desenvolvimento_${prefixo.toLowerCase().replace(/-/g, '_')}`;
const NOME_T = `${prefixo} T`;
const NOME_D = `${prefixo} D`;
const admin = () => tokenDe('admin');

let equipeT = '';
let equipeD = '';
let interno: UsuarioTeste;
let gestor: UsuarioTeste;
let externoD: UsuarioTeste;
/** Os ids que cada usuário dev.* vê nos Testes de Desempenho antes deste arquivo (cenário 7). */
let retratoDev: Record<string, Record<string, string[]>> = {};

async function capturarTela(page: Page, nome: string): Promise<void> {
  for (const fechar of await page.locator('.p-toast-close-button').all()) {
    await fechar.click({ timeout: 1000 }).catch(() => undefined);
  }
  await page.waitForTimeout(600);
  const pasta = join(__dirname, 'capturas');
  mkdirSync(pasta, { recursive: true });
  await page.screenshot({ path: join(pasta, `${nome}.png`) });
}

async function escolherOpcao(page: Page, controle: Locator, opcao: string | RegExp): Promise<void> {
  await controle.click();
  await page.getByRole('option', { name: opcao, exact: typeof opcao === 'string' }).click();
}

/** Os nomes das opções de um p-select (abre, lê e fecha). */
async function opcoesDe(page: Page, controle: Locator): Promise<string[]> {
  const opcoes = page.getByRole('option');
  // Uma lista que ainda está fechando (animação) misturaria as opções de dois selects.
  await expect(opcoes).toHaveCount(0);
  await controle.click();
  await expect(opcoes.first()).toBeVisible();
  const nomes = (await opcoes.allTextContents()).map((t) => t.trim());
  await page.keyboard.press('Escape');
  await expect(opcoes).toHaveCount(0);
  return nomes;
}

async function gruposDe(token: string): Promise<string[]> {
  return (await chamar<{ chave: string }[]>(token, 'GET', '/grupos')).corpo.map((g) => g.chave);
}

/** Um relatório completo na equipe (com projeto e RTC dela), criado pelo administrador. `letra`: T ou D. */
async function criarRelatorioNaEquipe(equipeId: string, letra: string): Promise<string> {
  const bateria = `${prefixo} relatório de ${letra}`;
  const sigla = (await listar(admin(), 'sigla', { tamanho: '1' })).itens[0].id;
  const projeto = await criar(admin(), 'projeto', { equipeId, sigla, nome: `Projeto ${bateria}` });
  // O ID externo do RTC tem no máximo 20 caracteres.
  const rtc = await criar(admin(), 'rtc', { equipeId, rtc_externo_id: `${prefixo}${letra}`.slice(0, 20) });
  return criar(admin(), 'relatorio_teste', {
    equipeId,
    projeto,
    rtc,
    ambiente: await idPor(admin(), 'ambiente', 'nome', 'Homologação'),
    status_resultado: await idPor(admin(), 'status_resultado', 'descricao', 'Aprovado'),
    data_execucao: '2026-10-01T10:00:00-03:00',
    bateria_testes: bateria,
    tipo_teste: 'Carga',
    executado_por: 'Equipe E2E',
  });
}

/** Os ids de cada tipo de item de Testes de Desempenho que o usuário dev.* vê (até 100 por tipo, mais o total). */
async function retratoDe(usuario: UsuarioDev): Promise<Record<string, string[]>> {
  const token = tokenDe(usuario);
  const entidades = (await chamar<{ chave: string }[]>(token, 'GET', `/grupos/${GRUPO_TESTES}/entidades`)).corpo;
  const retrato: Record<string, string[]> = {};
  for (const entidade of entidades) {
    const pagina = (
      await chamar<{ total: number; itens: { id: string }[] }>(
        token,
        'GET',
        `/grupos/${GRUPO_TESTES}/entidades/${entidade.chave}/registros?tamanho=100&ordenacao=criadoEm`,
      )
    ).corpo;
    retrato[entidade.chave] = [`total:${pagina.total}`, ...pagina.itens.map((i) => i.id)];
  }
  return retrato;
}

const USUARIOS_DEV: UsuarioDev[] = ['dev.externo', 'dev.interno', 'dev.gestor'];

test.beforeAll(async () => {
  // Três logins: se outro arquivo já gastou o limite do minuto (RN23), um deles espera 61 s.
  test.setTimeout(300_000);
  await limparE2E(admin());
  for (const usuario of USUARIOS_DEV) {
    retratoDev[usuario] = await retratoDe(usuario);
  }
  const t = await chamar<{ id: string }>(admin(), 'POST', '/equipes', { nome: NOME_T });
  const d = await chamar<{ id: string }>(admin(), 'POST', '/equipes', { nome: NOME_D });
  expect([t.status, d.status], 'POST /equipes').toEqual([201, 201]);
  [equipeT, equipeD] = [t.corpo.id, d.corpo.id];
  // RN90: equipe nova não é habilitada em nenhuma listagem. T é a equipe de testes: habilitada em Testes de Desempenho.
  await habilitarNaListagem(admin(), GRUPO_TESTES, equipeT);
  await criarRelatorioNaEquipe(equipeT, 'T');

  interno = await criarUsuarioDeTeste(admin(), prefixo, 'int', 'Interno de T', 'INTERNO', [equipeT]);
  gestor = await criarUsuarioDeTeste(admin(), prefixo, 'ges', 'Gestor de T e D', 'GESTOR', [equipeT, equipeD]);
  externoD = await criarUsuarioDeTeste(admin(), prefixo, 'extd', 'Externo de D', 'EXTERNO', [equipeD]);
});

test.afterAll(async () => {
  // O helper compartilhado: usuários e equipes de teste desativados, equipes tiradas das listagens, listagem de Tasks desativada.
  await limparE2E(admin());
});

test('1. o ADMINISTRADOR cria a listagem de Tasks pela interface; sem equipes, só ele a vê, com a faixa de aviso (RN93)', async ({
  page,
  browser,
}) => {
  await entrarComo(page, 'admin', '/configuracoes/areas');
  await page.getByRole('button', { name: 'Nova área de gestão' }).click();
  const dialogo = page.getByRole('dialog');
  await campo(dialogo, /^Nome/).fill(NOME_LISTAGEM);
  await expect(campo(dialogo, 'Chave')).toHaveValue(GRUPO);
  await dialogo.getByRole('button', { name: 'Criar área de gestão' }).click();
  await expect(page.getByRole('heading', { level: 1, name: NOME_LISTAGEM })).toBeVisible();

  // Listagem nova nasce sem equipes: a faixa, com o atalho para a aba Equipes.
  const faixa = page.locator('.faixa-sem-equipes');
  await expect(faixa).toContainText('Nenhuma equipe habilitada: só administradores veem esta área de gestão.');
  await capturarTela(page, 'equipes-05-faixa-sem-equipes');

  // O tipo de item "task": título, status (LISTA), responsável e prazo (DATA).
  await page.getByRole('button', { name: 'Novo tipo de item' }).click();
  await campo(page.getByRole('dialog'), /^Nome \(singular\)/).fill('Task');
  await campo(page.getByRole('dialog'), 'Nome no plural').fill('Tasks');
  await page.getByRole('dialog').getByRole('button', { name: 'Criar', exact: true }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Task' })).toBeVisible();
  await page.getByRole('tab', { name: 'Campos' }).click();
  const painel = page.locator('.painel-lateral-campo');
  for (const novo of [
    { nome: 'Título', obrigatorio: true },
    { nome: 'Status', tipo: 'Lista de opções', opcoes: ['A fazer', 'Em andamento', 'Concluída'] },
    { nome: 'Responsável' },
    { nome: 'Prazo', tipo: 'Data' },
  ]) {
    await page.getByRole('button', { name: 'Novo campo' }).click();
    await campo(painel, /^Nome/).fill(novo.nome);
    if (novo.tipo) {
      await escolherOpcao(page, campoSelecao(painel, 'Tipo'), novo.tipo);
    }
    if (novo.obrigatorio) {
      await painel.getByRole('switch', { name: 'Obrigatório' }).check();
    }
    for (const [i, opcao] of (novo.opcoes ?? []).entries()) {
      await painel.getByRole('button', { name: 'Adicionar opção' }).click();
      await painel.getByRole('textbox', { name: `Opção ${i + 1}` }).fill(opcao);
    }
    await painel.getByRole('button', { name: 'Criar campo' }).click();
    await expect(painel).toBeHidden();
  }
  await expect(page.locator('.tabela-admin tbody tr')).toHaveCount(4);
  await page.getByRole('tab', { name: 'Geral' }).click();
  await escolherOpcao(page, campoSelecao(page, 'Campo de exibição'), 'Título');
  await page.getByRole('button', { name: 'Salvar alterações' }).click();
  await expect(page.getByText('Alterar "Task": feito.')).toBeVisible();

  // Ninguém mais a vê: nem o GESTOR de T e D (a API nem a devolve), nem o EXTERNO só de D, que fica sem nenhuma listagem.
  expect(await gruposDe(gestor.token)).not.toContain(GRUPO);
  expect((await chamar(gestor.token, 'GET', `/grupos/${GRUPO}/entidades`)).status).toBe(404);
  const paginaExterno = await abrirComo(browser, externoD.token, '/');
  await expect(paginaExterno).toHaveURL(/\/sem-listagens$/);
  await expect(
    paginaExterno.getByRole('heading', { level: 1, name: 'Nenhuma área de gestão disponível' }),
  ).toBeAttached();
  await expect(paginaExterno.getByText('Nenhuma área de gestão disponível para as suas equipes')).toBeVisible();
  await expect(paginaExterno.getByText('Fale com o administrador do sistema')).toBeVisible();
  await capturarTela(paginaExterno, 'equipes-04-sem-listagens');
  await paginaExterno.context().close();

  // O administrador também vê a faixa na tela de uso; o atalho leva à aba Equipes, onde ele habilita só D.
  await page.goto(`/${GRUPO}/task`);
  await expect(page.locator('.faixa-sem-equipes')).toBeVisible();
  await page.locator('.faixa-sem-equipes').getByRole('link', { name: 'Habilitar equipes' }).click();
  await expect(page).toHaveURL(new RegExp(`/configuracoes/areas/${GRUPO}\\?aba=equipes$`));
  await page.getByRole('checkbox', { name: NOME_D, exact: true }).check();
  await page.getByRole('button', { name: 'Salvar alterações' }).click();
  await expect(page.getByText('Alterar as equipes da área de gestão: feito.')).toBeVisible();
  await expect(page.locator('.faixa-sem-equipes')).toHaveCount(0);
});

test('2. o INTERNO só de T não vê a listagem de Tasks: nem no seletor, nem pelo endereço direto', async ({
  browser,
}) => {
  expect(await gruposDe(interno.token)).toEqual([GRUPO_TESTES]);
  const pagina = await abrirComo(browser, interno.token, '/');
  await expect(pagina).toHaveURL(new RegExp(`/${GRUPO_TESTES}$`));
  // Uma área só: o cabeçalho mostra só o nome, sem seletor.
  await expect(campoSelecao(pagina, 'Área de gestão')).toHaveCount(0);
  await expect(pagina.locator('.area-unica')).toContainText('Testes de Desempenho');

  await pagina.goto(`/${GRUPO}/task`);
  await expect(pagina.getByRole('heading', { level: 1, name: 'Página não encontrada' })).toBeVisible();
  await expect(pagina).toHaveURL(new RegExp(`/${GRUPO}/task$`));
  expect((await chamar(interno.token, 'GET', `/grupos/${GRUPO}/entidades/task/registros`)).status).toBe(404);
  await pagina.context().close();
});

test('3. o GESTOR de T e D alterna entre as listagens; ao criar, a equipe é só a dele naquela listagem', async ({
  browser,
}) => {
  const pagina = await abrirComo(browser, gestor.token, `/${GRUPO_TESTES}/relatorio_teste`);
  const seletor = campoSelecao(pagina, 'Área de gestão');
  await seletor.click();
  await expect(pagina.getByRole('option')).toHaveText(['Tasks de Desenvolvimento ' + prefixo, 'Testes de Desempenho']);
  await capturarTela(pagina, 'equipes-02-seletor-gestor');
  await pagina.getByRole('option', { name: NOME_LISTAGEM }).click();
  // Trocar a área no cabeçalho abre o Início dela.
  await expect(pagina).toHaveURL(new RegExp(`/${GRUPO}$`));

  // Em Tasks, a única equipe dele habilitada é D: ela é usada sozinha, sem select.
  await pagina.getByRole('button', { name: 'Novo: Task' }).click();
  await expect(campoSelecao(pagina, /^Equipe/)).toHaveCount(0);
  await expect(pagina.locator('.info-equipe')).toHaveText(`Equipe: ${NOME_D}`);
  await campo(pagina, /^Título/).fill(`${prefixo} Revisar o pipeline`);
  await escolherOpcao(pagina, campoSelecao(pagina, 'Status'), 'Em andamento');
  await campo(pagina, 'Responsável').fill('Gestor E2E');
  await digitarData(pagina, 'Prazo', '30/11/2026');
  await pagina.getByRole('button', { name: /^Salvar\b/ }).click();
  await expect(pagina.getByRole('heading', { level: 1, name: `${prefixo} Revisar o pipeline` })).toBeVisible();
  // O detalhe mostra a equipe do item.
  await expect(pagina.locator('.info-equipe')).toHaveText(`Equipe: ${NOME_D}`);
  const segunda = await chamar(gestor.token, 'POST', `/grupos/${GRUPO}/entidades/task/registros`, {
    titulo: `${prefixo} Atualizar dependências`,
    status: 'A fazer',
  });
  expect(segunda.status, 'POST task sem equipeId: a única dele na listagem (RN91)').toBe(201);

  // Volta para Testes de Desempenho: lá, a equipe dele é só T.
  await campoSelecao(pagina, 'Área de gestão').click();
  await pagina.getByRole('option', { name: 'Testes de Desempenho' }).click();
  await expect(pagina).toHaveURL(new RegExp(`/${GRUPO_TESTES}$`));
  await pagina.getByRole('button', { name: 'Novo: Relatório de Teste' }).click();
  await expect(pagina.locator('.info-equipe')).toHaveText(`Equipe: ${NOME_T}`);
  await expect(campoSelecao(pagina, /^Equipe/)).toHaveCount(0);
  // A API confere: D não está habilitada em Testes de Desempenho, então não é "equipe dele" ali (RN91: 403, como uma equipe alheia).
  const emD = await chamar(gestor.token, 'POST', `/grupos/${GRUPO_TESTES}/entidades/relatorio_teste/registros`, {
    equipeId: equipeD,
    bateria_testes: 'não deve gravar',
  });
  expect(emD.status).toBe(403);
  await pagina.context().close();
});

test('4. com T e D habilitadas em Testes, o GESTOR vê a coluna e o filtro de Equipe (só as dele); o INTERNO só de T, não', async ({
  browser,
}) => {
  await habilitarNaListagem(admin(), GRUPO_TESTES, equipeD);
  try {
    await criarRelatorioNaEquipe(equipeD, 'D');
    const lista = `/${GRUPO_TESTES}/relatorio_teste?bateria_testes.contem=${prefixo}`;
    const pagina = await abrirComo(browser, gestor.token, lista);
    const linhas = pagina.locator('tbody tr');
    await expect(linhas).toHaveCount(2);
    await expect(pagina.getByRole('columnheader', { name: 'Equipe', exact: true })).toBeVisible();
    await expect(pagina.locator('td.celula-equipe')).toHaveText([NOME_D, NOME_T].sort());

    // O filtro oferece só as equipes dele nesta listagem e fica na URL.
    await pagina.getByRole('button', { name: /^Filtros/ }).click();
    const filtro = campoSelecao(pagina, 'Equipe');
    expect((await opcoesDe(pagina, filtro)).sort()).toEqual([NOME_D, NOME_T].sort());
    await escolherOpcao(pagina, filtro, NOME_D);
    await pagina.getByRole('button', { name: 'Filtrar', exact: true }).click();
    await expect(pagina).toHaveURL(new RegExp(`equipeId=${equipeD}`));
    await expect(linhas).toHaveCount(1);
    await expect(linhas.first()).toContainText(NOME_D);
    await capturarTela(pagina, 'equipes-03-coluna-e-filtro');
    await pagina.reload();
    await expect(linhas).toHaveCount(1);
    await expect(linhas.first()).toContainText(`${prefixo} relatório de D`);

    // A coluna entra no seletor de colunas (e vem marcada).
    await pagina.getByRole('button', { name: /^Colunas visíveis/ }).click();
    await expect(
      pagina.getByRole('dialog', { name: 'Colunas visíveis' }).getByRole('checkbox', { name: 'Equipe', exact: true }),
    ).toBeChecked();
    await pagina.keyboard.press('Escape');

    // Painel: o filtro de equipe também só tem as dele nesta listagem.
    await pagina.goto(`/painel/${GRUPO_TESTES}`);
    expect(await opcoesDe(pagina, campoSelecao(pagina, 'Equipe'))).toEqual(['Minhas equipes', NOME_D, NOME_T]);
    await pagina.context().close();

    // O INTERNO só de T: uma equipe só na listagem, sem coluna e sem filtro de Equipe.
    const doInterno = await abrirComo(browser, interno.token, lista);
    await expect(doInterno.locator('tbody tr')).toHaveCount(1);
    await expect(doInterno.getByRole('columnheader', { name: 'Equipe', exact: true })).toHaveCount(0);
    await doInterno.getByRole('button', { name: /^Filtros/ }).click();
    await expect(doInterno.getByRole('button', { name: 'Filtrar', exact: true })).toBeVisible();
    await expect(campoSelecao(doInterno, 'Equipe')).toHaveCount(0);
    await doInterno.context().close();
  } finally {
    // D volta a não ser habilitada em Testes de Desempenho (o relatório dela fica oculto, RN92).
    await mudarEquipesDaListagem(admin(), GRUPO_TESTES, { tirar: [equipeD] });
  }
});

test('5. configurações da equipe, painel e fila de aprovação só oferecem as equipes do GESTOR em cada listagem', async ({
  browser,
}) => {
  const pagina = await abrirComo(browser, gestor.token, '/configuracoes/equipe');
  // O cabeçalho também tem um seletor de área: os da tela ficam no conteúdo principal.
  const conteudo = pagina.getByRole('main');
  const listagem = campoSelecao(conteudo, 'Área de gestão');
  const equipe = campoSelecao(conteudo, 'Equipe');
  expect((await opcoesDe(pagina, listagem)).sort()).toEqual([NOME_LISTAGEM, 'Testes de Desempenho'].sort());
  await escolherOpcao(pagina, listagem, NOME_LISTAGEM);
  await expect(pagina).toHaveURL(new RegExp(`grupo=${GRUPO}`));
  await expect(pagina).toHaveURL(new RegExp(`equipeId=${equipeD}`));
  expect(await opcoesDe(pagina, equipe)).toEqual([NOME_D]);
  await expect(pagina.getByRole('cell', { name: 'Tasks', exact: true })).toBeVisible();
  await escolherOpcao(pagina, listagem, 'Testes de Desempenho');
  await expect(pagina).toHaveURL(new RegExp(`equipeId=${equipeT}`));
  expect(await opcoesDe(pagina, equipe)).toEqual([NOME_T]);
  // Pedir pela URL uma equipe que não está habilitada na área: a tela troca pela que está.
  await pagina.goto(`/configuracoes/equipe?equipeId=${equipeD}&grupo=${GRUPO_TESTES}`);
  await expect(pagina).toHaveURL(new RegExp(`equipeId=${equipeT}`));

  // Painel de Tasks: ele tem uma equipe só ali, sem filtro de equipe.
  await pagina.goto(`/painel/${GRUPO}`);
  await expect(pagina.getByRole('heading', { level: 1 })).toBeVisible();
  await expect(campoSelecao(conteudo, 'Equipe')).toHaveCount(0);

  // Fila: abre na ÁREA ATUAL (Tasks, a do painel), com a opção "Todas as áreas"; em Tasks, a equipe é só D.
  await pagina.goto('/aprovacoes');
  await expect(pagina).toHaveURL(new RegExp(`grupo=${GRUPO}`));
  expect(await opcoesDe(pagina, campoSelecao(conteudo, 'Área de gestão'))).toEqual([
    'Todas as áreas',
    ...[NOME_LISTAGEM, 'Testes de Desempenho'].sort(),
  ]);
  expect(await opcoesDe(pagina, campoSelecao(conteudo, 'Equipe'))).toEqual([NOME_D]);
  await escolherOpcao(pagina, campoSelecao(conteudo, 'Área de gestão'), 'Todas as áreas');
  await expect(pagina).toHaveURL(/todas=1/);
  expect((await opcoesDe(pagina, campoSelecao(conteudo, 'Equipe'))).sort()).toEqual([NOME_D, NOME_T].sort());
  await pagina.context().close();
});

test('6. tirar D da listagem de Tasks: a simulação diz quantos itens ficam ocultos; o GESTOR deixa de ver; reabilitar devolve tudo', async ({
  page,
  browser,
}) => {
  await entrarComo(page, 'admin', `/configuracoes/areas/${GRUPO}?aba=equipes`);
  const caixaD = page.getByRole('checkbox', { name: NOME_D, exact: true });
  await expect(caixaD).toBeChecked();
  await caixaD.uncheck();
  await expect(page.getByText(`2 itens da equipe ${NOME_D} ficarão ocultos para os membros dela.`)).toBeVisible();
  await page.getByRole('button', { name: 'Salvar alterações' }).click();
  const dialogo = page.locator('.dialogo-alteracao');
  await expect(dialogo).toContainText(`Os itens da equipe '${NOME_D}' ficam ocultos para os membros dela`);
  await expect(dialogo).toContainText('Itens que ficam ocultos: 2.');
  await expect(dialogo).toContainText('voltam ao habilitar a equipe de novo');
  await capturarTela(page, 'equipes-01-simulacao');
  await page.getByRole('button', { name: 'Confirmar' }).click();
  await expect(page.getByText('Alterar as equipes da área de gestão: feito.')).toBeVisible();
  await expect(page.locator('.faixa-sem-equipes')).toBeVisible();

  // O GESTOR deixa de ver a listagem (404 em tudo sob ela); o EXTERNO só de D fica sem nenhuma.
  expect(await gruposDe(gestor.token)).toEqual([GRUPO_TESTES]);
  expect((await chamar(gestor.token, 'GET', `/grupos/${GRUPO}/entidades/task/registros`)).status).toBe(404);
  const doGestor = await abrirComo(browser, gestor.token, `/${GRUPO}/task`);
  await expect(doGestor.getByRole('heading', { level: 1, name: 'Página não encontrada' })).toBeVisible();
  await doGestor.context().close();
  const doExterno = await abrirComo(browser, externoD.token, '/');
  await expect(doExterno).toHaveURL(/\/sem-listagens$/);
  await doExterno.context().close();

  // Nada foi apagado: o administrador continua vendo as duas tasks, como de equipe não habilitada.
  const vistas = await chamar<{ total: number }>(admin(), 'GET', `/grupos/${GRUPO}/entidades/task/registros`);
  expect(vistas.corpo.total).toBe(2);
  await page.goto(`/${GRUPO}/task`);
  await expect(page.locator('td.celula-equipe')).toHaveText(['Equipe não habilitada', 'Equipe não habilitada']);

  // Reabilitar devolve tudo.
  await page.goto(`/configuracoes/areas/${GRUPO}?aba=equipes`);
  await page.getByRole('checkbox', { name: NOME_D, exact: true }).check();
  await page.getByRole('button', { name: 'Salvar alterações' }).click();
  await expect(page.getByText('Alterar as equipes da área de gestão: feito.')).toBeVisible();
  const deNovo = await chamar<{ total: number }>(gestor.token, 'GET', `/grupos/${GRUPO}/entidades/task/registros`);
  expect(deNovo.corpo.total).toBe(2);
  const outraVez = await abrirComo(browser, gestor.token, `/${GRUPO}/task`);
  await expect(outraVez.locator('tbody tr')).toHaveCount(2);
  await outraVez.context().close();
});

test('7. regressão: os usuários dev.* veem os Testes de Desempenho com os mesmos itens, agora sem a aba Avaliações', async ({
  page,
}) => {
  for (const usuario of USUARIOS_DEV) {
    expect(await gruposDe(tokenDe(usuario)), usuario).toContain(GRUPO_TESTES);
    expect(await gruposDe(tokenDe(usuario)), usuario).not.toContain(GRUPO);
    expect(await retratoDe(usuario), usuario).toEqual(retratoDev[usuario]);
    // Nenhum critério de avaliação ativo nas equipes deles (os modelos da Alfa e da Beta foram desligados).
    const entidades = (
      await chamar<{ chave: string; criteriosAvaliacao: unknown[] }[]>(
        tokenDe(usuario),
        'GET',
        `/grupos/${GRUPO_TESTES}/entidades`,
      )
    ).corpo;
    expect(entidades.find((e) => e.chave === 'relatorio_teste')?.criteriosAvaliacao ?? [], usuario).toEqual([]);
  }

  await entrarComo(page, 'dev.gestor', `/${GRUPO_TESTES}/relatorio_teste`);
  await expect(page.getByRole('columnheader', { name: 'Equipe', exact: true })).toHaveCount(0);
  await page.locator('tbody tr').first().getByRole('link').first().click();
  await expect(page.getByRole('tab', { name: 'Dados' })).toBeVisible();
  await expect(page.getByRole('tab', { name: 'Histórico' })).toBeVisible();
  await expect(page.getByRole('tab', { name: 'Avaliações' })).toHaveCount(0);
});
