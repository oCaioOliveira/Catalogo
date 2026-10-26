import { formatarDataCompleta, formatarRelativo } from './datas-relativas';

describe('datas relativas', () => {
  const agora = new Date(2026, 9, 24, 16, 0, 0);
  const antes = (ms: number) => new Date(agora.getTime() - ms);

  it('minutos e horas', () => {
    expect(formatarRelativo(antes(20_000), agora)).toBe('agora mesmo');
    expect(formatarRelativo(antes(60_000), agora)).toBe('há 1 minuto');
    expect(formatarRelativo(antes(45 * 60_000), agora)).toBe('há 45 minutos');
    expect(formatarRelativo(antes(3 * 3_600_000), agora)).toBe('há 3 horas');
  });

  it('ontem, dias e, de uma semana para trás, a data', () => {
    expect(formatarRelativo(new Date(2026, 9, 23, 14, 30), agora)).toBe('ontem às 14:30');
    expect(formatarRelativo(new Date(2026, 9, 20, 9, 0), agora)).toBe('há 4 dias');
    expect(formatarRelativo(new Date(2026, 8, 12, 9, 0), agora)).toBe('12/09/2026');
  });

  it('o futuro (relógio atrasado) é "agora mesmo"', () => {
    expect(formatarRelativo(new Date(agora.getTime() + 60_000), agora)).toBe('agora mesmo');
  });

  it('a data completa da dica', () => {
    expect(formatarDataCompleta(new Date(2026, 9, 24, 9, 5))).toBe('24/10/2026 às 09:05');
  });
});
