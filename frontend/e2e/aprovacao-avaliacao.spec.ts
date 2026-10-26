import { Browser, Page, expect, test } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { GRUPO, chamar, criar, idPor, listar, prefixoUnico, tokenDe } from './apoio/api';
import { habilitarNaListagem, limparE2E } from './apoio/limpeza';
import {
  campo,
  capturar,
  capturarTela,
  digitarData,
  entrarComToken,
  entrarComo,
  escolherReferencia,
} from './apoio/sessao';

/**
 * Aprovação e avaliação de itens (RN33 a RN48, ADR 0045). O arquivo cria a PRÓPRIA equipe ("Equipe E2E-…") com um EXTERNO, um
 * segundo EXTERNO, um INTERNO e um GESTOR, e liga a aprovação e a avaliação SÓ nela: as equipes de desenvolvimento (Alfa, Beta…)
 * nunca são configuradas (o cenário 12 confere). Os cenários seguem uma história (um depende do anterior), por isso rodam em série.
 * A equipe é habilitada em Testes de Desempenho (RN90). No início e no fim (mesmo com falha), o helper compartilhado
 * (e2e/apoio/limpeza.ts) desmonta as equipes e os usuários de teste: pendentes aprovados, aprovação e modelo desligados, usuários
 * e equipe desativados, equipe tirada das listagens.
 * Capturas em e2e/capturas/aprov-*.png.
 */

const prefixo = prefixoUnico();
const ENTIDADE = 'relatorio_teste';
const LISTA = `/${GRUPO}/${ENTIDADE}`;

interface UsuarioTeste {
  id: string;
  token: string;
}

let equipeId = '';
let externo: UsuarioTeste;
let outroExterno: UsuarioTeste;
let interno: UsuarioTeste;
let gestor: UsuarioTeste;
let projeto = '';
let rtc = '';
let ambiente = '';
let statusResultado = '';
/** Retrato das equipes de desenvolvimento antes deste arquivo (cenário 12: nada pode mudar nelas). */
let retratoAntes: Record<string, { aprovacao: string[]; criterios: string[] }> = {};
/** O relatório da história: criado pelo EXTERNO (2), rejeitado (3), reenviado e aprovado (4), avaliado (8). */
let relatorioId = '';

test.describe.configure({ mode: 'serial' });

/** Uma senha nova forte (nunca impressa nem gravada). */
function senhaForte(): string {
  return `Nova${randomBytes(9).toString('base64url')}9a`;
}

/** Login pela API, com uma espera se o limite de 10 por minuto (RN23) estourar. */
async function entrar(matricula: string, senha: string): Promise<string> {
  for (let tentativa = 1; tentativa <= 2; tentativa++) {
    const { status, corpo } = await chamar<{ token: string }>(null, 'POST', '/auth/login', { matricula, senha });
    if (status === 200) {
      return corpo.token;
    }
    if (status !== 429) {
      throw new Error(`Login de ${matricula}: ${status}`);
    }
    await new Promise((r) => setTimeout(r, 61_000));
  }
  throw new Error(`Login de ${matricula}: limite de tentativas`);
}

/** Cria o usuário na equipe do teste, entra com a senha provisória e troca a senha (o token continua válido, RN25). */
async function criarUsuario(sufixo: string, nome: string, perfil: string): Promise<UsuarioTeste> {
  const admin = tokenDe('admin');
  const matricula = `${prefixo.toLowerCase()}-${sufixo}`;
  const { status, corpo } = await chamar<{ senhaProvisoria: string; usuario: { id: string } }>(
    admin,
    'POST',
    '/usuarios',
    { matricula, nome: `${nome} ${prefixo}`, perfil, equipes: [equipeId] },
  );
  expect(status, `POST /usuarios ${perfil}`).toBe(201);
  const token = await entrar(matricula, corpo.senhaProvisoria);
  const troca = await chamar(token, 'POST', '/auth/trocar-senha', {
    senhaAtual: corpo.senhaProvisoria,
    novaSenha: senhaForte(),
  });
  expect(troca.status, 'trocar-senha').toBe(204);
  return { id: corpo.usuario.id, token };
}

