import { CriterioAvaliacaoDto, ModeloAvaliacaoDto } from '../../api/models';

/** Tipos de critério (RN42). */
export type TipoCriterio = 'ESTRELAS' | 'TEXTO';
export type RegraComentario = 'NENHUM' | 'OPCIONAL' | 'OBRIGATORIO';

/** Um critério de um modelo de avaliação. `possuiRespostas`: já foi respondido (então não sai da lista nem muda de tipo, RN43). */
export interface CriterioAvaliacao {
  id: string | null;
  chave: string;
  nome: string;
  descricao: string | null;
  tipo: TipoCriterio;
  obrigatorio: boolean;
  comentario: RegraComentario;
  tamanhoMaximoTexto: number;
  ordem: number;
  ativo: boolean;
  possuiRespostas: boolean;
}

export interface ModeloAvaliacao {
  id: string;
  nome: string;
  descricao: string | null;
  ativo: boolean;
  versao: string;
  quantidadeAvaliacoes: number;
  criterios: CriterioAvaliacao[];
}

export const TAMANHO_TEXTO_PADRAO = 2000;

export function criterioDeDto(dto: CriterioAvaliacaoDto): CriterioAvaliacao {
  return {
    id: dto.id ?? null,
    chave: dto.chave ?? '',
    nome: dto.nome ?? dto.chave ?? '',
    descricao: dto.descricao ?? null,
    tipo: (dto.tipo as TipoCriterio) ?? 'ESTRELAS',
    obrigatorio: dto.obrigatorio ?? false,
    comentario: (dto.comentario as RegraComentario) ?? 'NENHUM',
    tamanhoMaximoTexto: dto.tamanhoMaximoTexto ?? TAMANHO_TEXTO_PADRAO,
    ordem: dto.ordem ?? 0,
    ativo: dto.ativo ?? true,
    possuiRespostas: dto.possuiRespostas ?? false,
  };
}

export function modeloDeDto(dto: ModeloAvaliacaoDto): ModeloAvaliacao {
  return {
    id: dto.id ?? '',
    nome: dto.nome ?? '',
    descricao: dto.descricao ?? null,
    ativo: dto.ativo ?? false,
    versao: dto.versao ?? '',
    quantidadeAvaliacoes: dto.quantidadeAvaliacoes ?? 0,
    criterios: (dto.criterios ?? []).map(criterioDeDto).sort((a, b) => a.ordem - b.ordem),
  };
}
