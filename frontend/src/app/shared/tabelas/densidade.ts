/**
 * Densidade das tabelas (todas as listagens): "compacto" é o padrão (o tamanho "small" do p-table, mais linhas por tela);
 * "confortavel" é o tamanho normal. A escolha fica no localStorage, para todo o sistema. É só conveniência: sem armazenamento
 * (navegação privada, bloqueado), vale o padrão. Funções puras (densidade.spec.ts).
 */
export type Densidade = 'compacto' | 'confortavel';

export const DENSIDADE_PADRAO: Densidade = 'compacto';
export const CHAVE_DENSIDADE = 'catalogo.densidadeTabelas';

export function lerDensidade(armazenamento: Pick<Storage, 'getItem'> | null = armazenamentoLocal()): Densidade {
  try {
    const valor = armazenamento?.getItem(CHAVE_DENSIDADE);
    return valor === 'compacto' || valor === 'confortavel' ? valor : DENSIDADE_PADRAO;
  } catch {
    return DENSIDADE_PADRAO;
  }
}

export function gravarDensidade(
  densidade: Densidade,
  armazenamento: Pick<Storage, 'setItem'> | null = armazenamentoLocal(),
): void {
  try {
    armazenamento?.setItem(CHAVE_DENSIDADE, densidade);
  } catch {
    // Sem armazenamento: a escolha vale até recarregar a página.
  }
}

/** O `size` do p-table para a densidade. */
export function tamanhoDaTabela(densidade: Densidade): 'small' | undefined {
  return densidade === 'compacto' ? 'small' : undefined;
}

function armazenamentoLocal(): Storage | null {
  try {
    return localStorage;
  } catch {
    return null;
  }
}
