/**
 * Colunas visíveis da listagem, NA ORDEM escolhida, salvas no localStorage por grupo e entidade (sem a coluna fixa, que é sempre a
 * primeira). É só conveniência: se o armazenamento não estiver disponível (navegação privada, bloqueado), usa as colunas que
 * cabem na tela. A versão 2 (com Equipe, Situação e Criado em no seletor) ignora o que a anterior guardou.
 */
function chave(grupo: string, entidade: string): string {
  return `catalogo.colunas.v2.${grupo}.${entidade}`;
}

export function esquecerColunas(grupo: string, entidade: string): void {
  try {
    localStorage.removeItem(chave(grupo, entidade));
  } catch {
    // Sem armazenamento: nada guardado.
  }
}

export function lerColunas(grupo: string, entidade: string): string[] | null {
  try {
    const texto = localStorage.getItem(chave(grupo, entidade));
    const colunas: unknown = texto ? JSON.parse(texto) : null;
    return Array.isArray(colunas) && colunas.every((c) => typeof c === 'string') ? colunas : null;
  } catch {
    return null;
  }
}

export function gravarColunas(grupo: string, entidade: string, colunas: string[]): void {
  try {
    localStorage.setItem(chave(grupo, entidade), JSON.stringify(colunas));
  } catch {
    // Sem armazenamento: a escolha vale só até sair da tela.
  }
}
