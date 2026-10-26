import { Directive, ElementRef, afterEveryRender, inject, input } from '@angular/core';

/**
 * Liga a mensagem de erro (ou de ajuda) ao campo para leitores de tela: põe `aria-describedby` e `aria-invalid`
 * no <input> de dentro de um componente do PrimeNG (que nem sempre aceita esse atributo como propriedade).
 *   <p-select appDescritoPor="erros-nome" [invalido]="true" ... />
 */
@Directive({ selector: '[appDescritoPor]' })
export class DescritoPorDirective {
  readonly appDescritoPor = input<string | null>(null);
  readonly invalido = input(false);

  private readonly elemento = inject<ElementRef<HTMLElement>>(ElementRef);

  constructor() {
    afterEveryRender(() => {
      const host = this.elemento.nativeElement;
      const alvo = host.matches('input, textarea')
        ? host
        : host.querySelector<HTMLElement>('input, textarea, [role="combobox"], [role="switch"]');
      if (!alvo) {
        return;
      }
      const ids = this.appDescritoPor();
      if (ids) {
        alvo.setAttribute('aria-describedby', ids);
      } else {
        alvo.removeAttribute('aria-describedby');
      }
      alvo.setAttribute('aria-invalid', String(this.invalido()));
    });
  }
}
