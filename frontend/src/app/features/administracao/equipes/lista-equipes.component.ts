import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { Skeleton } from 'primeng/skeleton';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { Tag } from 'primeng/tag';
import { AdministracaoService } from '../../../core/administracao/administracao.service';
import { EquipeAdmin } from '../../../core/administracao/modelo-administracao';
import { PaginaDe } from '../../../core/registros/modelo-registro';
import { environment } from '../../../../environments/environment';
import { AcoesAdministracaoService } from '../comum/acoes-usuario.service';
import {
  FiltrosEquipes,
  filtrosEquipesDaUrl,
  parametrosApiEquipes,
  parametrosDosFiltrosEquipes,
} from '../modelo/filtros-url';
import { AlternarDensidadeComponent } from '../../../shared/tabelas/alternar-densidade.component';
import { DensidadeService } from '../../../shared/tabelas/densidade.service';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';
import { EstadoErroComponent } from '../../../shared/estados/estado-erro.component';
import { EstadoVazioComponent } from '../../../shared/estados/estado-vazio.component';
import { MenuAcoesLinhaComponent } from '../../../shared/tabelas/menu-acoes-linha.component';
import { SombraRolagemDirective } from '../../../shared/tabelas/sombra-rolagem.directive';
import { cliqueAbreLinha } from '../../../shared/tabelas/colunas-tabela';

/** /configuracoes/equipes — as equipes, com situação e membros ativos; busca por nome e filtro de situação NA URL. */
@Component({
  selector: 'app-lista-equipes',
  imports: [
    SombraRolagemDirective,
    MenuAcoesLinhaComponent,
    EstadoVazioComponent,
    EstadoErroComponent,
    CabecalhoPaginaComponent,
    FormsModule,
    RouterLink,
    Button,
    InputText,
    Select,
    Skeleton,
    TableModule,
    Tag,
    AlternarDensidadeComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './lista-equipes.component.html',
})
export class ListaEquipesComponent {
  readonly densidade = inject(DensidadeService);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly administracao = inject(AdministracaoService);
  private readonly acoes = inject(AcoesAdministracaoService);

  private readonly parametrosUrl = toSignal(this.rota.queryParams, { initialValue: {} });
  readonly filtros = computed<FiltrosEquipes>(() => filtrosEquipesDaUrl(this.parametrosUrl()));

  readonly resultado = signal<PaginaDe<EquipeAdmin> | null>(null);
  /** As ações ("⋯") de cada linha: editar e ativar ou desativar. */
  readonly acoesPorLinha = computed(() => {
    const mapa = new Map<string, MenuItem[]>();
    for (const equipe of this.resultado()?.itens ?? []) {
      mapa.set(equipe.id, [
        { label: 'Editar', icon: 'pi pi-pencil', routerLink: ['/configuracoes/equipes', equipe.id, 'editar'] },
        equipe.ativa
          ? {
              label: 'Desativar',
              icon: 'pi pi-ban',
              styleClass: 'acao-perigosa',
              command: () => void this.alterarAtivacao(equipe),
            }
          : { label: 'Ativar', icon: 'pi pi-check-circle', command: () => void this.alterarAtivacao(equipe) },
      ]);
    }
    return mapa;
  });
  readonly carregando = signal(true);
  readonly erro = signal(false);
  busca = '';

  readonly opcoesSituacao = [
    { valor: true, rotulo: 'Ativas' },
    { valor: false, rotulo: 'Inativas' },
  ];
  readonly opcoesTamanho = environment.opcoesTamanhoPagina;
  readonly linhasEsqueleto = Array.from({ length: 4 }, (_, i) => i);

  constructor() {
    effect(() => {
      const filtros = this.filtros();
      untracked(() => {
        this.busca = filtros.busca;
        void this.consultar(filtros);
      });
    });
  }

  private async consultar(filtros: FiltrosEquipes): Promise<void> {
    this.carregando.set(true);
    this.erro.set(false);
    try {
      this.resultado.set(await this.administracao.listarEquipes(parametrosApiEquipes(filtros)));
    } catch {
      this.erro.set(true);
    } finally {
      this.carregando.set(false);
    }
  }

  tentarDeNovo(): void {
    void this.consultar(this.filtros());
  }

  pesquisar(): void {
    this.navegar({ ...this.filtros(), busca: this.busca, pagina: 1 });
  }

  filtrarSituacao(ativa: boolean | null): void {
    this.navegar({ ...this.filtros(), ativa, pagina: 1 });
  }

  aoMudarTabela(evento: TableLazyLoadEvent): void {
    const atual = this.filtros();
    const tamanho = evento.rows ?? atual.tamanho;
    const pagina = tamanho !== atual.tamanho ? 1 : Math.floor((evento.first ?? 0) / tamanho) + 1;
    if (pagina !== atual.pagina || tamanho !== atual.tamanho) {
      this.navegar({ ...atual, pagina, tamanho });
    }
  }

  /** A linha abre o detalhe da equipe (menos cliques no link e no "⋯"). */
  abrir(equipe: EquipeAdmin, evento: MouseEvent): void {
    if (cliqueAbreLinha(evento.target as Element, window.getSelection()?.toString() ?? '')) {
      void this.router.navigate([equipe.id], { relativeTo: this.rota });
    }
  }

  async alterarAtivacao(equipe: EquipeAdmin): Promise<void> {
    if (await this.acoes.alterarAtivacaoEquipe(equipe, !equipe.ativa)) {
      this.tentarDeNovo();
    }
  }

  private navegar(filtros: FiltrosEquipes): void {
    void this.router.navigate([], { relativeTo: this.rota, queryParams: parametrosDosFiltrosEquipes(filtros) });
  }
}
