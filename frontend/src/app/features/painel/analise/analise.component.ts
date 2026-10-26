import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { InputNumber } from 'primeng/inputnumber';
import { MultiSelect } from 'primeng/multiselect';
import { Select } from 'primeng/select';
import { FiltrosRegistrosComponent } from '../../registros/filtros/filtros-registros.component';
import { filtrosDoCampo, valoresDaListaDeFiltros } from '../../registros/filtros/filtros-url';
import { FiltrosPainelComponent } from '../filtros/filtros-painel.component';
import { CoresGraficoService } from '../graficos/cores';
import { ConfiguracaoWidget, TipoVisualizacao } from '../modelo/configuracao';
import {
  EstadoAnalise,
  EstadoPainel,
  analiseDaUrl,
  configuracaoParaCopiar,
  parametrosDaAnalise,
  parametrosDoEstadoPainel,
} from '../modelo/estado-url';
import {
  PERIODOS,
  camposDeFiltro,
  normalizarAnalise,
  opcoesDeDimensao,
  opcoesDeMetrica,
  opcoesDePeriodo,
} from '../modelo/opcoes-analise';
import { NOMES_VISUALIZACAO, visualizacoesPermitidas } from '../modelo/visualizacoes';
import { ContextoPainel } from '../pagina/contexto-painel';
import { WidgetComponent } from '../widget/widget.component';
import { CabecalhoPaginaComponent } from '../../../shared/pagina/cabecalho-pagina.component';
import { EstadoErroComponent } from '../../../shared/estados/estado-erro.component';
import { EstadoVazioComponent } from '../../../shared/estados/estado-vazio.component';

/**
 * /painel/:grupo/analise — o GESTOR monta o próprio gráfico sem programar: entidade, até 2 dimensões (também por um salto de
 * referência: "Relatório › Ambiente"), até 5 métricas, filtros (os mesmos componentes da listagem, com caminhos), o campo do
 * período e a visualização (só as que servem para a combinação). Tudo fica na URL: o link reproduz a análise. O resultado é
 * desenhado pelo MESMO componente dos widgets, com a configuração montada aqui; "Copiar configuração" dá o JSON para colar no
 * arquivo do grupo. Os erros 400 da API aparecem junto do controle correspondente.
 */
@Component({
  selector: 'app-analise',
  imports: [
    EstadoVazioComponent,
    EstadoErroComponent,
    CabecalhoPaginaComponent,
    FormsModule,
    Button,
    InputNumber,
    MultiSelect,
    Select,
    FiltrosPainelComponent,
    FiltrosRegistrosComponent,
    WidgetComponent,
  ],
  providers: [CoresGraficoService, ContextoPainel],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './analise.component.html',
})
export class AnaliseComponent {
  readonly grupo = input.required<string>();

  private readonly rota = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly mensagens = inject(MessageService);
  readonly contexto = inject(ContextoPainel);

  private readonly parametrosUrl = toSignal(this.rota.queryParams, { initialValue: {} });
  private readonly hoje = new Date();

  readonly estado = computed<EstadoAnalise>(() => analiseDaUrl(this.parametrosUrl(), this.hoje));
  readonly configuracao = computed(() => this.estado().configuracao);
  /** Tentar de novo quando os metadados da área não carregaram. */
  recarregarContexto(): void {
    void this.contexto.carregar(this.grupo());
  }

  readonly entidades = computed(() => this.contexto.entidades() ?? []);
  readonly entidade = computed(() => this.entidades().find((e) => e.chave === this.configuracao().entidade) ?? null);
  readonly parametrosPainel = computed(() => parametrosDoEstadoPainel(this.estado().painel));

