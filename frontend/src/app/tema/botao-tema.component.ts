import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Tooltip } from 'primeng/tooltip';
import { OPCOES_TEMA, proximaPreferencia } from './preferencia-tema';
import { TemaService } from './tema.service';

/**
 * O atalho de tema (cabeçalho e login): o ícone mostra o modo atual (sol, lua ou monitor) e cada clique passa ao próximo
 * (claro → escuro → seguir o sistema). A mesma escolha do menu do usuário (TemaService, salva no localStorage).
 */
@Component({
  selector: 'app-botao-tema',
  imports: [Tooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="botao-cabecalho botao-tema"
      [attr.aria-label]="rotulo()"
      [attr.data-tema]="tema.preferencia()"
      pTooltip="Tema: claro, escuro ou do sistema"
      tooltipPosition="bottom"
      (click)="alternar()"
    >
      <i [class]="atual().icone" aria-hidden="true"></i>
    </button>
  `,
})
export class BotaoTemaComponent {
  readonly tema = inject(TemaService);

  readonly atual = computed(
    () => OPCOES_TEMA.find((opcao) => opcao.valor === this.tema.preferencia()) ?? OPCOES_TEMA[2],
  );
  readonly rotulo = computed(() => {
    const proxima = OPCOES_TEMA.find((opcao) => opcao.valor === proximaPreferencia(this.tema.preferencia()));
    return `Tema: ${this.atual().rotulo.toLowerCase()}. Mudar para ${proxima?.rotulo.toLowerCase()}`;
  });

  alternar(): void {
    this.tema.escolher(proximaPreferencia(this.tema.preferencia()));
  }
}
