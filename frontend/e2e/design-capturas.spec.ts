import { Page, expect, test } from '@playwright/test';
import { existsSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { idPor, tokenDe } from './apoio/api';
import { entrarComo } from './apoio/sessao';

/**
 * Capturas "antes e depois" da etapa de design e usabilidade (e2e/capturas/design/). A fase vem de CAPTURA_FASE ("antes" ou
 * "depois", o padrão). As de "antes" não são sobrescritas: se o arquivo já existe, a captura é pulada. As de "depois" saem nos
 * dois temas (sufixos -claro e -escuro). Largura de 1366 px, a mais comum nas estações de trabalho.
 *   CAPTURA_FASE=antes npx playwright test e2e/design-capturas.spec.ts
 */
const fase = process.env['CAPTURA_FASE'] === 'antes' ? 'antes' : 'depois';
const pasta = join(__dirname, 'capturas', 'design');
const GRUPO = 'testes_desempenho';

test.use({ viewport: { width: 1366, height: 768 } });

type Tema = 'claro' | 'escuro';
const temas: Tema[] = fase === 'antes' ? ['claro'] : ['claro', 'escuro'];

function arquivo(nome: string, tema: Tema): string {
  return join(pasta, fase === 'antes' ? `antes-${nome}.png` : `depois-${nome}-${tema}.png`);
}

/** Captura a área visível (diálogos abertos continuam na imagem). Na fase "antes", nunca sobrescreve. */
async function capturarDesign(page: Page, nome: string, tema: Tema): Promise<void> {
  mkdirSync(pasta, { recursive: true });
  const destino = arquivo(nome, tema);
  if (fase === 'antes' && existsSync(destino)) {
    return;
  }
  await page.waitForTimeout(300);
  await page.screenshot({ path: destino });
}

/** O tema da interface: a escolha fica no localStorage (catalogo.tema), lida antes da primeira tela. */
async function usarTema(page: Page, tema: Tema): Promise<void> {
  if (fase === 'depois') {
    await page.addInitScript((t) => localStorage.setItem('catalogo.tema', t), tema);
  }
}

for (const tema of temas) {
  test(`capturas de design (${fase}, ${tema})`, async ({ page, browser }) => {
    test.setTimeout(180_000);
    const projeto = await idPor(tokenDe('admin'), 'projeto', 'nome', 'Transferências Alfa');

    // Login (sem sessão).
    const anonimo = await browser.newPage({ viewport: { width: 1366, height: 768 } });
    await usarTema(anonimo, tema);
    await anonimo.goto('/login');
    await expect(anonimo.getByRole('button', { name: 'Entrar' })).toBeVisible();
    await capturarDesign(anonimo, '01-login', tema);
    await anonimo.close();

    await usarTema(page, tema);
    // Início da área (antes: a tela que abria ao entrar, a lista de relatórios).
    await entrarComo(page, 'dev.gestor', `/${GRUPO}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.waitForLoadState('networkidle');
    await capturarDesign(page, '02-inicio', tema);

    // Listagem de relatórios com muitas colunas.
    await page.goto(`/${GRUPO}/relatorio_teste`);
    await expect(page.locator('tbody tr').first()).toBeVisible();
    await page.waitForLoadState('networkidle');
    await capturarDesign(page, '03-listagem-relatorios', tema);

    // Detalhe de projeto, com os relacionados.
    await page.goto(`/${GRUPO}/projeto/${projeto}`);
    await expect(page.getByRole('tab', { name: 'Dados' })).toBeVisible();
    await page.waitForLoadState('networkidle');
    await capturarDesign(page, '04-detalhe-projeto', tema);

    // Formulário de relatório com o select de RTC aberto (depois: o diálogo "Criar novo RTC").
    await page.goto(`/${GRUPO}/relatorio_teste/novo`);
    const rtc = page.getByRole('combobox', { name: /^RTC/ });
    await expect(rtc).toBeVisible();
    await rtc.click();
    await rtc.pressSequentially('RTC-NOVO-1', { delay: 30 });
    if (fase === 'depois') {
      await page.getByRole('option', { name: "+ Criar RTC 'RTC-NOVO-1'" }).click();
      await expect(page.getByRole('dialog')).toBeVisible();
    } else {
      await page.waitForTimeout(1000);
    }
    await capturarDesign(page, '05-formulario-relatorio-criar-rtc', tema);
    if (fase === 'depois') {
      await page.getByRole('dialog').getByRole('button', { name: 'Cancelar' }).click();
    }

    // Painel gerencial.
    await page.goto(`/painel/${GRUPO}`);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(800);
    await capturarDesign(page, '06-painel', tema);

    // Configurações da equipe.
    await page.goto(fase === 'antes' ? '/configuracoes-equipe' : '/configuracoes/equipe');
    await expect(page.getByRole('switch').first()).toBeVisible();
    await capturarDesign(page, '07-configuracoes-equipe', tema);

    // Usuários (administrador).
    const admin = await browser.newPage({ viewport: { width: 1366, height: 768 } });
    await usarTema(admin, tema);
    await entrarComo(admin, 'admin', fase === 'antes' ? '/admin/usuarios' : '/configuracoes/usuarios');
    await expect(admin.locator('tbody tr').first()).toBeVisible();
    await capturarDesign(admin, '08-usuarios', tema);
    await admin.close();
  });
}
