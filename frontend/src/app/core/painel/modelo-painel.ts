import { IndicadoresDto, VisaoGeralDto } from '../../api/models';

/**
 * As respostas do painel (docs/api/painel.md) como o front usa: com os campos obrigatórios preenchidos (os modelos gerados
 * deixam tudo opcional). Uma linha de indicadores é dinâmica: uma propriedade por dimensão e por métrica, pela chave.
 */

export interface DimensaoIndicador {
  /** Como veio no agruparPor: "relatorio_teste.data_execucao:mes". */
  chave: string;
  /** Para legendas: "Data de execução (Relatório) (mês)". */
  nome: string;
  /** O tipo do campo ("REFERENCIA", "DATA_HORA"...), ou "EQUIPE" e "SITUACAO_APROVACAO". */
  tipo: string;
  /** "dia", "mes", "trimestre" ou "ano" nas dimensões de data; nulo nas demais. */
  periodo: string | null;
}

export interface MetricaIndicador {
  chave: string;
  nome: string;
}

/** Uma linha: `{ "relatorio_teste.ambiente": { id, rotulo }, "contagem": 3, "media:vazao_media_tps": 20 }`. */
export type LinhaIndicador = Record<string, unknown>;

export interface Indicadores {
  dimensoes: DimensaoIndicador[];
  metricas: MetricaIndicador[];
  linhas: LinhaIndicador[];
  truncado: boolean;
}

export function indicadoresDeDto(dto: IndicadoresDto): Indicadores {
  return {
    dimensoes: (dto.dimensoes ?? []).map((d) => ({
      chave: d.chave ?? '',
      nome: d.nome ?? d.chave ?? '',
      tipo: d.tipo ?? '',
      periodo: d.periodo ?? null,
    })),
    metricas: (dto.metricas ?? []).map((m) => ({ chave: m.chave ?? '', nome: m.nome ?? m.chave ?? '' })),
    linhas: dto.linhas ?? [],
    truncado: dto.truncado ?? false,
  };
}

/** A visão geral é usada como veio (os blocos de aprovação e avaliação são nulos quando não se aplicam). */
export type VisaoGeral = VisaoGeralDto;

/** Período e equipe do painel, como a API recebe: datas "aaaa-mm-dd"; equipe nula = todas as que o usuário vê. */
export interface FiltrosPainel {
  de: string;
  ate: string;
  equipeId: string | null;
}
