import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MenuItem, MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { Menu } from 'primeng/menu';
import { Message } from 'primeng/message';
import { Skeleton } from 'primeng/skeleton';
import { SplitButton } from 'primeng/splitbutton';
import { Tag } from 'primeng/tag';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { SITUACOES, mostraSituacao, situacaoDe } from '../../../core/aprovacao/regras-aprovacao';
import { AuthService } from '../../../core/auth/auth.service';
import { Listagem, mostraEquipe, nomeDaEquipe } from '../../../core/metadados/listagens';
import { MetadadosService } from '../../../core/metadados/metadados.service';
import { Entidade } from '../../../core/metadados/modelo';
import { classeIcone, nomePlural, podeOrdenarPor, referenciaComoLink } from '../../../core/metadados/regras-metadados';
import { PermissoesService } from '../../../core/permissoes/permissoes.service';
import { PaginaDe, Registro } from '../../../core/registros/modelo-registro';
import { RegistrosService } from '../../../core/registros/registros.service';
import { MediaEstrelasComponent } from '../../../shared/avaliacao/media-estrelas.component';
import { ValorCampoComponent } from '../../../shared/exibicao/valor-campo.component';
import { FormatoExportacao, itensExportacao } from '../../../shared/exportacao/opcoes-exportacao';
import { formatarDataHoraSistema } from '../../../shared/formatacao/formatar-valor';
import { ValorCampo } from '../../../shared/formulario/valores';
import { EstadoErroComponent } from '../../../shared/estados/estado-erro.component';
import { EstadoVazioComponent } from '../../../shared/estados/estado-vazio.component';
import { PassoTrilha } from '../../../shared/navegacao/trilha.component';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';
import { AlternarDensidadeComponent } from '../../../shared/tabelas/alternar-densidade.component';
import { ColunaRedimensionavelDirective } from '../../../shared/tabelas/coluna-redimensionavel.directive';
import { cliqueAbreLinha, colunasQueCabem, colunasVisiveis } from '../../../shared/tabelas/colunas-tabela';
import { DensidadeService } from '../../../shared/tabelas/densidade.service';
import { MenuAcoesLinhaComponent } from '../../../shared/tabelas/menu-acoes-linha.component';
import { gravarLargura, lerLarguras } from '../../../shared/tabelas/preferencias-tabela';
import { SeletorColunasComponent } from '../../../shared/tabelas/seletor-colunas.component';
import { SombraRolagemDirective } from '../../../shared/tabelas/sombra-rolagem.directive';
import { environment } from '../../../../environments/environment';
import { baixarArquivo } from '../comum/baixar-arquivo';
import { ExclusaoService } from '../comum/exclusao.service';
import { esquecerColunas, gravarColunas, lerColunas } from '../comum/preferencias-colunas';
import { rotuloDoRegistro } from '../comum/rotulos';
import { FiltrosRegistrosComponent } from '../filtros/filtros-registros.component';
import {
  CHAVE_CRIADO_EM,
  CHAVE_SITUACAO,
  ColunaRegistro,
  colunasCandidatasAoPadrao,
  colunasDaEntidade,
} from './colunas-registros';
import {
  CHAVE_EQUIPE,
  PARAMETRO_PAINEL_FILTROS,
  EstadoListagem,
  FiltroDoCampo,
  ORDENACAO_PADRAO,
  estadoDosParametros,
  filtroDeEquipe,
  filtrosDaEntidade,
  lerOrdenacao,
  parametrosDaApi,
  parametrosDaListaDeFiltros,
  parametrosDaListagem,
  valoresDaListaDeFiltros,
} from '../filtros/filtros-url';

