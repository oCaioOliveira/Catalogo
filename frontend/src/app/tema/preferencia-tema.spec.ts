import { lerPreferencia, proximaPreferencia, resolverTema } from './preferencia-tema';

describe('preferência de tema', () => {
  it('lê só os valores conhecidos; o resto é "seguir o sistema"', () => {
    expect(lerPreferencia('claro')).toBe('claro');
    expect(lerPreferencia('escuro')).toBe('escuro');
    expect(lerPreferencia('sistema')).toBe('sistema');
    expect(lerPreferencia(null)).toBe('sistema');
    expect(lerPreferencia('azul')).toBe('sistema');
  });

  it('"seguir o sistema" usa o prefers-color-scheme', () => {
    expect(resolverTema('sistema', true)).toBe('escuro');
    expect(resolverTema('sistema', false)).toBe('claro');
  });

  it('uma escolha explícita vale mesmo contra o sistema', () => {
    expect(resolverTema('claro', true)).toBe('claro');
    expect(resolverTema('escuro', false)).toBe('escuro');
  });

  it('o botão de tema alterna claro → escuro → sistema → claro', () => {
    expect(proximaPreferencia('claro')).toBe('escuro');
    expect(proximaPreferencia('escuro')).toBe('sistema');
    expect(proximaPreferencia('sistema')).toBe('claro');
  });
});
