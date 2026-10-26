import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

/**
 * Cores da marca CAIXA (Manual de Identidade Visual): o Azul CAIXA (Pantone 287C) é a cor PRIMÁRIA; o Laranja CAIXA
 * (Pantone 151C) é só DESTAQUE PONTUAL (filete de marca, contador de aprovações, indicador principal do painel e 2ª série dos
 * gráficos). Regras de uso e o que não fazer: docs/design/guia-de-interface.md → "Cores".
 */
export const CORES_MARCA = {
  azul: '#0066B3',
  laranja: '#F7941E',
  /** Tons de apoio do manual (degradês da marca). */
  azulEscuro: '#004F9F',
  azulClaro: '#5BC5F2',
  laranjaEscuro: '#EF7D00',
  cinzaClaro: '#DCDDDE',
} as const;

/** Escala do Azul CAIXA: o 500 é a cor da marca; o 700 é o azul escuro do manual. Primária do tema (botões, links, foco). */
const caixa = {
  50: '#EEF5FB',
  100: '#D5E7F6',
  200: '#AACFEE',
  300: '#78B3E4',
  400: '#4597D6',
  500: CORES_MARCA.azul,
  600: '#005BA3',
  700: CORES_MARCA.azulEscuro,
  800: '#003F7A',
  900: '#002F5B',
  950: '#001E3B',
};

/**
 * Neutros levemente frios (puxados para o azul da marca), no lugar do cinza puro. Valem para os dois temas: no claro, o fundo é
 * o 50 e as superfícies o branco; no escuro, azul-acinzentado bem escuro (nunca preto): página 950, cartões 900, blocos 800.
 */
const neutro = {
  0: '#FFFFFF',
  50: '#F5F8FB',
  100: '#EAEFF5',
  200: '#D8E0E9',
  300: '#BAC6D3',
  400: '#8D9CAE',
  500: '#5B6B80',
  600: '#4A596D',
  700: '#364456',
  800: '#243142',
  900: '#172230',
  950: '#0D1621',
};

const superficie = Object.fromEntries(Object.keys(neutro).map((passo) => [passo, `{neutro.${passo}}`]));

/**
 * Paleta CATEGÓRICA dos gráficos (ordem fixa; a mesma categoria tem a mesma cor em todos os gráficos, graficos/cores.ts).
 * 1ª série = Azul CAIXA, 2ª = Laranja CAIXA (par seguro para daltonismo); depois verde-azulado, magenta, castanho,
 * celeste, framboesa e violeta, sem repetir as cores semânticas (verde de sucesso, amarelo de alerta, vermelho de perigo). Cada tema
 * tem os seus tons, validados contra a sua superfície. No claro, o laranja e o celeste são variantes MAIS ESCURAS da mesma família,
 * para todas as séries terem pelo menos 3:1 contra o fundo branco (o laranja da marca puro fica em 2,3:1). Resultado da validação:
 * docs/design/guia-de-interface.md → "Gráficos". Um gráfico de uma série só usa a 1ª cor (opcoes-grafico.ts).
 */
const grafico = {
  claro: {
    1: CORES_MARCA.azul,
    2: '#D27100',
    3: '#0E9384',
    4: '#9C3A9C',
    5: '#7D460B',
    6: '#1F8BC9',
    7: '#C2417E',
    8: '#5E3D99',
  },
  escuro: {
    1: '#2B7EC9',
    2: '#DB7000',
    3: '#1EA28F',
    4: '#B747A4',
    5: '#964D09',
    6: '#3292B3',
    7: '#C04D6E',
    8: '#7565BB',
  },
};

/** Destaque de marca (laranja) e o texto que vai sobre ele: sempre escuro (o laranja com texto branco não passa no contraste). */
const destaque = { laranja: CORES_MARCA.laranja, texto: neutro[950] };

/**
 * Tema do Catalogo: o Aura do PrimeNG com a identidade da CAIXA. É o ÚNICO lugar de valores de cor: os fundamentos
 * (fundamentos.scss) dão nomes de uso a estas variáveis (--p-*), e os componentes usam só os nomes de uso.
 */
export const TemaCatalogo = definePreset(Aura, {
  primitive: { caixa, neutro, grafico, destaque },
  semantic: {
    primary: Object.fromEntries(Object.keys(caixa).map((passo) => [passo, `{caixa.${passo}}`])),
    colorScheme: {
      light: {
        surface: superficie,
        // Bordas dos campos com contraste de 3:1 contra o fundo (WCAG 2.2, 1.4.11; revisão de usabilidade, achado A2).
        formField: {
          borderColor: '{surface.500}',
          hoverBorderColor: '{surface.600}',
          invalidBorderColor: '{red.600}',
        },
      },
      dark: {
        surface: superficie,
        // No escuro, o 500 fica abaixo de 3:1 sobre os cartões (900): a borda usa o 400.
        formField: {
          borderColor: '{surface.400}',
          hoverBorderColor: '{surface.300}',
          invalidBorderColor: '{red.400}',
        },
      },
    },
  },
  components: {
    // Alerta é AMARELO, nunca o laranja (o Aura usa laranja em tags e badges "warn"; não use botão "warn"): o usuário não pode confundir o
    // destaque da marca com um aviso. Informação usa a escala do azul da marca.
    tag: {
      colorScheme: {
        light: {
          warn: { background: '{yellow.100}', color: '{yellow.800}' },
          info: { background: '{primary.100}', color: '{primary.700}' },
        },
        dark: {
          warn: { background: 'color-mix(in srgb, {yellow.500}, transparent 84%)', color: '{yellow.300}' },
          info: { background: 'color-mix(in srgb, {primary.400}, transparent 84%)', color: '{primary.300}' },
        },
      },
    },
    badge: {
      colorScheme: {
        light: { warn: { background: '{yellow.500}', color: '{yellow.950}' } },
        dark: { warn: { background: '{yellow.400}', color: '{yellow.950}' } },
      },
    },
  },
});

/** Opções do tema: o modo escuro liga com a classe .catalogo-escuro no <html> (TemaService; cores semânticas em fundamentos.scss). */
export const opcoesTema = {
  darkModeSelector: '.catalogo-escuro',
  cssLayer: false,
};

/**
 * As cores dos gráficos como VARIÁVEIS do tema (lidas em tempo de execução por graficos/cores.ts), nunca hex no componente. Ordem
 * FIXA (as cores são atribuídas nessa ordem e nunca recicladas: a 9ª série vira "Outros", neutra). Validadas com o validador de
 * paletas (separação para daltonismo e entre vizinhas) contra a superfície de cada tema, todas com pelo menos 3:1 contra o fundo.
 * Todo gráfico tem legenda (com mais de uma série) e "Ver dados" (a tabela com os mesmos números).
 */
export const PALETA_GRAFICOS = {
  claro: [1, 2, 3, 4, 5, 6, 7, 8].map((n) => `--p-grafico-claro-${n}`),
  escuro: [1, 2, 3, 4, 5, 6, 7, 8].map((n) => `--p-grafico-escuro-${n}`),
  /** "Outros" e séries além da 8ª. */
  neutra: '--p-surface-400',
  texto: '--p-text-color',
  textoSecundario: '--p-text-muted-color',
  grade: '--p-content-border-color',
  superficie: '--p-content-background',
} as const;
