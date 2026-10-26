import { LIMITES, opcoesDoTipo, rotuloDoTipo, tiposNaEdicao, TIPOS_CAMPO } from './tipos-campo';

describe('tipos de campo no painel lateral', () => {
  it('cada tipo mostra só as opções dele', () => {
    expect(opcoesDoTipo('TEXTO')).toEqual({
      tamanhoMaximo: true,
      precisaoEscala: false,
      opcoesLista: false,
      referencia: false,
    });
    expect(opcoesDoTipo('DECIMAL')).toEqual({
      tamanhoMaximo: false,
      precisaoEscala: true,
      opcoesLista: false,
      referencia: false,
    });
    expect(opcoesDoTipo('LISTA')).toEqual({
      tamanhoMaximo: false,
      precisaoEscala: false,
      opcoesLista: true,
      referencia: false,
    });
    expect(opcoesDoTipo('REFERENCIA')).toEqual({
      tamanhoMaximo: false,
      precisaoEscala: false,
      opcoesLista: false,
      referencia: true,
    });
    for (const tipo of ['TEXTO_LONGO', 'INTEIRO', 'BOOLEANO', 'DATA', 'DATA_HORA', 'URL'] as const) {
      expect(Object.values(opcoesDoTipo(tipo)).some((v) => v))
        .withContext(tipo)
        .toBeFalse();
    }
  });

  it('todos os tipos do back-end têm nome na tela', () => {
    expect(TIPOS_CAMPO.length).toBe(10);
    expect(rotuloDoTipo('URL')).toBe('Endereço (URL)');
  });

  it('na edição, só as conversões permitidas (RN66)', () => {
    expect(tiposNaEdicao('TEXTO')).toEqual(['TEXTO', 'TEXTO_LONGO', 'URL']);
    expect(tiposNaEdicao('TEXTO_LONGO')).toEqual(['TEXTO_LONGO', 'URL']);
    expect(tiposNaEdicao('URL')).toEqual(['URL', 'TEXTO_LONGO']);
    expect(tiposNaEdicao('DECIMAL')).toEqual(['DECIMAL']);
    expect(tiposNaEdicao('REFERENCIA')).toEqual(['REFERENCIA']);
    expect(tiposNaEdicao('URL')).not.toContain('TEXTO');
  });

  it('limites iguais aos CHECKs do banco', () => {
    expect(LIMITES.tamanhoMaximo.maximo).toBe(4000);
    expect(LIMITES.precisao.maximo).toBe(38);
    expect(LIMITES.escala.maximo).toBe(10);
  });
});
