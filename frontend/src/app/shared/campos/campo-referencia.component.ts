import { ChangeDetectionStrategy, Component, effect, input, output, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { AutoComplete, AutoCompleteCompleteEvent, AutoCompleteSelectEvent } from 'primeng/autocomplete';
import { OpcaoReferencia } from '../../core/metadados/modelo';
import { DescritoPorDirective } from '../acessibilidade/descrito-por.directive';
import { ehOpcaoCriarNovo, opcaoCriarNovo } from '../formulario/criar-relacionado';
import { ValorCampo } from '../formulario/valores';

/** Como buscar as opções: recebe o texto digitado e devolve a primeira página de opções do servidor. */
export type BuscarOpcoes = (busca: string) => Promise<OpcaoReferencia[]>;

/**
 * REFERENCIA: select com busca NO SERVIDOR. Cada tecla (com uma pausa) chama `buscar`, que pede ao back-end as
 * opções que contêm o texto. Mostra o rótulo; o valor do controle é { id, rotulo } (só o id vai para a API).
 * Só aceita uma opção da lista (forceSelection).
 *
 * Com `criarNovo` (o nome do tipo referenciado), a lista termina com "+ Criar RTC" ou "+ Criar RTC 'texto'"
 *: escolher essa opção não muda o valor do campo e emite `criar` com o texto digitado; quem usa abre o diálogo e,
 * ao salvar, põe o item novo no campo. Os filtros das listagens não passam `criarNovo`.
 */
@Component({
  selector: 'app-campo-referencia',
  imports: [ReactiveFormsModule, AutoComplete, DescritoPorDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-autocomplete
      optionLabel="rotulo"
      dataKey="id"
      appendTo="body"
      emptyMessage="Nenhuma opção encontrada"
      dropdownAriaLabel="Mostrar opções"
      [fluid]="true"
      [dropdown]="true"
      [forceSelection]="true"
      [showClear]="true"
      [delay]="300"
      [placeholder]="placeholder()"
      [suggestions]="sugestoes()"
      [inputId]="inputId()"
      [formControl]="controle()"
      [appDescritoPor]="descricaoId()"
      [invalido]="invalido()"
      [invalid]="invalido()"
      (completeMethod)="pesquisar($event)"
      (onSelect)="aoEscolher($event)"
    >
      <ng-template #item let-opcao>
        <span [class.opcao-criar-novo]="ehCriar(opcao)">{{ opcao.rotulo }}</span>
      </ng-template>
    </p-autocomplete>
  `,
})
export class CampoReferenciaComponent {
  readonly controle = input.required<FormControl<ValorCampo>>();
  readonly inputId = input.required<string>();
  readonly buscar = input.required<BuscarOpcoes>();
  readonly descricaoId = input<string | null>(null);
  readonly invalido = input(false);
  readonly placeholder = input('Digite para buscar');
  /** O nome do tipo referenciado, quando o usuário pode criar um item novo dele (null = sem a opção). */
  readonly criarNovo = input<string | null>(null);
  /** O texto digitado, ao escolher "+ Criar <tipo>". */
  readonly criar = output<string>();

  readonly sugestoes = signal<OpcaoReferencia[]>([]);
  private ultimaBusca = '';
  /** O valor antes de escolher "+ Criar <tipo>": volta para o campo (a opção especial não é um item). */
  private valorAnterior: ValorCampo = null;

  readonly ehCriar = ehOpcaoCriarNovo;

  constructor() {
    effect((limpar) => {
      const controle = this.controle();
      this.valorAnterior = controle.value;
      const inscricao = controle.valueChanges.subscribe((valor) => {
        if (!ehOpcaoCriarNovo(valor)) {
          this.valorAnterior = valor;
        }
      });
      limpar(() => inscricao.unsubscribe());
    });
  }

  async pesquisar(evento: AutoCompleteCompleteEvent): Promise<void> {
    this.ultimaBusca = evento.query ?? '';
    let opcoes: OpcaoReferencia[];
    try {
      opcoes = await this.buscar()(this.ultimaBusca);
    } catch {
      // O erro já virou toast no interceptor; a lista fica só com a opção de criar (se houver).
      opcoes = [];
    }
    const tipo = this.criarNovo();
    this.sugestoes.set(tipo ? [...opcoes, opcaoCriarNovo(this.ultimaBusca, tipo)] : opcoes);
  }

  aoEscolher(evento: AutoCompleteSelectEvent): void {
    if (!ehOpcaoCriarNovo(evento.value)) {
      return;
    }
    this.controle().setValue(this.valorAnterior);
    this.criar.emit(this.ultimaBusca.trim());
  }
}
