import { Locator, Page, expect, test } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { cadastrosAlfa, chamar, prefixoUnico, tokenDe } from './apoio/api';
import { campo, campoSelecao, entrarComo, escolherReferencia } from './apoio/sessao';

/**
 * Administração de listagens pela INTERFACE (etapa 5): criar a listagem "Inventário" do zero, usá-la pelas telas normais e alterar
 * a estrutura com dados (relatório de bloqueio, valor padrão, unicidade, desativar, dependência, ordem, permissões e conflito).
 * Os testes rodam EM SÉRIE e compartilham a listagem criada no 1º, com um prefixo único. A estrutura de Testes de Desempenho não é
 * tocada. A listagem nasce sem equipes (RN93): o 1º teste habilita a Equipe Alfa pela aba Equipes. No fim, a listagem é DESATIVADA
 * (senão ela viraria a tela inicial dos outros testes; a preparação global desativa sobras).
 */
test.describe.configure({ mode: 'serial' });

const prefixo = prefixoUnico();
const NOME_LISTAGEM = `Inventário ${prefixo}`;
const GRUPO = `inventario_${prefixo.toLowerCase().replace(/-/g, '_')}`;
const admin = () => tokenDe('admin');

async function capturarListagem(page: Page, nome: string, paginaInteira = true): Promise<void> {
  // Sem os avisos de "feito" por cima e com as animações (painel lateral, diálogo) já terminadas.
  for (const fechar of await page.locator('.p-toast-close-button').all()) {
    await fechar.click({ timeout: 1000 }).catch(() => undefined);
  }
  await page.waitForTimeout(600);
  const pasta = join(__dirname, 'capturas', 'listagens');
  mkdirSync(pasta, { recursive: true });
  await page.screenshot({ path: join(pasta, `${nome}.png`), fullPage: paginaInteira });
}

// ---- Atalhos da administração (pela tela) ----

async function criarTipo(page: Page, nome: string, plural: string, global = false): Promise<void> {
  await page.getByRole('button', { name: 'Novo tipo de item' }).click();
  const dialogo = page.getByRole('dialog');
  await campo(dialogo, /^Nome \(singular\)/).fill(nome);
  await campo(dialogo, 'Nome no plural').fill(plural);
  if (global) {
    await dialogo.getByRole('radio', { name: /^Global/ }).check();
  }
  await dialogo.getByRole('button', { name: 'Criar', exact: true }).click();
  await expect(page.getByRole('heading', { level: 1, name: nome })).toBeVisible();
}

interface NovoCampo {
  nome: string;
  tipo?: string;
  obrigatorio?: boolean;
  tamanho?: number;
  precisao?: number;
  escala?: number;
  opcoes?: string[];
  referencia?: string;
}

function painel(page: Page): Locator {
  return page.locator('.painel-lateral-campo');
}

async function digitarNumero(alvo: Locator, valor: number): Promise<void> {
  await alvo.click();
  await alvo.press('Control+a');
  await alvo.pressSequentially(String(valor));
  await alvo.press('Tab');
}

async function escolherOpcao(page: Page, controle: Locator, opcao: string | RegExp): Promise<void> {
  await controle.click();
  await page.getByRole('option', { name: opcao, exact: typeof opcao === 'string' }).click();
}

