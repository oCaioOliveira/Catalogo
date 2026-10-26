import { Indicadores, MetricaIndicador } from '../../../core/painel/modelo-painel';
import { ROTULO_SEM_VALOR, rotuloDoValorDimensao } from './formatacao';

/**
 * Resposta de GET .../indicadores → dados de gráfico, de tabela ("Ver dados") e de cartões KPI. Funções PURAS, sem Angular
 * nem Chart.js (transformar-dados.spec.ts); o componente só as chama.
 *
 *   sem dimensão  → uma série por métrica, um ponto ("Total"), ou um cartão por métrica (KPI);
 *   1 dimensão    → um rótulo por linha (na ordem da API) e uma série por métrica;
 *   2 dimensões   → rótulos = valores da 1ª dimensão; séries = valores da 2ª (na ordem em que aparecem), com a 1ª métrica.
 *                   Mais de 8 séries: as 7 primeiras e "Outros" com a soma das demais (só contagem e soma são empilhadas).
 *
 * Cada série tem uma `chave` estável (a da métrica, ou o valor da 2ª dimensão): é por ela que a cor é escolhida, então a mesma
 * categoria tem a mesma cor em todos os gráficos da página (cores.ts).
 */

export const MAXIMO_SERIES = 8;
export const ROTULO_OUTROS = 'Outros';

export interface SerieGrafico {
  /** Identidade da série, para a cor: "metrica:media:vazao_media_tps" ou "valor:Aprovado". */
  chave: string;
  /**
   * A família da cor: "metricas", ou o campo da 2ª dimensão ("status_resultado", sem caminho nem período). As cores são
   * atribuídas em ordem fixa DENTRO de cada família: num gráfico as séries são de uma família só, então nunca repetem cor.
   */
  familia: string;
  rotulo: string;
  valores: (number | null)[];
  unidade: string | null;
  /** "Outros" e "(sem valor)": cor neutra, não gasta uma cor da paleta. */
  neutra: boolean;
}

export interface DadosGrafico {
  rotulos: string[];
  series: SerieGrafico[];
  /** Título do eixo de valores: a unidade comum às séries, "Quantidade" (só contagens) ou nulo. */
  tituloEixo: string | null;
}

export interface ColunaTabela {
  rotulo: string;
  numerica: boolean;
  unidade: string | null;
}

export interface TabelaDados {
  colunas: ColunaTabela[];
  linhas: (string | number | null)[][];
}

export interface CartaoKpi {
  chave: string;
  rotulo: string;
  valor: number | null;
  unidade: string | null;
}

/** Unidade de cada métrica, pela chave ("media:percentil_95_ms" → "ms"). Ausente = sem unidade. */
export type Unidades = Record<string, string | null>;

export const FAMILIA_METRICAS = 'metricas';

/** "relatorio_teste.status_resultado" e "status_resultado" são a mesma família de cores; o período não conta. */
export function familiaDaDimensao(chave: string): string {
  return chave.split(':')[0].split('.').pop() ?? chave;
}

const CONTAGEM: MetricaIndicador = { chave: 'contagem', nome: 'Contagem' };

function metricas(indicadores: Indicadores): MetricaIndicador[] {
  return indicadores.metricas.length > 0 ? indicadores.metricas : [CONTAGEM];
}

function numero(valor: unknown): number | null {
  if (valor === null || valor === undefined || valor === '') {
    return null;
  }
  const n = Number(valor);
  return Number.isFinite(n) ? n : null;
}

function serieNeutra(rotulo: string): boolean {
  return rotulo === ROTULO_OUTROS || rotulo === ROTULO_SEM_VALOR;
}

/** Sem linhas, ou (sem dimensão) a linha do total com contagem 0. */
export function semDados(indicadores: Indicadores): boolean {
  return (
    indicadores.linhas.length === 0 ||
    (indicadores.dimensoes.length === 0 && indicadores.linhas.every((l) => numero(l['contagem']) === 0))
  );
}