/**
 * A listagem GENÉRICA de qualquer entidade (/:grupo/:entidade), montada pelos metadados. Paginação, ordenação e
 * filtros são feitos pela API (p-table em modo lazy) e ficam na URL: a URL é a fonte da verdade, e cada
 * mudança na tabela vira uma navegação (docs/adr/0037). Título e trilha no plural; tabela na densidade escolhida (compacta por
 * padrão); URLs e referências para itens por equipe como link (shared/exibicao/valor-campo.component.ts).
 * Tabelas com muitas colunas (docs/design/guia-de-interface.md → "Tabelas"): a LINHA é clicável (abre o item; o campo de exibição,
 * fixo à esquerda, continua sendo um link de verdade para o teclado e o leitor de tela); as ações ficam no "⋯" preso à direita; uma
 * sombra indica as colunas fora da área visível; na primeira visita, só as colunas que cabem (pelo menos 6 em 1366 px), e a escolha,
 * a ordem e as larguras ficam salvas por tipo de item.
 * Aprovação e avaliação só onde o recurso está ativo (ADR 0045): a coluna Situação (sempre visível) e o filtro por ela, se alguma
 * equipe que o usuário vê tem a aprovação ligada; colunas opcionais com a média de cada critério de estrelas, que ordenam e filtram
 * por `avaliacao.<critério>` (RN48).
 * Equipe (RN91): nas entidades por equipe, a coluna (visível por padrão, no seletor de colunas) e o filtro de Equipe aparecem para
 * quem tem mais de uma equipe NESTA listagem, ou é ADMINISTRADOR; o filtro oferece só as equipes do usuário na listagem.
 */
