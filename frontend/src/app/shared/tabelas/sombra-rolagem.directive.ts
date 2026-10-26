import { DestroyRef, Directive, ElementRef, afterNextRender, inject } from '@angular/core';
import { estadoRolagem } from './colunas-tabela';

/**
 * Indicação de ROLAGEM HORIZONTAL em tabelas largas (no <p-table> com [scrollable]): quando há colunas fora da área visível, uma
 * sombra aparece na borda daquele lado (esquerda e/ou direita) e some ao chegar ao fim. Liga os atributos `data-rolagem-esquerda`
 * e `data-rolagem-direita` no próprio elemento (atributos, e não classes: o p-table reescreve o próprio `class` a cada detecção de
 * mudanças); o desenho está em styles.scss. Recalcula ao rolar e quando a tabela ou a tela mudam de tamanho (colunas escondidas,
 * largura da janela, menu recolhido).
 */
@Directive({ selector: '[appSombraRolagem]' })
export class SombraRolagemDirective {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  constructor() {
    const destruir = inject(DestroyRef);
    afterNextRender(() => {
      let container: HTMLElement | null = null;
      const atualizar = () => {
        if (!container) {
          return;
        }
        const estado = estadoRolagem(container.scrollLeft, container.clientWidth, container.scrollWidth);
        this.host.toggleAttribute('data-rolagem-esquerda', estado.esquerda);
        this.host.toggleAttribute('data-rolagem-direita', estado.direita);
      };
      const observador = new ResizeObserver(atualizar);
      // A tabela pode desenhar o contêiner depois (estado de carregamento): procura de novo quando o conteúdo muda.
      const ligar = () => {
        const novo = this.host.querySelector<HTMLElement>('.p-datatable-table-container');
        if (novo && novo !== container) {
          container?.removeEventListener('scroll', atualizar);
          observador.disconnect();
          container = novo;
          container.addEventListener('scroll', atualizar, { passive: true });
          observador.observe(container);
          const tabela = container.querySelector('table');
          if (tabela) {
            observador.observe(tabela);
          }
        }
        atualizar();
      };
      const mutacoes = new MutationObserver(ligar);
      mutacoes.observe(this.host, { childList: true, subtree: true });
      ligar();
      destruir.onDestroy(() => {
        mutacoes.disconnect();
        observador.disconnect();
        container?.removeEventListener('scroll', atualizar);
      });
    });
  }
}
