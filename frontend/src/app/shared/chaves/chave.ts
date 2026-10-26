/**
 * A chave de grupo, entidade, campo ou restrição gerada a partir do NOME (RN53: `^[a-z0-9_]+$`, até 100): minúsculas, sem
 * acentos, com "_" no lugar de espaços e símbolos, sem "_" repetidos nem nas pontas. Começando por número, ganha "c_" (uma chave
 * só de números confunde quem lê URLs e JSON). A chave não muda depois de criada (RN51): a tela só a gera na criação.
 */
export const TAMANHO_MAXIMO_CHAVE = 100;

export function gerarChave(nome: string): string {
  const chave = nome
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .slice(0, TAMANHO_MAXIMO_CHAVE)
    .replace(/_+$/g, '');
  return /^[0-9]/.test(chave) ? `c_${chave}`.slice(0, TAMANHO_MAXIMO_CHAVE) : chave;
}

/** A mesma regra do back-end, para validar a chave digitada à mão. */
export function chaveValida(chave: string): boolean {
  return /^[a-z0-9_]+$/.test(chave) && chave.length <= TAMANHO_MAXIMO_CHAVE;
}
