import { entidadesDesempenho } from '../../../../testes/metadados-desempenho';
import { PAINEL_TESTES_DESEMPENHO } from '../configuracao/testes-desempenho.painel';
import { ConfiguracaoWidget } from './configuracao';
import { validarWidget } from './validacao-widget';

describe('validarWidget (configuração × metadados)', () => {
  const entidades = entidadesDesempenho();
  const valido: ConfiguracaoWidget = {
    id: 'w',
    titulo: 'W',
    entidade: 'cenario_teste',
    agruparPor: ['relatorio_teste.ambiente'],
    metricas: ['media:vazao_media_tps'],
    campoPeriodo: 'relatorio_teste.data_execucao',
    visualizacao: 'colunas',
  };

  it('os 7 widgets de Testes de Desempenho são válidos', () => {
    for (const widget of PAINEL_TESTES_DESEMPENHO.widgets) {
      expect(validarWidget(widget, entidades)).withContext(widget.id).toEqual([]);
    }
  });

  it('entidade inexistente', () => {
    expect(validarWidget({ ...valido, entidade: 'nao_existe' }, entidades)[0]).toContain("Entidade 'nao_existe'");
  });

  it('campo inexistente (renomeado ou desativado) na dimensão, na métrica e no filtro', () => {
    const erros = validarWidget(
      {
        ...valido,
        agruparPor: ['relatorio_teste.nao_existe'],
        metricas: ['media:vazao_antiga'],
        filtros: { 'relatorio_teste.tipo_antigo': 'Carga' },
        visualizacao: 'colunas',
      },
      entidades,
    );
    expect(erros.length).toBe(3);
    expect(erros.join(' ')).toContain('nao_existe');
    expect(erros.join(' ')).toContain('vazao_antiga');
    expect(erros.join(' ')).toContain('tipo_antigo');
  });

  it('tipos errados: média de texto, data sem período, campo de período que não é data', () => {
    const erros = validarWidget(
      {
        ...valido,
        agruparPor: ['relatorio_teste.data_execucao'],
        metricas: ['media:relatorio_teste.tipo_teste'],
        campoPeriodo: 'relatorio_teste.tipo_teste',
      },
      entidades,
    );
    expect(erros.some((e) => e.includes('informe o período'))).toBeTrue();
    expect(erros.some((e) => e.includes('só INTEIRO e DECIMAL'))).toBeTrue();
    expect(erros.some((e) => e.includes('precisa ser DATA'))).toBeTrue();
  });

  it('filtro com operador incompatível com o tipo', () => {
    expect(validarWidget({ ...valido, filtros: { 'relatorio_teste.tipo_teste.de': 'x' } }, entidades)[0]).toContain(
      "operador 'de'",
    );
    expect(
      validarWidget({ ...valido, filtros: { 'relatorio_teste.data_execucao.de': '2026-01-01' } }, entidades),
    ).toEqual([]);
  });

  it('visualização que não serve, ordenação fora do widget e limite fora da faixa', () => {
    const erros = validarWidget({ ...valido, visualizacao: 'kpi', ordenarPor: '-x', limite: 0 }, entidades);
    expect(erros.length).toBe(3);
  });
});
