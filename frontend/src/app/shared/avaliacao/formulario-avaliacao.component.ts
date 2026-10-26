import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Rating } from 'primeng/rating';
import { Textarea } from 'primeng/textarea';
import { CriterioAvaliacao } from '../../core/avaliacao/modelo-avaliacao';
import { RespostaFormulario, respostaVazia, rotuloEstrelas } from '../../core/avaliacao/regras-avaliacao';

/**
 * O formulário de avaliação de um item (RN42 a RN46), montado pelos critérios ATIVOS do modelo: estrelas com `p-rating` (cada
 * estrela é um rádio "N de 5 estrelas", pelo teclado com as setas), o comentário conforme a regra do critério e os critérios de
 * texto. É o mesmo na pré-visualização do editor de modelo e em "Minha avaliação". Os erros vêm com as chaves da API
 * (`respostas.<criterio>`, `respostas.<criterio>.comentario`...) e aparecem junto do controle.
 */
@Component({
  selector: 'app-formulario-avaliacao',
  imports: [FormsModule, Rating, Textarea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (criterio of ativos(); track criterio.chave) {
      @let valor = valorDe(criterio.chave);
      @let id = prefixoId() + '-' + criterio.chave;
      <div class="criterio-avaliacao">
        @if (criterio.tipo === 'ESTRELAS') {
          <div
            role="radiogroup"
            [attr.aria-labelledby]="id + '-rotulo'"
            [attr.aria-describedby]="descricao(criterio, id)"
          >
            <span class="campo-rotulo" [id]="id + '-rotulo'">
              {{ criterio.nome }}
              @if (criterio.obrigatorio) {
                <span class="obrigatorio" aria-hidden="true">*</span><span class="sr-only">(obrigatório)</span>
              }
            </span>
            <div class="linha-estrelas">
              <p-rating
                [ngModel]="valor.estrelas"
                [readonly]="somenteLeitura()"
                [name]="id"
                (ngModelChange)="mudar(criterio.chave, { estrelas: $event })"
              />
              <span class="texto-secundario" aria-hidden="true">{{ rotulo(valor.estrelas) }}</span>
            </div>
          </div>
          @if (criterio.descricao) {
            <small class="campo-dica" [id]="id + '-dica'">{{ criterio.descricao }}</small>
          }
          @for (m of errosDe('respostas.' + criterio.chave); track m) {
            <small class="campo-erros" [id]="id + '-erros'">{{ m }}</small>
          }
          @if (criterio.comentario !== 'NENHUM') {
            <label class="campo-rotulo rotulo-comentario" [for]="id + '-comentario'">
              Comentário sobre {{ criterio.nome }}
              @if (criterio.comentario === 'OBRIGATORIO') {
                <span class="obrigatorio" aria-hidden="true">*</span><span class="sr-only">(obrigatório)</span>
              } @else {
                <span class="texto-secundario">(opcional)</span>
              }
            </label>
            <textarea
              pTextarea
              rows="2"
              class="w-full"
              [id]="id + '-comentario'"
              [readonly]="somenteLeitura()"
              [attr.aria-invalid]="!!erros()['respostas.' + criterio.chave + '.comentario']"
              [attr.aria-describedby]="
                erros()['respostas.' + criterio.chave + '.comentario'] ? id + '-comentario-erros' : null
              "
              [attr.maxlength]="criterio.tamanhoMaximoTexto"
              [ngModel]="valor.comentario"
              (ngModelChange)="mudar(criterio.chave, { comentario: $event })"
            ></textarea>
            @for (m of errosDe('respostas.' + criterio.chave + '.comentario'); track m) {
              <small class="campo-erros" [id]="id + '-comentario-erros'">{{ m }}</small>
            }
          }
        } @else {
          <label class="campo-rotulo" [for]="id + '-texto'">
            {{ criterio.nome }}
            @if (criterio.obrigatorio) {
              <span class="obrigatorio" aria-hidden="true">*</span><span class="sr-only">(obrigatório)</span>
            }
          </label>
          <textarea
            pTextarea
            rows="3"
            class="w-full"
            [id]="id + '-texto'"
            [readonly]="somenteLeitura()"
            [attr.aria-invalid]="
              !!(erros()['respostas.' + criterio.chave] || erros()['respostas.' + criterio.chave + '.texto'])
            "
            [attr.aria-describedby]="descricao(criterio, id)"
            [attr.maxlength]="criterio.tamanhoMaximoTexto"
            [ngModel]="valor.texto"
            (ngModelChange)="mudar(criterio.chave, { texto: $event })"
          ></textarea>
          @if (criterio.descricao) {
            <small class="campo-dica" [id]="id + '-dica'">{{ criterio.descricao }}</small>
          }
          @for (m of errosDoTexto(criterio.chave); track m) {
            <small class="campo-erros" [id]="id + '-erros'">{{ m }}</small>
          }
        }
      </div>
    } @empty {
      <p class="texto-secundario">Nenhum critério ativo.</p>
    }
  `,
})
export class FormularioAvaliacaoComponent {
  readonly criterios = input.required<CriterioAvaliacao[]>();
  readonly valores = input<Record<string, RespostaFormulario>>({});
  readonly erros = input<Partial<Record<string, string[]>>>({});
  readonly prefixoId = input('avaliacao');
  readonly somenteLeitura = input(false);
  readonly valoresChange = output<Record<string, RespostaFormulario>>();

  errosDe(chave: string): string[] {
    return this.erros()[chave] ?? [];
  }

  errosDoTexto(chave: string): string[] {
    return [...this.errosDe(`respostas.${chave}`), ...this.errosDe(`respostas.${chave}.texto`)];
  }

  ativos(): CriterioAvaliacao[] {
    return this.criterios().filter((c) => c.ativo);
  }

  valorDe(chave: string): RespostaFormulario {
    return this.valores()[chave] ?? respostaVazia();
  }

  rotulo(estrelas: number | null): string {
    return rotuloEstrelas(estrelas);
  }

  descricao(criterio: CriterioAvaliacao, id: string): string | null {
    const ids = [
      criterio.descricao ? `${id}-dica` : null,
      this.erros()[`respostas.${criterio.chave}`] || this.erros()[`respostas.${criterio.chave}.texto`]
        ? `${id}-erros`
        : null,
    ].filter((x) => x !== null);
    return ids.join(' ') || null;
  }

  mudar(chave: string, mudanca: Partial<RespostaFormulario>): void {
    this.valoresChange.emit({ ...this.valores(), [chave]: { ...this.valorDe(chave), ...mudanca } });
  }
}
