import {
  CampoEstrutura,
  EntidadeEstrutura,
  EstruturaListagem,
} from '../../../../core/administracao/estrutura-listagem';
import { TipoDado } from '../../../../core/metadados/modelo';

/** Atalhos para montar uma estrutura de listagem nos testes da administração. */
export function campoE(chave: string, tipo: TipoDado, extras: Partial<CampoEstrutura> = {}): CampoEstrutura {
  return {
    chave,
    nome: extras.nome ?? chave,
    tipo,
    obrigatorio: false,
    tamanhoMaximo: tipo === 'TEXTO' ? 100 : null,
    precisao: null,
    escala: null,
    opcoes: [],
    entidadeReferenciada: null,
    acaoExclusao: tipo === 'REFERENCIA' ? 'RESTRINGIR' : null,
    ordem: 0,
    ativo: true,
    dependencia: null,
    ...extras,
  };
}

export function entidadeE(
  chave: string,
  campos: CampoEstrutura[],
  extras: Partial<EntidadeEstrutura> = {},
): EntidadeEstrutura {
  return {
    chave,
    nome: extras.nome ?? chave,
    nomePlural: null,
    icone: null,
    descricao: null,
    ordem: 0,
    escopoEquipe: true,
    ativo: true,
    versao: 'v',
    campoExibicao: null,
    quantidadeRegistros: 0,
    campos,
    restricoes: [],
    ...extras,
  };
}

/**
 * Como Testes de Desempenho: projeto; endpoint (→ projeto); relatório (→ projeto, → ambiente global); cenário (→ relatório CASCATA,
 * → endpoint, que depende do projeto do relatório); ambiente (global).
 */
export function estruturaDesempenho(): EstruturaListagem {
  const ref = (chave: string, alvo: string, extras: Partial<CampoEstrutura> = {}) =>
    campoE(chave, 'REFERENCIA', { entidadeReferenciada: alvo, ...extras });
  return {
    chave: 'testes',
    nome: 'Testes',
    descricao: null,
    ativo: true,
    versao: 'g',
    quantidadeEquipes: 2,
    entidades: [
      entidadeE('projeto', [campoE('nome', 'TEXTO', { nome: 'Nome' })], { nome: 'Projeto', campoExibicao: 'nome' }),
      entidadeE('endpoint', [ref('projeto', 'projeto', { nome: 'Projeto' }), campoE('nome', 'TEXTO')], {
        nome: 'Endpoint de Serviço',
        nomePlural: 'Endpoints de Serviço',
      }),
      entidadeE('ambiente', [campoE('nome', 'TEXTO')], { nome: 'Ambiente', escopoEquipe: false }),
      entidadeE(
        'relatorio',
        [
          ref('projeto', 'projeto', { nome: 'Projeto' }),
          ref('ambiente', 'ambiente'),
          campoE('conclusao', 'TEXTO_LONGO'),
        ],
        { nome: 'Relatório', campoExibicao: null },
      ),
      entidadeE(
        'cenario',
        [
          ref('relatorio', 'relatorio', { nome: 'Relatório', obrigatorio: true, acaoExclusao: 'CASCATA' }),
          ref('endpoint', 'endpoint', {
            nome: 'Endpoint',
            dependencia: { via: 'relatorio', origem: 'projeto', alvo: 'projeto' },
          }),
          campoE('numero', 'INTEIRO'),
        ],
        { nome: 'Cenário' },
      ),
    ],
  };
}
