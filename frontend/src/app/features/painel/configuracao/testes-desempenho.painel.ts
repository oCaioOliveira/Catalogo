import { ConfiguracaoPainel } from '../modelo/configuracao';

/**
 * Widgets pré-configurados do grupo Testes de Desempenho (docs/frontend.md → "Painel gerencial", ADR 0039). As medidas ficam no
 * CENÁRIO; as dimensões de gerência (ambiente, projeto, status, tipo, data de execução), no RELATÓRIO: os widgets de cenário
 * usam caminhos `relatorio_teste.<campo>` (RN89). O período do painel vai para a DATA DE EXECUÇÃO (`campoPeriodo`), não para a
 * data de criação.
 *
 * Para acrescentar um widget: monte-o na análise personalizada (/painel/testes_desempenho/analise), clique em "Copiar
 * configuração" e cole o objeto na lista abaixo, ajustando o id, o título (com a unidade) e a descrição.
 */
export const PAINEL_TESTES_DESEMPENHO: ConfiguracaoPainel = {
  grupo: 'testes_desempenho',
  entidadePrincipal: 'relatorio_teste',
  widgets: [
    {
      id: 'medias-gerais',
      titulo: 'Médias gerais dos cenários no período',
      descricao:
        'Média de vazão (TPS), de percentil 95 (ms) e de taxa de erro (%) de todos os cenários executados no período.',
      entidade: 'cenario_teste',
      agruparPor: [],
      metricas: ['media:vazao_media_tps', 'media:percentil_95_ms', 'media:taxa_erro_percentual'],
      campoPeriodo: 'relatorio_teste.data_execucao',
      visualizacao: 'kpi',
    },
    {
      id: 'relatorios-por-mes-e-status',
      titulo: 'Relatórios executados por mês e status',
      descricao: 'Quantidade de relatórios por mês de execução, empilhados pelo status do resultado.',
      entidade: 'relatorio_teste',
      agruparPor: ['data_execucao:mes', 'status_resultado'],
      metricas: ['contagem'],
      campoPeriodo: 'data_execucao',
      visualizacao: 'colunas-empilhadas',
      limite: 500,
    },
    {
      id: 'relatorios-por-tipo',
      titulo: 'Relatórios por tipo de teste',
      descricao:
        'Quantidade de relatórios executados no período, por tipo de teste (do mais frequente ao menos frequente).',
      entidade: 'relatorio_teste',
      agruparPor: ['tipo_teste'],
      metricas: ['contagem'],
      campoPeriodo: 'data_execucao',
      visualizacao: 'barras-horizontais',
      ordenarPor: '-contagem',
    },
    {
      id: 'vazao-por-ambiente',
      titulo: 'Vazão média (TPS) por ambiente',
      descricao: 'Média da vazão dos cenários, pelo ambiente do relatório em que foram executados.',
      entidade: 'cenario_teste',
      agruparPor: ['relatorio_teste.ambiente'],
      metricas: ['media:vazao_media_tps'],
      campoPeriodo: 'relatorio_teste.data_execucao',
      visualizacao: 'colunas',
    },
    {
      id: 'evolucao-tempos-de-resposta',
      titulo: 'Evolução do percentil 95 e do tempo médio de resposta (ms)',
      descricao:
        'Média mensal do percentil 95 e do tempo médio de resposta dos cenários, pelo mês de execução do relatório.',
      entidade: 'cenario_teste',
      agruparPor: ['relatorio_teste.data_execucao:mes'],
      metricas: ['media:percentil_95_ms', 'media:tempo_medio_resposta_ms'],
      campoPeriodo: 'relatorio_teste.data_execucao',
      visualizacao: 'linhas',
    },
    {
      id: 'taxa-de-erro-por-projeto',
      titulo: 'Taxa de erro média (%) por projeto',
      descricao: 'Média da taxa de erro dos cenários por projeto do relatório, das maiores para as menores.',
      entidade: 'cenario_teste',
      agruparPor: ['relatorio_teste.projeto'],
      metricas: ['media:taxa_erro_percentual'],
      campoPeriodo: 'relatorio_teste.data_execucao',
      visualizacao: 'barras-horizontais',
      ordenarPor: '-media:taxa_erro_percentual',
    },
    {
      id: 'endpoints-maior-percentil-95',
      titulo: 'Top 10 endpoints com maior percentil 95 (ms)',
      descricao:
        'Os 10 endpoints com maior média de percentil 95 nos cenários do período, com o máximo e o número de cenários.',
      entidade: 'cenario_teste',
      agruparPor: ['endpoint_servico'],
      metricas: ['media:percentil_95_ms', 'maximo:percentil_95_ms', 'contagem'],
      campoPeriodo: 'relatorio_teste.data_execucao',
      visualizacao: 'tabela',
      ordenarPor: '-media:percentil_95_ms',
      limite: 10,
    },
  ],
};
