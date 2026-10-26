# Guia de interface

Para quem cria ou muda telas do front-end. **Telas novas seguem este guia** e usam os componentes padrão abaixo, sem valores soltos.
Termos de tela: [glossário](../glossario.md). Achados que originaram as regras: [revisão de usabilidade](revisao-de-usabilidade.md).

## Fundamentos (tokens)

| O quê | Onde (em `src/app/`) |
|---|---|
| Valores de cor: paleta da CAIXA, neutros, paleta dos gráficos, ajustes do PrimeNG | `tema/tema.ts`, o **único lugar de valores de cor** |
| Nomes de uso: espaçamento (`--espaco-*`), fonte (`--fonte-*`), raio, sombra, movimento e cores (`--cor-*`) nos dois temas | `tema/fundamentos.scss` |
| Logo (caminhos das versões positiva e negativa) | `tema/marca.ts` e `tema/logo-marca.component.ts` |
| Tema claro, escuro ou do sistema | `tema/tema.service.ts`, `tema/botao-tema.component.ts` e o script do `index.html`, que aplica o tema antes da primeira pintura |

Componentes e estilos usam **só** `var(--cor-*)`, `var(--espaco-*)` etc. Nada de hex, `px` de espaçamento ou `--p-blue-*` na tela.

## Componentes padrão

- **Moldura** (`core/layout/`): o cabeçalho tem a logo, o nome, o seletor de área de gestão, Aprovações, Configurações, o tema e o
  menu do usuário. Ao lado ficam o menu da área ou a área de configurações. A área atual vem só do `AreaAtualService`.
- **Cabeçalho de página** (`shared/pagina/cabecalho-pagina.component.ts`): trilha, `<h1>` com ícone, subtítulo e ações. **Uma
  ação primária por tela.**
