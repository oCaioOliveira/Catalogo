import { DESCRICOES_PERFIL, PERFIS, equipesParaSalvar, erroEquipes, exigeEquipes } from './perfis';

describe('perfis: equipes obrigatórias por perfil (RN31)', () => {
  it('só o ADMINISTRADOR dispensa equipe', () => {
    expect(exigeEquipes('ADMINISTRADOR')).toBeFalse();
    for (const perfil of ['EXTERNO', 'INTERNO', 'GESTOR'] as const) {
      expect(exigeEquipes(perfil)).withContext(perfil).toBeTrue();
    }
    expect(exigeEquipes(null)).toBeFalse(); // sem perfil escolhido, o erro é o do perfil
  });

  it('sem equipe: erro para os demais perfis, nenhum para o ADMINISTRADOR', () => {
    expect(erroEquipes('INTERNO', [])).toContain('pelo menos uma equipe');
    expect(erroEquipes('INTERNO', ['e1'])).toBeNull();
    expect(erroEquipes('ADMINISTRADOR', [])).toBeNull();
  });

  it('ao trocar para ADMINISTRADOR, as equipes escolhidas antes não vão para a API', () => {
    expect(equipesParaSalvar('ADMINISTRADOR', ['e1', 'e2'])).toEqual([]);
    expect(equipesParaSalvar('GESTOR', ['e1', 'e2'])).toEqual(['e1', 'e2']);
  });

  it('cada perfil tem a descrição curta', () => {
    expect(PERFIS.map((p) => p.valor)).toEqual(['EXTERNO', 'INTERNO', 'GESTOR', 'ADMINISTRADOR']);
    expect(DESCRICOES_PERFIL.GESTOR).toBe('Cria, edita, exclui e acessa o painel.');
  });
});
