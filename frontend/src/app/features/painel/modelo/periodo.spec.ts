import { calcularPeriodo, ehDataValida } from './periodo';

describe('periodo (atalhos)', () => {
  const hoje = new Date(2026, 9, 22); // 22/10/2026

  it('últimos 12 meses: do 1º dia do mês, 11 meses atrás, até hoje (12 meses inteiros nos gráficos por mês)', () => {
    expect(calcularPeriodo('12m', hoje)).toEqual({ de: '2025-11-01', ate: '2026-10-22' });
  });

  it('últimos 3 meses, 30 dias e ano atual', () => {
    expect(calcularPeriodo('3m', hoje)).toEqual({ de: '2026-08-01', ate: '2026-10-22' });
    expect(calcularPeriodo('30d', hoje)).toEqual({ de: '2026-09-23', ate: '2026-10-22' });
    expect(calcularPeriodo('ano', hoje)).toEqual({ de: '2026-01-01', ate: '2026-10-22' });
  });

  it('em janeiro, os 12 meses começam no ano anterior', () => {
    expect(calcularPeriodo('12m', new Date(2026, 0, 5)).de).toBe('2025-02-01');
  });

  it('data válida: aaaa-mm-dd que existe', () => {
    expect(ehDataValida('2026-02-28')).toBeTrue();
    expect(ehDataValida('2026-02-30')).toBeFalse();
    expect(ehDataValida('28/02/2026')).toBeFalse();
    expect(ehDataValida(undefined)).toBeFalse();
  });
});