async function criarCampo(page: Page, dados: NovoCampo): Promise<void> {
  await page.getByRole('button', { name: 'Novo campo' }).click();
  const p = painel(page);
  await campo(p, /^Nome/).fill(dados.nome);
  if (dados.tipo) {
    await escolherOpcao(page, campoSelecao(p, 'Tipo'), dados.tipo);
  }
  if (dados.obrigatorio) {
    await p.getByRole('switch', { name: 'Obrigatório' }).check();
  }
  if (dados.tamanho) {
    await digitarNumero(p.getByRole('spinbutton', { name: 'Tamanho máximo (caracteres)' }), dados.tamanho);
  }
  if (dados.precisao !== undefined) {
    await digitarNumero(p.getByRole('spinbutton', { name: 'Precisão (total de dígitos)' }), dados.precisao);
    await digitarNumero(p.getByRole('spinbutton', { name: 'Escala (casas decimais)' }), dados.escala ?? 0);
  }
  for (const [i, opcao] of (dados.opcoes ?? []).entries()) {
    await p.getByRole('button', { name: 'Adicionar opção' }).click();
    await p.getByRole('textbox', { name: `Opção ${i + 1}` }).fill(opcao);
  }
  if (dados.referencia) {
    await escolherOpcao(page, campoSelecao(p, 'Tipo de item referenciado'), dados.referencia);
  }
  await p.getByRole('button', { name: 'Criar campo' }).click();
  await expect(p).toBeHidden();
  await expect(page.locator('.tabela-admin tbody tr', { hasText: dados.nome }).first()).toBeVisible();
}

async function definirExibicao(page: Page, entidade: string, nomeCampo: string): Promise<void> {
  await page.getByRole('tab', { name: 'Geral' }).click();
  await escolherOpcao(page, campoSelecao(page, 'Campo de exibição'), nomeCampo);
  await page.getByRole('button', { name: 'Salvar alterações' }).click();
  await expect(page.getByText(`Alterar "${entidade}": feito.`)).toBeVisible();
}

async function abrirEditor(page: Page, entidade: string, aba?: string): Promise<void> {
  await entrarComo(page, 'admin', `/configuracoes/areas/${GRUPO}/entidades/${entidade}`);
  await expect(page.getByRole('tab', { name: 'Geral' })).toBeVisible();
  if (aba) {
    await page.getByRole('tab', { name: aba }).click();
  }
}

// ---- Atalhos de dados (pela API, só para PREPARAR) ----

async function criarItem(entidade: string, dados: Record<string, unknown>): Promise<string> {
  const { status, corpo } = await chamar<{ id: string }>(
    admin(),
    'POST',
    `/grupos/${GRUPO}/entidades/${entidade}/registros`,
    dados,
  );
  expect(status, JSON.stringify(corpo)).toBe(201);
  return corpo.id;
}

async function idPorNome(entidade: string, nome: string): Promise<string> {
  const { corpo } = await chamar<{ itens: { id: string }[] }>(
    admin(),
    'GET',
    `/grupos/${GRUPO}/entidades/${entidade}/registros?nome=${encodeURIComponent(nome)}`,
  );
  return corpo.itens[0].id;
}

async function estrutura(): Promise<{
  versao: string;
  entidades: { chave: string; versao: string }[];
}> {
  return (
    await chamar<{ versao: string; entidades: { chave: string; versao: string }[] }>(
      admin(),
      'GET',
      `/admin/metadados/grupos/${GRUPO}`,
    )
  ).corpo;
}

async function versaoDe(entidade: string): Promise<string> {
  return (await estrutura()).entidades.find((e) => e.chave === entidade)!.versao;
}

let equipe = '';

test.afterAll(async () => {
  const atual = await chamar<{ versao: string; ativo: boolean }>(admin(), 'GET', `/admin/metadados/grupos/${GRUPO}`);
  if (atual.status === 200 && atual.corpo.ativo) {
    await chamar(admin(), 'PATCH', `/admin/metadados/grupos/${GRUPO}/ativacao`, {
      versao: atual.corpo.versao,
      ativo: false,
    });
  }
});

