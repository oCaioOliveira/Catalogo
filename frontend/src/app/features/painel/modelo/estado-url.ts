import { Params } from '@angular/router';
import { FiltrosPainel } from '../../../core/painel/modelo-painel';
import { ConfiguracaoWidget, TipoVisualizacao } from './configuracao';
import { ATALHO_PADRAO, AtalhoPeriodo, calcularPeriodo, ehAtalho, ehDataValida } from './periodo';
import { NOMES_VISUALIZACAO, visualizacoesPermitidas } from './visualizacoes';

/**
 * O estado do painel e da análise personalizada mora na URL (como a listagem, ADR 0037): o link compartilhado mostra
 * exatamente o mesmo painel. Funções puras, nos dois sentidos (estado-url.spec.ts).
 *
 *   painel:  ?periodo=3m  |  ?periodo=personalizado&de=2026-01-01&ate=2026-03-31  |  &equipeId=<id>
 *            (o padrão, "Últimos 12 meses", fica fora da URL; as datas dos atalhos são recalculadas a cada dia)
 *   análise: os mesmos + ?entidade=cenario_teste&agruparPor=relatorio_teste.ambiente&metricas=media:vazao_media_tps
 *            &visualizacao=colunas&campoPeriodo=relatorio_teste.data_execucao&ordenarPor=...&limite=...&titulo=...
 *            &f.relatorio_teste.tipo_teste=Carga   (filtros fixos com o prefixo "f.", para não colidir com "de" e "ate")
 */

export interface EstadoPainel {
  atalho: AtalhoPeriodo;
  de: string;
  ate: string;
  /** Nulo = todas as equipes que o usuário vê ("Todas" para o administrador, "Minhas equipes" para o gestor). */
  equipeId: string | null;
}

export const PREFIXO_FILTRO = 'f.';
export const ID_ANALISE = 'analise';

export function estadoPainelDaUrl(parametros: Params, hoje: Date): EstadoPainel {
  const equipeId = texto(parametros['equipeId']) || null;
  const atalho = texto(parametros['periodo']);
  const de = texto(parametros['de']);
  const ate = texto(parametros['ate']);
  if (atalho === 'personalizado' && ehDataValida(de) && ehDataValida(ate) && de <= ate) {
    return { atalho: 'personalizado', de, ate, equipeId };
  }
  const valido = ehAtalho(atalho) && atalho !== 'personalizado' ? atalho : ATALHO_PADRAO;
  return { atalho: valido, ...calcularPeriodo(valido as Exclude<AtalhoPeriodo, 'personalizado'>, hoje), equipeId };
}

export function parametrosDoEstadoPainel(estado: EstadoPainel): Record<string, string> {
  const parametros: Record<string, string> = {};
  if (estado.atalho !== ATALHO_PADRAO) {
    parametros['periodo'] = estado.atalho;
  }
  if (estado.atalho === 'personalizado') {
    parametros['de'] = estado.de;
    parametros['ate'] = estado.ate;
  }
  if (estado.equipeId) {
    parametros['equipeId'] = estado.equipeId;
  }
  return parametros;
}

export function filtrosDoEstado(estado: EstadoPainel): FiltrosPainel {
  return { de: estado.de, ate: estado.ate, equipeId: estado.equipeId };
}

/** A análise: a configuração do widget que ela monta (o mesmo formato dos pré-configurados) e os filtros globais. */
export interface EstadoAnalise {
  configuracao: ConfiguracaoWidget;
  painel: EstadoPainel;
}

