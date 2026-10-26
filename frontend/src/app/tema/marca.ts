// A marca CAIXA no Catalogo: o ÚNICO lugar do caminho da logo. Funções puras, sem Angular: marca.spec.ts.
// Para trocar os arquivos: substitua-os em src/assets/marca/ (mesmos nomes) ou mude os caminhos abaixo e as dimensões (o viewBox
// do SVG). Veja docs/design/guia-de-interface.md → "Logo".
import { TemaAplicado } from './preferencia-tema';

export interface LogoMarca {
  /** Versão para fundo claro (obrigatória). */
  claro: string;
  /** Versão para fundo escuro (a "negativa" do manual de marca); sem ela, vale a clara. */
  escuro: string | null;
  /** Dimensões do arquivo, em pixels: só a proporção importa (para o width/height da <img>, sem "pulo" ao carregar). */
  largura: number;
  altura: number;
}

/**
 * A logo da CAIXA em vetor: a POSITIVA (CAIXA em Azul CAIXA, o X em azul e laranja) para fundo claro e a NEGATIVA (palavra em
 * branco, a parte laranja do X mantida) para fundo escuro, direto sobre o fundo, sem selo. Para uso oficial, ainda deve ser
 * confirmada com a área de marca da CAIXA.
 */
export const LOGO_CAIXA: LogoMarca = {
  claro: 'assets/marca/caixa-logo.svg',
  escuro: 'assets/marca/caixa-logo-negativa.svg',
  largura: 2000,
  altura: 453,
};

/** O texto alternativo da logo (o nome da marca). */
export const NOME_MARCA = 'CAIXA';

/** O arquivo da logo para o tema: a variante escura, se existir; senão, a clara. */
export function logoDoTema(logo: LogoMarca, tema: TemaAplicado): string {
  return tema === 'escuro' && logo.escuro ? logo.escuro : logo.claro;
}

/** A largura, em pixels inteiros, para uma altura, preservando a proporção do arquivo. */
export function larguraProporcional(logo: LogoMarca, altura: number): number {
  return Math.round((altura * logo.largura) / logo.altura);
}
