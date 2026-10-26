import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ToggleSwitch } from 'primeng/toggleswitch';
import { Campo } from '../../core/metadados/modelo';
import { DescritoPorDirective } from '../acessibilidade/descrito-por.directive';
import { ValorCampo } from '../formulario/valores';

/** BOOLEANO: toggle (sim/não). */
@Component({
  selector: 'app-campo-booleano',
  imports: [ReactiveFormsModule, ToggleSwitch, DescritoPorDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-toggleswitch
      [inputId]="inputId()"
      [formControl]="controle()"
      [appDescritoPor]="descricaoId()"
      [invalido]="invalido()"
    />
    <span class="texto-booleano" aria-hidden="true">{{ controle().value ? 'Sim' : 'Não' }}</span>
  `,
})
export class CampoBooleanoComponent {
  readonly campo = input.required<Campo>();
  readonly controle = input.required<FormControl<ValorCampo>>();
  readonly inputId = input.required<string>();
  readonly descricaoId = input<string | null>(null);
  readonly invalido = input(false);
}
