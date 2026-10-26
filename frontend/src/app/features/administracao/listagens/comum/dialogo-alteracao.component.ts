import { ChangeDetectionStrategy, Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormControl } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { Message } from 'primeng/message';
import { CampoFormularioComponent } from '../../../../shared/campos/campo-formulario.component';
import { BuscarOpcoes } from '../../../../shared/campos/campo-referencia.component';
import { obrigatorio, validadoresDoCampo } from '../../../../shared/formulario/validadores';
import { ValorCampo, valorParaApi } from '../../../../shared/formulario/valores';
import { OpcoesFiltroService } from '../../../registros/comum/opcoes-filtro.service';
import { orientacao, resumoDoImpacto } from '../modelo/simulacao';
import { AlteracaoEstruturaService } from './alteracao-estrutura.service';

/**
 * O diálogo das alterações de estrutura (AlteracaoEstruturaService), um só para todas as telas da administração de listagens:
 *   - confirmar: o resumo do efeito ("120 itens receberão o valor padrão") e Confirmar/Cancelar;
 *   - bloqueado: o RELATÓRIO de impacto (quantidade, exemplos com link para cada item, grupos de duplicados, usos) e a orientação
 *     do que fazer. Não há botão de confirmar;
 *   - valor-padrao: o MESMO componente de campo do tipo (com a validação dele) para o valor que os itens vazios vão receber;
 *   - conflito: outra pessoa alterou a estrutura; ao fechar, a tela recarrega.
 */