- **Estados** (`shared/estados/`): `app-estado-vazio` (ícone, frase e ação) e `app-estado-erro` (mensagem humana e "Tentar de
  novo"). O carregamento usa `p-skeleton` no formato do conteúdo.
- **Tabelas** (`shared/tabelas/`):
  - a linha é clicável;
  - a primeira coluna (o campo de exibição) fica fixa e é um link de verdade;
  - as ações ficam no "⋯" (`menu-acoes-linha`), preso à direita;
  - uma sombra indica a rolagem horizontal (`appSombraRolagem`);
  - há seletor de colunas, densidade e larguras salvas.
- **Formulários** (`shared/formulario/`, `shared/campos/`):
  - foco no primeiro campo (`appFocoInicial`);
  - rótulo associado a todo controle;
  - erros ligados por `aria-describedby`;
  - botão com verbo e objeto ("Salvar relatório de teste").
- **Salvar sempre visível**: em listas ou formulários longos, a ação de salvar fica sempre visível.
  - **Lista editável longa** (ex.: aba Equipes da área de gestão): cabeçalho fixo `.barra-lista-fixa`, com título, contador,
    busca, ações rápidas ("Selecionar todas as visíveis", "Limpar seleção", "Mostrar só habilitadas") e, à direita,
    "Descartar" e "Salvar alterações". Os botões só habilitam com alterações, e um indicador mostra "3 alterações não salvas".
  - **Formulário longo:** a barra `.acoes-formulario` fica presa no rodapé (`.pagina-formulario` nos itens; `.acoes-fixas` nos
    demais, como usuário, modelo de avaliação, aba Geral do tipo de item e campo).
  - **Sair com alterações pendentes** avisa antes (`shared/formulario/alteracoes-pendentes.guard.ts`: o guard de rota e
    `confirmarSaidaSemSalvar`, também na troca de aba).
- **Cards em grade** (Início e indicadores do painel):
  - todos os cards da linha têm a altura do maior (a grade estica);
  - o conteúdo fica em coluna, e o rodapé (o botão "Novo" ou o número do indicador) é empurrado para baixo com `margin-top: auto`,
    nunca com altura fixa;
  - título e descrição têm no máximo 2 linhas, com reticências e o texto completo na dica.
- **Dica de campo × nota de seção**:
  - `.campo-dica.bloco` é só para a dica logo abaixo de um `.campo`: a margem negativa a encosta no campo;
  - abaixo de uma tabela ou de outro bloco, use `.nota-secao`, que fica no fluxo normal, 16 px abaixo.
- **Cabeçalho**:
  - logo, nome e seletor ficam num contêiner flex (`.identidade-cabecalho`) com um único `gap`;
  - as barrinhas são elementos próprios (`.separador-cabecalho`), sem margem: o nome fica no meio, à mesma distância das duas;
  - o E2E `ajustes-layout.spec.ts` mede isso.
- **Exportar**: todo menu de exportação usa `itensExportacao(...)` (`shared/exportacao/opcoes-exportacao.ts`): "Planilha
  Excel (.xlsx)" primeiro e "Arquivo CSV (.csv)" com a dica. Não escreva os rótulos na tela.
- **Importar**: nas listagens, o botão dividido "Importar" fica ao lado de "Exportar", para quem pode criar ali; a seta leva a
  "Importações anteriores". O assistente (`features/importacao/`) é uma página com etapas visíveis (`p-stepper`), uma ação primária
  por etapa na barra fixa do rodapé, e as células com erro da prévia têm fundo `--cor-perigo-suave`, borda e ícone (nunca só a cor).
- **Diálogo × página**: criar um item relacionado é um diálogo de **um nível** ("+ Criar <Tipo>" no select de referência,
  `criar-relacionado.ts`). Todo o resto é página.

## Textos

Use os termos da coluna "Na tela" do glossário, sempre "Área de gestão". O nome do sistema na tela é **"Catálogo"**, com acento.
No código, continua `Catalogo`. Botões dizem a ação ("Salvar relatório", "Excluir
projeto"). Mensagens de erro explicam o que fazer, sem código técnico.

## Cores

O sistema **lembra a CAIXA sem virar peça publicitária**. Referência: Manual de Identidade Visual da CAIXA.

| Papel | Claro | Escuro | Uso |
|---|---|---|---|
| Primária (Azul CAIXA, 287C) | `#0066B3` (botão, link, foco) | `#4597D6` (botão, texto escuro); link `#78B3E4` | botões primários, links, item ativo, foco, seleção |
| Escala do azul `caixa` 50 a 950 | `#EEF5FB` … `#001E3B` (700 = `#004F9F`, azul escuro do manual) | os mesmos | fundos suaves, bordas, hover e ativo |
| Laranja CAIXA (151C) | `#F7941E` | `#F7941E` | **só destaque pontual**: filete de marca (cabeçalho e cartão do login), contador de aprovações e indicador principal do painel (nos gráficos claros, o tom mais escuro `#D27100`) |
| Neutros `neutro` (frios, puxados para o azul) | fundo `#F5F8FB`, superfície `#FFFFFF`, texto `#364456`, secundário `#5B6B80` | página `#0D1621`, cartões `#172230`, blocos `#243142`, secundário `#8D9CAE` | todo o resto |
| Sucesso | `green.700` `#15803D` | `green.400` `#4ADE80` | tags "Aprovado", confirmações |
| Alerta (âmbar, **não** o laranja) | `yellow.800` `#854D0E`, destaque `yellow.500` | `yellow.300` `#FDE047` | tags "Pendente", avisos |
| Perigo | `red.700` `#B91C1C` | `red.400` `#F87171` | tags "Rejeitado", erros |
| Informação | tons do azul (`primary.100` e `primary.700`) | `primary.300` | tags e mensagens informativas |

**Não faça:**
- texto laranja sobre fundo claro, que fica em 2,3:1 e reprova; sobre o laranja, só texto escuro (8:1);
- botão primário laranja, fundo laranja em área grande ou um quinto uso do laranja;
- `#0066B3` como texto no tema escuro (2,7:1): use `--cor-link`;
- severidade `warn` em botão;
- usar o laranja para avisar: aviso é âmbar.

Contraste (WCAG 2.2 AA) conferido nos dois temas pelo E2E `e2e/marca.spec.ts` (axe, regra `color-contrast`) em login, Início,
listagem e painel.

## Gráficos

Paleta categórica com ordem fixa (`PALETA_GRAFICOS` em `tema/tema.ts`): a mesma categoria tem a mesma cor em todos os gráficos.

| Série | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
|---|---|---|---|---|---|---|---|---|
| Claro | `#0066B3` azul | `#D27100` laranja | `#0E9384` verde-azulado | `#9C3A9C` magenta | `#7D460B` castanho | `#1F8BC9` celeste | `#C2417E` framboesa | `#5E3D99` violeta |
| Contraste com `#FFFFFF` | 5,91 | 3,44 | 3,80 | 6,00 | 7,63 | 3,75 | 4,83 | 8,07 |
| Escuro | `#2B7EC9` | `#DB7000` | `#1EA28F` | `#B747A4` | `#964D09` | `#3292B3` | `#C04D6E` | `#7565BB` |

**Gráfico com uma série só:** usa a 1ª cor (Azul CAIXA), não a cor que a métrica reservou pela posição no arquivo de
configuração (`opcoes-grafico.ts`). Com várias séries, as cores ficam estáveis em todo o painel. "Outros" (da 9ª série em diante)
usa o neutro 400.

No claro, o laranja e o celeste são variantes **mais escuras** da mesma família, e todas as séries têm pelo menos 3:1 contra o
fundo. O laranja da marca puro (`#F7941E`) fica em 2,3:1 e não entra nos gráficos claros.

Resultado do validador de paletas (simulação de protanopia, deuteranopia e tritanopia):
- **Claro** (fundo `#FFFFFF`):
  - todos os testes passam, inclusive o de contraste;
  - entre vizinhas, o pior par tem ΔE 10,3 (meta ≥ 8);
  - as **5 primeiras** passam em todos os pares, mas magenta × azul fica com ΔE 6,0 para protanopia, a faixa que exige legenda e
    "Ver dados", e o painel tem os dois.
- **Escuro** (cartão `#172230`):
  - entre vizinhas, o pior par tem ΔE 8,1;
  - as **5 primeiras** passam em todos os pares (pior par 7,9, na faixa de 6 a 8);
  - o castanho fica em 2,57:1, porque um castanho mais claro se confunde com o laranja; a legenda e "Ver dados" compensam.

Cor nova ou ordem nova: rode o validador de novo nos dois temas e atualize esta tabela.

## Logo

A logo é **vetorial** (SVG), em duas versões, em `src/assets/marca/`:
- `caixa-logo.svg`, a **positiva**, para fundo claro: CAIXA em Azul CAIXA e o X em azul e laranja;
- `caixa-logo-negativa.svg`, a **negativa**, para fundo escuro: a palavra em branco e a parte laranja do X mantida.

Para uso oficial, a logo ainda deve ser **confirmada com a área de marca da CAIXA** (manual de marca: área de proteção e tamanho
mínimo).

- **Onde trocar:** substitua os arquivos (mesmos nomes) ou mude `LOGO_CAIXA` em `src/app/tema/marca.ts` (caminhos e as
  dimensões do `viewBox`).
- **Uso:**
  - direto sobre o fundo, **sem selo**: a versão segue o tema na hora, inclusive em "seguir o sistema";
  - 24 px de altura no cabeçalho e 44 px no login, com `width` e `height` definidos e `alt="CAIXA"`;
  - no cabeçalho, é um link para o Início da área atual ("Início — Catálogo");
  - abaixo de 1024 px, fica só a logo, sem o nome.
