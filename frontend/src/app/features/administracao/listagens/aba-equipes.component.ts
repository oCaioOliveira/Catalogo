import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Checkbox } from 'primeng/checkbox';
import { InputText } from 'primeng/inputtext';
import { Message } from 'primeng/message';
import { Skeleton } from 'primeng/skeleton';
import { Tag } from 'primeng/tag';
import { EquipesDaListagem } from '../../../core/administracao/estrutura-listagem';
import { MetadadosAdminService } from '../../../core/administracao/metadados-admin.service';
import { AlteracaoEstruturaService } from './comum/alteracao-estrutura.service';
import {
  alterouEquipes,
  equipesVisiveis,
  habilitadas,
  listaParaGravar,
  marcarVisiveis,
  ordenarEquipes,
  quantasAlteracoes,
  saindoComItens,
  textoAlteracoes,
  textoContador,
} from './modelo/equipes-listagem';

/**
 * Aba "Equipes" do detalhe da listagem (RN90 a RN93): TODAS as equipes (as ativas primeiro; as inativas identificadas), com a marca
 * das habilitadas.
 * Só os membros das equipes habilitadas veem a listagem; sem nenhuma, só o administrador. Salvar passa pela simulação, como toda
 * alteração de estrutura (ADR 0044): tirar uma equipe com itens mostra quantos ficam ocultos e que voltam ao habilitá-la de novo.
 * Lista longa: o cabeçalho da lista fica FIXO (contador, busca, "só habilitadas", ações rápidas, Descartar e Salvar), e os botões só
 * habilitam com alterações. Sair com alterações pendentes avisa antes (o detalhe pergunta `temAlteracoesPendentes`).
 */