test('1. cria a listagem "Inventário" pela interface: categoria (global) e equipamento (por equipe)', async ({
  page,
}) => {
  test.setTimeout(180_000);
  equipe = (await cadastrosAlfa(admin())).equipe;
  await entrarComo(page, 'admin', '/configuracoes/areas');
  await page.getByRole('button', { name: 'Nova área de gestão' }).click();
  const dialogo = page.getByRole('dialog');
  await campo(dialogo, /^Nome/).fill(NOME_LISTAGEM);
  // A chave é gerada a partir do nome (minúsculas, sem acentos, com underscore).
  await expect(campo(dialogo, 'Chave')).toHaveValue(GRUPO);
  await capturarListagem(page, 'guia-01-nova-listagem');
  await dialogo.getByRole('button', { name: 'Criar área de gestão' }).click();
  await expect(page).toHaveURL(new RegExp(`/configuracoes/areas/${GRUPO}$`));
  await expect(page.getByRole('heading', { level: 1, name: NOME_LISTAGEM })).toBeVisible();

  // categoria: global, com nome único.
  await page.getByRole('button', { name: 'Novo tipo de item' }).click();
  await campo(page.getByRole('dialog'), /^Nome \(singular\)/).fill('Categoria');
  await capturarListagem(page, 'guia-02-novo-tipo-de-item');
  await page.getByRole('dialog').getByRole('button', { name: 'Cancelar' }).click();
  await criarTipo(page, 'Categoria', 'Categorias', true);
  await page.getByRole('tab', { name: 'Campos' }).click();
  await criarCampo(page, { nome: 'Nome', obrigatorio: true, tamanho: 50 });
  await definirExibicao(page, 'Categoria', 'Nome');
  await page.getByRole('tab', { name: 'Unicidades' }).click();
  await escolherOpcao(page, page.locator('.nova-unicidade p-multiselect'), 'Nome');
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Criar unicidade' }).click();
  await expect(page.locator('.lista-unicidades')).toContainText('Nome');
  await capturarListagem(page, 'guia-05-unicidades');

  // equipamento: por equipe, com todos os tipos pedidos.
  await page.getByRole('navigation', { name: 'Trilha' }).getByRole('link', { name: NOME_LISTAGEM }).click();
  await criarTipo(page, 'Equipamento', 'Equipamentos');
  await page.getByRole('tab', { name: 'Campos' }).click();
  await page.getByRole('button', { name: 'Novo campo' }).click();
  await campo(painel(page), /^Nome/).fill('Nome');
  await capturarListagem(page, 'guia-03-novo-campo');
  await painel(page).getByRole('button', { name: 'Cancelar' }).click();
  await expect(painel(page)).toBeHidden();
  await criarCampo(page, { nome: 'Nome', obrigatorio: true, tamanho: 100 });
  await criarCampo(page, {
    nome: 'Categoria',
    tipo: 'Referência a outro tipo de item',
    obrigatorio: true,
    referencia: 'Categoria',
  });
  await page.getByRole('button', { name: 'Editar o campo Categoria' }).click();
  await expect(painel(page).getByRole('radio', { name: /^Restringir/ })).toBeChecked();
  await capturarListagem(page, 'guia-04-campo-referencia');
  await painel(page).getByRole('button', { name: 'Cancelar' }).click();
  await expect(painel(page)).toBeHidden();
  await criarCampo(page, { nome: 'Data de aquisição', tipo: 'Data' });
  await criarCampo(page, { nome: 'Valor', tipo: 'Número decimal', precisao: 10, escala: 2 });
  await criarCampo(page, { nome: 'Situação', tipo: 'Lista de opções', opcoes: ['Em uso', 'Baixado'] });
  await criarCampo(page, { nome: 'Manual', tipo: 'Endereço (URL)' });
  await criarCampo(page, { nome: 'Observações', tipo: 'Texto longo' });
  await expect(page.locator('.tabela-admin tbody tr')).toHaveCount(7);
  await definirExibicao(page, 'Equipamento', 'Nome');

  // Captura: o painel lateral de um campo LISTA.
  await page.getByRole('tab', { name: 'Campos' }).click();
  await page.getByRole('button', { name: 'Editar o campo Situação' }).click();
  await expect(painel(page).getByRole('textbox', { name: 'Opção 2' })).toHaveValue('Baixado');
  await capturarListagem(page, '04-painel-lateral-campo-lista');
  await painel(page).getByRole('button', { name: 'Cancelar' }).click();
  await expect(painel(page)).toBeHidden();
  await capturarListagem(page, '02-editor-equipamento-campos');

  // RN93: a listagem nasce sem equipes (só administradores a veem). Habilita a Equipe Alfa pela aba Equipes.
  await page.getByRole('navigation', { name: 'Trilha' }).getByRole('link', { name: NOME_LISTAGEM }).click();
  await expect(page.getByText('Nenhuma equipe habilitada')).toBeVisible();
  await page.getByRole('button', { name: 'Habilitar equipes' }).click();
  await expect(page).toHaveURL(/aba=equipes/);
  await page.getByRole('checkbox', { name: 'Equipe Alfa', exact: true }).check();
  await capturarListagem(page, 'guia-07-habilitar-equipes', false);
  await page.getByRole('button', { name: 'Salvar alterações' }).click();
  await expect(page.getByText('Alterar as equipes da área de gestão: feito.')).toBeVisible();
  await expect(page.getByText('Nenhuma equipe habilitada')).toHaveCount(0);
});

