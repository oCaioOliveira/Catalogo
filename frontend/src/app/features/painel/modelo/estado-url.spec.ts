import { ConfiguracaoWidget } from './configuracao';
import {
  analiseDaUrl,
  configuracaoParaCopiar,
  estadoPainelDaUrl,
  opcoesDeEquipe,
  parametrosDaAnalise,
  parametrosDoEstadoPainel,
} from './estado-url';

describe('estado do painel na URL (ida e volta)', () => {
  const hoje = new Date(2026, 9, 22);

  it('sem parâmetros: últimos 12 meses, todas as equipes, e a URL fica vazia', () => {
    const estado = estadoPainelDaUrl({}, hoje);
    expect(estado).toEqual({ atalho: '12m', de: '2025-11-01', ate: '2026-10-22', equipeId: null });
    expect(parametrosDoEstadoPainel(estado)).toEqual({});
  });

  it('atalho e equipe vão e voltam', () => {
    const estado = estadoPainelDaUrl({ periodo: '3m', equipeId: 'e1' }, hoje);
    expect(estado.de).toBe('2026-08-01');
    expect(parametrosDoEstadoPainel(estado)).toEqual({ periodo: '3m', equipeId: 'e1' });
  });

  it('personalizado guarda as datas; datas inválidas ou invertidas voltam ao padrão', () => {
    const parametros = { periodo: 'personalizado', de: '2026-01-01', ate: '2026-03-31' };
    expect(parametrosDoEstadoPainel(estadoPainelDaUrl(parametros, hoje))).toEqual(parametros);
    expect(estadoPainelDaUrl({ periodo: 'personalizado', de: '2026-03-31', ate: '2026-01-01' }, hoje).atalho).toBe(
      '12m',
    );
    expect(estadoPainelDaUrl({ periodo: 'semana' }, hoje).atalho).toBe('12m');
  });
});

describe('estado da análise na URL (ida e volta)', () => {
  const hoje = new Date(2026, 9, 22);
  const configuracao: ConfiguracaoWidget = {
    id: 'analise',
    titulo: 'Análise personalizada',
    entidade: 'cenario_teste',
    agruparPor: ['relatorio_teste.ambiente'],
    metricas: ['media:vazao_media_tps', 'contagem'],
    filtros: { 'relatorio_teste.tipo_teste': 'Carga', 'relatorio_teste.data_execucao.de': '2026-01-01' },
    campoPeriodo: 'relatorio_teste.data_execucao',
    visualizacao: 'barras-horizontais',
    ordenarPor: '-media:vazao_media_tps',
    limite: 20,
  };
  const painel = { atalho: '30d' as const, de: '2026-09-23', ate: '2026-10-22', equipeId: 'e1' };

  it('a configuração inteira (e os filtros globais) vai para a URL e volta igual', () => {
    const parametros = parametrosDaAnalise({ configuracao, painel });
    expect(parametros['f.relatorio_teste.tipo_teste']).toBe('Carga');
    expect(parametros['agruparPor']).toBe('relatorio_teste.ambiente');
    expect(analiseDaUrl(parametros, hoje)).toEqual({ configuracao, painel });
  });

  it('"Abrir na análise" de um widget leva o título; a visualização inválida vira a primeira permitida', () => {
    const doWidget = { ...configuracao, id: 'vazao', titulo: 'Vazão média (TPS) por ambiente' };
    const parametros = parametrosDaAnalise({ configuracao: doWidget, painel });
    expect(parametros['titulo']).toBe('Vazão média (TPS) por ambiente');
    expect(analiseDaUrl({ ...parametros, visualizacao: 'kpi' }, hoje).configuracao.visualizacao).toBe('colunas');
  });

  it('"Copiar configuração" sugere um id a partir da entidade e das dimensões', () => {
    expect(configuracaoParaCopiar(configuracao).id).toBe('cenario-teste-relatorio-teste-ambiente');
  });
});

describe('opcoesDeEquipe (filtro de equipe)', () => {
  const equipes = [
    { id: 'b', nome: 'Beta' },
    { id: 'a', nome: 'Alfa' },
  ];

  it('ADMINISTRADOR: "Todas" e cada equipe (em ordem alfabética)', () => {
    expect(opcoesDeEquipe('ADMINISTRADOR', equipes)).toEqual([
      { valor: null, rotulo: 'Todas' },
      { valor: 'a', rotulo: 'Alfa' },
      { valor: 'b', rotulo: 'Beta' },
    ]);
  });

  it('GESTOR com mais de uma equipe na listagem: "Minhas equipes" e as dele; com uma só, sem filtro', () => {
    expect(opcoesDeEquipe('GESTOR', equipes)?.[0]).toEqual({ valor: null, rotulo: 'Minhas equipes' });
    expect(opcoesDeEquipe('GESTOR', [equipes[0]])).toBeNull();
  });

  it('RN91: só as equipes do usuário NA listagem (um GESTOR de T e D, numa listagem só de D, não vê o filtro)', () => {
    expect(opcoesDeEquipe('GESTOR', [{ id: 'd', nome: 'D' }])).toBeNull();
    expect(opcoesDeEquipe('ADMINISTRADOR', [{ id: 'd', nome: 'D' }])).toEqual([
      { valor: null, rotulo: 'Todas' },
      { valor: 'd', rotulo: 'D' },
    ]);
  });
});
