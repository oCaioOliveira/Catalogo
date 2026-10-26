import { Browser, BrowserContext, Page, expect, test } from '@playwright/test';
import { cadastrosAlfa, chamar, criar, criarRelatorio, entrarPelaApi, idPor, prefixoUnico, tokenDe } from './apoio/api';
import { habilitarNaListagem, limparE2E } from './apoio/limpeza';
import { acaoDaLinha, botaoMenuUsuario, capturar, entrarComo, entrarPelaTela, noMenuDoUsuario } from './apoio/sessao';

/**
 * Administração de usuários e equipes, e "Minha conta" (docs/frontend.md → "Administração"). Os testes rodam EM SÉRIE e
 * compartilham os dados criados aqui, sempre com um prefixo único. Os usuários dev.* não têm a senha trocada: a troca é feita
 * pelo usuário criado no próprio teste. Capturas em e2e/capturas/admin-*.png (a senha provisória sai MASCARADA).
 * No início e no fim, o helper compartilhado (e2e/apoio/limpeza.ts) desativa a equipe e os usuários de teste.
 */
test.describe.configure({ mode: 'serial', timeout: 240_000 });

test.beforeAll(async () => {
  await limparE2E(tokenDe('admin'));
});

test.afterAll(async () => {
  await limparE2E(tokenDe('admin'));
});

const prefixo = prefixoUnico();
const nomeEquipe = `${prefixo} Squad`;
const matricula = `${prefixo.toLowerCase()}-u1`;
const nomeUsuario = `Usuária ${prefixo}`;
/** Todas as senhas que aparecem no teste: nenhuma pode estar no histórico. */
const senhasVistas: string[] = [];
let senhaAtual = '';
let equipeId = '';
let usuarioId = '';
let relatorioEquipeNova = '';
let relatorioAlfa = '';

async function novoContexto(browser: Browser): Promise<{ contexto: BrowserContext; page: Page }> {
  const contexto = await browser.newContext({
    locale: 'pt-BR',
    timezoneId: 'America/Sao_Paulo',
    reducedMotion: 'reduce',
  });
  return { contexto, page: await contexto.newPage() };
}

async function escolher(page: Page, rotulo: string | RegExp, opcao: string | RegExp): Promise<void> {
  await page.getByRole('combobox', { name: rotulo }).click();
  await page.getByRole('option', { name: opcao }).first().click();
}

/** Espera o fim da animação de entrada de um overlay do PrimeNG (que não segue o reduced-motion), para a captura sair nítida. */
async function esperarOpaco(alvo: ReturnType<Page['locator']>): Promise<void> {
  await expect.poll(() => alvo.evaluate((e) => getComputedStyle(e.closest('.p-dialog') ?? e).opacity)).toBe('1');
}

/** A confirmação aberta (o layout tem mais de um p-confirmdialog no DOM; só um fica visível). */
function confirmacaoAberta(page: Page) {
  return page.getByRole('alertdialog').filter({ visible: true });
}

/**
 * Abre um p-multiselect achado pelo NOME ACESSÍVEL: o elemento com role="combobox" é um input visualmente oculto (é ele que o
 * leitor de tela anuncia), então o clique vai no componente em volta dele.
 */
async function abrirMultiselect(page: Page, rotulo: string | RegExp): Promise<void> {
  await page.getByRole('combobox', { name: rotulo }).locator('xpath=ancestor::p-multiselect[1]').click();
}

/** A senha provisória do diálogo (o "Copiar" põe na área de transferência; o teste confere as duas). */
async function lerSenhaDoDialogo(page: Page): Promise<string> {
  const dialogo = page.getByRole('dialog', { name: 'Senha provisória' });
  await expect(dialogo).toBeVisible();
  await expect(dialogo).toContainText('Esta senha não será exibida novamente. Envie ao usuário por um canal seguro.');
  const senha = (await dialogo.locator('[data-senha-provisoria]').textContent())!.trim();
  expect(senha).toHaveLength(16);
  await dialogo.getByRole('button', { name: 'Copiar' }).click();
  await expect(dialogo.getByText('Senha copiada.')).toBeVisible();
  expect(await page.evaluate(() => navigator.clipboard.readText())).toBe(senha);
  senhasVistas.push(senha);
  return senha;
}

