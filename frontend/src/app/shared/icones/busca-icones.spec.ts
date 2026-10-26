import { filtrarIcones, nomeDoIcone } from './busca-icones';
import { ICONES_PRIMEICONS } from './primeicons';

describe('seletor de ícone (busca)', () => {
  it('a lista vem do PrimeIcons e tem os ícones do seed', () => {
    for (const icone of [
      'pi-chart-line',
      'pi-briefcase',
      'pi-link',
      'pi-ticket',
      'pi-users',
      'pi-tag',
      'pi-server',
      'pi-flag',
    ]) {
      expect(ICONES_PRIMEICONS).toContain(icone);
    }
  });

  it('busca pelo nome em inglês, com ou sem "pi-"', () => {
    expect(filtrarIcones(ICONES_PRIMEICONS, 'briefcase')).toEqual(['pi-briefcase']);
    expect(filtrarIcones(ICONES_PRIMEICONS, 'pi-ticket')).toEqual(['pi-ticket']);
  });

  it('busca em português, com ou sem acento', () => {
    expect(filtrarIcones(ICONES_PRIMEICONS, 'gráfico')).toContain('pi-chart-line');
    expect(filtrarIcones(ICONES_PRIMEICONS, 'grafico')).toContain('pi-chart-bar');
    expect(filtrarIcones(ICONES_PRIMEICONS, 'usuário')).toContain('pi-user');
    expect(filtrarIcones(ICONES_PRIMEICONS, 'servidor')).toEqual(['pi-server']);
  });

  it('busca vazia devolve todos; sem resultado, lista vazia', () => {
    expect(filtrarIcones(ICONES_PRIMEICONS, '  ').length).toBe(ICONES_PRIMEICONS.length);
    expect(filtrarIcones(ICONES_PRIMEICONS, 'xyzxyz')).toEqual([]);
  });

  it('nome para o leitor de tela', () => {
    expect(nomeDoIcone('pi-chart-line')).toBe('chart line');
  });
});
