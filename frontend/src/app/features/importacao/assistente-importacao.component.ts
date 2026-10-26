import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { Message } from 'primeng/message';
import { RadioButton } from 'primeng/radiobutton';
import { Select } from 'primeng/select';
import { Skeleton } from 'primeng/skeleton';
import { Step, StepList, Stepper } from 'primeng/stepper';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { Tag } from 'primeng/tag';
import { ToggleSwitch } from 'primeng/toggleswitch';
import { Tooltip } from 'primeng/tooltip';
import { tratarNoComponente } from '../../core/erros/contexto-erros';
import { lerProblema } from '../../core/erros/problema';
import { ImportacaoService } from '../../core/importacao/importacao.service';
import {
  AcaoDuplicados,
  ImportacaoEnviada,
  LinhaImportacao,
  PreviaImportacao,
  ResumoImportacao,
  TratamentoErros,
} from '../../core/importacao/modelo-importacao';
import { Listagem } from '../../core/metadados/listagens';
import { MetadadosService } from '../../core/metadados/metadados.service';
import { Entidade } from '../../core/metadados/modelo';
import { classeIcone, nomePlural } from '../../core/metadados/regras-metadados';
import { PermissoesService } from '../../core/permissoes/permissoes.service';
import { EstadoErroComponent } from '../../shared/estados/estado-erro.component';
import { PassoTrilha } from '../../shared/navegacao/trilha.component';
import { CabecalhoPaginaComponent } from '../../shared/pagina/cabecalho-pagina.component';
import { DensidadeService } from '../../shared/tabelas/densidade.service';
import { SombraRolagemDirective } from '../../shared/tabelas/sombra-rolagem.directive';
import { baixarArquivo } from '../registros/comum/baixar-arquivo';
import {
  IGNORAR,
  MapeamentoTela,
  descricaoDuplicados,
  errosDaCelula,
  errosDaLinha,
  escolherDestino,
  lembrarMapeamento,
  linhasDosImpedimentos,
  mapeamentoInicial,
  mapeamentoParaApi,
  mensagemDesfazer,
  motivoNaoExecutavel,
  obrigatoriosSemColuna,
  opcoesDoSelect,
  problemaDoArquivo,
  referenciaDaCelula,
  rotuloExecutar,
  situacaoDaLinha,
  temColunaEquipe,
  temReferencias,
  textoConclusao,
} from './modelo/regras-importacao';

/** As etapas do assistente, na ordem (os números são os valores do p-stepper). */
export const ETAPAS = { arquivo: 1, colunas: 2, opcoes: 3, previa: 4, conclusao: 5 } as const;
type Etapa = (typeof ETAPAS)[keyof typeof ETAPAS];

const TAMANHO_PAGINA_PREVIA = 20;

/**
 * O assistente de importação de itens por planilha (/:grupo/:entidade/importar, RN97 a RN104), GENÉRICO para qualquer tipo de item:
 * tudo vem dos metadados e da API. Etapas: Arquivo (modelo e envio) → Colunas (mapeamento, com a sugestão da API) → Opções → Prévia
 * (simulação completa, sem gravar) → Conclusão (relatório e desfazer). Uma ação primária por etapa, sempre visível no rodapé. Para
 * corrigir, a pessoa reenvia a planilha e o mapeamento é reaproveitado pelo cabeçalho das colunas.
 */
