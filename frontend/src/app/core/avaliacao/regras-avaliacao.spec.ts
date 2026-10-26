import { CriterioAvaliacao } from './modelo-avaliacao';
import {
  mediaEmTexto,
  respostasDaApi,
  respostasParaApi,
  resumoDoCriterio,
  rotuloEstrelas,
  validarRespostas,
} from './regras-avaliacao';

function criterio(chave: string, extras: Partial<CriterioAvaliacao> = {}): CriterioAvaliacao {
  return {
    id: null,
    chave,
    nome: chave,
    descricao: null,
    tipo: 'ESTRELAS',
    obrigatorio: false,
    comentario: 'NENHUM',
    tamanhoMaximoTexto: 2000,
    ordem: 0,
    ativo: true,
    possuiRespostas: false,
    ...extras,
  };
}

describe('regras de avaliação na tela (RN42 a RN48)', () => {
  const qualidade = criterio('qualidade', { obrigatorio: true, comentario: 'OBRIGATORIO' });
  const clareza = criterio('clareza', { comentario: 'OPCIONAL' });
  const observacoes = criterio('observacoes', { tipo: 'TEXTO', tamanhoMaximoTexto: 10 });

  it('o resumo vira média em texto e distribuição de 5 a 1, com percentuais', () => {
    const resumo = resumoDoCriterio({
      chave: 'qualidade',
      nome: 'Qualidade',
      media: 4.5,
      quantidade: 2,
      distribuicao: { '4': 1, '5': 1 },
    });
    expect(resumo.mediaTexto).toBe('4,5');
    expect(resumo.distribuicao).toEqual([
      { estrelas: 5, quantidade: 1, percentual: 50 },
      { estrelas: 4, quantidade: 1, percentual: 50 },
      { estrelas: 3, quantidade: 0, percentual: 0 },
      { estrelas: 2, quantidade: 0, percentual: 0 },
      { estrelas: 1, quantidade: 0, percentual: 0 },
    ]);
    expect(resumoDoCriterio({ chave: 'x', quantidade: 0 }).distribuicao.every((b) => b.percentual === 0)).toBeTrue();
    expect(mediaEmTexto(null)).toBe('—');
    expect(mediaEmTexto(4)).toBe('4,0');
  });

  it('rótulo acessível das estrelas', () => {
    expect(rotuloEstrelas(4)).toBe('4 de 5 estrelas');
    expect(rotuloEstrelas(1)).toBe('1 de 5 estrelas');
    expect(rotuloEstrelas(null)).toBe('sem nota');
  });

  it('formulário → API: sem resposta não vai; comentário só onde é aceito; texto sem espaços', () => {
    const corpo = respostasParaApi([qualidade, clareza, observacoes, criterio('sem', { comentario: 'NENHUM' })], {
      qualidade: { estrelas: 4, comentario: '  bom  ', texto: '' },
      clareza: { estrelas: null, comentario: 'ignorado', texto: '' },
      observacoes: { estrelas: null, comentario: '', texto: '  ok  ' },
      sem: { estrelas: 3, comentario: 'não vai', texto: '' },
    });
    expect(corpo).toEqual({
      qualidade: { estrelas: 4, comentario: 'bom' },
      observacoes: { texto: 'ok' },
      sem: { estrelas: 3 },
    });
  });

  it('API → formulário, e de volta', () => {
    const valores = respostasDaApi([qualidade, observacoes], { qualidade: { estrelas: 5, comentario: 'ótimo' } });
    expect(valores['qualidade']).toEqual({ estrelas: 5, comentario: 'ótimo', texto: '' });
    expect(valores['observacoes']).toEqual({ estrelas: null, comentario: '', texto: '' });
  });

  it('validação com as mesmas chaves da API: obrigatório, comentário obrigatório e tamanho', () => {
    expect(validarRespostas([qualidade], { qualidade: { estrelas: null, comentario: '', texto: '' } })).toEqual({
      'respostas.qualidade': ['Dê uma nota de 1 a 5 estrelas.'],
    });
    expect(validarRespostas([qualidade], { qualidade: { estrelas: 4, comentario: '   ', texto: '' } })).toEqual({
      'respostas.qualidade.comentario': ['O comentário é obrigatório neste critério.'],
    });
    expect(
      validarRespostas([observacoes], { observacoes: { estrelas: null, comentario: '', texto: 'onze letras' } }),
    ).toEqual({
      'respostas.observacoes.texto': ['No máximo 10 caracteres (tem 11).'],
    });
    expect(validarRespostas([qualidade, clareza], { qualidade: { estrelas: 5, comentario: 'ok', texto: '' } })).toEqual(
      {},
    );
    // Critério desativado não é validado.
    expect(validarRespostas([{ ...qualidade, ativo: false }], {})).toEqual({});
  });
});
