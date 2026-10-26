import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { Skeleton } from 'primeng/skeleton';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { Tag } from 'primeng/tag';
import { Tooltip } from 'primeng/tooltip';
import { tratarNoComponente } from '../../core/erros/contexto-erros';
import { lerProblema } from '../../core/erros/problema';
import { ImportacaoService } from '../../core/importacao/importacao.service';
import { ResumoImportacao } from '../../core/importacao/modelo-importacao';
import { MetadadosService } from '../../core/metadados/metadados.service';
import { Entidade } from '../../core/metadados/modelo';
import { classeIcone, nomePlural } from '../../core/metadados/regras-metadados';
import { PermissoesService } from '../../core/permissoes/permissoes.service';
import { PaginaDe } from '../../core/registros/modelo-registro';
import { EstadoErroComponent } from '../../shared/estados/estado-erro.component';
import { EstadoVazioComponent } from '../../shared/estados/estado-vazio.component';
import { formatarDataHoraSistema } from '../../shared/formatacao/formatar-valor';
import { PassoTrilha } from '../../shared/navegacao/trilha.component';
import { CabecalhoPaginaComponent } from '../../shared/pagina/cabecalho-pagina.component';
import { DensidadeService } from '../../shared/tabelas/densidade.service';
import { SombraRolagemDirective } from '../../shared/tabelas/sombra-rolagem.directive';
import { baixarArquivo } from '../registros/comum/baixar-arquivo';
import { linhasDosImpedimentos, mensagemDesfazer, situacaoDaImportacao, textoTotais } from './modelo/regras-importacao';

const TAMANHO_PAGINA = 20;

/**
 * "Importações anteriores" de um tipo de item (/:grupo/:entidade/importacoes, RN104): data, quem importou, arquivo, resumo e
 * situação, com as ações de baixar o relatório e desfazer. Cada pessoa vê as próprias; o GESTOR, também as da sua equipe; o
 * ADMINISTRADOR, todas (quem decide é a API).
 */
