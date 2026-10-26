import { Params } from '@angular/router';
import { SITUACOES, Situacao } from '../../../core/aprovacao/regras-aprovacao';
import { Campo, Entidade, OpcaoReferencia } from '../../../core/metadados/modelo';
import { dataDeTextoIso, dataParaTextoIso } from '../../../shared/formatacao/datas';
import { ValorCampo } from '../../../shared/formulario/valores';
import { environment } from '../../../../environments/environment';

/**
 * Filtros, página e ordenação da listagem. O estado mora na URL (docs/adr/0037): os query params da tela são
 * EXATAMENTE os parâmetros que a API aceita (RN17 a RN19, docs/api/README.md), ex.:
 *   ?bateria_testes.contem=carga&data_execucao.de=2026-10-01&projeto=<id>&pagina=2&ordenacao=-vazao_media_tps
 * Recarregar a página ou compartilhar o link mantém a mesma consulta. Funções puras (filtros-url.spec.ts).
 */

/** Um controle do painel de filtros: o parâmetro da API que ele gera e o texto do rótulo. */
export interface FiltroDoCampo {
  campo: Campo;
  parametro: string;
  rotulo: string;
  /** Opções com rótulo próprio (ex.: a situação de aprovação). Sem elas, uma LISTA mostra as opções do campo como estão. */
  opcoes?: { rotulo: string; valor: string }[];
}

/**
 * Os filtros de cada tipo (RN18): texto e URL "contém"; LISTA, BOOLEANO e REFERENCIA "igual"; números e datas "de/até". O campo pode
 * ser de um registro APONTADO (RN89): `caminho` é o que vem antes da chave dele ("relatorio_teste." → o parâmetro
 * "relatorio_teste.tipo_teste.contem") e `rotuloCaminho`, o começo do rótulo ("Relatório › ").
 */
export function filtrosDoCampo(campo: Campo, caminho = '', rotuloCaminho = ''): FiltroDoCampo[] {
  const chave = `${caminho}${campo.chave}`;
  const nome = `${rotuloCaminho}${campo.nome}`;
  switch (campo.tipo) {
    case 'TEXTO':
    case 'TEXTO_LONGO':
    case 'URL':
      return [{ campo, parametro: `${chave}.contem`, rotulo: `${nome} (contém)` }];
    case 'INTEIRO':
    case 'DECIMAL':
    case 'DATA':
    case 'DATA_HORA':
      return [
        { campo, parametro: `${chave}.de`, rotulo: `${nome}: de` },
        { campo, parametro: `${chave}.ate`, rotulo: `${nome}: até` },
      ];
    default:
      return [{ campo, parametro: chave, rotulo: nome }];
  }
}

export function filtrosDaEntidade(entidade: Entidade): FiltroDoCampo[] {
  return [...entidade.campos.flatMap((campo) => filtrosDoCampo(campo)), ...filtrosDeAprovacaoEAvaliacao(entidade)];
}

/**
 * Os filtros que não vêm de campos, só onde o recurso está ativo (ADR 0045): a situação de aprovação (RN37), se alguma equipe que o
 * usuário vê tem a aprovação ligada; e a média de cada critério de estrelas, de/até (RN48), se há modelo de avaliação ativo.
 */
export function filtrosDeAprovacaoEAvaliacao(entidade: Entidade): FiltroDoCampo[] {
  const filtros: FiltroDoCampo[] = [];
  if (entidade.equipesComAprovacao.length > 0) {
    filtros.push({
      campo: campoDeSistema('situacaoAprovacao', 'Situação', 'LISTA'),
      parametro: 'situacaoAprovacao',
      rotulo: 'Situação',
      opcoes: (Object.keys(SITUACOES) as Situacao[]).map((valor) => ({ rotulo: SITUACOES[valor].rotulo, valor })),
    });
  }
  for (const criterio of entidade.criteriosAvaliacao) {
    const campo = { ...campoDeSistema(`avaliacao.${criterio.chave}`, criterio.nome, 'DECIMAL'), escala: 1 };
    filtros.push(...filtrosDoCampo(campo, '', 'Média de '));
  }
  return filtros;
}

