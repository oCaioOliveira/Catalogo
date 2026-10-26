import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { DatePicker } from 'primeng/datepicker';
import { Campo } from '../../core/metadados/modelo';
import { DescritoPorDirective } from '../acessibilidade/descrito-por.directive';
import { ValorCampo } from '../formulario/valores';

/** DATA (dd/mm/aaaa) e DATA_HORA (dd/mm/aaaa hh:mm, no fuso do navegador). O valor é um Date. */
@Component({
  selector: 'app-campo-data',
  imports: [ReactiveFormsModule, DatePicker, DescritoPorDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-datepicker
      dateFormat="dd/mm/yy"
      hourFormat="24"
      appendTo="body"
      [fluid]="true"
      [showIcon]="true"
      [showButtonBar]="true"
      [showTime]="comHora()"
      [inputId]="inputId()"
      [formControl]="controle()"
      [placeholder]="comHora() ? 'dd/mm/aaaa hh:mm' : 'dd/mm/aaaa'"
      [appDescritoPor]="descricaoId()"
      [invalido]="invalido()"
    />
  `,
})
export class CampoDataComponent {
  readonly campo = input.required<Campo>();
  readonly controle = input.required<FormControl<ValorCampo>>();
  readonly inputId = input.required<string>();
  readonly descricaoId = input<string | null>(null);
  readonly invalido = input(false);

  readonly comHora = computed(() => this.campo().tipo === 'DATA_HORA');
}