/** Primeiro acesso (ou depois da redefinição): entra com a provisória e troca por uma definitiva (RN25). */
async function trocarSenhaObrigatoria(page: Page, provisoria: string): Promise<string> {
  await entrarPelaTela(page, matricula, provisoria);
  await expect(page).toHaveURL(/\/trocar-senha/);
  await expect(page.getByText('troque a senha provisória')).toBeVisible();
  const nova = `Nova${Date.now()}x`;
  await page.locator('#senha-atual').fill(provisoria);
  await page.locator('#nova-senha').fill(nova);
  await page.locator('#confirmacao').fill(nova);
  await page.getByRole('button', { name: 'Trocar senha' }).click();
  await expect(page).not.toHaveURL(/\/trocar-senha/);
  senhasVistas.push(nova);
  return nova;
}

test('1. o ADMINISTRADOR cria uma equipe e um usuário INTERNO em duas equipes, copia a senha provisória e sai', async ({
  page,
  context,
}) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await entrarComo(page, 'admin', '/');
  // A engrenagem do cabeçalho leva à área de configurações, com menu lateral próprio.
  await page.getByRole('link', { name: 'Configurações', exact: true }).click();
  const menu = page.getByRole('navigation', { name: 'Menu de configurações' });
  await menu.getByRole('link', { name: 'Equipes', exact: true }).click();
  await page.getByRole('button', { name: 'Nova equipe' }).click();
  await page.getByRole('textbox', { name: /^Nome/ }).fill(nomeEquipe);
  await page.getByRole('button', { name: 'Criar equipe' }).click();
  await expect(page.getByRole('heading', { name: nomeEquipe })).toBeVisible();
  equipeId = page.url().split('/').pop()!;

  // Nome repetido: o erro aparece no campo.
  await page.goto('/configuracoes/equipes/novo');
  await page.getByRole('textbox', { name: /^Nome/ }).fill(nomeEquipe);
  await page.getByRole('button', { name: 'Criar equipe' }).click();
  await expect(page.locator('#equipe-nome-erros')).toContainText('Já existe uma equipe');

  await menu.getByRole('link', { name: 'Usuários', exact: true }).click();
  await page.getByRole('button', { name: 'Novo usuário' }).click();
  await page.getByRole('textbox', { name: /^Matrícula/ }).fill(matricula);
  await page.getByRole('textbox', { name: /^Nome/ }).fill(nomeUsuario);
  await escolher(page, /^Perfil/, /^Interno/);
  await expect(page.locator('#usuario-perfil-descricao')).toHaveText('Cria e edita.');
  // ADMINISTRADOR: o campo de equipes some, com a explicação.
  await escolher(page, /^Perfil/, /^Administrador/);
  await expect(page.getByRole('combobox', { name: /^Equipes/ })).toHaveCount(0);
  await expect(page.getByText('Administradores têm acesso a todas as equipes.')).toBeVisible();
  await escolher(page, /^Perfil/, /^Interno/);
  // Sem equipe: o erro aparece no campo, antes de ir para a API.
  await page.getByRole('button', { name: 'Criar usuário' }).click();
  await expect(page.locator('#usuario-equipes-erros')).toContainText('pelo menos uma equipe');
  await abrirMultiselect(page, /^Equipes/);
  await page.getByRole('option', { name: nomeEquipe }).click();
  await page.getByRole('option', { name: 'Equipe Alfa' }).click();
  await page.keyboard.press('Escape');
  await expect(page.getByRole('listbox')).toHaveCount(0);
  await capturar(page, 'admin-02-novo-usuario');

  await page.getByRole('button', { name: 'Criar usuário' }).click();
  const provisoria = await lerSenhaDoDialogo(page);
  // O diálogo não fecha com Esc: só pelo botão.
  await page.keyboard.press('Escape');
  await expect(page.getByRole('dialog', { name: 'Senha provisória' })).toBeVisible();
  await page.screenshot({
    path: `${__dirname}/capturas/admin-03-senha-provisoria.png`,
    mask: [page.locator('[data-senha-provisoria]')],
  });
  await page.getByRole('button', { name: 'Já copiei' }).click();
  await expect(page.getByRole('dialog', { name: 'Senha provisória' })).toHaveCount(0);
  await expect(page.getByRole('heading', { name: nomeUsuario })).toBeVisible();
  await expect(page.locator('body')).not.toContainText(provisoria);
  usuarioId = page.url().split('/').pop()!;
  senhaAtual = provisoria;

  await noMenuDoUsuario(page, 'Sair');
  await expect(page).toHaveURL(/\/login/);
});

