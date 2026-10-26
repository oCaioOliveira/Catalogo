# 0039. Widgets do painel definidos por configuração em arquivo

- Status: Aceito
- Data: 2026-10-22

## Contexto

O painel gerencial do front precisa de indicadores pré-configurados por listagem (para Testes de Desempenho, sete: médias gerais,
relatórios por mês e status, vazão por ambiente...). O motor e o front são genéricos (ADR 0001, ADR 0034): escrever um componente por
gráfico repetiria código e prenderia o painel a uma listagem. A agregação configurável da API (ADR 0030, ADR 0038) já descreve
qualquer desses gráficos com poucos parâmetros. Painéis montados e salvos pelos usuários no servidor exigiriam tabelas, permissões e
uma tela de edição, e ninguém pediu isso ainda.

## Decisão

- Um widget é um **objeto de configuração** tipado (`ConfiguracaoWidget`): título, descrição, entidade, `agruparPor`, `metricas`,
  filtros fixos, o campo de data que recebe o período, visualização, ordenação, limite e largura no grid.
- Os widgets de um grupo ficam num **arquivo de configuração por grupo** no front (`features/painel/configuracao/<grupo>.painel.ts`),
  versionado e revisado como código. Grupo sem arquivo: o painel mostra só a visão geral.
- **Um único componente** (`WidgetComponent`) desenha qualquer widget: valida a configuração contra os metadados, consulta a API e
  transforma a resposta com funções puras.
- A **análise personalizada** monta o mesmo objeto, e o "Copiar configuração" o devolve em JSON: transformar uma análise útil em widget
  pré-configurado é colar esse JSON no arquivo.
- A configuração é **validada no carregamento**: campo inexistente nos metadados gera erro claro no console e o widget aparece com estado
  de erro, sem quebrar a página.
- **Painéis salvos no servidor ficam fora do escopo**: a configuração em arquivo é a solução atual.

### Alternativas descartadas

- **Um componente por gráfico**: mais código, um `if (entidade === ...)` escondido em cada um, e nenhuma ligação com a análise.
- **Painéis salvos no servidor (tabela de widgets por usuário ou equipe)**: schema novo, permissões e tela de edição. Fica para quando
  houver pedido real (com um ADR que substitui este).
- **Configuração vinda de um endpoint de metadados**: exigiria administração e histórico de mais um tipo de metadado; o arquivo resolve
  com revisão de código e sem deploy do back-end.

## Consequências

- ✅ Widget novo = um objeto no arquivo, sem componente novo; o teste `validacao-widget.spec.ts` confere os widgets do arquivo.
- ✅ A análise personalizada e os widgets pré-configurados falam a mesma "língua", testada nos dois sentidos (configuração ↔ URL).
- ⚠️ Mudar o painel exige deploy do front. Um campo renomeado na administração de metadados deixa o widget em erro até o arquivo ser
  corrigido (as chaves são imutáveis, ADR 0027, então só a desativação causa isso na prática).
- ⚠️ O arquivo conhece chaves de campos de uma listagem: é configuração, não regra de tela, mas precisa acompanhar os metadados.
