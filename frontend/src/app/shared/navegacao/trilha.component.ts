import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Params, RouterLink } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { Breadcrumb } from 'primeng/breadcrumb';

/** Um passo da trilha. Com `link`, é navegável (com `parametros` na query, se houver); o último é a página atual. */
export interface PassoTrilha {
  rotulo: string;
  link?: string | readonly unknown[];
  parametros?: Params;
}

/**
 * A trilha ("breadcrumb") das páginas: o p-breadcrumb do PrimeNG, com as cores, o separador e o espaçamento do tema. Os passos com
 * link usam o roteador (sem recarregar a página); o ÚLTIMO é a página atual, em texto, com aria-current="page".
 */
@Component({
  selector: 'app-trilha',
  imports: [Breadcrumb, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-breadcrumb styleClass="trilha" [model]="itens()" [pt]="{ root: { 'aria-label': 'Trilha' } }">
      <ng-template #item let-item>
        @if (item.state?.atual) {
          <span class="p-breadcrumb-item-label trilha-atual" aria-current="page">{{ item.label }}</span>
        } @else if (item.routerLink) {
          <a class="p-breadcrumb-item-link" [routerLink]="item.routerLink" [queryParams]="item.queryParams">
            <span class="p-breadcrumb-item-label">{{ item.label }}</span>
          </a>
        } @else {
          <span class="p-breadcrumb-item-label">{{ item.label }}</span>
        }
      </ng-template>
    </p-breadcrumb>
  `,
})
export class TrilhaComponent {
  readonly passos = input.required<PassoTrilha[]>();

  readonly itens = computed<MenuItem[]>(() =>
    this.passos().map((p, i, todos) => ({
      label: p.rotulo,
      routerLink: p.link as MenuItem['routerLink'],
      queryParams: p.parametros,
      state: { atual: i === todos.length - 1 },
    })),
  );
}
