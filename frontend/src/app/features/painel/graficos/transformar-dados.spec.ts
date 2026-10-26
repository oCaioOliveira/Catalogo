import { Indicadores } from '../../../core/painel/modelo-painel';
import { formatarNumero, rotuloDoValorDimensao } from './formatacao';
import { MAXIMO_SERIES, montarDadosGrafico, montarKpis, montarTabela, semDados } from './transformar-dados';

const ambiente = { chave: 'relatorio_teste.ambiente', nome: 'Ambiente (Relatório)', tipo: 'REFERENCIA', periodo: null };
const mes = { chave: 'data_execucao:mes', nome: 'Data de execução (mês)', tipo: 'DATA_HORA', periodo: 'mes' };
const status = { chave: 'status_resultado', nome: 'Status', tipo: 'REFERENCIA', periodo: null };

describe('montarDadosGrafico (resposta → gráfico)', () => {
  it('1 dimensão de referência e várias métricas: rótulos pela ordem da API, uma série por métrica', () => {
    const resposta: Indicadores = {
      dimensoes: [ambiente],
      metricas: [
        { chave: 'media:percentil_95_ms', nome: 'Média de Percentil 95 (ms)' },
        { chave: 'media:tempo_medio_resposta_ms', nome: 'Média de Tempo médio (ms)' },
      ],
      linhas: [
        {
          'relatorio_teste.ambiente': { id: '2', rotulo: 'Produção' },
          'media:percentil_95_ms': 900,
          'media:tempo_medio_resposta_ms': 300,
        },
        {
          'relatorio_teste.ambiente': { id: '1', rotulo: 'Homologação' },
          'media:percentil_95_ms': 450.5,
          'media:tempo_medio_resposta_ms': null,
        },
      ],
      truncado: false,
    };
    const dados = montarDadosGrafico(resposta, {
      'media:percentil_95_ms': 'ms',
      'media:tempo_medio_resposta_ms': 'ms',
    });

    expect(dados.rotulos).toEqual(['Produção', 'Homologação']);
    expect(dados.series.map((s) => s.chave)).toEqual([
      'metrica:media:percentil_95_ms',
      'metrica:media:tempo_medio_resposta_ms',
    ]);
    expect(dados.series[0].valores).toEqual([900, 450.5]);
    expect(dados.series[1].valores).toEqual([300, null]);
    expect(dados.tituloEixo).toBe('ms');
  });

  it('2 dimensões: rótulos da 1ª, séries da 2ª na ordem em que aparecem, buracos nulos', () => {
    const resposta: Indicadores = {
      dimensoes: [mes, status],
      metricas: [{ chave: 'contagem', nome: 'Contagem' }],
      linhas: [
        { 'data_execucao:mes': '2026-03', status_resultado: { id: 'a', rotulo: 'Aprovado' }, contagem: 2 },
        { 'data_execucao:mes': '2026-03', status_resultado: { id: 'r', rotulo: 'Reprovado' }, contagem: 1 },
        { 'data_execucao:mes': '2026-04', status_resultado: { id: 'a', rotulo: 'Aprovado' }, contagem: 5 },
      ],
      truncado: false,
    };
    const dados = montarDadosGrafico(resposta);

    expect(dados.rotulos).toEqual(['mar/2026', 'abr/2026']);
    expect(dados.series.every((s) => s.familia === 'status_resultado')).toBeTrue();
    expect(dados.series.map((s) => [s.chave, s.valores])).toEqual([
      ['valor:Aprovado', [2, 5]],
      ['valor:Reprovado', [1, null]],
    ]);
    expect(dados.tituloEixo).toBe('Quantidade');
  });

  it('mais de 8 séries: as 7 primeiras e "Outros" (neutra) com a soma das demais', () => {
    const linhas = Array.from({ length: 10 }, (_, i) => ({
      'data_execucao:mes': '2026-03',
      status_resultado: `S${i}`,
      contagem: 1,
    }));
    const dados = montarDadosGrafico({
      dimensoes: [mes, { ...status, tipo: 'LISTA' }],
      metricas: [],
      linhas,
      truncado: false,
    });

    expect(dados.series.length).toBe(MAXIMO_SERIES);
    expect(dados.series[7]).toEqual(jasmine.objectContaining({ rotulo: 'Outros', neutra: true, valores: [3] }));
  });

  it('sem dimensão: um ponto "Total" por métrica; os KPIs com a unidade', () => {
    const resposta: Indicadores = {
      dimensoes: [],
      metricas: [{ chave: 'media:vazao_media_tps', nome: 'Média de Vazão média (TPS)' }],
      linhas: [{ contagem: 12, 'media:vazao_media_tps': 150.25 }],
      truncado: false,
    };
    expect(montarDadosGrafico(resposta).rotulos).toEqual(['Total']);
    expect(montarKpis(resposta, { 'media:vazao_media_tps': 'TPS' })).toEqual([
      { chave: 'media:vazao_media_tps', rotulo: 'Média de Vazão média (TPS)', valor: 150.25, unidade: 'TPS' },
    ]);
  });

  it('sem dados: nenhuma linha, ou a linha do total com contagem 0', () => {
    expect(semDados({ dimensoes: [ambiente], metricas: [], linhas: [], truncado: false })).toBeTrue();
    expect(semDados({ dimensoes: [], metricas: [], linhas: [{ contagem: 0 }], truncado: false })).toBeTrue();
    expect(semDados({ dimensoes: [], metricas: [], linhas: [{ contagem: 3 }], truncado: false })).toBeFalse();
  });
});

