import { ChangeDetectionStrategy, ChangeDetectorRef, Component, effect, inject, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Button, ButtonDirective } from 'primeng/button';
import { InputGroup } from 'primeng/inputgroup';
import { InputText } from 'primeng/inputtext';
import { Tooltip } from 'primeng/tooltip';
import { Campo } from '../../core/metadados/modelo';
import { DescritoPorDirective } from '../acessibilidade/descrito-por.directive';
import { MAXIMO_CARACTERES_URL, erroDeUrl } from '../formulario/validadores';
import { ValorCampo } from '../formulario/valores';

/**
 * URL (ADR 0042): input do tipo "url" (teclado certo no celular) com a validação do tipo, e ao lado um botão que abre o endereço
 * numa nova aba (rel="noopener noreferrer"). O botão só fica ativo com uma URL válida; ele é um link de verdade (<a>), para
 * funcionar também com o botão do meio do mouse e o "abrir em nova aba".
 */
@Component({
  selector: 'app-campo-url',
  imports: [ReactiveFormsModule, Button, ButtonDirective, InputGroup, InputText, Tooltip, DescritoPorDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-inputgroup>
      <input
        pInputText
        type="url"
        inputmode="url"
        autocomplete="url"
        placeholder="https://"
        [id]="inputId()"
        [formControl]="controle()"
        [attr.maxlength]="maximo"
        [appDescritoPor]="descricaoId()"
        [invalido]="invalido()"
      />
      @if (endereco(); as href) {
        <a
          pButton
          class="botao-abrir-link"
          severity="secondary"
          icon="pi pi-external-link"
          target="_blank"
          rel="noopener noreferrer"
          [href]="href"
          [attr.aria-label]="'Abrir ' + campo().nome + ' numa nova aba'"
          [pTooltip]="'Abrir ' + campo().nome + ' numa nova aba'"
          tooltipPosition="top"
        ></a>
      } @else {
        <p-button
          icon="pi pi-external-link"
          severity="secondary"
          [disabled]="true"
          [ariaLabel]="'Abrir ' + campo().nome + ' (informe uma URL válida)'"
        />
      }
    </p-inputgroup>
  `,
})
export class CampoUrlComponent {
  readonly campo = input.required<Campo>();
  readonly controle = input.required<FormControl<ValorCampo>>();
  readonly inputId = input.required<string>();
  readonly descricaoId = input<string | null>(null);
  readonly invalido = input(false);

  readonly maximo = MAXIMO_CARACTERES_URL;
  private readonly detector = inject(ChangeDetectorRef);

  constructor() {
    // OnPush: o botão depende do valor digitado.
    effect((limpar) => {
      const inscricao = this.controle().valueChanges.subscribe(() => this.detector.markForCheck());
      limpar(() => inscricao.unsubscribe());
    });
  }

  /** O endereço para o botão, só quando o valor é uma URL válida. */
  endereco(): string | null {
    const texto = String(this.controle().value ?? '').trim();
    return texto && erroDeUrl(texto) === null ? texto : null;
  }
}
