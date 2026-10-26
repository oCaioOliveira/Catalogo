import { entidadeDesempenho, entidadesDesempenho } from '../../../../testes/metadados-desempenho';
import {
  TIPOS_DIMENSAO,
  TIPOS_NUMERICOS,
  camposAlcancaveis,
  ehErro,
  resolverCaminho,
  unidadeDoCampo,
} from './caminhos';
import { opcoesDeDimensao, opcoesDeMetrica, opcoesDePeriodo, normalizarAnalise } from './opcoes-analise';

describe('caminhos por referência (RN89)', () => {
  const entidades = entidadesDesempenho();
  const cenario = entidadeDesempenho('cenario_teste');

  it('resolve 1 e 2 saltos, com rótulo legível', () => {
    const umSalto = resolverCaminho(entidades, cenario, 'relatorio_teste.ambiente');
    expect(ehErro(umSalto) ? umSalto.erro : umSalto.rotulo).toBe('Relatório › Ambiente');

    const doisSaltos = resolverCaminho(entidades, cenario, 'endpoint_servico.projeto.nome');
    expect(ehErro(doisSaltos) ? doisSaltos.erro : doisSaltos.rotulo).toBe('Endpoint › Projeto › Nome');
  });

  it('recusa mais de 2 saltos, campo desconhecido e salto que não é referência', () => {
    const erros = ['relatorio_teste.projeto.nome.x', 'relatorio_teste.nao_existe', 'numero_cenario.nome'].map((c) =>
      resolverCaminho(entidades, cenario, c),
    );
    expect(erros.every(ehErro)).toBeTrue();
  });

  it('campos alcançáveis: os da entidade e os de um salto, filtrados por tipo', () => {
    const dimensoes = camposAlcancaveis(entidades, cenario, TIPOS_DIMENSAO).map((c) => c.caminho);
    expect(dimensoes).toContain('relatorio_teste');
    expect(dimensoes).toContain('relatorio_teste.ambiente');
    expect(dimensoes).toContain('relatorio_teste.data_execucao');
    expect(dimensoes).not.toContain('relatorio_teste.conclusao'); // TEXTO_LONGO não agrupa
    expect(dimensoes).not.toContain('vazao_media_tps');

    const numericos = camposAlcancaveis(entidades, cenario, TIPOS_NUMERICOS).map((c) => c.caminho);
    expect(numericos).toEqual([
      'numero_cenario',
      'usuarios_virtuais',
      'tempo_medio_resposta_ms',
      'percentil_95_ms',
      'vazao_media_tps',
      'taxa_erro_percentual',
    ]);
  });

  it('a unidade é o texto curto entre parênteses no fim do nome do campo', () => {
    const campo = (nome: string) => ({ ...cenario.campos[0], nome });
    expect(unidadeDoCampo(campo('Vazão média (TPS)'))).toBe('TPS');
    expect(unidadeDoCampo(campo('Taxa de erro (%)'))).toBe('%');
    expect(unidadeDoCampo(campo('Usuários virtuais'))).toBeNull();
  });
});

describe('opções da análise personalizada', () => {
  const entidades = entidadesDesempenho();
  const cenario = entidadeDesempenho('cenario_teste');

  it('dimensões com rótulos "Relatório › Ambiente", marcando as de data, e as de sistema', () => {
    const opcoes = opcoesDeDimensao(entidades, cenario);
    expect(opcoes).toContain({ valor: 'relatorio_teste.ambiente', rotulo: 'Relatório › Ambiente', data: false });
    expect(opcoes).toContain({
      valor: 'relatorio_teste.data_execucao',
      rotulo: 'Relatório › Data de execução',
      data: true,
    });
    expect(opcoes.map((o) => o.valor)).toContain('equipe');
    expect(opcoes.map((o) => o.valor)).toContain('criadoEm');
  });

  it('métricas: contagem e as 4 funções de cada campo numérico', () => {
    const opcoes = opcoesDeMetrica(entidades, cenario);
    expect(opcoes[0].valor).toBe('contagem');
    expect(opcoes).toContain({ valor: 'media:vazao_media_tps', rotulo: 'Média de Vazão média (TPS)' });
    expect(opcoes.length).toBe(1 + 6 * 4);
  });

  it('campo do período: a data de criação ou uma data (também por caminho)', () => {
    expect(opcoesDePeriodo(entidades, cenario).map((o) => o.valor)).toEqual([null, 'relatorio_teste.data_execucao']);
  });

  it('normaliza: visualização que deixou de servir e ordenação de dimensão removida', () => {
    const normalizada = normalizarAnalise({
      id: 'analise',
      titulo: '',
      entidade: 'cenario_teste',
      agruparPor: [],
      metricas: ['media:vazao_media_tps'],
      campoPeriodo: null,
      visualizacao: 'colunas',
      ordenarPor: '-relatorio_teste.ambiente',
    });
    expect(normalizada.visualizacao).toBe('kpi');
    expect(normalizada.ordenarPor).toBeUndefined();
  });
});
