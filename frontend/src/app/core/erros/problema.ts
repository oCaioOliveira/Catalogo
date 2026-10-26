import { HttpErrorResponse } from '@angular/common/http';

/**
 * Um erro da API (ProblemDetails, RFC 9457) já lido. Toda resposta de erro traz `codigo` e `traceId`
 * (docs/api/codigos-de-erro.md); os 400 de validação trazem `errors` por campo.
 */
export interface Problema {
  status: number;
  codigo: string;
  titulo: string | null;
  detalhe: string | null;
  traceId: string | null;
  /** Mensagens por campo ou parâmetro: { "nome": ["Campo obrigatório."] }. */
  erros: Record<string, string[]>;
  /** Dados extras de alguns erros (ex.: CONFLITO_UNICIDADE → { restricao, campos }). */
  detalhes: Record<string, unknown> | null;
}

/** Status 0: o navegador não conseguiu falar com a API (fora do ar, sem rede, proxy parado). */
export const SEM_CONEXAO = 'SEM_CONEXAO';

export function lerProblema(erro: unknown): Problema {
  if (!(erro instanceof HttpErrorResponse)) {
    return {
      status: -1,
      codigo: 'ERRO_INTERNO',
      titulo: null,
      detalhe: String(erro),
      traceId: null,
      erros: {},
      detalhes: null,
    };
  }

  if (erro.status === 0) {
    return { status: 0, codigo: SEM_CONEXAO, titulo: null, detalhe: null, traceId: null, erros: {}, detalhes: null };
  }

  const corpo = (typeof erro.error === 'object' && erro.error !== null ? erro.error : {}) as Record<string, unknown>;
  return {
    status: erro.status,
    codigo: typeof corpo['codigo'] === 'string' ? corpo['codigo'] : `HTTP_${erro.status}`,
    titulo: typeof corpo['title'] === 'string' ? corpo['title'] : null,
    detalhe: typeof corpo['detail'] === 'string' ? corpo['detail'] : null,
    traceId: typeof corpo['traceId'] === 'string' ? corpo['traceId'] : null,
    erros: (corpo['errors'] as Record<string, string[]> | undefined) ?? {},
    detalhes: (corpo['detalhes'] as Record<string, unknown> | undefined) ?? null,
  };
}
