import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Select } from 'primeng/select';
import { Skeleton } from 'primeng/skeleton';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { AprovacaoService, FiltrosFila, Pendente } from '../../core/aprovacao/aprovacao.service';
import { PendentesService } from '../../core/aprovacao/pendentes.service';
import {
  TODAS_AS_AREAS,
  areaInicialDaFila,
  formatarAguardando,
  opcoesDaFila,
} from '../../core/aprovacao/regras-aprovacao';
import { AreaAtualService } from '../../core/metadados/area-atual.service';
import { Listagem } from '../../core/metadados/listagens';
import { MetadadosService } from '../../core/metadados/metadados.service';
import { Entidade } from '../../core/metadados/modelo';
import { nomePlural } from '../../core/metadados/regras-metadados';
import { PaginaDe } from '../../core/registros/modelo-registro';
import { formatarDataHoraSistema } from '../../shared/formatacao/formatar-valor';
import { EstadoErroComponent } from '../../shared/estados/estado-erro.component';
import { EstadoVazioComponent } from '../../shared/estados/estado-vazio.component';
import { CabecalhoPaginaComponent } from '../../shared/pagina/cabecalho-pagina.component';
import { cliqueAbreLinha } from '../../shared/tabelas/colunas-tabela';
import { DensidadeService } from '../../shared/tabelas/densidade.service';
import { SombraRolagemDirective } from '../../shared/tabelas/sombra-rolagem.directive';

/**
 * /aprovacoes — a fila de itens pendentes que o usuário pode decidir (RN36): os mais antigos primeiro, com tipo de item, rótulo,
 * equipe, autor e há quanto tempo aguarda. Filtros por área de gestão, tipo de item e equipe na URL (?grupo=&entidade=&equipeId=&pagina=).
 * Sem área na URL, a fila abre filtrada na ÁREA ATUAL, com a opção "Todas as áreas" (?todas=1); escolher uma área
 * aqui muda a área atual, e trocar no cabeçalho muda aqui.
 * Abrir um item leva ao detalhe, onde ficam Aprovar e Rejeitar; depois da decisão, o detalhe volta para esta fila.
 */
