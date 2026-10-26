import { expect, test } from '@playwright/test';
import { cadastrosAlfa, chamar, prefixoUnico, tokenDe } from './apoio/api';
import { limparE2E } from './apoio/limpeza';
import { botaoMenuUsuario, capturar } from './apoio/sessao';

// RN23 e RN25: login pela tela e troca de senha obrigatória no primeiro acesso. O usuário criado aqui (matrícula e2e-…) é
// desativado pelo helper compartilhado de limpeza, no início e no fim.
test.describe('Login', () => {
  test.beforeAll(async () => {
    await limparE2E(tokenDe('admin'));
  });

  test.afterAll(async () => {
    await limparE2E(tokenDe('admin'));
  });

  test('credenciais inválidas mostram a mensagem genérica', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveURL(/\/login/);
    await capturar(page, '01-login');
    await page.getByLabel('Matrícula').fill('nao.existe');
    await page.locator('#senha').fill('SenhaErrada123');
    await page.getByRole('button', { name: 'Entrar' }).click();
    await expect(page.getByText('Matrícula ou senha inválidas.')).toBeVisible();
  });

  test('primeiro acesso: troca a senha provisória e entra', async ({ page }) => {
    // Usuário novo, criado pelo administrador: recebe uma senha provisória (RN25).
    const admin = tokenDe('admin');
    const { equipe } = await cadastrosAlfa(admin);
    const matricula = prefixoUnico().toLowerCase();
    const criado = await chamar<{ senhaProvisoria: string }>(admin, 'POST', '/usuarios', {
      matricula,
      nome: 'Usuário do teste E2E',
      perfil: 'INTERNO',
      equipes: [equipe],
    });
    expect(criado.status).toBe(201);

    await page.goto('/login');
    await page.getByLabel('Matrícula').fill(matricula);
    await page.locator('#senha').fill(criado.corpo.senhaProvisoria);
    await page.getByRole('button', { name: 'Entrar' }).click();

    await expect(page).toHaveURL(/\/trocar-senha/);
    await expect(page.getByText('troque a senha provisória')).toBeVisible();
    const nova = `Nova${Date.now()}x`;
    await page.locator('#senha-atual').fill(criado.corpo.senhaProvisoria);
    await page.locator('#nova-senha').fill('curta1');
    await page.locator('#confirmacao').fill('curta1');
    await page.getByRole('button', { name: 'Trocar senha' }).click();
    await expect(page.getByText('A senha precisa ter pelo menos 10 caracteres.')).toBeVisible();

    await page.locator('#nova-senha').fill(nova);
    await page.locator('#confirmacao').fill(nova);
    await page.getByRole('button', { name: 'Trocar senha' }).click();

    // Entra no Início da área; a matrícula aparece no menu do usuário.
    await expect(page).toHaveURL(/\/testes_desempenho$/);
    await expect(page.getByRole('heading', { level: 1 })).toContainText(/^(Bom dia|Boa tarde|Boa noite)/);
    await botaoMenuUsuario(page).click();
    await expect(page.getByRole('dialog', { name: 'Menu do usuário' }).getByText(matricula)).toBeVisible();
  });
});
