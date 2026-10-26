import { Campo, Entidade } from '../../../core/metadados/modelo';
import { ColunaTabela, LARGURAS_SISTEMA, larguraDoTipo } from '../../../shared/tabelas/colunas-tabela';
import { CHAVE_EQUIPE } from '../filtros/filtros-url';

// As colunas da listagem genérica de registros (montadas pelos metadados). Funções puras: colunas-registros.spec.ts.

export const CHAVE_SITUACAO = 'situacaoAprovacao';
export const CHAVE_CRIADO_EM = 'criadoEm';
export const PREFIXO_MEDIA = 'avaliacao.';

/** Uma coluna da listagem: a coluna genérica mais, quando é um campo, o campo (para o valor, o tipo e a ordenação). */
export interface ColunaRegistro extends ColunaTabela {
  campo?: Campo;
  /** O critério de estrelas (colunas de média, RN48). */
  criterio?: string;
  numero?: boolean;
}

/**
 * O campo que fica FIXO à esquerda (sempre visível, primeiro, com o link para o item): o campo de exibição; sem ele, o primeiro
 * campo que não é texto longo.
 */
export function campoFixo(entidade: Entidade): Campo | null {
  return (
    entidade.campos.find((c) => c.chave === entidade.campoExibicao) ??
    entidade.campos.find((c) => c.tipo !== 'TEXTO_LONGO') ??
    null
  );
}

/**
 * Todas as colunas que a listagem oferece, na ordem padrão: o campo fixo; a Equipe (RN91) e a Situação (RN34), quando cabem;
 * os demais campos (texto longo por último: não cabe numa célula); as médias dos critérios de estrelas (RN48); Criado em.
 */
export function colunasDaEntidade(
  entidade: Entidade,
  opcoes: { comEquipe: boolean; comSituacao: boolean },
): ColunaRegistro[] {
  const fixo = campoFixo(entidade);
  const campos = entidade.campos.filter((c) => c !== fixo);
  const curtos = campos.filter((c) => c.tipo !== 'TEXTO_LONGO');
  const longos = campos.filter((c) => c.tipo === 'TEXTO_LONGO');
  const deCampo = (campo: Campo, fixa = false): ColunaRegistro => ({
    chave: campo.chave,
    nome: campo.nome,
    largura: larguraDoTipo(campo.tipo),
    campo,
    fixa,
    numero: campo.tipo === 'INTEIRO' || campo.tipo === 'DECIMAL',
  });
  return [
    ...(fixo ? [deCampo(fixo, true)] : []),
    ...(opcoes.comEquipe ? [{ chave: CHAVE_EQUIPE, nome: 'Equipe', largura: LARGURAS_SISTEMA.equipe }] : []),
    ...(opcoes.comSituacao ? [{ chave: CHAVE_SITUACAO, nome: 'Situação', largura: LARGURAS_SISTEMA.situacao }] : []),
    ...curtos.map((c) => deCampo(c)),
    ...longos.map((c) => deCampo(c)),
    ...entidade.criteriosAvaliacao.map((c) => ({
      chave: `${PREFIXO_MEDIA}${c.chave}`,
      nome: `Média: ${c.nome}`,
      largura: LARGURAS_SISTEMA.media,
      criterio: c.chave,
      numero: true,
    })),
    { chave: CHAVE_CRIADO_EM, nome: 'Criado em', largura: LARGURAS_SISTEMA.criadoEm },
  ];
}

/** As colunas que entram na conta de "o que cabe" na primeira visita: texto longo e médias só se a pessoa escolher. */
export function colunasCandidatasAoPadrao(colunas: readonly ColunaRegistro[]): ColunaRegistro[] {
  return colunas.filter((c) => c.campo?.tipo !== 'TEXTO_LONGO' && !c.criterio);
}
