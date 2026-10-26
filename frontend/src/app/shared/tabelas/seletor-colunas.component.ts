import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Button } from 'primeng/button';
import { Popover } from 'primeng/popover';
import {
  ColunaTabela,
  alternarColuna,
  colunasVisiveis,
  moverColuna,
  ordemDoSeletor,
  textoColunasVisiveis,
} from './colunas-tabela';

/**
 * O seletor de colunas: o botão mostra "6 de 9 colunas" e abre a lista para mostrar, esconder e REORDENAR (↑ e ↓, pelo mouse ou
 * pelo teclado). A coluna fixa (o campo de exibição) aparece marcada e travada. "Restaurar o padrão" volta às colunas que cabem.
 */
@Component({
  selector: 'app-seletor-colunas',
  imports: [Button, Popover],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-button
      icon="pi pi-table"
      severity="secondary"
      [outlined]="true"
      [label]="texto()"
      [ariaLabel]="'Colunas visíveis: ' + texto()"
      aria-haspopup="dialog"
      (onClick)="painel.toggle($event)"
    />
    <p-popover #painel styleClass="popover-colunas">
      <div class="seletor-colunas" role="dialog" aria-labelledby="seletor-colunas-titulo">
        <p id="seletor-colunas-titulo" class="seletor-colunas-titulo">Colunas visíveis</p>
        <ul>
          @for (coluna of ordem(); track coluna.chave; let i = $index) {
            <li [class.escondida]="!visivel(coluna)">
              <label>
                <input
                  type="checkbox"
                  [checked]="visivel(coluna)"
                  [disabled]="coluna.fixa"
                  (change)="alternar(coluna, $any($event.target).checked)"
                />
                {{ coluna.nome }}
                @if (coluna.fixa) {
                  <small>(sempre visível)</small>
                }
              </label>
              @if (visivel(coluna) && !coluna.fixa) {
                <span class="mover-coluna">
                  <p-button
                    icon="pi pi-arrow-up"
                    size="small"
                    severity="secondary"
                    [text]="true"
                    [disabled]="posicao(coluna) === 0"
                    [ariaLabel]="'Mover ' + coluna.nome + ' para a esquerda'"
                    (onClick)="mover(coluna, -1)"
                  />
                  <p-button
                    icon="pi pi-arrow-down"
                    size="small"
                    severity="secondary"
                    [text]="true"
                    [disabled]="posicao(coluna) === escolhidas().length - 1"
                    [ariaLabel]="'Mover ' + coluna.nome + ' para a direita'"
                    (onClick)="mover(coluna, 1)"
                  />
                </span>
              }
            </li>
          }
        </ul>
        <p-button
          label="Restaurar o padrão"
          icon="pi pi-replay"
          size="small"
          severity="secondary"
          [text]="true"
          (onClick)="restaurar.emit()"
        />
      </div>
    </p-popover>
  `,
})
export class SeletorColunasComponent {
  readonly colunas = input.required<ColunaTabela[]>();
  /** As escolhidas (sem a fixa), na ordem da tabela. */
  readonly escolhidas = input.required<string[]>();
  readonly mudou = output<string[]>();
  readonly restaurar = output<void>();

  readonly ordem = computed(() => ordemDoSeletor(this.colunas(), this.escolhidas()));
  readonly texto = computed(() =>
    textoColunasVisiveis(colunasVisiveis(this.colunas(), this.escolhidas()).length, this.colunas().length),
  );

  visivel(coluna: ColunaTabela): boolean {
    return !!coluna.fixa || this.escolhidas().includes(coluna.chave);
  }

  posicao(coluna: ColunaTabela): number {
    return this.escolhidas().indexOf(coluna.chave);
  }

  alternar(coluna: ColunaTabela, visivel: boolean): void {
    this.mudou.emit(alternarColuna(this.escolhidas(), coluna.chave, visivel));
  }

  mover(coluna: ColunaTabela, direcao: -1 | 1): void {
    this.mudou.emit(moverColuna(this.escolhidas(), coluna.chave, direcao));
  }
}
