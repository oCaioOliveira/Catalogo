import { destinoAoTrocarArea, ehAreaDeConfiguracoes, filtrarPorNome, iniciais } from './navegacao';

describe('navegação (moldura)', () => {
  it('reconhece a área de configurações', () => {
    expect(ehAreaDeConfiguracoes('/configuracoes/equipe?grupo=x')).toBeTrue();
    expect(ehAreaDeConfiguracoes('/configuracoes')).toBeTrue();
    expect(ehAreaDeConfiguracoes('/testes_desempenho/relatorio_teste')).toBeFalse();
    expect(ehAreaDeConfiguracoes('/configuracoes-equipe')).toBeFalse();
  });

  it('trocar a área no painel abre o painel da nova; nas telas da área, o Início da nova', () => {
    expect(destinoAoTrocarArea('/painel/testes?periodo=3m', 'tasks')).toEqual(['/painel', 'tasks']);
    expect(destinoAoTrocarArea('/testes/relatorio_teste/123', 'tasks')).toEqual(['/', 'tasks']);
    expect(destinoAoTrocarArea('/testes', 'tasks')).toEqual(['/', 'tasks']);
  });

  it('fora das áreas, trocar a área não sai da tela', () => {
    expect(destinoAoTrocarArea('/configuracoes/equipe', 'tasks')).toBeNull();
    expect(destinoAoTrocarArea('/aprovacoes?grupo=testes', 'tasks')).toBeNull();
    expect(destinoAoTrocarArea('/minha-conta', 'tasks')).toBeNull();
    expect(destinoAoTrocarArea('/', 'tasks')).toBeNull();
  });

  it('iniciais do avatar', () => {
    expect(iniciais('Gabriela Gestora')).toBe('GG');
    expect(iniciais('  ana maria de souza ')).toBe('AS');
    expect(iniciais('Administrador')).toBe('A');
    expect(iniciais('')).toBe('?');
    expect(iniciais(null)).toBe('?');
  });

  it('a busca do menu ignora acentos e maiúsculas', () => {
    const itens = [{ nome: 'Relatórios de Teste' }, { nome: 'Projetos' }, { nome: 'RTCs' }];
    expect(filtrarPorNome(itens, 'relatorio')).toEqual([{ nome: 'Relatórios de Teste' }]);
    expect(filtrarPorNome(itens, 'RT')).toEqual([{ nome: 'RTCs' }]);
    expect(filtrarPorNome(itens, '  ')).toEqual(itens);
  });
});
