import { LOGO_CAIXA, LogoMarca, larguraProporcional, logoDoTema } from './marca';

describe('marca', () => {
  const comEscura: LogoMarca = { claro: 'clara.svg', escuro: 'escura.svg', largura: 400, altura: 100 };
  const semEscura: LogoMarca = { ...comEscura, escuro: null };

  it('usa a variante escura no tema escuro, quando ela existe', () => {
    expect(logoDoTema(comEscura, 'escuro')).toBe('escura.svg');
    expect(logoDoTema(comEscura, 'claro')).toBe('clara.svg');
  });

  it('sem a variante escura, usa a clara nos dois temas', () => {
    expect(logoDoTema(semEscura, 'escuro')).toBe('clara.svg');
    expect(logoDoTema(semEscura, 'claro')).toBe('clara.svg');
  });

  it('calcula a largura pela proporção do arquivo, sem distorcer', () => {
    expect(larguraProporcional(comEscura, 24)).toBe(96);
    expect(larguraProporcional(LOGO_CAIXA, 24)).toBe(106);
  });

  it('a logo da CAIXA é vetorial: positiva no tema claro, negativa no escuro', () => {
    expect(logoDoTema(LOGO_CAIXA, 'claro')).toBe('assets/marca/caixa-logo.svg');
    expect(logoDoTema(LOGO_CAIXA, 'escuro')).toBe('assets/marca/caixa-logo-negativa.svg');
  });
});
