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
import { Button } from 'primeng/button';
import { Message } from 'primeng/message';
import { Skeleton } from 'primeng/skeleton';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { Tag } from 'primeng/tag';
import { mostraSituacao, situacaoDe, SITUACOES } from '../../../core/aprovacao/regras-aprovacao';
import { EquipeDaListagem } from '../../../core/metadados/listagens';
import { Campo, Entidade } from '../../../core/metadados/modelo';
import { classeIcone, nomeEmFrase, podeOrdenarPor, referenciaComoLink } from '../../../core/metadados/regras-metadados';
import { PermissoesService } from '../../../core/permissoes/permissoes.service';
import { PaginaDe, Registro } from '../../../core/registros/modelo-registro';
import { RegistrosService } from '../../../core/registros/registros.service';
import { MediaEstrelasComponent } from '../../../shared/avaliacao/media-estrelas.component';
import { ValorCampoComponent } from '../../../shared/exibicao/valor-campo.component';
import { cliqueAbreLinha } from '../../../shared/tabelas/colunas-tabela';
import { DensidadeService } from '../../../shared/tabelas/densidade.service';
import { SombraRolagemDirective } from '../../../shared/tabelas/sombra-rolagem.directive';
import { rotuloDoRegistro } from '../comum/rotulos';
import { ORDENACAO_PADRAO, lerOrdenacao } from '../filtros/filtros-url';
import { SecaoRelacionada, TAMANHO_PAGINA_SECAO, colunasDaSecao, linkNovo, linkVerTodos } from './relacionados';

/**
 * Uma seção SOMENTE LEITURA do detalhe (RN94, ADR 0047): os itens de outra entidade que apontam para o item aberto por um campo
 * REFERENCIA (ex.: os Relatórios de Teste de um Projeto). A tabela vem da listagem filtrada pelo campo e pelo id do item, paginada e
 * ordenada no servidor, e só é carregada quando a seção está aberta (cada seção carrega e falha sozinha). Colunas: os primeiros
 * campos da entidade (relacionados.ts → colunasDaSecao), mais a situação e as médias de avaliação onde o recurso está ativo.
 */
