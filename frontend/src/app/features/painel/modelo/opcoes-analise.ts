import { Entidade } from '../../../core/metadados/modelo';
import {
  CaminhoResolvido,
  TIPOS_DATA,
  TIPOS_DIMENSAO,
  TIPOS_FILTRO,
  TIPOS_NUMERICOS,
  camposAlcancaveis,
} from './caminhos';
import { ConfiguracaoWidget } from './configuracao';
import { visualizacoesPermitidas } from './visualizacoes';

/**
 * As opções dos controles da análise personalizada, a partir dos METADADOS (opcoes-analise.spec.ts): os campos da entidade e
 * os alcançáveis por UM salto de referência, com rótulos como "Relatório › Ambiente". A API aceita até 2 saltos; a tela
 * oferece um, para a lista não ficar enorme (um widget de arquivo pode usar dois).
 */

export interface OpcaoCampo {
  valor: string;
  rotulo: string;
  /** Dimensão de data: precisa de período (dia, mês, trimestre, ano). */
  data: boolean;
}

export const PERIODOS = [
  { valor: 'dia', rotulo: 'Dia' },
  { valor: 'mes', rotulo: 'Mês' },
  { valor: 'trimestre', rotulo: 'Trimestre' },
  { valor: 'ano', rotulo: 'Ano' },
];

export const FUNCOES = [
  { valor: 'media', rotulo: 'Média de' },
  { valor: 'soma', rotulo: 'Soma de' },
  { valor: 'minimo', rotulo: 'Mínimo de' },
  { valor: 'maximo', rotulo: 'Máximo de' },
];

function opcao(caminho: CaminhoResolvido): OpcaoCampo {
  return { valor: caminho.caminho, rotulo: caminho.rotulo, data: TIPOS_DATA.includes(caminho.campo.tipo) };
}

/** Campos que agrupam (e as dimensões de sistema: equipe, situação de aprovação e data de criação). */
export function opcoesDeDimensao(entidades: Entidade[], entidade: Entidade): OpcaoCampo[] {
  const sistema: OpcaoCampo[] = [
    ...(entidade.escopoEquipe ? [{ valor: 'equipe', rotulo: 'Equipe', data: false }] : []),
    { valor: 'situacaoAprovacao', rotulo: 'Situação de aprovação', data: false },
    { valor: 'criadoEm', rotulo: 'Data de criação', data: true },
  ];
  return [...camposAlcancaveis(entidades, entidade, TIPOS_DIMENSAO).map(opcao), ...sistema];
}

/** "contagem" e, para cada campo numérico (também por caminho), média, soma, mínimo e máximo. */
export function opcoesDeMetrica(entidades: Entidade[], entidade: Entidade): { valor: string; rotulo: string }[] {
  const numericos = camposAlcancaveis(entidades, entidade, TIPOS_NUMERICOS);
  return [
    { valor: 'contagem', rotulo: 'Contagem de registros' },
    ...numericos.flatMap((c) =>
      FUNCOES.map((f) => ({ valor: `${f.valor}:${c.caminho}`, rotulo: `${f.rotulo} ${c.rotulo}` })),
    ),
  ];
}

/** O campo que recebe o período: a data de criação (null) ou um campo DATA/DATA_HORA. */
export function opcoesDePeriodo(entidades: Entidade[], entidade: Entidade): { valor: string | null; rotulo: string }[] {
  return [
    { valor: null, rotulo: 'Data de criação do registro' },
    ...camposAlcancaveis(entidades, entidade, TIPOS_DATA).map((c) => ({ valor: c.caminho, rotulo: c.rotulo })),
  ];
}

export function camposDeFiltro(entidades: Entidade[], entidade: Entidade): CaminhoResolvido[] {
  return camposAlcancaveis(entidades, entidade, TIPOS_FILTRO);
}

/**
 * Depois de uma mudança, a configuração continua coerente: a visualização volta para a primeira permitida se deixou de
 * servir, e a ordenação sai se a dimensão ou métrica dela saiu.
 */
export function normalizarAnalise(configuracao: ConfiguracaoWidget): ConfiguracaoWidget {
  const permitidas = visualizacoesPermitidas(configuracao.agruparPor, configuracao.metricas);
  const normalizada = { ...configuracao };
  if (!permitidas.includes(normalizada.visualizacao)) {
    normalizada.visualizacao = permitidas[0];
  }
  const ordem = normalizada.ordenarPor?.replace(/^-/, '');
  if (ordem && ![...normalizada.agruparPor, ...normalizada.metricas, 'contagem'].includes(ordem)) {
    delete normalizada.ordenarPor;
  }
  return normalizada;
}
