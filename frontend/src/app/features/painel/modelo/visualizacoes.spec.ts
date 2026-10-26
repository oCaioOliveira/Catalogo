import { ehDimensaoDeData, visualizacoesPermitidas } from './visualizacoes';

describe('visualizacoesPermitidas (tipos por combinação)', () => {
  it('sem dimensão: KPI ou tabela', () => {
    expect(visualizacoesPermitidas([], ['media:vazao'])).toEqual(['kpi', 'tabela']);
  });

  it('1 dimensão que não é data: colunas, barras horizontais ou tabela (sem linhas)', () => {
    expect(visualizacoesPermitidas(['tipo_teste'], [])).toEqual(['colunas', 'barras-horizontais', 'tabela']);
  });

  it('1 dimensão de data: também linhas', () => {
    expect(visualizacoesPermitidas(['relatorio_teste.data_execucao:mes'], ['media:x', 'media:y'])).toContain('linhas');
  });

  it('2 dimensões e contagem: colunas empilhadas (e linhas se a 1ª é data)', () => {
    expect(visualizacoesPermitidas(['data_execucao:mes', 'status_resultado'], ['contagem'])).toEqual([
      'colunas-empilhadas',
      'linhas',
      'tabela',
    ]);
    expect(visualizacoesPermitidas(['ambiente', 'status_resultado'], [])).toEqual(['colunas-empilhadas', 'tabela']);
  });

  it('2 dimensões com média: não empilha (média não se soma)', () => {
    expect(visualizacoesPermitidas(['ambiente', 'status_resultado'], ['media:vazao'])).toEqual(['tabela']);
    expect(visualizacoesPermitidas(['ambiente', 'status_resultado'], ['soma:vazao'])).toContain('colunas-empilhadas');
  });

  it('2 dimensões e mais de uma métrica: só tabela', () => {
    expect(visualizacoesPermitidas(['a', 'b'], ['contagem', 'soma:x'])).toEqual(['tabela']);
  });

  it('dimensão de data = tem período', () => {
    expect(ehDimensaoDeData('criadoEm:ano')).toBeTrue();
    expect(ehDimensaoDeData('relatorio_teste.data_execucao:trimestre')).toBeTrue();
    expect(ehDimensaoDeData('relatorio_teste.ambiente')).toBeFalse();
  });
});
