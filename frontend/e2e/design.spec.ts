import { Page, expect, test } from '@playwright/test';
import { chamar, idPor, prefixoUnico, tokenDe } from './apoio/api';
import { habilitarNaListagem, limparE2E } from './apoio/limpeza';
import {
  acaoDaLinha,
  botaoMenuUsuario,
  campoSelecao,
  campoTexto,
  digitarData,
  escolherReferencia,
  entrarComo,
} from './apoio/sessao';
import { UsuarioTeste, abrirComo, criarUsuarioDeTeste } from './apoio/usuarios-teste';

/**
 * Etapa de design e usabilidade: área atual única, navegação com área de configurações, criar item
 * relacionado sem sair do formulário, tabelas largas (linha clicável, "⋯", sombra de rolagem), página Início, tema.
 * Os testes rodam EM SÉRIE e compartilham uma equipe "Equipe E2E-…", um GESTOR dela e uma segunda área de gestão ("Tarefas
 * E2E-…", chave com _e2e_). O helper compartilhado de limpeza desativa tudo no início e no fim.
 */
test.describe.configure({ mode: 'serial' });

const prefixo = prefixoUnico();
const GRUPO = 'testes_desempenho';
const AREA2 = `tarefas_e2e_${prefixo.toLowerCase().replace(/-/g, '_')}`;
const NOME_AREA2 = `Tarefas ${prefixo}`;
const NOME_EQUIPE = `Equipe ${prefixo}`;
const RTC_NOVO = `${prefixo}`.slice(0, 16) + '-RTC';
const BATERIA = `${prefixo} criar RTC no select`;
const admin = () => tokenDe('admin');

let equipe: string;
let gestor: UsuarioTeste;

/** A segunda área de gestão, pela API (só para PREPARAR): um tipo de item "Tarefa" por equipe, com o título. */
async function criarSegundaArea(): Promise<void> {
  const base = `/admin/metadados/grupos`;
  expect((await chamar(admin(), 'POST', base, { chave: AREA2, nome: NOME_AREA2 })).status).toBe(201);
  const versaoGrupo = async () => (await chamar<{ versao: string }>(admin(), 'GET', `${base}/${AREA2}`)).corpo.versao;
  const versaoTarefa = async () =>
    (
      await chamar<{ entidades: { chave: string; versao: string }[] }>(admin(), 'GET', `${base}/${AREA2}`)
    ).corpo.entidades.find((e) => e.chave === 'tarefa')!.versao;
  const tipo = await chamar(admin(), 'POST', `${base}/${AREA2}/entidades`, {
    versao: await versaoGrupo(),
    chave: 'tarefa',
    nome: 'Tarefa',
    nomePlural: 'Tarefas',
    escopoEquipe: true,
    icone: 'pi-check-square',
  });
  expect(tipo.status).toBe(201);
  const campo = await chamar(admin(), 'POST', `${base}/${AREA2}/entidades/tarefa/campos`, {
    versao: await versaoTarefa(),
    chave: 'titulo',
    nome: 'Título',
    tipoDado: 'TEXTO',
    tamanhoMaximo: 100,
    obrigatorio: true,
  });
  expect(campo.status).toBe(201);
}

/** A tabela da listagem: os atributos data-rolagem-* da SombraRolagemDirective dizem de que lado há colunas escondidas. */
function tabela(page: Page) {
  return page.locator('p-table[appsombrarolagem]').first();
}

test.beforeAll(async () => {
  test.setTimeout(300_000);
  await limparE2E(admin());
  const criada = await chamar<{ id: string }>(admin(), 'POST', '/equipes', { nome: NOME_EQUIPE });
  expect(criada.status).toBe(201);
  equipe = criada.corpo.id;
  await habilitarNaListagem(admin(), GRUPO, equipe);
  await criarSegundaArea();
  await habilitarNaListagem(admin(), AREA2, equipe);
  gestor = await criarUsuarioDeTeste(admin(), prefixo, 'ges', 'Gestor de design', 'GESTOR', [equipe]);
  const sigla = await idPor(admin(), 'sigla', 'codigo_sigla', 'PIX');
  const resposta = await chamar(admin(), 'POST', `/grupos/${GRUPO}/entidades/projeto/registros`, {
    equipeId: equipe,
    sigla,
    nome: `Projeto ${prefixo}`,
  });
  expect(resposta.status).toBe(201);
});

test.afterAll(async () => {
  await limparE2E(admin());
});

