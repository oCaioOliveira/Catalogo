import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { PassoTrilha, TrilhaComponent } from '../navegacao/trilha.component';

/**
 * O cabeçalho PADRÃO de toda tela (docs/design/guia-de-interface.md): a trilha, o título (<h1>) com ícone, um subtítulo opcional
 * (ex.: a descrição do tipo de item) e as ações à direita. Uma ÚNICA ação primária por tela; as demais, secundárias.
 *
 *   <app-cabecalho-pagina [trilha]="trilha()" titulo="Relatórios de Teste" icone="pi-chart-line" [subtitulo]="descricao">
 *     <p-button acoes label="Filtros" severity="secondary" [outlined]="true" />
 *     <p-button acoes label="Novo relatório" icon="pi pi-plus" />
 *   </app-cabecalho-pagina>
 */
@Component({
  selector: 'app-cabecalho-pagina',
  imports: [TrilhaComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (trilha().length > 0) {
      <app-trilha [passos]="trilha()" />
    }
    <div class="cabecalho-pagina">
      <div class="cabecalho-pagina-titulos">
        <h1>
          @if (classeIcone(); as classe) {
            <span class="icone-titulo" aria-hidden="true"><i [class]="classe"></i></span>
          }
          <span class="texto-titulo">{{ titulo() }}</span>
        </h1>
        @if (subtitulo()) {
          <p class="subtitulo-pagina">{{ subtitulo() }}</p>
        }
        <ng-content select="[complemento]" />
      </div>
      <div class="acoes-pagina">
        <ng-content select="[acoes]" />
      </div>
    </div>
  `,
})
export class CabecalhoPaginaComponent {
  readonly titulo = input.required<string>();
  readonly trilha = input<PassoTrilha[]>([]);
  /** Um ícone do PrimeIcons, com ou sem o "pi " ("pi-users" ou "pi pi-users"). */
  readonly icone = input<string | null>(null);
  readonly subtitulo = input<string | null>(null);

  readonly classeIcone = computed(() => {
    const icone = this.icone();
    if (!icone) {
      return null;
    }
    return icone.startsWith('pi ') ? icone : `pi ${icone}`;
  });
}
