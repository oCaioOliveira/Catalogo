import { Problema } from '../erros/problema';
import {
  Listagem,
  VALIDADE_METADADOS_MS,
  cacheValido,
  escolherListagem,
  metadadosDesatualizados,
  mostraEquipe,
  nomeDaEquipe,
} from './listagens';

describe('listagens (navegação e validade dos metadados)', () => {
  const alfa = { id: 'a', nome: 'Equipe Alfa' };
  const beta = { id: 'b', nome: 'Equipe Beta' };
  const listagem = (chave: string, nome: string, ativo: boolean, minhasEquipes = [alfa]): Listagem => ({
    chave,
    nome,
    ativo,
    minhasEquipes,
    possuiEquipes: minhasEquipes.length > 0,
  });
  const testes = listagem('testes_desempenho', 'Testes de Desempenho', true);
  const inventario = listagem('inventario', 'Inventário', true);
  const antiga = listagem('antiga', 'Antiga', false);

  it('o cache vale por 5 minutos', () => {
    expect(VALIDADE_METADADOS_MS).toBe(300_000);
    expect(cacheValido(1_000, 1_000 + 299_999)).toBeTrue();
    expect(cacheValido(1_000, 1_000 + 300_000)).toBeFalse();
    expect(cacheValido(0, 10, 5)).toBeFalse();
  });

  it('abre a última listagem usada, se ainda estiver ativa', () => {
    expect(escolherListagem([inventario, testes], 'testes_desempenho')).toBe('testes_desempenho');
  });

  it('sem última (ou inativa, ou que não existe mais), a primeira ativa', () => {
    expect(escolherListagem([antiga, inventario, testes], null)).toBe('inventario');
    expect(escolherListagem([antiga, inventario, testes], 'antiga')).toBe('inventario');
    expect(escolherListagem([inventario], 'sumiu')).toBe('inventario');
    expect(escolherListagem([antiga], null)).toBeNull();
  });

  it('RN90: a última usada que deixou de ser visível (a API não a devolve mais) dá lugar à primeira visível', () => {
    // "tasks" era a última, mas a equipe do usuário foi tirada dela: a API devolve só as visíveis.
    expect(escolherListagem([testes], 'tasks')).toBe('testes_desempenho');
    expect(escolherListagem([], 'tasks')).toBeNull();
  });

  describe('RN91: coluna e filtro de Equipe', () => {
    it('aparecem em entidade por equipe para quem tem mais de uma equipe NA listagem', () => {
      expect(mostraEquipe(listagem('l', 'L', true, [alfa, beta]), true, 'GESTOR')).toBeTrue();
      expect(mostraEquipe(listagem('l', 'L', true, [alfa, beta]), true, 'INTERNO')).toBeTrue();
    });

    it('com uma equipe só na listagem não aparecem (mesmo que o usuário tenha outras fora dela)', () => {
      expect(mostraEquipe(listagem('l', 'L', true, [alfa]), true, 'GESTOR')).toBeFalse();
      expect(mostraEquipe(listagem('l', 'L', true, []), true, 'INTERNO')).toBeFalse();
    });

    it('o ADMINISTRADOR sempre vê, nas entidades por equipe', () => {
      expect(mostraEquipe(listagem('l', 'L', true, [alfa]), true, 'ADMINISTRADOR')).toBeTrue();
      expect(mostraEquipe(listagem('l', 'L', true, []), true, 'ADMINISTRADOR')).toBeTrue();
    });

    it('nunca em entidade global, nem sem a listagem', () => {
      expect(mostraEquipe(listagem('l', 'L', true, [alfa, beta]), false, 'ADMINISTRADOR')).toBeFalse();
      expect(mostraEquipe(null, true, 'ADMINISTRADOR')).toBeFalse();
    });

    it('o nome da equipe do item vem das equipes da listagem; fora delas (RN92), "Equipe não habilitada"', () => {
      expect(nomeDaEquipe([alfa, beta], 'b')).toBe('Equipe Beta');
      expect(nomeDaEquipe([alfa], 'z')).toBe('Equipe não habilitada');
      expect(nomeDaEquipe([alfa], null)).toBeNull();
    });
  });

  describe('metadados desatualizados', () => {
    const problema = (extras: Partial<Problema>): Problema => ({
      status: 400,
      codigo: 'VALIDACAO',
      titulo: null,
      detalhe: null,
      traceId: null,
      erros: {},
      detalhes: null,
      ...extras,
    });

    it('a API listou campos desconhecidos ou inativos', () => {
      const erro = problema({ erros: { cor: ['x'] }, detalhes: { camposDesatualizados: ['cor'] } });
      expect(metadadosDesatualizados(erro, ['cor', 'nome'])).toBeTrue();
    });

    it('erro num campo que o formulário não tem (ex.: obrigatório criado depois)', () => {
      expect(metadadosDesatualizados(problema({ erros: { patrimonio: ['Campo obrigatório.'] } }), ['nome'])).toBeTrue();
    });

    it('erros comuns nos campos do formulário, na equipe ou gerais não contam', () => {
      expect(
        metadadosDesatualizados(problema({ erros: { nome: ['x'], equipeId: ['y'], $: ['z'] } }), ['nome']),
      ).toBeFalse();
      expect(metadadosDesatualizados(problema({ status: 409, erros: { outro: ['x'] } }), ['nome'])).toBeFalse();
    });
  });
});
