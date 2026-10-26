import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Skeleton } from 'primeng/skeleton';
import { AdministracaoService } from '../../../core/administracao/administracao.service';
import { tratarNoComponente } from '../../../core/erros/contexto-erros';
import { mensagemDoErro } from '../../../core/erros/mensagens-erro';
import { lerProblema } from '../../../core/erros/problema';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';
import { FocoInicialDirective } from '../../../shared/acessibilidade/foco-inicial.directive';

/** /configuracoes/equipes/novo e /configuracoes/equipes/:id/editar: só o nome, único (o 409 de nome repetido aparece no campo). */
@Component({
  selector: 'app-formulario-equipe',
  imports: [
    FocoInicialDirective,
    CabecalhoPaginaComponent,
    ReactiveFormsModule,
    RouterLink,
    Button,
    InputText,
    Skeleton,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-cabecalho-pagina
      [trilha]="[
        { rotulo: 'Configurações' },
        { rotulo: 'Equipes', link: '/configuracoes/equipes' },
        { rotulo: edicao() ? 'Editar' : 'Nova' },
      ]"
      [titulo]="edicao() ? 'Editar equipe' : 'Nova equipe'"
      icone="pi-users"
    />
    @if (carregando()) {
      <p-skeleton height="8rem" />
    } @else {
      <form class="formulario formulario-estreito" [formGroup]="formulario" (ngSubmit)="salvar()" novalidate>
        <div class="campo">
          <label class="campo-rotulo" for="equipe-nome">
            Nome <span class="obrigatorio" aria-hidden="true">*</span><span class="sr-only">(obrigatório)</span>
          </label>
          <input
            pInputText
            id="equipe-nome"
            appFocoInicial
            autocomplete="off"
            formControlName="nome"
            [attr.aria-describedby]="erros().length > 0 ? 'equipe-nome-erros' : null"
            [attr.aria-invalid]="erros().length > 0"
          />
          @if (erros().length > 0) {
            <ul class="campo-erros" id="equipe-nome-erros" aria-live="polite">
              @for (erro of erros(); track $index) {
                <li>{{ erro }}</li>
              }
            </ul>
          }
        </div>
        <div class="acoes-formulario">
          <p-button
            type="button"
            label="Cancelar"
            severity="secondary"
            [text]="true"
            [routerLink]="edicao() ? ['/configuracoes/equipes', id()] : ['/configuracoes/equipes']"
          />
          <p-button
            type="submit"
            [label]="edicao() ? 'Salvar' : 'Criar equipe'"
            icon="pi pi-check"
            [loading]="salvando()"
          />
        </div>
      </form>
    }
  `,
})
export class FormularioEquipeComponent {
  readonly id = input<string | undefined>(undefined);

  private readonly administracao = inject(AdministracaoService);
  private readonly router = inject(Router);
  private readonly mensagens = inject(MessageService);

  readonly nome = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(150)],
  });
  readonly formulario = new FormGroup({ nome: this.nome });
  readonly edicao = computed(() => !!this.id());
  readonly carregando = signal(false);
  readonly salvando = signal(false);
  readonly tentouSalvar = signal(false);
  readonly errosServidor = signal<string[]>([]);

  constructor() {
    effect(() => {
      const id = this.id();
      untracked(() => void this.carregar(id));
    });
  }

  private async carregar(id: string | undefined): Promise<void> {
    if (!id) {
      return;
    }
    this.carregando.set(true);
    try {
      this.nome.setValue((await this.administracao.obterEquipe(id)).nome);
    } catch {
      void this.router.navigate(['/configuracoes/equipes']);
    } finally {
      this.carregando.set(false);
    }
  }

  erros(): string[] {
    const cliente: string[] = [];
    if ((this.nome.dirty || this.tentouSalvar()) && this.nome.hasError('required')) {
      cliente.push('Campo obrigatório.');
    }
    if (this.nome.hasError('maxlength')) {
      cliente.push('Deve ter no máximo 150 caracteres.');
    }
    return [...cliente, ...this.errosServidor()];
  }

  async salvar(): Promise<void> {
    this.tentouSalvar.set(true);
    this.nome.markAsTouched();
    if (this.nome.invalid) {
      return;
    }
    this.errosServidor.set([]);
    this.salvando.set(true);
    try {
      const id = this.id();
      const equipe = id
        ? await this.administracao.atualizarEquipe(id, this.nome.value.trim(), tratarNoComponente(400, 409))
        : await this.administracao.criarEquipe(this.nome.value.trim(), tratarNoComponente(400, 409));
      this.mensagens.add({ severity: 'success', summary: id ? 'Equipe salva.' : 'Equipe criada.' });
      void this.router.navigate(['/configuracoes/equipes', equipe.id]);
    } catch (erro) {
      const problema = lerProblema(erro);
      const doCampo = problema.erros['nome'] ?? [];
      // 409 de nome repetido: o detalhe da API diz qual nome (é o campo que ela aponta em detalhes.campos); os demais, pelo código.
      const mensagem =
        problema.codigo === 'CONFLITO_UNICIDADE' && problema.detalhe
          ? problema.detalhe
          : mensagemDoErro(problema.codigo, problema.status);
      this.errosServidor.set(doCampo.length > 0 ? doCampo : [mensagem]);
    } finally {
      this.salvando.set(false);
    }
  }
}
