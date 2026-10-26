import { campo, entidade } from '../../../../testes/metadados-de-teste';
import { environment } from '../../../../environments/environment';
import {
  estadoDosParametros,
  filtrosDeAprovacaoEAvaliacao,
  filtrosDoCampo,
  filtrosDosParametros,
  lerOrdenacao,
  parametrosDaApi,
  parametrosDaListagem,
  parametrosDosFiltros,
  parametrosDaListaDeFiltros,
  valoresDaListaDeFiltros,
  filtroDeEquipe,
} from './filtros-url';

describe('filtros-url (RN17 a RN19)', () => {
  const meta = entidade('relatorio_teste', [
    campo('bateria', 'TEXTO'),
    campo('contexto', 'TEXTO_LONGO'),
    campo('usuarios', 'INTEIRO'),
    campo('vazao', 'DECIMAL', { escala: 2 }),
    campo('data_execucao', 'DATA_HORA'),
    campo('dia', 'DATA'),
    campo('ativo', 'BOOLEANO'),
    campo('metodo', 'LISTA', { opcoes: ['GET'] }),
    campo('projeto', 'REFERENCIA', { entidadeReferenciada: 'projeto' }),
  ]);

  it('cada tipo gera os parâmetros que a API aceita', () => {
    expect(filtrosDoCampo(meta.campos[0]).map((f) => f.parametro)).toEqual(['bateria.contem']);
    expect(filtrosDoCampo(meta.campos[2]).map((f) => f.parametro)).toEqual(['usuarios.de', 'usuarios.ate']);
    expect(filtrosDoCampo(meta.campos[4]).map((f) => f.parametro)).toEqual(['data_execucao.de', 'data_execucao.ate']);
    expect(filtrosDoCampo(meta.campos[7]).map((f) => f.parametro)).toEqual(['metodo']);
    expect(filtrosDoCampo(meta.campos[8]).map((f) => f.parametro)).toEqual(['projeto']);
  });

  it('valores da tela → parâmetros (só os preenchidos, ponto decimal, datas ISO, referência pelo id)', () => {
    const parametros = parametrosDosFiltros(meta, {
      'bateria.contem': ' carga ',
      'usuarios.de': 100,
      'vazao.ate': 12.5,
      'data_execucao.de': new Date(2026, 9, 1),
      'dia.ate': null,
      ativo: false,
      metodo: 'GET',
      projeto: { id: 'p1', rotulo: 'Pix' },
      'contexto.contem': '',
    });
    expect(parametros).toEqual({
      'bateria.contem': 'carga',
      'usuarios.de': '100',
      'vazao.ate': '12.5',
      'data_execucao.de': '2026-10-01',
      ativo: 'false',
      metodo: 'GET',
      projeto: 'p1',
    });
  });

  it('o caminho de volta (URL → tela) devolve os mesmos valores', () => {
    const url = {
      'bateria.contem': 'carga',
      'usuarios.de': '100',
      'vazao.ate': '12.5',
      'data_execucao.de': '2026-10-01',
      ativo: 'true',
      projeto: 'p1',
      pagina: '2',
      desconhecido: 'x',
    };
    const valores = filtrosDosParametros(meta, url);
    expect(valores['usuarios.de']).toBe(100);
    expect(valores['vazao.ate']).toBe(12.5);
    expect((valores['data_execucao.de'] as Date).getDate()).toBe(1);
    expect(valores['ativo']).toBeTrue();
    expect(valores['projeto']).toEqual({ id: 'p1', rotulo: null });
    expect(valores['desconhecido']).toBeUndefined();
    // Ida e volta: os mesmos parâmetros.
    const soFiltros: Record<string, string> = { ...url };
    delete soFiltros['pagina'];
    delete soFiltros['desconhecido'];
    expect(parametrosDosFiltros(meta, valores)).toEqual(soFiltros);
  });

  it('página, tamanho e ordenação: padrões fora da URL, sempre explícitos para a API', () => {
    const estado = estadoDosParametros({ pagina: '3', ordenacao: '-vazao' });
    expect(estado).toEqual({ pagina: 3, tamanho: environment.tamanhoPagina, ordenacao: '-vazao' });
    expect(parametrosDaListagem({ metodo: 'GET' }, estado)).toEqual({
      metodo: 'GET',
      pagina: '3',
      ordenacao: '-vazao',
    });
    expect(parametrosDaListagem({}, estadoDosParametros({}))).toEqual({});
    expect(parametrosDaApi({}, estadoDosParametros({}))).toEqual({
      pagina: '1',
      tamanho: String(environment.tamanhoPagina),
      ordenacao: '-criadoEm',
    });
  });

  it('página e tamanho inválidos na URL voltam ao padrão', () => {
    expect(estadoDosParametros({ pagina: '0', tamanho: '500' })).toEqual({
      pagina: 1,
      tamanho: environment.tamanhoPagina,
      ordenacao: '-criadoEm',
    });
  });

  it('ordenação "-campo" é decrescente', () => {
    expect(lerOrdenacao('-vazao')).toEqual({ campo: 'vazao', descendente: true });
    expect(lerOrdenacao('bateria')).toEqual({ campo: 'bateria', descendente: false });
  });

  it('URL filtra por "contém", como o texto', () => {
    const url = campo('url', 'URL');
    expect(filtrosDoCampo(url).map((f) => f.parametro)).toEqual(['url.contem']);
    expect(parametrosDaListaDeFiltros(filtrosDoCampo(url), { 'url.contem': ' grafana ' })).toEqual({
      'url.contem': 'grafana',
    });
  });
});

