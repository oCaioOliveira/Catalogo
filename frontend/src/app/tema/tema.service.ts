import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { CHAVE_TEMA, PreferenciaTema, TemaAplicado, lerPreferencia, resolverTema } from './preferencia-tema';

/** A classe que liga o modo escuro do PrimeNG (opcoesTema.darkModeSelector) e das cores semânticas (fundamentos.scss). */
export const CLASSE_ESCURO = 'catalogo-escuro';

/**
 * O tema da interface: claro, escuro ou "seguir o sistema" (o padrão). A escolha fica no localStorage (catalogo.tema, com
 * try/catch: sem armazenamento, vale só nesta aba), e "seguir o sistema" acompanha a mudança do sistema operacional na hora.
 * Aplica a classe .catalogo-escuro no <html>; quem desenha com cores lidas em tempo de execução (os gráficos) observa `aplicado`.
 */
@Injectable({ providedIn: 'root' })
export class TemaService {
  readonly preferencia = signal<PreferenciaTema>(lerPreferencia(ler()));
  private readonly sistemaEscuro = signal(consultaEscuro()?.matches ?? false);
  readonly aplicado = computed<TemaAplicado>(() => resolverTema(this.preferencia(), this.sistemaEscuro()));

  constructor() {
    const consulta = consultaEscuro();
    const aoMudar = (evento: MediaQueryListEvent) => {
      this.sistemaEscuro.set(evento.matches);
      this.aplicar();
    };
    consulta?.addEventListener('change', aoMudar);
    inject(DestroyRef).onDestroy(() => consulta?.removeEventListener('change', aoMudar));
    this.aplicar();
  }

  escolher(preferencia: PreferenciaTema): void {
    this.preferencia.set(preferencia);
    try {
      localStorage.setItem(CHAVE_TEMA, preferencia);
    } catch {
      // Sem armazenamento: a escolha vale até fechar a aba.
    }
    this.aplicar();
  }

  private aplicar(): void {
    document.documentElement.classList.toggle(CLASSE_ESCURO, this.aplicado() === 'escuro');
  }
}

function ler(): string | null {
  try {
    return localStorage.getItem(CHAVE_TEMA);
  } catch {
    return null;
  }
}

function consultaEscuro(): MediaQueryList | null {
  return typeof window.matchMedia === 'function' ? window.matchMedia('(prefers-color-scheme: dark)') : null;
}