test('2. a área aparece no seletor do cabeçalho; o administrador cadastra pelas telas normais, filtra e ordena', async ({
  page,
}) => {
  await entrarComo(page, 'admin', '/testes_desempenho/relatorio_teste');
  const seletor = campoSelecao(page, 'Área de gestão');
  await seletor.click();
  await expect(page.getByRole('option', { name: NOME_LISTAGEM })).toBeVisible();
  // Só a área visível: a captura da página inteira redimensiona a tela, o que fecharia a lista aberta.
  await capturarListagem(page, '08-seletor-de-listagem', false);
  await page.getByRole('option', { name: NOME_LISTAGEM }).click();
  // Trocar a área no cabeçalho abre o Início dela; os atalhos são os tipos POR EQUIPE (a categoria é global).
  await expect(page).toHaveURL(new RegExp(`/${GRUPO}$`));
  await expect(page.locator('.grade-atalhos')).toContainText('Equipamentos');
  await expect(page.locator('.grade-atalhos')).not.toContainText('Categorias');
  const menu = page.getByRole('navigation', { name: 'Menu da área de gestão' });
  await expect(menu.getByRole('link', { name: 'Equipamentos' })).toBeVisible();

  // Os cadastros de apoio (globais) ficam no grupo recolhível do menu.
  await menu.getByRole('button', { name: 'Cadastros de apoio' }).click();
  await menu.getByRole('link', { name: 'Categorias' }).click();
  await page.getByRole('button', { name: 'Novo' }).click();
  await campo(page, /^Nome/).fill(`Notebook ${prefixo}`);
  await page.getByRole('button', { name: /^Salvar\b/ }).click();
  await expect(page.getByRole('heading', { level: 1, name: `Notebook ${prefixo}` })).toBeVisible();

  await menu.getByRole('link', { name: 'Equipamentos' }).click();
  await page.getByRole('button', { name: 'Novo' }).click();
  await escolherOpcao(page, campoSelecao(page, /^Equipe/), 'Equipe Alfa');
  await campo(page, /^Nome/).fill(`${prefixo} Notebook do setor de compras`);
  await escolherReferencia(page, /^Categoria/, 'Notebook', `Notebook ${prefixo}`);
  await page.getByRole('spinbutton', { name: 'Valor' }).pressSequentially('4500');
  await page.getByRole('button', { name: /^Salvar\b/ }).click();
  await expect(page.getByRole('heading', { level: 1, name: `${prefixo} Notebook do setor de compras` })).toBeVisible();

  const monitor = await criarItem('categoria', { nome: `Monitor ${prefixo}` });
  await criarItem('equipamento', { equipeId: equipe, nome: `${prefixo} Monitor`, categoria: monitor, valor: 800 });
  await criarItem('equipamento', {
    equipeId: equipe,
    nome: `${prefixo} Notebook 2`,
    categoria: await idPorNome('categoria', `Notebook ${prefixo}`),
    valor: 3000,
  });

  await page.goto(`/${GRUPO}/equipamento`);
  await expect(page.locator('tbody tr')).toHaveCount(3);
  await page.getByRole('button', { name: /^Filtros/ }).click();
  await campo(page, 'Nome (contém)').fill('Monitor');
  await page.getByRole('button', { name: 'Filtrar', exact: true }).click();
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await expect(page.locator('tbody tr').first()).toContainText(`${prefixo} Monitor`);

  await page.goto(`/${GRUPO}/equipamento`);
  await page.getByRole('columnheader', { name: /^Valor/ }).click();
  await expect(page).toHaveURL(/ordenacao=valor/);
  await expect(page.locator('tbody tr').first()).toContainText(`${prefixo} Monitor`);
});

