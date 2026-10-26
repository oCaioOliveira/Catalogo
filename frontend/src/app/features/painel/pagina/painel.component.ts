import { ChangeDetectionStrategy, Component, computed, effect, inject, input, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Skeleton } from 'primeng/skeleton';
import { montarMenu } from '../../../core/metadados/regras-metadados';
import { configuracaoDoPainel } from '../configuracao/paineis';
import { FiltrosPainelComponent } from '../filtros/filtros-painel.component';
import { CoresGraficoService } from '../graficos/cores';
import { FAMILIA_METRICAS } from '../graficos/transformar-dados';
import { EstadoPainel, estadoPainelDaUrl, parametrosDoEstadoPainel } from '../modelo/estado-url';
import { VisaoGeralComponent } from '../visao-geral/visao-geral.component';
import { WidgetComponent } from '../widget/widget.component';
import { ContextoPainel } from './contexto-painel';
import { EstadoErroComponent } from '../../../shared/estados/estado-erro.component';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';

/**
 * /painel/:grupo — o painel gerencial (GESTOR e ADMINISTRADOR, RN78): filtros globais no topo (período e equipe, na URL), a
 * visão geral e os widgets pré-configurados do grupo (configuracao/<grupo>.painel.ts). Sem arquivo de configuração, só a visão
 * geral. Mudar um filtro muda a URL, e todos os blocos recarregam a partir dela.
 */
@Component({
  selector: 'app-painel',
  imports: [
    RouterLink,
    Skeleton,
    CabecalhoPaginaComponent,
    EstadoErroComponent,
    FiltrosPainelComponent,
    VisaoGeralComponent,
    WidgetComponent,
  ],
  providers: [CoresGraficoService, ContextoPainel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './painel.component.html',
})
export class PainelComponent {
  readonly grupo = input.required<string>();

  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly cores = inject(CoresGraficoService);
  readonly contexto = inject(ContextoPainel);

  private readonly parametrosUrl = toSignal(this.rota.queryParams, { initialValue: {} });
  private readonly hoje = new Date();

  readonly estado = computed(() => estadoPainelDaUrl(this.parametrosUrl(), this.hoje));
  readonly parametrosPainel = computed(() => parametrosDoEstadoPainel(this.estado()));
  readonly configuracao = computed(() => configuracaoDoPainel(this.grupo()));
  readonly kpis = computed(() => (this.configuracao()?.widgets ?? []).filter((w) => w.visualizacao === 'kpi'));
  readonly graficos = computed(() => (this.configuracao()?.widgets ?? []).filter((w) => w.visualizacao !== 'kpi'));
  readonly principal = computed(() => {
    const entidades = this.contexto.entidades() ?? [];
    const chave = this.configuracao()?.entidadePrincipal;
    return (chave ? entidades.find((e) => e.chave === chave) : montarMenu(entidades).principais[0]) ?? null;
  });

  constructor() {
    effect(() => {
      const grupo = this.grupo();
      untracked(() => void this.carregar(grupo));
    });
  }

  private async carregar(grupo: string): Promise<void> {
    // As cores das métricas são reservadas na ordem do arquivo: a mesma métrica tem a mesma cor em todo o painel.
    this.cores.registrar(
      (configuracaoDoPainel(grupo)?.widgets ?? []).flatMap((w) => w.metricas.map((m) => `metrica:${m}`)),
      FAMILIA_METRICAS,
    );
    await this.contexto.carregar(grupo);
  }

  tentarDeNovo(): void {
    void this.carregar(this.grupo());
  }

  mudarFiltros(estado: EstadoPainel): void {
    void this.router.navigate([], { relativeTo: this.rota, queryParams: parametrosDoEstadoPainel(estado) });
  }
}
