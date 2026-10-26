import { EquipeNaListagem } from '../../../../core/administracao/estrutura-listagem';
import {
  alterouEquipes,
  equipesVisiveis,
  habilitadas,
  listaParaGravar,
  marcarVisiveis,
  ordenarEquipes,
  quantasAlteracoes,
  saindoComItens,
  textoAlteracoes,
  textoContador,
} from './equipes-listagem';

describe('equipes da listagem (RN90 a RN93)', () => {
  const equipe = (id: string, habilitada: boolean, quantidadeItens = 0, ativa = true): EquipeNaListagem => ({
    id,
    nome: id.toUpperCase(),
    ativa,
    habilitada,
    quantidadeItens,
  });
  const equipes = [equipe('t', true), equipe('d', true, 3), equipe('x', false, 0, false)];

  it('parte das habilitadas e sabe se a escolha mudou', () => {
    expect([...habilitadas(equipes)]).toEqual(['t', 'd']);
    expect(alterouEquipes(equipes, new Set(['d', 't']))).toBeFalse();
    expect(alterouEquipes(equipes, new Set(['t']))).toBeTrue();
    expect(alterouEquipes(equipes, new Set(['t', 'd', 'x']))).toBeTrue();
  });

  it('RN92: avisa só das equipes que saem E têm itens', () => {
    expect(saindoComItens(equipes, new Set(['t'])).map((e) => e.id)).toEqual(['d']);
    expect(saindoComItens(equipes, new Set(['d'])).map((e) => e.id)).toEqual([]);
  });

  it('RN93: a lista pode ficar vazia; a ordem é a da tela', () => {
    expect(listaParaGravar(equipes, new Set())).toEqual([]);
    expect(listaParaGravar(equipes, new Set(['x', 't']))).toEqual(['t', 'x']);
  });

  it('ordena as ativas primeiro, depois as inativas, cada grupo por nome', () => {
    const lista = [equipe('zeta', false, 0, true), equipe('beta', false, 0, false), equipe('alfa', false, 0, true)];
    expect(ordenarEquipes(lista).map((e) => e.id)).toEqual(['alfa', 'zeta', 'beta']);
  });

  it('conta as alterações não salvas (marcadas e desmarcadas) e monta os textos da barra', () => {
    expect(quantasAlteracoes(equipes, new Set(['t', 'd']))).toBe(0);
    expect(quantasAlteracoes(equipes, new Set(['t', 'x']))).toBe(2);
    expect(textoAlteracoes(0)).toBe('');
    expect(textoAlteracoes(1)).toBe('1 alteração não salva');
    expect(textoAlteracoes(3)).toBe('3 alterações não salvas');
    expect(textoContador(12, 40)).toBe('12 de 40 equipes habilitadas');
  });

  it('a busca ignora acentos e maiúsculas; "só habilitadas" mostra a escolha atual', () => {
    const lista = [
      { ...equipe('a', false), nome: 'Equipe Ágil' },
      { ...equipe('b', true), nome: 'Squad Beta' },
    ];
    expect(equipesVisiveis(lista, 'agil', false, new Set()).map((e) => e.id)).toEqual(['a']);
    expect(equipesVisiveis(lista, '', true, new Set(['a'])).map((e) => e.id)).toEqual(['a']);
    expect(equipesVisiveis(lista, '  ', false, new Set()).map((e) => e.id)).toEqual(['a', 'b']);
  });

  it('selecionar e limpar mexem só nas visíveis; selecionar ignora as inativas', () => {
    const visiveis = [equipe('t', false), equipe('x', false, 0, false)];
    expect([...marcarVisiveis(new Set(['d']), visiveis, true)].sort()).toEqual(['d', 't']);
    expect([...marcarVisiveis(new Set(['d', 't', 'x']), visiveis, false)]).toEqual(['d']);
  });
});
