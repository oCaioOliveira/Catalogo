import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Tooltip } from 'primeng/tooltip';
import { Campo, OpcaoReferencia } from '../../core/metadados/modelo';
import { formatarValor } from '../formatacao/formatar-valor';

/**
 * Um valor de campo em LEITURA, igual nas tabelas e no detalhe (o tipo decide como aparece):
 *   - URL: link que abre numa nova aba (target="_blank", rel="noopener noreferrer"); na tabela, o texto é truncado e a dica mostra
 *     o endereço inteiro (ADR 0042);
 *   - REFERENCIA: link para o item apontado quando `comoLink` (entidades por equipe), senão texto (cadastros de apoio globais,
 *     regras-metadados.ts → referenciaComoLink);
 *   - BOOLEANO: na tabela, ícone (com o texto para o leitor de tela); no detalhe, "Sim"/"Não";
 *   - TEXTO_LONGO: preserva as quebras de linha;
 *   - os demais: o texto formatado em pt-BR (formatar-valor.ts).
 * Vazio não mostra nada: quem usa decide (a tabela deixa a célula vazia; o detalhe junta os vazios numa linha só).
 */
@Component({
  selector: 'app-valor-campo',
  imports: [RouterLink, Tooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  // Na tabela: uma linha só, com reticências se não couber (o detalhe mostra o valor inteiro); o texto inteiro fica na dica do
  // navegador (title), para quem passa o mouse (a URL já tem a dela).
  host: {
    '[class.valor-tabela]': "modo() === 'tabela'",
    '[attr.title]':
      "modo() === 'tabela' && campo().tipo !== 'URL' && campo().tipo !== 'BOOLEANO' ? texto() || null : null",
  },
  template: `
    @switch (campo().tipo) {
      @case ('URL') {
        @if (texto()) {
          <a
            class="link-url"
            [class.link-url-truncado]="modo() === 'tabela'"
            target="_blank"
            rel="noopener noreferrer"
            [href]="texto()"
            [pTooltip]="modo() === 'tabela' ? texto() : undefined"
            tooltipPosition="top"
            ><span class="texto-url">{{ texto() }}</span
            ><i class="pi pi-external-link icone-link-externo" aria-hidden="true"></i
            ><span class="sr-only"> (abre numa nova aba)</span></a
          >
        }
      }
      @case ('REFERENCIA') {
        @if (referencia(); as ref) {
          @if (comoLink()) {
            <a [routerLink]="['/', grupo(), campo().entidadeReferenciada, ref.id]">{{ texto() }}</a>
          } @else {
            {{ texto() }}
          }
        }
      }
      @case ('BOOLEANO') {
        @if (booleano() !== null) {
          @if (modo() === 'tabela') {
            <i
              class="pi"
              [class.pi-check-circle]="booleano()"
              [class.pi-times-circle]="!booleano()"
              [class.texto-sucesso]="booleano()"
              aria-hidden="true"
            ></i>
            <span class="sr-only">{{ texto() }}</span>
          } @else {
            {{ texto() }}
          }
        }
      }
      @case ('TEXTO_LONGO') {
        <span class="texto-longo">{{ texto() }}</span>
      }
      @default {
        {{ texto() }}
      }
    }
  `,
})
export class ValorCampoComponent {
  readonly campo = input.required<Campo>();
  readonly valor = input<unknown>(null);
  /** Só para o link das referências. */
  readonly grupo = input('');
  /** REFERENCIA: link (entidade por equipe) ou texto (cadastro de apoio global). */
  readonly comoLink = input(false);
  readonly modo = input<'tabela' | 'detalhe'>('tabela');

  readonly texto = computed(() => formatarValor(this.campo(), this.valor()));
  readonly referencia = computed(() => {
    const valor = this.valor() as OpcaoReferencia | null | undefined;
    return valor && typeof valor === 'object' && 'id' in valor ? valor : null;
  });
  readonly booleano = computed(() => {
    const valor = this.valor();
    return typeof valor === 'boolean' ? valor : null;
  });
}
