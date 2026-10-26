import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
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
import { EquipeAdmin, UsuarioAdmin } from '../../../core/administracao/modelo-administracao';
import { NOMES_PERFIL } from '../../../core/auth/usuario';
import { PaginaDe } from '../../../core/registros/modelo-registro';
import { formatarDataHoraSistema } from '../../../shared/formatacao/formatar-valor';
import { cliqueAbreLinha } from '../../../shared/tabelas/colunas-tabela';
import { environment } from '../../../../environments/environment';
import { AcoesAdministracaoService } from '../comum/acoes-usuario.service';
import { severidadePerfil } from '../comum/apresentacao';
import { SenhaProvisoriaDialogComponent } from '../comum/senha-provisoria-dialog.component';
import {
  FiltrosUsuarios,
  ORDENACAO_USUARIOS_PADRAO,
  filtrosUsuariosDaUrl,
  parametrosApiUsuarios,
  parametrosDosFiltrosUsuarios,
} from '../modelo/filtros-url';
import { PERFIS } from '../modelo/perfis';
import { situacaoDoUsuario } from '../modelo/situacao';
import { AlternarDensidadeComponent } from '../../../shared/tabelas/alternar-densidade.component';
import { DensidadeService } from '../../../shared/tabelas/densidade.service';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';
import { EstadoErroComponent } from '../../../shared/estados/estado-erro.component';
import { EstadoVazioComponent } from '../../../shared/estados/estado-vazio.component';
import { MenuAcoesLinhaComponent } from '../../../shared/tabelas/menu-acoes-linha.component';
import { SombraRolagemDirective } from '../../../shared/tabelas/sombra-rolagem.directive';
import { DataRelativaComponent } from '../../../shared/formatacao/data-relativa.component';

/**
 * /configuracoes/usuarios — os usuários (só ADMINISTRADOR), em p-table lazy: busca por matrícula ou nome, filtros de perfil, equipe e
 * situação, página e ordenação NA URL (como a listagem de registros). Ações por linha: ver, redefinir senha (com o diálogo da
 * senha provisória) e ativar/desativar.
 */