test('1. primeiro acesso a uma área: o Início dá as boas-vindas, com os atalhos e sem itens recentes', async ({
  browser,
}) => {
  const pagina = await abrirComo(browser, gestor.token, `/${AREA2}`);
  await expect(pagina.getByRole('heading', { level: 1 })).toContainText(/^(Bom dia|Boa tarde|Boa noite), Gestor/);
  await expect(pagina.getByText(`Você está na área de gestão ${NOME_AREA2}.`)).toBeVisible();
  const atalhos = pagina.locator('.grade-atalhos');
  await expect(atalhos.getByRole('link', { name: /Tarefas/ })).toBeVisible();
  await expect(atalhos).toContainText('0 itens');
  await expect(pagina.getByRole('button', { name: 'Novo: Tarefa' })).toBeVisible();
  await expect(pagina.getByText(`Boas-vindas à área de gestão ${NOME_AREA2}`)).toBeVisible();
  await pagina.context().close();
});

test('2. área atual única: na segunda área, Configurações da equipe já abre nela; trocar lá vale para a volta', async ({
  browser,
}) => {
  const pagina = await abrirComo(browser, gestor.token, `/${AREA2}`);
  const seletorCabecalho = campoSelecao(pagina.getByRole('banner'), 'Área de gestão');
  await expect(seletorCabecalho).toContainText(NOME_AREA2);

  // A engrenagem abre a área de configurações, com o menu próprio e o "Voltar para <área atual>".
  await pagina.getByRole('link', { name: 'Configurações', exact: true }).click();
  await expect(pagina).toHaveURL(/\/configuracoes\/equipe/);
  const menu = pagina.getByRole('navigation', { name: 'Menu de configurações' });
  await expect(menu.getByRole('link', { name: `Voltar para ${NOME_AREA2}` })).toBeVisible();
  const area = campoSelecao(pagina.getByRole('main'), 'Área de gestão');
  await expect(area).toContainText(NOME_AREA2);
  await expect(pagina).toHaveURL(new RegExp(`grupo=${AREA2}`));
  await expect(pagina.getByRole('cell', { name: 'Tarefas', exact: true })).toBeVisible();

  // Trocar a área aqui muda a área atual: o "Voltar" e o cabeçalho acompanham.
  await area.click();
  await pagina.getByRole('option', { name: 'Testes de Desempenho' }).click();
  await expect(pagina).toHaveURL(new RegExp(`grupo=${GRUPO}`));
  await expect(menu.getByRole('link', { name: 'Voltar para Testes de Desempenho' })).toBeVisible();
  await expect(seletorCabecalho).toContainText('Testes de Desempenho');
  await menu.getByRole('link', { name: 'Voltar para Testes de Desempenho' }).click();
  await expect(pagina).toHaveURL(new RegExp(`/${GRUPO}$`));
  await pagina.context().close();
});

test('3. criar um RTC novo pelo select do relatório, sem sair do formulário', async ({ browser }) => {
  const pagina = await abrirComo(browser, gestor.token, `/${GRUPO}/relatorio_teste/novo`);
  await campoTexto(pagina, /^Bateria de testes/).fill(BATERIA);
  await campoTexto(pagina, /^Tipo de teste/).fill('Carga');
  await escolherReferencia(pagina, /^Projeto/, prefixo.slice(0, 6), `Projeto ${prefixo}`);

  // O RTC ainda não existe: "+ Criar RTC '…'", no fim da lista.
  const rtc = campoSelecao(pagina, /^RTC/);
  await rtc.click();
  await rtc.pressSequentially(RTC_NOVO, { delay: 20 });
  await pagina.getByRole('option', { name: `+ Criar RTC '${RTC_NOVO}'` }).click();
  const dialogo = pagina.getByRole('dialog', { name: 'Novo: RTC' });
  await expect(dialogo).toBeVisible();
  // O texto digitado vem no campo de exibição; a equipe é a do relatório (a única do gestor nesta área).
  await expect(campoTexto(dialogo, /^ID externo do RTC/)).toHaveValue(RTC_NOVO);
  // Um nível só: dentro do diálogo, nenhum select oferece "Criar novo".
  await dialogo.getByRole('button', { name: 'Salvar' }).click();
  await expect(dialogo).toBeHidden();
  await expect(pagina.getByText(`Item criado e escolhido em “RTC”.`)).toBeVisible();

  // O formulário principal manteve o que já estava preenchido, e o RTC novo está escolhido.
  await expect(rtc).toHaveValue(RTC_NOVO);
  await expect(campoTexto(pagina, /^Bateria de testes/)).toHaveValue(BATERIA);
  await expect(campoSelecao(pagina, /^Projeto/)).toHaveValue(`Projeto ${prefixo}`);

  await escolherReferencia(pagina, /^Ambiente/, 'Homo', 'Homologação');
  await escolherReferencia(pagina, /^Status/, 'Aprov', /^Aprovado$/);
  await digitarData(pagina, /^Data de execução/, '01/10/2026 10:00');
  await campoTexto(pagina, /^Executado por/).fill('Equipe E2E');
  await pagina.getByRole('button', { name: /^Salvar\b/ }).click();
  await expect(pagina.getByRole('heading', { level: 1, name: BATERIA })).toBeVisible();
  await expect(pagina.getByRole('link', { name: RTC_NOVO })).toBeVisible();

  // O RTC existe na listagem dele.
  await pagina.goto(`/${GRUPO}/rtc?rtc_externo_id.contem=${encodeURIComponent(RTC_NOVO)}`);
  await expect(pagina.locator('tbody tr')).toHaveCount(1);
  await expect(pagina.locator('tbody tr').first()).toContainText(RTC_NOVO);
  await pagina.context().close();
});

