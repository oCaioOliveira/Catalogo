import {
  ColunaTabela,
  LARGURA_ACOES,
  alternarColuna,
  cliqueAbreLinha,
  colunasQueCabem,
  colunasVisiveis,
  estadoRolagem,
  larguraDoTipo,
  moverColuna,
  ordemDoSeletor,
  textoColunasVisiveis,
} from './colunas-tabela';

describe('colunas das tabelas', () => {
  // O relatório de teste: o campo de exibição (bateria) e as demais colunas, com as larguras por tipo.
  const relatorio: ColunaTabela[] = [
    { chave: 'bateria', nome: 'Bateria', largura: larguraDoTipo('TEXTO'), fixa: true },
    { chave: 'projeto', nome: 'Projeto', largura: larguraDoTipo('REFERENCIA') },
    { chave: 'rtc', nome: 'RTC', largura: larguraDoTipo('REFERENCIA') },
    { chave: 'ambiente', nome: 'Ambiente', largura: larguraDoTipo('REFERENCIA') },
    { chave: 'status', nome: 'Status', largura: larguraDoTipo('REFERENCIA') },
    { chave: 'data', nome: 'Data de execução', largura: larguraDoTipo('DATA_HORA') },
    { chave: 'tipo', nome: 'Tipo de teste', largura: larguraDoTipo('TEXTO') },
    { chave: 'executado', nome: 'Executado por', largura: larguraDoTipo('TEXTO') },
    { chave: 'criadoEm', nome: 'Criado em', largura: 150 },
  ];

  it('datas e números são mais estreitos que textos', () => {
    expect(larguraDoTipo('DATA')).toBeLessThan(larguraDoTipo('TEXTO'));
    expect(larguraDoTipo('INTEIRO')).toBeLessThan(larguraDoTipo('REFERENCIA'));
    expect(larguraDoTipo('URL')).toBeGreaterThan(larguraDoTipo('TEXTO'));
  });

  it('em 1366 px (área útil de uns 1054 px com o menu aberto), pelo menos 6 colunas, a fixa primeiro', () => {
    const visiveis = colunasQueCabem(relatorio, 1054);

    expect(visiveis.length).toBeGreaterThanOrEqual(6);
    expect(visiveis[0]).toBe('bateria');
    expect(visiveis).not.toContain('executado');
  });

  it('numa tela larga, cabem mais; nunca menos que o mínimo numa estreita', () => {
    expect(colunasQueCabem(relatorio, 1920 - 312).length).toBeGreaterThan(colunasQueCabem(relatorio, 1054).length);
    expect(colunasQueCabem(relatorio, 500).length).toBe(6);
    expect(colunasQueCabem(relatorio.slice(0, 3), 300)).toEqual(['bateria', 'projeto', 'rtc']);
  });

  it('a soma das larguras escolhidas cabe na área quando passa do mínimo', () => {
    const visiveis = colunasQueCabem(relatorio, 1600);
    const soma = relatorio.filter((c) => visiveis.includes(c.chave)).reduce((t, c) => t + c.largura, LARGURA_ACOES);
    expect(soma).toBeLessThanOrEqual(1600);
  });

  it('a ordem da pessoa vale, com a fixa sempre primeiro; chaves que sumiram são ignoradas', () => {
    const visiveis = colunasVisiveis(relatorio, ['status', 'sumiu', 'projeto', 'bateria']);
    expect(visiveis.map((c) => c.chave)).toEqual(['bateria', 'status', 'projeto']);
  });

  it('no seletor, as visíveis primeiro (na ordem delas) e depois as escondidas', () => {
    const ordem = ordemDoSeletor(relatorio.slice(0, 4), ['ambiente', 'projeto']).map((c) => c.chave);
    expect(ordem).toEqual(['bateria', 'ambiente', 'projeto', 'rtc']);
  });

  it('texto do seletor', () => {
    expect(textoColunasVisiveis(6, 9)).toBe('6 de 9 colunas');
    expect(textoColunasVisiveis(1, 1)).toBe('1 de 1 coluna');
  });

  it('mover e alternar colunas', () => {
    expect(moverColuna(['a', 'b', 'c'], 'b', -1)).toEqual(['b', 'a', 'c']);
    expect(moverColuna(['a', 'b', 'c'], 'c', 1)).toEqual(['a', 'b', 'c']);
    expect(alternarColuna(['a', 'b'], 'c', true)).toEqual(['a', 'b', 'c']);
    expect(alternarColuna(['a', 'b', 'c'], 'b', false)).toEqual(['a', 'c']);
  });

  it('indicação de rolagem: sombra de cada lado só enquanto há o que rolar', () => {
    expect(estadoRolagem(0, 800, 800)).toEqual({ esquerda: false, direita: false });
    expect(estadoRolagem(0, 800, 1200)).toEqual({ esquerda: false, direita: true });
    expect(estadoRolagem(200, 800, 1200)).toEqual({ esquerda: true, direita: true });
    expect(estadoRolagem(400, 800, 1200)).toEqual({ esquerda: true, direita: false });
    expect(estadoRolagem(399.5, 800, 1200)).toEqual({ esquerda: true, direita: false });
  });

  it('clique na linha: abre, menos em links, botões e com texto selecionado', () => {
    const celula = document.createElement('td');
    const link = document.createElement('a');
    const icone = document.createElement('i');
    link.appendChild(icone);
    celula.appendChild(link);

    expect(cliqueAbreLinha(celula, '')).toBeTrue();
    expect(cliqueAbreLinha(icone, '')).toBeFalse();
    expect(cliqueAbreLinha(celula, 'texto copiado')).toBeFalse();
  });
});
