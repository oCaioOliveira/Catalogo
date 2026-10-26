import { expect, test } from '@playwright/test';
import { API, cadastrosAlfa, chamar, criar, criarRelatorio, idPor, prefixoUnico, tokenDe } from './apoio/api';
import { campo, capturar, digitarData, entrarComo, escolherReferencia } from './apoio/sessao';

test.describe('Cadastro de relatórios', () => {
  test('cria um relatório completo e adiciona cenários e links no detalhe', async ({ page }) => {
    const prefixo = prefixoUnico();
    await entrarComo(page, 'dev.gestor', '/testes_desempenho/relatorio_teste');
    await page.getByRole('button', { name: 'Novo' }).click();
    await expect(page.getByRole('heading', { name: 'Novo: Relatório de Teste' })).toBeVisible();

    // Validação no front, a partir dos metadados: os obrigatórios vazios aparecem embaixo de cada campo.
    await page.getByRole('button', { name: /^Salvar\b/ }).click();
    await expect(page.getByText('Corrija os campos destacados.')).toBeVisible();
    await expect(page.locator('#formulario-bateria_testes-erros')).toHaveText('Campo obrigatório.');
    await capturar(page, '03-novo-relatorio-com-erros');

    await escolherReferencia(page, /^Projeto/, 'Transfer', 'Transferências Alfa');
    await escolherReferencia(page, /^RTC/, 'RTC-ALFA-101', 'RTC-ALFA-101');
    await escolherReferencia(page, /^Ambiente/, 'Homolog', 'Homologação');
    await escolherReferencia(page, /^Status/, 'Aprovado', /^Aprovado$/);
    await digitarData(page, /^Data de execução/, '15/09/2026 10:30');
    await campo(page, /^Bateria de testes/).fill(`${prefixo} completo`);
    await campo(page, /^Tipo de teste/).fill('Carga');
    await campo(page, /^Executado por/).fill('Equipe E2E');
    await campo(page, /^Objetivo/).fill('Validar a vazão da transferência.');
    await page.getByRole('button', { name: /^Salvar\b/ }).click();

    await expect(page.getByRole('heading', { name: `${prefixo} completo` })).toBeVisible();
    await expect(page.getByText('Item criado.')).toBeVisible();
    await expect(page.getByText('15/09/2026 10:30', { exact: true })).toBeVisible();

    // Cenário: o select de endpoint só traz os endpoints do PROJETO do relatório (dependência RN08/RN21).
    await page.getByRole('button', { name: 'Adicionar cenário de teste' }).click();
    const dialogo = page.getByRole('dialog');
    await expect(campo(dialogo, /^Relatório/)).toBeDisabled();
    await campo(dialogo, /^Endpoint/).click();
    await campo(dialogo, /^Endpoint/).pressSequentially('-');
    // Os endpoints do projeto e, no fim, "+ Criar <tipo>", que não é um endpoint.
    const opcoes = page.getByRole('option').filter({ hasNotText: '+ Criar' });
    await expect(opcoes).toHaveCount(3);
    for (const texto of await opcoes.allTextContents()) {
      expect(texto).toContain('transferências-alfa');
    }
    await opcoes.filter({ hasText: 'enviar' }).click();
    await campo(dialogo, /^Nº do cenário/).pressSequentially('1');
    await campo(dialogo, /^Usuários virtuais/).pressSequentially('250');
    await campo(dialogo, /^Tempo médio/).pressSequentially('180');
    await campo(dialogo, /^Percentil 95/).pressSequentially('420');
    await campo(dialogo, /^Vazão média/).pressSequentially('123,45');
    await campo(dialogo, /^Taxa de erro/).pressSequentially('0,5');
    await dialogo.getByRole('button', { name: /^Salvar\b/ }).click();
    await expect(dialogo).toBeHidden();
    const cenarios = page
      .locator('app-secao-filhos')
      .filter({ has: page.getByRole('heading', { name: 'Cenários de Teste' }) });
    await expect(cenarios.locator('tbody tr')).toHaveCount(1);
    await expect(cenarios.locator('tbody tr').first()).toContainText('123,45');

    await page.getByRole('button', { name: 'Adicionar link' }).click();
    await campo(page.getByRole('dialog'), /^Texto de exibição/).fill('Painel do teste');
    await campo(page.getByRole('dialog'), /^URL/).fill('https://grafana.interno/d/e2e');
    await page
      .getByRole('dialog')
      .getByRole('button', { name: /^Salvar\b/ })
      .click();
    await expect(page.getByRole('dialog')).toBeHidden();
    await expect(page.getByText('Painel do teste')).toBeVisible();
    await capturar(page, '04-detalhe-relatorio');

    await page.getByRole('tab', { name: 'Histórico' }).click();
    await expect(page.getByText('Criação').first()).toBeVisible();
    await capturar(page, '05-detalhe-historico');
  });

  test('erro de validação que só o back-end conhece aparece no campo certo', async ({ page }) => {
    const gestor = tokenDe('dev.gestor');
    const prefixo = prefixoUnico();
    const relatorio = await criarRelatorio(gestor, `${prefixo} validação`);
    const { equipe } = await cadastrosAlfa(tokenDe('admin'));
    const rtcTemporario = await criar(gestor, 'rtc', { equipeId: equipe, rtc_externo_id: prefixo.slice(0, 20) });

    await entrarComo(page, 'dev.gestor', `/testes_desempenho/relatorio_teste/${relatorio}/editar`);
    await escolherReferencia(page, /^RTC/, prefixo.slice(0, 8), prefixo.slice(0, 20));
    // O RTC escolhido deixa de existir antes de salvar: só o servidor sabe (RN06).
    expect(
      (await chamar(gestor, 'DELETE', `/grupos/testes_desempenho/entidades/rtc/registros/${rtcTemporario}`)).status,
    ).toBe(204);
    await page.getByRole('button', { name: 'Salvar alterações' }).click();

    await expect(page.locator('#formulario-rtc-erros')).toBeVisible();
    await expect(campo(page, /^RTC/)).toHaveAttribute('aria-invalid', 'true');
    await expect(page.locator('#formulario-bateria_testes-erros')).toHaveCount(0);
  });

  test('conflito de concorrência: duas abas editando o mesmo item', async ({ page, context }) => {
    const gestor = tokenDe('dev.gestor');
    const relatorio = await criarRelatorio(gestor, `${prefixoUnico()} concorrência`);
    const url = `/testes_desempenho/relatorio_teste/${relatorio}/editar`;
    const outraAba = await context.newPage();
    await entrarComo(page, 'dev.gestor', url);
    await entrarComo(outraAba, 'dev.gestor', url);
    await expect(campo(page, /^Tipo de teste/)).toHaveValue('Carga');
    await expect(campo(outraAba, /^Tipo de teste/)).toHaveValue('Carga');

    await campo(page, /^Tipo de teste/).fill('Estresse');
    await page.getByRole('button', { name: 'Salvar alterações' }).click();
    await expect(page.getByText('Alterações salvas.')).toBeVisible();

    await campo(outraAba, /^Executado por/).fill('Outra pessoa');
    await outraAba.getByRole('button', { name: 'Salvar alterações' }).click();
    const aviso = outraAba.getByRole('dialog', { name: 'Outra pessoa alterou este item' });
    await expect(aviso).toBeVisible();
    await aviso.getByRole('button', { name: /Recarregar/ }).click();
    await expect(aviso).toBeHidden();
    await expect(campo(outraAba, /^Tipo de teste/)).toHaveValue('Estresse');
  });

  test('excluir um relatório com cenários avisa que os filhos vão junto', async ({ page }) => {
    const gestor = tokenDe('dev.gestor');
    const c = await cadastrosAlfa(tokenDe('admin'));
    const relatorio = await criarRelatorio(gestor, `${prefixoUnico()} exclusão`);
    const endpoint = await idPor(gestor, 'endpoint_servico', 'nome_servico', 'enviar-transferências-alfa');
    for (const numero of [1, 2]) {
      await criar(gestor, 'cenario_teste', {
        equipeId: c.equipe,
        relatorio_teste: relatorio,
        endpoint_servico: endpoint,
        numero_cenario: numero,
        usuarios_virtuais: 10,
        tempo_medio_resposta_ms: 100,
        percentil_95_ms: 200,
        vazao_media_tps: 1,
        taxa_erro_percentual: 0,
      });
    }

    await entrarComo(page, 'dev.gestor', `/testes_desempenho/relatorio_teste/${relatorio}`);
    await page.locator('.cabecalho-pagina').getByRole('button', { name: 'Excluir' }).click();
    const confirmacao = page.getByRole('alertdialog', { name: 'Excluir item' });
    await expect(confirmacao).toContainText('também serão excluídos');
    await expect(confirmacao).toContainText('Cenário de Teste');
    await confirmacao.getByRole('button', { name: 'Excluir' }).click();

    await expect(page).toHaveURL(/\/testes_desempenho\/relatorio_teste$/);
    expect(
      (await chamar(gestor, 'GET', `/grupos/testes_desempenho/entidades/relatorio_teste/registros/${relatorio}`))
        .status,
    ).toBe(404);
    const restantes = await fetch(
      `${API}/api/v1/grupos/testes_desempenho/entidades/cenario_teste/registros?relatorio_teste=${relatorio}`,
      {
        headers: { Authorization: `Bearer ${gestor}` },
      },
    );
    expect(((await restantes.json()) as { total: number }).total).toBe(0);
  });
});
