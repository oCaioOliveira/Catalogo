import { Page, expect, test } from '@playwright/test';
import ExcelJS from 'exceljs';
import { readFileSync } from 'node:fs';
import { GRUPO, chamar, criar, idPor, listar, prefixoUnico, tokenDe } from './apoio/api';
import { habilitarNaListagem, limparE2E } from './apoio/limpeza';
import { lerPlanilha, preencherPrimeiraAba } from './apoio/planilhas';
import { capturar as capturarPagina, capturarTela as capturarJanela, entrarComToken } from './apoio/sessao';
import { UsuarioTeste, criarUsuarioDeTeste } from './apoio/usuarios-teste';

/**
 * Importação de itens por planilha (RN97 a RN104). O arquivo cria a PRÓPRIA equipe ("Equipe E2E-…") com um GESTOR (importa, altera e
 * desfaz) e um EXTERNO (sem aprovação: não importa), e os cadastros dela (dois projetos, cada um com um endpoint, e um RTC). Os
 * cenários seguem uma história (relatórios → cenários → relatório de resultado → desfazer), por isso rodam em série. As planilhas são
 * montadas pelo próprio teste, a partir do modelo baixado na tela. No início e no fim, o helper compartilhado (e2e/apoio/limpeza.ts)
 * desmonta a equipe e os usuários de teste.
 *
 * Relatório de Teste não tem regra de unicidade nem campo LISTA no seed: o "duplicado" é exercido nos cenários (Relatório + Nº do
 * cenário), e a "opção fora da lista" é um Status que não está na lista suspensa do modelo.
 * Capturas em e2e/capturas/importacao-*.png (tema claro e escuro).
 */

const prefixo = prefixoUnico();
const RELATORIOS = `/${GRUPO}/relatorio_teste`;
const CENARIOS = `/${GRUPO}/cenario_teste`;
const XLSX = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';

const CABECALHOS_RELATORIO = [
  'Projeto *',
  'RTC *',
  'Ambiente *',
  'Status *',
  'Data de execução *',
  'Bateria de testes *',
  'Tipo de teste *',
  'Executado por *',
  'Contexto',
  'Objetivo',
  'Apontamentos',
  'Conclusão',
];
const CABECALHOS_CENARIO = [
  'Relatório *',
  'Endpoint *',
  'Nº do cenário *',
  'Usuários virtuais *',
  'Tempo médio (ms) *',
  'Percentil 95 (ms) *',
  'Vazão média (TPS) *',
  'Taxa de erro (%) *',
];

let equipeId = '';
let gestor: UsuarioTeste;
let externo: UsuarioTeste;
const projeto = `Transferências ${prefixo}`;
const outroProjeto = `Outro ${prefixo}`;
const rtc = `RTC-${prefixo}`;
let modeloRelatorios: Buffer;
let relatorioCenarios: Buffer;

test.describe.configure({ mode: 'serial' });

test.beforeAll(async () => {
  test.setTimeout(240_000);
  const admin = tokenDe('admin');
  await limparE2E(admin);
  const equipe = await chamar<{ id: string }>(admin, 'POST', '/equipes', { nome: `Equipe ${prefixo}` });
  expect(equipe.status, 'POST /equipes').toBe(201);
  equipeId = equipe.corpo.id;
  await habilitarNaListagem(admin, GRUPO, equipeId);

  gestor = await criarUsuarioDeTeste(admin, prefixo, 'ges', 'Gestor', 'GESTOR', [equipeId]);
  externo = await criarUsuarioDeTeste(admin, prefixo, 'ext', 'Externo', 'EXTERNO', [equipeId]);

  const sigla = (await listar(admin, 'sigla', { tamanho: '1' })).itens[0].id;
  const idProjeto = await criar(admin, 'projeto', { equipeId, sigla, nome: projeto });
  const idOutro = await criar(admin, 'projeto', { equipeId, sigla, nome: outroProjeto });
  await criar(admin, 'endpoint_servico', {
    equipeId,
    projeto: idProjeto,
    nome_servico: `consulta ${prefixo}`,
    metodo: 'POST',
    endpoint_url_fila: '/api/consulta',
    ativo: true,
  });
  await criar(admin, 'endpoint_servico', {
    equipeId,
    projeto: idOutro,
    nome_servico: `outro ${prefixo}`,
    metodo: 'GET',
    endpoint_url_fila: '/api/outro',
    ativo: true,
  });
  await criar(admin, 'rtc', { equipeId, rtc_externo_id: rtc });
  // Os cadastros de apoio (globais) do npm run dados-dev.
  await idPor(admin, 'ambiente', 'nome', 'Homologação');
  await idPor(admin, 'status_resultado', 'descricao', 'Aprovado');
});

