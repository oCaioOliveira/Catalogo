# 0040. Gráficos com o p-chart do PrimeNG (Chart.js)

- Status: Aceito
- Data: 2026-10-22

## Contexto

O painel gerencial precisa de gráficos de colunas, colunas empilhadas, barras horizontais e linhas, com cores do tema, números em
pt-BR e acessibilidade. O front já usa PrimeNG (ADR 0033), que tem um componente de gráficos (`p-chart`) sobre o Chart.js (MIT).
Outra biblioteca (ECharts, ngx-charts, Highcharts) traria outro modelo de configuração, outro tema e, no caso do Highcharts, licença
comercial.

## Decisão

- Todo gráfico usa o **`p-chart`** do PrimeNG. A dependência `chart.js` entra no `package.json` porque é o motor que o `p-chart`
  carrega; nenhuma outra biblioteca de gráficos.
- As regras visuais ficam em **funções puras**, testadas sem navegador: a resposta da API vira dados de gráfico
  (`transformar-dados.ts`) e esses dados viram a configuração do Chart.js (`opcoes-grafico.ts`): eixos de colunas e barras começando
  em zero, um eixo de valores só, legenda só com mais de uma série, números pt-BR com unidade e marcas finas.
- **Cores só do tema**: a paleta categórica é uma lista de variáveis do tema (`PALETA_GRAFICOS`, em `src/app/tema/tema.ts`), lida em
  tempo de execução. Passos diferentes para o tema claro e para o escuro, os dois conferidos com um validador de paletas (separação para
  daltonismo e entre cores vizinhas). As cores são atribuídas pela identidade da série, em ordem fixa, sem reciclar.
- **Acessibilidade**: todo gráfico tem "Ver dados" (a tabela com os mesmos números), e o canvas é rotulado pelo título e pela descrição.

### Alternativas descartadas

- **ECharts / ngx-charts**: boas bibliotecas, mas um segundo sistema de tema e de componentes ao lado do PrimeNG.
- **Highcharts**: licença comercial.
- **SVG desenhado à mão**: muito código para eixos, dicas e responsividade.

## Consequências

- ✅ Um só fornecedor de componentes; o gráfico segue o tema do PrimeNG.
- ✅ As regras visuais têm testes unitários (`cores.spec.ts`, `transformar-dados.spec.ts`).
- ⚠️ Chart.js desenha em canvas: o texto do gráfico não é lido por leitores de tela, por isso a tabela "Ver dados" é obrigatória.
- ⚠️ O bundle do painel cresce com o Chart.js, mas carrega só nas rotas do painel (carregamento sob demanda).
