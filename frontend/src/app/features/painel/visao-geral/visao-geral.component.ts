import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Button } from 'primeng/button';
import { Message } from 'primeng/message';
import { Skeleton } from 'primeng/skeleton';
import { Entidade } from '../../../core/metadados/modelo';
import { VisaoGeral } from '../../../core/painel/modelo-painel';
import { PainelService } from '../../../core/painel/painel.service';
import { formatarNumero } from '../graficos/formatacao';
import { EstadoPainel, filtrosDoEstado } from '../modelo/estado-url';
import { BlocoAprovacaoComponent, BlocoAvaliacaoComponent } from './blocos-gestao.component';
import { CartaoGraficoComponent } from './cartao-grafico.component';
import {
  atividadeDaVisaoGeral,
  indicadoresCriadosPorMes,
  indicadoresPorEquipe,
  kpisDaVisaoGeral,
  resumoAprovacao,
  resumoAvaliacao,
} from './visao-geral';

/**
 * A visão geral do grupo (GET .../visao-geral, RN81 a RN84): cartões, itens criados por mês, itens por equipe, os usuários mais
 * ativos e, quando a API os traz, os blocos de aprovação e avaliação. Recarrega quando o período ou a equipe mudam.
 */
@Component({
  selector: 'app-visao-geral',
  imports: [Button, Message, Skeleton, CartaoGraficoComponent, BlocoAprovacaoComponent, BlocoAvaliacaoComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './visao-geral.component.html',
})
export class VisaoGeralComponent {
  readonly grupo = input.required<string>();
  readonly estadoPainel = input.required<EstadoPainel>();
  readonly principal = input<Entidade | null>(null);

  private readonly painel = inject(PainelService);

  readonly visao = signal<VisaoGeral | null>(null);
  readonly carregando = signal(true);
  readonly erro = signal(false);
  private pedido = 0;

  readonly kpis = computed(() => {
    const visao = this.visao();
    return visao ? kpisDaVisaoGeral(visao, this.principal()) : [];
  });
  readonly criadosPorMes = computed(() => indicadoresCriadosPorMes(this.visao() ?? {}));
  readonly porEquipe = computed(() => indicadoresPorEquipe(this.visao() ?? {}));
  readonly atividade = computed(() => atividadeDaVisaoGeral(this.visao() ?? {}));
  readonly aprovacao = computed(() => resumoAprovacao(this.visao() ?? {}));
  readonly avaliacao = computed(() => resumoAvaliacao(this.visao() ?? {}));

  readonly formatarNumero = formatarNumero;

  constructor() {
    effect(() => {
      const grupo = this.grupo();
      const estado = this.estadoPainel();
      untracked(() => void this.carregar(grupo, estado));
    });
  }

  tentarDeNovo(): void {
    void this.carregar(this.grupo(), this.estadoPainel());
  }

  private async carregar(grupo: string, estado: EstadoPainel): Promise<void> {
    const pedido = ++this.pedido;
    this.carregando.set(true);
    this.erro.set(false);
    try {
      const visao = await this.painel.visaoGeral(grupo, filtrosDoEstado(estado));
      if (pedido === this.pedido) {
        this.visao.set(visao);
      }
    } catch {
      if (pedido === this.pedido) {
        this.erro.set(true);
      }
    } finally {
      if (pedido === this.pedido) {
        this.carregando.set(false);
      }
    }
  }
}
