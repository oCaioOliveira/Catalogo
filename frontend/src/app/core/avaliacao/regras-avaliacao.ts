import { ResumoCriterioDto } from '../../api/models';
import { CriterioAvaliacao } from './modelo-avaliacao';

// Regras de tela da avaliação (RN42 a RN48), em funções puras (regras-avaliacao.spec.ts). A API valida de novo.

/** Uma barra da distribuição: quantas notas N e o percentual sobre o total do critério. */
export interface BarraDistribuicao {
  estrelas: number;
  quantidade: number;
  percentual: number;
}

/** Um critério de estrelas no resumo do item: a média (em estrelas e em número), a quantidade e a distribuição de 5 a 1. */
export interface ResumoCriterio {
  chave: string;
  nome: string;
  media: number | null;
  mediaTexto: string;
  quantidade: number;
  distribuicao: BarraDistribuicao[];
}

/** "4,3" (uma casa, como a API arredonda, RN47); sem notas: "—". */
export function mediaEmTexto(media: number | null | undefined): string {
  return media === null || media === undefined
    ? '—'
    : media.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
}

export function resumoDoCriterio(dto: ResumoCriterioDto): ResumoCriterio {
  const quantidade = dto.quantidade ?? 0;
  return {
    chave: dto.chave ?? '',
    nome: dto.nome ?? dto.chave ?? '',
    media: dto.media ?? null,
    mediaTexto: mediaEmTexto(dto.media),
    quantidade,
    distribuicao: [5, 4, 3, 2, 1].map((estrelas) => {
      const n = dto.distribuicao?.[String(estrelas)] ?? 0;
      return { estrelas, quantidade: n, percentual: quantidade > 0 ? Math.round((n / quantidade) * 100) : 0 };
    }),
  };
}

/** A resposta de um critério no formulário (RN42): estrelas e comentário, ou o texto livre. */
export interface RespostaFormulario {
  estrelas: number | null;
  comentario: string;
  texto: string;
}

export function respostaVazia(): RespostaFormulario {
  return { estrelas: null, comentario: '', texto: '' };
}

/** As respostas guardadas (formato da API) → o formulário. */
export function respostasDaApi(
  criterios: CriterioAvaliacao[],
  respostas: Record<string, unknown> | null | undefined,
): Record<string, RespostaFormulario> {
  const valores: Record<string, RespostaFormulario> = {};
  for (const criterio of criterios) {
    const r = (respostas?.[criterio.chave] ?? null) as {
      estrelas?: number;
      comentario?: string;
      texto?: string;
    } | null;
    valores[criterio.chave] = {
      estrelas: typeof r?.estrelas === 'number' ? r.estrelas : null,
      comentario: r?.comentario ?? '',
      texto: r?.texto ?? '',
    };
  }
  return valores;
}

/**
 * O formulário → o corpo do PUT (RN45, RN46): critério sem resposta não vai; comentário só nos critérios que o aceitam e não em
 * branco; textos sem os espaços das pontas.
 */
export function respostasParaApi(
  criterios: CriterioAvaliacao[],
  valores: Record<string, RespostaFormulario>,
): Record<string, unknown> {
  const respostas: Record<string, unknown> = {};
  for (const criterio of criterios.filter((c) => c.ativo)) {
    const v = valores[criterio.chave] ?? respostaVazia();
    if (criterio.tipo === 'ESTRELAS') {
      if (v.estrelas === null) {
        continue;
      }
      const comentario = v.comentario.trim();
      respostas[criterio.chave] =
        criterio.comentario !== 'NENHUM' && comentario
          ? { estrelas: v.estrelas, comentario }
          : { estrelas: v.estrelas };
    } else if (v.texto.trim()) {
      respostas[criterio.chave] = { texto: v.texto.trim() };
    }
  }
  return respostas;
}

/**
 * A mesma validação da API (RN46), com as MESMAS chaves de erro (`respostas.<criterio>`, `respostas.<criterio>.comentario`...):
 * assim os erros do front e os do servidor aparecem no mesmo lugar.
 */
export function validarRespostas(
  criterios: CriterioAvaliacao[],
  valores: Record<string, RespostaFormulario>,
): Record<string, string[]> {
  const erros: Record<string, string[]> = {};
  const adicionar = (chave: string, mensagem: string) => (erros[chave] = [...(erros[chave] ?? []), mensagem]);
  for (const criterio of criterios.filter((c) => c.ativo)) {
    const v = valores[criterio.chave] ?? respostaVazia();
    const base = `respostas.${criterio.chave}`;
    if (criterio.tipo === 'ESTRELAS') {
      if (v.estrelas === null) {
        if (criterio.obrigatorio) {
          adicionar(base, 'Dê uma nota de 1 a 5 estrelas.');
        }
        continue;
      }
      const comentario = v.comentario.trim();
      if (criterio.comentario === 'OBRIGATORIO' && !comentario) {
        adicionar(`${base}.comentario`, 'O comentário é obrigatório neste critério.');
      }
      if (comentario.length > criterio.tamanhoMaximoTexto) {
        adicionar(
          `${base}.comentario`,
          `No máximo ${criterio.tamanhoMaximoTexto} caracteres (tem ${comentario.length}).`,
        );
      }
    } else {
      const texto = v.texto.trim();
      if (!texto && criterio.obrigatorio) {
        adicionar(base, 'Resposta obrigatória.');
      }
      if (texto.length > criterio.tamanhoMaximoTexto) {
        adicionar(`${base}.texto`, `No máximo ${criterio.tamanhoMaximoTexto} caracteres (tem ${texto.length}).`);
      }
    }
  }
  return erros;
}

/** O rótulo acessível de uma nota: "4 de 5 estrelas" (ou "sem nota"). */
export function rotuloEstrelas(estrelas: number | null | undefined): string {
  if (!estrelas) {
    return 'sem nota';
  }
  return estrelas === 1 ? '1 de 5 estrelas' : `${estrelas} de 5 estrelas`;
}
