import { destinoDaAdministracaoAntiga, destinoDasConfiguracoesAntigas, destinoDaUrlAntiga } from './rotas-antigas';

describe('rotas antigas (redirecionamento para /configuracoes)', () => {
  it('a administração de listagens vira "Áreas de gestão"', () => {
    expect(destinoDaAdministracaoAntiga(['listagens'])).toBe('/configuracoes/areas');
    expect(destinoDaAdministracaoAntiga(['listagens', 'inventario'])).toBe('/configuracoes/areas/inventario');
    expect(destinoDaAdministracaoAntiga(['listagens', 'inventario', 'entidades', 'equipamento'])).toBe(
      '/configuracoes/areas/inventario/entidades/equipamento',
    );
  });

  it('usuários e equipes mudam só o prefixo; /admin sozinho abre Usuários', () => {
    expect(destinoDaAdministracaoAntiga([])).toBe('/configuracoes/usuarios');
    expect(destinoDaAdministracaoAntiga(['usuarios', '123', 'editar'])).toBe('/configuracoes/usuarios/123/editar');
    expect(destinoDaAdministracaoAntiga(['equipes', 'novo'])).toBe('/configuracoes/equipes/novo');
  });

  it('configurações da equipe', () => {
    expect(destinoDasConfiguracoesAntigas([])).toBe('/configuracoes/equipe');
    expect(destinoDasConfiguracoesAntigas(['e1', 'testes', 'relatorio_teste', 'modelo'])).toBe(
      '/configuracoes/equipe/e1/testes/relatorio_teste/modelo',
    );
  });

  it('a URL inteira mantém a query e o fragmento; o que não é antigo não muda', () => {
    expect(destinoDaUrlAntiga('/admin/usuarios?busca=admin&perfil=GESTOR')).toBe(
      '/configuracoes/usuarios?busca=admin&perfil=GESTOR',
    );
    expect(destinoDaUrlAntiga('/configuracoes-equipe?grupo=testes#topo')).toBe(
      '/configuracoes/equipe?grupo=testes#topo',
    );
    expect(destinoDaUrlAntiga('/admin')).toBe('/configuracoes/usuarios');
    expect(destinoDaUrlAntiga('/testes/relatorio_teste')).toBeNull();
  });
});