test('4. Início: "Continuar de onde parou" mostra o relatório e o RTC que o gestor acabou de criar', async ({
  browser,
}) => {
  const pagina = await abrirComo(browser, gestor.token, `/${GRUPO}`);
  const recentes = pagina.locator('.lista-recentes');
  await expect(recentes.getByRole('link', { name: new RegExp(BATERIA) })).toBeVisible();
  await expect(recentes.getByRole('link', { name: new RegExp(RTC_NOVO) })).toBeVisible();
  await expect(recentes.locator('li').first()).toContainText('Relatório de Teste');
  await expect(recentes.locator('li').first()).toContainText(/agora mesmo|há \d+ minuto/);
  await recentes.getByRole('link', { name: new RegExp(RTC_NOVO) }).click();
  await expect(pagina.getByRole('heading', { level: 1, name: RTC_NOVO })).toBeVisible();
  await pagina.context().close();
});

test('5. linha clicável e ações no "⋯": clicar em qualquer ponto abre o item; o menu da linha edita', async ({
  browser,
}) => {
  const lista = `/${GRUPO}/relatorio_teste?bateria_testes.contem=${encodeURIComponent(BATERIA)}`;
  const pagina = await abrirComo(browser, gestor.token, lista);
  const linha = pagina.locator('tbody tr').first();
  await expect(pagina.locator('tbody tr')).toHaveCount(1);
  // A coluna principal é um link de verdade (teclado e leitor de tela); sem o ícone "ver".
  await expect(linha.getByRole('link', { name: BATERIA, exact: true })).toBeVisible();
  await expect(linha.getByRole('button', { name: /^Ver / })).toHaveCount(0);
  // Um clique numa célula sem link (a do ambiente, um cadastro de apoio, em texto) abre o detalhe.
  await linha.getByRole('cell', { name: 'Homologação', exact: true }).click();
  await expect(pagina.getByRole('heading', { level: 1, name: BATERIA })).toBeVisible();

  await pagina.goto(lista);
  await acaoDaLinha(pagina, BATERIA, 'Editar');
  await expect(pagina).toHaveURL(/\/editar$/);
  await expect(pagina.getByRole('heading', { level: 1, name: `Editar: ${BATERIA}` })).toBeVisible();
  await pagina.context().close();
});

test('6. com muitas colunas, a sombra indica o que está fora da área visível e some no fim', async ({ browser }) => {
  const pagina = await abrirComo(browser, gestor.token, `/${GRUPO}/relatorio_teste`);
  await expect(pagina.locator('tbody tr').first()).toBeVisible();
  // Na primeira visita, só as colunas que cabem: nada para rolar.
  await expect(pagina.getByRole('button', { name: /^Colunas visíveis: 6 de/ })).toBeVisible();
  await expect(tabela(pagina)).not.toHaveAttribute('data-rolagem-direita');

  // Mostra todas as colunas: agora há colunas à direita.
  await pagina.getByRole('button', { name: /^Colunas visíveis/ }).click();
  const seletor = pagina.getByRole('dialog', { name: 'Colunas visíveis' });
  // Espera o painel abrir antes de contar (sem isso, a contagem podia dar 0 e o laço nem rodava).
  await expect(seletor.getByRole('checkbox').first()).toBeVisible();
  const naoMarcadas = seletor.getByRole('checkbox', { checked: false });
  while ((await naoMarcadas.count()) > 0) {
    await naoMarcadas.first().check();
  }
  await pagina.keyboard.press('Escape');
  await expect(tabela(pagina)).toHaveAttribute('data-rolagem-direita');
  await expect(tabela(pagina)).not.toHaveAttribute('data-rolagem-esquerda');

  // Rolar até o fim: a sombra da direita some, a da esquerda aparece.
  await pagina.locator('.p-datatable-table-container').evaluate((e) => e.scrollTo({ left: e.scrollWidth }));
  await expect(tabela(pagina)).toHaveAttribute('data-rolagem-esquerda');
  await expect(tabela(pagina)).not.toHaveAttribute('data-rolagem-direita');

  // A escolha fica salva por tipo de item; "Restaurar o padrão" volta às que cabem.
  await pagina.reload();
  await expect(pagina.getByRole('button', { name: /^Colunas visíveis: (\d+) de \1 colunas/ })).toBeVisible();
  await pagina.getByRole('button', { name: /^Colunas visíveis/ }).click();
  await pagina.getByRole('button', { name: 'Restaurar o padrão' }).click();
  await pagina.keyboard.press('Escape');
  await expect(pagina.getByRole('button', { name: /^Colunas visíveis: 6 de/ })).toBeVisible();
  await pagina.context().close();
});

