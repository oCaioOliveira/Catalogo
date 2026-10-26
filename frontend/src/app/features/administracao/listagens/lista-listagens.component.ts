import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { InputText } from 'primeng/inputtext';
import { Message } from 'primeng/message';
import { Skeleton } from 'primeng/skeleton';
import { TableModule } from 'primeng/table';
import { Tag } from 'primeng/tag';
import { Textarea } from 'primeng/textarea';
import { ResumoListagem } from '../../../core/administracao/estrutura-listagem';
import { MetadadosAdminService } from '../../../core/administracao/metadados-admin.service';
import { DescritoPorDirective } from '../../../shared/acessibilidade/descrito-por.directive';
import { AlternarDensidadeComponent } from '../../../shared/tabelas/alternar-densidade.component';
import { DensidadeService } from '../../../shared/tabelas/densidade.service';
import { AlteracaoEstruturaService } from './comum/alteracao-estrutura.service';
import { aplicarErros, errosDe, ligarChaveAoNome } from './comum/chave-automatica';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';
import { EstadoErroComponent } from '../../../shared/estados/estado-erro.component';
import { EstadoVazioComponent } from '../../../shared/estados/estado-vazio.component';
import { MenuAcoesLinhaComponent } from '../../../shared/tabelas/menu-acoes-linha.component';
import { SombraRolagemDirective } from '../../../shared/tabelas/sombra-rolagem.directive';
import { MenuItem } from 'primeng/api';
import { cliqueAbreLinha } from '../../../shared/tabelas/colunas-tabela';
import { AreaAtualService } from '../../../core/metadados/area-atual.service';

/**
 * /configuracoes/areas — as listagens (grupos), ativas e inativas, com quantos tipos de item e itens cada uma tem. Criar, editar e
 * ativar/desativar passam pela simulação (AlteracaoEstruturaService). A chave é gerada a partir do nome e só pode ser editada na
 * criação (RN51).
 */
@Component({
  selector: 'app-lista-listagens',
  imports: [
    SombraRolagemDirective,
    MenuAcoesLinhaComponent,
    EstadoVazioComponent,
    EstadoErroComponent,
    CabecalhoPaginaComponent,
    ReactiveFormsModule,
    RouterLink,
    Button,
    Dialog,
    InputText,
    Message,
    Skeleton,
    TableModule,
    Tag,
    Textarea,
    DescritoPorDirective,
    AlternarDensidadeComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './lista-listagens.component.html',
})
export class ListaListagensComponent {
  readonly densidade = inject(DensidadeService);
  private readonly admin = inject(MetadadosAdminService);
  private readonly alteracao = inject(AlteracaoEstruturaService);
  private readonly router = inject(Router);
  private readonly destruir = inject(DestroyRef);

  readonly listagens = signal<ResumoListagem[] | null>(null);
  /** A área atual, marcada na lista. */
  readonly areaAtual = inject(AreaAtualService).grupo;
  /** As ações ("⋯") de cada linha: editar e ativar ou desativar. */
  readonly acoesPorLinha = computed(() => {
    const mapa = new Map<string, MenuItem[]>();
    for (const listagem of this.listagens() ?? []) {
      mapa.set(listagem.chave, [
        { label: 'Editar', icon: 'pi pi-pencil', command: () => this.editar(listagem) },
        listagem.ativo
          ? {
              label: 'Desativar',
              icon: 'pi pi-ban',
              styleClass: 'acao-perigosa',
              command: () => void this.alternarAtivacao(listagem),
            }
          : { label: 'Ativar', icon: 'pi pi-check-circle', command: () => void this.alternarAtivacao(listagem) },
      ]);
    }
    return mapa;
  });
  readonly erro = signal(false);

  /** O diálogo de criar (editando = null) ou editar uma listagem. */
  readonly dialogoAberto = signal(false);
  readonly editando = signal<ResumoListagem | null>(null);
  readonly salvando = signal(false);
  readonly errosGerais = signal<string[]>([]);
  readonly nome = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(150)],
  });
  readonly chave = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/^[a-z0-9_]+$/), Validators.maxLength(100)],
  });
  readonly descricao = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] });
  readonly errosDe = errosDe;

  constructor() {
    ligarChaveAoNome(this.nome, this.chave, this.destruir);
    void this.carregar();
  }

  async carregar(): Promise<void> {
    this.erro.set(false);
    try {
      this.listagens.set(await this.admin.listagens());
    } catch {
      this.erro.set(true);
    }
  }

  nova(): void {
    this.editando.set(null);
    this.abrir('', '', '');
    this.chave.enable();
  }

  /** A linha abre a área de gestão (menos cliques no link e no "⋯"). */
  abrirLinha(listagem: ResumoListagem, evento: MouseEvent): void {
    if (cliqueAbreLinha(evento.target as Element, window.getSelection()?.toString() ?? '')) {
      void this.router.navigate(['/configuracoes/areas', listagem.chave]);
    }
  }

  editar(listagem: ResumoListagem): void {
    this.editando.set(listagem);
    this.abrir(listagem.nome, listagem.chave, listagem.descricao ?? '');
    this.chave.disable();
  }

  private abrir(nome: string, chave: string, descricao: string): void {
    this.nome.reset(nome);
    this.chave.reset(chave);
    this.descricao.reset(descricao);
    this.errosGerais.set([]);
    this.dialogoAberto.set(true);
  }

  async salvar(): Promise<void> {
    for (const c of [this.nome, this.chave, this.descricao]) {
      c.markAsTouched();
    }
    if (this.nome.invalid || (this.chave.enabled && this.chave.invalid) || this.descricao.invalid) {
      return;
    }
    const editando = this.editando();
    const corpo = { nome: this.nome.value.trim(), descricao: this.descricao.value.trim() || null };
    this.salvando.set(true);
    const desfecho = await this.alteracao.executar(
      editando
        ? {
            grupo: editando.chave,
            titulo: `Alterar a área de gestão "${corpo.nome}"`,
            chamar: (simular) =>
              this.admin.alterarListagem(
                editando.chave,
                { ...corpo, versao: editando.versao, chave: editando.chave },
                simular,
              ),
          }
        : {
            grupo: this.chave.value,
            titulo: `Criar a área de gestão "${corpo.nome}"`,
            chamar: (simular) => this.admin.criarListagem({ ...corpo, chave: this.chave.value }, simular),
          },
    );
    this.salvando.set(false);
    if (desfecho.status === 'gravado') {
      this.dialogoAberto.set(false);
      if (editando) {
        await this.carregar();
      } else {
        void this.router.navigate(['/configuracoes/areas', this.chave.value]);
      }
    } else if (desfecho.status === 'invalido') {
      const gerais = aplicarErros(
        { nome: this.nome, chave: this.chave, descricao: this.descricao } as Record<string, FormControl<unknown>>,
        desfecho.erros,
      );
      this.errosGerais.set(desfecho.mensagem ? [desfecho.mensagem, ...gerais] : gerais);
    } else if (desfecho.status === 'conflito') {
      this.dialogoAberto.set(false);
      await this.carregar();
    }
  }

  async alternarAtivacao(listagem: ResumoListagem): Promise<void> {
    const desfecho = await this.alteracao.executar({
      grupo: listagem.chave,
      titulo: `${listagem.ativo ? 'Desativar' : 'Ativar'} a área de gestão "${listagem.nome}"`,
      chamar: (simular) => this.admin.ativarListagem(listagem.chave, listagem.versao, !listagem.ativo, simular),
    });
    if (desfecho.status === 'gravado' || desfecho.status === 'conflito') {
      await this.carregar();
    }
  }
}
