import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
} from '@angular/core';
import { FormControl } from '@angular/forms';
import { Campo } from '../../core/metadados/modelo';
import { mensagensDeErro } from '../formulario/validadores';
import { ValorCampo } from '../formulario/valores';
import { CampoBooleanoComponent } from './campo-booleano.component';
import { CampoDataComponent } from './campo-data.component';
import { CampoListaComponent } from './campo-lista.component';
import { CampoNumeroComponent } from './campo-numero.component';
import { BuscarOpcoes, CampoReferenciaComponent } from './campo-referencia.component';
import { CampoTextoLongoComponent } from './campo-texto-longo.component';
import { CampoTextoComponent } from './campo-texto.component';
import { CampoUrlComponent } from './campo-url.component';

/**
 * Um campo do formulário genérico: o rótulo, o componente DO TIPO do campo (tabela em docs/frontend.md), a dica e
 * as mensagens de erro embaixo, ligadas ao campo por aria-describedby. Tipo de dado novo = um componente novo e
 * um @case aqui (ver frontend/AGENTS.md).
 */
@Component({
  selector: 'app-campo-formulario',
  imports: [
    CampoTextoComponent,
    CampoTextoLongoComponent,
    CampoNumeroComponent,
    CampoBooleanoComponent,
    CampoDataComponent,
    CampoListaComponent,
    CampoReferenciaComponent,
    CampoUrlComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="campo">
      <label [for]="inputId()" [id]="inputId() + '-rotulo'" class="campo-rotulo">
        {{ campo().nome }}
        @if (campo().obrigatorio) {
          <span class="obrigatorio" aria-hidden="true">*</span>
          <span class="sr-only">(obrigatório)</span>
        }
      </label>

      @switch (campo().tipo) {
        @case ('TEXTO') {
          <app-campo-texto
            [campo]="campo()"
            [controle]="controle()"
            [inputId]="inputId()"
            [descricaoId]="descricaoId()"
            [invalido]="mostrarErros()"
          />
        }
        @case ('TEXTO_LONGO') {
          <app-campo-texto-longo
            [campo]="campo()"
            [controle]="controle()"
            [inputId]="inputId()"
            [descricaoId]="descricaoId()"
            [invalido]="mostrarErros()"
          />
        }
        @case ('INTEIRO') {
          <app-campo-numero
            [campo]="campo()"
            [controle]="controle()"
            [inputId]="inputId()"
            [descricaoId]="descricaoId()"
            [invalido]="mostrarErros()"
          />
        }
        @case ('DECIMAL') {
          <app-campo-numero
            [campo]="campo()"
            [controle]="controle()"
            [inputId]="inputId()"
            [descricaoId]="descricaoId()"
            [invalido]="mostrarErros()"
          />
        }
        @case ('BOOLEANO') {
          <app-campo-booleano
            [campo]="campo()"
            [controle]="controle()"
            [inputId]="inputId()"
            [descricaoId]="descricaoId()"
            [invalido]="mostrarErros()"
          />
        }
        @case ('DATA') {
          <app-campo-data
            [campo]="campo()"
            [controle]="controle()"
            [inputId]="inputId()"
            [descricaoId]="descricaoId()"
            [invalido]="mostrarErros()"
          />
        }
        @case ('DATA_HORA') {
          <app-campo-data
            [campo]="campo()"
            [controle]="controle()"
            [inputId]="inputId()"
            [descricaoId]="descricaoId()"
            [invalido]="mostrarErros()"
          />
        }
        @case ('LISTA') {
          <app-campo-lista
            [campo]="campo()"
            [controle]="controle()"
            [inputId]="inputId()"
            [descricaoId]="descricaoId()"
            [invalido]="mostrarErros()"
          />
        }
        @case ('URL') {
          <app-campo-url
            [campo]="campo()"
            [controle]="controle()"
            [inputId]="inputId()"
            [descricaoId]="descricaoId()"
            [invalido]="mostrarErros()"
          />
        }
        @case ('REFERENCIA') {
          <app-campo-referencia
            [controle]="controle()"
            [inputId]="inputId()"
            [buscar]="buscar()!"
            [descricaoId]="descricaoId()"
            [invalido]="mostrarErros()"
            [criarNovo]="criarNovo()"
            (criar)="criar.emit($event)"
          />
        }
      }

      @if (dica()) {
        <small class="campo-dica" [id]="inputId() + '-dica'">{{ dica() }}</small>
      }
      @if (mostrarErros()) {
        <ul class="campo-erros" [id]="inputId() + '-erros'" aria-live="polite">
          @for (mensagem of mensagens(); track mensagem) {
            <li>{{ mensagem }}</li>
          }
        </ul>
      }
    </div>
  `,
})
export class CampoFormularioComponent {
  readonly campo = input.required<Campo>();
  readonly controle = input.required<FormControl<ValorCampo>>();
  /** Prefixo dos ids, para dois formulários na mesma tela (página e diálogo) não repetirem ids. */
  readonly prefixoId = input('campo');
  /** Busca das opções (só REFERENCIA). */
  readonly buscar = input<BuscarOpcoes | null>(null);
  /** Texto de ajuda embaixo do campo (ex.: por que ele está desabilitado). */
  readonly dica = input<string | null>(null);
  /** Só REFERENCIA: o nome do tipo referenciado, quando dá para criar um item novo dele sem sair. */
  readonly criarNovo = input<string | null>(null);
  /** "+ Criar <tipo>" escolhido, com o texto digitado. */
  readonly criar = output<string>();

  private readonly detector = inject(ChangeDetectorRef);

  readonly inputId = computed(() => `${this.prefixoId()}-${this.campo().chave}`);

  constructor() {
    // O componente é OnPush: quando o controle muda de estado (tocado, erros do servidor), redesenha.
    effect((limpar) => {
      const inscricao = this.controle().events.subscribe(() => this.detector.markForCheck());
      limpar(() => inscricao.unsubscribe());
    });
  }

  /**
   * Os erros aparecem depois que a pessoa ALTERA o campo ou tenta salvar (o formulário marca todos como alterados), não só por
   * passar por ele: com o foco automático no primeiro campo, sair dele já pintava "Campo obrigatório" de vermelho antes de
   * qualquer digitação (revisão de usabilidade, achado A3).
   */
  mostrarErros(): boolean {
    const controle = this.controle();
    return controle.invalid && (controle.dirty || controle.hasError('servidor'));
  }

  mensagens(): string[] {
    return mensagensDeErro(this.controle().errors);
  }

  /** Os ids que descrevem o campo: a dica e as mensagens de erro (quando aparecem). */
  descricaoId(): string | null {
    const ids = [this.dica() ? `${this.inputId()}-dica` : null, this.mostrarErros() ? `${this.inputId()}-erros` : null];
    return ids.filter((id) => id !== null).join(' ') || null;
  }
}
