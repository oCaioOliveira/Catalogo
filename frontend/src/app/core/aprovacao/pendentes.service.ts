import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter } from 'rxjs';
import { AprovacaoService } from './aprovacao.service';

/** De quanto em quanto tempo o contador de pendentes do menu é atualizado, além de a cada navegação (ADR 0045). */
export const INTERVALO_CONTADOR_MS = 2 * 60 * 1000;

/**
 * O contador de "Aprovações" do menu (GESTOR e ADMINISTRADOR): quantos itens pendentes ele pode decidir. Atualizado ao navegar e
 * a cada 2 minutos, sem notificação em tempo real (ADR 0045). Também guarda o endereço da fila (com os filtros), para o detalhe
 * voltar a ela depois de uma decisão.
 */
@Injectable({ providedIn: 'root' })
export class PendentesService {
  private readonly aprovacao = inject(AprovacaoService);
  private readonly router = inject(Router);
  private readonly destruir = inject(DestroyRef);

  readonly quantidade = signal<number | null>(null);
  /** A fila como a pessoa a deixou (filtros na URL). */
  readonly urlDaFila = signal('/aprovacoes');
  private iniciado = false;

  /** Começa a contar (o layout chama, só para quem vê a fila). Chamadas repetidas não duplicam o intervalo. */
  iniciar(): void {
    if (this.iniciado) {
      return;
    }
    this.iniciado = true;
    void this.atualizar();
    const intervalo = setInterval(() => void this.atualizar(), INTERVALO_CONTADOR_MS);
    const navegacao = this.router.events
      .pipe(filter((e) => e instanceof NavigationEnd))
      .subscribe(() => void this.atualizar());
    this.destruir.onDestroy(() => {
      clearInterval(intervalo);
      navegacao.unsubscribe();
    });
  }

  async atualizar(): Promise<void> {
    try {
      this.quantidade.set(await this.aprovacao.quantidadePendentes());
    } catch {
      // Sem contador (ex.: API fora do ar): o menu mostra só o item.
    }
  }
}
