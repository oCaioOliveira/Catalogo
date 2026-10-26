import { ChangeDetectionStrategy, Component, DestroyRef, afterNextRender, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Toast } from 'primeng/toast';
import { esconderIconesDecorativos } from './shared/acessibilidade/icones-decorativos';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Toast],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <!-- Toasts num só canto, com tempo para ler (5 s; os de erro, mais: erro.interceptor.ts). -->
    <p-toast position="top-right" [life]="5000" />
    <router-outlet />
  `,
})
export class App {
  constructor() {
    const destruir = inject(DestroyRef);
    afterNextRender(() => {
      const observador = esconderIconesDecorativos(document.body);
      destruir.onDestroy(() => observador.disconnect());
    });
  }
}
