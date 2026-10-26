import { ChartData, ChartOptions } from 'chart.js';
import { TipoVisualizacao } from '../modelo/configuracao';
import { formatarNumero } from './formatacao';
import { DadosGrafico, SerieGrafico } from './transformar-dados';
import { TemaGrafico } from './cores';

/**
 * Dados do gráfico → configuração do Chart.js (usada pelo p-chart do PrimeNG). As regras visuais do painel ficam AQUI, num
 * lugar só (opcoes-grafico.spec.ts):
 *   - eixos de colunas e barras começam em zero; um eixo de valores só (nunca dois);
 *   - legenda só com mais de uma série; textos nas cores de texto do tema, nunca na cor da série;
 *   - números pt-BR com a unidade nos eixos e nas dicas; só valores inteiros: marcas inteiras no eixo; grade discreta;
 *   - sem animação para quem pediu menos movimento (prefers-reduced-motion);
 *   - marcas finas: barras com cantos de 4px e no máximo 36px de espessura, 2px de separação entre partes empilhadas,
 *     linhas de 2px com pontos de 8px.
 */

export type TipoGrafico = 'bar' | 'line';

export interface ConfiguracaoGrafico {
  tipo: TipoGrafico;
  data: ChartData;
  opcoes: ChartOptions;
}

export function configurarGrafico(
  dados: DadosGrafico,
  visualizacao: TipoVisualizacao,
  corDaSerie: (serie: SerieGrafico) => string,
  tema: TemaGrafico,
): ConfiguracaoGrafico {
  const linhas = visualizacao === 'linhas';
  const horizontal = visualizacao === 'barras-horizontais';
  const empilhado = visualizacao === 'colunas-empilhadas';
  const unidadePorSerie = new Map(dados.series.map((s) => [s.rotulo, s.unidade]));
  const eixoUnidade = dados.series.every((s) => s.unidade === dados.series[0]?.unidade)
    ? (dados.series[0]?.unidade ?? null)
    : null;

  // Uma série só (e não "Outros"): a cor primária da marca (Azul CAIXA), não a cor que a métrica reservou na paleta pela posição no
  // arquivo de configuração. Com várias séries, as cores estáveis da paleta (a mesma categoria, a mesma cor em todo o painel).
  const serieUnica = dados.series.length === 1 && !dados.series[0].neutra;
  const datasets = dados.series.map((serie) => {
    const cor = serieUnica ? tema.corSerieUnica : corDaSerie(serie);
    return linhas
      ? {
          label: serie.rotulo,
          data: serie.valores,
          borderColor: cor,
          backgroundColor: cor,
          borderWidth: 2,
          pointRadius: 4,
          pointHoverRadius: 6,
          pointBorderColor: tema.superficie,
          pointBorderWidth: 2,
          tension: 0,
          spanGaps: true,
        }
      : {
          label: serie.rotulo,
          data: serie.valores,
          backgroundColor: cor,
          borderColor: tema.superficie,
          borderWidth: empilhado ? 1 : 0,
          borderRadius: 4,
          maxBarThickness: 36,
        };
  });

  const inteiros = dados.series.every((s) => s.valores.every((v) => v === null || Number.isInteger(v)));
  const eixoValores = {
    beginAtZero: true,
    stacked: empilhado,
    title: { display: dados.tituloEixo !== null, text: dados.tituloEixo ?? '', color: tema.textoSecundario },
    ticks: {
      color: tema.textoSecundario,
      precision: inteiros ? 0 : undefined,
      callback: (valor: string | number) => formatarNumero(Number(valor), eixoUnidade),
    },
    grid: { color: tema.grade },
    border: { display: false },
  };
  const eixoCategorias = {
    stacked: empilhado,
    ticks: { color: tema.textoSecundario, autoSkip: true, maxRotation: 45 },
    grid: { display: false },
    border: { color: tema.grade },
  };

  return {
    tipo: linhas ? 'line' : 'bar',
    data: { labels: dados.rotulos, datasets } as ChartData,
    opcoes: {
      responsive: true,
      maintainAspectRatio: false,
      animation: tema.reduzirMovimento ? false : undefined,
      indexAxis: horizontal ? 'y' : 'x',
      interaction: { mode: linhas ? 'index' : 'nearest', intersect: false },
      scales: horizontal ? { x: eixoValores, y: eixoCategorias } : { x: eixoCategorias, y: eixoValores },
      plugins: {
        legend: {
          display: dados.series.length > 1,
          position: 'bottom',
          labels: { color: tema.texto, usePointStyle: true, boxWidth: 8, boxHeight: 8 },
        },
        tooltip: {
          callbacks: {
            label: (contexto) => {
              const rotulo = contexto.dataset.label ?? '';
              const valor = (horizontal ? contexto.parsed.x : contexto.parsed.y) as number | null;
              return `${rotulo}: ${formatarNumero(valor, unidadePorSerie.get(rotulo) ?? null)}`;
            },
          },
        },
      },
    } as ChartOptions,
  };
}