export function analiseDaUrl(parametros: Params, hoje: Date): EstadoAnalise {
  const agruparPor = lista(parametros['agruparPor']).slice(0, 2);
  const metricas = lista(parametros['metricas']).slice(0, 5);
  const permitidas = visualizacoesPermitidas(agruparPor, metricas);
  const pedida = texto(parametros['visualizacao']) as TipoVisualizacao;
  const filtros: Record<string, string> = {};
  for (const [chave, valor] of Object.entries(parametros)) {
    if (chave.startsWith(PREFIXO_FILTRO) && texto(valor)) {
      filtros[chave.slice(PREFIXO_FILTRO.length)] = texto(valor);
    }
  }
  const limite = Number(texto(parametros['limite']));
  const configuracao: ConfiguracaoWidget = {
    id: ID_ANALISE,
    titulo: texto(parametros['titulo']) || 'Análise personalizada',
    entidade: texto(parametros['entidade']),
    agruparPor,
    metricas,
    campoPeriodo: texto(parametros['campoPeriodo']) || null,
    visualizacao: pedida in NOMES_VISUALIZACAO && permitidas.includes(pedida) ? pedida : permitidas[0],
  };
  if (Object.keys(filtros).length > 0) {
    configuracao.filtros = filtros;
  }
  if (texto(parametros['ordenarPor'])) {
    configuracao.ordenarPor = texto(parametros['ordenarPor']);
  }
  if (Number.isInteger(limite) && limite >= 1 && limite <= 500) {
    configuracao.limite = limite;
  }
  return { configuracao, painel: estadoPainelDaUrl(parametros, hoje) };
}

/** O caminho de volta: também é o "Abrir na análise" de um widget (a configuração dele vai inteira para a URL). */
export function parametrosDaAnalise(estado: EstadoAnalise): Record<string, string> {
  const c = estado.configuracao;
  const parametros: Record<string, string> = { ...parametrosDoEstadoPainel(estado.painel) };
  if (c.entidade) {
    parametros['entidade'] = c.entidade;
  }
  if (c.agruparPor.length > 0) {
    parametros['agruparPor'] = c.agruparPor.join(',');
  }
  if (c.metricas.length > 0) {
    parametros['metricas'] = c.metricas.join(',');
  }
  parametros['visualizacao'] = c.visualizacao;
  if (c.campoPeriodo) {
    parametros['campoPeriodo'] = c.campoPeriodo;
  }
  if (c.ordenarPor) {
    parametros['ordenarPor'] = c.ordenarPor;
  }
  if (c.limite !== undefined) {
    parametros['limite'] = String(c.limite);
  }
  if (c.id !== ID_ANALISE && c.titulo) {
    parametros['titulo'] = c.titulo;
  }
  for (const [chave, valor] of Object.entries(c.filtros ?? {})) {
    parametros[`${PREFIXO_FILTRO}${chave}`] = valor;
  }
  return parametros;
}

/**
 * A configuração como vai para o "Copiar configuração": um widget pronto para colar no arquivo do grupo (o id e o título
 * são sugestões, para quem colar ajustar).
 */
export function configuracaoParaCopiar(configuracao: ConfiguracaoWidget): ConfiguracaoWidget {
  const copia: ConfiguracaoWidget = { ...configuracao };
  if (copia.id === ID_ANALISE) {
    copia.id = [copia.entidade, ...copia.agruparPor]
      .join('-')
      .replace(/[^a-z0-9]+/gi, '-')
      .toLowerCase();
  }
  return copia;
}

function texto(valor: unknown): string {
  if (Array.isArray(valor)) {
    return valor.length > 0 ? String(valor[0]) : '';
  }
  return valor === undefined || valor === null ? '' : String(valor).trim();
}

function lista(valor: unknown): string[] {
  return texto(valor)
    .split(',')
    .map((v) => v.trim())
    .filter((v) => v.length > 0);
}

export interface OpcaoEquipe {
  /** Nulo = todas as que o usuário vê. */
  valor: string | null;
  rotulo: string;
}

/**
 * As opções do filtro de equipe (RN79, RN91), sempre com as equipes do usuário NESTA listagem (`Listagem.minhasEquipes`; para o
 * ADMINISTRADOR, todas as habilitadas nela): o ADMINISTRADOR escolhe "Todas" ou uma equipe; o GESTOR com mais de uma equipe na
 * listagem, "Minhas equipes" ou uma delas; com uma equipe só, o filtro não aparece (null).
 */
export function opcoesDeEquipe(
  perfil: string | null | undefined,
  equipesNaListagem: readonly { id: string; nome: string }[],
): OpcaoEquipe[] | null {
  const opcoes = [...equipesNaListagem]
    .sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'))
    .map((e) => ({ valor: e.id, rotulo: e.nome }));
  if (perfil === 'ADMINISTRADOR') {
    return [{ valor: null, rotulo: 'Todas' }, ...opcoes];
  }
  if (opcoes.length > 1) {
    return [{ valor: null, rotulo: 'Minhas equipes' }, ...opcoes];
  }
  return null;
}