test.afterAll(async () => {
  await limparE2E(tokenDe('admin'));
});

/** Uma linha de relatório na ordem do modelo (as 4 colunas opcionais vazias). */
function linhaRelatorio(
  numero: number,
  extras: Partial<Record<'projeto' | 'status' | 'data', string>> = {},
): (string | number)[] {
  return [
    // Sem diferenciar maiúsculas e acentos (RN99).
    extras.projeto ?? `TRANSFERENCIAS ${prefixo}`,
    rtc,
    'homologacao',
    extras.status ?? 'Aprovado',
    extras.data ?? `${String((numero % 28) + 1).padStart(2, '0')}/10/2026 14:30`,
    `${prefixo} R${String(numero).padStart(2, '0')}`,
    'Carga',
    'Equipe E2E',
  ];
}

/** Preenche a primeira aba do modelo baixado (a partir da linha 2), mantendo as listas, os formatos e as Instruções. */
function preencherModelo(modelo: Buffer, linhas: (string | number)[][]): Promise<Buffer> {
  return preencherPrimeiraAba(modelo, linhas);
}

/** Uma planilha nova (sem modelo), com o cabeçalho e as linhas. */
async function novaPlanilha(cabecalhos: string[], linhas: unknown[][]): Promise<Buffer> {
  const pasta = new ExcelJS.Workbook();
  const aba = pasta.addWorksheet('Dados');
  aba.addRow(cabecalhos);
  linhas.forEach((linha) => aba.addRow(linha));
  return Buffer.from(await pasta.xlsx.writeBuffer());
}

async function baixar(page: Page, acao: () => Promise<void>): Promise<Buffer> {
  const [download] = await Promise.all([page.waitForEvent('download'), acao()]);
  return readFileSync(await download.path());
}

/** Envia a planilha na etapa Arquivo e espera a etapa Colunas. */
async function enviarArquivo(page: Page, nome: string, conteudo: Buffer, tipo = XLSX): Promise<void> {
  await page.getByLabel('Escolher arquivo').setInputFiles({ name: nome, mimeType: tipo, buffer: conteudo });
  await expect(page.getByRole('heading', { name: '2. Confira as colunas' })).toBeVisible();
}

/** Da etapa Colunas até a prévia, com as opções padrão. */
async function irParaPrevia(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Continuar' }).click();
  await expect(page.getByRole('heading', { name: '3. Escolha as opções' })).toBeVisible();
  await page.getByRole('button', { name: 'Ver prévia' }).click();
  await expect(page.getByRole('heading', { name: '4. Confira a prévia' })).toBeVisible();
}

function linhaDaPrevia(page: Page, numero: number) {
  return page
    .locator('.tabela-previa tbody tr')
    .filter({ has: page.locator('td.coluna-numero', { hasText: new RegExp(`^${numero}$`) }) });
}

function totalDaPrevia(page: Page, classe: string) {
  return page.locator(`.${classe} .numero-resumo`);
}

/** Abre "Importações anteriores" de um tipo de item e devolve a linha da importação pelo nome do arquivo. */
async function importacaoNoHistorico(page: Page, lista: string, arquivo: string) {
  await page.goto(`${lista}/importacoes`);
  await expect(page.getByRole('heading', { name: 'Importações anteriores', level: 1 })).toBeVisible();
  return page.locator('.tabela-importacoes tbody tr').filter({ hasText: arquivo }).first();
}

