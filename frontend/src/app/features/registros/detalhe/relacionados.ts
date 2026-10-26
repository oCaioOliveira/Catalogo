import { Params } from '@angular/router';
import { Campo, Entidade } from '../../../core/metadados/modelo';
import { nomePlural } from '../../../core/metadados/regras-metadados';
import { Relacionado } from '../../../core/registros/modelo-registro';
import { PARAMETRO_PAINEL_FILTROS } from '../filtros/filtros-url';

// Itens relacionados no detalhe de um item (RN94, ADR 0047), em funções puras (relacionados.spec.ts). As relações vêm da API
// (GET .../registros/{id}/relacionados), descobertas pelos metadados: nada aqui conhece um tipo de item.

export type { Relacionado };

/** Uma seção do detalhe, já com os metadados da entidade de origem. */
export interface SecaoRelacionada {
  /** Estável por entidade e campo ("cenario_teste.relatorio_teste"): chave do estado aberto/fechado e dos ids da tela. */
  id: string;
  entidade: Entidade;
  campo: Campo;
  filha: boolean;
  quantidade: number;
  /** "Relatórios de Teste (12)"; com mais de um campo da mesma entidade, "Relatórios de Teste — Ambiente (5)". */
  titulo: string;
  /** O título sem a quantidade (nome acessível e mensagens). */
  nome: string;
}

export interface SecoesDoDetalhe {
  /** As filhas (CASCATA e obrigatório): editáveis no próprio detalhe, sempre aparecem (mesmo vazias, com o botão de adicionar). */
  filhas: SecaoRelacionada[];
  /** Os demais relacionados, somente leitura, com pelo menos um item. */
  leitura: SecaoRelacionada[];
  /** Os nomes das relações somente leitura sem nenhum item: "Sem itens relacionados em: …". */
  vazias: string[];
}

/** Máximo de colunas de uma seção somente leitura. */
export const MAXIMO_COLUNAS_SECAO = 6;
/** Itens por página numa seção. */
export const TAMANHO_PAGINA_SECAO = 10;
/** Até quantas seções todas abrem; acima disso, só a primeira. */
export const SECOES_ABERTAS_POR_PADRAO = 3;

/**
 * As seções a partir da resposta da API, na ordem dela (a dos metadados). Uma relação cuja entidade não está nos metadados que o
 * front tem (ex.: desativada) fica de fora.
 */
export function montarSecoes(relacionados: readonly Relacionado[], todas: readonly Entidade[]): SecoesDoDetalhe {
  const camposPorEntidade = new Map<string, number>();
  for (const r of relacionados) {
    camposPorEntidade.set(r.entidade.chave, (camposPorEntidade.get(r.entidade.chave) ?? 0) + 1);
  }
  const secoes: SecaoRelacionada[] = [];
  for (const r of relacionados) {
    const entidade = todas.find((e) => e.chave === r.entidade.chave);
    const campo = entidade?.campos.find((c) => c.chave === r.campo.chave);
    if (!entidade || !campo) {
      continue;
    }
    const plural = nomePlural(entidade);
    const nome = (camposPorEntidade.get(entidade.chave) ?? 0) > 1 ? `${plural} — ${campo.nome}` : plural;
    secoes.push({
      id: `${entidade.chave}.${campo.chave}`,
      entidade,
      campo,
      filha: r.filha,
      quantidade: r.quantidade,
      titulo: `${nome} (${r.quantidade})`,
      nome,
    });
  }
  const leitura = secoes.filter((s) => !s.filha);
  return {
    filhas: secoes.filter((s) => s.filha),
    leitura: leitura.filter((s) => s.quantidade > 0),
    vazias: leitura.filter((s) => s.quantidade === 0).map((s) => s.nome),
  };
}

/**
 * As colunas de uma seção somente leitura: os primeiros campos da entidade de origem, pela ordem dos metadados, até 6, sem o campo
 * que aponta para o item (seria sempre ele) e sem TEXTO_LONGO.
 */
