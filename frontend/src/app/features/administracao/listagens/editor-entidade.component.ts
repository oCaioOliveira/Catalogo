import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Message } from 'primeng/message';
import { Skeleton } from 'primeng/skeleton';
import { Tab, TabList, TabPanel, TabPanels, Tabs } from 'primeng/tabs';
import { Tag } from 'primeng/tag';
import { EstruturaListagem } from '../../../core/administracao/estrutura-listagem';
import { MetadadosAdminService } from '../../../core/administracao/metadados-admin.service';
import { lerProblema } from '../../../core/erros/problema';
import { CarregarHistorico, HistoricoComponent } from '../../../shared/historico/historico.component';
import { AbaCamposComponent } from './editor/aba-campos.component';
import { AbaDependenciasComponent } from './editor/aba-dependencias.component';
import { AbaGeralComponent } from './editor/aba-geral.component';
import { AbaPreviaComponent } from './editor/aba-previa.component';
import { AbaUnicidadesComponent } from './editor/aba-unicidades.component';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';
import { AreaAtualService } from '../../../core/metadados/area-atual.service';

/**
 * /configuracoes/areas/:grupo/entidades/:entidade — o editor de um tipo de item, em abas: Geral, Campos, Unicidades, Dependências,
 * Pré-visualização e Histórico. A estrutura da listagem inteira fica aqui (as abas recebem e devolvem a versão nova depois de cada
 * alteração); um conflito de versão (409) recarrega tudo.
 */
@Component({
  selector: 'app-editor-entidade',
  imports: [
    CabecalhoPaginaComponent,
    RouterLink,
    Message,
    Skeleton,
    Tabs,
    TabList,
    Tab,
    TabPanels,
    TabPanel,
    Tag,
    HistoricoComponent,
    AbaGeralComponent,
    AbaCamposComponent,
    AbaUnicidadesComponent,
    AbaDependenciasComponent,
    AbaPreviaComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (naoEncontrada()) {
      <p-message severity="warn">Este tipo de item não existe nesta área de gestão.</p-message>
      <p><a [routerLink]="['/configuracoes/areas', grupo()]">Voltar para a área de gestão</a></p>
    } @else if (estrutura() && entidadeAtual(); as entidade) {
      <app-cabecalho-pagina
        [trilha]="[
          { rotulo: 'Configurações' },
          { rotulo: 'Áreas de gestão', link: '/configuracoes/areas' },
          { rotulo: estrutura()!.nome, link: ['/configuracoes/areas', grupo()] },
          { rotulo: entidade.nome },
        ]"
        [titulo]="entidade.nome"
        [icone]="'pi ' + (entidade.icone || 'pi-file')"
      >
        <p complemento class="descricao">
          <code>{{ entidade.chave }}</code> ·
          {{ entidade.escopoEquipe ? 'Por equipe' : 'Cadastro de apoio (global)' }} ·
          {{ entidade.quantidadeRegistros }}
          {{ entidade.quantidadeRegistros === 1 ? 'item cadastrado' : 'itens cadastrados' }}
          ·
          <p-tag [value]="entidade.ativo ? 'Ativo' : 'Inativo'" [severity]="entidade.ativo ? 'success' : 'secondary'" />
        </p>
      </app-cabecalho-pagina>
      <p-tabs [value]="aba()" (valueChange)="aba.set($any($event))">
        <p-tablist>
          <p-tab value="geral">Geral</p-tab>
          <p-tab value="campos">Campos</p-tab>
          <p-tab value="unicidades">Unicidades</p-tab>
          <p-tab value="dependencias">Dependências</p-tab>
          <p-tab value="previa">Pré-visualização</p-tab>
          <p-tab value="historico">Histórico</p-tab>
        </p-tablist>
        <p-tabpanels>
          <p-tabpanel value="geral">
            <app-aba-geral [estrutura]="estrutura()!" [entidade]="entidade" (alterada)="aoAlterar($event)" />
          </p-tabpanel>
          <p-tabpanel value="campos">
            <app-aba-campos [estrutura]="estrutura()!" [entidade]="entidade" (alterada)="aoAlterar($event)" />
          </p-tabpanel>
          <p-tabpanel value="unicidades">
            <app-aba-unicidades [estrutura]="estrutura()!" [entidade]="entidade" (alterada)="aoAlterar($event)" />
          </p-tabpanel>
          <p-tabpanel value="dependencias">
            <app-aba-dependencias [estrutura]="estrutura()!" [entidade]="entidade" (alterada)="aoAlterar($event)" />
          </p-tabpanel>
          <p-tabpanel value="previa">
            @if (aba() === 'previa') {
              <app-aba-previa [grupo]="grupo()" [entidade]="entidade" />
            }
          </p-tabpanel>
          <p-tabpanel value="historico">
            @if (aba() === 'historico') {
              <app-historico [carregar]="carregarHistorico()" />
            }
          </p-tabpanel>
        </p-tabpanels>
      </p-tabs>
    } @else {
      <div aria-busy="true" aria-label="Carregando"><p-skeleton height="20rem" /></div>
    }
  `,
})
export class EditorEntidadeComponent {
  readonly grupo = input.required<string>();
  readonly entidade = input.required<string>();

  private readonly admin = inject(MetadadosAdminService);
  private readonly areaAtual = inject(AreaAtualService);

  readonly estrutura = signal<EstruturaListagem | null>(null);
  readonly naoEncontrada = signal(false);
  readonly aba = signal('geral');
  readonly entidadeAtual = computed(() => this.estrutura()?.entidades.find((e) => e.chave === this.entidade()) ?? null);
  readonly carregarHistorico = computed<CarregarHistorico>(() => {
    const [grupo, entidade] = [this.grupo(), this.entidade()];
    return (pagina, tamanho) => this.admin.historico(grupo, pagina, tamanho, entidade);
  });

  constructor() {
    effect(() => {
      const grupo = this.grupo();
      untracked(() => void this.carregar(grupo));
    });
  }

  async carregar(grupo = this.grupo()): Promise<void> {
    try {
      const estrutura = await this.admin.estrutura(grupo);
      this.estrutura.set(estrutura);
      // Administrar um tipo de item de uma área (ativa) faz dela a área atual, como no detalhe da área.
      if (estrutura.ativo) {
        this.areaAtual.definir(grupo);
      }
      this.naoEncontrada.set(!estrutura.entidades.some((e) => e.chave === this.entidade()));
    } catch (erro) {
      if (lerProblema(erro).status === 404) {
        this.naoEncontrada.set(true);
      } else {
        throw erro;
      }
    }
  }

  /** Uma aba gravou (a estrutura nova) ou houve conflito de versão (null: recarrega da API). */
  aoAlterar(estrutura: EstruturaListagem | null): void {
    if (estrutura) {
      this.estrutura.set(estrutura);
    } else {
      void this.carregar();
    }
  }
}
