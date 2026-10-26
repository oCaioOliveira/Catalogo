import { EquipeNaListagem } from '../../../../core/administracao/estrutura-listagem';
import { normalizarBusca } from '../../../../core/layout/navegacao';

// Regras da aba Equipes da listagem (RN90 a RN93), em funções puras (equipes-listagem.spec.ts). Quem decide é a API.

/** As equipes habilitadas hoje (o ponto de partida do seletor). */
export function habilitadas(equipes: readonly EquipeNaListagem[]): Set<string> {
  return new Set(equipes.filter((e) => e.habilitada).map((e) => e.id));
}

/** A escolha mudou em relação ao que está gravado? */
export function alterouEquipes(equipes: readonly EquipeNaListagem[], escolhidas: ReadonlySet<string>): boolean {
  const atuais = habilitadas(equipes);
  return atuais.size !== escolhidas.size || [...escolhidas].some((id) => !atuais.has(id));
}

/**
 * RN92: as equipes que estão saindo e têm itens nesta listagem. Os itens delas ficam ocultos para os membros (só o administrador
 * os vê) e voltam ao habilitá-las de novo. A tela avisa antes; a simulação da API confirma a quantidade.
 */
export function saindoComItens(
  equipes: readonly EquipeNaListagem[],
  escolhidas: ReadonlySet<string>,
): EquipeNaListagem[] {
  return equipes.filter((e) => e.habilitada && !escolhidas.has(e.id) && e.quantidadeItens > 0);
}

/** A lista completa para o PUT, na ordem da tela (estável para o histórico). */
export function listaParaGravar(equipes: readonly EquipeNaListagem[], escolhidas: ReadonlySet<string>): string[] {
  return equipes.filter((e) => escolhidas.has(e.id)).map((e) => e.id);
}

/** A ordem da aba: as ativas primeiro (são as que importam), depois as inativas; cada grupo por nome. */
export function ordenarEquipes(equipes: readonly EquipeNaListagem[]): EquipeNaListagem[] {
  return [...equipes].sort((a, b) => Number(b.ativa) - Number(a.ativa) || a.nome.localeCompare(b.nome, 'pt-BR'));
}

/** Quantas equipes mudaram em relação ao que está gravado (marcadas ou desmarcadas): o "3 alterações não salvas". */
export function quantasAlteracoes(equipes: readonly EquipeNaListagem[], escolhidas: ReadonlySet<string>): number {
  const atuais = habilitadas(equipes);
  return equipes.filter((e) => atuais.has(e.id) !== escolhidas.has(e.id)).length;
}

/** "3 alterações não salvas" (vazio sem alterações). */
export function textoAlteracoes(quantidade: number): string {
  if (quantidade === 0) {
    return '';
  }
  return quantidade === 1 ? '1 alteração não salva' : `${quantidade} alterações não salvas`;
}

/** "12 de 40 equipes habilitadas" (a escolha atual, ainda não gravada, contra o total). */
export function textoContador(escolhidas: number, total: number): string {
  return `${escolhidas} de ${total} ${total === 1 ? 'equipe habilitada' : 'equipes habilitadas'}`;
}

/** As equipes que a lista mostra: pela busca no nome (sem acentos nem maiúsculas) e, se pedido, só as marcadas. */
export function equipesVisiveis(
  equipes: readonly EquipeNaListagem[],
  busca: string,
  soHabilitadas: boolean,
  escolhidas: ReadonlySet<string>,
): EquipeNaListagem[] {
  const termo = normalizarBusca(busca);
  return equipes.filter(
    (e) => (!termo || normalizarBusca(e.nome).includes(termo)) && (!soHabilitadas || escolhidas.has(e.id)),
  );
}

/**
 * "Selecionar todas as visíveis" (marcar = true) e "Limpar seleção" (false): só mexem nas equipes que a busca mostra. Selecionar
 * ignora as inativas (habilitar uma equipe inativa não dá acesso a ninguém); limpar tira todas as visíveis.
 */
export function marcarVisiveis(
  escolhidas: ReadonlySet<string>,
  visiveis: readonly EquipeNaListagem[],
  marcar: boolean,
): Set<string> {
  const novas = new Set(escolhidas);
  for (const equipe of visiveis) {
    if (marcar && equipe.ativa) {
      novas.add(equipe.id);
    } else if (!marcar) {
      novas.delete(equipe.id);
    }
  }
  return novas;
}
