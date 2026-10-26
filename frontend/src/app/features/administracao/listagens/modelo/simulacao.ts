import { ImpactoDto } from '../../../../api/models';
import { Impacto, impactoDeDto } from '../../../../core/administracao/estrutura-listagem';
import { Problema } from '../../../../core/erros/problema';

/**
 * Toda alteração de estrutura é SIMULADA antes de gravar (?simular=true, RN56, ADR 0044). O resultado da simulação decide o que a
 * tela faz (funções puras, simulacao.spec.ts):
 *   - sem-impacto: grava direto;
 *   - com-impacto: permitido, mas com efeito nos itens (ex.: "120 itens receberão o valor padrão"): confirma antes;
 *   - pede-valor-padrao: o campo ficaria obrigatório com itens vazios (RN60, RN61): pede o valor padrão e simula de novo;
 *   - bloqueado: mostra o relatório dos impactos que bloqueiam (quantidade, exemplos com link, orientação), sem confirmar.
 */
export type Classificacao =
  | { tipo: 'sem-impacto' }
  | { tipo: 'com-impacto'; impactos: Impacto[] }
  | { tipo: 'pede-valor-padrao'; impacto: Impacto }
  | { tipo: 'bloqueado'; impactos: Impacto[] };

export const OBRIGATORIO_SEM_VALOR = 'CAMPO_OBRIGATORIO_SEM_VALOR';

export function classificarSimulacao(
  resultado: { permitido: boolean; impactos: Impacto[] },
  podePedirValorPadrao: boolean,
): Classificacao {
  // Impacto que não atinge nenhum item nem tem usos listados não muda nada (ex.: "registros sem valor" numa entidade vazia).
  const relevantes = resultado.impactos.filter((i) => i.quantidade > 0 || (i.usos?.length ?? 0) > 0);
  if (resultado.permitido) {
    return relevantes.length === 0 ? { tipo: 'sem-impacto' } : { tipo: 'com-impacto', impactos: relevantes };
  }
  const bloqueantes = relevantes.filter((i) => i.bloqueia);
  if (podePedirValorPadrao && bloqueantes.length > 0 && bloqueantes.every((i) => i.tipo === OBRIGATORIO_SEM_VALOR)) {
    return { tipo: 'pede-valor-padrao', impacto: bloqueantes[0] };
  }
  // O relatório mostra só o que BLOQUEIA (os efeitos informativos não aconteceriam). Bloqueio sem contagem (ex.: configuração de
  // equipe): mostra todos os bloqueios, para a pessoa saber o motivo.
  return {
    tipo: 'bloqueado',
    impactos: bloqueantes.length > 0 ? bloqueantes : resultado.impactos.filter((i) => i.bloqueia),
  };
}

/**
 * Um 409 de bloqueio (ALTERACAO_INCOMPATIVEL_COM_DADOS ou METADADO_EM_USO) traz o mesmo relatório em detalhes.impactos: a tela o
 * mostra como uma simulação bloqueada. Outros erros: null.
 */
export function impactosDoBloqueio(problema: Problema): Impacto[] | null {
  if (problema.codigo !== 'ALTERACAO_INCOMPATIVEL_COM_DADOS' && problema.codigo !== 'METADADO_EM_USO') {
    return null;
  }
  const impactos = problema.detalhes?.['impactos'];
  return Array.isArray(impactos) ? (impactos as ImpactoDto[]).map(impactoDeDto) : [];
}

/** O resumo de um impacto em uma frase, para o diálogo de confirmação e o aviso depois de gravar. */
export function resumoDoImpacto(impacto: Impacto): string {
  const itens = impacto.quantidade === 1 ? '1 item' : `${impacto.quantidade} itens`;
  switch (impacto.tipo) {
    case 'VALOR_PADRAO_APLICADO':
      return `${itens} ${impacto.quantidade === 1 ? 'receberá' : 'receberão'} o valor padrão.`;
    case 'ITENS_OCULTOS':
      // RN92: a descrição já diz de que equipe são e que voltam ao habilitá-la de novo.
      return `${impacto.descricao} Itens que ficam ocultos: ${impacto.quantidade}.`;
    case 'CHAVES_UNICAS_GERADAS':
      return `${itens} ${impacto.quantidade === 1 ? 'passa' : 'passam'} a ser conferidos pela unicidade nova.`;
    default:
      return impacto.quantidade > 0 ? `${impacto.descricao} (${itens})` : impacto.descricao;
  }
}

/** O que fazer quando a alteração é bloqueada, por tipo de impacto (docs/api/administracao-de-metadados.md). */
export function orientacao(tipo: string): string {
  return ORIENTACOES[tipo] ?? 'Corrija os itens listados e tente de novo.';
}

const ORIENTACOES: Record<string, string> = {
  CAMPO_OBRIGATORIO_SEM_VALOR: 'Preencha esses itens ou informe um valor padrão para eles.',
  TEXTO_ACIMA_DO_TAMANHO: 'Encurte o texto desses itens ou escolha um tamanho máximo maior.',
  NUMERO_FORA_DA_PRECISAO: 'Corrija o valor desses itens ou mantenha a precisão e a escala atuais.',
  VALOR_NAO_E_URL: 'Corrija esses itens para endereços completos (http:// ou https://) ou mantenha o tipo atual.',
  OPCAO_EM_USO: 'Troque a opção desses itens antes de removê-la da lista, ou desative o campo.',
  CAMPO_COM_VALORES: 'O campo tem valores guardados: desative-o em vez de excluir (os valores ficam guardados).',
  CAMPO_EM_USO: 'Tire o campo dos usos listados (exibição, unicidade ou dependência) ou mantenha-o ativo.',
  DUPLICATAS_NA_RESTRICAO: 'Corrija os itens repetidos (cada grupo abaixo tem os mesmos valores) e tente de novo.',
  DEPENDENCIA_VIOLADA: 'Corrija esses itens para combinarem com a regra nova, ou escolha outra regra.',
  ENTIDADE_COM_REGISTROS: 'O tipo de item já tem itens: o escopo não muda. Desative-o em vez de excluir.',
  ENTIDADE_COM_HISTORICO: 'O tipo de item já teve itens: desative-o em vez de excluir.',
  ENTIDADE_COM_CONFIGURACOES: 'Desligue a aprovação e os modelos de avaliação das equipes antes.',
  ENTIDADE_REFERENCIADA: 'Outros campos apontam para este tipo de item: desative-o em vez de excluir.',
  GRUPO_COM_ENTIDADES: 'A área de gestão tem tipos de item: desative-a em vez de excluir.',
};
