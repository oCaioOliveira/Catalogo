import { Injectable, computed, signal } from '@angular/core';
import { Densidade, gravarDensidade, lerDensidade, tamanhoDaTabela } from './densidade';

/** A densidade das tabelas, a mesma em todas as listagens (densidade.ts). */
@Injectable({ providedIn: 'root' })
export class DensidadeService {
  readonly densidade = signal<Densidade>(lerDensidade());
  /** Para o [size] do p-table. */
  readonly tamanhoTabela = computed(() => tamanhoDaTabela(this.densidade()));

  definir(densidade: Densidade): void {
    this.densidade.set(densidade);
    gravarDensidade(densidade);
  }
}
