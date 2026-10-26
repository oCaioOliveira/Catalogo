import AxeBuilder from '@axe-core/playwright';
import { Locator, Page, expect, test } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { GRUPO } from './apoio/api';
import { entrarComo } from './apoio/sessao';

/**
 * Identidade CAIXA (logo e paleta) e o atalho de tema no cabeçalho e no login. Capturas em e2e/capturas/marca/ (fora do git),
 * nos dois temas. O contraste de texto é conferido com o axe (regra color-contrast, WCAG 2 AA) nas telas principais.
 */
type Tema = 'claro' | 'escuro';
const TEMAS: Tema[] = ['claro', 'escuro'];
const pasta = join(__dirname, 'capturas', 'marca');

test.use({ viewport: { width: 1366, height: 768 } });

/** Escolhe o tema como a pessoa faria antes de abrir a página: o valor salvo no localStorage (lido antes da primeira tela). */
async function comTema(page: Page, tema: Tema | 'sistema'): Promise<void> {
  await page.addInitScript((t) => localStorage.setItem('catalogo.tema', t), tema);
}

async function capturarMarca(page: Page, nome: string, areaSoCabecalho = false): Promise<void> {
  mkdirSync(pasta, { recursive: true });
  await page.waitForTimeout(300);
  const caminho = join(pasta, `${nome}.png`);
  if (areaSoCabecalho) {
    await page.locator('.cabecalho-app').screenshot({ path: caminho });
  } else {
    await page.screenshot({ path: caminho });
  }
}

/** Só a regra de contraste (o resto das regras do axe fica para outra etapa); falha com a lista do que não passou. */
async function conferirContraste(page: Page, tela: string): Promise<void> {
  const resultado = await new AxeBuilder({ page }).withRules(['color-contrast']).analyze();
  const falhas = resultado.violations.flatMap((v) =>
    v.nodes.map((n) => `${tela}: ${n.target.join(' ')} — ${n.failureSummary?.split('\n').slice(1).join(' ').trim()}`),
  );
  expect(falhas, falhas.join('\n')).toEqual([]);
}

/** O arquivo da logo do tema: a positiva no claro, a negativa no escuro. */
const ARQUIVO_LOGO: Record<Tema, RegExp> = {
  claro: /assets\/marca\/caixa-logo\.svg$/,
  escuro: /assets\/marca\/caixa-logo-negativa\.svg$/,
};

/** A logo fica direto sobre o fundo: nem a imagem nem o que está em volta dela até o cabeçalho (ou a página) tem fundo próprio. */
async function semFundoAtras(logo: Locator, ate: string): Promise<void> {
  const fundos = await logo.evaluate((img, seletor) => {
    const lista: string[] = [];
    for (let el: Element | null = img; el && !el.matches(seletor); el = el.parentElement) {
      lista.push(getComputedStyle(el).backgroundColor);
    }
    return lista;
  }, ate);
  expect(
    fundos.every((cor) => cor === 'rgba(0, 0, 0, 0)' || cor === 'transparent'),
    fundos.join(', '),
  ).toBe(true);
}

function logoDoCabecalho(page: Page) {
  return page.getByRole('banner').getByRole('img', { name: 'CAIXA' });
}

