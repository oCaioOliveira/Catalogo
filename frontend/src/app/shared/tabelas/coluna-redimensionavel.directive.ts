import { DestroyRef, Directive, ElementRef, Renderer2, afterNextRender, inject, input, output } from '@angular/core';
import { LARGURA_MINIMA } from './colunas-tabela';

/** Quanto as setas mudam a largura pelo teclado (px). */
const PASSO_TECLADO = 16;

/**
 * Alça de redimensionar no canto direito de um <th>: arrastar com o mouse (ou o dedo) ou, com o foco nela, as setas ← e →. A
 * alça é um "separator" com o nome "Largura da coluna X" e o valor atual, para o teclado e o leitor de tela. Emite a largura nova
 * ao soltar (quem usa salva, ex.: por tipo de item). O PrimeNG mede por posição da coluna (nth-child), o que quebra quando a
 * pessoa esconde ou reordena colunas; por isso a tabela usa larguras explícitas em cada <th> (table-layout: fixed).
 */
@Directive({
  selector: 'th[appColunaRedimensionavel]',
  // O nome do cabeçalho é o da coluna: sem isso, o nome da alça ("Largura da coluna X") entraria nele.
  host: { '[attr.aria-label]': 'appColunaRedimensionavel()' },
})
export class ColunaRedimensionavelDirective {
  /** O nome da coluna, para o nome acessível da alça. */
  readonly appColunaRedimensionavel = input.required<string>();
  readonly redimensionada = output<number>();

  private readonly th = inject<ElementRef<HTMLTableCellElement>>(ElementRef).nativeElement;
  private readonly renderer = inject(Renderer2);

  constructor() {
    const destruir = inject(DestroyRef);
    afterNextRender(() => {
      const alca: HTMLSpanElement = this.renderer.createElement('span');
      alca.className = 'alca-redimensionar';
      alca.tabIndex = 0;
      alca.setAttribute('role', 'separator');
      alca.setAttribute('aria-orientation', 'vertical');
      this.atualizarRotulo(alca);
      this.renderer.appendChild(this.th, alca);
      const remover = [
        this.renderer.listen(alca, 'pointerdown', (e: PointerEvent) => this.arrastar(e, alca)),
        this.renderer.listen(alca, 'keydown', (e: KeyboardEvent) => this.teclado(e, alca)),
        // A alça fica dentro do <th> que ordena: o clique nela não pode ordenar a coluna.
        this.renderer.listen(alca, 'click', (e: MouseEvent) => e.stopPropagation()),
      ];
      destruir.onDestroy(() => remover.forEach((r) => r()));
    });
  }

  private atualizarRotulo(alca: HTMLElement): void {
    alca.setAttribute('aria-label', `Largura da coluna ${this.appColunaRedimensionavel()}`);
    alca.setAttribute('aria-valuenow', String(Math.round(this.th.offsetWidth)));
    alca.setAttribute('aria-valuemin', String(LARGURA_MINIMA));
  }

  private aplicar(largura: number): number {
    const valor = Math.round(Math.max(LARGURA_MINIMA, largura));
    this.th.style.width = `${valor}px`;
    return valor;
  }

  private arrastar(evento: PointerEvent, alca: HTMLElement): void {
    evento.preventDefault();
    evento.stopPropagation();
    const inicioX = evento.clientX;
    const larguraInicial = this.th.offsetWidth;
    alca.setPointerCapture(evento.pointerId);
    this.th.classList.add('redimensionando');
    const mover = (e: PointerEvent) => this.aplicar(larguraInicial + e.clientX - inicioX);
    const soltar = (e: PointerEvent) => {
      alca.removeEventListener('pointermove', mover);
      alca.removeEventListener('pointerup', soltar);
      alca.releasePointerCapture(e.pointerId);
      this.th.classList.remove('redimensionando');
      const final = this.aplicar(larguraInicial + e.clientX - inicioX);
      this.atualizarRotulo(alca);
      this.redimensionada.emit(final);
    };
    alca.addEventListener('pointermove', mover);
    alca.addEventListener('pointerup', soltar);
  }

  private teclado(evento: KeyboardEvent, alca: HTMLElement): void {
    if (evento.key !== 'ArrowLeft' && evento.key !== 'ArrowRight') {
      return;
    }
    evento.preventDefault();
    evento.stopPropagation();
    const final = this.aplicar(this.th.offsetWidth + (evento.key === 'ArrowRight' ? PASSO_TECLADO : -PASSO_TECLADO));
    this.atualizarRotulo(alca);
    this.redimensionada.emit(final);
  }
}
