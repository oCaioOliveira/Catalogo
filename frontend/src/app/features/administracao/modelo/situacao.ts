import { UsuarioAdmin } from '../../../core/administracao/modelo-administracao';

/**
 * A situação de um usuário na listagem e no detalhe (situacao.spec.ts): Inativo; Bloqueado até hh:mm (RN24, 5 senhas erradas
 * bloqueiam por 15 minutos); ou Ativo. Um bloqueio já vencido não conta.
 */
export interface Situacao {
  rotulo: string;
  severidade: 'success' | 'warn' | 'secondary';
}

export function situacaoDoUsuario(usuario: Pick<UsuarioAdmin, 'ativo' | 'bloqueadoAte'>, agora: Date): Situacao {
  if (!usuario.ativo) {
    return { rotulo: 'Inativo', severidade: 'secondary' };
  }
  if (usuario.bloqueadoAte && new Date(usuario.bloqueadoAte) > agora) {
    return { rotulo: `Bloqueado até ${horaLocal(new Date(usuario.bloqueadoAte))}`, severidade: 'warn' };
  }
  return { rotulo: 'Ativo', severidade: 'success' };
}

/** "14:05", no fuso do navegador (a API devolve UTC). */
function horaLocal(data: Date): string {
  return `${String(data.getHours()).padStart(2, '0')}:${String(data.getMinutes()).padStart(2, '0')}`;
}
