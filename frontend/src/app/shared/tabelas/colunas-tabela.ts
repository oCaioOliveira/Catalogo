import { TipoDado } from '../../core/metadados/modelo';

// Colunas das tabelas com muitas colunas (seção "Tabelas" de docs/design/guia-de-interface.md): larguras por tipo, quais cabem
// na primeira visita, ordem escolhida pela pessoa e o texto do seletor. Funções puras, sem Angular: colunas-tabela.spec.ts.

/** Uma coluna que a tabela pode mostrar. A `fixa` é a do campo de exibição: sempre visível, primeira e presa à esquerda. */
export interface ColunaTabela {
  chave: string;
  nome: string;
  /** Largura padrão (px), pelo tipo: datas e números estreitos, textos mais largos. */
  largura: number;
  fixa?: boolean;
}

/** A coluna estreita das ações ("⋯"), sempre presa à direita. */
export const LARGURA_ACOES = 56;

/** Menor largura aceita ao redimensionar (px). */
export const LARGURA_MINIMA = 72;

/** Quantas colunas aparecem, no mínimo, na primeira visita (mesmo que alguma fique fora da área visível). */
export const MINIMO_COLUNAS = 6;

/** Larguras padrão (px) das colunas que não são campos. */
export const LARGURAS_SISTEMA = { equipe: 150, situacao: 120, media: 150, criadoEm: 150 } as const;

/** A largura padrão de um campo pelo tipo (px). */
export function larguraDoTipo(tipo: TipoDado): number {
  switch (tipo) {
    case 'BOOLEANO':
      return 100;
    case 'INTEIRO':
      return 110;
    case 'DECIMAL':
    case 'DATA':
      return 120;
    case 'LISTA':
      return 140;
    case 'DATA_HORA':
    case 'REFERENCIA':
      return 150;
    case 'URL':
      return 200;
    default:
      return 180;
  }
}

/**
 * Na PRIMEIRA visita (nada salvo), as colunas que cabem confortavelmente em `disponivel` px, na ordem (a fixa sempre, e a das
 * ações é reservada). Pelo menos MINIMO_COLUNAS (ou todas, se houver menos): as demais ficam no seletor.
 */
export function colunasQueCabem(
  colunas: readonly ColunaTabela[],
  disponivel: number,
  minimo = MINIMO_COLUNAS,
): string[] {
  const ordenadas = [...colunas.filter((c) => c.fixa), ...colunas.filter((c) => !c.fixa)];
  const escolhidas: string[] = [];
  let usado = LARGURA_ACOES;
  for (const coluna of ordenadas) {
    const cabe = usado + coluna.largura <= disponivel;
    if (!coluna.fixa && !cabe && escolhidas.length >= minimo) {
      break;
    }
    escolhidas.push(coluna.chave);
    usado += coluna.largura;
  }
  return escolhidas;
}

/**
 * As colunas na ordem da pessoa: a fixa primeiro; depois as escolhidas, na ordem salva. Escolhidas que não existem mais (campo
 * desativado) são ignoradas.
 */
export function colunasVisiveis(todas: readonly ColunaTabela[], escolhidas: readonly string[]): ColunaTabela[] {
  const fixas = todas.filter((c) => c.fixa);
  const demais = escolhidas
    .map((chave) => todas.find((c) => c.chave === chave && !c.fixa))
    .filter((c): c is ColunaTabela => !!c);
  return [...fixas, ...demais];
}

/** Para o seletor: as visíveis (na ordem delas) e depois as escondidas (na ordem original). */
export function ordemDoSeletor(todas: readonly ColunaTabela[], escolhidas: readonly string[]): ColunaTabela[] {
  const visiveis = colunasVisiveis(todas, escolhidas);
  return [...visiveis, ...todas.filter((c) => !visiveis.includes(c))];
}

/** "6 de 9 colunas". */
export function textoColunasVisiveis(visiveis: number, total: number): string {
  return `${visiveis} de ${total} ${total === 1 ? 'coluna' : 'colunas'}`;
}

/** Move uma coluna escolhida uma posição para cima (-1) ou para baixo (+1). Fora dos limites, nada muda. */
export function moverColuna(escolhidas: readonly string[], chave: string, direcao: -1 | 1): string[] {
  const lista = [...escolhidas];
  const de = lista.indexOf(chave);
  const para = de + direcao;
  if (de < 0 || para < 0 || para >= lista.length) {
    return lista;
  }
  [lista[de], lista[para]] = [lista[para], lista[de]];
  return lista;
}

/** Mostra ou esconde uma coluna: ao mostrar, ela entra no fim; a ordem das outras se mantém. */
export function alternarColuna(escolhidas: readonly string[], chave: string, visivel: boolean): string[] {
  const sem = escolhidas.filter((c) => c !== chave);
  return visivel ? [...sem, chave] : sem;
}

/**
 * Indicação de rolagem horizontal: há colunas escondidas à esquerda (já rolou) e/ou à direita (ainda dá para rolar)?
 * Tolerância de 1 px para os arredondamentos do navegador.
 */
export function estadoRolagem(
  scrollLeft: number,
  clientWidth: number,
  scrollWidth: number,
): { esquerda: boolean; direita: boolean } {
  return {
    esquerda: scrollLeft > 1,
    direita: scrollLeft + clientWidth < scrollWidth - 1,
  };
}

/**
 * Clique numa linha clicável: abre o item, MENOS quando o clique foi num controle dentro dela (link, botão, campo, menu) ou
 * quando a pessoa estava selecionando texto. Assim o link do campo de exibição, o "⋯" e os links das referências funcionam.
 */
export function cliqueAbreLinha(alvo: Element | null, textoSelecionado: string): boolean {
  if (textoSelecionado.trim().length > 0) {
    return false;
  }
  return !alvo?.closest('a, button, input, select, textarea, label, [role="button"], [role="menuitem"], .p-checkbox');
}
