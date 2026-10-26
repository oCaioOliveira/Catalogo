import { Campo, Entidade, TipoDado } from '../app/core/metadados/modelo';

/** Atalhos para montar metadados nos testes unitários sem repetir todas as propriedades. */
export function campo(chave: string, tipo: TipoDado, extras: Partial<Campo> = {}): Campo {
  return {
    chave,
    nome: extras.nome ?? chave,
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
    ...extras,
  };
}

export function entidade(chave: string, campos: Campo[], extras: Partial<Entidade> = {}): Entidade {
  return {
    chave,
    nome: extras.nome ?? chave,
    nomePlural: null,
    descricao: null,
    icone: null,
    escopoEquipe: true,
    campoExibicao: campos[0]?.chave ?? null,
    ordem: 0,
    ativo: true,
    campos,
    restricoesUnicas: [],
    equipesComAprovacao: [],
    criteriosAvaliacao: [],
    ...extras,
  };
}
