import { Perfil } from '../../../core/auth/usuario';
import { UsuarioAdmin } from '../../../core/administracao/modelo-administracao';

/** Regras das ações da administração que dão para testar sem Angular (regras-administracao.spec.ts). */

/**
 * O aviso antes de salvar a edição do PRÓPRIO usuário: o administrador que troca o próprio perfil perde o acesso à
 * administração (só o ADMINISTRADOR administra, RN26). Nulo = sem aviso.
 */
export function avisoEdicaoPropria(
  usuarioLogadoId: string | null,
  alvo: Pick<UsuarioAdmin, 'id' | 'perfil'>,
  novoPerfil: Perfil,
): string | null {
  if (usuarioLogadoId !== alvo.id || alvo.perfil !== 'ADMINISTRADOR' || novoPerfil === 'ADMINISTRADOR') {
    return null;
  }
  return (
    'Você está mudando o SEU próprio perfil de Administrador para outro. Ao salvar, você perde o acesso à administração ' +
    'de usuários e equipes (só outro administrador poderá devolvê-lo).'
  );
}

/**
 * Os membros que ficariam SEM NENHUMA equipe ativa se esta fosse desativada: usuários ativos, não administradores, cuja única
 * equipe ativa é esta. A API recusa a desativação enquanto houver algum (RN31, 409 USUARIOS_SEM_EQUIPE); a tela mostra a
 * quantidade antes de confirmar.
 */
export function membrosSemOutraEquipeAtiva(membros: UsuarioAdmin[], equipeId: string): UsuarioAdmin[] {
  return membros.filter(
    (u) =>
      u.ativo &&
      u.perfil !== 'ADMINISTRADOR' &&
      u.equipes.some((e) => e.id === equipeId) &&
      !u.equipes.some((e) => e.id !== equipeId && e.ativa),
  );
}

/** Membros ativos que perdem o acesso aos itens DESTA equipe enquanto ela estiver inativa (continuam nas outras). */
export function membrosAtivos(membros: UsuarioAdmin[]): UsuarioAdmin[] {
  return membros.filter((u) => u.ativo);
}