test('3. diminuir o tamanho do nome abaixo de um valor existente mostra o relatório de bloqueio, com link para o item', async ({
  page,
}) => {
  await abrirEditor(page, 'equipamento', 'Campos');
  await page.getByRole('button', { name: 'Editar o campo Nome' }).click();
  // O prefixo tem uns 14 caracteres: com 30, só o nome longo ("... Notebook do setor de compras") passa do limite.
  await digitarNumero(painel(page).getByRole('spinbutton', { name: 'Tamanho máximo (caracteres)' }), 30);
  await painel(page).getByRole('button', { name: 'Salvar campo' }).click();

  const relatorio = page.locator('.dialogo-alteracao');
  await expect(relatorio).toContainText('Esta alteração não pode ser feita');
  await expect(relatorio).toContainText('1 item afetado');
  const link = relatorio.getByRole('link', { name: `${prefixo} Notebook do setor de compras` });
  await expect(link).toHaveAttribute('href', new RegExp(`/${GRUPO}/equipamento/`));
  await expect(relatorio).toContainText('O que fazer');
  await expect(relatorio.getByRole('button', { name: 'Confirmar' })).toHaveCount(0);
  await capturarListagem(page, '05-relatorio-de-bloqueio');
  await relatorio.getByRole('button', { name: 'Entendi' }).click();
  await expect(relatorio).toBeHidden();
});

test('4. tornar "Situação" obrigatória com itens vazios pede o valor padrão, e todos os itens ficam com ele', async ({
  page,
}) => {
  await abrirEditor(page, 'equipamento', 'Campos');
  await page.getByRole('button', { name: 'Editar o campo Situação' }).click();
  await painel(page).getByRole('switch', { name: 'Obrigatório' }).check();
  await painel(page).getByRole('button', { name: 'Salvar campo' }).click();

  const dialogo = page.locator('.dialogo-alteracao');
  await expect(dialogo).toContainText('3 itens estão sem valor em Situação');
  await escolherOpcao(page, campoSelecao(dialogo, /^Situação/), 'Em uso');
  await capturarListagem(page, '06-dialogo-valor-padrao');
  await dialogo.getByRole('button', { name: 'Continuar' }).click();
  await expect(dialogo).toContainText('3 itens receberão o valor padrão.');
  await dialogo.getByRole('button', { name: 'Confirmar' }).click();
  await expect(page.getByText(/Alterar o campo "Situação": feito\./)).toBeVisible();

  await page.goto(`/${GRUPO}/equipamento`);
  await expect(page.locator('tbody tr')).toHaveCount(3);
  for (const linha of await page.locator('tbody tr').all()) {
    await expect(linha).toContainText('Em uso');
  }
});

