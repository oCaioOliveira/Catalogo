import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { Message } from 'primeng/message';
import { Skeleton } from 'primeng/skeleton';
import { lerProblema } from '../../../core/erros/problema';
import { MetadadosService } from '../../../core/metadados/metadados.service';
import { Entidade } from '../../../core/metadados/modelo';
import { classeIcone, nomePlural } from '../../../core/metadados/regras-metadados';
import { Registro } from '../../../core/registros/modelo-registro';
import { RegistrosService } from '../../../core/registros/registros.service';
import { CampoFixo, FormularioRegistroComponent } from '../../../shared/formulario/formulario-registro.component';
import { PassoTrilha } from '../../../shared/navegacao/trilha.component';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';
import { ComAlteracoesPendentes } from '../../../shared/formulario/alteracoes-pendentes.guard';
import { OpcoesFiltroService } from '../comum/opcoes-filtro.service';
import { rotuloDoRegistro } from '../comum/rotulos';
import { Preenchimento, preenchimentoDaUrl } from '../detalhe/relacionados';

/** RN34 e RN35: quando o item foi (ou voltou) para a aprovação, a mensagem diz isso. */
export function mensagemDeSalvo(anterior: Registro | null, salvo: Registro): string {
  if (salvo.situacaoAprovacao === 'PENDENTE' && anterior?.situacaoAprovacao !== 'PENDENTE') {
    return anterior ? 'Item reenviado para aprovação.' : 'Item criado e enviado para aprovação.';
  }
  return anterior ? 'Alterações salvas.' : 'Item criado.';
}

/**
 * Página de criar (/:grupo/:entidade/novo) e editar (/:grupo/:entidade/:id/editar), com o formulário genérico. O título usa o
 * nome no SINGULAR ("Novo: Relatório de Teste"); a trilha, o plural da listagem.
 * Vinda do "Novo …" de uma seção de relacionados (RN94): ?preencher=<campo>&com=<id>[&equipe=<id>]&voltar=<detalhe> traz a
 * referência já preenchida (editável) e a equipe sugerida; salvar ou cancelar volta para o detalhe de origem.
 */
@Component({
  selector: 'app-pagina-formulario',
  imports: [Message, Skeleton, FormularioRegistroComponent, CabecalhoPaginaComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (naoEncontrado()) {
      <p-message severity="warn"
        >Este item não está disponível: ele pode ter sido excluído, ou você não tem acesso a ele.</p-message
      >
    } @else if (meta() && (!id() || registro())) {
      @let m = meta()!;
      <div class="pagina-formulario">
        <app-cabecalho-pagina
          [trilha]="trilha()"
          [titulo]="registro() ? 'Editar: ' + tituloRegistro() : 'Novo: ' + m.nome"
          [icone]="icone()"
          [subtitulo]="registro() ? m.nome : m.descricao"
        />
        <app-formulario-registro
          [grupo]="grupo()"
          [entidade]="m"
          [registro]="registro()"
          [valorInicial]="valorInicial()"
          [equipeInicial]="preenchimento()?.equipeId ?? null"
          (salvo)="aoSalvar($event)"
          (cancelado)="cancelar()"
        />
      </div>
    } @else {
      <div aria-busy="true" aria-label="Carregando"><p-skeleton height="20rem" /></div>
    }
  `,
})
export class PaginaFormularioComponent implements ComAlteracoesPendentes {
  readonly grupo = input.required<string>();
  readonly entidade = input.required<string>();
  /** Só na edição. */
  readonly id = input<string | undefined>(undefined);

  private readonly metadados = inject(MetadadosService);
  private readonly registros = inject(RegistrosService);
  private readonly router = inject(Router);
  private readonly rota = inject(ActivatedRoute);
  private readonly mensagens = inject(MessageService);
  private readonly opcoes = inject(OpcoesFiltroService);

  readonly meta = signal<Entidade | null>(null);
  readonly registro = signal<Registro | null>(null);
  readonly naoEncontrado = signal(false);
  /** O pré-preenchimento vindo de uma seção de relacionados (só na criação). */
  readonly preenchimento = signal<Preenchimento | null>(null);
  readonly valorInicial = signal<CampoFixo | null>(null);
  private readonly formulario = viewChild(FormularioRegistroComponent);

  readonly icone = computed(() => classeIcone(this.meta()));
  readonly nomeArea = signal('');
  readonly trilha = computed<PassoTrilha[]>(() => {
    const meta = this.meta();
    if (!meta) {
      return [];
    }
    const area: PassoTrilha = { rotulo: this.nomeArea(), link: ['/', this.grupo()] };
    const lista: PassoTrilha = { rotulo: nomePlural(meta), link: ['/', this.grupo(), meta.chave] };
    const registro = this.registro();
    return registro
      ? [
          area,
          lista,
          { rotulo: this.tituloRegistro(), link: ['/', this.grupo(), meta.chave, registro.id] },
          { rotulo: 'Editar' },
        ]
      : [area, lista, { rotulo: 'Novo' }];
  });

  constructor() {
    effect(() => {
      const [grupo, entidade, id] = [this.grupo(), this.entidade(), this.id()];
      untracked(() => void this.carregar(grupo, entidade, id));
    });
  }

  temAlteracoesPendentes(): boolean {
    return this.formulario()?.temAlteracoesPendentes() ?? false;
  }

  tituloRegistro(): string {
    const meta = this.meta();
    const registro = this.registro();
    return meta && registro ? rotuloDoRegistro(meta, registro) : '';
  }

  private async carregar(grupo: string, chave: string, id: string | undefined): Promise<void> {
    const meta = await this.metadados.entidade(grupo, chave);
    if (!meta) {
      void this.router.navigate(['/nao-encontrado'], { skipLocationChange: true });
      return;
    }
    this.nomeArea.set(await this.metadados.nomeDoGrupo(grupo));
    const preenchimento = id ? null : preenchimentoDaUrl(this.rota.snapshot.queryParams, meta);
    this.preenchimento.set(preenchimento);
    if (preenchimento) {
      // O rótulo do item apontado, para o select mostrar o nome (a URL só traz o id). Antes do formulário montar.
      const campo = meta.campos.find((c) => c.chave === preenchimento.campo)!;
      const rotulo = await this.opcoes.rotulo(grupo, campo.entidadeReferenciada ?? '', preenchimento.id);
      this.valorInicial.set({
        chave: preenchimento.campo,
        valor: { id: preenchimento.id, rotulo: rotulo ?? preenchimento.id },
      });
    }
    if (id) {
      try {
        this.registro.set(await this.registros.obter(grupo, chave, id));
      } catch (erro) {
        this.naoEncontrado.set(lerProblema(erro).status === 404);
      }
    }
    this.meta.set(meta);
  }

  aoSalvar(salvo: Registro): void {
    this.mensagens.add({ severity: 'success', summary: mensagemDeSalvo(this.registro(), salvo) });
    const voltar = this.preenchimento()?.voltar;
    void (voltar
      ? this.router.navigateByUrl(voltar)
      : this.router.navigate(['/', this.grupo(), this.entidade(), salvo.id]));
  }

  cancelar(): void {
    const voltar = this.preenchimento()?.voltar;
    if (voltar) {
      void this.router.navigateByUrl(voltar);
      return;
    }
    const id = this.id();
    void this.router.navigate(id ? ['/', this.grupo(), this.entidade(), id] : ['/', this.grupo(), this.entidade()]);
  }
}
