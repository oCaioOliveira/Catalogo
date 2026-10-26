import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { InputNumber } from 'primeng/inputnumber';
import { Campo } from '../../core/metadados/modelo';
import { ValorCampo } from '../formulario/valores';

/** INTEIRO (sem casas) e DECIMAL (com a escala do campo). Vírgula decimal na tela; o valor é um number. */
@Component({
  selector: 'app-campo-numero',
  imports: [ReactiveFormsModule, InputNumber],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-inputnumber
      mode="decimal"
      locale="pt-BR"
      [fluid]="true"
      [inputId]="inputId()"
      [formControl]="controle()"
      [useGrouping]="true"
      [minFractionDigits]="casas()"
      [maxFractionDigits]="casas()"
      [ariaDescribedBy]="descricaoId() ?? undefined"
      [invalid]="invalido()"
    />
  `,
})
export class CampoNumeroComponent {
  readonly campo = input.required<Campo>();
  readonly controle = input.required<FormControl<ValorCampo>>();
  readonly inputId = input.required<string>();
  readonly descricaoId = input<string | null>(null);
  readonly invalido = input(false);

  readonly casas = computed(() => (this.campo().tipo === 'DECIMAL' ? (this.campo().escala ?? 2) : 0));
}
