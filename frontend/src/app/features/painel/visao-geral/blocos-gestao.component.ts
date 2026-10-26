import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { formatarNumero } from '../graficos/formatacao';
import { CartaoGraficoComponent } from './cartao-grafico.component';
import { CriterioAvaliado, ResumoAprovacao } from './visao-geral';

/**
 * Bloco de APROVAÇÃO da visão geral (RN82). Só é desenhado quando a API traz dados de aprovação (entidades com aprovação ativa);
 * para Testes de Desempenho hoje não traz. Testado com dados simulados (blocos-gestao.component.spec.ts).
 */
@Component({
  selector: 'app-bloco-aprovacao',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="secao-painel" aria-labelledby="bloco-aprovacao-titulo">
      <h2 id="bloco-aprovacao-titulo">Aprovação</h2>
      <ul class="faixa-kpis" aria-label="Números de aprovação">
        @for (cartao of resumo().cartoes; track cartao.chave) {
          <li class="cartao-kpi">
            <span class="kpi-rotulo">{{ cartao.rotulo }}</span>
            <strong class="kpi-valor">{{ formatarNumero(cartao.valor, cartao.unidade) }}</strong>
          </li>
        }
      </ul>
      <div class="cartao-painel largura-total">
        <div class="tabela-rolagem">
          <table class="tabela-dados">
            <caption class="sr-only">
              Aprovação por tipo de item
            </caption>
            <thead>
              <tr>
                <th scope="col">Tipo de item</th>
                <th scope="col" class="coluna-numero">Pendentes</th>
                <th scope="col" class="coluna-numero">Aprovados</th>
                <th scope="col" class="coluna-numero">Rejeitados</th>
                <th scope="col" class="coluna-numero">Taxa de rejeição (%)</th>
              </tr>
            </thead>
            <tbody>
              @for (linha of resumo().porEntidade; track linha.nome) {
                <tr>
                  <td>{{ linha.nome }}</td>
                  <td class="coluna-numero">{{ formatarNumero(linha.pendentes) }}</td>
                  <td class="coluna-numero">{{ formatarNumero(linha.aprovados) }}</td>
                  <td class="coluna-numero">{{ formatarNumero(linha.rejeitados) }}</td>
                  <td class="coluna-numero">{{ formatarNumero(linha.taxaRejeicao) }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </section>
  `,
})
export class BlocoAprovacaoComponent {
  readonly resumo = input.required<ResumoAprovacao>();
  readonly formatarNumero = formatarNumero;
}

/**
 * Bloco de AVALIAÇÃO da visão geral (RN83): para cada critério de estrelas, a média, a distribuição das notas e o ranking.
 * Só é desenhado quando a API traz dados de avaliação. Testado com dados simulados.
 */
@Component({
  selector: 'app-bloco-avaliacao',
  imports: [CartaoGraficoComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="secao-painel" aria-labelledby="bloco-avaliacao-titulo">
      <h2 id="bloco-avaliacao-titulo">Avaliação</h2>
      <div class="grade-painel">
        @for (criterio of criterios(); track criterio.chave) {
          <app-cartao-grafico
            [id]="'avaliacao-' + criterio.chave"
            [titulo]="
              criterio.titulo +
              ': média ' +
              formatarNumero(criterio.media) +
              ' de 5 (' +
              criterio.quantidade +
              ' notas)'
            "
            descricao="Quantidade de notas de cada valor, de 1 a 5 estrelas."
            [indicadores]="criterio.distribuicao"
          />
          <div class="cartao-painel">
            <h3>{{ criterio.titulo }}: ranking</h3>
            <p class="texto-secundario">Só itens com pelo menos {{ minimo() }} notas.</p>
            <div class="rankings">
              <div>
                <h4>Melhores</h4>
                <ol>
                  @for (item of criterio.melhores; track $index) {
                    <li>{{ item.rotulo }}: {{ formatarNumero(item.media) }} ({{ item.quantidade }} notas)</li>
                  } @empty {
                    <li class="texto-secundario">Nenhum item com notas suficientes.</li>
                  }
                </ol>
              </div>
              <div>
                <h4>Piores</h4>
                <ol>
                  @for (item of criterio.piores; track $index) {
                    <li>{{ item.rotulo }}: {{ formatarNumero(item.media) }} ({{ item.quantidade }} notas)</li>
                  } @empty {
                    <li class="texto-secundario">Nenhum item com notas suficientes.</li>
                  }
                </ol>
              </div>
            </div>
          </div>
        }
      </div>
    </section>
  `,
})
export class BlocoAvaliacaoComponent {
  readonly criterios = input.required<CriterioAvaliado[]>();
  readonly minimo = input(3);
  readonly formatarNumero = formatarNumero;
}
