import { Entidade } from '../../../core/metadados/modelo';
import { TIPOS_DATA, TIPOS_DIMENSAO, TIPOS_NUMERICOS, ehErro, resolverCaminho } from './caminhos';
import { ConfiguracaoWidget } from './configuracao';
import { NOMES_VISUALIZACAO, visualizacoesPermitidas } from './visualizacoes';

/**
 * Confere a configuração de um widget contra os METADADOS ao carregar o painel (validacao-widget.spec.ts): entidade, dimensões,
 * métricas, campo de período, filtros fixos (também por caminho), ordenação, limite e a visualização. Devolve as mensagens (vazio
 * = válida). Um widget inválido é mostrado com estado de erro e a mensagem vai para o console; a página não quebra. A API valida
 * tudo de novo: isto pega cedo um campo renomeado ou desativado na administração de metadados.
 */
export function validarWidget(configuracao: ConfiguracaoWidget, entidades: Entidade[]): string[] {
  const entidade = entidades.find((e) => e.chave === configuracao.entidade);
  if (!entidade) {
    return [`Entidade '${configuracao.entidade}' não existe (ou não está disponível) no grupo.`];
  }

  const erros: string[] = [];
  if (configuracao.agruparPor.length > 2) {
    erros.push('No máximo 2 dimensões.');
  }
  if (configuracao.metricas.length > 5) {
    erros.push('No máximo 5 métricas.');
  }
  for (const dimensao of configuracao.agruparPor) {
    erros.push(...validarDimensao(entidades, entidade, dimensao));
  }
  for (const metrica of configuracao.metricas) {
    erros.push(...validarMetrica(entidades, entidade, metrica));
  }
  if (configuracao.campoPeriodo) {
    const caminho = resolverCaminho(entidades, entidade, configuracao.campoPeriodo);
    if (ehErro(caminho)) {
      erros.push(`Campo de período: ${caminho.erro}`);
    } else if (!TIPOS_DATA.includes(caminho.campo.tipo)) {
      erros.push(
        `Campo de período '${configuracao.campoPeriodo}' é ${caminho.campo.tipo}: precisa ser DATA ou DATA_HORA.`,
      );
    }
  }
  for (const chave of Object.keys(configuracao.filtros ?? {})) {
    erros.push(...validarFiltro(entidades, entidade, chave));
  }
  if (configuracao.ordenarPor) {
    const chave = configuracao.ordenarPor.replace(/^-/, '');
    if (![...configuracao.agruparPor, ...configuracao.metricas, 'contagem'].includes(chave)) {
      erros.push(`Ordenação '${chave}' não é uma dimensão nem uma métrica do widget.`);
    }
  }
  if (
    configuracao.limite !== undefined &&
    !(Number.isInteger(configuracao.limite) && configuracao.limite >= 1 && configuracao.limite <= 500)
  ) {
    erros.push('Limite deve ser um inteiro de 1 a 500.');
  }
  if (!visualizacoesPermitidas(configuracao.agruparPor, configuracao.metricas).includes(configuracao.visualizacao)) {
    erros.push(
      `Visualização '${NOMES_VISUALIZACAO[configuracao.visualizacao] ?? configuracao.visualizacao}' não serve para ` +
        `${configuracao.agruparPor.length} dimensão(ões) e estas métricas.`,
    );
  }
  return erros;
}

function validarDimensao(entidades: Entidade[], entidade: Entidade, dimensao: string): string[] {
  const [chave, periodo] = dimensao.split(':');
  if (periodo !== undefined && !['dia', 'mes', 'trimestre', 'ano'].includes(periodo)) {
    return [`Dimensão '${dimensao}': período desconhecido (use dia, mes, trimestre ou ano).`];
  }
  if (chave === 'equipe') {
    return entidade.escopoEquipe && periodo === undefined
      ? []
      : [`Dimensão '${dimensao}' não se aplica a '${entidade.nome}'.`];
  }
  if (chave === 'situacaoAprovacao') {
    return periodo === undefined ? [] : [`Dimensão '${dimensao}': período só em datas.`];
  }
  if (chave === 'criadoEm') {
    return periodo !== undefined ? [] : [`Dimensão '${dimensao}': informe o período (criadoEm:mes).`];
  }
  const caminho = resolverCaminho(entidades, entidade, chave);
  if (ehErro(caminho)) {
    return [`Dimensão: ${caminho.erro}`];
  }
  if (!TIPOS_DIMENSAO.includes(caminho.campo.tipo)) {
    return [`Dimensão '${dimensao}' é ${caminho.campo.tipo}: não agrupa.`];
  }
  const ehData = TIPOS_DATA.includes(caminho.campo.tipo);
  if (ehData && periodo === undefined) {
    return [`Dimensão '${dimensao}' é uma data: informe o período (${chave}:mes).`];
  }
  if (!ehData && periodo !== undefined) {
    return [`Dimensão '${dimensao}': período só em datas.`];
  }
  return [];
}

function validarMetrica(entidades: Entidade[], entidade: Entidade, metrica: string): string[] {
  if (metrica === 'contagem') {
    return [];
  }
  const [funcao, alvo] = metrica.split(':');
  if (funcao === 'media_avaliacao' && alvo) {
    return []; // Os critérios dependem da equipe: só a API sabe.
  }
  if (!['media', 'soma', 'minimo', 'maximo'].includes(funcao) || !alvo) {
    return [`Métrica '${metrica}': use contagem, media:campo, soma:campo, minimo:campo ou maximo:campo.`];
  }
  const caminho = resolverCaminho(entidades, entidade, alvo);
  if (ehErro(caminho)) {
    return [`Métrica: ${caminho.erro}`];
  }
  return TIPOS_NUMERICOS.includes(caminho.campo.tipo)
    ? []
    : [`Métrica '${metrica}': '${alvo}' é ${caminho.campo.tipo}; só INTEIRO e DECIMAL.`];
}

function validarFiltro(entidades: Entidade[], entidade: Entidade, chave: string): string[] {
  if (chave === 'equipeId' || chave === 'situacaoAprovacao' || chave.startsWith('avaliacao.')) {
    return [];
  }
  if (!ehErro(resolverCaminho(entidades, entidade, chave))) {
    return [];
  }
  const ponto = chave.lastIndexOf('.');
  const operador = ponto < 0 ? null : chave.slice(ponto + 1);
  const caminho = ponto < 0 ? null : resolverCaminho(entidades, entidade, chave.slice(0, ponto));
  if (!caminho || ehErro(caminho) || !operador) {
    return [`Filtro '${chave}': campo desconhecido.`];
  }
  const tipo = caminho.campo.tipo;
  const aceito =
    (operador === 'contem' && (tipo === 'TEXTO' || tipo === 'TEXTO_LONGO' || tipo === 'URL')) ||
    ((operador === 'de' || operador === 'ate') && [...TIPOS_NUMERICOS, ...TIPOS_DATA].includes(tipo));
  return aceito ? [] : [`Filtro '${chave}': operador '${operador}' não serve para ${tipo}.`];
}
