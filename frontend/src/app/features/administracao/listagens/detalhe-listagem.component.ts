import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  linkedSignal,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { Message } from 'primeng/message';
import { RadioButton } from 'primeng/radiobutton';
import { Skeleton } from 'primeng/skeleton';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { Tag } from 'primeng/tag';
import { Textarea } from 'primeng/textarea';
import { Tooltip } from 'primeng/tooltip';
import {
  EntidadeEstrutura,
  EstruturaListagem,
  ResultadoAlteracao,
} from '../../../core/administracao/estrutura-listagem';
import { MetadadosAdminService } from '../../../core/administracao/metadados-admin.service';
import { lerProblema } from '../../../core/erros/problema';
import { DescritoPorDirective } from '../../../shared/acessibilidade/descrito-por.directive';
import { ComAlteracoesPendentes, confirmarSaidaSemSalvar } from '../../../shared/formulario/alteracoes-pendentes.guard';
import { CarregarHistorico, HistoricoComponent } from '../../../shared/historico/historico.component';
import { AbaEquipesComponent } from './aba-equipes.component';
import { AlteracaoEstruturaService } from './comum/alteracao-estrutura.service';
import { aplicarErros, errosDe, ligarChaveAoNome } from './comum/chave-automatica';
import { corpoDaEntidade, moverItem, paiDe } from './modelo/regras-estrutura';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';
import { AreaAtualService } from '../../../core/metadados/area-atual.service';

const ABAS = ['tipos', 'equipes', 'historico'];

/**
 * /configuracoes/areas/:grupo — os tipos de item da listagem, na ordem do menu (arrastar e soltar, com o @angular/cdk, ou pelos botões
 * de mover, para quem usa teclado), criar um tipo novo, a aba Equipes (as habilitadas, RN90 a RN93; `?aba=equipes` abre direto nela)
 * e a aba Histórico (METADADO). Sem nenhuma equipe habilitada, uma faixa avisa que só administradores veem a listagem (RN93).
 * Toda alteração passa pela simulação.
 */
