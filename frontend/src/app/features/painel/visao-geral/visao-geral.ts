import { Entidade } from '../../../core/metadados/modelo';
import { nomePlural } from '../../../core/metadados/regras-metadados';
import { Indicadores, VisaoGeral } from '../../../core/painel/modelo-painel';
import { CartaoKpi } from '../graficos/transformar-dados';

/**
 * GET .../visao-geral → o que a página mostra (visao-geral.spec.ts). Os gráficos da visão geral viram `Indicadores` (o mesmo
 * formato da agregação configurável), para serem desenhados pelo mesmo componente, com as mesmas regras visuais e "Ver dados".
 * Os blocos de aprovação e avaliação só existem quando a API traz dados deles (null → o bloco não aparece).
 */

export function kpisDaVisaoGeral(visao: VisaoGeral, principal: Entidade | null): CartaoKpi[] {
  const volume = visao.volume ?? {};
  const daPrincipal = (volume.porEntidade ?? []).find((e) => e.entidade === principal?.chave);
  const nome = principal ? nomePlural(principal) : 'Itens';
  return [
    { chave: 'total', rotulo: `${nome}: total`, valor: daPrincipal?.total ?? 0, unidade: null },
    {
      chave: 'criados',
      rotulo: `${nome}: criados no período`,
      valor: daPrincipal?.criadosNoPeriodo ?? 0,
      unidade: null,
    },
    { chave: 'alterados', rotulo: 'Itens alterados no período', valor: volume.alteradosNoPeriodo ?? 0, unidade: null },
    {
      chave: 'equipes',
      rotulo: 'Equipes com itens',
      valor: (volume.porEquipe ?? []).filter((e) => (e.quantidade ?? 0) > 0).length,
      unidade: null,
    },
  ];
}

export function indicadoresCriadosPorMes(visao: VisaoGeral): Indicadores {
  return {
    dimensoes: [{ chave: 'mes', nome: 'Mês de criação', tipo: 'DATA_HORA', periodo: 'mes' }],
    metricas: [{ chave: 'quantidade', nome: 'Itens criados' }],
    linhas: (visao.volume?.criadosPorMes ?? []).map((m) => ({ mes: m.mes ?? null, quantidade: m.quantidade ?? 0 })),
    truncado: false,
  };
}

export function indicadoresPorEquipe(visao: VisaoGeral): Indicadores {
  return {
    dimensoes: [{ chave: 'equipe', nome: 'Equipe', tipo: 'EQUIPE', periodo: null }],
    metricas: [{ chave: 'quantidade', nome: 'Itens' }],
    linhas: (visao.volume?.porEquipe ?? []).map((e) => ({
      equipe: { id: e.equipeId ?? '', rotulo: e.nome ?? null },
      quantidade: e.quantidade ?? 0,
    })),
    truncado: false,
  };
}

export interface LinhaAtividade {
  matricula: string;
  nome: string;
  criacoes: number;
  alteracoes: number;
  total: number;
}

/** Os 10 usuários mais ativos (a API já devolve os 10, em ordem). */
export function atividadeDaVisaoGeral(visao: VisaoGeral): LinhaAtividade[] {
  return (visao.atividade ?? []).slice(0, 10).map((a) => ({
    matricula: a.matricula ?? '',
    nome: a.nome ?? '',
    criacoes: a.criacoes ?? 0,
    alteracoes: a.alteracoes ?? 0,
    total: a.total ?? 0,
  }));
}

export interface ResumoAprovacao {
  diasPendenteAntigo: number;
  cartoes: CartaoKpi[];
  porEntidade: {
    nome: string;
    pendentes: number;
    aprovados: number;
    rejeitados: number;
    taxaRejeicao: number | null;
  }[];
}

