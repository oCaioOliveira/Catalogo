import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { Button } from 'primeng/button';
import { Popover } from 'primeng/popover';
import { filtrarIcones, nomeDoIcone } from './busca-icones';
import { ICONES_PRIMEICONS } from './primeicons';

/** Quantos ícones por linha na grade (para as setas ↑ e ↓). */
const COLUNAS_GRADE = 8;

/**
 * O seletor VISUAL de ícone dos tipos de item (RN95), na aba Geral do editor: mostra o ícone atual e abre uma grade com busca
 * (em inglês, como no PrimeIcons, ou em português, pelos sinônimos de busca-icones.ts). A grade é um grupo de rádio: Tab entra
 * nela, as setas andam entre os ícones e Enter ou Espaço escolhe. "Sem ícone" volta ao genérico.
 *
 * A lista vem de primeicons.ts, GERADA a partir do CSS do pacote: depois de atualizar o PrimeIcons, gere de novo com o comando
 * do README do front ("Ícones dos tipos de item").
 */
@Component({
  selector: 'app-seletor-icone',
  imports: [Button, Popover],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="seletor-icone">
      <span class="icone-atual" aria-hidden="true"><i [class]="'pi ' + (valor() || generico())"></i></span>
      <span class="nome-icone-atual">{{ valor() ? nomeDoIcone(valor()!) : 'Genérico (sem ícone escolhido)' }}</span>
      <p-button
        [label]="valor() ? 'Trocar ícone' : 'Escolher ícone'"
        icon="pi pi-images"
        severity="secondary"
        size="small"
        [outlined]="true"
        [ariaLabel]="(valor() ? 'Trocar ícone' : 'Escolher ícone') + ' de ' + rotulo()"
        aria-haspopup="dialog"
        [disabled]="desabilitado()"
        (onClick)="painel.toggle($event); busca.set('')"
      />
      @if (valor()) {
        <p-button
          label="Usar o genérico"
          size="small"
          severity="secondary"
          [text]="true"
          [disabled]="desabilitado()"
          (onClick)="escolher(null)"
        />
      }
    </div>
    <p-popover #painel styleClass="popover-icones">
      <div class="painel-icones" role="dialog" [attr.aria-label]="'Ícone de ' + rotulo()">
        <label for="busca-icone" class="sr-only">Buscar ícone</label>
        <input
          id="busca-icone"
          class="p-inputtext busca-icone"
          type="search"
          placeholder="Buscar ícone (ex.: gráfico, users)"
          autocomplete="off"
          [value]="busca()"
          (input)="busca.set($any($event.target).value)"
        />
        <p class="total-icones" aria-live="polite">
          {{ encontrados().length === 1 ? '1 ícone' : encontrados().length + ' ícones' }}
        </p>
        <div class="grade-icones" role="radiogroup" [attr.aria-label]="'Ícones para ' + rotulo()">
          @for (icone of encontrados(); track icone; let i = $index) {
            <button
              type="button"
              role="radio"
              class="opcao-icone"
              [class.escolhido]="icone === valor()"
              [attr.aria-checked]="icone === valor()"
              [attr.aria-label]="nomeDoIcone(icone)"
              [attr.title]="nomeDoIcone(icone)"
              [tabIndex]="i === indiceFocavel() ? 0 : -1"
              (click)="escolher(icone); painel.hide()"
              (keydown)="navegar($event)"
            >
              <i [class]="'pi ' + icone" aria-hidden="true"></i>
            </button>
          } @empty {
            <p class="texto-secundario">Nenhum ícone encontrado. Tente outra palavra.</p>
          }
        </div>
      </div>
    </p-popover>
  `,
})
export class SeletorIconeComponent {
  /** O ícone escolhido ("pi-chart-line"), ou null (o genérico). */
  readonly valor = input<string | null>(null);
  /** O nome do tipo de item, para os nomes acessíveis. */
  readonly rotulo = input('o tipo de item');
  readonly generico = input('pi-file');
  readonly desabilitado = input(false);
  readonly mudou = output<string | null>();

  readonly busca = signal('');
  readonly encontrados = computed(() => filtrarIcones(ICONES_PRIMEICONS, this.busca()));
  /** Na grade, só um ícone recebe o Tab (o escolhido, se estiver na lista; senão, o primeiro). */
  readonly indiceFocavel = computed(() => Math.max(0, this.encontrados().indexOf(this.valor() ?? '')));
  readonly nomeDoIcone = nomeDoIcone;

  escolher(icone: string | null): void {
    this.mudou.emit(icone);
  }

  /** Setas andam pela grade (← → um ícone; ↑ ↓ uma linha); Home e End vão ao primeiro e ao último. */
  navegar(evento: KeyboardEvent): void {
    const botoes = Array.from(document.querySelectorAll<HTMLButtonElement>('.popover-icones .opcao-icone'));
    const atual = botoes.indexOf(document.activeElement as HTMLButtonElement);
    if (atual < 0) {
      return;
    }
    const passos: Record<string, number> = {
      ArrowRight: 1,
      ArrowLeft: -1,
      ArrowDown: COLUNAS_GRADE,
      ArrowUp: -COLUNAS_GRADE,
      Home: -atual,
      End: botoes.length - 1 - atual,
    };
    const passo = passos[evento.key];
    if (passo === undefined) {
      return;
    }
    evento.preventDefault();
    const proximo = Math.min(botoes.length - 1, Math.max(0, atual + passo));
    botoes[atual].tabIndex = -1;
    botoes[proximo].tabIndex = 0;
    botoes[proximo].focus();
  }
}