@Component({
  selector: 'app-dialogo-alteracao',
  imports: [RouterLink, Button, Dialog, Message, CampoFormularioComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (servico.dialogo(); as d) {
      <p-dialog
        [header]="cabecalho()"
        [modal]="true"
        [visible]="true"
        [closable]="true"
        [draggable]="false"
        [style]="{ width: 'min(44rem, 95vw)' }"
        styleClass="dialogo-alteracao"
        (visibleChange)="$event ? null : fechar()"
      >
        @switch (d.modo) {
          @case ('confirmar') {
            <p>Esta alteração é permitida e tem este efeito nos itens cadastrados:</p>
            <ul class="lista-impactos">
              @for (impacto of d.impactos; track $index) {
                <li>{{ resumo(impacto) }}</li>
              }
            </ul>
            <p>Deseja continuar?</p>
          }
          @case ('bloqueado') {
            <p-message severity="error" styleClass="mb-3">
              Esta alteração não pode ser feita: deixaria itens cadastrados inválidos. Nada foi alterado.
            </p-message>
            @for (impacto of d.impactos; track $index) {
              <section class="impacto" [class.impacto-informativo]="!impacto.bloqueia">
                <h3>{{ impacto.descricao }}</h3>
                @if (impacto.quantidade > 0) {
                  <p class="quantidade-impacto">
                    {{ impacto.quantidade === 1 ? '1 item afetado' : impacto.quantidade + ' itens afetados' }}
                    @if (impacto.exemplos.length < impacto.quantidade && impacto.exemplos.length > 0) {
                      <span class="texto-secundario">(exemplos abaixo)</span>
                    }
                  </p>
                }
                @if (impacto.grupos && impacto.grupos.length > 0) {
                  <ul class="grupos-duplicados">
                    @for (grupoItens of impacto.grupos; track $index) {
                      <li>
                        Mesmos valores:
                        @for (item of grupoItens; track item.id; let ultimo = $last) {
                          <a [routerLink]="linkDoItem(item.id)" target="_blank">{{ item.rotulo ?? '(sem rótulo)' }}</a
                          >{{ ultimo ? '' : ', ' }}
                        }
                      </li>
                    }
                  </ul>
                } @else if (impacto.exemplos.length > 0) {
                  <ul class="exemplos-impacto">
                    @for (item of impacto.exemplos; track item.id) {
                      <li>
                        <a [routerLink]="linkDoItem(item.id)" target="_blank">{{ item.rotulo ?? '(sem rótulo)' }}</a>
                      </li>
                    }
                  </ul>
                }
                @if (impacto.usos && impacto.usos.length > 0) {
                  <p>Usado em: {{ impacto.usos.join('; ') }}</p>
                }
                @if (impacto.bloqueia) {
                  <p class="orientacao-impacto"><strong>O que fazer:</strong> {{ orientacao(impacto.tipo) }}</p>
                }
              </section>
            }
          }
          @case ('valor-padrao') {
            <p>
              {{ d.impacto.quantidade === 1 ? '1 item está' : d.impacto.quantidade + ' itens estão' }} sem valor em
              <strong>{{ d.campo.nome }}</strong
              >, que vai ser obrigatório. Informe o valor que
              {{ d.impacto.quantidade === 1 ? 'ele vai receber' : 'eles vão receber' }} (os itens já preenchidos não
              mudam).
            </p>
            @if (controle(); as controle) {
              <app-campo-formulario
                prefixoId="valor-padrao"
                [campo]="campoValorPadrao()!"
                [controle]="controle"
                [buscar]="buscaValorPadrao()"
              />
            }
          }
          @case ('conflito') {
            <p>
              Outra pessoa alterou a estrutura desta área de gestão depois que você abriu a tela. Nada do que você fez
              agora foi gravado.
            </p>
            <p>A tela vai ser recarregada com a versão atual: confira e refaça a alteração.</p>
          }
        }
        <ng-template #footer>
          @switch (d.modo) {
            @case ('confirmar') {
              <p-button label="Cancelar" severity="secondary" [text]="true" (onClick)="servico.responder(false)" />
              <p-button label="Confirmar" icon="pi pi-check" (onClick)="servico.responder(true)" />
            }
            @case ('valor-padrao') {
              <p-button label="Cancelar" severity="secondary" [text]="true" (onClick)="servico.responder(undefined)" />
              <p-button label="Continuar" icon="pi pi-arrow-right" (onClick)="confirmarValorPadrao()" />
            }
            @case ('conflito') {
              <p-button label="Recarregar" icon="pi pi-refresh" (onClick)="servico.responder(true)" />
            }
            @default {
              <p-button label="Entendi" (onClick)="servico.responder(false)" />
            }
          }
        </ng-template>
      </p-dialog>
    }
  `,
})
export class DialogoAlteracaoComponent {
  readonly servico = inject(AlteracaoEstruturaService);
  private readonly opcoes = inject(OpcoesFiltroService);

  readonly controle = signal<FormControl<ValorCampo> | null>(null);
  readonly campoValorPadrao = computed(() => {
    const d = this.servico.dialogo();
    return d?.modo === 'valor-padrao' ? { ...d.campo, obrigatorio: true } : null;
  });
  readonly buscaValorPadrao = computed<BuscarOpcoes | null>(() => {
    const d = this.servico.dialogo();
    if (d?.modo !== 'valor-padrao' || d.campo.tipo !== 'REFERENCIA' || !d.campo.entidadeReferenciada) {
      return null;
    }
    const [grupo, entidade] = [d.grupo, d.campo.entidadeReferenciada];
    return (busca) => this.opcoes.buscar(grupo, entidade, busca);
  });
  readonly orientacao = orientacao;
  readonly resumo = resumoDoImpacto;

  constructor() {
    // Um controle novo a cada pedido de valor padrão, com os validadores do tipo (e obrigatório).
    effect(() => {
      const campo = this.campoValorPadrao();
      untracked(() =>
        this.controle.set(
          campo
            ? new FormControl<ValorCampo>(campo.tipo === 'BOOLEANO' ? false : null, {
                validators: [...validadoresDoCampo(campo), obrigatorio()],
              })
            : null,
        ),
      );
    });
  }

  cabecalho(): string {
    const d = this.servico.dialogo();
    switch (d?.modo) {
      case 'confirmar':
        return `Confirmar: ${d.titulo}`;
      case 'bloqueado':
        return `Não é possível: ${d.titulo}`;
      case 'valor-padrao':
        return `Valor padrão para "${d.campo.nome}"`;
      case 'conflito':
        return 'Outra pessoa alterou a estrutura';
      default:
        return '';
    }
  }

  linkDoItem(id: string): unknown[] {
    const d = this.servico.dialogo();
    return d && (d.modo === 'bloqueado' || d.modo === 'confirmar') && d.entidade
      ? ['/', d.grupo, d.entidade, id]
      : ['/'];
  }

  confirmarValorPadrao(): void {
    const controle = this.controle();
    const campo = this.campoValorPadrao();
    if (!controle || !campo) {
      return;
    }
    controle.markAsTouched();
    if (controle.invalid) {
      return;
    }
    this.servico.responder(valorParaApi(campo, controle.value));
  }

  /** Fechar pelo "x": no conflito, recarrega do mesmo jeito; nos outros, é cancelar. */
  fechar(): void {
    const d = this.servico.dialogo();
    this.servico.responder(d?.modo === 'conflito' ? true : d?.modo === 'valor-padrao' ? undefined : false);
  }
}
