import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { UIChart } from 'primeng/chart';
import { Indicadores } from '../../../core/painel/modelo-painel';
import { TipoVisualizacao } from '../modelo/configuracao';
import { CoresGraficoService } from './cores';
import { formatarNumero } from './formatacao';
import { configurarGrafico } from './opcoes-grafico';
import { Unidades, montarDadosGrafico, montarKpis, montarTabela } from './transformar-dados';
import { TemaService } from '../../../tema/tema.service';

/**
 * Desenha um resultado de indicadores: cartões (KPI), tabela ou gráfico (p-chart, o componente de gráficos do PrimeNG, sobre o
 * Chart.js). Só apresentação: a transformação é das funções puras de transformar-dados.ts e opcoes-grafico.ts. Com `verDados`,
 * o gráfico vira a tabela com os mesmos números (acessibilidade). O canvas é rotulado pelo título e pela descrição do cartão
 * (`rotuladoPor`, ids separados por espaço).
 */
@Component({
  selector: 'app-visualizacao',
  imports: [UIChart],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (modo()) {
      @case ('kpi') {
        <ul class="faixa-kpis" [attr.aria-label]="titulo()">
          @for (cartao of kpis(); track cartao.chave) {
            <li class="cartao-kpi">
              <span class="kpi-rotulo">{{ cartao.rotulo }}</span>
              <strong class="kpi-valor">{{ formatarNumero(cartao.valor, cartao.unidade) }}</strong>
            </li>
          }
        </ul>
      }
      @case ('tabela') {
        <div class="tabela-rolagem">
          <table class="tabela-dados">
            <caption class="sr-only">
              {{
                titulo()
              }}
            </caption>
            <thead>
              <tr>
                @for (coluna of tabela().colunas; track $index) {
                  <th scope="col" [class.coluna-numero]="coluna.numerica">{{ coluna.rotulo }}</th>
                }
              </tr>
            </thead>
            <tbody>
              @for (linha of tabela().linhas; track $index) {
                <tr>
                  @for (celula of linha; track $index) {
                    <td [class.coluna-numero]="tabela().colunas[$index].numerica">{{ celulaComoTexto(celula) }}</td>
                  }
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
      @default {
        <div class="area-grafico" [style.height]="altura()">
          <p-chart
            height="100%"
            [type]="grafico().tipo"
            [data]="grafico().data"
            [options]="grafico().opcoes"
            [ariaLabelledBy]="rotuladoPor()"
          />
        </div>
      }
    }
  `,
})
export class VisualizacaoComponent {
  readonly indicadores = input.required<Indicadores>();
  readonly visualizacao = input.required<TipoVisualizacao>();
  readonly verDados = input(false);
  readonly unidades = input<Unidades>({});
  readonly titulo = input('');
  readonly rotuladoPor = input<string | undefined>(undefined);

  private readonly cores = inject(CoresGraficoService, { optional: true }) ?? new CoresGraficoService();
  /** O tema aplicado (claro ou escuro): trocar o tema redesenha o gráfico com as cores dele. */
  private readonly temaAplicado = inject(TemaService).aplicado;

  readonly modo = computed<'kpi' | 'tabela' | 'grafico'>(() => {
    const visualizacao = this.visualizacao();
    if (visualizacao === 'kpi') {
      return 'kpi';
    }
    return visualizacao === 'tabela' || this.verDados() ? 'tabela' : 'grafico';
  });
  readonly kpis = computed(() => montarKpis(this.indicadores(), this.unidades()));
  readonly tabela = computed(() => montarTabela(this.indicadores(), this.unidades()));
  readonly grafico = computed(() => {
    this.temaAplicado();
    return configurarGrafico(
      montarDadosGrafico(this.indicadores(), this.unidades()),
      this.visualizacao(),
      (serie) => this.cores.corDaSerie(serie),
      this.cores.tema(),
    );
  });
  /** Barras horizontais crescem com o número de categorias (cada barra com espaço para o rótulo). */
  readonly altura = computed(() =>
    this.visualizacao() === 'barras-horizontais'
      ? `${Math.max(16, this.indicadores().linhas.length * 2 + 4)}rem`
      : '18rem',
  );

  readonly formatarNumero = formatarNumero;

  celulaComoTexto(celula: string | number | null): string {
    return typeof celula === 'number' || celula === null ? formatarNumero(celula) : celula;
  }
}
