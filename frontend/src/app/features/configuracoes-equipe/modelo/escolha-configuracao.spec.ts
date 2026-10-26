import { Listagem } from '../../../core/metadados/listagens';
import { escolhaDaConfiguracao } from './escolha-configuracao';

describe('escolhaDaConfiguracao (RN90, RN91)', () => {
  const t = { id: 't', nome: 'T' };
  const d = { id: 'd', nome: 'D' };
  const listagem = (chave: string, minhasEquipes = [t], ativo = true): Listagem => ({
    chave,
    nome: chave,
    ativo,
    minhasEquipes,
    possuiEquipes: minhasEquipes.length > 0,
  });
  const testes = listagem('testes_desempenho', [t]);
  const tasks = listagem('tasks', [d]);

  it('a equipe oferecida é só a do usuário NA listagem escolhida', () => {
    expect(escolhaDaConfiguracao([testes, tasks], 'tasks', null)).toEqual({
      listagens: [testes, tasks],
      equipes: [d],
      grupo: 'tasks',
      equipeId: 'd',
    });
    expect(escolhaDaConfiguracao([testes, tasks], 'testes_desempenho', 't').equipes).toEqual([t]);
  });

  it('equipe da URL que não está habilitada na listagem é trocada pela primeira que está', () => {
    expect(escolhaDaConfiguracao([testes, tasks], 'tasks', 't').equipeId).toBe('d');
  });

  it('listagem da URL que o usuário não vê, inativa ou sem equipe dele dá lugar à primeira que vale', () => {
    const semEquipe = listagem('nova', []);
    const inativa = listagem('antiga', [t], false);
    const escolha = escolhaDaConfiguracao([inativa, semEquipe, tasks], 'nova', null);
    expect(escolha.listagens).toEqual([tasks]);
    expect(escolha.grupo).toBe('tasks');
    expect(escolhaDaConfiguracao([testes], 'sumiu', null).grupo).toBe('testes_desempenho');
  });

  it('sem nenhuma listagem com equipe, nada fica escolhido', () => {
    expect(escolhaDaConfiguracao([listagem('nova', [])], null, null)).toEqual({
      listagens: [],
      equipes: [],
      grupo: null,
      equipeId: null,
    });
  });
});
