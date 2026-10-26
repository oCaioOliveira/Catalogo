import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { TableModule } from 'primeng/table';
import { EntidadeEstrutura } from '../../../../core/administracao/estrutura-listagem';
import { Campo } from '../../../../core/metadados/modelo';
import { colunasPadrao } from '../../../../core/metadados/regras-metadados';
import { CampoFormularioComponent } from '../../../../shared/campos/campo-formulario.component';
import { BuscarOpcoes } from '../../../../shared/campos/campo-referencia.component';
import { layoutDoFormulario, montarFormulario } from '../../../../shared/formulario/montador-formulario';
import { OpcoesFiltroService } from '../../../registros/comum/opcoes-filtro.service';
import { entidadeParaPreVisualizacao } from '../modelo/regras-estrutura';

/**
 * Pré-visualização: o formulário GENÉRICO (os mesmos componentes de campo, no mesmo layout) e as colunas padrão da tabela como os
 * usuários vão ver este tipo de item agora. Nada é gravado: não há botão de salvar.
 */
@Component({
  selector: 'app-aba-previa',
  imports: [TableModule, CampoFormularioComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p class="texto-secundario">
      Como os usuários verão "{{ entidade().nome }}" agora (só os campos ativos). Pode preencher para experimentar: nada
      é gravado.
    </p>
    <h3>Formulário</h3>
    <div class="formulario previa-formulario">
      @if (previa().campos.length === 0) {
        <p>Sem campos ativos.</p>
      }
      <div class="grade-campos">
        @for (campo of layout().curtos; track campo.chave) {
          <app-campo-formulario
            prefixoId="previa"
            [campo]="campo"
            [controle]="formulario().controls[campo.chave]"
            [buscar]="campo.tipo === 'REFERENCIA' ? busca(campo) : null"
            [dica]="
              campo.dependeDe ? 'No uso real, fica desabilitado até escolher ' + nomeDe(campo.dependeDe) + '.' : null
            "
          />
        }
      </div>
      <div class="campos-longos">
        @for (campo of layout().longos; track campo.chave) {
          <app-campo-formulario prefixoId="previa" [campo]="campo" [controle]="formulario().controls[campo.chave]" />
        }
      </div>
    </div>
    <h3>Colunas da tabela (padrão)</h3>
    <p-table styleClass="tabela-registros" [value]="[{}]" size="small" [tableStyle]="{ 'min-width': '30rem' }">
      <ng-template #header>
        <tr>
          @for (coluna of colunas(); track coluna.chave) {
            <th>{{ coluna.nome }}</th>
          }
          <th>Criado em</th>
        </tr>
      </ng-template>
      <ng-template #body>
        <tr>
          @for (coluna of colunas(); track coluna.chave) {
            <td class="texto-secundario">…</td>
          }
          <td class="texto-secundario">…</td>
        </tr>
      </ng-template>
    </p-table>
    <!-- Nota da SEÇÃO (não é dica de campo: .campo-dica.bloco sobe 8 px para encostar no campo e entrava na tabela). -->
    <p class="nota-secao">
      Textos longos não viram coluna; cada usuário pode escolher outras colunas na própria tabela.
    </p>
  `,
})
export class AbaPreviaComponent {
  readonly grupo = input.required<string>();
  readonly entidade = input.required<EntidadeEstrutura>();

  private readonly opcoes = inject(OpcoesFiltroService);

  readonly previa = computed(() => entidadeParaPreVisualizacao(this.entidade()));
  readonly formulario = computed(() => montarFormulario(this.previa()));
  readonly layout = computed(() => layoutDoFormulario(this.previa().campos));
  readonly colunas = computed(() => {
    const previa = this.previa();
    return colunasPadrao(previa).map((chave) => previa.campos.find((c) => c.chave === chave)!);
  });

  private readonly buscas = new Map<string, BuscarOpcoes>();

  busca(campo: Campo): BuscarOpcoes {
    let busca = this.buscas.get(campo.chave);
    if (!busca) {
      busca = (texto) => this.opcoes.buscar(this.grupo(), campo.entidadeReferenciada ?? '', texto);
      this.buscas.set(campo.chave, busca);
    }
    return busca;
  }

  nomeDe(chave: string): string {
    return this.previa().campos.find((c) => c.chave === chave)?.nome ?? chave;
  }
}