test('2. o novo usuário entra com a senha provisória, troca a senha e vê os itens das duas equipes', async ({
  page,
}) => {
  // Um relatório em cada equipe do usuário (pela API, como administrador). A equipe nova não é habilitada em nenhuma listagem
  // (RN90): habilita em Testes de Desempenho.
  const admin = tokenDe('admin');
  await habilitarNaListagem(admin, 'testes_desempenho', equipeId);
  const c = await cadastrosAlfa(admin);
  const sigla = await idPor(admin, 'sigla', 'codigo_sigla', 'PIX');
  const projeto = await criar(admin, 'projeto', { equipeId, sigla, nome: `${prefixo} Projeto` });
  const rtc = await criar(admin, 'rtc', { equipeId, rtc_externo_id: `${prefixo}`.slice(0, 20) });
  relatorioEquipeNova = await criar(admin, 'relatorio_teste', {
    equipeId,
    projeto,
    rtc,
    ambiente: c.ambiente,
    status_resultado: c.status,
    data_execucao: '2026-10-01T10:00:00-03:00',
    bateria_testes: `${prefixo} equipe nova`,
    tipo_teste: 'Carga',
    executado_por: 'Teste E2E',
  });
  relatorioAlfa = await criarRelatorio(admin, `${prefixo} equipe alfa`);

  senhaAtual = await trocarSenhaObrigatoria(page, senhaAtual);
  await page.goto(`/testes_desempenho/relatorio_teste?bateria_testes.contem=${prefixo}`);
  await expect(page.locator('tbody tr')).toHaveCount(2);
  await expect(page.getByRole('cell', { name: `${prefixo} equipe nova`, exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: `${prefixo} equipe alfa`, exact: true })).toBeVisible();
  // INTERNO não vê a área de configurações (a engrenagem do cabeçalho).
  await expect(page.getByRole('link', { name: 'Configurações', exact: true })).toHaveCount(0);
});

test('3. o ADMINISTRADOR redefine a senha: a antiga deixa de funcionar e a nova obriga a troca', async ({
  page,
  context,
  browser,
}) => {
  await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  await entrarComo(page, 'admin', `/configuracoes/usuarios/${usuarioId}`);
  await page.getByRole('button', { name: 'Redefinir senha' }).click();
  await confirmacaoAberta(page).getByRole('button', { name: 'Redefinir senha' }).click();
  const provisoria = await lerSenhaDoDialogo(page);
  await page.getByRole('button', { name: 'Já copiei' }).click();
  await expect(page.locator('body')).not.toContainText(provisoria);

  const { contexto, page: usuario } = await novoContexto(browser);
  await entrarPelaTela(usuario, matricula, senhaAtual);
  await expect(usuario.getByText('Matrícula ou senha inválidas.')).toBeVisible();
  senhaAtual = await trocarSenhaObrigatoria(usuario, provisoria);
  await contexto.close();
});

test('4. desativar impede o login; reativar devolve o acesso', async ({ page, browser }) => {
  await entrarComo(page, 'admin', `/configuracoes/usuarios?busca=${matricula}&perfil=INTERNO`);
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await capturar(page, 'admin-01-usuarios-com-filtros');
  await acaoDaLinha(page, matricula, 'Desativar');
  const confirmacao = confirmacaoAberta(page);
  await expect(confirmacao).toContainText('perde o acesso em até 1 minuto');
  await confirmacao.getByRole('button', { name: 'Desativar' }).click();
  await expect(page.locator('tbody tr').first()).toContainText('Inativo');

  const { contexto, page: usuario } = await novoContexto(browser);
  await entrarPelaTela(usuario, matricula, senhaAtual);
  await expect(usuario.getByText('Matrícula ou senha inválidas.')).toBeVisible();

  await acaoDaLinha(page, matricula, 'Ativar');
  await confirmacaoAberta(page).getByRole('button', { name: 'Ativar' }).click();
  await expect(page.locator('tbody tr').first()).toContainText('Ativo');
  await entrarPelaTela(usuario, matricula, senhaAtual);
  await expect(botaoMenuUsuario(usuario)).toBeVisible();
  await contexto.close();
});