export function montarKpis(indicadores: Indicadores, unidades: Unidades = {}): CartaoKpi[] {
  const linha = indicadores.linhas[0] ?? {};
  return metricas(indicadores).map((m) => ({
    chave: m.chave,
    rotulo: m.nome,
    valor: numero(linha[m.chave]),
    unidade: unidades[m.chave] ?? null,
  }));
}

export function montarDadosGrafico(indicadores: Indicadores, unidades: Unidades = {}): DadosGrafico {
  const [dimensao1, dimensao2] = indicadores.dimensoes;
  const todas = metricas(indicadores);

  if (dimensao2) {
    const metrica = todas[0];
    const rotulos: string[] = [];
    const series = new Map<string, Map<string, number | null>>();
    for (const linha of indicadores.linhas) {
      const x = rotuloDoValorDimensao(dimensao1, linha[dimensao1.chave]);
      const s = rotuloDoValorDimensao(dimensao2, linha[dimensao2.chave]);
      if (!rotulos.includes(x)) {
        rotulos.push(x);
      }
      if (!series.has(s)) {
        series.set(s, new Map());
      }
      series.get(s)!.set(x, numero(linha[metrica.chave]));
    }
    const familia = familiaDaDimensao(dimensao2.chave);
    let lista = [...series.entries()].map(([rotulo, valores]) => ({
      chave: `valor:${rotulo}`,
      familia,
      rotulo,
      valores: rotulos.map((x) => valores.get(x) ?? null),
      unidade: unidades[metrica.chave] ?? null,
      neutra: serieNeutra(rotulo),
    }));
    if (lista.length > MAXIMO_SERIES) {
      const resto = lista.slice(MAXIMO_SERIES - 1);
      lista = [
        ...lista.slice(0, MAXIMO_SERIES - 1),
        {
          chave: `valor:${ROTULO_OUTROS}`,
          familia,
          rotulo: ROTULO_OUTROS,
          valores: rotulos.map((_, i) => resto.reduce((soma, s) => soma + (s.valores[i] ?? 0), 0)),
          unidade: unidades[metrica.chave] ?? null,
          neutra: true,
        },
      ];
    }
    return { rotulos, series: lista, tituloEixo: tituloEixo([metrica], unidades) };
  }

  const rotulos = dimensao1
    ? indicadores.linhas.map((l) => rotuloDoValorDimensao(dimensao1, l[dimensao1.chave]))
    : ['Total'];
  const series = todas.map((m) => ({
    chave: `metrica:${m.chave}`,
    familia: FAMILIA_METRICAS,
    rotulo: m.nome,
    valores: indicadores.linhas.map((l) => numero(l[m.chave])),
    unidade: unidades[m.chave] ?? null,
    neutra: false,
  }));
  return { rotulos, series, tituloEixo: tituloEixo(todas, unidades) };
}

function tituloEixo(lista: MetricaIndicador[], unidades: Unidades): string | null {
  if (lista.every((m) => m.chave === 'contagem')) {
    return 'Quantidade';
  }
  const comuns = new Set(lista.map((m) => unidades[m.chave] ?? null));
  const [unica] = [...comuns];
  return comuns.size === 1 && unica ? unica : null;
}

/** A tabela de "Ver dados": as dimensões, as métricas e a contagem (que vem em toda linha), com os mesmos números. */
export function montarTabela(indicadores: Indicadores, unidades: Unidades = {}): TabelaDados {
  const lista = metricas(indicadores);
  const colunasMetricas = lista.some((m) => m.chave === 'contagem') ? lista : [...lista, CONTAGEM];
  return {
    colunas: [
      ...indicadores.dimensoes.map((d) => ({ rotulo: d.nome, numerica: false, unidade: null })),
      ...colunasMetricas.map((m) => ({ rotulo: m.nome, numerica: true, unidade: unidades[m.chave] ?? null })),
    ],
    linhas: indicadores.linhas.map((linha) => [
      ...indicadores.dimensoes.map((d) => rotuloDoValorDimensao(d, linha[d.chave])),
      ...colunasMetricas.map((m) => numero(linha[m.chave])),
    ]),
  };
}
