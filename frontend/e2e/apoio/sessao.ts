import { Locator, Page, expect } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { tokenDe, UsuarioDev } from './api';

/** Abre o front já logado como o usuário (o token vai para o sessionStorage, como faz o login, ADR 0035). */
export async function entrarComo(page: Page, usuario: UsuarioDev, caminho = '/'): Promise<void> {
  await page.addInitScript((token) => sessionStorage.setItem('catalogo.token', token), tokenDe(usuario));
  await page.goto(caminho);
  await expect(botaoMenuUsuario(page)).toBeVisible();
}

/** O mesmo que entrarComo, com o token de um usuário criado no próprio teste. */
export async function entrarComToken(page: Page, token: string, caminho = '/'): Promise<void> {
  await page.addInitScript((t) => sessionStorage.setItem('catalogo.token', t), token);
  await page.goto(caminho);
  await expect(botaoMenuUsuario(page)).toBeVisible();
}

/** O avatar do cabeçalho, que abre o menu do usuário (Minha conta, Trocar senha, Tema e Sair). Indica que a pessoa está logada. */
export function botaoMenuUsuario(page: Page): Locator {
  return page.getByRole('button', { name: /^Menu do usuário/ });
}

/** Abre o menu do usuário e clica num item dele ("Minha conta", "Trocar senha", "Sair"). */
export async function noMenuDoUsuario(page: Page, item: 'Minha conta' | 'Trocar senha' | 'Sair'): Promise<void> {
  await botaoMenuUsuario(page).click();
  const menu = page.getByRole('dialog', { name: 'Menu do usuário' });
  await expect(menu).toBeVisible();
  await (
    item === 'Minha conta' ? menu.getByRole('link', { name: item }) : menu.getByRole('button', { name: item })
  ).click();
}

/**
 * Ações de uma linha de tabela ("⋯"): abre o menu "Ações de <rótulo>" e clica na ação. As tabelas têm um botão só por linha, preso à
 * direita (docs/design/guia-de-interface.md → "Tabelas").
 */
export async function acaoDaLinha(escopo: Page | Locator, rotulo: string, acao: string): Promise<void> {
  await escopo.getByRole('button', { name: `Ações de ${rotulo}`, exact: true }).click();
  const pagina = 'page' in escopo ? escopo.page() : escopo;
  await pagina.getByRole('menuitem', { name: acao, exact: true }).click();
}

/** Captura só da área visível: diálogos e menus abertos continuam na imagem (a página inteira os fecha). */
export async function capturarTela(page: Page, nome: string): Promise<void> {
  const pasta = join(__dirname, '..', 'capturas');
  mkdirSync(pasta, { recursive: true });
  await page.screenshot({ path: join(pasta, `${nome}.png`) });
}

/** Captura de tela pedida na validação (pasta e2e/capturas/, fora do git). */
export async function capturar(page: Page, nome: string): Promise<void> {
  const pasta = join(__dirname, '..', 'capturas');
  mkdirSync(pasta, { recursive: true });
  await page.screenshot({ path: join(pasta, `${nome}.png`), fullPage: true });
}

type Escopo = Page | Locator;

/**
 * Os campos são achados pelo PAPEL e pelo NOME ACESSÍVEL (o rótulo), como um leitor de tela os encontra. Campos
 * obrigatórios têm "(obrigatório)" no nome: use expressões como /^Projeto/.
 */
export function campoTexto(escopo: Escopo, nome: string | RegExp): Locator {
  return escopo.getByRole('textbox', { name: nome });
}

export function campoNumero(escopo: Escopo, nome: string | RegExp): Locator {
  return escopo.getByRole('spinbutton', { name: nome });
}

/** Um campo de qualquer tipo pelo nome acessível (texto, número, select, datepicker ou busca). */
export function campo(escopo: Escopo, nome: string | RegExp): Locator {
  return escopo
    .getByRole('textbox', { name: nome })
    .or(escopo.getByRole('spinbutton', { name: nome }))
    .or(escopo.getByRole('combobox', { name: nome }))
    .or(escopo.getByRole('searchbox', { name: nome }));
}

/** Selects (referência, lista) e datepickers têm o papel "combobox". */
export function campoSelecao(escopo: Escopo, nome: string | RegExp): Locator {
  return escopo.getByRole('combobox', { name: nome });
}

/** Escolhe uma opção num select de referência (p-autocomplete): digita, espera a lista do servidor e clica. */
export async function escolherReferencia(
  escopo: Escopo,
  nome: string | RegExp,
  busca: string,
  opcao: string | RegExp,
): Promise<void> {
  const campo = campoSelecao(escopo, nome);
  await campo.click();
  // Na edição o campo já mostra o rótulo atual: apaga antes de digitar a busca.
  await campo.clear();
  await campo.pressSequentially(busca, { delay: 30 });
  const pagina = 'page' in escopo ? escopo.page() : escopo;
  await pagina.getByRole('option', { name: opcao }).first().click();
}

/** Digita uma data no datepicker (dd/mm/aaaa ou dd/mm/aaaa hh:mm), tecla a tecla, e sai do campo. */
export async function digitarData(escopo: Escopo, nome: string | RegExp, texto: string): Promise<void> {
  const campo = campoSelecao(escopo, nome);
  await campo.click();
  await campo.pressSequentially(texto, { delay: 20 });
  await campo.press('Tab');
}

/**
 * Login PELA TELA (para usuários criados no teste, que não têm token guardado). O login da API aceita 10 tentativas por minuto
 * (RN23): se a tela avisar, espera o minuto passar e tenta de novo.
 */
export async function entrarPelaTela(page: Page, matricula: string, senha: string): Promise<void> {
  for (let tentativa = 1; tentativa <= 2; tentativa++) {
    await page.goto('/login');
    await page.getByLabel('Matrícula').fill(matricula);
    await page.locator('#senha').fill(senha);
    await page.getByRole('button', { name: 'Entrar' }).click();
    const limite = page.getByText('Muitas tentativas de login');
    const resultado = await Promise.race([
      limite.waitFor({ timeout: 15_000 }).then(() => 'limite' as const),
      page.waitForURL((url) => !url.pathname.startsWith('/login'), { timeout: 15_000 }).then(() => 'entrou' as const),
      page
        .getByText('Matrícula ou senha inválidas.')
        .waitFor({ timeout: 15_000 })
        .then(() => 'recusado' as const),
    ]).catch(() => 'tempo' as const);
    if (resultado !== 'limite') {
      return;
    }
    await page.waitForTimeout(61_000);
  }
}