test('5. último administrador: desativar ou rebaixar mostra a mensagem específica e nada muda', async ({ page }) => {
  // Os dados de desenvolvimento têm DOIS administradores (admin e dev.administrador): a recusa real (409 ULTIMO_ADMINISTRADOR)
  // é provada no back-end (AdministracaoApiTests.UltimoAdministrador_NaoPodeSerDesativadoNemRebaixado). Aqui a resposta da API
  // é simulada, para conferir o que a tela faz com esse código, sem desativar nenhum administrador de verdade.
  const recusa = {
    status: 409,
    contentType: 'application/problem+json',
    body: JSON.stringify({
      status: 409,
      codigo: 'ULTIMO_ADMINISTRADOR',
      title: 'Regra de negócio',
      detail: 'Não é possível desativar ou rebaixar o último administrador ativo.',
      traceId: '00-e2e-00',
    }),
  };
  await page.route(/\/api\/v1\/usuarios\/[^/]+\/ativacao$/, (rota) => rota.fulfill(recusa));
  await page.route(/\/api\/v1\/usuarios\/[^/?]+$/, (rota) =>
    rota.request().method() === 'PUT' ? rota.fulfill(recusa) : rota.continue(),
  );
  await entrarComo(page, 'admin', '/configuracoes/usuarios?busca=admin&perfil=ADMINISTRADOR');
  const linhaAdmin = page.locator('tbody tr', { has: page.getByRole('link', { name: 'admin', exact: true }) });
  await expect(linhaAdmin).toContainText('Ativo');

  // Desativar o PRÓPRIO usuário pede uma segunda confirmação.
  await acaoDaLinha(linhaAdmin, 'admin', 'Desativar');
  await confirmacaoAberta(page).getByRole('button', { name: 'Desativar' }).click();
  await expect(confirmacaoAberta(page)).toContainText('Você está desativando o seu próprio usuário');
  await confirmacaoAberta(page).getByRole('button', { name: 'Sim, desativar o meu usuário' }).click();
  const mensagem = 'Não é possível desativar nem rebaixar o último administrador ativo.';
  await expect(page.getByText(mensagem)).toBeVisible();
  await expect(linhaAdmin).toContainText('Ativo');
  await expect(botaoMenuUsuario(page)).toBeVisible(); // continua logado

  // Rebaixar (editar o próprio perfil): primeiro o aviso de perder a administração, depois a recusa.
  const { corpo } = await chamar<{ itens: { id: string; matricula: string }[] }>(
    tokenDe('admin'),
    'GET',
    '/usuarios?busca=admin&perfil=ADMINISTRADOR',
  );
  const adminId = corpo.itens.find((u) => u.matricula === 'admin')!.id;
  await page.goto(`/configuracoes/usuarios/${adminId}/editar`);
  await escolher(page, /^Perfil/, /^Gestor/);
  await abrirMultiselect(page, /^Equipes/);
  await page.getByRole('option', { name: 'Equipe Alfa' }).click();
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Salvar' }).click();
  await expect(confirmacaoAberta(page)).toContainText('perde o acesso à administração');
  await confirmacaoAberta(page).getByRole('button', { name: 'Salvar mesmo assim' }).click();
  await expect(page.getByText(mensagem)).toBeVisible();
  await page.unrouteAll();
  await page.goto('/configuracoes/usuarios?busca=admin&perfil=ADMINISTRADOR');
  await expect(linhaAdmin).toContainText('Administrador');
  await page.getByRole('link', { name: 'Configurações', exact: true }).click();
  await expect(
    page
      .getByRole('navigation', { name: 'Menu de configurações' })
      .getByRole('link', { name: 'Usuários', exact: true }),
  ).toBeVisible();
});

