/**
 * Configurações do front num lugar só. Por enquanto só há execução local: a API é chamada por caminhos
 * relativos (/api/v1/...) e o `npm start` encaminha /api para ela pelo proxy (proxy.conf.json).
 */
export const environment = {
  // Não há listagem nem entidade fixas: a tela inicial vem dos metadados (ADR 0043).

  /** Itens por página das listagens (a API aceita de 1 a 100). */
  tamanhoPagina: 20,

  /** Opções de itens por página mostradas na tabela. */
  opcoesTamanhoPagina: [10, 20, 50, 100],

  /** Quantas opções o select de referência busca por vez. */
  tamanhoPaginaOpcoes: 20,
};
