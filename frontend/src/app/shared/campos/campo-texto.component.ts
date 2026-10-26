import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { InputText } from 'primeng/inputtext';
import { Campo } from '../../core/metadados/modelo';
import { DescritoPorDirective } from '../acessibilidade/descrito-por.directive';
import { ValorCampo } from '../formulario/valores';

/** TEXTO: input com o limite de caracteres do campo e contador. */
@Component({
  selector: 'app-campo-texto',
  imports: [ReactiveFormsModule, InputText, DescritoPorDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <input
      pInputText
      type="text"
      class="w-full"
      [id]="inputId()"
      [formControl]="controle()"
      [attr.maxlength]="campo().tamanhoMaximo"
      [appDescritoPor]="descricaoId()"
      [invalido]="invalido()"
    />
    @if (campo().tamanhoMaximo) {
      <small class="contador" aria-hidden="true">{{ tamanho() }}/{{ campo().tamanhoMaximo }}</small>
    }
  `,
})
export class CampoTextoComponent {
  readonly campo = input.required<Campo>();
  readonly controle = input.required<FormControl<ValorCampo>>();
  readonly inputId = input.required<string>();
  readonly descricaoId = input<string | null>(null);
  readonly invalido = input(false);

  tamanho(): number {
    return String(this.controle().value ?? '').length;
  }
}
