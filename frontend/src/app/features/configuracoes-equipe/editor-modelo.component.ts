import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { InputNumber } from 'primeng/inputnumber';
import { InputText } from 'primeng/inputtext';
import { Message } from 'primeng/message';
import { Select } from 'primeng/select';
import { Skeleton } from 'primeng/skeleton';
import { Textarea } from 'primeng/textarea';
import { ToggleSwitch } from 'primeng/toggleswitch';
import { Tooltip } from 'primeng/tooltip';
import { AvaliacaoService } from '../../core/avaliacao/avaliacao.service';
import { ModeloAvaliacao, RegraComentario, TipoCriterio } from '../../core/avaliacao/modelo-avaliacao';
import { RespostaFormulario } from '../../core/avaliacao/regras-avaliacao';
import { tratarNoComponente } from '../../core/erros/contexto-erros';
import { mensagemDoErro } from '../../core/erros/mensagens-erro';
import { lerProblema } from '../../core/erros/problema';
import { MetadadosService } from '../../core/metadados/metadados.service';
import { nomePlural } from '../../core/metadados/regras-metadados';
import { FormularioAvaliacaoComponent } from '../../shared/avaliacao/formulario-avaliacao.component';
import { moverItem } from '../administracao/listagens/modelo/regras-estrutura';
import {
  CriterioEditavel,
  chaveDoCriterio,
  corpoDoModelo,
  novoCriterio,
  separarErros,
  travasDoCriterio,
} from './modelo/regras-modelo';
import { CabecalhoPaginaComponent } from '../../shared/pagina/cabecalho-pagina.component';

/**
 * /configuracoes/equipe/:equipeId/:grupo/:entidade/modelo (o modelo ATIVO) e .../novo-modelo — o editor de um modelo de avaliação
 * (RN40 a RN43): nome, descrição e os critérios (ordem por arrastar e soltar, ou pelos botões de mover), com a pré-visualização
 * do formulário ao lado. A chave do critério é gerada do nome e trava depois de gravada; critérios com respostas não saem da lista
 * nem mudam de tipo (a saída é desativar). A edição envia a `versao`: num 409, avisa e recarrega.
 */
