import { chamar, tokenDe } from './api';
import { MATRICULA_E2E, NOME_EQUIPE_E2E, limparE2E } from './limpeza';

/**
 * Depois de TODAS as suítes: nenhuma equipe ou usuário de teste pode ficar ativo (toda suíte que os cria usa o helper de limpeza no
 * início e no fim, e2e/apoio/limpeza.ts). Se sobrou algum, desmonta (para não atrapalhar a próxima execução) e FALHA a execução,
 * dizendo quantos eram: alguma suíte esqueceu de limpar.
 */
export default async function verificar(): Promise<void> {
  const admin = tokenDe('admin');
  const equipes = await chamar<{ itens: { nome: string }[] }>(
    admin,
    'GET',
    '/equipes?ativa=true&busca=E2E-&tamanho=100',
  );
  const usuarios = await chamar<{ itens: { matricula: string }[] }>(
    admin,
    'GET',
    '/usuarios?ativo=true&busca=e2e-&tamanho=100',
  );
  const sobrasEquipes = (equipes.corpo?.itens ?? []).filter((e) => NOME_EQUIPE_E2E.test(e.nome)).length;
  const sobrasUsuarios = (usuarios.corpo?.itens ?? []).filter((u) => MATRICULA_E2E.test(u.matricula)).length;
  if (sobrasEquipes + sobrasUsuarios > 0) {
    await limparE2E(admin);
    throw new Error(
      `Sobraram ${sobrasEquipes} equipe(s) e ${sobrasUsuarios} usuário(s) de E2E ativos depois das suítes: ` +
        'toda suíte que cria equipes ou usuários precisa chamar limparE2E no início e no fim.',
    );
  }
  console.log('[verificação final] nenhuma equipe ou usuário de E2E ativo.');
}
