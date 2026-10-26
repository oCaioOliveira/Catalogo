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
import { RouterLink } from '@angular/router';
import { MessageService } from 'primeng/api';
import { Button } from 'primeng/button';
import { Menu } from 'primeng/menu';
import { Message } from 'primeng/message';
import { Skeleton } from 'primeng/skeleton';
import { Tooltip } from 'primeng/tooltip';
import { tratarNoComponente } from '../../../core/erros/contexto-erros';
import { lerProblema } from '../../../core/erros/problema';
import { Entidade } from '../../../core/metadados/modelo';
import { Indicadores } from '../../../core/painel/modelo-painel';
import { PainelService } from '../../../core/painel/painel.service';
import { baixarArquivo } from '../../registros/comum/baixar-arquivo';
import { VisualizacaoComponent } from '../graficos/visualizacao.component';
import { Unidades, semDados } from '../graficos/transformar-dados';
import { ehErro, resolverCaminho, unidadeDoCampo } from '../modelo/caminhos';
import { ConfiguracaoWidget } from '../modelo/configuracao';
import { EstadoPainel, ID_ANALISE, filtrosDoEstado, parametrosDaAnalise } from '../modelo/estado-url';
import { parametrosDoWidget } from '../modelo/parametros-widget';
import { validarWidget } from '../modelo/validacao-widget';
import { FormatoExportacao, itensExportacao } from '../../../shared/exportacao/opcoes-exportacao';

type EstadoWidget =
  | { tipo: 'carregando' }
  | { tipo: 'invalido'; mensagens: string[] }
  | { tipo: 'erro'; mensagens: string[] }
  | { tipo: 'vazio' }
  | { tipo: 'pronto'; indicadores: Indicadores };

/**
 * O ÚNICO componente de widget do painel: recebe a CONFIGURAÇÃO (modelo/configuracao.ts), confere-a contra os metadados,
 * consulta GET .../indicadores com o período e a equipe do painel, transforma a resposta (funções puras) e desenha. Cada
 * widget carrega sozinho, com os próprios estados (carregando, sem dados, erro com "Tentar de novo"): um erro num widget não
 * afeta os outros. Ações: "Ver dados" (a tabela com os mesmos números), exportar CSV/XLSX e "Abrir na análise".
 */
