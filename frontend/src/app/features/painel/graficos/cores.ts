import { Injectable } from '@angular/core';
import { PALETA_GRAFICOS } from '../../../tema/tema';
import { SerieGrafico } from './transformar-dados';

/**
 * Cores CATEGÓRICAS estáveis. As séries têm uma FAMÍLIA (as métricas, ou os valores de uma dimensão, ex.: os status) e cada
 * identidade recebe a próxima cor da paleta DA SUA FAMÍLIA na primeira vez que aparece, e fica com ela em TODOS os gráficos da
 * página. A cor segue a entidade, nunca a posição; dentro de uma família as cores não são recicladas (acabou a paleta: cor
 * neutra, como "Outros"). Num gráfico as séries são de uma família só, então nunca repetem cor. Classe pura (cores.spec.ts).
 */
export class PaletaCategorica {
  private readonly familias = new Map<string, Map<string, number>>();

  constructor(readonly tamanho: number) {}

  /** A posição da cor da chave na família (0 a tamanho-1), atribuída na primeira chamada; nulo se a paleta acabou. */
  indice(chave: string, familia = ''): number | null {
    let indices = this.familias.get(familia);
    if (!indices) {
      indices = new Map();
      this.familias.set(familia, indices);
    }
    let indice = indices.get(chave);
    if (indice === undefined) {
      if (indices.size >= this.tamanho) {
        return null;
      }
      indice = indices.size;
      indices.set(chave, indice);
    }
    return indice;
  }

  /** Reserva cores na ordem dada (ex.: as métricas dos widgets, na ordem do arquivo de configuração). */
  registrar(chaves: string[], familia = ''): void {
    chaves.forEach((chave) => this.indice(chave, familia));
  }
}

/** As cores do tema que os gráficos usam, já resolvidas (texto, grade, superfície). */
export interface TemaGrafico {
  texto: string;
  textoSecundario: string;
  grade: string;
  superficie: string;
  /** A cor de um gráfico com uma série só: a 1ª da paleta do tema (o Azul CAIXA, ou o tom dele para o fundo escuro). */
  corSerieUnica: string;
  /** O usuário pediu menos movimento (prefers-reduced-motion): gráficos sem animação. */
  reduzirMovimento: boolean;
}

/**
 * As cores dos gráficos de UMA página: a paleta (variáveis do tema, src/app/tema/tema.ts) lida em tempo de execução, então
 * os gráficos seguem o tema claro ou escuro (.catalogo-escuro). Fornecido no componente da página (providers), não na raiz:
 * cada página tem a sua atribuição de cores.
 */
@Injectable()
export class CoresGraficoService {
  private readonly paleta = new PaletaCategorica(PALETA_GRAFICOS.claro.length);

  registrar(chaves: string[], familia: string): void {
    this.paleta.registrar(chaves, familia);
  }

  corDaSerie(serie: SerieGrafico): string {
    const indice = serie.neutra ? null : this.paleta.indice(serie.chave, serie.familia);
    if (indice === null) {
      return lerVariavel(PALETA_GRAFICOS.neutra, '#9ca3af');
    }
    const passos = escuro() ? PALETA_GRAFICOS.escuro : PALETA_GRAFICOS.claro;
    return lerVariavel(passos[indice], '#2563eb');
  }

  tema(): TemaGrafico {
    return {
      texto: lerVariavel(PALETA_GRAFICOS.texto, '#334155'),
      textoSecundario: lerVariavel(PALETA_GRAFICOS.textoSecundario, '#64748b'),
      grade: lerVariavel(PALETA_GRAFICOS.grade, '#e2e8f0'),
      superficie: lerVariavel(PALETA_GRAFICOS.superficie, '#ffffff'),
      corSerieUnica: lerVariavel((escuro() ? PALETA_GRAFICOS.escuro : PALETA_GRAFICOS.claro)[0], '#0066b3'),
      reduzirMovimento: window.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false,
    };
  }
}

function escuro(): boolean {
  return document.documentElement.classList.contains('catalogo-escuro');
}

function lerVariavel(nome: string, padrao: string): string {
  return getComputedStyle(document.documentElement).getPropertyValue(nome).trim() || padrao;
}
