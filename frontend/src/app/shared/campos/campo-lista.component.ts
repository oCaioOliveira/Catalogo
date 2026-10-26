import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Select } from 'primeng/select';
import { Campo } from '../../core/metadados/modelo';
import { DescritoPorDirective } from '../acessibilidade/descrito-por.directive';
import { ValorCampo } from '../formulario/valores';

/**
 * LISTA: select com as opções do campo. O p-select desenha um elemento com role="combobox" que não é um <input>: o
 * <label for> não o rotula, então o nome acessível vem do rótulo pelo id dele (ariaLabelledBy = "<inputId>-rotulo", o id que
 * o campo-formulario dá ao <label>). Sem isso, o leitor de tela anunciaria só o valor escolhido.
 */
@Component({
  selector: 'app-campo-lista',
  imports: [ReactiveFormsModule, Select, DescritoPorDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-select
      appendTo="body"
      placeholder="Selecione"
      [fluid]="true"
      [showClear]="!campo().obrigatorio"
      [options]="campo().opcoes"
      [inputId]="inputId()"
      [ariaLabelledBy]="inputId() + '-rotulo'"
      [formControl]="controle()"
      [appDescritoPor]="descricaoId()"
      [invalid]="invalido()"
      [invalido]="invalido()"
    />
  `,
})
export class CampoListaComponent {
  readonly campo = input.required<Campo>();
  readonly controle = input.required<FormControl<ValorCampo>>();
  readonly inputId = input.required<string>();
  readonly descricaoId = input<string | null>(null);
  readonly invalido = input(false);
}