@Component({
  selector: 'app-listagem-registros',
  imports: [
    FormsModule,
    RouterLink,
    Button,
    Menu,
    Message,
    Skeleton,
    SplitButton,
    TableModule,
    Tag,
    MediaEstrelasComponent,
    FiltrosRegistrosComponent,
    ValorCampoComponent,
    AlternarDensidadeComponent,
    CabecalhoPaginaComponent,
    ColunaRedimensionavelDirective,
    EstadoErroComponent,
    EstadoVazioComponent,
    MenuAcoesLinhaComponent,
    SeletorColunasComponent,
    SombraRolagemDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './listagem-registros.component.html',
})
export class ListagemRegistrosComponent {
  /** Parâmetros da rota (withComponentInputBinding). */
  readonly grupo = input.required<string>();
  readonly entidade = input.required<string>();

  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly metadados = inject(MetadadosService);
  private readonly registros = inject(RegistrosService);
  private readonly exclusao = inject(ExclusaoService);
  private readonly mensagens = inject(MessageService);
  private readonly auth = inject(AuthService);
  private readonly elemento = inject<ElementRef<HTMLElement>>(ElementRef);
  readonly permissoes = inject(PermissoesService);
  readonly densidade = inject(DensidadeService);

  private readonly parametrosUrl = toSignal(this.rota.queryParams, { initialValue: {} });

  readonly meta = signal<Entidade | null>(null);
  /** A listagem atual, com as equipes do usuário nela (RN91). */
  readonly listagem = signal<Listagem | null>(null);
  readonly minhasEquipes = computed(() => this.listagem()?.minhasEquipes ?? []);
  /** RN93: listagem sem equipe habilitada (só o administrador chega aqui): a faixa de aviso, com o atalho para a aba Equipes. */
  readonly semEquipes = computed(() => {
    const listagem = this.listagem();
    return !!listagem && !listagem.possuiEquipes && this.permissoes.podeAdministrar();
  });
  /** Todas as entidades do grupo: para saber se uma referência é link e se dá para ordenar por ela. */
  readonly todas = signal<Entidade[]>([]);
  readonly nomeGrupo = signal('');
  readonly resultado = signal<PaginaDe<Registro> | null>(null);
  readonly carregando = signal(true);
  readonly erro = signal(false);
  readonly mostrarFiltros = signal(false);
  readonly colunasEscolhidas = signal<string[]>([]);
  readonly exportando = signal(false);

  readonly estado = computed<EstadoListagem>(() => estadoDosParametros(this.parametrosUrl()));
  /** RN91: coluna e filtro de Equipe (mais de uma equipe do usuário na listagem, ou ADMINISTRADOR). */
  readonly comEquipe = computed(() =>
    mostraEquipe(this.listagem(), this.meta()?.escopoEquipe ?? false, this.auth.usuario()?.perfil),
  );
  /** Os filtros da tela: o de Equipe (se couber) e os da entidade. */
  readonly filtros = computed<FiltroDoCampo[]>(() => {
    const meta = this.meta();
    if (!meta) {
      return [];
    }
    return [...(this.comEquipe() ? filtroDeEquipe(this.minhasEquipes()) : []), ...filtrosDaEntidade(meta)];
  });
  readonly valoresFiltros = computed<Record<string, ValorCampo>>(() =>
    valoresDaListaDeFiltros(this.filtros(), this.parametrosUrl()),
  );
  /** Só os filtros (sem página e ordenação), no formato da API. */
  readonly parametrosFiltro = computed(() => parametrosDaListaDeFiltros(this.filtros(), this.valoresFiltros()));
  readonly quantidadeFiltros = computed(() => Object.keys(this.parametrosFiltro()).length);

  readonly comSituacao = computed(() => (this.meta()?.equipesComAprovacao.length ?? 0) > 0);
  /** Todas as colunas que a tabela oferece (no seletor), na ordem padrão. */
  readonly todasColunas = computed<ColunaRegistro[]>(() => {
    const meta = this.meta();
    return meta ? colunasDaEntidade(meta, { comEquipe: this.comEquipe(), comSituacao: this.comSituacao() }) : [];
  });
  /** As visíveis, na ordem da pessoa (a fixa sempre primeiro). */
  readonly colunas = computed(() => colunasVisiveis(this.todasColunas(), this.colunasEscolhidas()) as ColunaRegistro[]);
  readonly larguras = signal<Record<string, number>>({});
  /** Colunas da tabela além das escolhidas: a das ações. Para o colspan das linhas vazias. */
  readonly totalColunas = computed(() => this.colunas().length + 1);
  /** As ações ("⋯") de cada linha, montadas uma vez por página. */
  readonly acoesPorLinha = computed(() => {
    const meta = this.meta();
    const mapa = new Map<string, MenuItem[]>();
    for (const registro of this.resultado()?.itens ?? []) {
      mapa.set(registro.id, meta ? this.montarAcoes(meta, registro) : []);
    }
    return mapa;
  });
  readonly ordenacao = computed(() => lerOrdenacao(this.estado().ordenacao));
  readonly titulo = computed(() => {
    const meta = this.meta();
    return meta ? nomePlural(meta) : '';
  });
  readonly icone = computed(() => classeIcone(this.meta()));
  readonly trilha = computed<PassoTrilha[]>(() => [
    { rotulo: this.nomeGrupo(), link: ['/', this.grupo()] },
    { rotulo: this.titulo() },
  ]);
  /** Por coluna: se ordena (RN19; as médias e Criado em também) e se a referência é link. */
  readonly ordenaveis = computed(
    () =>
      new Set(
        this.colunas()
          .filter((c) => (c.campo ? podeOrdenarPor(c.campo, this.todas()) : c.criterio || c.chave === CHAVE_CRIADO_EM))
          .map((c) => c.chave),
      ),
  );
  readonly links = computed(
    () =>
      new Set(
        this.colunas()
          .filter((c) => c.campo && referenciaComoLink(c.campo, this.todas()))
          .map((c) => c.chave),
      ),
  );
  readonly CHAVE_EQUIPE = CHAVE_EQUIPE;
  readonly CHAVE_SITUACAO = CHAVE_SITUACAO;
  readonly CHAVE_CRIADO_EM = CHAVE_CRIADO_EM;
  readonly linhasEsqueleto = Array.from({ length: 5 }, (_, i) => i);
  readonly opcoesTamanho = environment.opcoesTamanhoPagina;

  readonly itensExportar = itensExportacao((formato) => void this.exportar(formato));
  /** RN97: "Importar" abre o assistente; o menu ao lado leva às importações anteriores (RN104). */
  readonly itensImportar = computed<MenuItem[]>(() => [
    {
      label: 'Importações anteriores',
      icon: 'pi pi-history',
      routerLink: ['/', this.grupo(), this.entidade(), 'importacoes'],
    },
  ]);

  readonly formatarDataHoraSistema = formatarDataHoraSistema;

  constructor() {
    // Troca de entidade (menu): carrega os metadados e as colunas salvas.
    effect(() => {
      const grupo = this.grupo();
      const entidade = this.entidade();
      untracked(() => void this.carregarMetadados(grupo, entidade));
    });
    // Qualquer mudança na URL (filtros, página, ordenação): consulta a API de novo.
    effect(() => {
      const meta = this.meta();
      const parametros = { ...this.parametrosFiltro() };
      const estado = this.estado();
      if (meta) {
        untracked(() => void this.consultar(meta, parametros, estado));
      }
    });
  }

  private async carregarMetadados(grupo: string, chave: string): Promise<void> {
    this.meta.set(null);
    this.resultado.set(null);
    this.carregando.set(true);
    const meta = await this.metadados.entidade(grupo, chave);
    if (!meta) {
      void this.router.navigate(['/nao-encontrado'], { skipLocationChange: true });
      return;
    }
    this.todas.set(await this.metadados.entidades(grupo));
    this.listagem.set(await this.metadados.listagem(grupo));
    this.meta.set(meta);
    this.larguras.set(lerLarguras(`${grupo}.${chave}`));
    const salvas = lerColunas(grupo, chave);
    this.colunasEscolhidas.set(salvas && salvas.length > 0 ? salvas : this.colunasQueCabem());
    // "Ver todos na listagem" de uma seção de relacionados (RN94) chega com o painel de filtros aberto, para o filtro aparecer.
    if (this.rota.snapshot.queryParamMap.get(PARAMETRO_PAINEL_FILTROS) === 'aberto') {
      this.mostrarFiltros.set(true);
    }
    void this.metadados.nomeDoGrupo(grupo).then(
      (nome) => this.nomeGrupo.set(nome),
      () => this.nomeGrupo.set(grupo),
    );
  }

  private async consultar(meta: Entidade, filtros: Record<string, string>, estado: EstadoListagem): Promise<void> {
    this.carregando.set(true);
    this.erro.set(false);
    try {
      this.resultado.set(await this.registros.listar(this.grupo(), meta.chave, parametrosDaApi(filtros, estado)));
    } catch {
      this.erro.set(true);
    } finally {
      this.carregando.set(false);
    }
  }

  tentarDeNovo(): void {
    const meta = this.meta();
    if (meta) {
      void this.consultar(meta, this.parametrosFiltro(), this.estado());
    }
  }

  /** Página, tamanho ou ordenação mudaram na tabela: vira navegação (a URL guarda o estado). */
  aoMudarTabela(evento: TableLazyLoadEvent): void {
    const tamanho = evento.rows ?? this.estado().tamanho;
    const campo = typeof evento.sortField === 'string' ? evento.sortField : null;
    const novo: EstadoListagem = {
      pagina: Math.floor((evento.first ?? 0) / tamanho) + 1,
      tamanho,
      ordenacao: campo ? `${evento.sortOrder === -1 ? '-' : ''}${campo}` : ORDENACAO_PADRAO,
    };
    const atual = this.estado();
    if (novo.tamanho !== atual.tamanho || novo.ordenacao !== atual.ordenacao) {
      novo.pagina = 1;
    }
    if (novo.pagina !== atual.pagina || novo.tamanho !== atual.tamanho || novo.ordenacao !== atual.ordenacao) {
      this.navegar(this.parametrosFiltro(), novo);
    }
  }

  aplicarFiltros(filtros: Record<string, string>): void {
    this.navegar(filtros, { ...this.estado(), pagina: 1 });
  }

  limparFiltros(): void {
    this.aplicarFiltros({});
  }

  /** Primeira visita: as colunas que cabem confortavelmente na largura da área de conteúdo (pelo menos 6). */
  private colunasQueCabem(): string[] {
    const conteudo = this.elemento.nativeElement.closest('main') ?? this.elemento.nativeElement.parentElement;
    const disponivel = (conteudo?.clientWidth || window.innerWidth - 300) - 64;
    const fixa = this.todasColunas().find((c) => c.fixa)?.chave;
    return colunasQueCabem(colunasCandidatasAoPadrao(this.todasColunas()), disponivel).filter((c) => c !== fixa);
  }

  /** Mostrar, esconder ou reordenar colunas (seletor): vale na hora e fica salvo para este tipo de item. */
  alterarColunas(colunas: string[]): void {
    this.colunasEscolhidas.set(colunas);
    gravarColunas(this.grupo(), this.entidade(), colunas);
  }

  restaurarColunas(): void {
    esquecerColunas(this.grupo(), this.entidade());
    this.colunasEscolhidas.set(this.colunasQueCabem());
  }

  larguraDe(coluna: ColunaRegistro): number {
    return this.larguras()[coluna.chave] ?? coluna.largura;
  }

  redimensionar(coluna: ColunaRegistro, largura: number): void {
    this.larguras.set(gravarLargura(`${this.grupo()}.${this.entidade()}`, coluna.chave, largura));
  }

  /** A linha inteira abre o item (menos cliques em links, botões e o "⋯"); Ctrl/⌘ abre numa nova aba. */
  abrirLinha(registro: Registro, evento: MouseEvent): void {
    if (!cliqueAbreLinha(evento.target as Element, window.getSelection()?.toString() ?? '')) {
      return;
    }
    const url = this.router.createUrlTree([registro.id], { relativeTo: this.rota });
    if (evento.ctrlKey || evento.metaKey) {
      window.open(this.router.serializeUrl(url), '_blank', 'noopener');
      return;
    }
    void this.router.navigateByUrl(url);
  }

  private montarAcoes(meta: Entidade, registro: Registro): MenuItem[] {
    const acoes: MenuItem[] = [];
    if (this.permissoes.podeEditar(meta, registro)) {
      acoes.push({
        label: 'Editar',
        icon: 'pi pi-pencil',
        routerLink: ['/', this.grupo(), meta.chave, registro.id, 'editar'],
      });
    }
    if (this.permissoes.podeExcluir(meta, registro)) {
      acoes.push({
        label: 'Excluir',
        icon: 'pi pi-trash',
        styleClass: 'acao-perigosa',
        command: () => void this.excluir(registro),
      });
    }
    return acoes;
  }

  async excluir(registro: Registro): Promise<void> {
    const meta = this.meta();
    if (meta && (await this.exclusao.excluir(this.grupo(), meta, registro))) {
      this.tentarDeNovo();
    }
  }

  importar(): void {
    void this.router.navigate(['/', this.grupo(), this.entidade(), 'importar']);
  }

  async exportar(formato: FormatoExportacao): Promise<void> {
    this.exportando.set(true);
    try {
      const { arquivo, nome } = await this.registros.exportar(this.grupo(), this.entidade(), formato, {
        ...this.parametrosFiltro(),
        ordenacao: this.estado().ordenacao,
      });
      baixarArquivo(arquivo, nome);
      this.mensagens.add({ severity: 'success', summary: `Arquivo ${nome} gerado.` });
    } catch {
      // O interceptor já mostrou a mensagem (ex.: EXPORTACAO_MUITO_GRANDE).
    } finally {
      this.exportando.set(false);
    }
  }

  /** O rótulo do item, para o nome acessível das ações da linha ("Ver Bateria 05 Alfa"). */
  rotulo(registro: Registro): string {
    const meta = this.meta();
    return meta ? rotuloDoRegistro(meta, registro) : '';
  }

  mostraSituacao(meta: Entidade, registro: Registro): boolean {
    return mostraSituacao(meta, registro);
  }

  situacao(registro: Registro): { rotulo: string; severidade: 'warn' | 'success' | 'danger' } {
    return SITUACOES[situacaoDe(registro.situacaoAprovacao)];
  }

  mediaDe(registro: Registro, criterio: string): number | null {
    return registro.avaliacao?.medias?.[criterio] ?? null;
  }

  temFiltrosDisponiveis(): boolean {
    return this.filtros().length > 0;
  }

  /** O nome da equipe do item (RN91); para o administrador, um item de equipe não habilitada (RN92) aparece como tal. */
  nomeEquipe(registro: Registro): string {
    return nomeDaEquipe(this.minhasEquipes(), registro.equipeId) ?? '';
  }

  private navegar(filtros: Record<string, string>, estado: EstadoListagem): void {
    void this.router.navigate([], { relativeTo: this.rota, queryParams: parametrosDaListagem(filtros, estado) });
  }
}