for (const tema of TEMAS) {
  test(`a logo aparece no cabeçalho e no login, com alt="CAIXA" (tema ${tema})`, async ({ page, browser }) => {
    const contexto = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const anonimo = await contexto.newPage();
    await comTema(anonimo, tema);
    await anonimo.goto('/login');
    const logoLogin = anonimo.getByRole('img', { name: 'CAIXA' });
    await expect(logoLogin).toBeVisible();
    await expect(logoLogin).toHaveAttribute('alt', 'CAIXA');
    await expect(logoLogin).toHaveJSProperty('complete', true);
    expect(await logoLogin.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0);
    await expect(logoLogin).toHaveAttribute('src', ARQUIVO_LOGO[tema]);
    await semFundoAtras(logoLogin, '.pagina-acesso');
    await expect(anonimo.locator('html')).toHaveClass(tema === 'escuro' ? /catalogo-escuro/ : /^(?!.*catalogo-escuro)/);
    await capturarMarca(anonimo, `login-${tema}`);
    await contexto.close();

    await comTema(page, tema);
    await entrarComo(page, 'dev.gestor', `/${GRUPO}`);
    const logo = logoDoCabecalho(page);
    await expect(logo).toBeVisible();
    await expect(logo).toHaveAttribute('alt', 'CAIXA');
    await expect(logo).toHaveAttribute('src', ARQUIVO_LOGO[tema]);
    await expect(logo).toHaveAttribute('width', /\d+/);
    await expect(logo).toHaveAttribute('height', '24');
    await semFundoAtras(logo, '.cabecalho-app');
    await expect(page.locator('.selo-marca')).toHaveCount(0);
    // Proporção do arquivo preservada (sem distorção) e altura de cabeçalho (24 px).
    const caixa = await logo.boundingBox();
    expect(caixa!.height).toBeGreaterThanOrEqual(22);
    expect(caixa!.height).toBeLessThanOrEqual(32);
    expect(caixa!.width / caixa!.height).toBeCloseTo(2000 / 453, 1);
    await expect(page.getByRole('link', { name: 'Início — Catálogo' })).toBeVisible();
    await capturarMarca(page, `cabecalho-${tema}`, true);
  });
}

test('clicar na logo leva ao Início da área atual', async ({ page }) => {
  await entrarComo(page, 'dev.gestor', `/${GRUPO}/relatorio_teste`);
  await expect(page.locator('tbody tr').first()).toBeVisible();
  const link = page.getByRole('link', { name: 'Início — Catálogo' });
  await link.focus();
  // Foco visível no link da logo.
  expect(await link.evaluate((a) => getComputedStyle(a).outlineStyle)).not.toBe('none');
  await link.click();
  await expect(page).toHaveURL(new RegExp(`/${GRUPO}$`));
  await expect(
    page.getByRole('navigation', { name: 'Menu da área de gestão' }).getByRole('link', { name: 'Início' }),
  ).toHaveAttribute('aria-current', 'page');
});

test('em 1024 px e com o menu recolhido, a logo continua visível', async ({ page }) => {
  await page.setViewportSize({ width: 1024, height: 768 });
  await entrarComo(page, 'dev.gestor', `/${GRUPO}`);
  await expect(logoDoCabecalho(page)).toBeVisible();

  await page.getByRole('button', { name: 'Recolher menu' }).click();
  await expect(page.getByRole('button', { name: 'Expandir menu' })).toBeVisible();
  await expect(logoDoCabecalho(page)).toBeVisible();
  await capturarMarca(page, 'cabecalho-menu-recolhido');
  await page.getByRole('button', { name: 'Expandir menu' }).click();

  // Abaixo de 1024 px o menu vira gaveta: a logo fica, o nome "Catálogo" sai.
  await page.setViewportSize({ width: 900, height: 768 });
  await expect(logoDoCabecalho(page)).toBeVisible();
  await expect(page.getByRole('banner').getByText('Catálogo', { exact: true })).toBeHidden();
});