export function colunasDaSecao(entidade: Entidade, campoQueAponta: string): Campo[] {
  const campos = [...entidade.campos]
    .sort((a, b) => a.ordem - b.ordem)
    .filter((c) => c.chave !== campoQueAponta && c.tipo !== 'TEXTO_LONGO');
  // O campo de exibição vem primeiro: é a coluna principal, com o link para o item (linha clicável, como na listagem).
  const exibicao = campos.find((c) => c.chave === entidade.campoExibicao);
  return (exibicao ? [exibicao, ...campos.filter((c) => c !== exibicao)] : campos).slice(0, MAXIMO_COLUNAS_SECAO);
}

/**
 * "Ver todos na listagem": a listagem da entidade de origem filtrada pelo item, com o estado na URL de sempre (ADR 0037) e o painel
 * de filtros aberto, para o filtro aparecer.
 */
export function linkVerTodos(
  grupo: string,
  secao: Pick<SecaoRelacionada, 'entidade' | 'campo'>,
  idItem: string,
): { comandos: string[]; parametros: Params } {
  return {
    comandos: ['/', grupo, secao.entidade.chave],
    parametros: { [secao.campo.chave]: idItem, [PARAMETRO_PAINEL_FILTROS]: 'aberto' },
  };
}

/** O que a página de criação recebe pela URL para vir com a referência preenchida (e editável). */
export interface Preenchimento {
  campo: string;
  id: string;
  /** A equipe sugerida (a do item de origem), se o usuário puder criar nela. */
  equipeId: string | null;
  /** Para onde voltar depois de salvar ou cancelar: o detalhe do item de origem. */
  voltar: string | null;
}

/** "Novo …" de uma seção: /:grupo/:origem/novo?preencher=<campo>&com=<id>[&equipe=<id>]&voltar=<detalhe do item>. */
export function linkNovo(
  grupo: string,
  secao: Pick<SecaoRelacionada, 'entidade' | 'campo'>,
  item: { id: string; equipeId?: string | null },
  voltar: string,
): { comandos: string[]; parametros: Params } {
  const parametros: Params = { preencher: secao.campo.chave, com: item.id, voltar };
  if (secao.entidade.escopoEquipe && item.equipeId) {
    parametros['equipe'] = item.equipeId;
  }
  return { comandos: ['/', grupo, secao.entidade.chave, 'novo'], parametros };
}

/**
 * Lê o pré-preenchimento da URL da página de criação. Só vale para um campo REFERENCIA da entidade; "voltar" só aceita um caminho
 * interno ("/…", nunca "//…" nem um endereço completo).
 */
export function preenchimentoDaUrl(parametros: Params, entidade: Entidade): Preenchimento | null {
  const texto = (v: unknown): string | null => (typeof v === 'string' && v.trim() !== '' ? v.trim() : null);
  const campo = texto(parametros['preencher']);
  const id = texto(parametros['com']);
  if (!campo || !id || !entidade.campos.some((c) => c.chave === campo && c.tipo === 'REFERENCIA')) {
    return null;
  }
  const voltar = texto(parametros['voltar']);
  return {
    campo,
    id,
    equipeId: texto(parametros['equipe']),
    voltar: voltar && voltar.startsWith('/') && !voltar.startsWith('//') ? voltar : null,
  };
}

/** Aberta por padrão: com até 3 seções, todas; acima disso, só a primeira. */
export function abertaPorPadrao(indice: number, total: number): boolean {
  return total <= SECOES_ABERTAS_POR_PADRAO || indice === 0;
}

/** O estado guardado (localStorage) de uma seção, ou o padrão. */
export function secaoAberta(guardado: Record<string, boolean>, id: string, indice: number, total: number): boolean {
  return typeof guardado[id] === 'boolean' ? guardado[id] : abertaPorPadrao(indice, total);
}

/** A chave do localStorage com o estado das seções do detalhe de uma entidade. */
export function chaveEstadoSecoes(grupo: string, entidade: string): string {
  return `catalogo.secoesRelacionadas.${grupo}.${entidade}`;
}

/** Lê o estado guardado; texto ausente ou inválido = nada guardado. */
export function lerEstadoSecoes(texto: string | null): Record<string, boolean> {
  try {
    const valor: unknown = texto ? JSON.parse(texto) : {};
    if (!valor || typeof valor !== 'object' || Array.isArray(valor)) {
      return {};
    }
    return Object.fromEntries(Object.entries(valor).filter(([, v]) => typeof v === 'boolean')) as Record<
      string,
      boolean
    >;
  } catch {
    return {};
  }
}