describe('filtros-url com caminho por referência (RN89, análise do painel)', () => {
  const tipo = campo('tipo_teste', 'TEXTO', { nome: 'Tipo de teste' });
  const execucao = campo('data_execucao', 'DATA_HORA', { nome: 'Data de execução' });

  it('o parâmetro leva o caminho e o rótulo, o nome de quem aponta', () => {
    expect(filtrosDoCampo(tipo, 'relatorio_teste.', 'Relatório › ')).toEqual([
      { campo: tipo, parametro: 'relatorio_teste.tipo_teste.contem', rotulo: 'Relatório › Tipo de teste (contém)' },
    ]);
  });

  it('valores ↔ parâmetros, nos dois sentidos, para uma lista de filtros por caminho', () => {
    const filtros = [...filtrosDoCampo(tipo, 'relatorio_teste.'), ...filtrosDoCampo(execucao, 'relatorio_teste.')];
    const parametros = {
      'relatorio_teste.tipo_teste.contem': 'carga',
      'relatorio_teste.data_execucao.de': '2026-01-01',
    };
    const valores = valoresDaListaDeFiltros(filtros, parametros);
    expect(valores['relatorio_teste.data_execucao.de']).toEqual(new Date(2026, 0, 1));
    expect(parametrosDaListaDeFiltros(filtros, valores)).toEqual(parametros);
  });

  describe('filtros de aprovação e avaliação (ADR 0045)', () => {
    it('não aparecem sem aprovação nem modelo de avaliação ativos', () => {
      expect(filtrosDeAprovacaoEAvaliacao(entidade('x', [campo('nome', 'TEXTO')]))).toEqual([]);
    });

    it('situação (RN37) com rótulos em português e médias de/até por critério (RN48)', () => {
      const comRecursos = entidade('relatorio_teste', [campo('bateria', 'TEXTO')], {
        equipesComAprovacao: ['e1'],
        criteriosAvaliacao: [{ chave: 'qualidade', nome: 'Qualidade' }],
      });
      const filtros = filtrosDeAprovacaoEAvaliacao(comRecursos);
      expect(filtros.map((f) => f.parametro)).toEqual([
        'situacaoAprovacao',
        'avaliacao.qualidade.de',
        'avaliacao.qualidade.ate',
      ]);
      expect(filtros[0].opcoes?.map((o) => o.rotulo)).toEqual(['Pendente', 'Aprovado', 'Rejeitado']);
      expect(filtros[1].rotulo).toBe('Média de Qualidade: de');
      expect(
        parametrosDosFiltros(comRecursos, { situacaoAprovacao: 'PENDENTE', 'avaliacao.qualidade.de': 3.5 }),
      ).toEqual({
        situacaoAprovacao: 'PENDENTE',
        'avaliacao.qualidade.de': '3.5',
      });
    });
  });
});

describe('filtro de Equipe (RN91)', () => {
  const equipes = [
    { id: 't', nome: 'T' },
    { id: 'd', nome: 'D' },
  ];

  it('oferece só as equipes do usuário na listagem, no parâmetro equipeId da API', () => {
    const [filtro] = filtroDeEquipe(equipes);
    expect(filtro.parametro).toBe('equipeId');
    expect(filtro.rotulo).toBe('Equipe');
    expect(filtro.opcoes).toEqual([
      { rotulo: 'T', valor: 't' },
      { rotulo: 'D', valor: 'd' },
    ]);
  });

  it('vai e volta pela URL como os outros filtros', () => {
    const filtros = filtroDeEquipe(equipes);
    const valores = valoresDaListaDeFiltros(filtros, { equipeId: 'd', pagina: '2' });
    expect(valores).toEqual({ equipeId: 'd' });
    expect(parametrosDaListaDeFiltros(filtros, valores)).toEqual({ equipeId: 'd' });
  });

  it('sem equipes, nenhum filtro', () => {
    expect(filtroDeEquipe([])).toEqual([]);
  });
});
