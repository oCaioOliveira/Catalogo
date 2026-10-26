import { EquipeDaListagem, Listagem } from '../../../core/metadados/listagens';

/** O que a tela de configurações oferece e o que fica escolhido (RN90, RN91). */
export interface EscolhaConfiguracao {
  /** As listagens ativas em que o usuário tem alguma equipe (para o ADMINISTRADOR, alguma habilitada). */
  listagens: Listagem[];
  /** As equipes do usuário na listagem escolhida: só elas são configuradas nela. */
  equipes: EquipeDaListagem[];
  grupo: string | null;
  equipeId: string | null;
}

/**
 * A listagem vem primeiro: só se configura uma equipe HABILITADA numa listagem que o usuário vê (RN90, RN91). Uma listagem ou
 * equipe da URL que não vale (fora das opções) é trocada pela primeira que vale; sem nenhuma, nada fica escolhido.
 */
export function escolhaDaConfiguracao(
  listagens: readonly Listagem[],
  grupo: string | null,
  equipeId: string | null,
): EscolhaConfiguracao {
  const comEquipes = listagens.filter((l) => l.ativo && l.minhasEquipes.length > 0);
  const listagem = comEquipes.find((l) => l.chave === grupo) ?? comEquipes[0] ?? null;
  const equipes = listagem?.minhasEquipes ?? [];
  const equipe = equipes.find((e) => e.id === equipeId) ?? equipes[0] ?? null;
  return { listagens: comEquipes, equipes, grupo: listagem?.chave ?? null, equipeId: equipe?.id ?? null };
}
