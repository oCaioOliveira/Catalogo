import { TipoDado } from '../../../../core/metadados/modelo';

/** Os tipos de campo, com o nome e a explicação mostrados no painel lateral (RN04). */
export const TIPOS_CAMPO: readonly { tipo: TipoDado; rotulo: string; explicacao: string }[] = [
  { tipo: 'TEXTO', rotulo: 'Texto curto', explicacao: 'Uma linha de texto, com tamanho máximo (ex.: nome, código).' },
  {
    tipo: 'TEXTO_LONGO',
    rotulo: 'Texto longo',
    explicacao: 'Vários parágrafos, sem limite (ex.: observações). Não vira coluna.',
  },
  { tipo: 'INTEIRO', rotulo: 'Número inteiro', explicacao: 'Número sem casas decimais (ex.: quantidade).' },
  { tipo: 'DECIMAL', rotulo: 'Número decimal', explicacao: 'Número com casas decimais fixas (ex.: valor em reais).' },
  { tipo: 'BOOLEANO', rotulo: 'Sim ou não', explicacao: 'Uma marcação de sim ou não (ex.: ativo).' },
  { tipo: 'DATA', rotulo: 'Data', explicacao: 'Dia, mês e ano (ex.: data de aquisição).' },
  { tipo: 'DATA_HORA', rotulo: 'Data e hora', explicacao: 'Data com hora e minuto (ex.: execução do teste).' },
  { tipo: 'LISTA', rotulo: 'Lista de opções', explicacao: 'Uma escolha entre opções fixas (ex.: situação).' },
  {
    tipo: 'REFERENCIA',
    rotulo: 'Referência a outro tipo de item',
    explicacao: 'Escolha de um item de outro tipo desta área de gestão (ex.: a categoria do equipamento).',
  },
  { tipo: 'URL', rotulo: 'Endereço (URL)', explicacao: 'Link http ou https, que abre numa nova aba (ex.: manual).' },
];

export function rotuloDoTipo(tipo: TipoDado): string {
  return TIPOS_CAMPO.find((t) => t.tipo === tipo)?.rotulo ?? tipo;
}

/** As opções do painel lateral que mudam conforme o tipo (RN53: só vão os atributos do tipo). */
export interface OpcoesDoTipo {
  tamanhoMaximo: boolean;
  precisaoEscala: boolean;
  opcoesLista: boolean;
  referencia: boolean;
}

export function opcoesDoTipo(tipo: TipoDado): OpcoesDoTipo {
  return {
    tamanhoMaximo: tipo === 'TEXTO',
    precisaoEscala: tipo === 'DECIMAL',
    opcoesLista: tipo === 'LISTA',
    referencia: tipo === 'REFERENCIA',
  };
}

/**
 * Os tipos que um campo JÁ CRIADO pode assumir (RN66): o próprio e as conversões permitidas, TEXTO → TEXTO_LONGO,
 * TEXTO/TEXTO_LONGO → URL e URL → TEXTO_LONGO. As outras mudanças exigem um campo novo.
 */
export function tiposNaEdicao(atual: TipoDado): TipoDado[] {
  switch (atual) {
    case 'TEXTO':
      return ['TEXTO', 'TEXTO_LONGO', 'URL'];
    case 'TEXTO_LONGO':
      return ['TEXTO_LONGO', 'URL'];
    case 'URL':
      return ['URL', 'TEXTO_LONGO'];
    default:
      return [atual];
  }
}

/** Valores iniciais e limites dos atributos (os mesmos CHECKs do banco, RN53). */
export const LIMITES = {
  tamanhoMaximo: { minimo: 1, maximo: 4000, padrao: 100 },
  precisao: { minimo: 1, maximo: 38, padrao: 12 },
  escala: { minimo: 0, maximo: 10, padrao: 2 },
};

/** Ação ao excluir o item apontado por um campo REFERENCIA (RN11), com a explicação da tela. */
export const ACOES_EXCLUSAO: readonly { valor: 'RESTRINGIR' | 'CASCATA'; rotulo: string; explicacao: string }[] = [
  {
    valor: 'RESTRINGIR',
    rotulo: 'Restringir',
    explicacao:
      'O item apontado só pode ser excluído depois que nenhum item apontar mais para ele. É a opção mais segura.',
  },
  {
    valor: 'CASCATA',
    rotulo: 'Cascata',
    explicacao:
      'Excluir o item apontado exclui junto os itens que apontam para ele. Use para "partes" de um item ' +
      '(ex.: cenários de um relatório). Com o campo obrigatório, este tipo vira uma seção no detalhe do outro.',
  },
];