@Component({
  selector: 'app-widget',
  imports: [RouterLink, Button, Menu, Message, Skeleton, Tooltip, VisualizacaoComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './widget.component.html',
  host: {
    class: 'cartao-painel',
    '[class.largura-total]': 'larguraTotal()',
    '[attr.data-widget]': 'configuracao().id',
  },
})
export class WidgetComponent {
  readonly grupo = input.required<string>();
  readonly configuracao = input.required<ConfiguracaoWidget>();
  readonly estadoPainel = input.required<EstadoPainel>();
  readonly entidades = input.required<Entidade[]>();

  /** Os erros por parâmetro de um 400 (agruparPor, metricas, a chave de um filtro...), para a análise mostrar junto do controle. */
  readonly errosApi = output<Record<string, string[]>>();

  private readonly painel = inject(PainelService);
  private readonly mensagens = inject(MessageService);

  readonly estado = signal<EstadoWidget>({ tipo: 'carregando' });
  readonly verDados = signal(false);
  readonly exportando = signal(false);
  private pedido = 0;

  readonly idTitulo = computed(() => `widget-${this.configuracao().id}-titulo`);
  readonly idDescricao = computed(() => `widget-${this.configuracao().id}-descricao`);
  readonly rotuladoPor = computed(() =>
    this.configuracao().descricao ? `${this.idTitulo()} ${this.idDescricao()}` : this.idTitulo(),
  );
  readonly larguraTotal = computed(
    () => this.configuracao().largura === 2 || this.configuracao().visualizacao === 'kpi',
  );
  readonly temGrafico = computed(() => !['kpi', 'tabela'].includes(this.configuracao().visualizacao));
  readonly naAnalise = computed(() => this.configuracao().id === ID_ANALISE);
  readonly linkAnalise = computed(() =>
    parametrosDaAnalise({ configuracao: this.configuracao(), painel: this.estadoPainel() }),
  );
  readonly entidade = computed(() => this.entidades().find((e) => e.chave === this.configuracao().entidade) ?? null);
  readonly parametros = computed(() =>
    parametrosDoWidget(this.configuracao(), filtrosDoEstado(this.estadoPainel()), this.entidade()),
  );
  /** A unidade de cada métrica: a do nome do campo ("Percentil 95 (ms)" → ms), ou a declarada na configuração. */
  readonly unidades = computed<Unidades>(() => {
    const unidades: Unidades = {};
    const entidade = this.entidade();
    for (const metrica of this.configuracao().metricas) {
      const alvo = metrica.split(':')[1];
      const caminho = entidade && alvo ? resolverCaminho(this.entidades(), entidade, alvo) : null;
      unidades[metrica] = caminho && !ehErro(caminho) ? unidadeDoCampo(caminho.campo) : null;
    }
    return { ...unidades, ...(this.configuracao().unidades ?? {}) };
  });

  readonly itensExportar = itensExportacao((formato) => void this.exportar(formato));

  constructor() {
    // Mudou a configuração, o período, a equipe ou os metadados: consulta de novo.
    effect(() => {
      const configuracao = this.configuracao();
      const entidades = this.entidades();
      const parametros = this.parametros();
      untracked(() => void this.carregar(configuracao, entidades, parametros));
    });
  }

  readonly mensagensDoEstado = computed(() => {
    const estado = this.estado();
    return estado.tipo === 'erro' || estado.tipo === 'invalido' ? estado.mensagens : [];
  });
  readonly indicadoresDoEstado = computed(() => {
    const estado = this.estado();
    return estado.tipo === 'pronto' ? estado.indicadores : null;
  });

  tentarDeNovo(): void {
    void this.carregar(this.configuracao(), this.entidades(), this.parametros());
  }

  private async carregar(
    configuracao: ConfiguracaoWidget,
    entidades: Entidade[],
    parametros: Record<string, string>,
  ): Promise<void> {
    const pedido = ++this.pedido;
    const invalidos = validarWidget(configuracao, entidades);
    if (invalidos.length > 0) {
      console.error(`[painel] Widget '${configuracao.id}' com configuração inválida:\n- ${invalidos.join('\n- ')}`);
      this.estado.set({ tipo: 'invalido', mensagens: invalidos });
      return;
    }

    this.estado.set({ tipo: 'carregando' });
    try {
      const indicadores = await this.painel.indicadores(
        this.grupo(),
        configuracao.entidade,
        parametros,
        tratarNoComponente(400),
      );
      if (pedido === this.pedido) {
        this.estado.set(semDados(indicadores) ? { tipo: 'vazio' } : { tipo: 'pronto', indicadores });
        this.errosApi.emit({});
      }
    } catch (erro) {
      if (pedido !== this.pedido) {
        return;
      }
      const problema = lerProblema(erro);
      if (problema.status === 400) {
        this.errosApi.emit(problema.erros);
        this.estado.set({ tipo: 'erro', mensagens: Object.values(problema.erros).flat() });
      } else {
        // Os demais (403, 5xx, sem conexão) já viraram aviso no interceptor.
        this.estado.set({ tipo: 'erro', mensagens: [] });
      }
    }
  }

  async exportar(formato: FormatoExportacao): Promise<void> {
    this.exportando.set(true);
    try {
      const { arquivo, nome } = await this.painel.exportarIndicadores(
        this.grupo(),
        this.configuracao().entidade,
        formato,
        this.parametros(),
      );
      baixarArquivo(arquivo, nome);
      this.mensagens.add({ severity: 'success', summary: `Arquivo ${nome} gerado.` });
    } catch {
      // O interceptor já mostrou a mensagem.
    } finally {
      this.exportando.set(false);
    }
  }
}
