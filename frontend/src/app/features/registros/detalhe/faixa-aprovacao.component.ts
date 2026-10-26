import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { Message } from 'primeng/message';
import { Tag } from 'primeng/tag';
import { Textarea } from 'primeng/textarea';
import { AprovacaoService } from '../../../core/aprovacao/aprovacao.service';
import { PendentesService } from '../../../core/aprovacao/pendentes.service';
import {
  JUSTIFICATIVA,
  SITUACOES,
  faixaDeSituacao,
  validarJustificativa,
} from '../../../core/aprovacao/regras-aprovacao';
import { AuthService } from '../../../core/auth/auth.service';
import { tratarNoComponente } from '../../../core/erros/contexto-erros';
import { mensagemDoErro } from '../../../core/erros/mensagens-erro';
import { lerProblema } from '../../../core/erros/problema';
import { Entidade } from '../../../core/metadados/modelo';
import { Registro } from '../../../core/registros/modelo-registro';
import { formatarDataHoraSistema } from '../../../shared/formatacao/formatar-valor';

/**
 * A faixa do topo do detalhe com a situação de aprovação (RN34 a RN37), só onde a aprovação está ativa para a entidade e a
 * equipe do item: a última decisão (quem, quando e a justificativa completa) e, para o GESTOR da equipe e o ADMINISTRADOR num item
 * pendente, Aprovar e Rejeitar. Cada decisão abre um diálogo com a justificativa obrigatória (10 a 1000 caracteres, com contador).
 */
