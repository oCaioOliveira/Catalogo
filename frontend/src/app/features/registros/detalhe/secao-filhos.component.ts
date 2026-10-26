import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MenuItem, MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { Message } from 'primeng/message';
import { Skeleton } from 'primeng/skeleton';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { Campo, Entidade } from '../../../core/metadados/modelo';
import { classeIcone, nomeEmFrase, nomePlural, referenciaComoLink } from '../../../core/metadados/regras-metadados';
import { PermissoesService } from '../../../core/permissoes/permissoes.service';
import { PaginaDe, Registro } from '../../../core/registros/modelo-registro';
import { RegistrosService } from '../../../core/registros/registros.service';
import { ValorCampoComponent } from '../../../shared/exibicao/valor-campo.component';
import { cliqueAbreLinha } from '../../../shared/tabelas/colunas-tabela';
import { DensidadeService } from '../../../shared/tabelas/densidade.service';
import { MenuAcoesLinhaComponent } from '../../../shared/tabelas/menu-acoes-linha.component';
import { SombraRolagemDirective } from '../../../shared/tabelas/sombra-rolagem.directive';
import { CampoFixo, FormularioRegistroComponent } from '../../../shared/formulario/formulario-registro.component';
import { ExclusaoService } from '../comum/exclusao.service';
import { rotuloDoRegistro } from '../comum/rotulos';

/**
 * Uma seção do detalhe com os itens FILHOS do registro (ex.: os cenários de um relatório). A filha é a entidade
 * com um REFERENCIA obrigatório e CASCATA para esta (regras-metadados.ts). Criar, editar e excluir acontecem num
 * diálogo, com o mesmo formulário genérico; o campo que aponta para o pai vem preenchido e travado, e a equipe
 * é a do pai. Recolhível como as outras seções de relacionados (RN94): o título traz a quantidade e a tabela só é carregada aberta.
 */