@Component({
  selector: 'app-aba-equipes',
  imports: [FormsModule, RouterLink, Button, Checkbox, InputText, Message, Skeleton, Tag],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (erro()) {
      <p-message severity="error">Não foi possível carregar as equipes.</p-message>
    } @else if (dados(); as d) {
      <p class="texto-secundario">
        Só os membros das equipes marcadas veem esta área de gestão e criam itens nela. Uma equipe nova não é habilitada
        sozinha. Tirar uma equipe <strong>não apaga</strong> os itens dela: eles ficam ocultos para os membros (só
        administradores os veem) e voltam ao habilitá-la de novo.
      </p>
      @if (d.equipes.length === 0) {
        <p-message severity="info">
          Nenhuma equipe cadastrada. Crie uma em
          <a routerLink="/configuracoes/equipes/novo">Administração → Equipes</a>.
        </p-message>
      } @else {
        <!-- Cabeçalho FIXO da lista (guia de interface → "Salvar sempre visível"): contador, busca e as ações de salvar. -->
        <div class="barra-lista-fixa" role="region" aria-label="Equipes: busca e ações">
          <div class="barra-lista-linha">
            <div class="barra-lista-titulos">
              <h2 class="barra-lista-titulo">Equipes habilitadas</h2>
              <span class="barra-lista-contador">{{ contador() }}</span>
            </div>
            <div class="barra-lista-salvar">
              <span class="indicador-pendentes" role="status">
                @if (quantas() > 0) {
                  <i class="pi pi-circle-fill" aria-hidden="true"></i> {{ textoPendentes() }}
                }
              </span>
              <p-button
                label="Descartar"
                severity="secondary"
                [outlined]="true"
                [disabled]="!alterou() || salvando()"
                (onClick)="desfazer()"
              />
              <p-button
                label="Salvar alterações"
                icon="pi pi-check"
                [disabled]="!alterou()"
                [loading]="salvando()"
                (onClick)="salvar()"
              />
            </div>
          </div>
          <div class="barra-lista-linha">
            <div class="barra-lista-busca">
              <label for="busca-equipes" class="sr-only">Buscar equipe pelo nome</label>
              <input
                pInputText
                id="busca-equipes"
                type="search"
                placeholder="Buscar equipe"
                autocomplete="off"
                [ngModel]="busca()"
                (ngModelChange)="busca.set($event)"
              />
              <p-checkbox
                [binary]="true"
                inputId="so-habilitadas"
                [ngModel]="soHabilitadas()"
                (ngModelChange)="soHabilitadas.set($event)"
              />
              <label for="so-habilitadas">Mostrar só habilitadas</label>
            </div>
            <div class="barra-lista-rapidas">
              <p-button
                label="Selecionar todas as visíveis"
                severity="secondary"
                [text]="true"
                size="small"
                [disabled]="salvando() || visiveis().length === 0"
                (onClick)="marcarTodas(true)"
              />
              <p-button
                label="Limpar seleção"
                severity="secondary"
                [text]="true"
                size="small"
                [disabled]="salvando() || visiveis().length === 0"
                (onClick)="marcarTodas(false)"
              />
            </div>
          </div>
        </div>
        @if (saindo().length > 0) {
          <p-message severity="warn" styleClass="mb-3">
            @for (equipe of saindo(); track equipe.id) {
              <div>
                {{ equipe.quantidadeItens === 1 ? '1 item' : equipe.quantidadeItens + ' itens' }} da equipe
                {{ equipe.nome }} {{ equipe.quantidadeItens === 1 ? 'ficará oculto' : 'ficarão ocultos' }} para os
                membros dela.
              </div>
            }
          </p-message>
        }
        <table class="tabela-admin" aria-label="Equipes da área de gestão">
          <thead>
            <tr>
              <th class="coluna-alca"><span class="sr-only">Habilitada</span></th>
              <th>Equipe</th>
              <th>Situação</th>
              <th class="coluna-numero">Itens nesta área</th>
            </tr>
          </thead>
          <tbody>
            @for (equipe of visiveis(); track equipe.id; let i = $index) {
              @if (!equipe.ativa && (i === 0 || visiveis()[i - 1].ativa)) {
                <tr class="linha-grupo">
                  <th colspan="4" scope="colgroup">Equipes inativas</th>
                </tr>
              }
              <tr>
                <td class="coluna-alca">
                  <p-checkbox
                    [binary]="true"
                    [inputId]="'equipe-' + equipe.id"
                    [ngModel]="escolhidas().has(equipe.id)"
                    (ngModelChange)="marcar(equipe.id, $event)"
                    [disabled]="salvando()"
                  />
                </td>
                <td>
                  <label [for]="'equipe-' + equipe.id">{{ equipe.nome }}</label>
                </td>
                <td>
                  <p-tag
                    [value]="equipe.ativa ? 'Ativa' : 'Inativa'"
                    [severity]="equipe.ativa ? 'success' : 'secondary'"
                  />
                </td>
                <td class="coluna-numero">{{ equipe.quantidadeItens }}</td>
              </tr>
            } @empty {
              <tr>
                <td colspan="4" class="texto-secundario">Nenhuma equipe encontrada com esses filtros.</td>
              </tr>
            }
          </tbody>
        </table>
      }
    } @else {
      <div aria-busy="true" aria-label="Carregando"><p-skeleton height="10rem" /></div>
    }
  `,
})
export class AbaEquipesComponent {
  readonly grupo = input.required<string>();
  /** Gravou: o detalhe recarrega a estrutura (versão e quantidade de equipes, para a faixa de aviso). */
  readonly alterada = output<void>();

  private readonly admin = inject(MetadadosAdminService);
  private readonly alteracao = inject(AlteracaoEstruturaService);

  readonly dados = signal<EquipesDaListagem | null>(null);
  readonly erro = signal(false);
  readonly escolhidas = signal<ReadonlySet<string>>(new Set());
  readonly salvando = signal(false);

  readonly busca = signal('');
  readonly soHabilitadas = signal(false);

  readonly alterou = computed(() => alterouEquipes(this.dados()?.equipes ?? [], this.escolhidas()));
  readonly quantas = computed(() => quantasAlteracoes(this.dados()?.equipes ?? [], this.escolhidas()));
  readonly textoPendentes = computed(() => textoAlteracoes(this.quantas()));
  readonly contador = computed(() => textoContador(this.escolhidas().size, this.dados()?.equipes.length ?? 0));
  readonly visiveis = computed(() =>
    equipesVisiveis(this.dados()?.equipes ?? [], this.busca(), this.soHabilitadas(), this.escolhidas()),
  );
  readonly saindo = computed(() => saindoComItens(this.dados()?.equipes ?? [], this.escolhidas()));

  constructor() {
    effect(() => {
      const grupo = this.grupo();
      untracked(() => void this.carregar(grupo));
    });
  }

  async carregar(grupo = this.grupo()): Promise<void> {
    this.erro.set(false);
    try {
      const dados = await this.admin.equipes(grupo);
      this.dados.set({ ...dados, equipes: ordenarEquipes(dados.equipes) });
      this.escolhidas.set(habilitadas(dados.equipes));
    } catch {
      this.erro.set(true);
    }
  }

  marcar(id: string, marcada: boolean): void {
    const novas = new Set(this.escolhidas());
    if (marcada) {
      novas.add(id);
    } else {
      novas.delete(id);
    }
    this.escolhidas.set(novas);
  }

  /** "Selecionar todas as visíveis" (true) e "Limpar seleção" (false): respeitam a busca e o "só habilitadas". */
  marcarTodas(marcar: boolean): void {
    this.escolhidas.set(marcarVisiveis(this.escolhidas(), this.visiveis(), marcar));
  }

  /** Para o aviso de sair com alterações pendentes (troca de aba e alteracoesPendentesGuard do detalhe). */
  temAlteracoesPendentes(): boolean {
    return this.alterou() && !this.salvando();
  }

  desfazer(): void {
    this.escolhidas.set(habilitadas(this.dados()?.equipes ?? []));
  }

  async salvar(): Promise<void> {
    const dados = this.dados();
    if (!dados || !this.alterou()) {
      return;
    }
    const grupo = this.grupo();
    const equipes = listaParaGravar(dados.equipes, this.escolhidas());
    this.salvando.set(true);
    const desfecho = await this.alteracao.executar({
      grupo,
      titulo: 'Alterar as equipes da área de gestão',
      chamar: (simular) => this.admin.definirEquipes(grupo, equipes, dados.versao, simular),
    });
    this.salvando.set(false);
    if (desfecho.status === 'gravado' || desfecho.status === 'conflito') {
      await this.carregar(grupo);
      this.alterada.emit();
    }
  }
}
