import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MenuItem } from 'primeng/api';
import { Button } from 'primeng/button';
import { Menu } from 'primeng/menu';
import { Tooltip } from 'primeng/tooltip';

/**
 * As ações de uma linha (editar, excluir, ativar...) num único botão "⋯", estreito e preso à direita: libera espaço para as
 * colunas. O nome acessível diz de qual item são as ações ("Ações de Bateria 05 Alfa"). Sem nenhuma ação, o botão não aparece.
 */
@Component({
  selector: 'app-menu-acoes-linha',
  imports: [Button, Menu, Tooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (acoes().length > 0) {
      <p-button
        icon="pi pi-ellipsis-h"
        severity="secondary"
        size="small"
        [text]="true"
        [rounded]="true"
        [ariaLabel]="'Ações de ' + rotulo()"
        pTooltip="Ações"
        tooltipPosition="left"
        aria-haspopup="menu"
        (onClick)="menu.toggle($event)"
      />
      <p-menu #menu [popup]="true" [model]="acoes()" appendTo="body" />
    }
  `,
})
export class MenuAcoesLinhaComponent {
  readonly acoes = input.required<MenuItem[]>();
  /** O rótulo do item da linha. */
  readonly rotulo = input.required<string>();
}
