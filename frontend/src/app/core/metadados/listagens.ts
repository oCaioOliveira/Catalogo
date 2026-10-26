import { Problema } from '../erros/problema';

// Regras da navegação entre várias listagens (grupos) e da validade dos metadados em memória. Funções puras: listagens.spec.ts.
// ADR 0043.

/** Uma equipe, como os selects e a coluna de equipe a mostram. */
export interface EquipeDaListagem {
  id: string;
  nome: string;
}

/** Um grupo (listagem) como o menu o usa (GET /api/v1/grupos). */
export interface Listagem {
  chave: string;
  nome: string;
  /** A descrição da área de gestão (opcional): o que ela guarda, na página Início. */
  descricao?: string | null;
  ativo: boolean;
  /**
   * "As equipes do usuário nesta listagem" (RN91): as equipes ativas dele habilitadas nela; para o ADMINISTRADOR, todas as
   * habilitadas e ativas. É o que o select de equipe, a coluna e o filtro de equipe, o painel e as configurações oferecem.
   */
  minhasEquipes: EquipeDaListagem[];
  /** Há alguma equipe habilitada (RN93: sem nenhuma, só o ADMINISTRADOR vê a listagem). */
  possuiEquipes: boolean;
}

/**
 * Por quanto tempo os metadados em memória valem. Depois disso, a próxima tela que pedir recarrega da API. Alterações feitas
 * pela administração descartam o cache na hora (MetadadosService.invalidar); este prazo cobre as feitas em outra aba ou por
 * outra pessoa.
 */
export const VALIDADE_METADADOS_MS = 5 * 60 * 1000;

export function cacheValido(carregadoEm: number, agora: number, validade = VALIDADE_METADADOS_MS): boolean {
  return agora - carregadoEm < validade;
}

/**
 * A listagem que abre ao entrar: a última usada (localStorage), se ainda estiver ativa e visível; senão, a primeira ativa, na ordem
 * da API (por nome). A API só devolve as listagens que o usuário vê (RN90): uma que deixou de ser habilitada para as equipes dele
 * some da lista e não abre mais. Nenhuma ativa: null.
 */
export function escolherListagem(listagens: Listagem[], ultima: string | null): string | null {
  const ativas = listagens.filter((l) => l.ativo);
  return ativas.find((l) => l.chave === ultima)?.chave ?? ativas[0]?.chave ?? null;
}

/**
 * RN91: a coluna e o filtro de Equipe aparecem nas entidades POR EQUIPE quando o usuário tem MAIS DE UMA equipe na listagem, ou é
 * ADMINISTRADOR (que vê os itens de todas). Com uma equipe só, todos os itens são dela: a coluna não diria nada.
 */
export function mostraEquipe(
  listagem: Pick<Listagem, 'minhasEquipes'> | null | undefined,
  escopoEquipe: boolean,
  perfil: string | null | undefined,
): boolean {
  if (!listagem || !escopoEquipe) {
    return false;
  }
  return perfil === 'ADMINISTRADOR' || listagem.minhasEquipes.length > 1;
}

/** O nome da equipe de um item, pelas equipes da listagem. Fora delas (RN92: equipe que deixou de ser habilitada), um aviso. */
export function nomeDaEquipe(equipes: readonly EquipeDaListagem[], equipeId: string | null | undefined): string | null {
  if (!equipeId) {
    return null;
  }
  return equipes.find((e) => e.id === equipeId)?.nome ?? 'Equipe não habilitada';
}

/**
 * Um 400 do POST/PUT de registro indica que a tela usou metadados DESATUALIZADOS quando a API recusou campos como desconhecidos
 * ou inativos (RN01, RN02: detalhes.camposDesatualizados) ou quando ela reclama de um campo que o formulário nem tem (ex.: um
 * campo obrigatório criado depois que a tela abriu). Chaves de sistema (equipeId) e mensagens gerais ($) não contam.
 */
export function metadadosDesatualizados(problema: Problema, chavesDoFormulario: readonly string[]): boolean {
  if (problema.status !== 400) {
    return false;
  }
  const desatualizados = problema.detalhes?.['camposDesatualizados'];
  if (Array.isArray(desatualizados) && desatualizados.length > 0) {
    return true;
  }
  return Object.keys(problema.erros).some((chave) => !CHAVES_GERAIS.has(chave) && !chavesDoFormulario.includes(chave));
}

const CHAVES_GERAIS = new Set(['equipeId', '$']);
