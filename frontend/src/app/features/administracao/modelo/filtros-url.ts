import { Params } from '@angular/router';
import { Perfil } from '../../../core/auth/usuario';
import { environment } from '../../../../environments/environment';

/**
 * Filtros, página e ordenação das listagens da administração NA URL (como a listagem de registros, ADR 0037). Os query params da
 * tela são os parâmetros da API; os valores padrão ficam fora. Funções puras, nos dois sentidos (filtros-url.spec.ts).
 *
 *   usuários: ?busca=joao&perfil=GESTOR&equipeId=<id>&ativo=false&pagina=2&tamanho=50&ordenacao=-ultimoAcessoEm
 *   equipes:  ?busca=squad&ativa=true&pagina=2
 */

export const ORDENACAO_USUARIOS_PADRAO = 'nome';
const PERFIS: Perfil[] = ['EXTERNO', 'INTERNO', 'GESTOR', 'ADMINISTRADOR'];

export interface FiltrosUsuarios {
  busca: string;
  perfil: Perfil | null;
  equipeId: string | null;
  /** Nulo = todos; true = ativos; false = inativos. */
  ativo: boolean | null;
  pagina: number;
  tamanho: number;
  ordenacao: string;
}

export interface FiltrosEquipes {
  busca: string;
  ativa: boolean | null;
  pagina: number;
  tamanho: number;
}

export function filtrosUsuariosDaUrl(parametros: Params): FiltrosUsuarios {
  const perfil = texto(parametros['perfil']) as Perfil;
  const ordenacao = texto(parametros['ordenacao']);
  return {
    busca: texto(parametros['busca']),
    perfil: PERFIS.includes(perfil) ? perfil : null,
    equipeId: texto(parametros['equipeId']) || null,
    ativo: booleano(parametros['ativo']),
    pagina: inteiro(parametros['pagina'], 1, Number.MAX_SAFE_INTEGER) ?? 1,
    tamanho: inteiro(parametros['tamanho'], 1, 100) ?? environment.tamanhoPagina,
    ordenacao: /^-?(nome|matricula|ultimoAcessoEm|criadoEm)$/.test(ordenacao) ? ordenacao : ORDENACAO_USUARIOS_PADRAO,
  };
}

/** Os query params da tela: só o que difere do padrão. */
export function parametrosDosFiltrosUsuarios(filtros: FiltrosUsuarios): Record<string, string> {
  const parametros: Record<string, string> = {};
  if (filtros.busca.trim()) {
    parametros['busca'] = filtros.busca.trim();
  }
  if (filtros.perfil) {
    parametros['perfil'] = filtros.perfil;
  }
  if (filtros.equipeId) {
    parametros['equipeId'] = filtros.equipeId;
  }
  if (filtros.ativo !== null) {
    parametros['ativo'] = String(filtros.ativo);
  }
  paginacao(parametros, filtros.pagina, filtros.tamanho);
  if (filtros.ordenacao !== ORDENACAO_USUARIOS_PADRAO) {
    parametros['ordenacao'] = filtros.ordenacao;
  }
  return parametros;
}

/** O que a API recebe: os da tela, com página e tamanho sempre explícitos. */
export function parametrosApiUsuarios(filtros: FiltrosUsuarios): Record<string, string> {
  return {
    ...parametrosDosFiltrosUsuarios(filtros),
    pagina: String(filtros.pagina),
    tamanho: String(filtros.tamanho),
    ordenacao: filtros.ordenacao,
  };
}

export function filtrosEquipesDaUrl(parametros: Params): FiltrosEquipes {
  return {
    busca: texto(parametros['busca']),
    ativa: booleano(parametros['ativa']),
    pagina: inteiro(parametros['pagina'], 1, Number.MAX_SAFE_INTEGER) ?? 1,
    tamanho: inteiro(parametros['tamanho'], 1, 100) ?? environment.tamanhoPagina,
  };
}

export function parametrosDosFiltrosEquipes(filtros: FiltrosEquipes): Record<string, string> {
  const parametros: Record<string, string> = {};
  if (filtros.busca.trim()) {
    parametros['busca'] = filtros.busca.trim();
  }
  if (filtros.ativa !== null) {
    parametros['ativa'] = String(filtros.ativa);
  }
  paginacao(parametros, filtros.pagina, filtros.tamanho);
  return parametros;
}

export function parametrosApiEquipes(filtros: FiltrosEquipes): Record<string, string> {
  return { ...parametrosDosFiltrosEquipes(filtros), pagina: String(filtros.pagina), tamanho: String(filtros.tamanho) };
}

function paginacao(parametros: Record<string, string>, pagina: number, tamanho: number): void {
  if (pagina !== 1) {
    parametros['pagina'] = String(pagina);
  }
  if (tamanho !== environment.tamanhoPagina) {
    parametros['tamanho'] = String(tamanho);
  }
}

function texto(valor: unknown): string {
  const bruto = Array.isArray(valor) ? valor[0] : valor;
  return bruto === undefined || bruto === null ? '' : String(bruto).trim();
}

function booleano(valor: unknown): boolean | null {
  const t = texto(valor);
  return t === 'true' ? true : t === 'false' ? false : null;
}

function inteiro(valor: unknown, minimo: number, maximo: number): number | null {
  const n = Number(texto(valor));
  return texto(valor) !== '' && Number.isInteger(n) && n >= minimo && n <= maximo ? n : null;
}