@Component({
  selector: 'app-assistente-importacao',
  imports: [
    FormsModule,
    RouterLink,
    Button,
    Message,
    RadioButton,
    Select,
    Skeleton,
    Stepper,
    StepList,
    Step,
    TableModule,
    Tag,
    ToggleSwitch,
    Tooltip,
    CabecalhoPaginaComponent,
    EstadoErroComponent,
    SombraRolagemDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './assistente-importacao.component.html',
})
export class AssistenteImportacaoComponent {
  /** Parâmetros da rota (withComponentInputBinding). */
  readonly grupo = input.required<string>();
  readonly entidade = input.required<string>();

  private readonly importacao = inject(ImportacaoService);
  private readonly metadados = inject(MetadadosService);
  private readonly mensagens = inject(MessageService);
  private readonly confirmacao = inject(ConfirmationService);
  private readonly router = inject(Router);
  readonly permissoes = inject(PermissoesService);
  readonly densidade = inject(DensidadeService);

  readonly ETAPAS = ETAPAS;
  readonly etapa = signal<Etapa>(ETAPAS.arquivo);

  readonly meta = signal<Entidade | null>(null);
  readonly listagem = signal<Listagem | null>(null);
  readonly nomeGrupo = signal('');
  readonly erroCarregar = signal(false);

  // Etapa 1: o arquivo.
  readonly enviando = signal(false);
  readonly baixandoModelo = signal(false);
  readonly problemaArquivo = signal<string | null>(null);
  readonly arrastando = signal(false);
  readonly enviada = signal<ImportacaoEnviada | null>(null);
  /** O mapeamento escolhido no arquivo anterior, pelo cabeçalho (reenvio da planilha corrigida). */
  private anterior: Map<string, string> | undefined;

  // Etapa 2: as colunas.
  readonly mapeamento = signal<MapeamentoTela>({});
  readonly opcoesSelect = computed(() => opcoesDoSelect(this.enviada()?.destinos ?? []));
  readonly faltando = computed(() => obrigatoriosSemColuna(this.enviada()?.destinos ?? [], this.mapeamento()));
  readonly nomesFaltando = computed(() =>
    this.faltando()
      .map((f) => f.rotulo)
      .join(', '),
  );

  // Etapa 3: as opções.
  readonly duplicados = signal<AcaoDuplicados>('PULAR');
  readonly linhasComErro = signal<TratamentoErros>('SO_VALIDAS');
  readonly equipePadrao = signal<string | null>(null);
  readonly equipes = computed(() => {
    const meta = this.meta();
    return meta && meta.escopoEquipe
      ? this.permissoes.equipesParaCriar(meta, this.listagem()?.minhasEquipes ?? [])
      : [];
  });
  /** Precisa escolher a equipe padrão: por equipe, mais de uma equipe e sem coluna Equipe para todas as linhas. */
  readonly equipeObrigatoria = computed(() => this.equipes().length > 1 && !temColunaEquipe(this.mapeamento()));
  readonly restricoes = computed(() => {
    const meta = this.meta();
    return meta ? descricaoDuplicados(meta) : [];
  });
  readonly validando = signal(false);
  readonly erroOpcoes = signal<string | null>(null);

  // Etapa 4: a prévia.
  readonly previa = signal<PreviaImportacao | null>(null);
  readonly somenteComErro = signal(false);
  readonly carregandoPrevia = signal(false);
  readonly executando = signal(false);
  readonly baixandoRelatorio = signal(false);
  readonly avisoPrevia = signal<string | null>(null);
  /** As colunas do arquivo que vão para algum lugar (as ignoradas não aparecem na prévia). */
  readonly colunasPrevia = computed(() => {
    const enviada = this.enviada();
    const mapa = this.mapeamento();
    return (enviada?.colunas ?? []).filter((c) => (mapa[c.indice] ?? IGNORAR) !== IGNORAR);
  });
  readonly motivoNaoExecutavel = computed(() => {
    const previa = this.previa();
    return previa ? motivoNaoExecutavel(previa.totais, previa.executavel) : null;
  });
  readonly rotuloExecutar = computed(() => {
    const previa = this.previa();
    return previa ? rotuloExecutar(previa.totais) : 'Importar';
  });

  // Etapa 5: a conclusão.
  readonly resultado = signal<ResumoImportacao | null>(null);
  readonly desfazendo = signal(false);
  readonly textoConclusao = computed(() => {
    const totais = this.resultado()?.totais;
    return totais ? textoConclusao(totais) : '';
  });

  readonly titulo = computed(() => {
    const meta = this.meta();
    return meta ? `Importar ${nomePlural(meta).toLocaleLowerCase('pt-BR')}` : 'Importar planilha';
  });
  readonly icone = computed(() => classeIcone(this.meta()));
  readonly trilha = computed<PassoTrilha[]>(() => {
    const meta = this.meta();
    return [
      { rotulo: this.nomeGrupo(), link: ['/', this.grupo()] },
      { rotulo: meta ? nomePlural(meta) : '', link: ['/', this.grupo(), this.entidade()] },
      { rotulo: 'Importar planilha' },
    ];
  });
  readonly comReferencias = computed(() => {
    const meta = this.meta();
    return !!meta && temReferencias(meta);
  });

  readonly situacaoDaLinha = situacaoDaLinha;
  readonly linhasEsqueleto = Array.from({ length: 5 }, (_, i) => i);

  constructor() {
    effect(() => {
      const grupo = this.grupo();
      const entidade = this.entidade();
      untracked(() => void this.carregar(grupo, entidade));
    });
  }

  private async carregar(grupo: string, chave: string): Promise<void> {
    this.erroCarregar.set(false);
    try {
      const meta = await this.metadados.entidade(grupo, chave);
      if (!meta) {
        void this.router.navigate(['/nao-encontrado'], { skipLocationChange: true });
        return;
      }
      const listagem = await this.metadados.listagem(grupo);
      this.listagem.set(listagem);
      // Importar = criar (RN97): quem não cria aqui não chega ao assistente.
      if (!this.permissoes.podeCriar(meta, listagem?.minhasEquipes ?? [])) {
        void this.router.navigate(['/sem-permissao'], { skipLocationChange: true });
        return;
      }
      this.meta.set(meta);
      const equipes = this.equipes();
      this.equipePadrao.set(equipes.length === 1 ? equipes[0].id : null);
      void this.metadados.nomeDoGrupo(grupo).then(
        (nome) => this.nomeGrupo.set(nome),
        () => this.nomeGrupo.set(grupo),
      );
    } catch {
      this.erroCarregar.set(true);
    }
  }

  tentarDeNovo(): void {
    void this.carregar(this.grupo(), this.entidade());
  }

  // ---- Etapa 1: arquivo ----

  async baixarModelo(): Promise<void> {
    this.baixandoModelo.set(true);
    try {
      const { arquivo, nome } = await this.importacao.baixarModelo(this.grupo(), this.entidade());
      baixarArquivo(arquivo, nome);
    } catch {
      // O interceptor já mostrou a mensagem.
    } finally {
      this.baixandoModelo.set(false);
    }
  }

  aoEscolherArquivo(evento: Event): void {
    const entrada = evento.target as HTMLInputElement;
    const arquivo = entrada.files?.[0];
    entrada.value = '';
    if (arquivo) {
      void this.enviar(arquivo);
    }
  }

  aoArrastar(evento: DragEvent, dentro: boolean): void {
    evento.preventDefault();
    this.arrastando.set(dentro);
  }

  aoSoltar(evento: DragEvent): void {
    evento.preventDefault();
    this.arrastando.set(false);
    const arquivo = evento.dataTransfer?.files?.[0];
    if (arquivo) {
      void this.enviar(arquivo);
    }
  }

  async enviar(arquivo: File): Promise<void> {
    const problema = problemaDoArquivo(arquivo.name, arquivo.size);
    this.problemaArquivo.set(problema);
    if (problema) {
      return;
    }
    this.enviando.set(true);
    try {
      const enviada = await this.importacao.enviar(this.grupo(), this.entidade(), arquivo, tratarNoComponente(400));
      this.enviada.set(enviada);
      this.mapeamento.set(mapeamentoInicial(enviada.colunas, this.anterior));
      this.previa.set(null);
      this.etapa.set(ETAPAS.colunas);
    } catch (erro) {
      const lido = lerProblema(erro);
      if (lido.status === 400) {
        this.problemaArquivo.set(Object.values(lido.erros).flat().join(' ') || 'Não foi possível ler o arquivo.');
      }
    } finally {
      this.enviando.set(false);
    }
  }

  // ---- Etapa 2: colunas ----

  destinoDaColuna(indice: number): string {
    return (this.mapeamento() as Partial<MapeamentoTela>)[indice] ?? IGNORAR;
  }

  escolher(indice: number, valor: string): void {
    this.mapeamento.set(escolherDestino(this.mapeamento(), indice, valor));
  }

  // ---- Etapa 3: opções e prévia ----

  async validar(): Promise<void> {
    const enviada = this.enviada();
    if (!enviada) {
      return;
    }
    if (this.equipeObrigatoria() && !this.equipePadrao()) {
      this.erroOpcoes.set('Escolha a equipe dos itens.');
      return;
    }
    this.erroOpcoes.set(null);
    this.anterior = lembrarMapeamento(enviada.colunas, this.mapeamento());
    this.validando.set(true);
    try {
      this.somenteComErro.set(false);
      this.previa.set(
        await this.importacao.validar(
          this.grupo(),
          this.entidade(),
          enviada.id,
          mapeamentoParaApi(enviada.colunas, this.mapeamento()),
          {
            duplicados: this.duplicados(),
            linhasComErro: this.linhasComErro(),
            equipePadraoId: this.meta()?.escopoEquipe ? this.equipePadrao() : null,
          },
          { pagina: 1, tamanho: TAMANHO_PAGINA_PREVIA, somenteComErro: false },
          tratarNoComponente(400),
        ),
      );
      this.avisoPrevia.set(null);
      this.etapa.set(ETAPAS.previa);
    } catch (erro) {
      const lido = lerProblema(erro);
      if (lido.status === 400) {
        this.erroOpcoes.set(Object.values(lido.erros).flat().join(' ') || 'Confira as colunas e as opções.');
      }
    } finally {
      this.validando.set(false);
    }
  }

  // ---- Etapa 4: prévia ----

  aoMudarPagina(evento: TableLazyLoadEvent): void {
    const tamanho = evento.rows ?? TAMANHO_PAGINA_PREVIA;
    const pagina = Math.floor((evento.first ?? 0) / tamanho) + 1;
    void this.recarregarPrevia(pagina, tamanho);
  }

  alternarSomenteComErro(valor: boolean): void {
    this.somenteComErro.set(valor);
    void this.recarregarPrevia(1, this.previa()?.linhas.tamanho ?? TAMANHO_PAGINA_PREVIA);
  }

  private async recarregarPrevia(pagina: number, tamanho: number): Promise<void> {
    const enviada = this.enviada();
    if (!enviada) {
      return;
    }
    this.carregandoPrevia.set(true);
    try {
      this.previa.set(
        await this.importacao.obter(this.grupo(), this.entidade(), enviada.id, {
          pagina,
          tamanho,
          somenteComErro: this.somenteComErro(),
        }),
      );
    } catch {
      // O interceptor já mostrou a mensagem.
    } finally {
      this.carregandoPrevia.set(false);
    }
  }

  errosDaCelula(linha: LinhaImportacao, coluna: number): string[] {
    return errosDaCelula(linha, coluna);
  }

  errosDaLinha(linha: LinhaImportacao): string[] {
    return errosDaLinha(linha, this.meta());
  }

  dicaDaCelula(linha: LinhaImportacao, coluna: number): string | null {
    const erros = errosDaCelula(linha, coluna);
    if (erros.length > 0) {
      return erros.join('\n');
    }
    const referencia = referenciaDaCelula(linha, coluna);
    return referencia ? `Encontrado: ${referencia}` : null;
  }

  async baixarRelatorio(): Promise<void> {
    const id = this.resultado()?.id ?? this.enviada()?.id;
    if (!id) {
      return;
    }
    this.baixandoRelatorio.set(true);
    try {
      const { arquivo, nome } = await this.importacao.baixarRelatorio(this.grupo(), this.entidade(), id);
      baixarArquivo(arquivo, nome);
    } catch {
      // O interceptor já mostrou a mensagem.
    } finally {
      this.baixandoRelatorio.set(false);
    }
  }

  async executar(): Promise<void> {
    const enviada = this.enviada();
    if (!enviada) {
      return;
    }
    this.executando.set(true);
    try {
      this.resultado.set(
        await this.importacao.executar(this.grupo(), this.entidade(), enviada.id, tratarNoComponente(409)),
      );
      this.metadados.invalidar(this.grupo());
      this.etapa.set(ETAPAS.conclusao);
    } catch (erro) {
      const lido = lerProblema(erro);
      if (lido.codigo === 'IMPORTACAO_DADOS_MUDARAM') {
        // RN101: nada foi gravado e a prévia já foi refeita: mostra a nova e pede outra confirmação.
        this.avisoPrevia.set(
          'Os dados mudaram desde a prévia (por exemplo, um item referenciado foi excluído). Nada foi importado: confira a prévia atualizada e confirme de novo.',
        );
        this.somenteComErro.set(false);
        await this.recarregarPrevia(1, TAMANHO_PAGINA_PREVIA);
      } else if (lido.status === 409) {
        this.avisoPrevia.set(lido.detalhe ?? 'Não foi possível importar.');
      }
    } finally {
      this.executando.set(false);
    }
  }

  /** Volta à etapa Arquivo para enviar a planilha corrigida; o mapeamento escolhido é reaproveitado. */
  enviarOutroArquivo(): void {
    const enviada = this.enviada();
    if (enviada) {
      this.anterior = lembrarMapeamento(enviada.colunas, this.mapeamento());
    }
    this.problemaArquivo.set(null);
    this.etapa.set(ETAPAS.arquivo);
  }

  // ---- Etapa 5: conclusão ----

  desfazer(): void {
    const resultado = this.resultado();
    if (!resultado) {
      return;
    }
    this.confirmacao.confirm({
      header: 'Desfazer importação',
      message: mensagemDesfazer(resultado.totais),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Desfazer importação',
      rejectLabel: 'Cancelar',
      acceptButtonProps: { severity: 'danger', label: 'Desfazer importação' },
      rejectButtonProps: { severity: 'secondary', text: true, label: 'Cancelar' },
      accept: () => void this.confirmarDesfazer(resultado.id),
    });
  }

  private async confirmarDesfazer(id: string): Promise<void> {
    this.desfazendo.set(true);
    try {
      this.resultado.set(await this.importacao.desfazer(this.grupo(), this.entidade(), id, tratarNoComponente(409)));
      this.metadados.invalidar(this.grupo());
      this.mensagens.add({
        severity: 'success',
        summary: 'Importação desfeita: os itens criados por ela foram excluídos.',
      });
    } catch (erro) {
      const lido = lerProblema(erro);
      if (lido.status === 409) {
        this.mostrarRecusaDesfazer(lido.detalhe, lido.detalhes);
      }
    } finally {
      this.desfazendo.set(false);
    }
  }

  private mostrarRecusaDesfazer(detalhe: string | null, detalhes: Record<string, unknown> | null): void {
    const linhas = linhasDosImpedimentos(detalhes);
    this.confirmacao.confirm({
      key: 'bloqueio',
      header: 'Não é possível desfazer',
      message: linhas.length > 0 ? ['A importação não pode ser desfeita:', ...linhas].join('\n') : (detalhe ?? ''),
      icon: 'pi pi-ban',
      acceptLabel: 'Entendi',
      rejectVisible: false,
      acceptButtonProps: { label: 'Entendi' },
    });
  }

  novaImportacao(): void {
    this.enviada.set(null);
    this.previa.set(null);
    this.resultado.set(null);
    this.problemaArquivo.set(null);
    this.etapa.set(ETAPAS.arquivo);
  }
}
