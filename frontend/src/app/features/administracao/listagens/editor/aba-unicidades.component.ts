import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Message } from 'primeng/message';
import { MultiSelect } from 'primeng/multiselect';
import { Tooltip } from 'primeng/tooltip';
import { EntidadeEstrutura, EstruturaListagem } from '../../../../core/administracao/estrutura-listagem';
import { MetadadosAdminService } from '../../../../core/administracao/metadados-admin.service';
import { AlteracaoEstruturaService, Desfecho } from '../comum/alteracao-estrutura.service';
import { MAXIMO_CAMPOS_UNICIDADE, camposParaUnicidade, nomeDeUnicidade } from '../modelo/regras-estrutura';

/**
 * Aba Unicidades (RN09, RN55, RN71, RN72): cada restrição impede dois itens com os mesmos valores nos campos dela (sem diferenciar
 * maiúsculas nem acentos; nos tipos por equipe, dentro da equipe). Criar com duplicados existentes é bloqueado, e o relatório mostra
 * os grupos de repetidos. Remover é sempre permitido.
 */
@Component({
  selector: 'app-aba-unicidades',
  imports: [ReactiveFormsModule, Button, InputText, Message, MultiSelect, Tooltip],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="texto-secundario">
      Uma unicidade impede dois itens com os mesmos valores nos campos escolhidos (ex.: dois equipamentos com o mesmo
      nome). Maiúsculas e acentos não contam. Em tipos por equipe, vale dentro de cada equipe.
    </p>
    @if (entidade().restricoes.length === 0) {
      <p>Nenhuma unicidade.</p>
    } @else {
      <ul class="lista-unicidades">
        @for (restricao of entidade().restricoes; track restricao.nome) {
          <li>
            <span>
              <strong>{{ nomesDosCampos(restricao.campos) }}</strong>
              <span class="texto-secundario"> ({{ restricao.nome }})</span>
            </span>
            <p-button
              icon="pi pi-trash"
              severity="danger"
              [text]="true"
              [rounded]="true"
              size="small"
              [ariaLabel]="'Remover a unicidade ' + restricao.nome"
              pTooltip="Remover"
              (onClick)="remover(restricao.nome)"
            />
          </li>
        }
      </ul>
    }

    <form class="formulario-admin nova-unicidade" (submit)="$event.preventDefault(); criar()" novalidate>
      <h3>Nova unicidade</h3>
      @if (erros().length > 0) {
        <p-message severity="error" styleClass="mb-3">
          @for (m of erros(); track m) {
            <div>{{ m }}</div>
          }
        </p-message>
      }
      <div class="grade-campos">
        <div class="campo">
          <label id="unicidade-campos-rotulo" for="unicidade-campos" class="campo-rotulo">Campos (de 1 a 5)</label>
          <p-multiselect
            inputId="unicidade-campos"
            ariaLabelledBy="unicidade-campos-rotulo"
            optionLabel="nome"
            optionValue="chave"
            placeholder="Escolha os campos"
            appendTo="body"
            display="chip"
            [selectionLimit]="maximo"
            [showToggleAll]="false"
            [options]="opcoes()"
            [formControl]="campos"
          />
        </div>
        <div class="campo">
          <label for="unicidade-nome" class="campo-rotulo">Nome da regra</label>
          <input pInputText id="unicidade-nome" [formControl]="nome" [placeholder]="nomeSugerido()" />
          <small class="campo-dica"
            >Opcional: sem ele, usa {{ nomeSugerido() || 'um nome gerado pelos campos' }}.</small
          >
        </div>
      </div>
      <div class="acoes-formulario">
        <p-button
          type="submit"
          icon="pi pi-plus"
          label="Criar unicidade"
          [disabled]="(camposEscolhidos()?.length ?? 0) === 0"
        />
      </div>
    </form>
  `,
})
export class AbaUnicidadesComponent {
  readonly estrutura = input.required<EstruturaListagem>();
  readonly entidade = input.required<EntidadeEstrutura>();
  readonly alterada = output<EstruturaListagem | null>();

  private readonly admin = inject(MetadadosAdminService);
  private readonly alteracao = inject(AlteracaoEstruturaService);

  readonly maximo = MAXIMO_CAMPOS_UNICIDADE;
  readonly campos = new FormControl<string[]>([], { nonNullable: true });
  readonly nome = new FormControl('', { nonNullable: true });
  readonly camposEscolhidos = toSignal(this.campos.valueChanges);
  readonly erros = signal<string[]>([]);
  readonly opcoes = computed(() => camposParaUnicidade(this.entidade()).map((c) => ({ chave: c.chave, nome: c.nome })));
  readonly nomeSugerido = computed(() => {
    const campos = this.camposEscolhidos() ?? [];
    return campos.length ? nomeDeUnicidade(this.entidade().chave, campos) : '';
  });

  nomesDosCampos(chaves: string[]): string {
    return chaves.map((c) => this.entidade().campos.find((x) => x.chave === c)?.nome ?? c).join(' + ');
  }

  async criar(): Promise<void> {
    const campos = this.campos.value;
    if (campos.length === 0) {
      return;
    }
    const entidade = this.entidade();
    const nome = this.nome.value.trim() || nomeDeUnicidade(entidade.chave, campos);
    const desfecho = await this.alteracao.executar({
      grupo: this.estrutura().chave,
      entidade: entidade.chave,
      titulo: `Criar a unicidade de ${this.nomesDosCampos(campos)}`,
      chamar: (simular) =>
        this.admin.criarRestricao(this.estrutura().chave, entidade.chave, entidade.versao, nome, campos, simular),
    });
    if (desfecho.status === 'gravado') {
      this.campos.reset([]);
      this.nome.reset('');
    }
    this.tratar(desfecho);
  }

  async remover(restricao: string): Promise<void> {
    const entidade = this.entidade();
    const desfecho = await this.alteracao.executar({
      grupo: this.estrutura().chave,
      entidade: entidade.chave,
      titulo: `Remover a unicidade "${restricao}"`,
      chamar: (simular) =>
        this.admin.excluirRestricao(this.estrutura().chave, entidade.chave, restricao, entidade.versao, simular),
    });
    this.tratar(desfecho);
  }

  private tratar(desfecho: Desfecho): void {
    this.erros.set([]);
    if (desfecho.status === 'gravado') {
      this.alterada.emit(desfecho.estrutura);
    } else if (desfecho.status === 'conflito') {
      this.alterada.emit(null);
    } else if (desfecho.status === 'invalido') {
      this.erros.set([...(desfecho.mensagem ? [desfecho.mensagem] : []), ...Object.values(desfecho.erros).flat()]);
    }
  }
}