test('o botão de tema do cabeçalho alterna claro → escuro → sistema, e a escolha fica ao recarregar', async ({
  page,
}) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await comTema(page, 'claro');
  await entrarComo(page, 'dev.gestor', `/${GRUPO}`);
  const html = page.locator('html');
  const botao = () => page.getByRole('banner').getByRole('button', { name: /^Tema:/ });

  await expect(botao()).toHaveAccessibleName('Tema: claro. Mudar para escuro');
  await expect(logoDoCabecalho(page)).toHaveAttribute('src', ARQUIVO_LOGO.claro);
  await botao().click();
  await expect(html).toHaveClass(/catalogo-escuro/);
  // A logo troca na hora, sem recarregar.
  await expect(logoDoCabecalho(page)).toHaveAttribute('src', ARQUIVO_LOGO.escuro);
  await expect(botao()).toHaveAccessibleName('Tema: escuro. Mudar para seguir o sistema');
  await botao().click();
  // "Seguir o sistema" com o sistema claro.
  await expect(html).not.toHaveClass(/catalogo-escuro/);
  await expect(botao()).toHaveAccessibleName('Tema: seguir o sistema. Mudar para claro');
  await botao().click();
  await botao().click();
  await expect(botao()).toHaveAccessibleName('Tema: escuro. Mudar para seguir o sistema');

  // Abrir de novo sem o script de teste (aba nova, sem sessão: cai no login): a escolha salva (escuro) vale antes da primeira tela.
  expect(await page.evaluate(() => localStorage.getItem('catalogo.tema'))).toBe('escuro');
  const nova = await page.context().newPage();
  await nova.goto(`/${GRUPO}`);
  await expect(nova.locator('html')).toHaveClass(/catalogo-escuro/);
  await expect(nova.getByRole('button', { name: /^Tema:/ })).toHaveAccessibleName(
    'Tema: escuro. Mudar para seguir o sistema',
  );
  await nova.close();
});

test('o botão de tema do login alterna os modos, e a escolha fica ao recarregar', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await page.goto('/login');
  const botao = page.getByRole('button', { name: /^Tema:/ });
  await expect(botao).toHaveAccessibleName('Tema: seguir o sistema. Mudar para claro');
  await botao.click();
  await expect(botao).toHaveAccessibleName('Tema: claro. Mudar para escuro');
  await botao.click();
  await expect(page.locator('html')).toHaveClass(/catalogo-escuro/);
  await page.reload();
  await expect(page.locator('html')).toHaveClass(/catalogo-escuro/);
  await expect(page.getByRole('button', { name: /^Tema:/ })).toHaveAccessibleName(
    'Tema: escuro. Mudar para seguir o sistema',
  );
});

for (const tema of TEMAS) {
  test(`contraste de texto (WCAG AA) nas telas principais e capturas (tema ${tema})`, async ({ page, browser }) => {
    test.setTimeout(120_000);
    const contexto = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const anonimo = await contexto.newPage();
    await comTema(anonimo, tema);
    await anonimo.goto('/login');
    await expect(anonimo.getByRole('button', { name: 'Entrar' })).toBeVisible();
    await conferirContraste(anonimo, 'login');
    await contexto.close();

    await comTema(page, tema);
    await entrarComo(page, 'dev.gestor', `/${GRUPO}`);
    await page.waitForLoadState('networkidle');
    await capturarMarca(page, `inicio-${tema}`);
    await conferirContraste(page, 'Início');

    await page.goto(`/${GRUPO}/relatorio_teste`);
    await expect(page.locator('tbody tr').first()).toBeVisible();
    await page.waitForLoadState('networkidle');
    await capturarMarca(page, `listagem-${tema}`);
    await conferirContraste(page, 'listagem');

    await page.goto(`/painel/${GRUPO}`);
    await expect(page.locator('.cartao-kpi').first()).toBeVisible();
    await page.waitForLoadState('networkidle');
    await capturarMarca(page, `painel-${tema}`);
    await conferirContraste(page, 'painel');
  });
}

test('"seguir o sistema": quando o sistema operacional muda de tema, a logo troca na hora', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light' });
  await comTema(page, 'sistema');
  await entrarComo(page, 'dev.gestor', `/${GRUPO}`);
  await expect(logoDoCabecalho(page)).toHaveAttribute('src', ARQUIVO_LOGO.claro);
  await page.emulateMedia({ colorScheme: 'dark' });
  await expect(page.locator('html')).toHaveClass(/catalogo-escuro/);
  await expect(logoDoCabecalho(page)).toHaveAttribute('src', ARQUIVO_LOGO.escuro);
  await page.emulateMedia({ colorScheme: 'light' });
  await expect(logoDoCabecalho(page)).toHaveAttribute('src', ARQUIVO_LOGO.claro);
});
