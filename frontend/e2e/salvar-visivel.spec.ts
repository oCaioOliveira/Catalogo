import { Locator, Page, expect, test } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { GRUPO, chamar, prefixoUnico, tokenDe } from './apoio/api';
import { limparE2E } from './apoio/limpeza';
import { entrarComo } from './apoio/sessao';

/**
 * Cards da página Início com o "Novo" sempre no rodapé, o salvar sempre visível na aba Equipes de uma área de gestão (com 30
 * equipes de teste) e o painel com gráficos de uma e de várias séries. Capturas em e2e/capturas/marca/, nos dois temas.
 * A aba Equipes da área de desenvolvimento NUNCA é salva aqui: o teste só marca, busca, descarta e confere o aviso de sair.
 */
type Tema = 'claro' | 'escuro';
const TEMAS: Tema[] = ['claro', 'escuro'];
const pasta = join(__dirname, 'capturas', 'marca');
const QUANTAS_EQUIPES = 30;

test.use({ viewport: { width: 1366, height: 768 } });

let prefixo = '';
const nomeEquipe = (n: number) => `${prefixo} Squad ${String(n).padStart(2, '0')}`;

test.beforeAll(async () => {
  const admin = tokenDe('admin');
  await limparE2E(admin);
  prefixo = prefixoUnico();
  for (let n = 1; n <= QUANTAS_EQUIPES; n++) {
    const { status } = await chamar(admin, 'POST', '/equipes', { nome: nomeEquipe(n) });
    expect(status).toBeLessThan(400);
  }
});

test.afterAll(async () => {
  await limparE2E(tokenDe('admin'));
});

async function comTema(page: Page, tema: Tema): Promise<void> {
  await page.addInitScript((t) => localStorage.setItem('catalogo.tema', t), tema);
}

async function capturar(page: Page, nome: string, inteira = false): Promise<void> {
  mkdirSync(pasta, { recursive: true });
  await page.waitForTimeout(300);
  await page.screenshot({ path: join(pasta, `${nome}.png`), fullPage: inteira });
}

async function caixa(locator: Locator) {
  const box = await locator.boundingBox();
  expect(box).not.toBeNull();
  return box!;
}

for (const tema of TEMAS) {
  test(`Início: os botões "Novo" dos cards da mesma linha ficam na mesma altura, colados ao rodapé (tema ${tema})`, async ({
    page,
  }) => {
    await comTema(page, tema);
    await entrarComo(page, 'dev.gestor', `/${GRUPO}`);
    const cards = page.locator('.grade-atalhos > li.cartao-atalho');
    await expect(cards.first().getByRole('button', { name: /^Novo: / })).toBeVisible();
    await page.waitForLoadState('networkidle');

    // Agrupa os cards por linha (mesmo topo) e compara cada linha.
    const linhas = new Map<
      number,
      { card: { y: number; height: number }; botao: { y: number; height: number } | null }[]
    >();
    for (const card of await cards.all()) {
      const caixaCard = await caixa(card);
      const botao = card.getByRole('button', { name: /^Novo: / });
      const caixaBotao = (await botao.count()) > 0 ? await caixa(botao) : null;
      const topo = Math.round(caixaCard.y);
      linhas.set(topo, [...(linhas.get(topo) ?? []), { card: caixaCard, botao: caixaBotao }]);
    }
    for (const linha of linhas.values()) {
      // Mesma altura para todos os cards da linha.
      const alturas = linha.map((c) => Math.round(c.card.height));
      expect(Math.max(...alturas) - Math.min(...alturas), `alturas ${alturas}`).toBeLessThanOrEqual(1);
      const comBotao = linha.filter((c) => c.botao !== null);
      // Mesma posição vertical do "Novo" em todos, e colado ao rodapé (só o espaço interno do card abaixo dele).
      const tops = comBotao.map((c) => Math.round(c.botao!.y));
      expect(Math.max(...tops) - Math.min(...tops), `posições ${tops}`).toBeLessThanOrEqual(1);
      for (const c of comBotao) {
        const folga = c.card.y + c.card.height - (c.botao!.y + c.botao!.height);
        expect(folga).toBeGreaterThanOrEqual(12);
        expect(folga).toBeLessThanOrEqual(20);
      }
    }
    await capturar(page, `inicio-cards-${tema}`);
  });
}

