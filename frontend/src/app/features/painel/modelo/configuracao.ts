/**
 * O formato de um WIDGET do painel (docs/frontend.md → "Painel gerencial"). Widgets são CONFIGURAÇÃO, não componentes: um
 * único componente (widget/widget.component.ts) recebe este objeto, consulta a API de indicadores e desenha o resultado. A
 * análise personalizada usa o MESMO formato, e o "Copiar configuração" dela devolve um destes objetos em JSON: adicionar um
 * widget pré-configurado é colar esse JSON no arquivo do grupo (configuracao/<grupo>.painel.ts). ADR 0039.
 */

/** Como o resultado é mostrado. Nem toda combinação de dimensões e métricas aceita todos (visualizacoes.ts). */
export type TipoVisualizacao = 'kpi' | 'colunas' | 'colunas-empilhadas' | 'barras-horizontais' | 'linhas' | 'tabela';

export interface ConfiguracaoWidget {
  /** Identificador estável (kebab-case), único no painel. */
  id: string;
  /** Título, com a unidade quando houver: "Vazão média (TPS) por ambiente". */
  titulo: string;
  /** Uma frase sobre o que o widget mostra (aparece como dica e é a descrição acessível do gráfico). */
  descricao?: string;
  /** Chave da entidade consultada (GET .../entidades/{entidade}/indicadores). */
  entidade: string;
  /** Até 2 dimensões, no formato da API: "tipo_teste", "relatorio_teste.ambiente", "data_execucao:mes". */
  agruparPor: string[];
  /** Até 5 métricas: "contagem", "media:vazao_media_tps", "maximo:relatorio_teste.x"... Vazio = contagem. */
  metricas: string[];
  /** Filtros FIXOS, no formato da API (os da listagem, também por caminho): { "relatorio_teste.tipo_teste": "Carga" }. */
  filtros?: Record<string, string>;
  /**
   * O campo DATA ou DATA_HORA (também por caminho) que recebe o PERÍODO do painel, como `<campo>.de` e `<campo>.ate`. Nulo =
   * a data de criação dos registros (os parâmetros `de` e `ate` da API).
   */
  campoPeriodo: string | null;
  visualizacao: TipoVisualizacao;
  /** Chave de uma dimensão ou métrica; "-" na frente = decrescente. */
  ordenarPor?: string;
  /** Máximo de linhas (1 a 500; a API usa 100 sem ele). */
  limite?: number;
  /** Colunas que ocupa no grid de gráficos: 1 (metade, o padrão) ou 2 (a largura toda). KPIs sempre ocupam a faixa toda. */
  largura?: 1 | 2;
  /** Unidade de uma métrica, quando o nome do campo não a traz entre parênteses: { "soma:duracao": "min" }. */
  unidades?: Record<string, string>;
}

/** Os widgets pré-configurados de um grupo. */
export interface ConfiguracaoPainel {
  grupo: string;
  /** A entidade dos cartões "Total" e "Criados no período" da visão geral. Sem ela, a primeira entidade principal do menu. */
  entidadePrincipal?: string;
  widgets: ConfiguracaoWidget[];
}
