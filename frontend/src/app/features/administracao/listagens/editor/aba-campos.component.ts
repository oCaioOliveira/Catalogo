import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { Button } from 'primeng/button';
import { Drawer } from 'primeng/drawer';
import { Tag } from 'primeng/tag';
import { Tooltip } from 'primeng/tooltip';
import {
  CampoEstrutura,
  EntidadeEstrutura,
  EstruturaListagem,
} from '../../../../core/administracao/estrutura-listagem';
import { MetadadosAdminService } from '../../../../core/administracao/metadados-admin.service';
import { AlteracaoEstruturaService, Desfecho } from '../comum/alteracao-estrutura.service';
import { moverItem, usosDoCampo } from '../modelo/regras-estrutura';
import { rotuloDoTipo } from '../modelo/tipos-campo';
import { PainelCampoComponent } from './painel-campo.component';

/**
 * Aba Campos: todos os campos (ativos e inativos) na ordem do formulário, com tipo, obrigatoriedade, situação e onde cada um é
 * usado. Reordenar por arrastar e soltar (ou pelos botões), criar e editar no painel lateral, desativar e reativar (RN69: os valores
 * ficam guardados). Tudo passa pela simulação.
 */
@Component({
  selector: 'app-aba-campos',
  imports: [CdkDropList, CdkDrag, CdkDragHandle, Button, Drawer, Tag, Tooltip, PainelCampoComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cabecalho-secao">
      <p class="texto-secundario">
        A ordem desta lista é a ordem do formulário e das colunas. Arraste pela alça (<i
          class="pi pi-bars"
          aria-hidden="true"
        ></i
        >) ou use os botões de mover.
      </p>
      <p-button label="Novo campo" icon="pi pi-plus" (onClick)="abrir(null)" />
    </div>
    @if (entidade().campos.length === 0) {
      <p class="texto-secundario">Nenhum campo ainda. Comece pelo campo que identifica o item, como "Nome".</p>
    } @else {
      <table class="tabela-admin" aria-label="Campos do tipo de item">
        <thead>
          <tr>
            <th class="coluna-alca"><span class="sr-only">Arrastar</span></th>
            <th>Nome</th>
            <th>Chave</th>
            <th>Tipo</th>
            <th>Obrigatório</th>
            <th>Situação</th>
            <th>Usado em</th>
            <th class="coluna-acoes"><span class="sr-only">Ações</span></th>
          </tr>
        </thead>
        <tbody cdkDropList [cdkDropListDisabled]="ocupado()" (cdkDropListDropped)="aoSoltar($event)">
          @for (campo of entidade().campos; track campo.chave; let i = $index, primeiro = $first, ultimo = $last) {
            <tr cdkDrag class="linha-arrastavel" [class.linha-inativa]="!campo.ativo">
              <td class="coluna-alca">
                <span cdkDragHandle class="alca-arrastar" [attr.aria-label]="'Arrastar ' + campo.nome">
                  <i class="pi pi-bars" aria-hidden="true"></i>
                </span>
              </td>
              <td>{{ campo.nome }}</td>
              <td>
                <code>{{ campo.chave }}</code>
              </td>
              <td>
                {{ rotuloDoTipo(campo.tipo) }}
                @if (campo.tipo === 'TEXTO' && campo.tamanhoMaximo) {
                  <span class="texto-secundario">({{ campo.tamanhoMaximo }})</span>
                } @else if (campo.tipo === 'DECIMAL') {
                  <span class="texto-secundario">({{ campo.precisao }},{{ campo.escala }})</span>
                } @else if (campo.tipo === 'REFERENCIA') {
                  <span class="texto-secundario">→ {{ nomeDe(campo.entidadeReferenciada) }}</span>
                }
              </td>
              <td>{{ campo.obrigatorio ? 'Sim' : 'Não' }}</td>
              <td>
                <p-tag [value]="campo.ativo ? 'Ativo' : 'Inativo'" [severity]="campo.ativo ? 'success' : 'secondary'" />
              </td>
              <td>{{ usos(campo).join('; ') || '—' }}</td>
              <td class="coluna-acoes">
                <p-button
                  icon="pi pi-arrow-up"
                  severity="secondary"
                  [text]="true"
                  [rounded]="true"
                  size="small"
                  [disabled]="primeiro || ocupado()"
                  [ariaLabel]="'Mover ' + campo.nome + ' para cima'"
                  pTooltip="Mover para cima"
                  (onClick)="reordenar(i, i - 1)"
                />
                <p-button
                  icon="pi pi-arrow-down"
                  severity="secondary"
                  [text]="true"
                  [rounded]="true"
                  size="small"
                  [disabled]="ultimo || ocupado()"
                  [ariaLabel]="'Mover ' + campo.nome + ' para baixo'"
                  pTooltip="Mover para baixo"
                  (onClick)="reordenar(i, i + 1)"
                />
                <p-button
                  icon="pi pi-pencil"
                  [text]="true"
                  [rounded]="true"
                  size="small"
                  [ariaLabel]="'Editar o campo ' + campo.nome"
                  pTooltip="Editar"
                  (onClick)="abrir(campo)"
                />
                <p-button
                  [icon]="campo.ativo ? 'pi pi-ban' : 'pi pi-check-circle'"
                  [severity]="campo.ativo ? 'danger' : 'success'"
                  [text]="true"
                  [rounded]="true"
                  size="small"
                  [ariaLabel]="(campo.ativo ? 'Desativar o campo ' : 'Reativar o campo ') + campo.nome"
                  [pTooltip]="campo.ativo ? 'Desativar' : 'Reativar'"
                  (onClick)="alternarAtivacao(campo)"
                />
              </td>
            </tr>
          }
        </tbody>
      </table>
    }

    <p-drawer
      position="right"
      styleClass="painel-lateral-campo"
      [header]="emEdicao() ? 'Campo: ' + emEdicao()!.nome : 'Novo campo'"
      [modal]="true"
      [visible]="painelAberto()"
      (visibleChange)="$event ? null : painelAberto.set(false)"
    >
      @if (painelAberto()) {
        <app-painel-campo
          [estrutura]="estrutura()"
          [entidade]="entidade()"
          [campo]="emEdicao()"
          (alterada)="alterada.emit($event)"
          (fechar)="painelAberto.set(false)"
        />
      }
    </p-drawer>
  `,
})
export class AbaCamposComponent {
  readonly estrutura = input.required<EstruturaListagem>();
  readonly entidade = input.required<EntidadeEstrutura>();
  readonly alterada = output<EstruturaListagem | null>();

  private readonly admin = inject(MetadadosAdminService);
  private readonly alteracao = inject(AlteracaoEstruturaService);

  readonly painelAberto = signal(false);
  readonly emEdicao = signal<CampoEstrutura | null>(null);
  readonly ocupado = signal(false);
  readonly rotuloDoTipo = rotuloDoTipo;

  abrir(campo: CampoEstrutura | null): void {
    this.emEdicao.set(campo);
    this.painelAberto.set(true);
  }

  usos(campo: CampoEstrutura): string[] {
    return usosDoCampo(this.entidade(), campo, this.estrutura());
  }

  nomeDe(chave: string | null): string {
    return this.estrutura().entidades.find((e) => e.chave === chave)?.nome ?? chave ?? '';
  }

  aoSoltar(evento: CdkDragDrop<unknown>): void {
    if (evento.previousIndex !== evento.currentIndex) {
      void this.reordenar(evento.previousIndex, evento.currentIndex);
    }
  }

  /** PUT .../campos/ordem com TODAS as chaves (ativas e inativas), na ordem nova. Pode recalcular chaves únicas (RN09). */
  async reordenar(de: number, para: number): Promise<void> {
    const entidade = this.entidade();
    if (para < 0 || para >= entidade.campos.length) {
      return;
    }
    const ordem = moverItem(entidade.campos, de, para).map((c) => c.chave);
    this.ocupado.set(true);
    const desfecho = await this.alteracao.executar({
      grupo: this.estrutura().chave,
      entidade: entidade.chave,
      titulo: 'Reordenar os campos',
      chamar: (simular) =>
        this.admin.reordenarCampos(this.estrutura().chave, entidade.chave, entidade.versao, ordem, simular),
    });
    this.ocupado.set(false);
    this.tratar(desfecho);
  }

  async alternarAtivacao(campo: CampoEstrutura): Promise<void> {
    const entidade = this.entidade();
    const desfecho = await this.alteracao.executar({
      grupo: this.estrutura().chave,
      entidade: entidade.chave,
      titulo: `${campo.ativo ? 'Desativar' : 'Reativar'} o campo "${campo.nome}"`,
      chamar: (simular) =>
        this.admin.ativarCampo(
          this.estrutura().chave,
          entidade.chave,
          campo.chave,
          entidade.versao,
          !campo.ativo,
          simular,
        ),
    });
    this.tratar(desfecho);
  }

  private tratar(desfecho: Desfecho): void {
    if (desfecho.status === 'gravado') {
      this.alterada.emit(desfecho.estrutura);
    } else if (desfecho.status === 'conflito') {
      this.alterada.emit(null);
    }
  }
}