@Component({
  selector: 'app-lista-usuarios',
  imports: [
    DataRelativaComponent,
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
    SenhaProvisoriaDialogComponent,
    AlternarDensidadeComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './lista-usuarios.component.html',
})
export class ListaUsuariosComponent {
  readonly densidade = inject(DensidadeService);
  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly administracao = inject(AdministracaoService);
  private readonly acoes = inject(AcoesAdministracaoService);
  private readonly dialogoSenha = viewChild.required(SenhaProvisoriaDialogComponent);

  private readonly parametrosUrl = toSignal(this.rota.queryParams, { initialValue: {} });
  readonly filtros = computed<FiltrosUsuarios>(() => filtrosUsuariosDaUrl(this.parametrosUrl()));

  readonly resultado = signal<PaginaDe<UsuarioAdmin> | null>(null);
  /** As ações ("⋯") de cada linha: editar, redefinir a senha e ativar ou desativar. */
  readonly acoesPorLinha = computed(() => {
    const mapa = new Map<string, MenuItem[]>();
    for (const usuario of this.resultado()?.itens ?? []) {
      mapa.set(usuario.id, [
        { label: 'Editar', icon: 'pi pi-pencil', routerLink: ['/configuracoes/usuarios', usuario.id, 'editar'] },
        { label: 'Redefinir senha', icon: 'pi pi-key', command: () => void this.redefinirSenha(usuario) },
        usuario.ativo
          ? {
              label: 'Desativar',
              icon: 'pi pi-ban',
              styleClass: 'acao-perigosa',
              command: () => void this.alterarAtivacao(usuario),
            }
          : { label: 'Ativar', icon: 'pi pi-check-circle', command: () => void this.alterarAtivacao(usuario) },
      ]);
    }
    return mapa;
  });
  readonly carregando = signal(true);
  readonly erro = signal(false);
  readonly equipes = signal<EquipeAdmin[]>([]);
  /** O texto da busca enquanto é digitado (vai para a URL ao pesquisar). */
  busca = '';

  readonly opcoesPerfil = PERFIS.map((p) => ({ valor: p.valor, rotulo: p.rotulo }));
  readonly opcoesEquipe = computed(() =>
    this.equipes().map((e) => ({ valor: e.id, rotulo: e.ativa ? e.nome : `${e.nome} (inativa)` })),
  );
  readonly opcoesSituacao = [
    { valor: true, rotulo: 'Ativos' },
    { valor: false, rotulo: 'Inativos' },
  ];
  readonly opcoesTamanho = environment.opcoesTamanhoPagina;
  readonly linhasEsqueleto = Array.from({ length: 5 }, (_, i) => i);
  readonly ordenacao = computed(() => {
    const o = this.filtros().ordenacao;
    return { campo: o.replace(/^-/, ''), ordem: o.startsWith('-') ? -1 : 1 };
  });
  readonly agora = signal(new Date());

  nomePerfil(usuario: UsuarioAdmin): string {
    return NOMES_PERFIL[usuario.perfil];
  }
  readonly severidadePerfil = severidadePerfil;
  readonly formatarDataHoraSistema = formatarDataHoraSistema;

  constructor() {
    effect(() => {
      const filtros = this.filtros();
      untracked(() => {
        this.busca = filtros.busca;
        void this.consultar(filtros);
      });
    });
    void this.carregarEquipes();
  }

  private async carregarEquipes(): Promise<void> {
    try {
      this.equipes.set(await this.administracao.todasAsEquipes());
    } catch {
      // Sem a lista, o filtro de equipe fica vazio; o resto da tela funciona.
    }
  }

  private async consultar(filtros: FiltrosUsuarios): Promise<void> {
    this.carregando.set(true);
    this.erro.set(false);
    try {
      this.resultado.set(await this.administracao.listarUsuarios(parametrosApiUsuarios(filtros)));
      this.agora.set(new Date());
    } catch {
      this.erro.set(true);
    } finally {
      this.carregando.set(false);
    }
  }

  tentarDeNovo(): void {
    void this.consultar(this.filtros());
  }

  situacao(usuario: UsuarioAdmin) {
    return situacaoDoUsuario(usuario, this.agora());
  }

  pesquisar(): void {
    this.navegar({ ...this.filtros(), busca: this.busca, pagina: 1 });
  }

  filtrar(mudanca: Partial<FiltrosUsuarios>): void {
    this.navegar({ ...this.filtros(), ...mudanca, pagina: 1 });
  }

  limpar(): void {
    this.busca = '';
    void this.router.navigate([], { relativeTo: this.rota, queryParams: {} });
  }

  temFiltros(): boolean {
    const f = this.filtros();
    return !!f.busca || f.perfil !== null || f.equipeId !== null || f.ativo !== null;
  }

  aoMudarTabela(evento: TableLazyLoadEvent): void {
    const atual = this.filtros();
    const tamanho = evento.rows ?? atual.tamanho;
    const campo = typeof evento.sortField === 'string' ? evento.sortField : null;
    const ordenacao = campo ? `${evento.sortOrder === -1 ? '-' : ''}${campo}` : ORDENACAO_USUARIOS_PADRAO;
    let pagina = Math.floor((evento.first ?? 0) / tamanho) + 1;
    if (tamanho !== atual.tamanho || ordenacao !== atual.ordenacao) {
      pagina = 1;
    }
    if (pagina !== atual.pagina || tamanho !== atual.tamanho || ordenacao !== atual.ordenacao) {
      this.navegar({ ...atual, pagina, tamanho, ordenacao });
    }
  }

  /** As equipes numa linha só (a célula corta com reticências; o texto inteiro fica na dica). */
  textoEquipes(usuario: UsuarioAdmin): string {
    if (usuario.perfil === 'ADMINISTRADOR') {
      return 'Todas as equipes';
    }
    return usuario.equipes.map((e) => (e.ativa ? e.nome : `${e.nome} (inativa)`)).join(', ');
  }

  /** A linha abre o detalhe do usuário (menos cliques no link e no "⋯"). */
  abrir(usuario: UsuarioAdmin, evento: MouseEvent): void {
    if (cliqueAbreLinha(evento.target as Element, window.getSelection()?.toString() ?? '')) {
      void this.router.navigate([usuario.id], { relativeTo: this.rota });
    }
  }

  async redefinirSenha(usuario: UsuarioAdmin): Promise<void> {
    const senha = await this.acoes.redefinirSenha(usuario);
    if (senha) {
      // Direto para o diálogo: a senha não fica em nenhum outro lugar.
      this.dialogoSenha().mostrar(senha, `${usuario.nome} (${usuario.matricula})`);
      this.tentarDeNovo();
    }
  }

  async alterarAtivacao(usuario: UsuarioAdmin): Promise<void> {
    if (await this.acoes.alterarAtivacao(usuario, !usuario.ativo)) {
      this.tentarDeNovo();
    }
  }

  private navegar(filtros: FiltrosUsuarios): void {
    void this.router.navigate([], { relativeTo: this.rota, queryParams: parametrosDosFiltrosUsuarios(filtros) });
  }
}
