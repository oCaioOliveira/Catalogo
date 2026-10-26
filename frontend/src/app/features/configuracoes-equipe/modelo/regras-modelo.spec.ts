import { chaveDoCriterio, corpoDoModelo, novoCriterio, separarErros, travasDoCriterio } from './regras-modelo';

describe('editor do modelo de avaliação (RN42, RN43)', () => {
  it('a chave vem do nome, sem repetir', () => {
    expect(chaveDoCriterio('Qualidade técnica', [])).toBe('qualidade_tecnica');
    expect(chaveDoCriterio('Qualidade', ['qualidade'])).toBe('qualidade_2');
    expect(chaveDoCriterio('Qualidade', ['qualidade', 'qualidade_2'])).toBe('qualidade_3');
    expect(chaveDoCriterio('!!!', [])).toBe('criterio');
  });

  it('critério novo: tudo editável; salvo: chave travada; com respostas: não exclui nem muda o tipo', () => {
    const novo = novoCriterio(1);
    expect(travasDoCriterio(novo)).toEqual({
      podeExcluir: true,
      podeMudarTipo: true,
      podeMudarChave: true,
      explicacao: null,
    });
    const salvo = { ...novo, salvo: true };
    expect(travasDoCriterio(salvo).podeMudarChave).toBeFalse();
    expect(travasDoCriterio(salvo).podeExcluir).toBeTrue();
    const comRespostas = { ...salvo, possuiRespostas: true };
    const travas = travasDoCriterio(comRespostas);
    expect(travas.podeExcluir).toBeFalse();
    expect(travas.podeMudarTipo).toBeFalse();
    expect(travas.explicacao).toContain('desative');
  });

  it('o corpo leva a lista inteira, na ordem da tela, sem comentário nos de texto', () => {
    const a = { ...novoCriterio(9), chave: 'qualidade', nome: ' Qualidade ', comentario: 'OBRIGATORIO' as const };
    const b = {
      ...novoCriterio(1),
      chave: 'observacoes',
      nome: 'Observações',
      tipo: 'TEXTO' as const,
      comentario: 'OPCIONAL' as const,
    };
    const corpo = corpoDoModelo(' Avaliação ', '', [a, b], 'v1');
    expect(corpo.nome).toBe('Avaliação');
    expect(corpo.descricao).toBeNull();
    expect(corpo.versao).toBe('v1');
    expect(corpo.criterios?.map((c) => [c.chave, c.ordem, c.comentario])).toEqual([
      ['qualidade', 1, 'OBRIGATORIO'],
      ['observacoes', 2, 'NENHUM'],
    ]);
    expect(corpo.criterios?.[0].nome).toBe('Qualidade');
  });

  it('os erros da API vão para o modelo ou para o critério da posição', () => {
    expect(
      separarErros({
        nome: ['Obrigatório.'],
        'criterios[1].tipo': ['Tem respostas.'],
        'criterios[1].chave': ['Repetida.'],
      }),
    ).toEqual({ modelo: { nome: ['Obrigatório.'] }, criterios: { 1: ['Tem respostas.', 'Repetida.'] } });
  });
});
