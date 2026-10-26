import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { mediaEmTexto } from '../../core/avaliacao/regras-avaliacao';

/**
 * Uma média de estrelas (RN47, RN48) em estrelas (cheia, meia ou vazia) e em número ("4,3"). Sem nota: "—". Para o leitor de tela,
 * só o texto "Média 4,3 de 5" (as estrelas são decorativas).
 */
@Component({
  selector: 'app-media-estrelas',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (media() === null) {
      <span class="texto-secundario" aria-label="Sem avaliações">—</span>
    } @else {
      <span class="media-estrelas" role="img" [attr.aria-label]="'Média ' + texto() + ' de 5'">
        @for (icone of icones(); track $index) {
          <i class="pi" [class]="icone" aria-hidden="true"></i>
        }
        <span class="media-numero" aria-hidden="true">{{ texto() }}</span>
      </span>
    }
  `,
})
export class MediaEstrelasComponent {
  readonly media = input<number | null>(null);

  readonly texto = computed(() => mediaEmTexto(this.media()));
  /** Arredonda para a meia estrela mais próxima. */
  readonly icones = computed(() => {
    const metades = Math.round((this.media() ?? 0) * 2);
    return [1, 2, 3, 4, 5].map((i) =>
      metades >= i * 2 ? 'pi-star-fill' : metades === i * 2 - 1 ? 'pi-star-half-fill' : 'pi-star',
    );
  });
}
