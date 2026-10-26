import { Browser, Page, expect } from '@playwright/test';
import { randomBytes } from 'node:crypto';
import { chamar } from './api';
import { entrarComToken } from './sessao';

/**
 * Usuários criados pelo próprio teste (matrícula "e2e-…", desativados pelo helper de limpeza, e2e/apoio/limpeza.ts). A senha
 * provisória é trocada por uma forte aleatória, que nunca é impressa nem gravada.
 */
export interface UsuarioTeste {
  id: string;
  matricula: string;
  token: string;
}

function senhaForte(): string {
  return `Nova${randomBytes(9).toString('base64url')}9a`;
}

/** Login pela API, com uma espera se o limite de 10 por minuto (RN23) estourar. */
export async function entrarComEspera(matricula: string, senha: string): Promise<string> {
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

/** Cria o usuário nas equipes, entra com a senha provisória e troca a senha (o token continua válido, RN25). */
export async function criarUsuarioDeTeste(
  admin: string,
  prefixo: string,
  sufixo: string,
  nome: string,
  perfil: string,
  equipes: string[],
): Promise<UsuarioTeste> {
  const matricula = `${prefixo.toLowerCase()}-${sufixo}`;
  const { status, corpo } = await chamar<{ senhaProvisoria: string; usuario: { id: string } }>(
    admin,
    'POST',
    '/usuarios',
    {
      matricula,
      nome: `${nome} ${prefixo}`,
      perfil,
      equipes,
    },
  );
  expect(status, `POST /usuarios ${perfil}`).toBe(201);
  const token = await entrarComEspera(matricula, corpo.senhaProvisoria);
  const troca = await chamar(token, 'POST', '/auth/trocar-senha', {
    senhaAtual: corpo.senhaProvisoria,
    novaSenha: senhaForte(),
  });
  expect(troca.status, 'trocar-senha').toBe(204);
  return { id: corpo.usuario.id, matricula, token };
}

/** Uma página nova (outro contexto: outra sessão, sem o localStorage das outras) já logada com o token. */
export async function abrirComo(browser: Browser, token: string, caminho: string): Promise<Page> {
  const contexto = await browser.newContext({
    locale: 'pt-BR',
    timezoneId: 'America/Sao_Paulo',
    viewport: { width: 1400, height: 900 },
    reducedMotion: 'reduce',
  });
  const pagina = await contexto.newPage();
  await entrarComToken(pagina, token, caminho);
  return pagina;
}
