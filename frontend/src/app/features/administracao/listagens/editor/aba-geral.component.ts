import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Message } from 'primeng/message';
import { RadioButton } from 'primeng/radiobutton';
import { Select } from 'primeng/select';
import { Textarea } from 'primeng/textarea';
import { EntidadeEstrutura, EstruturaListagem } from '../../../../core/administracao/estrutura-listagem';
import { MetadadosAdminService } from '../../../../core/administracao/metadados-admin.service';
import { DescritoPorDirective } from '../../../../shared/acessibilidade/descrito-por.directive';
import { AlteracaoEstruturaService, Desfecho } from '../comum/alteracao-estrutura.service';
import { aplicarErros, errosDe } from '../comum/chave-automatica';
import { camposParaExibicao, corpoDaEntidade } from '../modelo/regras-estrutura';
import { SeletorIconeComponent } from '../../../../shared/icones/seletor-icone.component';

/**
 * Aba Geral do tipo de item: nome, plural, descrição, escopo (com as regras de mudança, RN74), campo de exibição (só os válidos,
 * RN54) e ativar/desativar (RN75). A chave não muda (RN51).
 */
@Component({
  selector: 'app-aba-geral',
  imports: [
    SeletorIconeComponent,
    ReactiveFormsModule,
    Button,
    InputText,
    Message,
    RadioButton,
    Select,
    Textarea,
    DescritoPorDirective,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="formulario-admin" (submit)="$event.preventDefault(); salvar()" novalidate>
      @if (errosGerais().length > 0) {
        <p-message severity="error" styleClass="mb-3">
          @for (m of errosGerais(); track m) {
            <div>{{ m }}</div>
          }
        </p-message>
      }
      <div class="grade-campos">
        <div class="campo">
          <label for="geral-nome" class="campo-rotulo">
            Nome (singular) <span class="obrigatorio" aria-hidden="true">*</span
            ><span class="sr-only">(obrigatório)</span>
          </label>
          <input
            pInputText
            id="geral-nome"
            [formControl]="nome"
            [invalido]="errosDe(nome).length > 0"
            [appDescritoPor]="null"
          />
          @for (m of errosDe(nome); track m) {
            <small class="campo-erros">{{ m }}</small>
          }
        </div>
        <div class="campo campo-largo">
          <span class="campo-rotulo" id="geral-icone-rotulo">Ícone</span>
          <app-seletor-icone
            aria-labelledby="geral-icone-rotulo"
            [valor]="icone()"
            [rotulo]="nome.value || entidade().nome"
            (mudou)="escolherIcone($event)"
          />
          <small class="campo-dica"
            >Aparece no menu, no título da lista, no detalhe, nas seções de relacionados e na página Início.</small
          >
          @for (m of errosIcone(); track m) {
            <small class="campo-erros">{{ m }}</small>
          }
        </div>
        <div class="campo">
          <label for="geral-plural" class="campo-rotulo">Nome no plural</label>
          <input pInputText id="geral-plural" [formControl]="nomePlural" />
          <small class="campo-dica"
            >No menu, nos títulos das listas e nas seções do detalhe. Sem ele, usa o nome.</small
          >
          @for (m of errosDe(nomePlural); track m) {
            <small class="campo-erros">{{ m }}</small>
          }
        </div>
        <div class="campo">
          <label for="geral-chave" class="campo-rotulo">Chave</label>
          <input
            pInputText
            id="geral-chave"
            [value]="entidade().chave"
            [disabled]="true"
            appDescritoPor="geral-chave-dica"
          />
          <small class="campo-dica" id="geral-chave-dica">
            <i class="pi pi-lock" aria-hidden="true"></i> A chave não muda: faz parte dos endereços e dos dados
            guardados.
          </small>
        </div>
      </div>
      <div class="campo">
        <label for="geral-descricao" class="campo-rotulo">Descrição</label>
        <textarea pTextarea id="geral-descricao" rows="2" [formControl]="descricao"></textarea>
      </div>

      <div class="campo">
        <label id="geral-exibicao-rotulo" for="geral-exibicao" class="campo-rotulo">Campo de exibição</label>
        <p-select
          inputId="geral-exibicao"
          ariaLabelledBy="geral-exibicao-rotulo"
          optionLabel="nome"
          optionValue="chave"
          placeholder="Nenhum"
          appendTo="body"
          [showClear]="true"
          [options]="opcoesExibicao()"
          [formControl]="campoExibicao"
          appDescritoPor="geral-exibicao-dica"
        />
        <small class="campo-dica" id="geral-exibicao-dica">
          O texto que representa cada item quando outro tipo aponta para ele (nos selects, nas tabelas e no título do
          detalhe). Escolha um campo que identifique o item, como o nome. Referências e textos longos não servem.
        </small>
        @for (m of errosDe(campoExibicao); track m) {
          <small class="campo-erros">{{ m }}</small>
        }
      </div>

      <fieldset class="grupo-opcoes">
        <legend class="campo-rotulo">Escopo</legend>
        <div class="opcao-explicada">
          <p-radiobutton
            inputId="geral-escopo-equipe"
            name="geral-escopo"
            [value]="true"
            [formControl]="escopoEquipe"
          />
          <label for="geral-escopo-equipe">
            <strong>Por equipe</strong>: cada item pertence a uma equipe, e só ela o vê e altera.
          </label>
        </div>
        <div class="opcao-explicada">
          <p-radiobutton
            inputId="geral-escopo-global"
            name="geral-escopo"
            [value]="false"
            [formControl]="escopoEquipe"
          />
          <label for="geral-escopo-global">
            <strong>Global</strong>: os mesmos itens para todas as equipes; só o administrador altera.
          </label>
        </div>
        <small class="campo-dica">
          @if (entidade().quantidadeRegistros > 0) {
            <i class="pi pi-lock" aria-hidden="true"></i> Este tipo já tem itens cadastrados: o escopo não muda mais.
          } @else {
            O escopo só muda enquanto não houver itens nem configurações de equipe. Um tipo global só pode apontar para
            outros globais.
          }
        </small>
        @for (m of errosDe(escopoEquipe); track m) {
          <small class="campo-erros">{{ m }}</small>
        }
      </fieldset>

      <div class="acoes-formulario acoes-fixas">
        <p-button type="submit" icon="pi pi-check" label="Salvar alterações" [loading]="salvando()" />
      </div>
    </form>

    <section class="secao-situacao">
      <h2>Situação</h2>
      @if (entidade().ativo) {
        <p>
          Ativo. Desativar esconde este tipo de item de quem não é administrador (o menu e os endereços dele somem);
          para o administrador, os itens ficam só para leitura. Nada é apagado, e dá para reativar depois.
        </p>
        <p-button
          label="Desativar este tipo de item"
          icon="pi pi-ban"
          severity="danger"
          [outlined]="true"
          (onClick)="alternarAtivacao()"
        />
      } @else {
        <p>Inativo: só o administrador o vê, e só para leitura.</p>
        <p-button
          label="Reativar este tipo de item"
          icon="pi pi-check-circle"
          severity="success"
          (onClick)="alternarAtivacao()"
        />
      }
    </section>
  `,
})
export class AbaGeralComponent {
  readonly estrutura = input.required<EstruturaListagem>();
  readonly entidade = input.required<EntidadeEstrutura>();
  readonly alterada = output<EstruturaListagem | null>();

  private readonly admin = inject(MetadadosAdminService);
  private readonly alteracao = inject(AlteracaoEstruturaService);

  readonly nome = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(150)],
  });
  readonly nomePlural = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(150)] });
  readonly descricao = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(500)] });
  readonly campoExibicao = new FormControl<string | null>(null);
  /** O ícone (RN95): escolhido no seletor visual; só vai para a API ao salvar, junto com o resto. */
  readonly icone = signal<string | null>(null);
  readonly errosIcone = signal<string[]>([]);
  readonly escopoEquipe = new FormControl(true, { nonNullable: true });
  readonly salvando = signal(false);
  readonly errosGerais = signal<string[]>([]);
  readonly opcoesExibicao = signal<{ chave: string; nome: string }[]>([]);
  readonly errosDe = errosDe;

  constructor() {
    // Estrutura nova (depois de gravar ou recarregar): o formulário mostra o que está gravado. O preenchimento fica fora do
    // rastreamento (untracked): o estado dos controles (touched, pristine) também é signal, e mexer num campo não pode refazer o reset.
    effect(() => {
      const e = this.entidade();
      untracked(() => this.preencher(e));
    });
  }

  private preencher(e: EntidadeEstrutura): void {
    {
      this.nome.reset(e.nome);
      this.nomePlural.reset(e.nomePlural ?? '');
      this.descricao.reset(e.descricao ?? '');
      this.campoExibicao.reset(e.campoExibicao);
      this.icone.set(e.icone);
      this.errosIcone.set([]);
      this.escopoEquipe.reset(e.escopoEquipe);
      if (e.quantidadeRegistros > 0) {
        this.escopoEquipe.disable();
      } else {
        this.escopoEquipe.enable();
      }
      this.opcoesExibicao.set(camposParaExibicao(e).map((c) => ({ chave: c.chave, nome: c.nome })));
    }
  }

  async salvar(): Promise<void> {
    this.nome.markAsTouched();
    if (this.nome.invalid || this.nomePlural.invalid || this.descricao.invalid) {
      return;
    }
    const e = this.entidade();
    this.salvando.set(true);
    const desfecho = await this.alteracao.executar({
      grupo: this.estrutura().chave,
      entidade: e.chave,
      titulo: `Alterar "${this.nome.value.trim()}"`,
      chamar: (simular) =>
        this.admin.alterarEntidade(
          this.estrutura().chave,
          e.chave,
          corpoDaEntidade(e, {
            nome: this.nome.value.trim(),
            nomePlural: this.nomePlural.value.trim() || null,
            descricao: this.descricao.value.trim() || null,
            campoExibicao: this.campoExibicao.value,
            escopoEquipe: this.escopoEquipe.getRawValue(),
            icone: this.icone(),
          }),
          simular,
        ),
    });
    this.salvando.set(false);
    this.tratar(desfecho);
  }

  escolherIcone(icone: string | null): void {
    this.icone.set(icone);
    this.errosIcone.set([]);
  }

  async alternarAtivacao(): Promise<void> {
    const e = this.entidade();
    const desfecho = await this.alteracao.executar({
      grupo: this.estrutura().chave,
      entidade: e.chave,
      titulo: `${e.ativo ? 'Desativar' : 'Reativar'} "${e.nome}"`,
      chamar: (simular) => this.admin.ativarEntidade(this.estrutura().chave, e.chave, e.versao, !e.ativo, simular),
    });
    this.tratar(desfecho);
  }

  private tratar(desfecho: Desfecho): void {
    this.errosGerais.set([]);
    if (desfecho.status === 'gravado') {
      this.alterada.emit(desfecho.estrutura);
    } else if (desfecho.status === 'conflito') {
      this.alterada.emit(null);
    } else if (desfecho.status === 'invalido') {
      const { icone, ...demais } = desfecho.erros;
      this.errosIcone.set(icone ?? []);
      const gerais = aplicarErros(
        {
          nome: this.nome,
          nomePlural: this.nomePlural,
          descricao: this.descricao,
          campoExibicao: this.campoExibicao,
          escopoEquipe: this.escopoEquipe,
        } as Record<string, FormControl<unknown>>,
        demais,
      );
      this.errosGerais.set(desfecho.mensagem ? [desfecho.mensagem, ...gerais] : gerais);
    }
  }
}