test('6. desativar uma equipe avisa quantos são afetados; o membro perde e recupera o acesso aos itens dela', async ({
  page,
}) => {
  const tokenUsuario = await entrarPelaApi(matricula, senhaAtual);
  const ler = (id: string) =>
    chamar(tokenUsuario, 'GET', `/grupos/testes_desempenho/entidades/relatorio_teste/registros/${id}`);
  expect((await ler(relatorioEquipeNova)).status).toBe(200);

  await entrarComo(page, 'admin', `/configuracoes/equipes?busca=${encodeURIComponent(prefixo)}`);
  await acaoDaLinha(page, nomeEquipe, 'Desativar');
  const confirmacao = confirmacaoAberta(page);
  await expect(confirmacao).toContainText(
    'os membros perdem o acesso aos itens dela (1 usuário(s) ativo(s) afetado(s))',
  );
  await expect(confirmacao).toContainText('Os itens NÃO são apagados');
  await expect(confirmacao).toContainText('Nenhum usuário fica sem equipe ativa.');
  await esperarOpaco(confirmacao);
  await capturar(page, 'admin-05-aviso-desativar-equipe');
  await confirmacao.getByRole('button', { name: 'Desativar' }).click();
  await expect(page.locator('tbody tr').first()).toContainText('Inativa');

  // O cache do usuário é descartado na hora pela API (RN23): o item da equipe inativa vira 404; o da Alfa continua.
  expect((await ler(relatorioEquipeNova)).status).toBe(404);
  expect((await ler(relatorioAlfa)).status).toBe(200);

  await acaoDaLinha(page, nomeEquipe, 'Ativar');
  await confirmacaoAberta(page).getByRole('button', { name: 'Ativar' }).click();
  await expect(page.locator('tbody tr').first()).toContainText('Ativa');
  expect((await ler(relatorioEquipeNova)).status).toBe(200);

  // Um membro que SÓ tem esta equipe: a regra RN31 não deixa desativá-la, e a tela já diz isso (sem o botão de confirmar).
  const admin = tokenDe('admin');
  const unico = await chamar(admin, 'POST', '/usuarios', {
    matricula: `${prefixo.toLowerCase()}-u2`,
    nome: `Só da ${nomeEquipe}`,
    perfil: 'EXTERNO',
    equipes: [equipeId],
  });
  expect(unico.status).toBe(201);
  await page.reload();
  await acaoDaLinha(page, nomeEquipe, 'Desativar');
  await expect(confirmacao).toContainText('1 usuário(s) ficariam sem NENHUMA equipe ativa');
  await expect(confirmacao.getByRole('button', { name: 'Desativar' })).toHaveCount(0);
  await confirmacao.getByRole('button', { name: 'Fechar' }).click();
  await expect(page.locator('tbody tr').first()).toContainText('Ativa');
});

test('7. o histórico do usuário mostra criação, mudança de equipes e redefinição de senha, sem nenhum valor de senha', async ({
  page,
}) => {
  await entrarComo(page, 'admin', `/configuracoes/usuarios/${usuarioId}/editar`);
  await expect(page.getByRole('textbox', { name: /^Matrícula/ })).toHaveAttribute('readonly', '');
  await abrirMultiselect(page, /^Equipes/);
  await page.getByRole('option', { name: 'Equipe Beta' }).click();
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Salvar' }).click();
  await expect(page.getByRole('heading', { name: nomeUsuario })).toBeVisible();

  await page.getByRole('tab', { name: 'Histórico' }).click();
  const historico = page.getByRole('tabpanel');
  await expect(historico.getByText('Criação')).toBeVisible();
  await expect(historico).toContainText(`Equipes: ${nomeEquipe}, Equipe Alfa`);
  await expect(historico).toContainText('Equipe Beta');
  await expect(historico).toContainText('Senha:');
  await expect(historico).toContainText('redefinida');
  const texto = (await page.locator('body').textContent()) ?? '';
  for (const senha of senhasVistas) {
    expect(texto).not.toContain(senha);
  }

  // O detalhe da equipe, com os membros (e o link para cada um).
  await page.goto(`/configuracoes/equipes/${equipeId}`);
  await expect(page.getByRole('link', { name: matricula, exact: true })).toBeVisible();
  await expect(page.getByText('Só tem esta equipe ativa')).toBeVisible();
  await capturar(page, 'admin-04-detalhe-equipe-membros');
});