@Component({
  selector: 'app-editor-modelo',
  imports: [
    CabecalhoPaginaComponent,
    FormsModule,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
    Button,
    InputNumber,
    InputText,
    Message,
    Select,
    Skeleton,
    Textarea,
    ToggleSwitch,
    Tooltip,
    FormularioAvaliacaoComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './editor-modelo.component.html',
})
export class EditorModeloComponent {
  readonly equipeId = input.required<string>();
  readonly grupo = input.required<string>();
  readonly entidade = input.required<string>();
  /** Da rota: true em .../novo-modelo. */
  readonly novo = input(false);

  private readonly avaliacao = inject(AvaliacaoService);
  private readonly metadados = inject(MetadadosService);
  private readonly mensagens = inject(MessageService);
  private readonly router = inject(Router);

  readonly carregado = signal(false);
  readonly modelo = signal<ModeloAvaliacao | null>(null);
  readonly nome = signal('');
  readonly descricao = signal('');
  readonly criterios = signal<CriterioEditavel[]>([]);
  readonly nomeEntidade = signal('');
  readonly salvando = signal(false);
  readonly errosModelo = signal<Partial<Record<string, string[]>>>({});
  readonly errosCriterios = signal<Partial<Record<number, string[]>>>({});
  readonly conflito = signal(false);
  readonly valoresPrevia = signal<Record<string, RespostaFormulario>>({});

  readonly tipos: { valor: TipoCriterio; rotulo: string }[] = [
    { valor: 'ESTRELAS', rotulo: 'Estrelas (1 a 5)' },
    { valor: 'TEXTO', rotulo: 'Texto livre' },
  ];
  readonly regrasComentario: { valor: RegraComentario; rotulo: string }[] = [
    { valor: 'NENHUM', rotulo: 'Sem comentário' },
    { valor: 'OPCIONAL', rotulo: 'Comentário opcional' },
    { valor: 'OBRIGATORIO', rotulo: 'Comentário obrigatório' },
  ];
  readonly travas = travasDoCriterio;
  readonly titulo = computed(() => (this.modelo() ? `Editar: ${this.modelo()!.nome}` : 'Novo modelo de avaliação'));

  constructor() {
    effect(() => {
      const [equipeId, grupo, entidade, novo] = [this.equipeId(), this.grupo(), this.entidade(), this.novo()];
      untracked(() => void this.carregar(equipeId, grupo, entidade, novo));
    });
  }

  async carregar(
    equipeId = this.equipeId(),
    grupo = this.grupo(),
    entidade = this.entidade(),
    novo = this.novo(),
  ): Promise<void> {
    this.carregado.set(false);
    this.conflito.set(false);
    this.errosModelo.set({});
    this.errosCriterios.set({});
    const meta = await this.metadados.entidade(grupo, entidade);
    this.nomeEntidade.set(meta ? nomePlural(meta) : entidade);
    const ativo = novo
      ? null
      : ((await this.avaliacao.modelos(equipeId, grupo, entidade)).find((m) => m.ativo) ?? null);
    this.modelo.set(ativo);
    this.nome.set(ativo?.nome ?? '');
    this.descricao.set(ativo?.descricao ?? '');
    this.criterios.set(ativo ? ativo.criterios.map((c) => ({ ...c, salvo: true })) : [{ ...novoCriterio(1) }]);
    this.carregado.set(true);
  }

  // ---- Critérios ----

  mudarCriterio(indice: number, mudanca: Partial<CriterioEditavel>): void {
    this.criterios.update((lista) =>
      lista.map((c, i) => {
        if (i !== indice) {
          return c;
        }
        const novo = { ...c, ...mudanca };
        // Enquanto não foi gravado, a chave acompanha o nome (RN42: depois, ela trava).
        if (mudanca.nome !== undefined && !c.salvo) {
          novo.chave = chaveDoCriterio(
            mudanca.nome,
            lista.filter((_, j) => j !== i).map((x) => x.chave),
          );
        }
        if (novo.tipo === 'TEXTO') {
          novo.comentario = 'NENHUM';
        }
        return novo;
      }),
    );
  }

  adicionarCriterio(): void {
    this.criterios.update((lista) => [...lista, novoCriterio(lista.length + 1)]);
  }

  removerCriterio(indice: number): void {
    this.criterios.update((lista) => lista.filter((_, i) => i !== indice));
  }

  mover(de: number, para: number): void {
    if (para >= 0 && para < this.criterios().length) {
      this.criterios.update((lista) => moverItem(lista, de, para));
    }
  }

  aoSoltar(evento: CdkDragDrop<unknown>): void {
    this.mover(evento.previousIndex, evento.currentIndex);
  }

  // ---- Gravar ----

  async salvar(): Promise<void> {
    const [equipeId, grupo, entidade] = [this.equipeId(), this.grupo(), this.entidade()];
    const modelo = this.modelo();
    const corpo = corpoDoModelo(this.nome(), this.descricao(), this.criterios(), modelo?.versao ?? null);
    this.salvando.set(true);
    this.errosModelo.set({});
    this.errosCriterios.set({});
    try {
      const contexto = tratarNoComponente(400, 409);
      if (modelo) {
        await this.avaliacao.alterarModelo(equipeId, grupo, entidade, corpo, contexto);
      } else {
        await this.avaliacao.criarModelo(equipeId, grupo, entidade, corpo, contexto);
      }
      this.metadados.invalidar(grupo);
      this.mensagens.add({
        severity: 'success',
        summary: modelo ? 'Modelo de avaliação salvo.' : 'Modelo de avaliação criado.',
      });
      void this.router.navigate(['/configuracoes/equipe'], { queryParams: { equipeId, grupo } });
    } catch (erro) {
      const problema = lerProblema(erro);
      if (problema.status === 400) {
        const { modelo: doModelo, criterios } = separarErros(problema.erros);
        this.errosModelo.set(doModelo);
        this.errosCriterios.set(criterios);
      } else if (problema.codigo === 'CONFLITO_CONCORRENCIA') {
        this.conflito.set(true);
      } else {
        this.errosModelo.set({ '': [problema.detalhe ?? mensagemDoErro(problema.codigo, problema.status)] });
      }
    } finally {
      this.salvando.set(false);
    }
  }

  cancelar(): void {
    void this.router.navigate(['/configuracoes/equipe'], {
      queryParams: { equipeId: this.equipeId(), grupo: this.grupo() },
    });
  }

  errosDoModelo(chave: string): string[] {
    return this.errosModelo()[chave] ?? [];
  }

  errosDoCriterio(indice: number): string[] {
    return this.errosCriterios()[indice] ?? [];
  }

  errosGerais(): string[] {
    return Object.entries(this.errosModelo())
      .filter(([chave]) => chave !== 'nome' && chave !== 'descricao')
      .flatMap(([, mensagens]) => mensagens ?? []);
  }
}