  readonly opcoesEntidade = computed(() => this.entidades().map((e) => ({ valor: e.chave, rotulo: e.nome })));
  readonly opcoesDimensao = computed(() => {
    const entidade = this.entidade();
    return entidade ? opcoesDeDimensao(this.entidades(), entidade) : [];
  });
  readonly opcoesMetrica = computed(() => {
    const entidade = this.entidade();
    return entidade ? opcoesDeMetrica(this.entidades(), entidade) : [];
  });
  readonly opcoesPeriodo = computed(() => {
    const entidade = this.entidade();
    return entidade ? opcoesDePeriodo(this.entidades(), entidade) : [];
  });
  readonly camposFiltro = computed(() => {
    const entidade = this.entidade();
    return entidade ? camposDeFiltro(this.entidades(), entidade) : [];
  });
  readonly opcoesVisualizacao = computed(() =>
    visualizacoesPermitidas(this.configuracao().agruparPor, this.configuracao().metricas).map((v) => ({
      valor: v,
      rotulo: NOMES_VISUALIZACAO[v],
    })),
  );
  readonly opcoesOrdenacao = computed(() => {
    const c = this.configuracao();
    const chaves = [...new Set([...c.agruparPor, ...(c.metricas.length > 0 ? c.metricas : []), 'contagem'])];
    const rotulo = (chave: string) =>
      this.opcoesMetrica().find((o) => o.valor === chave)?.rotulo ?? this.rotuloDaDimensao(chave);
    return [
      { valor: '', rotulo: 'Padrão (pelas dimensões)' },
      ...chaves.flatMap((chave) => [
        { valor: chave, rotulo: `${rotulo(chave)} (crescente)` },
        { valor: `-${chave}`, rotulo: `${rotulo(chave)} (decrescente)` },
      ]),
    ];
  });

  readonly periodos = PERIODOS;

  /** Os campos com filtro na tela: os que já têm valor na URL e os escolhidos em "Filtrar por". */
  readonly camposComFiltro = signal<string[]>([]);
  readonly filtrosDisponiveis = computed(() =>
    this.camposFiltro()
      .filter((c) => this.camposComFiltro().includes(c.caminho))
      .flatMap((c) => {
        const prefixo = c.saltos.map((s) => `${s.chave}.`).join('');
        const rotulo = c.saltos.map((s) => `${s.nome} › `).join('');
        return filtrosDoCampo(c.campo, prefixo, rotulo);
      }),
  );
  readonly valoresFiltros = computed(() =>
    valoresDaListaDeFiltros(this.filtrosDisponiveis(), this.configuracao().filtros ?? {}),
  );

  /** Erros do último 400, por parâmetro da API. */
  readonly errosApi = signal<Record<string, string[]>>({});
  readonly errosDimensoes = computed(() => this.errosApi()['agruparPor'] ?? []);
  readonly errosMetricas = computed(() => this.errosApi()['metricas'] ?? []);
  readonly errosOrdenacao = computed(() => [
    ...(this.errosApi()['ordenarPor'] ?? []),
    ...(this.errosApi()['limite'] ?? []),
  ]);
  readonly errosPeriodo = computed(() => {
    const prefixo = this.configuracao().campoPeriodo;
    const chaves = prefixo ? [`${prefixo}.de`, `${prefixo}.ate`] : ['de', 'ate'];
    return chaves.flatMap((c) => this.errosApi()[c] ?? []);
  });
  readonly errosFiltros = computed(() => {
    const conhecidas = new Set(['agruparPor', 'metricas', 'ordenarPor', 'limite', 'de', 'ate']);
    const periodo = this.configuracao().campoPeriodo;
    return Object.entries(this.errosApi())
      .filter(([chave]) => !conhecidas.has(chave) && !(periodo && chave.startsWith(`${periodo}.`)))
      .flatMap(([chave, mensagens]) => mensagens.map((m) => `${chave}: ${m}`));
  });

  constructor() {
    effect(() => {
      const grupo = this.grupo();
      untracked(() => void this.contexto.carregar(grupo));
    });
    // Os campos com filtro na URL aparecem no painel de filtros (ex.: ao abrir um link compartilhado).
    effect(() => {
      const filtros = Object.keys(this.configuracao().filtros ?? {});
      const comValor = this.camposFiltro()
        .filter((c) => filtros.some((f) => f === c.caminho || f.startsWith(`${c.caminho}.`)))
        .map((c) => c.caminho);
      untracked(() => this.camposComFiltro.update((atuais) => [...new Set([...atuais, ...comValor])]));
    });
  }

  rotuloDaDimensao(chave: string): string {
    const [campo, periodo] = chave.split(':');
    const nome = this.opcoesDimensao().find((o) => o.valor === campo)?.rotulo ?? campo;
    const nomePeriodo = PERIODOS.find((p) => p.valor === periodo)?.rotulo.toLowerCase();
    return nomePeriodo ? `${nome} (${nomePeriodo})` : nome;
  }

  /** "relatorio_teste.data_execucao:mes" → { campo, periodo }. */
  partesDaDimensao(chave: string): { campo: string; periodo: string | null } {
    const [campo, periodo] = chave.split(':');
    return { campo, periodo: periodo ?? null };
  }