/** Uma página nova (outro contexto: outra sessão) já logada com o token. */
async function abrirComo(browser: Browser, usuario: UsuarioTeste, caminho: string): Promise<Page> {
  const contexto = await browser.newContext({
    locale: 'pt-BR',
    timezoneId: 'America/Sao_Paulo',
    viewport: { width: 1400, height: 900 },
    reducedMotion: 'reduce',
  });
  const pagina = await contexto.newPage();
  await entrarComToken(pagina, usuario.token, caminho);
  return pagina;
}

function corpoRelatorio(bateria: string): Record<string, unknown> {
  return {
    equipeId,
    projeto,
    rtc,
    ambiente,
    status_resultado: statusResultado,
    data_execucao: '2026-10-01T10:00:00-03:00',
    bateria_testes: bateria,
    tipo_teste: 'Carga',
    executado_por: 'Equipe E2E',
  };
}

async function decidirPelaApi(token: string, id: string, decisao: 'APROVAR' | 'REJEITAR'): Promise<void> {
  const { status } = await chamar(token, 'POST', `/grupos/${GRUPO}/entidades/${ENTIDADE}/registros/${id}/aprovacao`, {
    decisao,
    justificativa: 'Decisão do teste automatizado.',
  });
  expect(status).toBe(200);
}

/** Dá estrelas num critério do formulário de avaliação (o p-rating: cada estrela é um rádio "N de 5 estrelas"). */
async function darEstrelas(pagina: Page, criterio: string, estrelas: number): Promise<void> {
  const grupo = pagina.getByRole('radiogroup', { name: new RegExp(`^${criterio}`) });
  await grupo
    .locator('.p-rating-option')
    .nth(estrelas - 1)
    .click();
}

test.beforeAll(async () => {
  // Quatro logins: se outro arquivo já gastou o limite do minuto (RN23), um deles espera 61 s.
  test.setTimeout(240_000);
  const admin = tokenDe('admin');
  await limparE2E(admin);
  retratoAntes = await retratoDasEquipesDev();
  const equipe = await chamar<{ id: string }>(admin, 'POST', '/equipes', { nome: `Equipe ${prefixo}` });
  expect(equipe.status, 'POST /equipes').toBe(201);
  equipeId = equipe.corpo.id;
  // Equipe nova não é habilitada em nenhuma listagem (RN90).
  await habilitarNaListagem(admin, GRUPO, equipeId);

  externo = await criarUsuario('ext', 'Externo', 'EXTERNO');
  outroExterno = await criarUsuario('ext2', 'Outro Externo', 'EXTERNO');
  interno = await criarUsuario('int', 'Interno', 'INTERNO');
  gestor = await criarUsuario('ges', 'Gestor', 'GESTOR');

  // Cadastros da equipe nova (o administrador cria; nascem aprovados) e os globais do npm run dados-dev.
  const sigla = (await listar(admin, 'sigla', { tamanho: '1' })).itens[0].id;
  projeto = await criar(admin, 'projeto', { equipeId, sigla, nome: `Projeto ${prefixo}` });
  rtc = await criar(admin, 'rtc', { equipeId, rtc_externo_id: `RTC-${prefixo}` });
  ambiente = await idPor(admin, 'ambiente', 'nome', 'Homologação');
  statusResultado = await idPor(admin, 'status_resultado', 'descricao', 'Aprovado');
});

test.afterAll(async () => {
  // O helper compartilhado (e2e/apoio/limpeza.ts): pendentes aprovados, aprovação e modelo desligados, usuários e equipe desativados.
  await limparE2E(tokenDe('admin'));
});