test('8. só o ADMINISTRADOR vê Usuários, Equipes e Áreas de gestão; as rotas (novas e antigas) mostram "sem permissão"', async ({
  browser,
}) => {
  for (const perfil of ['dev.gestor', 'dev.interno', 'dev.externo'] as const) {
    const { contexto, page } = await novoContexto(browser);
    await entrarComo(page, perfil, '/');
    const engrenagem = page.getByRole('link', { name: 'Configurações', exact: true });
    if (perfil === 'dev.gestor') {
      // O GESTOR entra na área de configurações, mas só vê Configurações da equipe.
      await engrenagem.click();
      const menu = page.getByRole('navigation', { name: 'Menu de configurações' });
      await expect(menu.getByRole('link', { name: 'Configurações da equipe' })).toBeVisible();
      await expect(menu.getByRole('link', { name: 'Usuários', exact: true })).toHaveCount(0);
    } else {
      await expect(engrenagem).toHaveCount(0);
    }
    for (const rota of ['/configuracoes/usuarios', '/admin/usuarios', '/admin/equipes/novo', '/configuracoes/areas']) {
      await page.goto(rota);
      await expect(page.getByRole('heading', { name: 'Sem permissão' })).toBeAttached();
      await expect(page.getByText('O seu perfil não tem acesso a esta página')).toBeVisible();
    }
    await contexto.close();
  }
});

test('9. todo perfil acessa "Minha conta", e o usuário troca a própria senha', async ({ page, browser }) => {
  for (const perfil of ['dev.externo', 'dev.interno', 'dev.gestor', 'admin'] as const) {
    const { contexto, page: outra } = await novoContexto(browser);
    await entrarComo(outra, perfil, '/');
    await noMenuDoUsuario(outra, 'Minha conta');
    await expect(outra.getByRole('heading', { name: 'Minha conta' })).toBeVisible();
    await expect(outra.getByRole('main').getByRole('button', { name: 'Trocar senha' })).toBeVisible();
    if (perfil === 'dev.gestor') {
      await expect(outra.getByText('Cria, edita, exclui e acessa o painel.')).toBeVisible();
      await capturar(outra, 'admin-06-minha-conta');
    }
    await contexto.close();
  }

  // A troca voluntária: com o usuário criado neste teste (a senha dos dev.* não muda).
  await entrarPelaTela(page, matricula, senhaAtual);
  await noMenuDoUsuario(page, 'Minha conta');
  await page.getByRole('main').getByRole('button', { name: 'Trocar senha' }).click();
  const nova = `Outra${Date.now()}y`;
  await page.locator('#senha-atual').fill(senhaAtual);
  await page.locator('#nova-senha').fill(nova);
  await page.locator('#confirmacao').fill(nova);
  await page.getByRole('button', { name: 'Trocar senha' }).click();
  await expect(page.getByText('Senha alterada.')).toBeVisible();
  await expect(page).toHaveURL(/\/minha-conta$/);
  // A sessão continua: a tela seguinte abre sem novo login.
  await page
    .getByRole('navigation', { name: 'Menu da área de gestão' })
    .getByRole('link', { name: 'Relatórios de Teste' })
    .click();
  await expect(page.locator('tbody tr').first()).toBeVisible();
  senhaAtual = nova;
});

test('10. formulário de relatório: cada controle é achado pelo nome do campo (acessibilidade)', async ({ page }) => {
  const admin = tokenDe('admin');
  const { corpo: entidades } = await chamar<
    { chave: string; campos: { nome: string; tipoDado: string; obrigatorio: boolean }[] }[]
  >(admin, 'GET', '/grupos/testes_desempenho/entidades');
  const relatorio = entidades.find((e) => e.chave === 'relatorio_teste')!;
  await entrarComo(page, 'admin', '/testes_desempenho/relatorio_teste/novo');
  await expect(page.getByRole('heading', { name: 'Novo: Relatório de Teste' })).toBeVisible();

  const papeis: Record<string, 'textbox' | 'spinbutton' | 'combobox' | 'switch'> = {
    TEXTO: 'textbox',
    TEXTO_LONGO: 'textbox',
    INTEIRO: 'spinbutton',
    DECIMAL: 'spinbutton',
    DATA: 'combobox',
    DATA_HORA: 'combobox',
    LISTA: 'combobox',
    REFERENCIA: 'combobox',
    BOOLEANO: 'switch',
  };
  const escapar = (texto: string) => texto.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  for (const campo of relatorio.campos) {
    const nome = new RegExp(`^${escapar(campo.nome)}${campo.obrigatorio ? ' ?\\*? ?\\(obrigatório\\)' : ''}\\s*$`);
    await expect(
      page.getByRole(papeis[campo.tipoDado], { name: nome }),
      `${campo.nome} (${campo.tipoDado})`,
    ).toHaveCount(1);
  }
  // A equipe (o administrador escolhe): também pelo nome.
  await expect(page.getByRole('combobox', { name: /^Equipe/ })).toHaveCount(1);
});
