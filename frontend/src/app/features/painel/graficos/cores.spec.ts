import { PaletaCategorica } from './cores';

describe('PaletaCategorica (cores estáveis)', () => {
  it('a mesma categoria recebe sempre a mesma cor, na ordem em que aparece', () => {
    const paleta = new PaletaCategorica(8);
    expect(paleta.indice('valor:Aprovado')).toBe(0);
    expect(paleta.indice('valor:Reprovado')).toBe(1);
    expect(paleta.indice('valor:Aprovado')).toBe(0); // outro gráfico, a mesma cor
  });

  it('as cores reservadas na ordem do arquivo não dependem de qual widget carrega primeiro', () => {
    const paleta = new PaletaCategorica(8);
    paleta.registrar(['metrica:media:x', 'metrica:contagem']);
    expect(paleta.indice('metrica:contagem')).toBe(1);
  });

  it('cada família tem a sua ordem: os status não ficam sem cor porque as métricas já usaram a paleta', () => {
    const paleta = new PaletaCategorica(8);
    paleta.registrar(['m1', 'm2', 'm3', 'm4', 'm5', 'm6', 'm7', 'm8'], 'metricas');
    expect(paleta.indice('valor:Reprovado', 'status_resultado')).toBe(0);
    expect(paleta.indice('m8', 'metricas')).toBe(7);
  });

  it('acabou a paleta: nulo (cor neutra), nunca recicla uma cor já usada', () => {
    const paleta = new PaletaCategorica(2);
    paleta.registrar(['a', 'b']);
    expect(paleta.indice('c')).toBeNull();
  });
});
