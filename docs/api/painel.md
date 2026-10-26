# Painel gerencial

Números para o painel do front: uma **visão geral** por grupo e uma **agregação configurável** por entidade, que também pode ser
**exportada**. Tudo é calculado no banco a partir dos metadados ([ADR 0030](../adr/0030-indicadores-genericos-pelos-metadados.md),
[ADR 0031](../adr/0031-agregacao-no-banco-em-linq.md), [ADR 0038](../adr/0038-caminhos-por-referencia.md)). Regras: RN78 a RN89 em
[`regras-de-negocio.md`](../regras-de-negocio.md#painel-gerencial).

| Endpoint | O quê |
|---|---|
| `GET /api/v1/painel/grupos/{grupo}/visao-geral?de=&ate=&equipeId=` | volume, aprovação, avaliação e atividade do grupo |
| `GET /api/v1/painel/grupos/{grupo}/entidades/{entidade}/indicadores?agruparPor=&metricas=&ordenarPor=&limite=&de=&ate=&<filtros>` | agregação configurável |
| `GET /api/v1/painel/grupos/{grupo}/entidades/{entidade}/indicadores/exportar?formato=csv\|xlsx&...` | a mesma agregação em arquivo |

## Quem acessa (RN78, RN79)

- **GESTOR** e **ADMINISTRADOR**. INTERNO e EXTERNO: **403** `SEM_PERMISSAO` ([ADR 0032](../adr/0032-painel-so-para-gestor-e-administrador.md)).
- Os números contam **só o que o usuário veria na listagem**: o GESTOR, as entidades globais e as equipes dele; o ADMINISTRADOR, tudo.
  Um registro de outra equipe não muda nenhum número do gestor.
- `equipeId` restringe as entidades **por equipe** a uma equipe (o GESTOR, só às dele: senão 403). As entidades **globais** continuam
  inteiras: não são de equipe nenhuma.

## Período e fuso (RN80)

- `de` e `ate` são **datas** `aaaa-mm-dd` no **fuso de negócio** (`Painel:FusoHorario`, padrão `America/Sao_Paulo`), **inclusivas**. Sem
  elas, não há limite daquele lado.
- O fuso entra na conta: `de=2026-03-01&ate=2026-03-31` é de 01/03 00:00 a 31/03 23:59:59 **em São Paulo** (03:00 UTC de 01/03 até 03:00
  UTC de 01/04). Um registro criado em **2026-04-01 01:00 UTC** é de **março**, tanto no filtro quanto em `criadoEm:mes`.
- O deslocamento do fuso é **fixo** (sem horário de verão, que o Brasil não tem desde 2019).
- Campos DATA e DATA_HORA dos **usuários** (ex.: `data_execucao`) são usados como estão gravados (a DATA_HORA em UTC), sem conversão.
- Na visão geral, o período vale para o que é "no período" (criados, alterados, decisões, atividade). Totais, situações e avaliações são
  o **estado atual**. Nos indicadores, `de` e `ate` filtram a **data de criação** dos registros.
- A resposta devolve o período aplicado: `"periodo": { "de": "2026-03-01", "ate": "2026-03-31", "fusoHorario": "America/Sao_Paulo" }`.

## Visão geral

`GET /api/v1/painel/grupos/testes_desempenho/visao-geral?de=2026-03-01&ate=2026-03-31`

```json
{
  "grupo": "testes_desempenho",
  "periodo": { "de": "2026-03-01", "ate": "2026-03-31", "fusoHorario": "America/Sao_Paulo" },
  "equipeId": null,
  "volume": { "total": 41, "criadosNoPeriodo": 12, "alteradosNoPeriodo": 3, "porEntidade": [ ... ], "porEquipe": [ ... ], "criadosPorMes": [ ... ] },
  "aprovacao": { "diasPendenteAntigo": 7, "total": { ... }, "porEntidade": [ ... ] },
  "avaliacao": { "minimoAvaliacoesRanking": 3, "porEntidade": [ ... ] },
  "atividade": [ { "usuarioId": "…", "matricula": "c123456", "nome": "Ana", "criacoes": 8, "alteracoes": 2, "total": 10 } ]
}
```

### Volume (RN81)

| Campo | Fórmula |
|---|---|
| `porEntidade[].total` | registros que existem hoje (estado atual) |
| `porEntidade[].criadosNoPeriodo` | registros com `criadoEm` no período |
| `porEntidade[].alteradosNoPeriodo` | registros com **pelo menos uma** ALTERACAO no histórico dentro do período (não só a última) |
| `porEquipe[]` | `{ equipeId, nome, quantidade }`: total por equipe, somando as entidades por equipe |
| `criadosPorMes[]` | `{ mes: "2026-03", quantidade }`: criados no período, pelo mês **no fuso de negócio** |
| `total`, `criadosNoPeriodo`, `alteradosNoPeriodo` | a soma das entidades |

### Aprovação (RN82)

Só aparece onde a aprovação está **ativa** (entidade por equipe + equipe com aprovação); senão `"aprovacao": null`. `total` e cada
`porEntidade[].numeros` têm:

| Campo | Fórmula |
|---|---|
| `pendentes`, `aprovados`, `rejeitados` | registros em cada situação (estado atual) |
| `pendentesAntigos` | pendentes criados há mais de `diasPendenteAntigo` dias (`Painel:DiasPendenteAntigo`, padrão 7) |
| `tempoMedioPrimeiraDecisaoHoras` | ver abaixo |
| `primeirasDecisoes` | quantos registros entraram no tempo médio |
| `aprovacoes`, `rejeicoes` | decisões (APROVACAO, REJEICAO) do histórico com data no período, **todas** |
| `taxaRejeicao` | `rejeicoes / (aprovacoes + rejeicoes)`, de 0 a 1 (4 casas). Sem decisões: `null` |

**Tempo médio até a primeira decisão**: para cada registro, a data da **primeira** APROVACAO ou REJEICAO no histórico (um registro
rejeitado, corrigido e aprovado tem duas decisões; vale a primeira). Entram os registros cuja primeira decisão caiu no período:

```
tempo do registro = primeira decisão − criadoEm
tempoMedioPrimeiraDecisaoHoras = média dos tempos, em horas (2 casas)
```

Ex.: um registro decidido 2 h depois de criado e outro 4 h depois → `3.0`. No `total` do grupo é a média de **todos** os tempos juntos
(cada entidade pesa pelo número de registros).

### Avaliação (RN83)

Só nas entidades com **modelo de avaliação ativo**; senão `"avaliacao": null`. Para cada critério de **estrelas** ativo:

```json
{
  "criterio": "qualidade", "nome": "Qualidade", "media": 3.5, "quantidade": 8,
  "distribuicao": { "1": 0, "2": 3, "3": 1, "4": 1, "5": 3 },
  "melhores": [ { "id": "…", "rotulo": "Bateria 1", "media": 4.0, "quantidade": 3 } ],
  "piores": [ { "id": "…", "rotulo": "Bateria 2", "media": 2.0, "quantidade": 3 } ]
}
```

| Campo | Fórmula |
|---|---|
| `media` | média de **todas** as notas do critério (2 casas) |
| `quantidade`, `distribuicao` | número de notas e quantas de cada valor (1 a 5) |
| `melhores` / `piores` | **ranking**: a média das notas de **cada item**; entram só os itens com pelo menos `minimoAvaliacoesRanking` notas (`Painel:MinimoAvaliacoesRanking`, padrão 3). Os 5 de maior média (empate: mais notas) e os 5 de menor média (empate: mais notas) |

Ex.: itens com notas (5, 4, 3), (2, 2, 2) e (5, 5): `media` = 28 / 8 = 3,5; o terceiro tem média 5, mas só 2 notas, e fica **fora** do
ranking.

### Atividade (RN84)

Os 10 usuários que mais **criaram** e **alteraram** registros do grupo no período (pelo histórico), com a mesma visibilidade dos números.
Ordem: `total` decrescente, depois a matrícula.

## Agregação configurável

`GET /api/v1/painel/grupos/{grupo}/entidades/{entidade}/indicadores`

| Parâmetro | O quê |
|---|---|
| `agruparPor` | até **2** dimensões, separadas por vírgula (tabela abaixo). Sem ele: **sempre** uma linha só, com o total (sem registros: `contagem` 0) |
| `metricas` | até **5**: `contagem` (padrão), `media:<campo>`, `soma:<campo>`, `minimo:<campo>`, `maximo:<campo>` (INTEIRO e DECIMAL; o campo pode ser um [caminho](#caminhos-por-referência-rn89)), `media_avaliacao:<criterio>` (estrelas) |
| `ordenarPor` | chave de uma dimensão ou métrica (inclusive `contagem`); `-` na frente = decrescente. Padrão: pelas dimensões |
| `limite` | máximo de linhas (padrão 100, máximo 500). Se havia mais: `"truncado": true` |
| `de`, `ate` | período da **data de criação** (RN80) |
| os filtros da listagem | `chave=valor`, `chave.contem=`, `chave.de=`, `chave.ate=`, `equipeId`, `situacaoAprovacao`, `avaliacao.<criterio>.de` ([README](README.md#paginação-filtros-e-ordenação)), também por [caminho](#caminhos-por-referência-rn89) (`relatorio_teste.tipo_teste=Carga`). `pagina`, `tamanho` e `ordenacao` não se aplicam (400) |

### Dimensões (RN85)

| Dimensão | Valor na linha |
|---|---|
| campo LISTA | `"POST"` |
| campo BOOLEANO | `true` / `false` |
| campo REFERENCIA | `{ "id": "…", "rotulo": "Homologação" }` |
| campo TEXTO | `"Bateria 1"`; só os **50 mais frequentes** viram grupos, o resto vem como `"Outros"` (por último) |
| campo DATA ou DATA_HORA com período | `:dia` → `"2026-03-15"`, `:mes` → `"2026-03"`, `:trimestre` → `"2026-T1"`, `:ano` → `"2026"` |
| `equipe` | `{ "id": "…", "rotulo": "Equipe A" }` (só em entidade por equipe) |
| `situacaoAprovacao` | `"PENDENTE"`, `"APROVADO"`, `"REJEITADO"` |
| `criadoEm:<período>` | como as datas, no fuso de negócio |
| `<referencia>.<campo>`, `<referencia>.<referencia>.<campo>` | um campo de um registro **apontado**, até 2 saltos ([caminhos](#caminhos-por-referência-rn89)). Ex.: em `cenario_teste`, `relatorio_teste.ambiente` |

Registros sem valor na dimensão formam um grupo com valor `null` (que vem primeiro na ordem crescente).

### Caminhos por referência (RN89)

Onde se escreve a chave de um campo (dimensão, campo de métrica, filtro), pode-se escrever um **caminho** que atravessa até **2** campos
REFERENCIA. Em `cenario_teste` (as medidas), os campos do relatório estão a um salto e os do projeto do relatório, a dois:

| Caminho | Chega em |
|---|---|
| `relatorio_teste.ambiente` | o ambiente do relatório do cenário |
| `relatorio_teste.data_execucao:mes` | o mês de execução do relatório (dimensão) |
| `relatorio_teste.tipo_teste=Carga` | filtro: cenários de relatórios do tipo Carga |
| `relatorio_teste.data_execucao.de=2026-01-01` | filtro com operador: o último trecho é o operador |
| `endpoint_servico.projeto` | o projeto do endpoint do cenário |
| `relatorio_teste.projeto.sigla` | a sigla do projeto do relatório (2 saltos) |

- Cada salto precisa ser um campo REFERENCIA **ativo**; o último campo segue as regras do tipo (dimensão, métrica ou operador do filtro).
  Caminho inválido: 400 `VALIDACAO`, com todos os erros juntos, na chave do parâmetro (`agruparPor`, `metricas` ou a do filtro).
- O nome na resposta diz o caminho: `"Ambiente (Relatório)"`, `"Sigla (Relatório › Projeto)"`.
- As regras de equipe e aprovação valem para a entidade **consultada**; os registros apontados são da mesma equipe ou globais (RN29).
- Métrica por caminho é calculada **por registro consultado**: dois cenários do mesmo relatório contam o valor do relatório duas vezes.
- **Descobrir os caminhos**: em `GET /api/v1/grupos/{grupo}/entidades`, cada campo REFERENCIA traz `entidadeReferenciada` (a chave da
  entidade apontada); os campos dela são os alcançáveis por mais um salto.
- **Período sobre uma data de usuário**: `de`/`ate` filtram a criação. Para limitar pela data de execução, use os filtros do campo, também
  por caminho: `relatorio_teste.data_execucao.de=2026-01-01&relatorio_teste.data_execucao.ate=2026-06-30` (data sem hora = o dia inteiro
  em UTC, RN18, igual ao agrupamento `:mes` dessa data, RN80).

### Métricas (RN86)

- **`contagem` conta registros** e vem em **toda** linha, pedida ou não.
- **`media`, `minimo` e `maximo` ignoram os registros sem valor** no campo: a média de 10, 30 e "vazio" é 20 (e a contagem é 3). Grupo
  sem nenhum valor: `null`. A `soma` sem valores é `0`.
- **`media_avaliacao:<criterio>`** é a média das **médias de cada item** (um item com 50 notas pesa igual a um com 1); itens sem nota não
  entram.
- Números com até 4 casas.
- Sem `agruparPor` e sem registros: `[{ "contagem": 0, "media:...": null, "soma:...": 0 }]` (a linha do total sempre existe).

### Resposta

```json
{
  "dimensoes": [
    { "chave": "relatorio_teste.ambiente", "nome": "Ambiente (Relatório)", "tipo": "REFERENCIA", "periodo": null },
    { "chave": "relatorio_teste.data_execucao:mes", "nome": "Data de execução (Relatório) (mês)", "tipo": "DATA_HORA", "periodo": "mes" }
  ],
  "metricas": [ { "chave": "media:vazao_media_tps", "nome": "Média de Vazão média (TPS)" } ],
  "linhas": [
    { "relatorio_teste.ambiente": { "id": "…", "rotulo": "Homologação" }, "relatorio_teste.data_execucao:mes": "2026-03", "contagem": 2, "media:vazao_media_tps": 15 },
    { "relatorio_teste.ambiente": { "id": "…", "rotulo": "Homologação" }, "relatorio_teste.data_execucao:mes": "2026-04", "contagem": 1, "media:vazao_media_tps": 30 }
  ],
  "truncado": false,
  "periodo": { "de": null, "ate": null, "fusoHorario": "America/Sao_Paulo" }
}
```

Cada linha é um objeto com uma propriedade por dimensão e por métrica, **pela chave**, e sempre `contagem`. `dimensoes` e `metricas`
dizem o nome (para a legenda) e o tipo de cada coluna.

### Erros

Todos os erros de todos os parâmetros (agregação, filtros, período) vêm **juntos** num 400 `VALIDACAO`, na chave do parâmetro:

```json
{
  "status": 400, "codigo": "VALIDACAO",
  "errors": {
    "agruparPor": ["'vazao_media_tps' é DECIMAL: dimensões aceitam LISTA, BOOLEANO, REFERENCIA, TEXTO, ou DATA e DATA_HORA com período."],
    "metricas": ["'media:relatorio_teste': 'relatorio_teste' é REFERENCIA; media só vale para INTEIRO e DECIMAL."],
    "limite": ["Deve ser um número inteiro de 1 a 500."]
  }
}
```

## Exemplos: Testes de Desempenho

As medidas (`vazao_media_tps`, `percentil_95_ms`, `taxa_erro_percentual`) são do **cenário** (`cenario_teste`); o ambiente, o projeto,
o status, o tipo e a data de execução são do **relatório** (`relatorio_teste`). Por isso as consultas de medidas agrupam e filtram por
**caminhos** `relatorio_teste.<campo>`.

**Relatórios por status e por mês de execução**

```
GET /api/v1/painel/grupos/testes_desempenho/entidades/relatorio_teste/indicadores
    ?agruparPor=status_resultado,data_execucao:mes
```

Linhas: `{ "status_resultado": { "id", "rotulo": "Sucesso" }, "data_execucao:mes": "2026-03", "contagem": 12 }`. Para o gráfico de linhas
por mês, de um status só: `?status_resultado=<id>&agruparPor=data_execucao:mes`.

**Média de vazão e percentil 95 por ambiente**

```
GET /api/v1/painel/grupos/testes_desempenho/entidades/cenario_teste/indicadores
    ?agruparPor=relatorio_teste.ambiente
    &metricas=media:vazao_media_tps,media:percentil_95_ms,maximo:percentil_95_ms
    &ordenarPor=-media:vazao_media_tps
```

`media:percentil_95_ms` é a média dos percentis 95 dos cenários (o percentil de cada cenário é um campo; o painel não calcula percentis).

**Taxa de erro média por projeto, só testes de Carga executados no 1º semestre**

```
GET /api/v1/painel/grupos/testes_desempenho/entidades/cenario_teste/indicadores
    ?agruparPor=relatorio_teste.projeto&metricas=media:taxa_erro_percentual,contagem&ordenarPor=-media:taxa_erro_percentual
    &relatorio_teste.tipo_teste=Carga
    &relatorio_teste.data_execucao.de=2026-01-01&relatorio_teste.data_execucao.ate=2026-06-30
```

Com equipe: `&equipeId=<id>`. `de`/`ate` filtrariam a **criação** do cenário, não a execução.

**Evolução mensal do percentil 95 (pela execução do relatório)**

```
GET /api/v1/painel/grupos/testes_desempenho/entidades/cenario_teste/indicadores
    ?agruparPor=relatorio_teste.data_execucao:mes&metricas=media:percentil_95_ms,media:tempo_medio_resposta_ms
```

## Exportação (RN88)

`GET .../indicadores/exportar?formato=csv|xlsx&<os mesmos parâmetros>` devolve **as mesmas linhas** (inclusive o `limite`) num arquivo
`<entidade>_indicadores_<aaaa-mm-dd>.<formato>`. Colunas: as dimensões (REFERENCIA e equipe pelo rótulo), `Contagem` e as métricas, com os
nomes de `dimensoes`/`metricas`. Mesmo formato e proteção contra fórmula da exportação da listagem (CSV UTF-8 com BOM, `;`, vírgula
decimal).

## Configuração

| Chave | Padrão | O quê |
|---|---|---|
| `Painel:FusoHorario` | `America/Sao_Paulo` | fuso de negócio (RN80) |
| `Painel:DiasPendenteAntigo` | `7` | a partir de quantos dias um pendente é "antigo" (RN82) |
| `Painel:MinimoAvaliacoesRanking` | `3` | notas mínimas para entrar no ranking (RN83) |

## Desempenho

Sem índices novos. O teste lento `DesempenhoPainelTests` (fora do `dotnet test` padrão, ver [`tests/AGENTS.md`](../../backend/tests/AGENTS.md))
gera 20 mil relatórios e 20 mil cenários e mede três agregações de 2 dimensões pela API: de **0,16 s a 0,4 s** no SQL Server local do
Docker. Se passar de 2 s com dados reais, a proposta de índices vai para `database/propostas/` (sem aplicar) e vira um ADR.