test('aba Equipes: cabeçalho fixo com Salvar sempre visível, busca, ações rápidas, Descartar e o aviso ao sair', async ({
  page,
}) => {
  test.setTimeout(120_000);
  await entrarComo(page, 'admin', `/configuracoes/areas/${GRUPO}?aba=equipes`);
  const salvar = page.getByRole('button', { name: 'Salvar alterações' });
  const descartar = page.getByRole('button', { name: 'Descartar' });
  const pendentes = page.locator('.indicador-pendentes');
  await expect(page.getByRole('checkbox', { name: nomeEquipe(1), exact: true })).toBeVisible();

  // Sem alterações: os dois botões desabilitados.
  await expect(salvar).toBeDisabled();
  await expect(descartar).toBeDisabled();

  // Marcar uma equipe habilita os botões e mostra o indicador.
  await page.getByRole('checkbox', { name: nomeEquipe(1), exact: true }).check();
  await expect(salvar).toBeEnabled();
  await expect(pendentes).toHaveText(/1 alteração não salva/);

  // Rolar até o fim da lista: o Salvar continua visível (o cabeçalho da lista é fixo).
  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
  await expect(page.getByRole('checkbox', { name: nomeEquipe(QUANTAS_EQUIPES), exact: true })).toBeVisible();
  await expect(salvar).toBeInViewport();
  await expect(pendentes).toBeInViewport();

  // A busca filtra (sem diferenciar maiúsculas); "Selecionar todas as visíveis" e "Limpar seleção" respeitam a busca.
  const busca = page.getByRole('searchbox', { name: 'Buscar equipe pelo nome' });
  await busca.fill(nomeEquipe(7).toLowerCase());
  await expect(page.getByRole('checkbox', { name: /Squad/ })).toHaveCount(1);
  await busca.fill(prefixo);
  await expect(page.getByRole('checkbox', { name: /Squad/ })).toHaveCount(QUANTAS_EQUIPES);
  await page.getByRole('button', { name: 'Selecionar todas as visíveis' }).click();
  await expect(pendentes).toHaveText(new RegExp(`${QUANTAS_EQUIPES} alterações não salvas`));
  await page.getByRole('button', { name: 'Limpar seleção' }).click();
  await expect(salvar).toBeDisabled();

  // "Descartar" desfaz.
  await page.getByRole('checkbox', { name: nomeEquipe(2), exact: true }).check();
  await expect(salvar).toBeEnabled();
  await descartar.click();
  await expect(page.getByRole('checkbox', { name: nomeEquipe(2), exact: true })).not.toBeChecked();
  await expect(salvar).toBeDisabled();

  // "Mostrar só habilitadas" mostra só a escolha atual.
  await page.getByRole('checkbox', { name: nomeEquipe(3), exact: true }).check();
  await page.getByRole('checkbox', { name: 'Mostrar só habilitadas' }).check();
  await expect(page.getByRole('checkbox', { name: /Squad/ })).toHaveCount(1);
  await page.getByRole('checkbox', { name: 'Mostrar só habilitadas' }).uncheck();

  // Sair da página com alterações pendentes: o aviso; "Continuar editando" fica na tela, com a marcação.
  await page.getByRole('navigation', { name: 'Menu de configurações' }).getByRole('link', { name: 'Usuários' }).click();
  const aviso = page
    .getByRole('alertdialog', { name: 'Sair sem salvar?' })
    .or(page.getByRole('dialog', { name: 'Sair sem salvar?' }));
  await expect(aviso).toBeVisible();
  await aviso.getByRole('button', { name: 'Continuar editando' }).click();
  await expect(page).toHaveURL(/aba=equipes/);
  await expect(page.getByRole('checkbox', { name: nomeEquipe(3), exact: true })).toBeChecked();

  // Trocar de aba também avisa.
  await page.getByRole('tab', { name: 'Tipos de item' }).click();
  await expect(aviso).toBeVisible();
  await aviso.getByRole('button', { name: 'Continuar editando' }).click();
  await expect(page.getByRole('tab', { name: 'Equipes' })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByRole('checkbox', { name: nomeEquipe(3), exact: true })).toBeChecked();

  // Sair sem salvar descarta (nada é gravado na área de desenvolvimento).
  await page.getByRole('navigation', { name: 'Menu de configurações' }).getByRole('link', { name: 'Usuários' }).click();
  await aviso.getByRole('button', { name: 'Sair sem salvar' }).click();
  await expect(page).toHaveURL(/\/configuracoes\/usuarios/);
});

for (const tema of TEMAS) {
  test(`capturas: aba Equipes rolada até o meio, com alterações pendentes, e o painel (tema ${tema})`, async ({
    page,
  }) => {
    await comTema(page, tema);
    await entrarComo(page, 'admin', `/configuracoes/areas/${GRUPO}?aba=equipes`);
    await page.getByRole('checkbox', { name: nomeEquipe(1), exact: true }).check();
    await page.getByRole('checkbox', { name: nomeEquipe(4), exact: true }).check();
    await page.getByRole('checkbox', { name: nomeEquipe(5), exact: true }).check();
    await expect(page.locator('.indicador-pendentes')).toHaveText(/3 alterações não salvas/);
    await page.evaluate(() => window.scrollTo(0, (document.documentElement.scrollHeight - window.innerHeight) / 2));
    await expect(page.getByRole('button', { name: 'Salvar alterações' })).toBeInViewport();
    await capturar(page, `equipes-salvar-fixo-${tema}`);
    await page.getByRole('button', { name: 'Descartar' }).click();

    // Painel: "Itens criados por mês" (uma série, Azul CAIXA) e "Relatórios executados por mês e status" (várias séries).
    // Como o gestor de desenvolvimento: o administrador vê todas as equipes (inclusive as de testes antigos) e a página fica enorme.
    await entrarComo(page, 'dev.gestor', `/painel/${GRUPO}`);
    await expect(page.locator('.cartao-kpi').first()).toBeVisible();
    await page.waitForLoadState('networkidle');
    await capturar(page, `painel-series-${tema}`, true);
  });
}
