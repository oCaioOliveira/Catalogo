import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

/**
 * Acesso direto à API para PREPARAR os dados de cada teste (não é o que está sendo testado). Cada teste cria os
 * próprios itens com um prefixo único, para não depender da ordem nem de outros testes.
 */

export const API = (process.env.CATALOGO_API ?? 'http://localhost:5080').replace(/\/$/, '');
export const GRUPO = 'testes_desempenho';
const ARQUIVO_TOKENS = join(__dirname, '..', '.estado', 'tokens.json');

export type UsuarioDev = 'admin' | 'dev.externo' | 'dev.interno' | 'dev.gestor';

export function senhaDe(usuario: UsuarioDev): string {
  const senha = usuario === 'admin' ? process.env['CATALOGO_ADMIN_SENHA'] : process.env['CATALOGO_SENHA_DEV'];
  if (!senha) {
    throw new Error('Defina CATALOGO_ADMIN_SENHA e CATALOGO_SENHA_DEV (as mesmas do npm run dados-dev).');
  }
  return senha;
}

/** Um prefixo único por teste: E2E-<hora><aleatório>. */
export function prefixoUnico(): string {
  return `E2E-${Date.now().toString(36)}${Math.floor(Math.random() * 1296).toString(36)}`.toUpperCase();
}

export async function chamar<T = Record<string, unknown>>(
  token: string | null,
  metodo: string,
  caminho: string,
  corpo?: unknown,
): Promise<{ status: number; corpo: T }> {
  const resposta = await fetch(`${API}/api/v1${caminho}`, {
    method: metodo,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: corpo === undefined ? undefined : JSON.stringify(corpo),
  });
  const texto = await resposta.text();
  return { status: resposta.status, corpo: (texto ? JSON.parse(texto) : null) as T };
}

export async function entrarPelaApi(matricula: string, senha: string): Promise<string> {
  const { status, corpo } = await chamar<{ token: string }>(null, 'POST', '/auth/login', { matricula, senha });
  if (status !== 200) {
    throw new Error(`Login de ${matricula} falhou (${status}). Rodou o npm run dados-dev? As senhas estão certas?`);
  }
  return corpo.token;
}

export function gravarTokens(tokens: Record<string, string>): void {
  mkdirSync(dirname(ARQUIVO_TOKENS), { recursive: true });
  writeFileSync(ARQUIVO_TOKENS, JSON.stringify(tokens), { mode: 0o600 });
}

/** O token de um usuário de exemplo, obtido uma vez na preparação global (o login da API tem limite por minuto). */
export function tokenDe(usuario: UsuarioDev): string {
  const tokens = JSON.parse(readFileSync(ARQUIVO_TOKENS, 'utf8')) as Record<string, string>;
  return tokens[usuario];
}

interface Pagina {
  itens: { id: string; equipeId: string | null; dados: Record<string, unknown> }[];
  total: number;
}

export async function listar(token: string, entidade: string, filtros: Record<string, string>): Promise<Pagina> {
  const consulta = new URLSearchParams(filtros).toString();
  return (await chamar<Pagina>(token, 'GET', `/grupos/${GRUPO}/entidades/${entidade}/registros?${consulta}`)).corpo;
}

/** O id do registro com `campo = valor` (criado pelo npm run dados-dev). */
export async function idPor(token: string, entidade: string, campo: string, valor: string): Promise<string> {
  const pagina = await listar(token, entidade, { [campo]: valor, tamanho: '1' });
  if (!pagina.itens[0]) {
    throw new Error(`Não achei ${entidade}.${campo} = "${valor}". Rode o npm run dados-dev.`);
  }
  return pagina.itens[0].id;
}

export async function criar(token: string, entidade: string, dados: Record<string, unknown>): Promise<string> {
  const { status, corpo } = await chamar<{ id: string }>(
    token,
    'POST',
    `/grupos/${GRUPO}/entidades/${entidade}/registros`,
    dados,
  );
  if (status !== 201) {
    throw new Error(`POST ${entidade}: ${status} ${JSON.stringify(corpo)}`);
  }
  return corpo.id;
}

/** Os ids dos cadastros do npm run dados-dev usados pelos testes (Equipe Alfa). */
export async function cadastrosAlfa(token: string): Promise<{
  equipe: string;
  projeto: string;
  outroProjeto: string;
  rtc: string;
  ambiente: string;
  status: string;
}> {
  // Busca pelo nome: as equipes de testes antigos (desativadas, "E2E-…") vêm antes na ordem e podem passar de uma página.
  const equipes = (
    await chamar<{ itens: { id: string; nome: string }[] }>(token, 'GET', '/equipes?busca=Equipe%20Alfa&tamanho=100')
  ).corpo.itens;
  return {
    equipe: equipes.find((e) => e.nome === 'Equipe Alfa')!.id,
    projeto: await idPor(token, 'projeto', 'nome', 'Transferências Alfa'),
    outroProjeto: await idPor(token, 'projeto', 'nome', 'Consultas Alfa'),
    rtc: await idPor(token, 'rtc', 'rtc_externo_id', 'RTC-ALFA-101'),
    ambiente: await idPor(token, 'ambiente', 'nome', 'Homologação'),
    status: await idPor(token, 'status_resultado', 'descricao', 'Aprovado'),
  };
}

/** Um relatório completo na Equipe Alfa, pela API. */
export async function criarRelatorio(
  token: string,
  bateria: string,
  extras: Record<string, unknown> = {},
): Promise<string> {
  // Os ids são buscados como administrador (só ele lista as equipes); o relatório é criado com o token informado.
  const c = await cadastrosAlfa(tokenDe('admin'));
  return criar(token, 'relatorio_teste', {
    equipeId: c.equipe,
    projeto: c.projeto,
    rtc: c.rtc,
    ambiente: c.ambiente,
    status_resultado: c.status,
    data_execucao: '2026-09-15T10:00:00-03:00',
    bateria_testes: bateria,
    tipo_teste: 'Carga',
    executado_por: 'Teste E2E',
    ...extras,
  });
}
