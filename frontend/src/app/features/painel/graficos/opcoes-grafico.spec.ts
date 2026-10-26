import { configurarGrafico } from './opcoes-grafico';
import { DadosGrafico } from './transformar-dados';

describe('configurarGrafico (regras visuais)', () => {
  const tema = {
    texto: '#111',
    textoSecundario: '#555',
    grade: '#ddd',
    superficie: '#fff',
    corSerieUnica: '#0066b3',
    reduzirMovimento: false,
  };
  const cor = () => '#2563eb';
  const umaSerie: DadosGrafico = {
    rotulos: ['A', 'B'],
    series: [
      {
        chave: 'metrica:contagem',
        familia: 'metricas',
        rotulo: 'Contagem',
        valores: [1, 2],
        unidade: null,
        neutra: false,
      },
    ],
    tituloEixo: 'Quantidade',
  };
  const duasSeries: DadosGrafico = {
    ...umaSerie,
    series: [...umaSerie.series, { ...umaSerie.series[0], chave: 'metrica:soma:x', rotulo: 'Soma' }],
  };

  it('legenda só com mais de uma série', () => {
    expect(configurarGrafico(umaSerie, 'colunas', cor, tema).opcoes.plugins?.legend?.display).toBeFalse();
    expect(configurarGrafico(duasSeries, 'colunas', cor, tema).opcoes.plugins?.legend?.display).toBeTrue();
  });

  it('colunas e barras começam em zero; barras horizontais usam o eixo y para as categorias', () => {
    const colunas = configurarGrafico(umaSerie, 'colunas', cor, tema).opcoes.scales as Record<
      string,
      { beginAtZero?: boolean }
    >;
    expect(colunas['y'].beginAtZero).toBeTrue();
    const barras = configurarGrafico(umaSerie, 'barras-horizontais', cor, tema);
    expect(barras.opcoes.indexAxis).toBe('y');
    expect((barras.opcoes.scales as Record<string, { beginAtZero?: boolean }>)['x'].beginAtZero).toBeTrue();
  });

  it('empilhadas empilham os dois eixos; linhas viram gráfico de linha', () => {
    const empilhado = configurarGrafico(duasSeries, 'colunas-empilhadas', cor, tema).opcoes.scales as Record<
      string,
      { stacked?: boolean }
    >;
    expect(empilhado['x'].stacked && empilhado['y'].stacked).toBeTrue();
    expect(configurarGrafico(umaSerie, 'linhas', cor, tema).tipo).toBe('line');
  });

  it('as séries usam a cor da paleta; o texto da legenda, a cor de texto do tema', () => {
    const grafico = configurarGrafico(
      duasSeries,
      'colunas',
      (s) => (s.chave === 'metrica:contagem' ? '#a' : '#b'),
      tema,
    );
    expect(grafico.data.datasets.map((d) => d.backgroundColor)).toEqual(['#a', '#b']);
    expect((grafico.opcoes.plugins?.legend?.labels as { color: string }).color).toBe('#111');
  });

  it('uma série só usa a cor primária (Azul CAIXA), não a reservada pela posição; "Outros" sozinho continua neutro', () => {
    expect(configurarGrafico(umaSerie, 'colunas', cor, tema).data.datasets[0].backgroundColor).toBe('#0066b3');
    expect(configurarGrafico(umaSerie, 'linhas', cor, tema).data.datasets[0].borderColor).toBe('#0066b3');
    const soOutros: DadosGrafico = { ...umaSerie, series: [{ ...umaSerie.series[0], neutra: true }] };
    expect(configurarGrafico(soOutros, 'colunas', () => '#9ca3af', tema).data.datasets[0].backgroundColor).toBe(
      '#9ca3af',
    );
  });

  it('só valores inteiros (contagens): marcas inteiras no eixo; com decimais, livres', () => {
    const escalas = (dados: DadosGrafico) =>
      configurarGrafico(dados, 'colunas', cor, tema).opcoes.scales as Record<string, { ticks: { precision?: number } }>;
    expect(escalas(umaSerie)['y'].ticks.precision).toBe(0);
    const decimais = { ...umaSerie, series: [{ ...umaSerie.series[0], valores: [1.5, 2] }] };
    expect(escalas(decimais)['y'].ticks.precision).toBeUndefined();
  });

  it('sem animação para quem pediu menos movimento', () => {
    expect(
      configurarGrafico(umaSerie, 'colunas', cor, { ...tema, reduzirMovimento: true }).opcoes.animation,
    ).toBeFalse();
    expect(configurarGrafico(umaSerie, 'colunas', cor, tema).opcoes.animation).toBeUndefined();
  });
});