@Component({
  selector: 'app-secao-filhos',
  imports: [
    RouterLink,
    Button,
    Dialog,
    Message,
    Skeleton,
    TableModule,
    FormularioRegistroComponent,
    MenuAcoesLinhaComponent,
    SombraRolagemDirective,
    ValorCampoComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './secao-filhos.component.html',
})
export class SecaoFilhosComponent {
  readonly grupo = input.required<string>();
  readonly pai = input.required<Entidade>();
  readonly registroPai = input.required<Registro>();
  readonly filha = input.required<Entidade>();
  readonly campoPai = input.required<Campo>();
  /** Todas as entidades do grupo (referências: link ou texto). */
  readonly todas = input<Entidade[]>([]);
  /** O título com a quantidade ("Cenários de Teste (3)", relacionados.ts). Nulo = só o plural. */
  readonly tituloComQuantidade = input<string | null>(null);
  readonly aberta = input(true);
  readonly alternar = output<void>();
  /** Criou, alterou ou excluiu um filho: o detalhe atualiza as quantidades. */
  readonly alterada = output<void>();
  /** O filho nasce na equipe do pai: é nela que se confere se dá para criar (RN91). */
  readonly equipeDoPai = computed(() => {
    const equipeId = this.registroPai().equipeId;
    return equipeId ? [{ id: equipeId, nome: '' }] : [];
  });

  private readonly registros = inject(RegistrosService);
  private readonly exclusao = inject(ExclusaoService);
  private readonly mensagens = inject(MessageService);
  private readonly router = inject(Router);
  readonly permissoes = inject(PermissoesService);
  readonly densidade = inject(DensidadeService);

  readonly tamanho = 10;
  readonly pagina = signal(1);
  readonly resultado = signal<PaginaDe<Registro> | null>(null);
  readonly carregando = signal(true);
  readonly erro = signal(false);

  /** O diálogo: aberto para criar (registro null) ou editar (o registro lido de novo, com a versão atual). */
  readonly dialogoAberto = signal(false);
  readonly emEdicao = signal<Registro | null>(null);

  /** As colunas: os campos da filha, menos o que aponta para o pai (é sempre ele) e os TEXTO_LONGO. A URL entra, como link. */
  readonly colunas = computed(() => {
    const campos = this.filha().campos.filter((c) => c.chave !== this.campoPai().chave && c.tipo !== 'TEXTO_LONGO');
    // O campo de exibição primeiro: a coluna principal, com o link para o item.
    const exibicao = campos.find((c) => c.chave === this.filha().campoExibicao);
    return exibicao ? [exibicao, ...campos.filter((c) => c !== exibicao)] : campos;
  });
  readonly nomeEmFrase = nomeEmFrase;
  readonly principal = computed(() => this.colunas()[0]?.chave ?? null);
  readonly icone = computed(() => classeIcone(this.filha()));
  /** As ações ("⋯") de cada linha: editar e excluir no diálogo, conforme a permissão. */
  readonly acoesPorLinha = computed(() => {
    const mapa = new Map<string, MenuItem[]>();
    for (const registro of this.resultado()?.itens ?? []) {
      const acoes: MenuItem[] = [];
      if (this.permissoes.podeEditar(this.filha(), registro)) {
        acoes.push({ label: 'Editar', icon: 'pi pi-pencil', command: () => void this.editar(registro) });
      }
      if (this.permissoes.podeExcluir(this.filha(), registro)) {
        acoes.push({
          label: 'Excluir',
          icon: 'pi pi-trash',
          styleClass: 'acao-perigosa',
          command: () => void this.excluir(registro),
        });
      }
      mapa.set(registro.id, acoes);
    }
    return mapa;
  });
  readonly links = computed(
    () =>
      new Set(
        this.colunas()
          .filter((c) => referenciaComoLink(c, this.todas()))
          .map((c) => c.chave),
      ),
  );
  /** O título da seção, no plural ("Links", "Cenários de Teste"). */
  readonly titulo = computed(() => nomePlural(this.filha()));
  readonly tituloExibido = computed(() => this.tituloComQuantidade() ?? this.titulo());
  readonly campoFixo = computed<CampoFixo>(() => ({
    chave: this.campoPai().chave,
    valor: { id: this.registroPai().id, rotulo: rotuloDoRegistro(this.pai(), this.registroPai()) },
  }));
  readonly idSecao = computed(() => `secao-${this.filha().chave}`);

  constructor() {
    effect(() => {
      const pagina = this.pagina();
      const pai = this.registroPai();
      if (this.aberta()) {
        untracked(() => void this.carregar(pai.id, pagina));
      }
    });
  }

  async carregar(idPai: string, pagina: number): Promise<void> {
    this.carregando.set(true);
    this.erro.set(false);
    try {
      this.resultado.set(
        await this.registros.listar(this.grupo(), this.filha().chave, {
          [this.campoPai().chave]: idPai,
          pagina: String(pagina),
          tamanho: String(this.tamanho),
          ordenacao: 'criadoEm',
        }),
      );
    } catch {
      this.erro.set(true);
    } finally {
      this.carregando.set(false);
    }
  }

  recarregar(): void {
    void this.carregar(this.registroPai().id, this.pagina());
  }

  aoMudarPagina(evento: TableLazyLoadEvent): void {
    const pagina = Math.floor((evento.first ?? 0) / this.tamanho) + 1;
    if (pagina !== this.pagina()) {
      this.pagina.set(pagina);
    }
  }

  novo(): void {
    this.emEdicao.set(null);
    this.dialogoAberto.set(true);
  }

  async editar(registro: Registro): Promise<void> {
    // Lê de novo: a linha da tabela pode estar desatualizada, e a edição precisa da versão atual (RN12).
    this.emEdicao.set(await this.registros.obter(this.grupo(), this.filha().chave, registro.id));
    this.dialogoAberto.set(true);
  }

  async excluir(registro: Registro): Promise<void> {
    if (await this.exclusao.excluir(this.grupo(), this.filha(), registro)) {
      this.recarregar();
      this.alterada.emit();
    }
  }

  aoSalvar(): void {
    this.mensagens.add({
      severity: 'success',
      summary: this.emEdicao() ? 'Alterações salvas.' : `${this.filha().nome}: item criado.`,
    });
    this.fechar();
    this.recarregar();
    this.alterada.emit();
  }

  fechar(): void {
    this.dialogoAberto.set(false);
    this.emEdicao.set(null);
  }

  /** A linha abre o detalhe do item filho (menos cliques em links e no "⋯"). */
  abrir(registro: Registro, evento: MouseEvent): void {
    if (cliqueAbreLinha(evento.target as Element, window.getSelection()?.toString() ?? '')) {
      void this.router.navigate(['/', this.grupo(), this.filha().chave, registro.id]);
    }
  }

  /** O rótulo do item, para o nome acessível das ações da linha. */
  rotulo(registro: Registro): string {
    return rotuloDoRegistro(this.filha(), registro);
  }
}
