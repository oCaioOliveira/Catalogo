import { campo, entidade } from '../../../../testes/metadados-de-teste';
import {
  Relacionado,
  abertaPorPadrao,
  colunasDaSecao,
  lerEstadoSecoes,
  linkNovo,
  linkVerTodos,
  montarSecoes,
  preenchimentoDaUrl,
  secaoAberta,
} from './relacionados';

// RN94: as seções de itens relacionados do detalhe, montadas a partir da resposta da API e dos metadados.
describe('relacionados (seções do detalhe)', () => {
  const ref = (chave: string, alvo: string, ordem: number) =>
    campo(chave, 'REFERENCIA', { entidadeReferenciada: alvo, ordem, nome: chave === 'ambiente' ? 'Ambiente' : chave });
  const relatorio = entidade(
    'relatorio_teste',
    [
      campo('bateria', 'TEXTO', { ordem: 1 }),
      ref('projeto', 'projeto', 2),
      ref('ambiente', 'ambiente', 3),
      ref('ambiente_destino', 'ambiente', 4),
      campo('contexto', 'TEXTO_LONGO', { ordem: 5 }),
      campo('tipo', 'LISTA', { ordem: 6 }),
      campo('data', 'DATA', { ordem: 7 }),
      campo('vazao', 'DECIMAL', { ordem: 8 }),
      campo('executado_por', 'TEXTO', { ordem: 9 }),
      campo('observacao', 'TEXTO', { ordem: 10 }),
    ],
    { nome: 'Relatório de Teste', nomePlural: 'Relatórios de Teste' },
  );
  const endpoint = entidade('endpoint_servico', [ref('projeto', 'projeto', 1)], { nomePlural: 'Endpoints de Serviço' });
  const cenario = entidade('cenario_teste', [ref('relatorio_teste', 'relatorio_teste', 1)], { nomePlural: 'Cenários' });
  const todas = [relatorio, endpoint, cenario];
  const rel = (entidadeChave: string, campoChave: string, quantidade: number, filha = false): Relacionado => ({
    entidade: { chave: entidadeChave, nome: entidadeChave, nomePlural: null },
    campo: { chave: campoChave, nome: campoChave },
    filha,
    quantidade,
  });

  describe('montarSecoes', () => {
    it('separa filhas (sempre aparecem) e somente leitura (só com itens), com a quantidade no título', () => {
      const secoes = montarSecoes(
        [
          rel('cenario_teste', 'relatorio_teste', 0, true),
          rel('relatorio_teste', 'projeto', 12),
          rel('endpoint_servico', 'projeto', 0),
        ],
        todas,
      );
      expect(secoes.filhas.map((s) => s.titulo)).toEqual(['Cenários (0)']);
      expect(secoes.leitura.map((s) => s.titulo)).toEqual(['Relatórios de Teste (12)']);
      expect(secoes.vazias).toEqual(['Endpoints de Serviço']);
      expect(secoes.leitura[0].id).toBe('relatorio_teste.projeto');
    });

    it('com mais de um campo da mesma entidade, o título inclui o campo', () => {
      const secoes = montarSecoes(
        [rel('relatorio_teste', 'ambiente', 5), rel('relatorio_teste', 'ambiente_destino', 0)],
        todas,
      );
      expect(secoes.leitura.map((s) => s.titulo)).toEqual(['Relatórios de Teste — Ambiente (5)']);
      expect(secoes.vazias).toEqual(['Relatórios de Teste — ambiente_destino']);
    });

    it('relação de entidade ou campo que o front não conhece (desativados) fica de fora; sem relações, nada', () => {
      expect(montarSecoes([rel('sumiu', 'projeto', 3), rel('relatorio_teste', 'campo_inativo', 1)], todas)).toEqual({
        filhas: [],
        leitura: [],
        vazias: [],
      });
    });
  });

  it('colunas: os primeiros 6 campos pela ordem, sem o que aponta para o item e sem TEXTO_LONGO', () => {
    expect(colunasDaSecao(relatorio, 'projeto').map((c) => c.chave)).toEqual([
      'bateria',
      'ambiente',
      'ambiente_destino',
      'tipo',
      'data',
      'vazao',
    ]);
  });

  it('colunas: o campo de exibição vem primeiro (a coluna principal, com o link do item)', () => {
    const porData = { ...relatorio, campoExibicao: 'data' };
    expect(colunasDaSecao(porData, 'projeto').map((c) => c.chave)).toEqual([
      'data',
      'bateria',
      'ambiente',
      'ambiente_destino',
      'tipo',
      'vazao',
    ]);
  });

  it('"Ver todos": a listagem da origem filtrada pelo campo e pelo id', () => {
    const secao = montarSecoes([rel('relatorio_teste', 'projeto', 2)], todas).leitura[0];
    expect(linkVerTodos('testes', secao, 'p1')).toEqual({
      comandos: ['/', 'testes', 'relatorio_teste'],
      parametros: { projeto: 'p1', painelFiltros: 'aberto' },
    });
  });

  describe('"Novo …" com a referência preenchida', () => {
    const secao = montarSecoes([rel('relatorio_teste', 'projeto', 2)], todas).leitura[0];

    it('o link leva o campo, o item, a equipe dele (entidade por equipe) e a volta', () => {
      expect(linkNovo('testes', secao, { id: 'p1', equipeId: 'e1' }, '/testes/projeto/p1')).toEqual({
        comandos: ['/', 'testes', 'relatorio_teste', 'novo'],
        parametros: { preencher: 'projeto', com: 'p1', equipe: 'e1', voltar: '/testes/projeto/p1' },
      });
      expect(linkNovo('testes', secao, { id: 'a1', equipeId: null }, '/x').parametros['equipe']).toBeUndefined();
    });

    it('a página de criação lê o preenchimento de volta', () => {
      expect(
        preenchimentoDaUrl({ preencher: 'projeto', com: 'p1', equipe: 'e1', voltar: '/testes/projeto/p1' }, relatorio),
      ).toEqual({ campo: 'projeto', id: 'p1', equipeId: 'e1', voltar: '/testes/projeto/p1' });
    });

    it('só um campo REFERENCIA da entidade; "voltar" só um caminho interno', () => {
      expect(preenchimentoDaUrl({ preencher: 'bateria', com: 'x' }, relatorio)).toBeNull();
      expect(preenchimentoDaUrl({ preencher: 'nao_existe', com: 'x' }, relatorio)).toBeNull();
      expect(preenchimentoDaUrl({ preencher: 'projeto' }, relatorio)).toBeNull();
      expect(
        preenchimentoDaUrl({ preencher: 'projeto', com: 'p1', voltar: 'https://fora' }, relatorio)?.voltar,
      ).toBeNull();
      expect(preenchimentoDaUrl({ preencher: 'projeto', com: 'p1', voltar: '//fora' }, relatorio)?.voltar).toBeNull();
    });
  });

  describe('seções recolhíveis', () => {
    it('até 3 seções, todas abertas; acima disso, só a primeira', () => {
      expect([0, 1, 2].map((i) => abertaPorPadrao(i, 3))).toEqual([true, true, true]);
      expect([0, 1, 2, 3].map((i) => abertaPorPadrao(i, 4))).toEqual([true, false, false, false]);
    });

    it('o estado guardado vale mais que o padrão; guardado inválido é ignorado', () => {
      expect(secaoAberta({ a: false }, 'a', 0, 1)).toBeFalse();
      expect(secaoAberta({ b: true }, 'b', 3, 5)).toBeTrue();
      expect(secaoAberta({}, 'c', 3, 5)).toBeFalse();
      expect(lerEstadoSecoes('{"a":false,"b":"x"}')).toEqual({ a: false });
      expect(lerEstadoSecoes('não é json')).toEqual({});
      expect(lerEstadoSecoes(null)).toEqual({});
      expect(lerEstadoSecoes('[1]')).toEqual({});
    });
  });
});
