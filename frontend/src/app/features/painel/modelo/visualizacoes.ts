import { TipoVisualizacao } from './configuracao';

/**
 * Que visualizações fazem sentido para uma combinação de dimensões e métricas. A análise personalizada só oferece estas, e a
 * validação dos widgets recusa as outras. Funções puras (visualizacoes.spec.ts):
 *   - sem dimensão: KPI (um cartão por métrica) ou tabela;
 *   - 1 dimensão: colunas, barras horizontais ou tabela; linhas só se a dimensão é de data (com período);
 *   - 2 dimensões e 1 métrica: colunas empilhadas (só contagem ou soma: empilhar médias não soma nada), linhas se a primeira
 *     é de data, ou tabela; com mais de uma métrica, só tabela.
 */

export const NOMES_VISUALIZACAO: Record<TipoVisualizacao, string> = {
  kpi: 'Cartões (KPI)',
  colunas: 'Colunas',
  'colunas-empilhadas': 'Colunas empilhadas',
  'barras-horizontais': 'Barras horizontais',
  linhas: 'Linhas',
  tabela: 'Tabela',
};

/** "data_execucao:mes", "relatorio_teste.data_execucao:ano", "criadoEm:dia". */
export function ehDimensaoDeData(chave: string): boolean {
  return /:(dia|mes|trimestre|ano)$/.test(chave);
}

/** Métricas que se somam (as que podem ser empilhadas): contagem e soma. */
function ehAditiva(metrica: string): boolean {
  return metrica === 'contagem' || metrica.startsWith('soma:');
}

export function visualizacoesPermitidas(agruparPor: string[], metricas: string[]): TipoVisualizacao[] {
  const efetivas = metricas.length === 0 ? ['contagem'] : metricas;
  if (agruparPor.length === 0) {
    return ['kpi', 'tabela'];
  }
  if (agruparPor.length === 1) {
    return ehDimensaoDeData(agruparPor[0])
      ? ['colunas', 'linhas', 'barras-horizontais', 'tabela']
      : ['colunas', 'barras-horizontais', 'tabela'];
  }
  if (efetivas.length > 1) {
    return ['tabela'];
  }
  const permitidas: TipoVisualizacao[] = [];
  if (ehAditiva(efetivas[0])) {
    permitidas.push('colunas-empilhadas');
  }
  if (ehDimensaoDeData(agruparPor[0])) {
    permitidas.push('linhas');
  }
  permitidas.push('tabela');
  return permitidas;
}
