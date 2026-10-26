import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Message } from 'primeng/message';
import { Skeleton } from 'primeng/skeleton';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { PendentesService } from '../../../core/aprovacao/pendentes.service';
import { AvaliacaoService } from '../../../core/avaliacao/avaliacao.service';
import { CriterioAvaliacao } from '../../../core/avaliacao/modelo-avaliacao';
import { lerProblema } from '../../../core/erros/problema';
import { EquipeDaListagem, nomeDaEquipe } from '../../../core/metadados/listagens';
import { MetadadosService } from '../../../core/metadados/metadados.service';
import { Entidade } from '../../../core/metadados/modelo';
import { classeIcone, nomePlural, referenciaComoLink } from '../../../core/metadados/regras-metadados';
import { PermissoesService } from '../../../core/permissoes/permissoes.service';
import { Registro, Relacionado } from '../../../core/registros/modelo-registro';
import { RegistrosService } from '../../../core/registros/registros.service';
import { ValorCampoComponent } from '../../../shared/exibicao/valor-campo.component';
import { formatarDataHoraSistema } from '../../../shared/formatacao/formatar-valor';
import { DataRelativaComponent } from '../../../shared/formatacao/data-relativa.component';
import { EstadoErroComponent } from '../../../shared/estados/estado-erro.component';
import { PassoTrilha } from '../../../shared/navegacao/trilha.component';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';
import { ExclusaoService } from '../comum/exclusao.service';
import { leituraDoRegistro, listaDeNomes } from '../comum/leitura-registro';
import { rotuloDoRegistro } from '../comum/rotulos';
import { AbaAvaliacoesComponent } from './aba-avaliacoes.component';
import { FaixaAprovacaoComponent } from './faixa-aprovacao.component';
import { HistoricoRegistroComponent } from './historico-registro.component';
import { SecaoFilhosComponent } from './secao-filhos.component';
import { SecaoRelacionadosComponent } from './secao-relacionados.component';
import { SecaoRelacionada, chaveEstadoSecoes, lerEstadoSecoes, montarSecoes, secaoAberta } from './relacionados';

/**
 * Detalhe de um registro (/:grupo/:entidade/:id): os campos em leitura, as seções de itens RELACIONADOS e a aba Histórico. Tudo
 * montado pelos metadados. Os relacionados (RN94, ADR 0047) vêm de uma chamada só (GET .../{id}/relacionados): primeiro as filhas
 * (editáveis, sempre visíveis), depois as seções somente leitura com itens, e no fim "Sem itens relacionados em: …". Cada seção é
 * recolhível (até 3, todas abertas; acima disso, só a primeira), com o estado guardado no localStorage por entidade. Os campos preenchidos curtos ficam no grid; os textos longos, na largura toda (com as quebras de linha); os
 * vazios, de qualquer tipo, não ocupam espaço: viram uma linha discreta "Não preenchidos: …" (comum/leitura-registro.ts).
 * Referências para itens por equipe são link; para cadastros de apoio globais, texto.
 * Aprovação e avaliação só aparecem onde o recurso está ativo para a entidade e a equipe do item (ADR 0045): a faixa de aprovação
 * no topo e a aba Avaliações, com os critérios do modelo ativo na equipe.
 */