test('5. unicidade no nome com duplicados mostra os duplicados; corrigidos, ela é criada e barra repetições no formulário', async ({
  page,
}) => {
  const categoria = await idPorNome('categoria', `Notebook ${prefixo}`);
  const repetido = `${prefixo} Duplicado`;
  await criarItem('equipamento', { equipeId: equipe, nome: repetido, categoria, situacao: 'Em uso' });
  const segundo = await criarItem('equipamento', {
    equipeId: equipe,
    nome: repetido.toUpperCase(),
    categoria,
    situacao: 'Em uso',
  });

  await abrirEditor(page, 'equipamento', 'Unicidades');
  await escolherOpcao(page, page.locator('.nova-unicidade p-multiselect'), 'Nome');
  await page.keyboard.press('Escape');
  await page.getByRole('button', { name: 'Criar unicidade' }).click();
  const dialogo = page.locator('.dialogo-alteracao');
  await expect(dialogo).toContainText('Mesmos valores');
  await expect(dialogo.getByRole('link', { name: repetido, exact: true })).toBeVisible();
  await expect(dialogo.getByRole('link', { name: repetido.toUpperCase(), exact: true })).toBeVisible();
  await dialogo.getByRole('button', { name: 'Entendi' }).click();

  // Corrige o item repetido e tenta de novo.
  const atual = await chamar<{ versao: string; dados: Record<string, unknown> }>(
    admin(),
    'GET',
    `/grupos/${GRUPO}/entidades/equipamento/registros/${segundo}`,
  );
  const corrigido = await chamar(admin(), 'PUT', `/grupos/${GRUPO}/entidades/equipamento/registros/${segundo}`, {
    versao: atual.corpo.versao,
    nome: `${prefixo} Corrigido`,
    categoria,
    situacao: 'Em uso',
  });
  expect(corrigido.status).toBe(200);
  await page.getByRole('button', { name: 'Criar unicidade' }).click();
  // Permitida, com efeito nos itens existentes: confirma antes de gravar.
  await expect(dialogo).toContainText('5 itens passam a ser conferidos pela unicidade nova.');
  await dialogo.getByRole('button', { name: 'Confirmar' }).click();
  await expect(page.getByText(/Criar a unicidade de Nome: feito\./)).toBeVisible();
  await expect(page.locator('.lista-unicidades')).toContainText('Nome');

  // Agora o formulário barra a repetição, no campo.
  await page.goto(`/${GRUPO}/equipamento/novo`);
  await escolherOpcao(page, campoSelecao(page, /^Equipe/), 'Equipe Alfa');
  await campo(page, /^Nome/).fill(repetido.toLowerCase());
  await escolherReferencia(page, /^Categoria/, 'Notebook', `Notebook ${prefixo}`);
  await escolherOpcao(page, campoSelecao(page, /^Situação/), 'Em uso');
  await page.getByRole('button', { name: /^Salvar\b/ }).click();
  await expect(page.locator('#formulario-nome-erros')).toContainText('Já existe');
});

test('6. desativar um campo o tira do formulário e da tabela; reativar devolve os valores', async ({ page }) => {
  const id = await idPorNome('equipamento', `${prefixo} Monitor`);
  const atual = await chamar<{ versao: string; dados: Record<string, { id: string } | unknown> }>(
    admin(),
    'GET',
    `/grupos/${GRUPO}/entidades/equipamento/registros/${id}`,
  );
  const dados = atual.corpo.dados as Record<string, unknown>;
  await chamar(admin(), 'PUT', `/grupos/${GRUPO}/entidades/equipamento/registros/${id}`, {
    versao: atual.corpo.versao,
    nome: dados['nome'],
    categoria: (dados['categoria'] as { id: string }).id,
    valor: dados['valor'],
    situacao: dados['situacao'],
    manual: 'https://manuais.exemplo.com/monitor',
  });

  await abrirEditor(page, 'equipamento', 'Campos');
  await page.getByRole('button', { name: 'Desativar o campo Manual' }).click();
  await expect(page.getByText(/Desativar o campo "Manual": feito\./)).toBeVisible();
  await page.goto(`/${GRUPO}/equipamento`);
  await expect(page.locator('tbody tr').first()).toBeVisible();
  await expect(page.getByRole('columnheader', { name: 'Manual' })).toHaveCount(0);
  await page.goto(`/${GRUPO}/equipamento/novo`);
  await expect(page.getByRole('heading', { name: 'Novo: Equipamento' })).toBeVisible();
  await expect(page.getByRole('textbox', { name: 'Manual' })).toHaveCount(0);

  await abrirEditor(page, 'equipamento', 'Campos');
  await page.getByRole('button', { name: 'Reativar o campo Manual' }).click();
  await expect(page.getByText(/Reativar o campo "Manual": feito\./)).toBeVisible();
  await page.goto(`/${GRUPO}/equipamento/${id}`);
  await expect(page.getByRole('link', { name: /manuais\.exemplo\.com\/monitor/ })).toBeVisible();
});

