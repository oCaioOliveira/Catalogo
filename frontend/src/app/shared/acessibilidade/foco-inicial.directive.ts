import { Directive, ElementRef, afterNextRender, inject, input } from '@angular/core';

/**
 * Foco automático no primeiro campo do formulário ao abrir a tela (docs/design/guia-de-interface.md → "Formulários"). Para
 * componentes do PrimeNG (p-password, p-select), foca o primeiro controle de dentro. `appFocoInicial="false"` desliga (ex.: o
 * campo só é o primeiro em um dos modos da tela).
 * Os erros de "campo obrigatório" só aparecem depois que a pessoa altera o campo ou tenta salvar: sair do campo focado sem
 * digitar não pinta a tela de vermelho.
 */
@Directive({ selector: '[appFocoInicial]' })
export class FocoInicialDirective {
  readonly appFocoInicial = input<boolean | ''>('');

  constructor() {
    const elemento = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
    afterNextRender(() => {
      if (this.appFocoInicial() === false) {
        return;
      }
      const alvo = elemento.matches('input, textarea, select, button, [tabindex]')
        ? elemento
        : elemento.querySelector<HTMLElement>('input, textarea, [role="combobox"], [tabindex="0"]');
      alvo?.focus();
    });
  }
}
