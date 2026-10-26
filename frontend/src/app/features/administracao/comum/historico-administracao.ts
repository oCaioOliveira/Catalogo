import { MudancaDto } from '../../../api/models';
import { NOMES_PERFIL, Perfil } from '../../../core/auth/usuario';

/**
 * Como mostrar o histórico de usuários e equipes (RN32; historico-administracao.spec.ts). O histórico NUNCA tem senha: a troca e a
 * redefinição aparecem como o campo "senha" com "alterada" ou "redefinida", e é só isso que aparece na tela.
 */

const NOMES_CAMPO: Record<string, string> = {
  matricula: 'Matrícula',
  nome: 'Nome',
  perfil: 'Perfil',
  ativo: 'Situação',
  ativa: 'Situação',
  equipes: 'Equipes',
  senha: 'Senha',
};

export function nomeCampoAdministracao(chave: string): string {
  return NOMES_CAMPO[chave] ?? chave;
}

/** O valor de uma mudança: perfis pelo nome, situação por extenso, equipes (ids) pelo nome e a senha só como o evento. */
export function valorAdministracao(
  mudanca: MudancaDto,
  lado: 'de' | 'para',
  nomesDasEquipes: ReadonlyMap<string, string>,
): string {
  const bruto: unknown = mudanca[lado];
  if (bruto === null || bruto === undefined) {
    return '(vazio)';
  }
  switch (mudanca.campo) {
    case 'senha':
      // Só o evento ("alterada", "redefinida"): a API nunca manda valor de senha, e a tela não mostraria.
      return bruto === 'redefinida' ? 'redefinida' : bruto === 'alterada' ? 'alterada' : '(alterada)';
    case 'perfil':
      return NOMES_PERFIL[bruto as Perfil] ?? String(bruto);
    case 'ativo':
    case 'ativa':
      return bruto === true ? 'Ativo' : bruto === false ? 'Inativo' : String(bruto);
    case 'equipes': {
      const ids = Array.isArray(bruto) ? bruto.map(String) : [];
      return ids.length === 0 ? '(nenhuma)' : ids.map((id) => nomesDasEquipes.get(id) ?? id).join(', ');
    }
    default:
      return typeof bruto === 'object' ? JSON.stringify(bruto) : String(bruto);
  }
}