test('7. monta uma dependência pelo construtor guiado e o formulário filtra as opções', async ({ page }) => {
  // Preparação pela API: "Modelo" (global, com a categoria) e o campo equipamento.modelo.
  const base = `/admin/metadados/grupos/${GRUPO}`;
  let resposta = await chamar(admin(), 'POST', `${base}/entidades`, {
    versao: (await estrutura()).versao,
    chave: 'modelo',
    nome: 'Modelo',
    nomePlural: 'Modelos',
    escopoEquipe: false,
  });
  expect(resposta.status).toBe(201);
  await chamar(admin(), 'POST', `${base}/entidades/modelo/campos`, {
    versao: await versaoDe('modelo'),
    chave: 'nome',
    nome: 'Nome',
    tipoDado: 'TEXTO',
    tamanhoMaximo: 50,
    obrigatorio: true,
  });
  await chamar(admin(), 'POST', `${base}/entidades/modelo/campos`, {
    versao: await versaoDe('modelo'),
    chave: 'categoria',
    nome: 'Categoria',
    tipoDado: 'REFERENCIA',
    entidadeReferenciada: 'categoria',
    acaoExclusao: 'RESTRINGIR',
  });
  await chamar(admin(), 'PUT', `${base}/entidades/modelo`, {
    versao: await versaoDe('modelo'),
    nome: 'Modelo',
    nomePlural: 'Modelos',
    escopoEquipe: false,
    campoExibicao: 'nome',
  });
  resposta = await chamar(admin(), 'POST', `${base}/entidades/equipamento/campos`, {
    versao: await versaoDe('equipamento'),
    chave: 'modelo',
    nome: 'Modelo',
    tipoDado: 'REFERENCIA',
    entidadeReferenciada: 'modelo',
    acaoExclusao: 'RESTRINGIR',
  });
  expect(resposta.status).toBe(201);
  await criarItem('modelo', {
    nome: `M-Notebook ${prefixo}`,
    categoria: await idPorNome('categoria', `Notebook ${prefixo}`),
  });
  await criarItem('modelo', {
    nome: `M-Monitor ${prefixo}`,
    categoria: await idPorNome('categoria', `Monitor ${prefixo}`),
  });

  await abrirEditor(page, 'equipamento', 'Dependências');
  const cartao = page.locator('.cartao-dependencia', { has: page.getByRole('heading', { name: 'Modelo' }) });
  await cartao.getByRole('button', { name: 'Criar uma regra' }).click();
  // Uma combinação só: os selects já vêm escolhidos, e a frase aparece.
  await expect(cartao.locator('.frase-dependencia')).toHaveText(
    'As opções de Modelo mostram só os itens de Modelos cujo Categoria é igual ao valor escolhido em Categoria neste mesmo formulário.',
  );
  await expect(cartao.locator('.exemplo-dependencia')).toContainText(
    '"Modelo" fica desabilitado até alguém escolher "Categoria"',
  );
  await capturarListagem(page, '03-editor-equipamento-dependencias');
  await cartao.getByRole('button', { name: 'Salvar regra' }).click();
  await expect(page.getByText(/Definir a dependência de "Modelo": feito\./)).toBeVisible();
  await expect(cartao.getByRole('button', { name: 'Alterar a regra' })).toBeVisible();

  await page.goto(`/${GRUPO}/equipamento/novo`);
  await escolherOpcao(page, campoSelecao(page, /^Equipe/), 'Equipe Alfa');
  await expect(campoSelecao(page, /^Modelo/)).toBeDisabled();
  await escolherReferencia(page, /^Categoria/, 'Monitor', `Monitor ${prefixo}`);
  await expect(campoSelecao(page, /^Modelo/)).toBeEnabled();
  await campoSelecao(page, /^Modelo/).click();
  await campoSelecao(page, /^Modelo/).pressSequentially('M-', { delay: 30 });
  await expect(page.getByRole('option', { name: `M-Monitor ${prefixo}` })).toBeVisible();
  await expect(page.getByRole('option', { name: `M-Notebook ${prefixo}` })).toHaveCount(0);
});

