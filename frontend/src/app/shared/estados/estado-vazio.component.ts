import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * O estado VAZIO padrão (docs/design/guia-de-interface.md): um ícone, uma frase que explica e a ação principal (projetada).
 *   <app-estado-vazio icone="pi-inbox" titulo="Nenhum item ainda" texto="Cadastre o primeiro para começar.">
 *     <p-button label="Novo relatório" icon="pi pi-plus" />
 *   </app-estado-vazio>
 */
@Component({
  selector: 'app-estado-vazio',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="estado-vazio" [class.compacto]="compacto()">
      <span class="estado-icone" aria-hidden="true"><i [class]="'pi ' + icone()"></i></span>
      <p class="estado-titulo">{{ titulo() }}</p>
      @if (texto()) {
        <p class="estado-texto">{{ texto() }}</p>
      }
      <div class="estado-acoes"><ng-content /></div>
    </div>
  `,
})
export class EstadoVazioComponent {
  readonly titulo = input.required<string>();
  readonly texto = input<string | null>(null);
  readonly icone = input('pi-inbox');
  /** Dentro de tabelas e seções: menos espaço em volta. */
  readonly compacto = input(false);
}