@Component({
  selector: 'app-secao-relacionados',
  imports: [
    RouterLink,
    Button,
    Message,
    Skeleton,
    TableModule,
    Tag,
    MediaEstrelasComponent,
    SombraRolagemDirective,
    ValorCampoComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './secao-relacionados.component.html',
})
export class SecaoRelacionadosComponent {
  readonly grupo = input.required<string>();
  readonly secao = input.required<SecaoRelacionada>();
  /** O item aberto (o que é apontado). */
  readonly item = input.required<Registro>();
  /** Todas as entidades do grupo (referências: link ou texto; ordenação). */
  readonly todas = input<Entidade[]>([]);
  /** As equipes do usuário nesta listagem (RN91): decide se aparece "Novo …". */
  readonly equipesNaListagem = input<EquipeDaListagem[]>([]);
  /** O endereço do detalhe do item: "Novo …" volta para ele depois de salvar. */
  readonly voltar = input.required<string>();
  readonly aberta = input(true);
  readonly alternar = output<void>();

  private readonly registros = inject(RegistrosService);
  private readonly router = inject(Router);
  readonly permissoes = inject(PermissoesService);
  readonly densidade = inject(DensidadeService);

  readonly tamanho = TAMANHO_PAGINA_SECAO;
  readonly pagina = signal(1);
  readonly ordenacao = signal(ORDENACAO_PADRAO);
  readonly ordem = computed(() => lerOrdenacao(this.ordenacao()));
  readonly resultado = signal<PaginaDe<Registro> | null>(null);
  readonly carregando = signal(false);
  readonly erro = signal(false);

  readonly entidade = computed(() => this.secao().entidade);
  readonly colunas = computed(() => colunasDaSecao(this.entidade(), this.secao().campo.chave));
  readonly ordenaveis = computed(
    () =>
      new Set(
        this.colunas()
          .filter((c) => podeOrdenarPor(c, this.todas()))
          .map((c) => c.chave),
      ),
  );
  readonly links = computed(
    () =>
      new Set(
        this.colunas()
          .filter((c) => referenciaComoLink(c, this.todas()))
          .map((c) => c.chave),
      ),
  );
  /** Situação e médias só onde o recurso está ativo (ADR 0045), como na listagem. */
  readonly comSituacao = computed(() => this.entidade().equipesComAprovacao.length > 0);
  readonly colunasAvaliacao = computed(() =>
    this.entidade().criteriosAvaliacao.map((c) => ({
      chave: `avaliacao.${c.chave}`,
      criterio: c.chave,
      nome: `Média: ${c.nome}`,
    })),
  );
  readonly totalColunas = computed(
    () => this.colunas().length + this.colunasAvaliacao().length + (this.comSituacao() ? 1 : 0),
  );
  /** A primeira coluna é a principal: o link para o item (o campo de exibição, quando a entidade tem um). */
  readonly principal = computed(() => this.colunas()[0]?.chave ?? null);
  readonly icone = computed(() => classeIcone(this.entidade()));
  readonly nomeEmFrase = nomeEmFrase;
  readonly verTodos = computed(() => linkVerTodos(this.grupo(), this.secao(), this.item().id));
  readonly novo = computed(() => linkNovo(this.grupo(), this.secao(), this.item(), this.voltar()));
  readonly podeCriar = computed(() => this.permissoes.podeCriar(this.entidade(), this.equipesNaListagem()));
  readonly idSecao = computed(() => `relacionados-${this.secao().id.replace('.', '-')}`);

  constructor() {
    effect(() => {
      const [aberta, pagina, ordenacao, item] = [this.aberta(), this.pagina(), this.ordenacao(), this.item()];
      if (aberta) {
        untracked(() => void this.carregar(item.id, pagina, ordenacao));
      }
    });
  }

  async carregar(id: string, pagina: number, ordenacao: string): Promise<void> {
    this.carregando.set(true);
    this.erro.set(false);
    try {
      this.resultado.set(
        await this.registros.listar(this.grupo(), this.entidade().chave, {
          [this.secao().campo.chave]: id,
          pagina: String(pagina),
          tamanho: String(this.tamanho),
          ordenacao,
        }),
      );
    } catch {
      this.erro.set(true);
    } finally {
      this.carregando.set(false);
    }
  }

  recarregar(): void {
    void this.carregar(this.item().id, this.pagina(), this.ordenacao());
  }

  aoMudarTabela(evento: TableLazyLoadEvent): void {
    const campo = typeof evento.sortField === 'string' ? evento.sortField : null;
    const ordenacao = campo ? `${evento.sortOrder === -1 ? '-' : ''}${campo}` : ORDENACAO_PADRAO;
    const pagina = ordenacao !== this.ordenacao() ? 1 : Math.floor((evento.first ?? 0) / this.tamanho) + 1;
    if (ordenacao !== this.ordenacao()) {
      this.ordenacao.set(ordenacao);
    }
    if (pagina !== this.pagina()) {
      this.pagina.set(pagina);
    }
  }

  /** A linha leva ao detalhe do item; um clique num link ou botão dentro dela segue o caminho dele. */
  abrir(registro: Registro, evento: MouseEvent): void {
    if (!cliqueAbreLinha(evento.target as Element, window.getSelection()?.toString() ?? '')) {
      return;
    }
    void this.router.navigate(['/', this.grupo(), this.entidade().chave, registro.id]);
  }

  ehNumero(campo: Campo): boolean {
    return campo.tipo === 'INTEIRO' || campo.tipo === 'DECIMAL';
  }

  mostraSituacao(registro: Registro): boolean {
    return mostraSituacao(this.entidade(), registro);
  }

  situacao(registro: Registro): { rotulo: string; severidade: 'warn' | 'success' | 'danger' } {
    return SITUACOES[situacaoDe(registro.situacaoAprovacao)];
  }

  mediaDe(registro: Registro, criterio: string): number | null {
    return registro.avaliacao?.medias?.[criterio] ?? null;
  }

  rotulo(registro: Registro): string {
    return rotuloDoRegistro(this.entidade(), registro);
  }
}