test('1. o GESTOR liga a aprovação de Relatórios de Teste na equipe dele, vendo antes a explicação', async ({
  page,
}) => {
  await entrarComToken(page, gestor.token, '/');
  // A engrenagem do cabeçalho abre a área de configurações direto em Configurações da equipe.
  await page.getByRole('link', { name: 'Configurações', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Configurações da equipe', level: 1 })).toBeVisible();
  // O GESTOR só tem a equipe dele: ela já vem escolhida.
  await expect(page).toHaveURL(new RegExp(`equipeId=${equipeId}`));
  if (!page.url().includes(`grupo=${GRUPO}`)) {
    await page.goto(`/configuracoes/equipe?equipeId=${equipeId}&grupo=${GRUPO}`);
  }
  const rotulo = page.locator(`label[for="aprovacao-${ENTIDADE}"]`);
  await expect(rotulo).toContainText('Desligada');
  await capturar(page, 'aprov-01-configuracoes-da-equipe');

  await page.getByRole('switch', { name: /Aprovação de Relatórios de Teste/ }).click();
  const dialogo = page.getByRole('dialog', { name: 'Ligar a aprovação' });
  await expect(dialogo).toContainText('Externos');
  await expect(dialogo).toContainText('pendentes');
  await expect(dialogo).toContainText(`Equipe ${prefixo}`);
  await capturarTela(page, 'aprov-02-explicacao-ligar-aprovacao');
  await dialogo.getByRole('button', { name: 'Ligar a aprovação' }).click();
  await expect(dialogo).toBeHidden();
  await expect(rotulo).toContainText('Ligada');
});

test('2. o EXTERNO cria um relatório: fica Pendente, com a mensagem; outro EXTERNO da equipe não o vê', async ({
  page,
  browser,
}) => {
  await entrarComToken(page, externo.token, LISTA);
  await page.getByRole('button', { name: 'Novo' }).click();
  await expect(page.getByText('Este item será enviado para aprovação.')).toBeVisible();
  // A equipe com aprovação é a única dele: aparece como informação, sem select.
  await expect(page.locator('.info-equipe')).toContainText(`Equipe ${prefixo}`);

  await escolherReferencia(page, /^Projeto/, prefixo.slice(0, 8), `Projeto ${prefixo}`);
  await escolherReferencia(page, /^RTC/, prefixo.slice(0, 8), `RTC-${prefixo}`);
  await escolherReferencia(page, /^Ambiente/, 'Homolog', 'Homologação');
  await escolherReferencia(page, /^Status/, 'Aprovado', /^Aprovado$/);
  await digitarData(page, /^Data de execução/, '01/10/2026 10:00');
  await campo(page, /^Bateria de testes/).fill(`${prefixo} A`);
  await campo(page, /^Tipo de teste/).fill('Carga');
  await campo(page, /^Executado por/).fill('Equipe E2E');
  await page.getByRole('button', { name: /^Salvar\b/ }).click();

  await expect(page.getByText('Item criado e enviado para aprovação.')).toBeVisible();
  await expect(page.getByRole('heading', { name: `${prefixo} A`, level: 1 })).toBeVisible();
  await expect(page.locator('.faixa-aprovacao')).toContainText('Pendente — aguardando a aprovação');
  relatorioId = page.url().split('/').pop()!;

  // RN37: o pendente é só do autor (e de quem decide).
  const outro = await abrirComo(browser, outroExterno, `${LISTA}/${relatorioId}`);
  await expect(outro.getByText('Este item não está disponível')).toBeVisible();
  await outro.goto(`${LISTA}?bateria_testes.contem=${prefixo}`);
  await expect(outro.getByText('Nenhum item encontrado com estes filtros')).toBeVisible();
  await outro.context().close();
});

test('3. o GESTOR vê o contador, abre a fila e rejeita: sem justificativa, o erro aparece no campo', async ({
  page,
}) => {
  await entrarComToken(page, gestor.token, '/');
  const menu = page.getByRole('link', { name: /Aprovações/ });
  await expect(menu).toContainText('1');
  await menu.click();
  await expect(page.getByRole('heading', { name: 'Aprovações', level: 1 })).toBeVisible();
  await expect(page.getByText('1 item pendente')).toBeVisible();
  const linha = page.getByRole('row', { name: new RegExp(`${prefixo} A`) });
  await expect(linha).toContainText('Relatório de Teste');
  await expect(linha).toContainText(`Externo ${prefixo}`);
  await expect(linha).toContainText('menos de 1 hora');
  await capturar(page, 'aprov-03-fila-com-contador');

  await linha.getByRole('link', { name: `${prefixo} A` }).click();
  await expect(page.locator('.faixa-aprovacao')).toContainText('Pendente de aprovação');
  await page.getByRole('button', { name: 'Rejeitar' }).click();
  const dialogo = page.getByRole('dialog', { name: 'Rejeitar o item' });
  await dialogo.getByRole('button', { name: 'Rejeitar' }).click();
  await expect(dialogo.locator('#justificativa-erro')).toHaveText('Informe a justificativa.');
  await expect(dialogo.locator('#justificativa')).toHaveAttribute('aria-invalid', 'true');
  await dialogo.locator('#justificativa').fill('curta');
  await dialogo.getByRole('button', { name: 'Rejeitar' }).click();
  await expect(dialogo.locator('#justificativa-erro')).toContainText('pelo menos 10 caracteres');
  await dialogo.locator('#justificativa').fill('Faltou o objetivo do teste. Preencha e reenvie.');
  await expect(dialogo.locator('#justificativa-contador')).toContainText('47/1000');
  await capturarTela(page, 'aprov-04-dialogo-rejeicao');
  await dialogo.getByRole('button', { name: 'Rejeitar' }).click();

  // Veio da fila: volta para ela, já sem o item.
  await expect(page).toHaveURL(/\/aprovacoes/);
  await expect(page.getByText('Nenhum item aguardando aprovação')).toBeVisible();
  await expect(page.getByRole('link', { name: /Aprovações/ })).not.toContainText('1');
});

test('4. o EXTERNO vê a rejeição, corrige e reenvia; o GESTOR aprova; o EXTERNO não edita mais', async ({
  page,
  browser,
}) => {
  await entrarComToken(page, externo.token, `${LISTA}/${relatorioId}`);
  const faixa = page.locator('.faixa-aprovacao');
  await expect(faixa).toContainText('Rejeitado — corrija e reenvie');
  await expect(faixa.locator('.faixa-justificativa')).toHaveText('Faltou o objetivo do teste. Preencha e reenvie.');
  await expect(faixa).toContainText(`Gestor ${prefixo}`);
  await capturar(page, 'aprov-05-faixa-rejeitado-externo');

  await page.getByRole('button', { name: 'Editar' }).click();
  await expect(page.getByText('Ao reenviar, o item volta a ficar pendente de aprovação.')).toBeVisible();
  await campo(page, /^Objetivo/).fill('Medir a vazão da transferência com 200 usuários.');
  await page.getByRole('button', { name: 'Reenviar para aprovação' }).click();
  await expect(page.getByText('Item reenviado para aprovação.')).toBeVisible();
  await expect(faixa).toContainText('Pendente — aguardando a aprovação');

  const paginaGestor = await abrirComo(browser, gestor, `${LISTA}/${relatorioId}`);
  await paginaGestor.getByRole('button', { name: 'Aprovar' }).click();
  const dialogo = paginaGestor.getByRole('dialog', { name: 'Aprovar o item' });
  await dialogo.locator('#justificativa').fill('Objetivo preenchido, resultados conferidos.');
  await dialogo.getByRole('button', { name: 'Aprovar' }).click();
  // Sem vir da fila: fica no item, atualizado.
  await expect(paginaGestor.locator('.faixa-aprovacao')).toContainText('Aprovado');
  await expect(paginaGestor.getByRole('button', { name: 'Aprovar' })).toBeHidden();
  await paginaGestor.context().close();

  await page.reload();
  await expect(faixa).toContainText('Aprovado');
  await expect(faixa.locator('.faixa-justificativa')).toHaveText('Objetivo preenchido, resultados conferidos.');
  await expect(page.getByRole('button', { name: 'Editar' })).toBeHidden();
  await expect(page.getByRole('button', { name: 'Excluir' })).toBeHidden();
});

test('5. o histórico mostra a criação, a rejeição, o reenvio e a aprovação, com as justificativas', async ({
  page,
}) => {
  await entrarComToken(page, gestor.token, `${LISTA}/${relatorioId}`);
  await page.getByRole('tab', { name: 'Histórico' }).click();
  const linhas = page.locator('p-tabpanel[value="historico"] tbody tr');
  await expect(linhas).toHaveCount(4);
  await expect(linhas.nth(0)).toContainText('Aprovação');
  await expect(linhas.nth(0).locator('.faixa-justificativa')).toContainText('Objetivo preenchido');
  await expect(linhas.nth(1)).toContainText('Alteração');
  await expect(linhas.nth(1)).toContainText('Situação de aprovação: Rejeitado');
  await expect(linhas.nth(1)).toContainText('Pendente');
  await expect(linhas.nth(2)).toContainText('Rejeição');
  await expect(linhas.nth(2).locator('.faixa-justificativa')).toContainText('Faltou o objetivo');
  await expect(linhas.nth(3)).toContainText('Criação');
  await capturar(page, 'aprov-06-historico-decisoes');
});

test('6. desligar a aprovação com itens pendentes mostra a quantidade e o link para a fila filtrada', async ({
  page,
}) => {
  await criar(externo.token, ENTIDADE, corpoRelatorio(`${prefixo} C`));

  await entrarComToken(page, gestor.token, `/configuracoes/equipe?equipeId=${equipeId}&grupo=${GRUPO}`);
  const rotulo = page.locator(`label[for="aprovacao-${ENTIDADE}"]`);
  await expect(rotulo).toContainText('Ligada');
  await page.getByRole('switch', { name: /Aprovação de Relatórios de Teste/ }).click();
  const dialogo = page.getByRole('dialog', { name: 'Ainda há itens pendentes' });
  await expect(dialogo).toContainText('o item pendente');
  await capturarTela(page, 'aprov-07-bloqueio-pendentes');
  await dialogo.getByRole('link', { name: 'Abrir a fila de aprovação, já filtrada' }).click();

  await expect(page).toHaveURL(new RegExp(`/aprovacoes\\?.*entidade=${ENTIDADE}`));
  await expect(page).toHaveURL(new RegExp(`equipeId=${equipeId}`));
  await expect(page.getByRole('row', { name: new RegExp(`${prefixo} C`) })).toBeVisible();
  // A aprovação continua ligada.
  await page.goto(`/configuracoes/equipe?equipeId=${equipeId}&grupo=${GRUPO}`);
  await expect(rotulo).toContainText('Ligada');
});

test('7. o GESTOR cria um modelo com Qualidade (estrelas, comentário obrigatório) e Observações (texto)', async ({
  page,
}) => {
  await entrarComToken(page, gestor.token, `/configuracoes/equipe?equipeId=${equipeId}&grupo=${GRUPO}`);
  await page.getByRole('button', { name: 'Criar um modelo de avaliação para Relatórios de Teste' }).click();
  await expect(page.getByRole('heading', { level: 1 })).toContainText('modelo');

  await page.locator('#modelo-nome').fill('Avaliação do relatório');
  await page.locator('#criterio-0-nome').fill('Qualidade');
  await expect(page.locator('#criterio-0-chave')).toHaveValue('qualidade');
  await page.locator('#criterio-0-comentario').click();
  await page.getByRole('option', { name: 'Comentário obrigatório' }).click();
  await page.getByRole('button', { name: 'Adicionar critério' }).click();
  await page.locator('#criterio-1-nome').fill('Observações');
  await expect(page.locator('#criterio-1-chave')).toHaveValue('observacoes');
  await page.locator('#criterio-1-tipo').click();
  await page.getByRole('option', { name: 'Texto livre' }).click();

  const previa = page.locator('.previa-avaliacao');
  await expect(previa.getByRole('radiogroup', { name: /^Qualidade/ })).toBeVisible();
  await expect(previa.getByRole('textbox', { name: /Comentário sobre Qualidade/ })).toBeVisible();
  await expect(previa.getByRole('textbox', { name: /Observações/ })).toBeVisible();
  await darEstrelas(page, 'Qualidade', 4);
  await capturar(page, 'aprov-08-editor-modelo-com-previa');

  await page.getByRole('button', { name: 'Criar modelo' }).click();
  await expect(page).toHaveURL(/\/configuracoes\/equipe\?/);
  const linha = page.getByRole('row', { name: /Relatórios de Teste/ });
  await expect(linha).toContainText('Ativo');
  await expect(linha).toContainText('Avaliação do relatório');
});

test('8. INTERNO e GESTOR avaliam; o comentário obrigatório é cobrado; o resumo confere; o EXTERNO só vê o resumo', async ({
  page,
  browser,
}) => {
  await entrarComToken(page, interno.token, `${LISTA}/${relatorioId}`);
  await page.getByRole('tab', { name: 'Avaliações' }).click();
  await expect(page.getByText('Ainda sem avaliações.')).toBeVisible();
  await darEstrelas(page, 'Qualidade', 4);
  await page.getByRole('button', { name: 'Salvar avaliação' }).click();
  await expect(page.locator('#minha-qualidade-comentario-erros')).toHaveText(
    'O comentário é obrigatório neste critério.',
  );
  await page.locator('#minha-qualidade-comentario').fill('Relatório claro, faltou só o gráfico de erros.');
  await page.getByRole('button', { name: 'Salvar avaliação' }).click();
  await expect(page.getByText('Avaliação registrada.')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Salvar alterações' })).toBeVisible();

  const paginaGestor = await abrirComo(browser, gestor, `${LISTA}/${relatorioId}`);
  await paginaGestor.getByRole('tab', { name: 'Avaliações' }).click();
  await darEstrelas(paginaGestor, 'Qualidade', 2);
  await paginaGestor.locator('#minha-qualidade-comentario').fill('Os números do cenário 2 não batem com a planilha.');
  await paginaGestor.locator('#minha-observacoes-texto').fill('Refazer o cenário 2 na próxima bateria.');
  await paginaGestor.getByRole('button', { name: 'Salvar avaliação' }).click();
  await expect(paginaGestor.getByText('Avaliação registrada.')).toBeVisible();

  const resumo = paginaGestor.getByLabel('Resumo de Qualidade');
  await expect(resumo.getByRole('img', { name: 'Média 3,0 de 5' })).toBeVisible();
  await expect(resumo).toContainText('2 notas');
  await expect(paginaGestor.getByText('2 avaliadores')).toBeVisible();
  const barras = resumo.getByRole('list', { name: 'Distribuição das notas de Qualidade' }).getByRole('listitem');
  await expect(barras.nth(0)).toContainText('5 de 5 estrelas: 0');
  await expect(barras.nth(1)).toContainText('4 de 5 estrelas: 1');
  await expect(barras.nth(3)).toContainText('2 de 5 estrelas: 1');
  const individuais = paginaGestor.locator('.avaliacoes-individuais');
  await expect(individuais.locator('.avaliacao-individual')).toHaveCount(2);
  await expect(individuais).toContainText('Refazer o cenário 2 na próxima bateria.');
  await capturar(paginaGestor, 'aprov-09-aba-avaliacoes-gestor');
  await paginaGestor.context().close();

  const paginaExterno = await abrirComo(browser, externo, `${LISTA}/${relatorioId}`);
  await paginaExterno.getByRole('tab', { name: 'Avaliações' }).click();
  await expect(
    paginaExterno.getByLabel('Resumo de Qualidade').getByRole('img', { name: 'Média 3,0 de 5' }),
  ).toBeVisible();
  await expect(paginaExterno.getByRole('heading', { name: 'Minha avaliação' })).toBeHidden();
  await expect(paginaExterno.getByRole('heading', { name: 'Avaliações individuais' })).toBeHidden();
  await expect(paginaExterno.getByText('Refazer o cenário 2')).toBeHidden();
  await capturar(paginaExterno, 'aprov-10-aba-avaliacoes-externo');
  await paginaExterno.context().close();
});

test('9. a coluna de média da Qualidade aparece na listagem e ordena; o painel mostra os blocos com os dados', async ({
  page,
}) => {
  // Um segundo relatório (do INTERNO: nasce aprovado) com nota 5, e o C (aprovado agora) sem nota.
  const segundo = await criar(interno.token, ENTIDADE, corpoRelatorio(`${prefixo} B`));
  const avaliacao = await chamar(
    interno.token,
    'PUT',
    `/grupos/${GRUPO}/entidades/${ENTIDADE}/registros/${segundo}/avaliacoes/minha`,
    { respostas: { qualidade: { estrelas: 5, comentario: 'Completo.' } } },
  );
  expect(avaliacao.status).toBe(201);
  const pendente = (await listar(gestor.token, ENTIDADE, { 'bateria_testes.contem': `${prefixo} C` })).itens[0];
  await decidirPelaApi(gestor.token, pendente.id, 'APROVAR');

  await entrarComToken(page, gestor.token, `${LISTA}?bateria_testes.contem=${prefixo}`);
  await expect(page.getByRole('columnheader', { name: 'Situação' })).toBeVisible();
  // A média é uma coluna opcional: entra pelo seletor de colunas.
  await page.getByRole('button', { name: /^Colunas visíveis/ }).click();
  await page
    .getByRole('dialog', { name: 'Colunas visíveis' })
    .getByRole('checkbox', { name: 'Média: Qualidade' })
    .check();
  await page.keyboard.press('Escape');
  const cabecalho = page.getByRole('columnheader', { name: /Média: Qualidade/ });
  await expect(cabecalho).toBeVisible();

  const primeiraLinha = page.locator('.tabela-registros tbody tr').first();
  const linhas = page.locator('.tabela-registros tbody tr');
  await cabecalho.click();
  await expect(page).toHaveURL(/ordenacao=avaliacao\.qualidade/);
  await expect(primeiraLinha).toContainText(`${prefixo} A`);
  await expect(linhas.nth(1)).toContainText(`${prefixo} B`);
  // Sem nota: sempre por último, com "—".
  await expect(linhas.nth(2)).toContainText(`${prefixo} C`);
  await expect(linhas.nth(2).locator('.celula-avaliacao')).toHaveText('—');
  await cabecalho.click();
  await expect(page).toHaveURL(/ordenacao=-avaliacao\.qualidade/);
  await expect(primeiraLinha).toContainText(`${prefixo} B`);
  await expect(primeiraLinha.getByRole('img', { name: 'Média 5,0 de 5' })).toBeVisible();
  await expect(linhas.nth(2)).toContainText(`${prefixo} C`);
  await expect(primeiraLinha.locator('.celula-aprovacao')).toContainText('Aprovado');
  await capturar(page, 'aprov-11-listagem-situacao-e-media');

  // Filtro pela situação (RN37), no painel de filtros.
  await page.getByRole('button', { name: /^Filtros/ }).click();
  await expect(page.getByRole('combobox', { name: 'Situação', exact: true })).toBeVisible();
  await expect(page.getByText('Média de Qualidade: de')).toBeVisible();

  // Painel (§5): os blocos de aprovação e avaliação com os dados criados aqui.
  await page.goto(`/painel/${GRUPO}`);
  await expect(page.getByRole('heading', { name: 'Aprovação', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Avaliação', exact: true })).toBeVisible();
  await capturar(page, 'aprov-12-painel-blocos');
});

test('10. excluir um critério com respostas fica bloqueado; desativado, ele sai do formulário e do resumo', async ({
  page,
  browser,
}) => {
  await entrarComToken(page, gestor.token, `/configuracoes/equipe?equipeId=${equipeId}&grupo=${GRUPO}`);
  await page.getByRole('button', { name: 'Editar o modelo de avaliação de Relatórios de Teste' }).click();
  await expect(page.locator('#criterio-0-nome')).toHaveValue('Qualidade');
  await expect(page.getByRole('button', { name: 'Excluir o critério Qualidade' })).toBeDisabled();
  await expect(page.locator('.cartao-criterio').first().getByText('Este critério já tem respostas')).toBeVisible();
  await expect(page.locator('#criterio-0-chave')).toBeDisabled();
  await capturar(page, 'aprov-13-criterio-com-respostas');

  await page.locator('#criterio-0-ativo').click();
  await expect(page.locator('.previa-avaliacao').getByRole('radiogroup', { name: /^Qualidade/ })).toBeHidden();
  await page.getByRole('button', { name: 'Salvar modelo' }).click();
  await expect(page).toHaveURL(/\/configuracoes\/equipe\?/);

  const paginaInterno = await abrirComo(browser, interno, `${LISTA}/${relatorioId}`);
  await paginaInterno.getByRole('tab', { name: 'Avaliações' }).click();
  await expect(paginaInterno.getByRole('heading', { name: 'Minha avaliação' })).toBeVisible();
  await expect(paginaInterno.getByRole('textbox', { name: /Observações/ })).toBeVisible();
  await expect(paginaInterno.getByRole('radiogroup', { name: /^Qualidade/ })).toBeHidden();
  await expect(paginaInterno.getByLabel('Resumo de Qualidade')).toBeHidden();
  await paginaInterno.context().close();
});

test('11. o INTERNO não vê "Configurações" nem "Aprovações"', async ({ page }) => {
  await entrarComToken(page, interno.token, LISTA);
  const menu = page.getByRole('navigation', { name: 'Menu da área de gestão' });
  await expect(menu.getByRole('link', { name: `Relatórios de Teste` })).toBeVisible();
  // Os atalhos do cabeçalho (a engrenagem e a caixa de entrada) são só de GESTOR e ADMINISTRADOR.
  await expect(page.getByRole('link', { name: 'Configurações', exact: true })).toHaveCount(0);
  await expect(page.getByRole('link', { name: /^Aprovações/ })).toHaveCount(0);
  for (const rota of ['/aprovacoes', '/configuracoes/equipe', '/configuracoes-equipe']) {
    await page.goto(rota);
    await expect(page.getByText('O seu perfil não tem acesso a esta página')).toBeVisible();
  }
});

/** O que os metadados dizem das equipes de desenvolvimento (as do dev.gestor): aprovação e critérios de avaliação por entidade. */
async function retratoDasEquipesDev(): Promise<Record<string, { aprovacao: string[]; criterios: string[] }>> {
  const { corpo } = await chamar<
    { chave: string; equipesComAprovacao: string[]; criteriosAvaliacao: { chave: string }[] }[]
  >(tokenDe('dev.gestor'), 'GET', `/grupos/${GRUPO}/entidades`);
  return Object.fromEntries(
    corpo.map((e) => [
      e.chave,
      { aprovacao: e.equipesComAprovacao, criterios: e.criteriosAvaliacao.map((c) => c.chave) },
    ]),
  );
}

test('12. nas equipes de desenvolvimento nada muda: sem aprovação, sem faixa, sem pendentes, avaliação como estava', async ({
  page,
}) => {
  // A API: o E2E não mexeu nas equipes de desenvolvimento (o retrato é o mesmo de antes do arquivo) e nenhuma tem aprovação.
  // A avaliação NÃO é conferida como vazia: a Equipe Alfa pode ter um modelo criado à mão antes (dado de desenvolvimento).
  const depois = await retratoDasEquipesDev();
  expect(depois).toEqual(retratoAntes);
  for (const [chave, e] of Object.entries(depois)) {
    expect(e.aprovacao, chave).toEqual([]);
  }
  const comAvaliacao = depois[ENTIDADE].criterios.length > 0;

  await entrarComo(page, 'dev.externo', LISTA);
  await expect(page.locator('.tabela-registros tbody tr').first()).toBeVisible();
  await expect(page.getByRole('columnheader', { name: 'Situação' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Novo' })).toHaveCount(0);
  await page.locator('.tabela-registros tbody tr').first().locator('.link-linha').click();
  await expect(page.getByRole('tab', { name: 'Dados' })).toBeVisible();
  await expect(page.locator('.faixa-aprovacao')).toHaveCount(0);
  if (!comAvaliacao) {
    await expect(page.getByRole('tab', { name: 'Avaliações' })).toHaveCount(0);
  }

  const contexto = await page.context().browser()!.newContext({ locale: 'pt-BR', timezoneId: 'America/Sao_Paulo' });
  const paginaGestor = await contexto.newPage();
  await entrarComo(paginaGestor, 'dev.gestor', LISTA);
  // Sem nenhuma área com a aprovação ligada para as equipes dele, nem o atalho de Aprovações aparece.
  await expect(paginaGestor.getByRole('link', { name: /^Aprovações/ })).toHaveCount(0);
  await expect(paginaGestor.getByRole('columnheader', { name: 'Situação' })).toHaveCount(0);
  await paginaGestor.goto(`/configuracoes/equipe?grupo=${GRUPO}`);
  await expect(paginaGestor.locator('label[for^="aprovacao-"]').first()).toContainText('Desligada');
  await expect(paginaGestor.getByText('Ligada', { exact: true })).toHaveCount(0);
  await contexto.close();
});
