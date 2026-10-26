# 0038. Caminhos por referência em filtros, dimensões e métricas

- Status: Aceito
- Data: 2026-10-22
- Substitui, do [ADR 0030](0030-indicadores-genericos-pelos-metadados.md), só o limite de "um nível de referência, só em dimensões".

## Contexto

Nos Testes de Desempenho, as medidas (vazão, percentil 95, taxa de erro) ficam no **cenário**, e as dimensões que a gerência usa
(ambiente, projeto, status, tipo, data de execução) ficam no **relatório** apontado pelo cenário. O ADR 0030 permitia agrupar por
`relatorio_teste.ambiente` (um nível, só em dimensões), mas não filtrar por `relatorio_teste.tipo_teste` nem limitar o período pela
`relatorio_teste.data_execucao`. Faltava também chegar a dados a dois saltos (o projeto do relatório, a sigla do projeto) e usar um número
de outra entidade como métrica. O painel do front precisa disso para os indicadores pré-configurados e a análise personalizada.

## Decisão

- Filtros (listagem, exportação e painel), dimensões e campos de métrica aceitam um **caminho** `<referencia>.<campo>` com **até 2 saltos**
  por campos REFERENCIA (RN89).
- **Um intérprete só**: `CaminhoCampo.Resolver` (Domain, código puro) confere cada salto (REFERENCIA ativo, entidade apontada disponível) e
  o último campo (ativo). O tipo do último campo é conferido por quem usa, com as regras de sempre. `InterpretadorConsulta` e
  `InterpretadorIndicadores` chamam o mesmo método, então os erros são iguais na listagem e no painel.
- **Em LINQ, um JOIN por salto** com `RegistroReferencia`, do último salto para o primeiro, trocando o registro apontado pelo que aponta
  (`Atravessar` em `ConsultaIndicadoresLinq`; `AplicarCondicaoPorCaminho` em `ConsultaRegistrosLinq`). A agregação continua sendo um SQL
  só, com `GROUP BY` no banco (ADR 0031), conferido por `ToQueryString()` em teste.
- **Visibilidade**: o escopo de equipe e aprovação continua aplicado à entidade consultada. O caminho só lê registros apontados por ela,
  que pelo isolamento de referências (RN29) são da mesma equipe ou globais. Não há regra de visibilidade nova; um teste de integração
  confirma que um caminho não leva a dado de outra equipe.
- **Descoberta**: o front não precisa de endpoint novo. `GET .../entidades` já traz `entidadeReferenciada` em cada campo REFERENCIA.
- **Período sobre uma data de usuário** (ex.: data de execução): usa os filtros `campo.de`/`campo.ate`, também por caminho, e não um
  parâmetro novo. Assim o filtro trata a data como o agrupamento `:mes` dela (o valor gravado, RN18 e RN80), e os dois não divergem.

### Alternativas descartadas

- **Mais de 2 saltos ou caminhos livres**: cada salto é um JOIN; o SQL cresceria sem limite, e nenhuma pergunta atual precisa disso.
- **Campos "desnormalizados" no cenário** (copiar ambiente e projeto do relatório): duplicaria dados e exigiria sincronizar a cópia a cada
  alteração do relatório.
- **Parâmetro novo `campoPeriodo`** para o período sobre a data de execução no fuso de negócio: o agrupamento `:mes` dessa data usa o valor
  gravado (UTC), então filtrar no fuso e agrupar em UTC poria um relatório num mês no filtro e noutro no gráfico.
- **Endpoint de "caminhos disponíveis"**: os metadados já bastam.

## Consequências

- ✅ "Vazão média por ambiente" e "taxa de erro por projeto, só testes de Carga" são uma URL, sem código por listagem.
- ✅ Listagem e painel aceitam exatamente os mesmos caminhos, com as mesmas mensagens de erro.
- ⚠️ Métrica por caminho é calculada por registro consultado: dois cenários do mesmo relatório contam o número do relatório duas vezes.
- ⚠️ Cada salto é um JOIN a mais: o SQL cresce com o número de caminhos pedidos (no máximo 2 dimensões, 5 métricas e os filtros).