  ehDimensaoDeData(campo: string): boolean {
    return this.opcoesDimensao().find((o) => o.valor === campo)?.data ?? false;
  }

  // ---- Mudanças: cada uma vira navegação (a URL é a fonte da verdade). ----

  escolherEntidade(entidade: string): void {
    this.camposComFiltro.set([]);
    this.aplicar({ ...this.configuracao(), entidade, agruparPor: [], metricas: [], campoPeriodo: null, filtros: {} });
  }

  mudarDimensao(indice: number, campo: string, periodo: string | null): void {
    const chave = this.ehDimensaoDeData(campo) ? `${campo}:${periodo ?? 'mes'}` : campo;
    const agruparPor = [...this.configuracao().agruparPor];
    agruparPor[indice] = chave;
    this.aplicar({ ...this.configuracao(), agruparPor });
  }

  adicionarDimensao(): void {
    const usadas = this.configuracao().agruparPor.map((d) => this.partesDaDimensao(d).campo);
    const livre = this.opcoesDimensao().find((o) => !usadas.includes(o.valor));
    if (livre) {
      const chave = livre.data ? `${livre.valor}:mes` : livre.valor;
      this.aplicar({ ...this.configuracao(), agruparPor: [...this.configuracao().agruparPor, chave] });
    }
  }

  removerDimensao(indice: number): void {
    this.aplicar({ ...this.configuracao(), agruparPor: this.configuracao().agruparPor.filter((_, i) => i !== indice) });
  }

  mudarMetrica(indice: number, metrica: string): void {
    const metricas = [...this.metricasNaTela()];
    metricas[indice] = metrica;
    this.aplicar({ ...this.configuracao(), metricas });
  }

  adicionarMetrica(): void {
    const usadas = this.metricasNaTela();
    const livre = this.opcoesMetrica().find((o) => !usadas.includes(o.valor));
    if (livre) {
      this.aplicar({ ...this.configuracao(), metricas: [...usadas, livre.valor] });
    }
  }

  removerMetrica(indice: number): void {
    this.aplicar({ ...this.configuracao(), metricas: this.metricasNaTela().filter((_, i) => i !== indice) });
  }

  /** Sem métricas na URL, a API conta registros: a tela mostra a "Contagem" como a métrica única. */
  metricasNaTela(): string[] {
    return this.configuracao().metricas.length > 0 ? this.configuracao().metricas : ['contagem'];
  }

  mudarCampoPeriodo(campoPeriodo: string | null): void {
    this.aplicar({ ...this.configuracao(), campoPeriodo });
  }

  mudarVisualizacao(visualizacao: TipoVisualizacao): void {
    this.aplicar({ ...this.configuracao(), visualizacao });
  }

  mudarOrdenacao(ordenarPor: string): void {
    const configuracao: ConfiguracaoWidget = { ...this.configuracao(), ordenarPor };
    if (!ordenarPor) {
      delete configuracao.ordenarPor;
    }
    this.aplicar(configuracao);
  }

  mudarLimite(limite: number | null): void {
    const configuracao = { ...this.configuracao() };
    if (limite && limite >= 1 && limite <= 500) {
      configuracao.limite = limite;
    } else {
      delete configuracao.limite;
    }
    this.aplicar(configuracao);
  }

  aplicarFiltros(filtros: Record<string, string>): void {
    this.aplicar({ ...this.configuracao(), filtros });
  }

  mudarPainel(painel: EstadoPainel): void {
    this.navegar({ configuracao: this.configuracao(), painel });
  }

  async copiarConfiguracao(): Promise<void> {
    const json = JSON.stringify(configuracaoParaCopiar(this.configuracao()), null, 2);
    try {
      await navigator.clipboard.writeText(json);
      this.mensagens.add({
        severity: 'success',
        summary: 'Configuração copiada.',
        detail: 'Cole no arquivo do painel do grupo.',
      });
    } catch {
      this.mensagens.add({ severity: 'warn', summary: 'Não foi possível copiar.', detail: json, life: 20000 });
    }
  }

  private aplicar(configuracao: ConfiguracaoWidget): void {
    this.navegar({ configuracao: normalizarAnalise(configuracao), painel: this.estado().painel });
  }

  private navegar(estado: EstadoAnalise): void {
    this.errosApi.set({});
    void this.router.navigate([], { relativeTo: this.rota, queryParams: parametrosDaAnalise(estado) });
  }
}
