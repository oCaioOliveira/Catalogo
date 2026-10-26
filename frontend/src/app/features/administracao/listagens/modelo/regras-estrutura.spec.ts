import { campoE, entidadeE, estruturaDesempenho } from './estrutura-de-teste';
import {
  camposParaExibicao,
  camposParaUnicidade,
  entidadeParaPreVisualizacao,
  entidadesReferenciaveis,
  moverItem,
  nomeDeUnicidade,
  paiDe,
  usosDoCampo,
} from './regras-estrutura';

describe('regras da estrutura (administração de listagens)', () => {
  const estrutura = estruturaDesempenho();
  const porChave = (chave: string) => estrutura.entidades.find((e) => e.chave === chave)!;

  it('filha: REFERENCIA obrigatória com CASCATA', () => {
    expect(paiDe(porChave('cenario'))).toBe('relatorio');
    expect(paiDe(porChave('endpoint'))).toBeNull();
  });

  it('usos do campo: exibição, unicidade e dependências (dele e de outros campos)', () => {
    const projeto = { ...porChave('projeto'), restricoes: [{ nome: 'uq_projeto_nome', campos: ['nome'] }] };
    expect(usosDoCampo(projeto, projeto.campos[0], estrutura)).toEqual(['Exibição', 'Unicidade (simples)']);
    const cenario = porChave('cenario');
    expect(usosDoCampo(cenario, cenario.campos[1], estrutura)).toEqual(['Dependência (filtra as opções)']);
    expect(usosDoCampo(cenario, cenario.campos[0], estrutura)).toEqual(['Dependência de "Endpoint"']);
    const relatorio = porChave('relatorio');
    expect(usosDoCampo(relatorio, relatorio.campos[0], estrutura)).toEqual(['Dependência de "Endpoint"']);
    const endpoint = porChave('endpoint');
    expect(usosDoCampo(endpoint, endpoint.campos[0], estrutura)).toEqual(['Dependência de "Endpoint"']);
    expect(usosDoCampo(cenario, cenario.campos[2], estrutura)).toEqual([]);
  });

  it('exibição: só campos ativos, nem REFERENCIA nem TEXTO_LONGO; unicidade: sem TEXTO_LONGO', () => {
    const e = entidadeE('x', [
      campoE('nome', 'TEXTO'),
      campoE('ref', 'REFERENCIA'),
      campoE('obs', 'TEXTO_LONGO'),
      campoE('url', 'URL'),
      campoE('antigo', 'TEXTO', { ativo: false }),
    ]);
    expect(camposParaExibicao(e).map((c) => c.chave)).toEqual(['nome', 'url']);
    expect(camposParaUnicidade(e).map((c) => c.chave)).toEqual(['nome', 'ref', 'url']);
    expect(nomeDeUnicidade('equipamento', ['nome'])).toBe('uq_equipamento_nome');
  });

  it('referências: tipo global só aponta para globais (RN54); por equipe, para todos os ativos', () => {
    expect(entidadesReferenciaveis(estrutura, porChave('ambiente')).map((e) => e.chave)).toEqual(['ambiente']);
    expect(entidadesReferenciaveis(estrutura, porChave('relatorio')).length).toBe(5);
  });

  it('pré-visualização: só campos ativos, com o dependeDe da RN21', () => {
    const cenario = entidadeParaPreVisualizacao(porChave('cenario'));
    expect(cenario.campos.find((c) => c.chave === 'endpoint')?.dependeDe).toBe('relatorio');
    const comInativo = entidadeE('x', [campoE('a', 'TEXTO'), campoE('b', 'TEXTO', { ativo: false })]);
    expect(entidadeParaPreVisualizacao(comInativo).campos.map((c) => c.chave)).toEqual(['a']);
  });

  it('arrastar e soltar muda só a posição do item', () => {
    expect(moverItem(['a', 'b', 'c', 'd'], 3, 0)).toEqual(['d', 'a', 'b', 'c']);
    expect(moverItem(['a', 'b', 'c'], 0, 2)).toEqual(['b', 'c', 'a']);
  });
});