test('8. arrastar e soltar um campo muda a ordem no formulário', async ({ page }) => {
  await abrirEditor(page, 'equipamento', 'Campos');
  const alcaValor = page.getByLabel('Arrastar Valor');
  const linhaNome = page.locator('.tabela-admin tbody tr').first();
  await expect(linhaNome).toContainText('Nome');
  const origem = (await alcaValor.boundingBox())!;
  const destino = (await linhaNome.boundingBox())!;
  await page.mouse.move(origem.x + origem.width / 2, origem.y + origem.height / 2);
  await page.mouse.down();
  await page.mouse.move(origem.x + origem.width / 2, origem.y - 10, { steps: 5 });
  await page.mouse.move(destino.x + 20, destino.y + 2, { steps: 20 });
  await page.mouse.up();
  await expect(page.getByText(/Reordenar os campos: feito\./)).toBeVisible();
  await expect(page.locator('.tabela-admin tbody tr').first()).toContainText('Valor');

  await page.goto(`/${GRUPO}/equipamento/novo`);
  await expect(page.locator('.grade-campos app-campo-formulario .campo-rotulo').first()).toContainText('Valor');
});

test('9. GESTOR, INTERNO e EXTERNO não administram áreas de gestão: a rota (nova e antiga) mostra "sem permissão"', async ({
  browser,
}) => {
  for (const usuario of ['dev.gestor', 'dev.interno', 'dev.externo'] as const) {
    const contexto = await browser.newContext();
    const pagina = await contexto.newPage();
    await entrarComo(pagina, usuario, '/testes_desempenho/relatorio_teste');
    for (const rota of ['/configuracoes/areas', '/admin/listagens']) {
      await pagina.goto(rota);
      await expect(pagina.getByText('O seu perfil não tem acesso a esta página')).toBeVisible();
    }
    await contexto.close();
  }
});

test('10. duas abas editando a mesma estrutura: a segunda recebe o aviso de conflito', async ({ page, context }) => {
  const outra = await context.newPage();
  await abrirEditor(page, 'categoria');
  // O token fica no sessionStorage, que é de cada aba (ADR 0035): a segunda aba também entra.
  await entrarComo(outra, 'admin', `/configuracoes/areas/${GRUPO}/entidades/categoria`);
  await expect(outra.getByRole('tab', { name: 'Geral' })).toBeVisible();

  await campo(page, 'Nome no plural').fill('Categorias (aba 1)');
  await page.getByRole('button', { name: 'Salvar alterações' }).click();
  await expect(page.getByText(/: feito\./).first()).toBeVisible();

  await campo(outra, 'Nome no plural').fill('Categorias (aba 2)');
  await outra.getByRole('button', { name: 'Salvar alterações' }).click();
  const aviso = outra.locator('.dialogo-alteracao');
  await expect(aviso).toContainText('Outra pessoa alterou a estrutura desta área de gestão');
  await aviso.getByRole('button', { name: 'Recarregar' }).click();
  await expect(campo(outra, 'Nome no plural')).toHaveValue('Categorias (aba 1)');
});

test('capturas: lista de listagens, estrutura da listagem e pré-visualização', async ({ page }) => {
  await entrarComo(page, 'admin', `/configuracoes/areas/${GRUPO}`);
  await expect(page.locator('.tabela-admin tbody tr')).toHaveCount(3);
  await capturarListagem(page, 'guia-06-tipos-de-item');
  await abrirEditor(page, 'equipamento', 'Pré-visualização');
  await expect(page.getByRole('heading', { name: 'Colunas da tabela (padrão)' })).toBeVisible();
  await capturarListagem(page, '07-pre-visualizacao');
  await page.goto('/configuracoes/areas');
  await expect(page.getByRole('link', { name: NOME_LISTAGEM, exact: true })).toBeVisible();
  await capturarListagem(page, '01-lista-de-listagens');
});