@Component({
  selector: 'app-fila-aprovacao',
  imports: [
    FormsModule,
    RouterLink,
    Button,
    Select,
    Skeleton,
    TableModule,
    CabecalhoPaginaComponent,
    EstadoErroComponent,
    EstadoVazioComponent,
    SombraRolagemDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './fila-aprovacao.component.html',
})
export class FilaAprovacaoComponent {
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly aprovacao = inject(AprovacaoService);
  private readonly pendentes = inject(PendentesService);
  private readonly metadados = inject(MetadadosService);
  private readonly areaAtual = inject(AreaAtualService);
  readonly densidade = inject(DensidadeService);

  private readonly parametros = toSignal(this.rota.queryParamMap);
  readonly filtros = computed<FiltrosFila>(() => ({
    grupo: this.parametros()?.get('grupo') ?? undefined,
    entidade: this.parametros()?.get('entidade') ?? undefined,
    equipeId: this.parametros()?.get('equipeId') ?? undefined,
  }));
  readonly pagina = computed(() => Math.max(1, Number(this.parametros()?.get('pagina') ?? 1) || 1));
  readonly todas = computed(() => this.parametros()?.get('todas') === '1');
  /** O filtro de área já foi decidido (a área atual entra na URL antes da primeira consulta). */
  private readonly pronto = signal(false);
  readonly TODAS_AS_AREAS = TODAS_AS_AREAS;
  /** As opções do filtro de área: "Todas as áreas" e as áreas em que o usuário tem equipe. */
  readonly opcoesArea = computed(() => [{ chave: TODAS_AS_AREAS, nome: 'Todas as áreas' }, ...this.listagens()]);
  readonly areaEscolhida = computed(() => this.filtros().grupo ?? (this.todas() ? TODAS_AS_AREAS : null));
  readonly tamanho = 20;

  readonly resultado = signal<PaginaDe<Pendente> | null>(null);
  readonly erro = signal(false);
  private readonly todasAsListagens = signal<Listagem[]>([]);
  /** RN91: só listagens em que o usuário tem equipe e, no filtro de equipe, as equipes dele na listagem filtrada. */
  private readonly opcoes = computed(() => opcoesDaFila(this.todasAsListagens(), this.filtros().grupo));
  readonly listagens = computed(() => this.opcoes().listagens);
  readonly equipes = computed(() => this.opcoes().equipes);
  readonly entidades = signal<Entidade[]>([]);
  readonly agora = signal(new Date());
  readonly formatarDataHoraSistema = formatarDataHoraSistema;

  constructor() {
    void this.carregarOpcoes();
    effect(() => {
      const [filtros, pagina, pronto] = [this.filtros(), this.pagina(), this.pronto()];
      if (pronto) {
        untracked(() => void this.carregar(filtros, pagina));
      }
    });
    // A área trocada no seletor do cabeçalho vale aqui também (menos com "Todas as áreas").
    let anterior: string | null | undefined;
    effect(() => {
      const area = this.areaAtual.grupo();
      const primeira = anterior === undefined;
      anterior = area;
      if (!primeira && area && !this.todas() && area !== this.filtros().grupo) {
        untracked(() => this.filtrar({ grupo: area }));
      }
    });
    effect(() => {
      const grupo = this.filtros().grupo;
      untracked(() => void this.carregarEntidades(grupo));
    });
  }

  private async carregarOpcoes(): Promise<void> {
    try {
      this.todasAsListagens.set(await this.metadados.listagens());
      const inicial = areaInicialDaFila(
        { grupo: this.filtros().grupo, todas: this.parametros()?.get('todas') },
        this.areaAtual.grupo(),
        this.todasAsListagens(),
      );
      if (inicial) {
        await this.router.navigate([], {
          relativeTo: this.rota,
          queryParams: { grupo: inicial },
          queryParamsHandling: 'merge',
          replaceUrl: true,
        });
      }
    } finally {
      this.pronto.set(true);
    }
  }

  /** O filtro de área: uma área (que vira a área atual) ou "Todas as áreas". */
  escolherArea(valor: string | null): void {
    if (valor === TODAS_AS_AREAS || !valor) {
      this.filtrar({ grupo: undefined }, true);
      return;
    }
    this.areaAtual.definir(valor);
    this.filtrar({ grupo: valor });
  }

  abrir(item: Pendente, evento: MouseEvent): void {
    if (cliqueAbreLinha(evento.target as Element, window.getSelection()?.toString() ?? '')) {
      void this.router.navigate(['/', item.grupo, item.entidade.chave, item.id], { queryParams: { daFila: 1 } });
    }
  }

  private async carregarEntidades(grupo: string | undefined): Promise<void> {
    this.entidades.set(grupo ? (await this.metadados.entidades(grupo)).filter((e) => e.escopoEquipe) : []);
  }

  async carregar(filtros = this.filtros(), pagina = this.pagina()): Promise<void> {
    this.erro.set(false);
    this.pendentes.urlDaFila.set(this.router.url);
    try {
      this.resultado.set(await this.aprovacao.pendentes(filtros, pagina, this.tamanho));
      this.agora.set(new Date());
      void this.pendentes.atualizar();
    } catch {
      this.erro.set(true);
    }
  }

  filtrar(mudanca: Partial<FiltrosFila>, todas = false): void {
    const filtros = { ...this.filtros(), ...mudanca };
    // Trocar a listagem limpa o tipo de item (ele é de outra listagem) e a equipe, se ela não estiver habilitada na nova (RN91).
    if ('grupo' in mudanca) {
      filtros.entidade = undefined;
      if (!opcoesDaFila(this.todasAsListagens(), filtros.grupo).equipes.some((e) => e.id === filtros.equipeId)) {
        filtros.equipeId = undefined;
      }
    }
    void this.router.navigate([], {
      relativeTo: this.rota,
      queryParams: {
        grupo: filtros.grupo || undefined,
        todas: !filtros.grupo && (todas || this.todas()) ? '1' : undefined,
        entidade: filtros.entidade || undefined,
        equipeId: filtros.equipeId || undefined,
      },
    });
  }

  aoMudarPagina(evento: TableLazyLoadEvent): void {
    const pagina = Math.floor((evento.first ?? 0) / this.tamanho) + 1;
    if (pagina !== this.pagina()) {
      void this.router.navigate([], {
        relativeTo: this.rota,
        queryParams: { pagina: pagina > 1 ? pagina : undefined },
        queryParamsHandling: 'merge',
      });
    }
  }

  aguardando(item: Pendente): string {
    return formatarAguardando(item.criadoEm, this.agora());
  }

  nomeDoTipo(entidade: Entidade): string {
    return nomePlural(entidade);
  }
}