/** RN82. Nulo quando a aprovação não está ativa em nenhuma entidade visível. */
export function resumoAprovacao(visao: VisaoGeral): ResumoAprovacao | null {
  const aprovacao = visao.aprovacao;
  if (!aprovacao || (aprovacao.porEntidade ?? []).length === 0) {
    return null;
  }
  const total = aprovacao.total ?? {};
  const dias = aprovacao.diasPendenteAntigo ?? 7;
  const taxa = (valor: number | null | undefined) => (valor === null || valor === undefined ? null : valor * 100);
  return {
    diasPendenteAntigo: dias,
    cartoes: [
      { chave: 'pendentes', rotulo: 'Pendentes', valor: total.pendentes ?? 0, unidade: null },
      {
        chave: 'antigos',
        rotulo: `Pendentes há mais de ${dias} dias`,
        valor: total.pendentesAntigos ?? 0,
        unidade: null,
      },
      { chave: 'aprovados', rotulo: 'Aprovados', valor: total.aprovados ?? 0, unidade: null },
      { chave: 'rejeitados', rotulo: 'Rejeitados', valor: total.rejeitados ?? 0, unidade: null },
      {
        chave: 'tempo',
        rotulo: 'Tempo médio até a 1ª decisão',
        valor: total.tempoMedioPrimeiraDecisaoHoras ?? null,
        unidade: 'h',
      },
      { chave: 'taxa', rotulo: 'Taxa de rejeição no período', valor: taxa(total.taxaRejeicao), unidade: '%' },
    ],
    porEntidade: (aprovacao.porEntidade ?? []).map((e) => ({
      nome: e.nome ?? e.entidade ?? '',
      pendentes: e.numeros?.pendentes ?? 0,
      aprovados: e.numeros?.aprovados ?? 0,
      rejeitados: e.numeros?.rejeitados ?? 0,
      taxaRejeicao: taxa(e.numeros?.taxaRejeicao),
    })),
  };
}

export interface CriterioAvaliado {
  chave: string;
  titulo: string;
  media: number | null;
  quantidade: number;
  distribuicao: Indicadores;
  melhores: { rotulo: string; media: number; quantidade: number }[];
  piores: { rotulo: string; media: number; quantidade: number }[];
}

/**
 * RN83. Só os critérios com pelo menos uma nota; nulo quando não sobra nenhum (sem modelo de avaliação ativo, ou nenhum item
 * avaliado): o bloco só aparece com dados.
 */
export function resumoAvaliacao(visao: VisaoGeral): { minimo: number; criterios: CriterioAvaliado[] } | null {
  const avaliacao = visao.avaliacao;
  const criterios = (avaliacao?.porEntidade ?? []).flatMap((entidade) =>
    (entidade.criterios ?? [])
      .filter((c) => (c.quantidade ?? 0) > 0)
      .map((c) => ({
        chave: `${entidade.entidade}-${c.criterio}`,
        titulo: `${entidade.nome ?? entidade.entidade}: ${c.nome ?? c.criterio}`,
        media: c.media ?? null,
        quantidade: c.quantidade ?? 0,
        distribuicao: {
          dimensoes: [{ chave: 'estrelas', nome: 'Estrelas', tipo: 'LISTA', periodo: null }],
          metricas: [{ chave: 'quantidade', nome: 'Notas' }],
          linhas: ['1', '2', '3', '4', '5'].map((estrelas) => ({
            estrelas: `${estrelas} ★`,
            quantidade: c.distribuicao?.[estrelas] ?? 0,
          })),
          truncado: false,
        },
        melhores: (c.melhores ?? []).map(itemRanking),
        piores: (c.piores ?? []).map(itemRanking),
      })),
  );
  return avaliacao && criterios.length > 0 ? { minimo: avaliacao.minimoAvaliacoesRanking ?? 3, criterios } : null;
}

function itemRanking(item: { rotulo?: string | null; media?: number; quantidade?: number }) {
  return { rotulo: item.rotulo ?? '(sem rótulo)', media: item.media ?? 0, quantidade: item.quantidade ?? 0 };
}
