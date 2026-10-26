import { NOMES_PERFIL, Perfil } from '../../../core/auth/usuario';

/**
 * Os perfis (RN26) com a descrição curta mostrada no formulário de usuário e em "Minha conta", e a regra de equipes por perfil
 * (RN31): só o ADMINISTRADOR dispensa equipe, porque tem acesso a todas. Funções puras (perfis.spec.ts).
 */

export const DESCRICOES_PERFIL: Record<Perfil, string> = {
  EXTERNO: 'Só visualiza.',
  INTERNO: 'Cria e edita.',
  GESTOR: 'Cria, edita, exclui e acessa o painel.',
  ADMINISTRADOR: 'Faz tudo, em todas as equipes.',
};

export const PERFIS: { valor: Perfil; rotulo: string; descricao: string }[] = (
  ['EXTERNO', 'INTERNO', 'GESTOR', 'ADMINISTRADOR'] as Perfil[]
).map((perfil) => ({ valor: perfil, rotulo: NOMES_PERFIL[perfil], descricao: DESCRICOES_PERFIL[perfil] }));

export const EXPLICACAO_ADMINISTRADOR = 'Administradores têm acesso a todas as equipes.';

/** RN31: perfis diferentes de ADMINISTRADOR precisam de pelo menos uma equipe (ativa). */
export function exigeEquipes(perfil: Perfil | null): boolean {
  return perfil !== null && perfil !== 'ADMINISTRADOR';
}

/**
 * As equipes que vão para a API: as escolhidas, se o perfil exige equipes; nenhuma para o ADMINISTRADOR (o campo some da tela e
 * uma escolha antiga não deve ir junto).
 */
export function equipesParaSalvar(perfil: Perfil | null, escolhidas: string[]): string[] {
  return exigeEquipes(perfil) ? escolhidas : [];
}

/** O erro do campo equipes no formulário (nulo = ok). A API confere de novo, inclusive se as equipes estão ativas. */
export function erroEquipes(perfil: Perfil | null, escolhidas: string[]): string | null {
  return exigeEquipes(perfil) && escolhidas.length === 0 ? 'Escolha pelo menos uma equipe ativa.' : null;
}
