import { ConfiguracaoWidget } from './configuracao';
import { parametrosDoWidget } from './parametros-widget';

describe('parametrosDoWidget (configuração → parâmetros da API)', () => {
  const filtros = { de: '2025-11-01', ate: '2026-10-22', equipeId: null };
  const base: ConfiguracaoWidget = {
    id: 'w',
    titulo: 'W',
    entidade: 'cenario_teste',
    agruparPor: ['relatorio_teste.ambiente'],
    metricas: ['media:vazao_media_tps'],
    campoPeriodo: 'relatorio_teste.data_execucao',
    visualizacao: 'colunas',
  };

  it('junta dimensões e métricas por vírgula e põe o período no campo declarado (também por caminho)', () => {
    expect(parametrosDoWidget(base, filtros, { escopoEquipe: true })).toEqual({
      agruparPor: 'relatorio_teste.ambiente',
      metricas: 'media:vazao_media_tps',
      'relatorio_teste.data_execucao.de': '2025-11-01',
      'relatorio_teste.data_execucao.ate': '2026-10-22',
    });
  });

  it('sem campo de período, o período filtra a data de criação (de e ate)', () => {
    const parametros = parametrosDoWidget({ ...base, campoPeriodo: null }, filtros, { escopoEquipe: true });
    expect(parametros['de']).toBe('2025-11-01');
    expect(parametros['ate']).toBe('2026-10-22');
    expect(Object.keys(parametros).some((p) => p.endsWith('.de'))).toBeFalse();
  });

  it('sem dimensão nem métrica, não manda agruparPor nem metricas (a API conta registros)', () => {
    const parametros = parametrosDoWidget({ ...base, agruparPor: [], metricas: [] }, filtros, null);
    expect(parametros['agruparPor']).toBeUndefined();
    expect(parametros['metricas']).toBeUndefined();
  });

  it('manda ordenação, limite e os filtros fixos', () => {
    const parametros = parametrosDoWidget(
      { ...base, ordenarPor: '-media:vazao_media_tps', limite: 10, filtros: { 'relatorio_teste.tipo_teste': 'Carga' } },
      filtros,
      null,
    );
    expect(parametros['ordenarPor']).toBe('-media:vazao_media_tps');
    expect(parametros['limite']).toBe('10');
    expect(parametros['relatorio_teste.tipo_teste']).toBe('Carga');
  });

  it('a equipe só vai para entidade por equipe (numa global a API recusaria)', () => {
    const comEquipe = { ...filtros, equipeId: 'e1' };
    expect(parametrosDoWidget(base, comEquipe, { escopoEquipe: true })['equipeId']).toBe('e1');
    expect(parametrosDoWidget(base, comEquipe, { escopoEquipe: false })['equipeId']).toBeUndefined();
  });

  it('o período global prevalece sobre um filtro fixo da mesma chave', () => {
    const parametros = parametrosDoWidget(
      { ...base, filtros: { 'relatorio_teste.data_execucao.de': '2020-01-01' } },
      filtros,
      null,
    );
    expect(parametros['relatorio_teste.data_execucao.de']).toBe('2025-11-01');
  });
});