test('7. o menu lateral recolhido vira só ícones e continua assim depois de recarregar', async ({ browser }) => {
  const pagina = await abrirComo(browser, gestor.token, `/${GRUPO}`);
  const menu = pagina.getByRole('navigation', { name: 'Menu da área de gestão' });
  const relatorios = menu.getByRole('link', { name: 'Relatórios de Teste' });
  const larguraAberto = (await menu.boundingBox())!.width;

  await pagina.getByRole('button', { name: 'Recolher menu' }).click();
  await expect(pagina.getByRole('button', { name: 'Expandir menu' })).toBeVisible();
  await expect.poll(async () => (await menu.boundingBox())!.width).toBeLessThan(larguraAberto / 2);
  // Só o ícone na tela, mas o nome continua acessível (e na dica).
  await expect(relatorios).toBeVisible();
  await relatorios.hover();
  await expect(pagina.getByRole('tooltip')).toContainText('Relatórios de Teste');

  await pagina.reload();
  await expect(pagina.getByRole('button', { name: 'Expandir menu' })).toBeVisible();
  await expect.poll(async () => (await menu.boundingBox())!.width).toBeLessThan(larguraAberto / 2);
  await pagina.getByRole('button', { name: 'Expandir menu' }).click();
  await expect(pagina.getByRole('button', { name: 'Recolher menu' })).toBeVisible();
  await pagina.context().close();
});

test('8. as rotas antigas redirecionam para a área de configurações, mantendo os parâmetros', async ({ page }) => {
  await entrarComo(page, 'admin', '/admin/usuarios?busca=admin');
  await expect(page).toHaveURL(/\/configuracoes\/usuarios\?busca=admin$/);
  await expect(page.getByRole('heading', { level: 1, name: 'Usuários' })).toBeVisible();

  await page.goto(`/admin/listagens/${GRUPO}`);
  await expect(page).toHaveURL(new RegExp(`/configuracoes/areas/${GRUPO}$`));
  await expect(page.getByRole('heading', { level: 1, name: 'Testes de Desempenho' })).toBeVisible();

  await page.goto(`/configuracoes-equipe?grupo=${GRUPO}`);
  await expect(page).toHaveURL(new RegExp(`/configuracoes/equipe\\?.*grupo=${GRUPO}`));
  await expect(page.getByRole('heading', { level: 1, name: 'Configurações da equipe' })).toBeVisible();

  // Na área de configurações, o menu lateral é outro, com o "Voltar".
  const menu = page.getByRole('navigation', { name: 'Menu de configurações' });
  await expect(menu.getByRole('link', { name: 'Áreas de gestão' })).toBeVisible();
  await expect(menu.getByRole('link', { name: /^Voltar para / })).toBeVisible();
});

test('9. troca de tema pelo menu do usuário: escuro, claro e "seguir o sistema", lembrado ao recarregar', async ({
  browser,
}) => {
  const pagina = await abrirComo(browser, gestor.token, `/${GRUPO}`);
  const html = pagina.locator('html');
  const menu = pagina.getByRole('dialog', { name: 'Menu do usuário' });
  const fecharMenu = async () => {
    await pagina.keyboard.press('Escape');
    await expect(menu).toBeHidden();
  };
  const escolher = async (tema: 'Claro' | 'Escuro' | 'Seguir o sistema') => {
    await botaoMenuUsuario(pagina).click();
    await menu.getByRole('radio', { name: tema }).check();
    await fecharMenu();
  };

  await escolher('Escuro');
  await expect(html).toHaveClass(/catalogo-escuro/);
  await pagina.reload();
  await expect(html).toHaveClass(/catalogo-escuro/);
  await botaoMenuUsuario(pagina).click();
  await expect(menu.getByRole('radio', { name: 'Escuro' })).toBeChecked();
  await fecharMenu();

  await escolher('Claro');
  await expect(html).not.toHaveClass(/catalogo-escuro/);
  expect(await pagina.evaluate(() => localStorage.getItem('catalogo.tema'))).toBe('claro');

  // "Seguir o sistema" acompanha o prefers-color-scheme do sistema operacional.
  await pagina.emulateMedia({ colorScheme: 'dark' });
  await escolher('Seguir o sistema');
  await expect(html).toHaveClass(/catalogo-escuro/);
  await pagina.emulateMedia({ colorScheme: 'light' });
  await expect(html).not.toHaveClass(/catalogo-escuro/);
  await pagina.context().close();
});