/** Chave da coluna e parâmetro do filtro de equipe (o mesmo nome da API, RN26). */
export const CHAVE_EQUIPE = 'equipeId';

/**
 * RN91: o filtro de Equipe, com as equipes do usuário NA listagem (as que a API aceita em `equipeId`). Quem decide se ele aparece é
 * `mostraEquipe` (core/metadados/listagens.ts). Sem equipes, nenhum filtro.
 */
export function filtroDeEquipe(equipes: readonly { id: string; nome: string }[]): FiltroDoCampo[] {
  if (equipes.length === 0) {
    return [];
  }
  return [
    {
      campo: campoDeSistema(CHAVE_EQUIPE, 'Equipe', 'LISTA'),
      parametro: CHAVE_EQUIPE,
      rotulo: 'Equipe',
      opcoes: equipes.map((e) => ({ rotulo: e.nome, valor: e.id })),
    },
  ];
}

function campoDeSistema(chave: string, nome: string, tipo: Campo['tipo']): Campo {
  return {
    chave,
    nome,
    tipo,
    obrigatorio: false,
    ordem: 0,
    ativo: true,
    tamanhoMaximo: null,
    precisao: null,
    escala: null,
    opcoes: [],
    entidadeReferenciada: null,
    acaoExclusao: null,
    dependeDe: null,
  };
}

/** Valor da tela → texto do parâmetro (ponto decimal, datas aaaa-mm-dd, referência pelo id). Vazio → null. */
export function valorFiltroParaParametro(campo: Campo, valor: ValorCampo): string | null {
  if (valor === null || valor === undefined || valor === '') {
    return null;
  }
  switch (campo.tipo) {
    case 'DATA':
    case 'DATA_HORA':
      // Filtro de DATA_HORA só com a data = o dia inteiro (RN18).
      return valor instanceof Date ? dataParaTextoIso(valor) : null;
    case 'REFERENCIA':
      return (valor as OpcaoReferencia).id || null;
    case 'BOOLEANO':
      return valor === true ? 'true' : 'false';
    case 'INTEIRO':
    case 'DECIMAL':
      return typeof valor === 'number' ? String(valor) : null;
    default: {
      const texto = String(valor).trim();
      return texto === '' ? null : texto;
    }
  }
}

/** Texto do parâmetro → valor da tela (o caminho de volta). A referência volta só com o id (o rótulo é buscado depois). */
export function parametroParaValorFiltro(campo: Campo, texto: string | null | undefined): ValorCampo {
  if (texto === null || texto === undefined || texto === '') {
    return null;
  }
  switch (campo.tipo) {
    case 'DATA':
    case 'DATA_HORA':
      return dataDeTextoIso(texto);
    case 'REFERENCIA':
      return { id: texto, rotulo: null };
    case 'BOOLEANO':
      return texto === 'true' ? true : texto === 'false' ? false : null;
    case 'INTEIRO':
    case 'DECIMAL': {
      const numero = Number(texto);
      return Number.isFinite(numero) ? numero : null;
    }
    default:
      return texto;
  }
}

/** Os valores do painel de filtros (parâmetro → valor) → os parâmetros da API, só os preenchidos. */
export function parametrosDosFiltros(entidade: Entidade, valores: Record<string, ValorCampo>): Record<string, string> {
  return parametrosDaListaDeFiltros(filtrosDaEntidade(entidade), valores);
}

/** O mesmo, para uma lista de filtros qualquer (ex.: a da análise do painel, com caminhos). */
export function parametrosDaListaDeFiltros(
  filtros: FiltroDoCampo[],
  valores: Record<string, ValorCampo>,
): Record<string, string> {
  const parametros: Record<string, string> = {};
  for (const filtro of filtros) {
    const texto = valorFiltroParaParametro(filtro.campo, valores[filtro.parametro] ?? null);
    if (texto !== null) {
      parametros[filtro.parametro] = texto;
    }
  }
  return parametros;
}

