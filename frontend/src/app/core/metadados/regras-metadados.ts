import { Campo, Entidade } from './modelo';

// Regras que o front tira dos metadados. Funções puras, sem Angular: testadas em regras-metadados.spec.ts.

/**
 * O campo que faz de `entidade` uma FILHA de outra: um REFERENCIA obrigatório com ação de exclusão CASCATA
 * (ex.: cenario_teste.relatorio_teste). Filhas não aparecem no menu: são editadas no detalhe do pai.
 */
export function campoDoPai(entidade: Entidade): Campo | null {
  return entidade.campos.find((c) => c.tipo === 'REFERENCIA' && c.obrigatorio && c.acaoExclusao === 'CASCATA') ?? null;
}

export function ehEntidadeFilha(entidade: Entidade): boolean {
  return campoDoPai(entidade) !== null;
}

/** As entidades filhas de `pai`, com o campo de cada uma que aponta para ele. */
export function filhasDe(pai: Entidade, todas: Entidade[]): { entidade: Entidade; campoPai: Campo }[] {
  return todas.flatMap((entidade) => {
    const campo = campoDoPai(entidade);
    return campo && campo.entidadeReferenciada === pai.chave ? [{ entidade, campoPai: campo }] : [];
  });
}

/** Seções do menu: as entidades principais (por equipe) e os cadastros de apoio (globais). Filhas ficam de fora. */
export function montarMenu(entidades: Entidade[]): { principais: Entidade[]; apoio: Entidade[] } {
  const doMenu = entidades.filter((e) => !ehEntidadeFilha(e)).sort((a, b) => a.ordem - b.ordem);
  return {
    principais: doMenu.filter((e) => e.escopoEquipe),
    apoio: doMenu.filter((e) => !e.escopoEquipe),
  };
}

/**
 * A tela inicial de uma listagem (ADR 0043): a primeira entidade POR EQUIPE e não filha, pela `ordem` dos metadados (é a primeira
 * das principais do menu). Listagem só com cadastros globais: o primeiro deles. Nada de entidade fixa no front.
 */
export function entidadeInicial(entidades: Entidade[]): Entidade | null {
  const menu = montarMenu(entidades);
  return menu.principais[0] ?? menu.apoio[0] ?? null;
}

/**
 * O nome do tipo de item no meio de uma frase ("Salvar relatório de teste", "Adicionar cenário de teste"): em minúsculas, menos as
 * siglas, que ficam como estão ("RTC", "URL").
 */
export function nomeEmFrase(nome: string): string {
  return nome
    .split(' ')
    .map((palavra) =>
      palavra.length > 1 && palavra === palavra.toLocaleUpperCase('pt-BR')
        ? palavra
        : palavra.toLocaleLowerCase('pt-BR'),
    )
    .join(' ');
}

/** O ícone genérico, para tipos de item sem ícone (RN95). */
export const ICONE_GENERICO = 'pi-file';

/** As classes do ícone do tipo de item ("pi pi-chart-line"), ou do genérico quando ele não tem um (RN95). */
export function classeIcone(entidade: Pick<Entidade, 'icone'> | null | undefined): string {
  return `pi ${entidade?.icone || ICONE_GENERICO}`;
}

/** O nome no plural ("Relatórios de Teste"), para títulos de listagem, menu, seções de filhos e trilhas. Sem plural, o nome. */
export function nomePlural(entidade: Entidade): string {
  return entidade.nomePlural ?? entidade.nome;
}

/** A entidade que um campo REFERENCIA aponta, entre as que o usuário vê (null se não for REFERENCIA ou ela não estiver lá). */
export function entidadeApontada(campo: Campo, todas: Entidade[]): Entidade | null {
  return campo.tipo === 'REFERENCIA' ? (todas.find((e) => e.chave === campo.entidadeReferenciada) ?? null) : null;
}

/**
 * Uma referência aparece como LINK só quando aponta para uma entidade POR EQUIPE (Projeto, RTC, Relatório): o item apontado é um
 * cadastro do dia a dia, que vale abrir. Referências para cadastros de apoio GLOBAIS (Ambiente, Status, Sigla, Comunidade) aparecem
 * como texto, no detalhe e nas tabelas. Entidade apontada que o usuário não vê: texto também (o link daria "não encontrado").
 */
export function referenciaComoLink(campo: Campo, todas: Entidade[]): boolean {
  return entidadeApontada(campo, todas)?.escopoEquipe === true;
}

/**
 * RN19: ordena-se por todo campo com valor comparável, menos TEXTO_LONGO. REFERENCIA ordena pelo campo de exibição do registro
 * apontado: só quando a entidade apontada tem um (sem ele, a API responde 400).
 */
export function podeOrdenarPor(campo: Campo, todas: Entidade[]): boolean {
  if (campo.tipo === 'TEXTO_LONGO') {
    return false;
  }
  if (campo.tipo === 'REFERENCIA') {
    const apontada = entidadeApontada(campo, todas);
    return !!apontada?.campoExibicao && apontada.campos.some((c) => c.chave === apontada.campoExibicao);
  }
  return true;
}

/** Colunas da tabela por padrão: todos os campos ativos, menos TEXTO_LONGO (a URL entra: é curta e vira link). */
export function colunasPadrao(entidade: Entidade): string[] {
  return entidade.campos.filter((c) => c.tipo !== 'TEXTO_LONGO').map((c) => c.chave);
}
