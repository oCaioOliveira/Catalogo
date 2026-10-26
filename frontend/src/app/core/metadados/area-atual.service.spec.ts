import { TestBed } from '@angular/core/testing';
import { AreaAtualService } from './area-atual.service';
import { Listagem } from './listagens';

// Um serviço só diz qual é a área de gestão atual, para todas as telas.
describe('AreaAtualService', () => {
  const area = (chave: string, ativo = true): Listagem => ({
    chave,
    nome: chave,
    ativo,
    minhasEquipes: [{ id: 'a', nome: 'Equipe Alfa' }],
    possuiEquipes: true,
  });

  beforeEach(() => localStorage.removeItem('catalogo.ultimaListagem'));
  afterEach(() => localStorage.removeItem('catalogo.ultimaListagem'));

  it('a área escolhida (pela rota ou pelo seletor) vira a atual e fica guardada como a última usada', () => {
    const servico = TestBed.inject(AreaAtualService);
    servico.atualizarAreas([area('testes'), area('tasks')]);

    servico.definir('tasks');

    expect(servico.grupo()).toBe('tasks');
    expect(servico.area()?.chave).toBe('tasks');
    expect(localStorage.getItem('catalogo.ultimaListagem')).toBe('tasks');
  });

  it('fora das rotas de área, começa pela última usada', () => {
    localStorage.setItem('catalogo.ultimaListagem', 'tasks');

    const servico = TestBed.inject(AreaAtualService);
    servico.atualizarAreas([area('testes'), area('tasks')]);

    expect(servico.grupo()).toBe('tasks');
  });

  it('a última usada que deixou de ser visível dá lugar à primeira visível', () => {
    localStorage.setItem('catalogo.ultimaListagem', 'sumiu');

    const servico = TestBed.inject(AreaAtualService);
    servico.atualizarAreas([area('desativada', false), area('testes'), area('tasks')]);

    expect(servico.grupo()).toBe('testes');
  });

  it('sem nenhuma área visível, não há área atual', () => {
    const servico = TestBed.inject(AreaAtualService);
    servico.definir('testes');

    servico.atualizarAreas([]);

    expect(servico.grupo()).toBeNull();
    expect(servico.area()).toBeNull();
  });

  it('antes de carregar as áreas, vale a escolhida (a da rota)', () => {
    const servico = TestBed.inject(AreaAtualService);

    servico.definir('tasks');

    expect(servico.grupo()).toBe('tasks');
    expect(servico.area()).toBeNull();
  });
});