/** Os query params da URL → os valores do painel de filtros (parâmetros desconhecidos são ignorados). */
export function filtrosDosParametros(entidade: Entidade, parametros: Params): Record<string, ValorCampo> {
  return valoresDaListaDeFiltros(filtrosDaEntidade(entidade), parametros);
}

/** O mesmo, para uma lista de filtros qualquer. */
export function valoresDaListaDeFiltros(filtros: FiltroDoCampo[], parametros: Params): Record<string, ValorCampo> {
  const valores: Record<string, ValorCampo> = {};
  for (const filtro of filtros) {
    const valor = parametroParaValorFiltro(filtro.campo, textoDoParametro(parametros[filtro.parametro]));
    if (valor !== null) {
      valores[filtro.parametro] = valor;
    }
  }
  return valores;
}

/**
 * Estado só da TELA (não vai para a API): ?painelFiltros=aberto abre o painel de filtros ao chegar (ex.: "Ver todos na listagem"
 * de uma seção de relacionados, RN94). Os parâmetros da API são montados só pelos filtros conhecidos, então ele nunca vai junto.
 */
export const PARAMETRO_PAINEL_FILTROS = 'painelFiltros';

/** Página e ordenação da listagem. Ordenação no formato da API: "campo" ou "-campo". */
export interface EstadoListagem {
  pagina: number;
  tamanho: number;
  ordenacao: string;
}

export const ORDENACAO_PADRAO = '-criadoEm';

export function estadoDosParametros(parametros: Params): EstadoListagem {
  const pagina = Number(textoDoParametro(parametros['pagina']));
  const tamanho = Number(textoDoParametro(parametros['tamanho']));
  return {
    pagina: Number.isInteger(pagina) && pagina >= 1 ? pagina : 1,
    tamanho: Number.isInteger(tamanho) && tamanho >= 1 && tamanho <= 100 ? tamanho : environment.tamanhoPagina,
    ordenacao: textoDoParametro(parametros['ordenacao']) || ORDENACAO_PADRAO,
  };
}

/**
 * Os query params da tela (e da API): filtros + página + ordenação. Os valores padrão (página 1, tamanho padrão,
 * -criadoEm) ficam de fora, para a URL ficar curta.
 */
export function parametrosDaListagem(filtros: Record<string, string>, estado: EstadoListagem): Record<string, string> {
  const parametros: Record<string, string> = { ...filtros };
  if (estado.pagina !== 1) {
    parametros['pagina'] = String(estado.pagina);
  }
  if (estado.tamanho !== environment.tamanhoPagina) {
    parametros['tamanho'] = String(estado.tamanho);
  }
  if (estado.ordenacao !== ORDENACAO_PADRAO) {
    parametros['ordenacao'] = estado.ordenacao;
  }
  return parametros;
}

/** O que a API recebe: sempre com página e tamanho explícitos. */
export function parametrosDaApi(filtros: Record<string, string>, estado: EstadoListagem): Record<string, string> {
  return { ...filtros, pagina: String(estado.pagina), tamanho: String(estado.tamanho), ordenacao: estado.ordenacao };
}

/** "-vazao" → { campo: "vazao", descendente: true }. */
export function lerOrdenacao(ordenacao: string): { campo: string; descendente: boolean } {
  return ordenacao.startsWith('-')
    ? { campo: ordenacao.slice(1), descendente: true }
    : { campo: ordenacao, descendente: false };
}

function textoDoParametro(valor: unknown): string | null {
  if (Array.isArray(valor)) {
    return valor.length > 0 ? String(valor[0]) : null;
  }
  return valor === undefined || valor === null ? null : String(valor);
}