@Component({
  selector: 'app-faixa-aprovacao',
  imports: [FormsModule, Button, Dialog, Message, Tag, Textarea],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (faixa(); as f) {
      <section
        class="faixa-aprovacao"
        [class.faixa-pendente]="f.situacao === 'PENDENTE'"
        [class.faixa-aprovado]="f.situacao === 'APROVADO'"
        [class.faixa-rejeitado]="f.situacao === 'REJEITADO'"
        aria-labelledby="faixa-aprovacao-titulo"
      >
        <div class="faixa-conteudo">
          <p-tag [value]="situacoes[f.situacao].rotulo" [severity]="situacoes[f.situacao].severidade" />
          <h2 id="faixa-aprovacao-titulo" class="faixa-titulo">{{ f.titulo }}</h2>
          @if (registro().ultimaDecisao; as d) {
            <div class="faixa-decisao">
              <p class="texto-secundario">
                {{ d.decisao === 'APROVAR' ? 'Aprovado' : 'Rejeitado' }} por {{ d.decididoPor?.nome ?? '—' }} em
                {{ formatarDataHoraSistema(d.decididoEm) }}
              </p>
              @if (d.justificativa) {
                <blockquote class="faixa-justificativa">{{ d.justificativa }}</blockquote>
              }
            </div>
          }
        </div>
        @if (f.podeDecidir) {
          <div class="acoes-pagina">
            <p-button label="Aprovar" icon="pi pi-check" severity="success" (onClick)="abrir('APROVAR')" />
            <p-button
              label="Rejeitar"
              icon="pi pi-times"
              severity="danger"
              [outlined]="true"
              (onClick)="abrir('REJEITAR')"
            />
          </div>
        }
      </section>
    }

    <p-dialog
      [header]="decisao() === 'APROVAR' ? 'Aprovar o item' : 'Rejeitar o item'"
      [modal]="true"
      [visible]="!!decisao()"
      [draggable]="false"
      [style]="{ width: 'min(36rem, 95vw)' }"
      styleClass="dialogo-decisao"
      (visibleChange)="$event ? null : fechar()"
    >
      <form (submit)="$event.preventDefault(); decidir()" novalidate>
        @if (erroGeral()) {
          <p-message severity="error" styleClass="mb-3">{{ erroGeral() }}</p-message>
        }
        <p>
          {{
            decisao() === 'APROVAR'
              ? 'O item passa a valer para todos. Explique o que foi conferido.'
              : 'O autor recebe o item de volta para corrigir e reenviar. Explique o que precisa mudar.'
          }}
        </p>
        <div class="campo">
          <label for="justificativa" class="campo-rotulo">
            Justificativa <span class="obrigatorio" aria-hidden="true">*</span
            ><span class="sr-only">(obrigatório)</span>
          </label>
          <textarea
            pTextarea
            id="justificativa"
            name="justificativa"
            rows="4"
            class="w-full"
            [attr.maxlength]="limites.maximo"
            [attr.aria-invalid]="!!erroJustificativa()"
            [attr.aria-describedby]="
              erroJustificativa() ? 'justificativa-contador justificativa-erro' : 'justificativa-contador'
            "
            [ngModel]="justificativa()"
            (ngModelChange)="justificativa.set($event); erroJustificativa.set(null)"
          ></textarea>
          <small class="contador" id="justificativa-contador">
            {{ tamanho() }}/{{ limites.maximo }} (mínimo {{ limites.minimo }})
          </small>
          @if (erroJustificativa()) {
            <small class="campo-erros" id="justificativa-erro">{{ erroJustificativa() }}</small>
          }
        </div>
        <div class="acoes-formulario">
          <p-button type="button" label="Cancelar" severity="secondary" [text]="true" (onClick)="fechar()" />
          <p-button
            type="submit"
            [label]="decisao() === 'APROVAR' ? 'Aprovar' : 'Rejeitar'"
            [icon]="decisao() === 'APROVAR' ? 'pi pi-check' : 'pi pi-times'"
            [severity]="decisao() === 'APROVAR' ? 'success' : 'danger'"
            [loading]="decidindo()"
          />
        </div>
      </form>
    </p-dialog>
  `,
})
export class FaixaAprovacaoComponent {
  readonly grupo = input.required<string>();
  readonly entidade = input.required<Entidade>();
  readonly registro = input.required<Registro>();
  /** A decisão foi gravada: o registro atualizado (situação, versão e última decisão novas). */
  readonly decidido = output<Registro>();

  private readonly auth = inject(AuthService);
  private readonly aprovacao = inject(AprovacaoService);
  private readonly pendentes = inject(PendentesService);

  readonly faixa = computed(() => faixaDeSituacao(this.auth.usuario(), this.entidade(), this.registro()));
  readonly situacoes = SITUACOES;
  readonly limites = JUSTIFICATIVA;
  readonly formatarDataHoraSistema = formatarDataHoraSistema;

  readonly decisao = signal<'APROVAR' | 'REJEITAR' | null>(null);
  readonly justificativa = signal('');
  readonly erroJustificativa = signal<string | null>(null);
  readonly erroGeral = signal<string | null>(null);
  readonly decidindo = signal(false);
  readonly tamanho = computed(() => this.justificativa().trim().length);

  abrir(decisao: 'APROVAR' | 'REJEITAR'): void {
    this.justificativa.set('');
    this.erroJustificativa.set(null);
    this.erroGeral.set(null);
    this.decisao.set(decisao);
  }

  fechar(): void {
    this.decisao.set(null);
  }

  async decidir(): Promise<void> {
    const decisao = this.decisao();
    if (!decisao) {
      return;
    }
    const erro = validarJustificativa(this.justificativa());
    if (erro) {
      this.erroJustificativa.set(erro);
      return;
    }
    this.decidindo.set(true);
    try {
      const atualizado = await this.aprovacao.decidir(
        this.grupo(),
        this.entidade().chave,
        this.registro().id,
        decisao,
        this.justificativa().trim(),
        tratarNoComponente(400, 409),
      );
      this.decisao.set(null);
      void this.pendentes.atualizar();
      this.decidido.emit(atualizado);
    } catch (e) {
      const problema = lerProblema(e);
      const doCampo = problema.erros['justificativa'];
      if (doCampo) {
        this.erroJustificativa.set(doCampo.join(' '));
      } else {
        this.erroGeral.set(problema.detalhe ?? mensagemDoErro(problema.codigo, problema.status));
      }
    } finally {
      this.decidindo.set(false);
    }
  }
}
