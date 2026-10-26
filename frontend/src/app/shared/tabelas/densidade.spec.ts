import { CHAVE_DENSIDADE, DENSIDADE_PADRAO, gravarDensidade, lerDensidade, tamanhoDaTabela } from './densidade';

describe('densidade das tabelas', () => {
  function armazenamento(inicial: Record<string, string> = {}) {
    const dados = { ...inicial };
    return {
      dados,
      getItem: (chave: string) => dados[chave] ?? null,
      setItem: (chave: string, valor: string) => (dados[chave] = valor),
    };
  }

  it('padrão compacto, que é o "small" do p-table', () => {
    expect(DENSIDADE_PADRAO).toBe('compacto');
    expect(lerDensidade(armazenamento())).toBe('compacto');
    expect(tamanhoDaTabela('compacto')).toBe('small');
    expect(tamanhoDaTabela('confortavel')).toBeUndefined();
  });

  it('grava e lê a escolha', () => {
    const local = armazenamento();
    gravarDensidade('confortavel', local);
    expect(local.dados[CHAVE_DENSIDADE]).toBe('confortavel');
    expect(lerDensidade(local)).toBe('confortavel');
  });

  it('valor desconhecido volta ao padrão', () => {
    expect(lerDensidade(armazenamento({ [CHAVE_DENSIDADE]: 'gigante' }))).toBe('compacto');
  });

  it('sem armazenamento (ou se ele falhar), não quebra: usa o padrão', () => {
    const quebrado = {
      getItem: () => {
        throw new Error('bloqueado');
      },
      setItem: () => {
        throw new Error('bloqueado');
      },
    };
    expect(lerDensidade(quebrado)).toBe('compacto');
    expect(() => gravarDensidade('confortavel', quebrado)).not.toThrow();
    expect(lerDensidade(null)).toBe('compacto');
  });
});