@Component({
  selector: 'app-detalhe-listagem',
  imports: [
    CabecalhoPaginaComponent,
    ReactiveFormsModule,
    RouterLink,
    CdkDropList,
    CdkDrag,
    CdkDragHandle,
    Button,
    Dialog,
    InputText,
    Message,
    RadioButton,
    Skeleton,
    Tabs,
    TabList,
    Tab,
    TabPanels,
    TabPanel,
    Tag,
    Textarea,
    Tooltip,
    DescritoPorDirective,
    HistoricoComponent,
    AbaEquipesComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './detalhe-listagem.component.html',
})
export class DetalheListagemComponent implements ComAlteracoesPendentes {
  readonly grupo = input.required<string>();
  /** A aba pedida na URL (?aba=equipes, o atalho do detalhe da equipe e da faixa de aviso). */
  readonly aba = input<string | undefined>(undefined);

  private readonly admin = inject(MetadadosAdminService);
  private readonly alteracao = inject(AlteracaoEstruturaService);
  private readonly router = inject(Router);
  private readonly rota = inject(ActivatedRoute);
  private readonly destruir = inject(DestroyRef);
  private readonly areaAtual = inject(AreaAtualService);
  private readonly confirmacao = inject(ConfirmationService);

  readonly estrutura = signal<EstruturaListagem | null>(null);
  readonly naoEncontrada = signal(false);
  readonly reordenando = signal(false);
  /** A aba aberta. */
  readonly abaAberta = linkedSignal(() => (ABAS.includes(this.aba() ?? '') ? this.aba()! : 'tipos'));
  /** O valor do p-tabs: igual à aba aberta, menos enquanto o aviso de alterações pendentes espera a resposta. */
  readonly valorTabs = linkedSignal(() => this.abaAberta());
  private readonly abaEquipes = viewChild(AbaEquipesComponent);
  readonly carregarHistorico = computed<CarregarHistorico>(() => {
    const grupo = this.grupo();
    return (pagina, tamanho) => this.admin.historico(grupo, pagina, tamanho);
  });

  // Diálogo "Novo tipo de item".
  readonly dialogoAberto = signal(false);
  readonly salvando = signal(false);
  readonly errosGerais = signal<string[]>([]);
  readonly nome = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(150)],
  });
  readonly nomePlural = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(150)] });
  readonly chave = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^[a-z0-9_]+$/), Validators.maxLength(100)],
  });
  readonly descricao = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] });
  readonly escopoEquipe = new FormControl(true, { nonNullable: true });
  readonly errosDe = errosDe;
  readonly paiDe = paiDe;

  constructor() {
    ligarChaveAoNome(this.nome, this.chave, this.destruir);
    effect(() => {
      const grupo = this.grupo();
      untracked(() => void this.carregar(grupo));
    });
  }

  async carregar(grupo = this.grupo()): Promise<void> {
    try {
      const estrutura = await this.admin.estrutura(grupo);
      this.estrutura.set(estrutura);
      // Administrar os tipos de item de uma área (ativa) faz dela a área atual: ao voltar, a pessoa está nela.
      if (estrutura.ativo) {
        this.areaAtual.definir(grupo);
      }
    } catch (erro) {
      if (lerProblema(erro).status === 404) {
        this.naoEncontrada.set(true);
      } else {
        throw erro;
      }
    }
  }

  /** Troca de aba e guarda na URL (sem novo passo no histórico do navegador): recarregar ou compartilhar abre na mesma aba. */
  async mudarAba(aba: string | number | undefined): Promise<void> {
    const valor = String(aba ?? 'tipos');
    // Sair da aba Equipes com alterações pendentes: o mesmo aviso de sair da página. Continuar editando volta o p-tabs para lá.
    if (valor !== this.abaAberta() && this.temAlteracoesPendentes()) {
      const anterior = this.abaAberta();
      this.valorTabs.set(valor);
      if (!(await confirmarSaidaSemSalvar(this.confirmacao))) {
        this.valorTabs.set(anterior);
        return;
      }
    }
    this.abaAberta.set(valor);
    this.valorTabs.set(valor);
    void this.router.navigate([], {
      relativeTo: this.rota,
      queryParams: { aba: valor === 'tipos' ? undefined : valor },
      replaceUrl: true,
    });
  }

  /** Alterações não salvas na aba Equipes: alteracoesPendentesGuard (sair da página) e a troca de aba avisam antes. */
  temAlteracoesPendentes(): boolean {
    return this.abaEquipes()?.temAlteracoesPendentes() ?? false;
  }

  nomeDe(chave: string | null): string {
    return this.estrutura()?.entidades.find((e) => e.chave === chave)?.nome ?? chave ?? '';
  }

  // ---- Ordem ----

  aoSoltar(evento: CdkDragDrop<EntidadeEstrutura[]>): void {
    if (evento.previousIndex !== evento.currentIndex) {
      void this.reordenar(evento.previousIndex, evento.currentIndex);
    }
  }

  /** A ordem nova: um PUT por tipo de item que mudou de posição (a API não tem reordenação em lote de entidades). */
  async reordenar(de: number, para: number): Promise<void> {
    const estrutura = this.estrutura();
    if (!estrutura || para < 0 || para >= estrutura.entidades.length) {
      return;
    }
    const nova = moverItem(estrutura.entidades, de, para);
    // Mostra a ordem nova na hora; se a alteração não passar, recarrega.
    this.estrutura.set({ ...estrutura, entidades: nova });
    this.reordenando.set(true);
    const desfecho = await this.alteracao.executar({
      grupo: estrutura.chave,
      titulo: 'Reordenar os tipos de item',
      chamar: async (simular) => {
        let atual = estrutura;
        let ultimo: ResultadoAlteracao = { permitido: true, simulado: simular, impactos: [], estrutura: null };
        for (const [indice, entidade] of nova.entries()) {
          const naEstrutura = atual.entidades.find((e) => e.chave === entidade.chave)!;
          if (naEstrutura.ordem === indice + 1) {
            continue;
          }
          ultimo = await this.admin.alterarEntidade(
            estrutura.chave,
            entidade.chave,
            corpoDaEntidade(naEstrutura, { ordem: indice + 1 }),
            simular,
          );
          if (!ultimo.permitido) {
            return ultimo;
          }
          atual = ultimo.estrutura ?? atual;
        }
        return { ...ultimo, estrutura: simular ? null : atual };
      },
    });
    this.reordenando.set(false);
    if (desfecho.status === 'gravado' && desfecho.estrutura) {
      this.estrutura.set(desfecho.estrutura);
    } else {
      await this.carregar();
    }
  }

  // ---- Novo tipo de item ----

  novo(): void {
    this.nome.reset('');
    this.nomePlural.reset('');
    this.chave.reset('');
    this.descricao.reset('');
    this.escopoEquipe.reset(true);
    this.errosGerais.set([]);
    this.dialogoAberto.set(true);
  }

  async criar(): Promise<void> {
    const estrutura = this.estrutura();
    for (const c of [this.nome, this.chave, this.nomePlural, this.descricao]) {
      c.markAsTouched();
    }
    if (!estrutura || this.nome.invalid || this.chave.invalid || this.nomePlural.invalid || this.descricao.invalid) {
      return;
    }
    const chave = this.chave.value;
    this.salvando.set(true);
    const desfecho = await this.alteracao.executar({
      grupo: estrutura.chave,
      titulo: `Criar o tipo de item "${this.nome.value.trim()}"`,
      chamar: (simular) =>
        this.admin.criarEntidade(
          estrutura.chave,
          {
            versao: estrutura.versao,
            chave,
            nome: this.nome.value.trim(),
            nomePlural: this.nomePlural.value.trim() || null,
            descricao: this.descricao.value.trim() || null,
            escopoEquipe: this.escopoEquipe.value,
            ordem: Math.max(0, ...estrutura.entidades.map((e) => e.ordem)) + 1,
          },
          simular,
        ),
    });
    this.salvando.set(false);
    if (desfecho.status === 'gravado') {
      this.dialogoAberto.set(false);
      void this.router.navigate(['/configuracoes/areas', estrutura.chave, 'entidades', chave]);
    } else if (desfecho.status === 'invalido') {
      const gerais = aplicarErros(
        {
          nome: this.nome,
          chave: this.chave,
          nomePlural: this.nomePlural,
          descricao: this.descricao,
        } as Record<string, FormControl<unknown>>,
        desfecho.erros,
      );
      this.errosGerais.set(desfecho.mensagem ? [desfecho.mensagem, ...gerais] : gerais);
    } else if (desfecho.status === 'conflito') {
      this.dialogoAberto.set(false);
      await this.carregar();
    }
  }
}
