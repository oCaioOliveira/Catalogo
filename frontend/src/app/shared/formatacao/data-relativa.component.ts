import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { Tooltip } from 'primeng/tooltip';
import { formatarDataCompleta, formatarRelativo } from './datas-relativas';

/** Uma data relativa ("há 3 horas"), com a data completa na dica e no atributo datetime (leitores de tela e cópia). */
@Component({
  selector: 'app-data-relativa',
  imports: [Tooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (valor(); as data) {
      <time class="data-relativa" [attr.datetime]="iso()" [pTooltip]="completa()" tooltipPosition="top">{{
        relativa()
      }}</time>
    } @else {
      —
    }
  `,
})
export class DataRelativaComponent {
  /** ISO 8601 (da API) ou Date. Sem data: "—". */
  readonly data = input.required<string | Date | null | undefined>();
  /** O "agora" da comparação (para as telas que atualizam de tempos em tempos). */
  readonly agora = input<Date | null>(null);

  readonly valor = computed(() => {
    const data = this.data();
    if (!data) {
      return null;
    }
    const valor = data instanceof Date ? data : new Date(data);
    return Number.isNaN(valor.getTime()) ? null : valor;
  });
  readonly iso = computed(() => this.valor()?.toISOString() ?? null);
  readonly relativa = computed(() => {
    const valor = this.valor();
    return valor ? formatarRelativo(valor, this.agora() ?? new Date()) : '';
  });
  readonly completa = computed(() => {
    const valor = this.valor();
    return valor ? formatarDataCompleta(valor) : '';
  });
}
