import { ChangeDetectionStrategy, Component, computed, input, signal } from '@angular/core';
import { Button } from 'primeng/button';
import { Indicadores } from '../../../core/painel/modelo-painel';
import { VisualizacaoComponent } from '../graficos/visualizacao.component';
import { semDados } from '../graficos/transformar-dados';
import { TipoVisualizacao } from '../modelo/configuracao';

/** Um gráfico da visão geral num cartão, com título, descrição acessível e "Ver dados" (mesmas regras dos widgets). */
@Component({
  selector: 'app-cartao-grafico',
  imports: [Button, VisualizacaoComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'cartao-painel', '[class.largura-total]': 'larguraTotal()' },
  template: `
    <header class="cabecalho-cartao">
      <div class="titulo-cartao">
        <h3 [id]="idTitulo()">{{ titulo() }}</h3>
      </div>
      @if (!vazio()) {
        <div class="acoes-cartao">
          <p-button
            size="small"
            severity="secondary"
            [text]="true"
            [label]="verDados() ? 'Ver gráfico' : 'Ver dados'"
            [icon]="verDados() ? 'pi pi-chart-bar' : 'pi pi-table'"
            [attr.aria-pressed]="verDados()"
            (onClick)="verDados.set(!verDados())"
          />
        </div>
      }
    </header>
    <p class="sr-only" [id]="idDescricao()">{{ descricao() }}</p>
    @if (vazio()) {
      <p class="estado-cartao">Sem dados no período.</p>
    } @else {
      <app-visualizacao
        [indicadores]="indicadores()"
        [visualizacao]="visualizacao()"
        [verDados]="verDados()"
        [titulo]="titulo()"
        [rotuladoPor]="idTitulo() + ' ' + idDescricao()"
      />
    }
  `,
})
export class CartaoGraficoComponent {
  readonly id = input.required<string>();
  readonly titulo = input.required<string>();
  readonly descricao = input.required<string>();
  readonly indicadores = input.required<Indicadores>();
  readonly visualizacao = input<TipoVisualizacao>('colunas');
  readonly larguraTotal = input(false);

  readonly verDados = signal(false);
  readonly idTitulo = computed(() => `${this.id()}-titulo`);
  readonly idDescricao = computed(() => `${this.id()}-descricao`);
  readonly vazio = computed(() => semDados(this.indicadores()));
}
