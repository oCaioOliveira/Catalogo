import { ModeloAvaliacaoEntrada } from '../../../api/models';
import { CriterioAvaliacao, TAMANHO_TEXTO_PADRAO } from '../../../core/avaliacao/modelo-avaliacao';
import { gerarChave } from '../../../shared/chaves/chave';

// Regras do editor de modelo de avaliação (RN42, RN43), em funções puras (regras-modelo.spec.ts). A API valida de novo.

/** Um critério no editor: `salvo` = já existe no modelo gravado (a chave fica travada, RN42). */
export interface CriterioEditavel extends CriterioAvaliacao {
  salvo: boolean;
}

/** O que o editor deixa mudar num critério. Com respostas, excluir e mudar o tipo ficam travados (RN43): a saída é desativar. */
export interface TravasCriterio {
  podeExcluir: boolean;
  podeMudarTipo: boolean;
  podeMudarChave: boolean;
  explicacao: string | null;
}

export function travasDoCriterio(criterio: CriterioEditavel): TravasCriterio {
  const comRespostas = criterio.possuiRespostas;
  return {
    podeExcluir: !comRespostas,
    podeMudarTipo: !comRespostas,
    podeMudarChave: !criterio.salvo,
    explicacao: comRespostas
      ? 'Este critério já tem respostas: não pode ser excluído nem mudar de tipo (as notas dadas ficariam sem sentido). ' +
        'Para tirá-lo do formulário e das médias, desative-o; as respostas antigas ficam guardadas.'
      : null,
  };
}

/** A chave gerada a partir do nome, sem repetir a de outro critério do modelo (`qualidade`, `qualidade_2`...). */
export function chaveDoCriterio(nome: string, outras: string[]): string {
  const base = gerarChave(nome) || 'criterio';
  let chave = base;
  for (let n = 2; outras.includes(chave); n++) {
    chave = `${base}_${n}`;
  }
  return chave;
}

export function novoCriterio(ordem: number): CriterioEditavel {
  return {
    id: null,
    chave: '',
    nome: '',
    descricao: null,
    tipo: 'ESTRELAS',
    obrigatorio: false,
    comentario: 'NENHUM',
    tamanhoMaximoTexto: TAMANHO_TEXTO_PADRAO,
    ordem,
    ativo: true,
    possuiRespostas: false,
    salvo: false,
  };
}

/** O corpo do POST/PUT: a lista INTEIRA de critérios, na ordem da tela (RN43). Texto não tem comentário (RN42). */
export function corpoDoModelo(
  nome: string,
  descricao: string,
  criterios: CriterioEditavel[],
  versao: string | null,
): ModeloAvaliacaoEntrada {
  return {
    versao,
    nome: nome.trim(),
    descricao: descricao.trim() || null,
    criterios: criterios.map((c, i) => ({
      chave: c.chave,
      nome: c.nome.trim(),
      descricao: c.descricao?.trim() || null,
      tipo: c.tipo,
      obrigatorio: c.obrigatorio,
      comentario: c.tipo === 'TEXTO' ? 'NENHUM' : c.comentario,
      tamanhoMaximoTexto: c.tamanhoMaximoTexto,
      ordem: i + 1,
      ativo: c.ativo,
    })),
  };
}

/**
 * Os erros da API (`nome`, `criterios`, `criterios[1].tipo`...) separados por destino: os do modelo e os de cada critério (pela
 * posição na lista enviada, que é a da tela).
 */
export function separarErros(erros: Record<string, string[]>): {
  modelo: Record<string, string[]>;
  criterios: Record<number, string[]>;
} {
  const modelo: Record<string, string[]> = {};
  const criterios: Record<number, string[]> = {};
  for (const [chave, mensagens] of Object.entries(erros)) {
    const doCriterio = /^criterios\[(\d+)\]/.exec(chave);
    if (doCriterio) {
      const indice = Number(doCriterio[1]);
      criterios[indice] = [...(criterios[indice] ?? []), ...mensagens];
    } else {
      modelo[chave] = [...(modelo[chave] ?? []), ...mensagens];
    }
  }
  return { modelo, criterios };
}
