import { HistoricoDto, RegistroDto } from '../../api/models';

/**
 * Um registro como vem da API (RegistroDto). Os valores dos campos ficam em `dados`, com as chaves dos campos
 * da entidade: são dinâmicos (vêm dos metadados), por isso `Record<string, unknown>`.
 * REFERENCIA vem como { id, rotulo }; DATA como "aaaa-mm-dd"; DATA_HORA em UTC ("...Z").
 */
export interface Registro extends Omit<RegistroDto, 'dados'> {
  id: string;
  dados: Record<string, unknown>;
}

export interface PaginaDe<T> {
  itens: T[];
  pagina: number;
  tamanho: number;
  total: number;
  totalPaginas: number;
}

export type ItemHistorico = HistoricoDto;

/** Corpo do POST/PUT: os campos (referência só com o id) e, conforme o caso, `equipeId` e `versao`. */
export type CorpoRegistro = Record<string, unknown>;

export function registroDeDto(dto: RegistroDto): Registro {
  return { ...dto, id: dto.id ?? '', dados: (dto.dados as Record<string, unknown> | null) ?? {} };
}

/**
 * RN94: uma relação do item aberto (GET .../registros/{id}/relacionados): os itens de `entidade` que apontam para ele por `campo`,
 * com a quantidade que o usuário vê. `filha`: campo obrigatório e CASCATA (editada no detalhe do item).
 */
export interface Relacionado {
  entidade: { chave: string; nome: string; nomePlural: string | null };
  campo: { chave: string; nome: string };
  filha: boolean;
  quantidade: number;
}

/** Um item que o usuário criou ou alterou por último (RN96, "Continuar de onde parou" na página Início). */
export interface Recente {
  id: string;
  /** A chave do tipo de item. */
  entidade: string;
  /** O valor do campo de exibição (nulo sem ele, RN20). */
  rotulo: string | null;
  /** ISO 8601 (UTC): a última criação ou alteração feita por ele. */
  ocorridoEm: string;
}