/** Espera as animações (mensagens, diálogos, dicas) terminarem: a captura sai com as cores finais. */
async function semAnimacoes(page: Page): Promise<void> {
  await page.waitForFunction(() => document.getAnimations().every((a) => a.playState !== 'running'));
}

/** Captura da página inteira, numa janela alta o bastante para a barra de ações fixa ficar no fim (e não no meio da imagem). */
async function capturar(page: Page, nome: string): Promise<void> {
  const altura = await page.evaluate(() => document.documentElement.scrollHeight);
  await page.setViewportSize({ width: 1400, height: Math.max(900, altura) });
  await semAnimacoes(page);
  await capturarPagina(page, nome);
  await page.setViewportSize({ width: 1400, height: 900 });
}

/** Captura da área visível (com a dica ou o diálogo aberto). */
async function capturarTela(page: Page, nome: string): Promise<void> {
  await semAnimacoes(page);
  await capturarJanela(page, nome);
}

/** Texto seguro dentro do HTML da captura do modelo. */
function seguro(texto: string): string {
  return texto.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

/** Confirma o "Desfazer importação" no diálogo de confirmação. */
async function confirmarDesfazer(page: Page): Promise<void> {
  const confirmacao = page.getByRole('alertdialog').filter({ hasText: 'Desfazer exclui' });
  await expect(confirmacao).toBeVisible();
  await confirmacao.getByRole('button', { name: 'Desfazer importação' }).click();
}

/** Desenha o modelo (as abas de dados e de instruções) numa página, para a captura "modelo aberto". */
async function mostrarModelo(page: Page, modelo: Buffer, tema: 'claro' | 'escuro'): Promise<void> {
  const [dados, instrucoes] = await lerPlanilha(modelo);
  const cabecalhos = dados.linhas[0];
  const escuro = tema === 'escuro';
  const html = `<html><head><meta charset="utf-8"><style>
    body{font-family:Calibri,Arial,sans-serif;font-size:13px;margin:16px;background:${escuro ? '#1f1f1f' : '#fff'};color:${escuro ? '#e6e6e6' : '#222'}}
    h2{font-size:14px;margin:16px 0 6px} table{border-collapse:collapse} td,th{border:1px solid ${escuro ? '#444' : '#d0d7de'};padding:3px 6px;vertical-align:top}
    th{background:${escuro ? '#2d3a4a' : '#e8eef5'};text-align:left} .obrig{background:${escuro ? '#5a4b16' : '#fff2cc'}}
    .lista::after{content:" ▾";color:#0066b3} .aba{display:inline-block;padding:2px 10px;border:1px solid #999;border-bottom:none;margin-right:4px}
    .instr td:nth-child(4){max-width:640px}
  </style></head><body>
    <div><span class="aba"><b>${seguro(dados.nome)}</b></span><span class="aba">${seguro(instrucoes.nome)}</span><span class="aba">(Listas, oculta)</span></div>
    <table><tr>${cabecalhos
      .map(
        (c, i) =>
          `<th class="${c.endsWith('*') ? 'obrig' : ''} ${dados.colunasComLista.includes(i + 1) ? 'lista' : ''}">${seguro(c)}</th>`,
      )
      .join('')}</tr>
    ${[1, 2, 3].map(() => `<tr>${cabecalhos.map(() => '<td>&nbsp;</td>').join('')}</tr>`).join('')}</table>
    <h2>Aba "${seguro(instrucoes.nome)}"</h2>
    <table class="instr">${instrucoes.linhas.map((l) => `<tr>${(l ?? []).map((v) => `<td>${seguro(v)}</td>`).join('')}</tr>`).join('')}</table>
  </body></html>`;
  await page.setContent(html);
}

test('1. baixar o modelo de Relatório de Teste: colunas, listas suspensas e aba Instruções', async ({ page }) => {
  await entrarComToken(page, gestor.token, RELATORIOS);
  await page.getByRole('button', { name: 'Importar', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Importar relatórios de teste', level: 1 })).toBeVisible();
  await expect(
    page.getByText('Referências são identificadas pelo nome. Se houver nomes repetidos, use a coluna (ID).'),
  ).toBeVisible();
  await capturar(page, 'importacao-1-arquivo-claro');

  modeloRelatorios = await baixar(page, () => page.getByRole('button', { name: 'Baixar modelo' }).click());

  const abas = await lerPlanilha(modeloRelatorios);
  const dados = abas[0];
  // O nome dos campos ativos, na ordem do formulário, com "*" nos obrigatórios; sem coluna Equipe (o gestor tem uma equipe só).
  expect(dados.nome).toBe('Relatórios de Teste');
  expect(dados.linhas[0]).toEqual(CABECALHOS_RELATORIO);
  // Listas suspensas nas referências com poucos itens (Projeto, RTC, Ambiente, Status), com a lista numa aba oculta.
  expect(dados.colunasComLista).toEqual([1, 2, 3, 4]);
  expect(abas.find((a) => a.nome === 'Listas')?.oculta).toBe(true);
  const instrucoes = abas.find((a) => a.nome === 'Instruções')!;
  const textos = instrucoes.linhas.map((l) => (l ?? []).join(' ')).join('\n');
  expect(textos).toContain('pelo NOME do item');
  expect(textos).toContain('Data de execução *');
  expect(textos).toContain('dd/mm/aaaa hh:mm');

  await mostrarModelo(page, modeloRelatorios, 'claro');
  await capturar(page, 'importacao-modelo-claro');
});

test('2. importar 20 relatórios, 3 com erro: prévia com as células certas, importar só as válidas, listagem e histórico', async ({
  page,
}) => {
  test.setTimeout(120_000);
  const linhas = Array.from({ length: 20 }, (_, i) => {
    const numero = i + 1;
    if (numero === 4) return linhaRelatorio(numero, { projeto: 'Projeto que não existe' });
    if (numero === 7) return linhaRelatorio(numero, { data: '31/02/2026 10:00' });
    if (numero === 11) return linhaRelatorio(numero, { status: 'Status fora da lista' });
    return linhaRelatorio(numero);
  });
  const arquivo = await preencherModelo(modeloRelatorios, linhas);

  await entrarComToken(page, gestor.token, `${RELATORIOS}/importar`);
  await enviarArquivo(page, `relatorios-${prefixo}.xlsx`, arquivo);
  // A sugestão já ligou as colunas pelo nome.
  await expect(page.getByRole('combobox', { name: 'Projeto *' })).toContainText('Projeto *');
  await capturar(page, 'importacao-2-colunas-claro');
  await page.getByRole('button', { name: 'Continuar' }).click();
  await expect(page.getByText('Os itens serão da equipe')).toBeVisible();
  await capturar(page, 'importacao-3-opcoes-claro');
  await page.getByRole('button', { name: 'Ver prévia' }).click();
  await expect(page.getByRole('heading', { name: '4. Confira a prévia' })).toBeVisible();

  await expect(totalDaPrevia(page, 'resumo-novos')).toHaveText('17');
  await expect(totalDaPrevia(page, 'resumo-erros')).toHaveText('3');
  // Cada erro na célula certa (linha da planilha = linha de dados + 1).
  const projetoErrado = linhaDaPrevia(page, 5).locator('td[data-coluna="Projeto *"]');
  await expect(projetoErrado).toHaveClass(/celula-erro/);
  await expect(projetoErrado.locator('.mensagem-celula')).toContainText("Nenhum item de 'Projeto'");
  await expect(linhaDaPrevia(page, 8).locator('td[data-coluna="Data de execução *"]')).toHaveClass(/celula-erro/);
  await expect(linhaDaPrevia(page, 8).locator('td[data-coluna="Data de execução *"] .mensagem-celula')).toContainText(
    'Data e hora inválidas',
  );
  await expect(linhaDaPrevia(page, 12).locator('td[data-coluna="Status *"]')).toHaveClass(/celula-erro/);
  await expect(linhaDaPrevia(page, 2).locator('td.celula-erro')).toHaveCount(0);
  await projetoErrado.hover();
  await expect(page.getByRole('tooltip')).toContainText("Nenhum item de 'Projeto'");
  await capturarTela(page, 'importacao-4-previa-claro');

  await page.getByRole('switch', { name: 'Mostrar só linhas com erro' }).click();
  await expect(page.locator('.tabela-previa tbody tr')).toHaveCount(3);
  await capturar(page, 'importacao-4-previa-so-erros-claro');

  await page.getByRole('button', { name: 'Importar 17 itens' }).click();
  await expect(page.getByRole('heading', { name: '5. Importação concluída' })).toBeVisible();
  await expect(page.getByText('17 itens criados, 3 linhas com erro não importadas.')).toBeVisible();
  await capturar(page, 'importacao-5-conclusao-claro');

  // A listagem: os 17 relatórios, na equipe do teste.
  const lista = await listar(gestor.token, 'relatorio_teste', { 'bateria_testes.contem': prefixo, tamanho: '50' });
  expect(lista.total).toBe(17);
  expect(lista.itens.every((i) => i.equipeId === equipeId)).toBeTruthy();
  await page.getByRole('button', { name: 'Ver na listagem' }).click();
  await expect(page.getByRole('heading', { name: 'Relatórios de Teste', level: 1 })).toBeVisible();
  await expect(page.getByRole('link', { name: `${prefixo} R20` })).toBeVisible();

  // O histórico do item mostra a origem "Importação".
  await page.getByRole('link', { name: `${prefixo} R20` }).click();
  await page.getByRole('tab', { name: 'Histórico' }).click();
  await expect(page.locator('.origem-historico')).toContainText('Importação');

  // E o histórico de importações.
  const linha = await importacaoNoHistorico(page, RELATORIOS, `relatorios-${prefixo}.xlsx`);
  await expect(linha).toContainText('17 novos · 3 com erro');
  await expect(linha).toContainText('Concluída');
  await capturar(page, 'importacao-historico-claro');
});

test('3. importar cenários para esses relatórios: referência pelo nome, dependência do endpoint e duplicado no arquivo', async ({
  page,
}) => {
  test.setTimeout(120_000);
  const cenario = (relatorio: string, endpoint: string, numero: number) => [
    `${prefixo} ${relatorio}`,
    endpoint,
    numero,
    100,
    250,
    480,
    35.5,
    0.25,
  ];
  const arquivo = await novaPlanilha(CABECALHOS_CENARIO, [
    cenario('R01', `consulta ${prefixo}`, 1),
    cenario('R02', `consulta ${prefixo}`, 1),
    cenario('R03', `CONSULTA ${prefixo}`, 1),
    // Endpoint de outro projeto: a dependência (o endpoint precisa ser do projeto do relatório, RN08) recusa.
    cenario('R05', `outro ${prefixo}`, 1),
    // Duplicado no arquivo (Relatório + Nº do cenário): erro nas duas linhas (RN102).
    cenario('R06', `consulta ${prefixo}`, 2),
    cenario('R06', `consulta ${prefixo}`, 2),
  ]);

  await entrarComToken(page, gestor.token, `${CENARIOS}/importar`);
  await enviarArquivo(page, `cenarios-${prefixo}.xlsx`, arquivo);
  await irParaPrevia(page);

  await expect(totalDaPrevia(page, 'resumo-novos')).toHaveText('3');
  await expect(totalDaPrevia(page, 'resumo-erros')).toHaveText('3');
  await expect(linhaDaPrevia(page, 5).locator('td[data-coluna="Endpoint *"] .mensagem-celula')).toContainText(
    "precisa ter 'Projeto' igual ao 'Projeto' do registro escolhido em 'Relatório'",
  );
  await expect(linhaDaPrevia(page, 6)).toContainText('Repetida no arquivo');
  await expect(linhaDaPrevia(page, 7)).toContainText('Repetida no arquivo');

  await page.getByRole('button', { name: 'Importar 3 itens' }).click();
  await expect(page.getByText('3 itens criados, 3 linhas com erro não importadas.')).toBeVisible();
  const cenarios = await listar(gestor.token, 'cenario_teste', { 'relatorio_teste.bateria_testes.contem': prefixo });
  expect(cenarios.total).toBe(3);

  // 4. O relatório de resultado: as linhas originais e as colunas Resultado e Mensagens.
  relatorioCenarios = await baixar(page, () => page.getByRole('button', { name: 'Baixar relatório' }).click());
  const [resultado] = await lerPlanilha(relatorioCenarios);
  expect(resultado.linhas[0].slice(-2)).toEqual(['Resultado', 'Mensagens']);
  expect(resultado.linhas[1][8]).toBe('Criado');
  expect(resultado.linhas[4][8]).toBe('Erro');
  expect(resultado.linhas[4][9]).toContain('Endpoint *:');

  // 5. Desfazer a importação dos cenários (na conclusão).
  await page.getByRole('button', { name: 'Desfazer importação' }).click();
  const confirmacao = page.getByRole('alertdialog').filter({ hasText: 'exclui 3 itens criados' });
  await expect(confirmacao).toBeVisible();
  await confirmacao.getByRole('button', { name: 'Desfazer importação' }).click();
  await expect(page.getByRole('heading', { name: 'Importação desfeita' })).toBeVisible();
  expect(
    (await listar(gestor.token, 'cenario_teste', { 'relatorio_teste.bateria_testes.contem': prefixo })).total,
  ).toBe(0);
});

test('5. desfazer a importação dos relatórios (sem cenários) e recusar o desfazer de um relatório alterado depois', async ({
  page,
}) => {
  test.setTimeout(120_000);
  await entrarComToken(page, gestor.token, `${RELATORIOS}/importacoes`);
  const linha = await importacaoNoHistorico(page, RELATORIOS, `relatorios-${prefixo}.xlsx`);
  await linha.getByRole('button', { name: `Desfazer a importação de relatorios-${prefixo}.xlsx` }).click();
  await confirmarDesfazer(page);
  await expect(page.getByText('Importação desfeita: os itens criados por ela foram excluídos.')).toBeVisible();
  await expect(linha).toContainText('Desfeita');
  expect((await listar(gestor.token, 'relatorio_teste', { 'bateria_testes.contem': prefixo })).total).toBe(0);

  // De novo, agora com um relatório alterado depois da importação.
  const arquivo = await preencherModelo(modeloRelatorios, [linhaRelatorio(30), linhaRelatorio(31)]);
  await page.goto(`${RELATORIOS}/importar`);
  await enviarArquivo(page, `alterados-${prefixo}.xlsx`, arquivo);
  await irParaPrevia(page);
  await page.getByRole('button', { name: 'Importar 2 itens' }).click();
  await expect(page.getByText('2 itens criados.')).toBeVisible();

  const [item] = (await listar(gestor.token, 'relatorio_teste', { bateria_testes: `${prefixo} R30` })).itens;
  const lido = await chamar<{ versao: string; dados: Record<string, unknown> }>(
    gestor.token,
    'GET',
    `/grupos/${GRUPO}/entidades/relatorio_teste/registros/${item.id}`,
  );
  const corpo = Object.fromEntries(
    Object.entries(lido.corpo.dados).map(([chave, valor]) => [
      chave,
      valor && typeof valor === 'object' && 'id' in valor ? (valor as { id: string }).id : valor,
    ]),
  );
  const alteracao = await chamar(
    gestor.token,
    'PUT',
    `/grupos/${GRUPO}/entidades/relatorio_teste/registros/${item.id}`,
    {
      ...corpo,
      tipo_teste: 'Estresse',
      versao: lido.corpo.versao,
    },
  );
  expect(alteracao.status).toBe(200);

  const recusada = await importacaoNoHistorico(page, RELATORIOS, `alterados-${prefixo}.xlsx`);
  await recusada.getByRole('button', { name: `Desfazer a importação de alterados-${prefixo}.xlsx` }).click();
  await confirmarDesfazer(page);
  const aviso = page.getByRole('alertdialog').filter({ hasText: 'Não é possível desfazer' });
  await expect(aviso).toContainText('Itens alterados depois da importação.');
  await expect(aviso).toContainText(`${prefixo} R30`);
  await capturarTela(page, 'importacao-desfazer-recusado-claro');
  await aviso.getByRole('button', { name: 'Entendi' }).click();
  expect((await listar(gestor.token, 'relatorio_teste', { 'bateria_testes.contem': prefixo })).total).toBe(2);
});

test('6. importar um CSV com separador ";" e números com vírgula decimal', async ({ page }) => {
  const csv = [
    CABECALHOS_CENARIO.map((c) => c.replace(' *', '')).join(';'),
    [`${prefixo} R31`, `consulta ${prefixo}`, '7', '1.200', '250', '480', '1.234,56', '0,25'].join(';'),
  ].join('\r\n');

  await entrarComToken(page, gestor.token, `${CENARIOS}/importar`);
  await enviarArquivo(
    page,
    `cenarios-${prefixo}.csv`,
    Buffer.concat([Buffer.from([0xef, 0xbb, 0xbf]), Buffer.from(csv, 'utf8')]),
    'text/csv',
  );
  await irParaPrevia(page);
  await expect(totalDaPrevia(page, 'resumo-novos')).toHaveText('1');
  await page.getByRole('button', { name: 'Importar 1 item' }).click();
  await expect(page.getByText('1 item criado.')).toBeVisible();

  const [cenario] = (
    await listar(gestor.token, 'cenario_teste', { 'relatorio_teste.bateria_testes': `${prefixo} R31` })
  ).itens;
  expect(Number(cenario.dados['vazao_media_tps'])).toBe(1234.56);
  expect(Number(cenario.dados['taxa_erro_percentual'])).toBe(0.25);
  expect(Number(cenario.dados['usuarios_virtuais'])).toBe(1200);
});

test('7. o EXTERNO não vê o botão Importar onde não pode criar', async ({ page }) => {
  await entrarComToken(page, externo.token, RELATORIOS);
  await expect(page.getByRole('heading', { name: 'Relatórios de Teste', level: 1 })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Exportar' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Importar', exact: true })).toHaveCount(0);
  // Nem pelo endereço direto.
  await page.goto(`${RELATORIOS}/importar`);
  await expect(page.getByRole('heading', { name: /permissão/i, level: 1 })).toBeVisible();
});

test('8. capturas do assistente, do modelo e do histórico no tema escuro', async ({ page }) => {
  test.setTimeout(120_000);
  await page.addInitScript(() => localStorage.setItem('catalogo.tema', 'escuro'));
  await entrarComToken(page, gestor.token, `${RELATORIOS}/importar`);
  await expect(page.getByRole('heading', { name: '1. Escolha a planilha' })).toBeVisible();
  await capturar(page, 'importacao-1-arquivo-escuro');

  const arquivo = await preencherModelo(modeloRelatorios, [
    linhaRelatorio(40),
    linhaRelatorio(41, { projeto: 'Projeto que não existe' }),
    linhaRelatorio(42, { data: '31/02/2026 10:00' }),
  ]);
  await enviarArquivo(page, `escuro-${prefixo}.xlsx`, arquivo);
  await capturar(page, 'importacao-2-colunas-escuro');
  await page.getByRole('button', { name: 'Continuar' }).click();
  await capturar(page, 'importacao-3-opcoes-escuro');
  await page.getByRole('button', { name: 'Ver prévia' }).click();
  await expect(totalDaPrevia(page, 'resumo-erros')).toHaveText('2');
  await linhaDaPrevia(page, 3).locator('td[data-coluna="Projeto *"]').hover();
  await expect(page.getByRole('tooltip')).toBeVisible();
  await capturarTela(page, 'importacao-4-previa-escuro');
  await page.getByRole('button', { name: 'Importar 1 item' }).click();
  await expect(page.getByRole('heading', { name: '5. Importação concluída' })).toBeVisible();
  await capturar(page, 'importacao-5-conclusao-escuro');

  const linha = await importacaoNoHistorico(page, RELATORIOS, `escuro-${prefixo}.xlsx`);
  await expect(linha).toContainText('Concluída');
  await capturar(page, 'importacao-historico-escuro');

  await mostrarModelo(page, modeloRelatorios, 'escuro');
  await capturar(page, 'importacao-modelo-escuro');
});