describe('montarTabela ("Ver dados")', () => {
  it('as dimensões pelo rótulo, as métricas e a contagem, com os mesmos números', () => {
    const tabela = montarTabela({
      dimensoes: [ambiente],
      metricas: [{ chave: 'media:vazao_media_tps', nome: 'Média de Vazão média (TPS)' }],
      linhas: [{ 'relatorio_teste.ambiente': null, contagem: 3, 'media:vazao_media_tps': 20 }],
      truncado: false,
    });
    expect(tabela.colunas.map((c) => c.rotulo)).toEqual([
      'Ambiente (Relatório)',
      'Média de Vazão média (TPS)',
      'Contagem',
    ]);
    expect(tabela.linhas).toEqual([['(sem valor)', 20, 3]]);
  });
});

describe('formatação pt-BR', () => {
  it('números com vírgula, milhar com ponto e a unidade', () => {
    expect(formatarNumero(1234.567, 'TPS')).toBe('1.234,57 TPS');
    expect(formatarNumero(2.5, '%')).toBe('2,5%');
    expect(formatarNumero(480, 'ms')).toBe('480 ms');
    expect(formatarNumero(null)).toBe('—');
  });

  it('valores de dimensão: períodos, referência, booleano, situação e nulo', () => {
    const dimensao = (periodo: string | null, tipo = 'DATA') => ({ chave: 'd', nome: 'd', tipo, periodo });
    expect(rotuloDoValorDimensao(dimensao('mes'), '2026-03')).toBe('mar/2026');
    expect(rotuloDoValorDimensao(dimensao('dia'), '2026-03-15')).toBe('15/03/2026');
    expect(rotuloDoValorDimensao(dimensao('trimestre'), '2026-T1')).toBe('1º tri/2026');
    expect(rotuloDoValorDimensao(dimensao(null, 'REFERENCIA'), { id: 'x', rotulo: 'Homologação' })).toBe('Homologação');
    expect(rotuloDoValorDimensao(dimensao(null, 'BOOLEANO'), true)).toBe('Sim');
    expect(rotuloDoValorDimensao(dimensao(null, 'SITUACAO_APROVACAO'), 'PENDENTE')).toBe('Pendente');
    expect(rotuloDoValorDimensao(dimensao(null), null)).toBe('(sem valor)');
  });
});