@Component({
  selector: 'app-historico-importacoes',
  imports: [
    RouterLink,
    Button,
    Skeleton,
    TableModule,
    Tag,
    Tooltip,
    CabecalhoPaginaComponent,
    EstadoErroComponent,
    EstadoVazioComponent,
    SombraRolagemDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './historico-importacoes.component.html',
})
export class HistoricoImportacoesComponent {
  readonly grupo = input.required<string>();
  readonly entidade = input.required<string>();

  private readonly importacao = inject(ImportacaoService);
  private readonly metadados = inject(MetadadosService);
  private readonly mensagens = inject(MessageService);
  private readonly confirmacao = inject(ConfirmationService);
  private readonly router = inject(Router);
  readonly permissoes = inject(PermissoesService);
  readonly densidade = inject(DensidadeService);

  readonly meta = signal<Entidade | null>(null);
  readonly podeImportar = signal(false);
  readonly nomeGrupo = signal('');
  readonly pagina = signal<PaginaDe<ResumoImportacao> | null>(null);
  readonly carregando = signal(true);
  readonly erro = signal(false);
  readonly emAndamento = signal<string | null>(null);

  readonly titulo = 'Importações anteriores';
  readonly icone = computed(() => classeIcone(this.meta()));
  readonly trilha = computed<PassoTrilha[]>(() => {
    const meta = this.meta();
    return [
      { rotulo: this.nomeGrupo(), link: ['/', this.grupo()] },
      { rotulo: meta ? nomePlural(meta) : '', link: ['/', this.grupo(), this.entidade()] },
      { rotulo: this.titulo },
    ];
  });
  readonly subtitulo = computed(() => {
    const meta = this.meta();
    return meta ? `As planilhas importadas em ${nomePlural(meta)}. Os arquivos ficam guardados por 7 dias.` : null;
  });
  readonly linhasEsqueleto = Array.from({ length: 5 }, (_, i) => i);
  readonly situacaoDaImportacao = situacaoDaImportacao;
  readonly textoTotais = textoTotais;
  readonly formatarDataHoraSistema = formatarDataHoraSistema;

  constructor() {
    effect(() => {
      const grupo = this.grupo();
      const entidade = this.entidade();
      untracked(() => void this.carregar(grupo, entidade));
    });
  }

  private async carregar(grupo: string, chave: string): Promise<void> {
    const meta = await this.metadados.entidade(grupo, chave);
    if (!meta) {
      void this.router.navigate(['/nao-encontrado'], { skipLocationChange: true });
      return;
    }
    const listagem = await this.metadados.listagem(grupo);
    this.podeImportar.set(this.permissoes.podeCriar(meta, listagem?.minhasEquipes ?? []));
    this.meta.set(meta);
    void this.metadados.nomeDoGrupo(grupo).then(
      (nome) => this.nomeGrupo.set(nome),
      () => this.nomeGrupo.set(grupo),
    );
    await this.consultar(1, TAMANHO_PAGINA);
  }

  aoMudarPagina(evento: TableLazyLoadEvent): void {
    const tamanho = evento.rows ?? TAMANHO_PAGINA;
    void this.consultar(Math.floor((evento.first ?? 0) / tamanho) + 1, tamanho);
  }

  tentarDeNovo(): void {
    void this.consultar(this.pagina()?.pagina ?? 1, this.pagina()?.tamanho ?? TAMANHO_PAGINA);
  }

  private async consultar(pagina: number, tamanho: number): Promise<void> {
    this.carregando.set(true);
    this.erro.set(false);
    try {
      this.pagina.set(await this.importacao.listar(this.grupo(), this.entidade(), pagina, tamanho));
    } catch {
      this.erro.set(true);
    } finally {
      this.carregando.set(false);
    }
  }

  async baixarRelatorio(item: ResumoImportacao): Promise<void> {
    this.emAndamento.set(item.id);
    try {
      const { arquivo, nome } = await this.importacao.baixarRelatorio(this.grupo(), this.entidade(), item.id);
      baixarArquivo(arquivo, nome);
    } catch {
      // O interceptor já mostrou a mensagem (ex.: arquivo expirado).
    } finally {
      this.emAndamento.set(null);
    }
  }

  desfazer(item: ResumoImportacao): void {
    this.confirmacao.confirm({
      header: 'Desfazer importação',
      message: `${item.nomeArquivo}: ${mensagemDesfazer(item.totais)}`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Desfazer importação',
      rejectLabel: 'Cancelar',
      acceptButtonProps: { severity: 'danger', label: 'Desfazer importação' },
      rejectButtonProps: { severity: 'secondary', text: true, label: 'Cancelar' },
      accept: () => void this.confirmarDesfazer(item),
    });
  }

  private async confirmarDesfazer(item: ResumoImportacao): Promise<void> {
    this.emAndamento.set(item.id);
    try {
      await this.importacao.desfazer(this.grupo(), this.entidade(), item.id, tratarNoComponente(409));
      this.metadados.invalidar(this.grupo());
      this.mensagens.add({
        severity: 'success',
        summary: 'Importação desfeita: os itens criados por ela foram excluídos.',
      });
      this.tentarDeNovo();
    } catch (erro) {
      const lido = lerProblema(erro);
      if (lido.status === 409) {
        const linhas = linhasDosImpedimentos(lido.detalhes);
        this.confirmacao.confirm({
          key: 'bloqueio',
          header: 'Não é possível desfazer',
          message:
            linhas.length > 0 ? ['A importação não pode ser desfeita:', ...linhas].join('\n') : (lido.detalhe ?? ''),
          icon: 'pi pi-ban',
          acceptLabel: 'Entendi',
          rejectVisible: false,
          acceptButtonProps: { label: 'Entendi' },
        });
      }
    } finally {
      this.emAndamento.set(null);
    }
  }
}
