import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Textarea } from 'primeng/textarea';
import { Campo } from '../../core/metadados/modelo';
import { DescritoPorDirective } from '../acessibilidade/descrito-por.directive';
import { ValorCampo } from '../formulario/valores';

/**
 * TEXTO_LONGO: textarea (sem limite de tamanho) com contador. Começa com 4 linhas e cresce sozinha conforme o texto (autoResize)
 * até ALTURA_MAXIMA_PX; dali em diante aparece a rolagem. O autoResize do PrimeNG compara a altura com o max-height EM PIXELS do
 * próprio elemento: por isso o limite vai inline e em px.
 */
export const ALTURA_MAXIMA_PX = 400;

@Component({
  selector: 'app-campo-texto-longo',
  imports: [ReactiveFormsModule, Textarea, DescritoPorDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <textarea
      pTextarea
      rows="4"
      class="w-full texto-longo-auto"
      [autoResize]="true"
      [style.max-height.px]="alturaMaxima"
      [id]="inputId()"
      [formControl]="controle()"
      [appDescritoPor]="descricaoId()"
      [invalido]="invalido()"
    ></textarea>
    <small class="contador" aria-hidden="true">{{ tamanho() }} caracteres</small>
  `,
})
export class CampoTextoLongoComponent {
  readonly campo = input.required<Campo>();
  readonly controle = input.required<FormControl<ValorCampo>>();
  readonly inputId = input.required<string>();
  readonly descricaoId = input<string | null>(null);
  readonly invalido = input(false);

  /** Umas 16 linhas: o suficiente para ler um parágrafo longo sem a página "pular". */
  readonly alturaMaxima = ALTURA_MAXIMA_PX;

  tamanho(): number {
    return String(this.controle().value ?? '').length;
  }
}