@Component({
  selector: 'app-detalhe-registro',
  imports: [
    RouterLink,
    Button,
    Message,
    Skeleton,
    Tabs,
    TabList,
    Tab,
    TabPanels,
    TabPanel,
    AbaAvaliacoesComponent,
    FaixaAprovacaoComponent,
    HistoricoRegistroComponent,
    SecaoFilhosComponent,
    SecaoRelacionadosComponent,
    CabecalhoPaginaComponent,
    DataRelativaComponent,
    EstadoErroComponent,
    ValorCampoComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './detalhe-registro.component.html',
})
export class DetalheRegistroComponent {
  readonly grupo = input.required<string>();
  readonly entidade = input.required<string>();
  readonly id = input.required<string>();

  private readonly metadados = inject(MetadadosService);
  private readonly registros = inject(RegistrosService);
  private readonly exclusao = inject(ExclusaoService);
  private readonly router = inject(Router);
  private readonly rota = inject(ActivatedRoute);
  private readonly avaliacao = inject(AvaliacaoService);
  private readonly pendentes = inject(PendentesService);
  readonly permissoes = inject(PermissoesService);

  readonly meta = signal<Entidade | null>(null);
  readonly todas = signal<Entidade[]>([]);
  readonly registro = signal<Registro | null>(null);
  readonly naoEncontrado = signal(false);
  readonly erro = signal(false);
  /** Os critérios do modelo de avaliação ativo na equipe do item; nulo sem modelo ativo (a aba Avaliações não aparece). */
  readonly criteriosAvaliacao = signal<CriterioAvaliacao[] | null>(null);
  /** As equipes do usuário nesta listagem (RN91), para o nome da equipe do item. */
  private readonly equipesDaListagem = signal<EquipeDaListagem[]>([]);
  /** A equipe do item, nas entidades por equipe (RN92: a de uma equipe que deixou de ser habilitada aparece como tal). */
  readonly nomeEquipe = computed(() =>
    this.meta()?.escopoEquipe ? nomeDaEquipe(this.equipesDaListagem(), this.registro()?.equipeId) : null,
  );

  /** A resposta de GET .../relacionados (nulo: ainda não carregou). */
  readonly relacionados = signal<Relacionado[] | null>(null);
  readonly erroRelacionados = signal(false);
  readonly secoes = computed(() => montarSecoes(this.relacionados() ?? [], this.todas()));
  /** O estado aberto/fechado escolhido pela pessoa (localStorage, por entidade). */
  private readonly estadoSecoes = signal<Record<string, boolean>>({});
  /** Todas as seções na ordem da tela, para a regra de quantas abrem por padrão. */
  private readonly ordemSecoes = computed(() => [...this.secoes().filhas, ...this.secoes().leitura].map((s) => s.id));
  /** O endereço deste detalhe: "Novo …" das seções volta para cá. */
  readonly enderecoDetalhe = computed(() => `/${this.grupo()}/${this.entidade()}/${this.id()}`);
  readonly equipesNaListagem = computed(() => this.equipesDaListagem());
  readonly titulo = computed(() => {
    const meta = this.meta();
    const registro = this.registro();
    return meta && registro ? rotuloDoRegistro(meta, registro) : '';
  });
  readonly leitura = computed(() => {
    const meta = this.meta();
    const registro = this.registro();
    return meta && registro ? leituraDoRegistro(meta, registro) : { curtos: [], longos: [], vazios: [] };
  });
  readonly naoPreenchidos = computed(() => listaDeNomes(this.leitura().vazios));
  readonly links = computed(() => {
    const todas = this.todas();
    return new Set((this.meta()?.campos ?? []).filter((c) => referenciaComoLink(c, todas)).map((c) => c.chave));
  });
  readonly trilha = computed<PassoTrilha[]>(() => {
    const meta = this.meta();
    return meta
      ? [
          { rotulo: this.nomeArea(), link: ['/', this.grupo()] },
          { rotulo: nomePlural(meta), link: ['/', this.grupo(), meta.chave] },
          { rotulo: this.titulo() },
        ]
      : [];
  });
  readonly icone = computed(() => classeIcone(this.meta()));
  /** O subtítulo: o tipo de item (e a equipe, quando a pessoa vê mais de uma). */
  readonly subtitulo = computed(() => {
    const meta = this.meta();
    const equipe = this.nomeEquipe();
    return meta ? (equipe ? `${meta.nome} · ${equipe}` : meta.nome) : null;
  });
  readonly nomeArea = signal('');
  readonly formatarDataHoraSistema = formatarDataHoraSistema;

  aberta(secao: SecaoRelacionada): boolean {
    const ordem = this.ordemSecoes();
    return secaoAberta(this.estadoSecoes(), secao.id, ordem.indexOf(secao.id), ordem.length);
  }

  alternar(secao: SecaoRelacionada): void {
    const novo = { ...this.estadoSecoes(), [secao.id]: !this.aberta(secao) };
    this.estadoSecoes.set(novo);
    try {
      localStorage.setItem(chaveEstadoSecoes(this.grupo(), this.entidade()), JSON.stringify(novo));
    } catch {
      // Sem armazenamento: vale só enquanto a página estiver aberta.
    }
  }

  async carregarRelacionados(): Promise<void> {
    this.erroRelacionados.set(false);
    try {
      this.relacionados.set(await this.registros.relacionados(this.grupo(), this.entidade(), this.id()));
    } catch {
      this.erroRelacionados.set(true);
    }
  }

  constructor() {
    effect(() => {
      const [grupo, entidade, id] = [this.grupo(), this.entidade(), this.id()];
      untracked(() => void this.carregar(grupo, entidade, id));
    });
  }

  tentarDeNovo(): void {
    const meta = this.meta();
    const registro = this.registro();
    void this.carregar(this.grupo(), meta?.chave ?? this.entidade(), registro?.id ?? this.id());
  }

  private async carregar(grupo: string, chave: string, id: string): Promise<void> {
    this.registro.set(null);
    this.naoEncontrado.set(false);
    this.erro.set(false);
    this.criteriosAvaliacao.set(null);
    this.relacionados.set(null);
    try {
      this.estadoSecoes.set(lerEstadoSecoes(localStorage.getItem(chaveEstadoSecoes(grupo, chave))));
    } catch {
      this.estadoSecoes.set({});
    }
    const meta = await this.metadados.entidade(grupo, chave);
    if (!meta) {
      void this.router.navigate(['/nao-encontrado'], { skipLocationChange: true });
      return;
    }
    this.meta.set(meta);
    this.todas.set(await this.metadados.entidades(grupo));
    this.equipesDaListagem.set(await this.metadados.equipesDaListagem(grupo));
    this.nomeArea.set(await this.metadados.nomeDoGrupo(grupo));
    try {
      const registro = await this.registros.obter(grupo, chave, id);
      this.registro.set(registro);
      void this.carregarAvaliacao(grupo, chave, registro);
      void this.carregarRelacionados();
    } catch (erro) {
      if (lerProblema(erro).status === 404) {
        this.naoEncontrado.set(true);
      } else {
        this.erro.set(true);
      }
    }
  }

  private async carregarAvaliacao(grupo: string, chave: string, registro: Registro): Promise<void> {
    try {
      const recursos = await this.avaliacao.recursosDoItem(grupo, chave, registro.equipeId);
      this.criteriosAvaliacao.set(recursos.formulario?.criterios ?? null);
    } catch {
      this.criteriosAvaliacao.set(null);
    }
  }

  /** Depois de aprovar ou rejeitar: quem veio da fila volta para ela (já atualizada); os outros ficam no item atualizado. */
  aoDecidir(registro: Registro): void {
    if (this.rota.snapshot.queryParamMap.has('daFila')) {
      void this.router.navigateByUrl(this.pendentes.urlDaFila());
    } else {
      this.registro.set(registro);
    }
  }

  async excluir(): Promise<void> {
    const meta = this.meta();
    const registro = this.registro();
    if (meta && registro && (await this.exclusao.excluir(this.grupo(), meta, registro))) {
      void this.router.navigate(['/', this.grupo(), meta.chave]);
    }
  }
}
