# 0031. Agregação no banco, em LINQ, por LEFT JOIN com os valores tipados

- Status: Aceito
- Data: 2026-10-21

## Contexto

Os indicadores (ADR 0030) agregam milhares de registros. Ler os registros para a memória e somar em C# seria lento e gastaria memória
do servidor. As consultas são só LINQ (ADR 0011), e os valores dos campos estão em `RegistroValor`/`RegistroReferencia` (ADR 0013).
O SQL Server não aceita **subconsulta** dentro de `GROUP BY` nem de `AVG` (o EF Core recusa traduzir), que é como a listagem ordena por
campo.

## Decisão

- **Toda agregação é feita no banco**, numa consulta LINQ que o EF Core traduz para um SQL só, com `GROUP BY`, `COUNT`, `AVG`, `SUM`,
  `MIN` e `MAX`. Nada de `ToList`/`AsEnumerable` antes de agregar; para a memória vêm só as linhas já agregadas (no máximo 501).
- A consulta **parte da mesma consulta filtrada da listagem** (`ConsultaRegistrosLinq.Filtrar`), com o escopo de segurança (ADR 0030,
  RN79).
- Montagem por partes (`ConsultaIndicadoresLinq` e `MontadorLinhas`, na Infrastructure):
  1. para cada dimensão e cada métrica, um método por tipo devolve **um valor por registro** (LISTA → `ValorTexto`, DATA → ano/mês...);
  2. esses valores entram por **LEFT JOIN** (`GroupJoin` + `SelectMany(DefaultIfEmpty)`) numa linha por registro — cada registro tem no
     máximo um valor por campo, então o JOIN não multiplica linhas;
  3. `GROUP BY` pelas colunas das dimensões. Os períodos agrupam por **números** (`DATEPART` de ano, mês, dia), não por datas montadas.
- O número de JOINs depende do pedido, então a projeção é montada com `System.Linq.Expressions` num lugar só (`MontadorLinhas`); as
  partes específicas de cada tipo continuam sendo LINQ comum.
- Os 50 valores mais frequentes de um TEXTO são uma subconsulta (`GROUP BY` + `TOP`) dentro do mesmo SQL, ligada por LEFT JOIN.
- **Sem índices novos** por padrão. O teste lento `DesempenhoPainelTests` mede a agregação com 20 mil relatórios (e um cenário por
  relatório); a proposta de índices fica para quando passar de 2 s (em `database/propostas/`, sem aplicar).

### Alternativas descartadas

- **Subconsultas correlacionadas** (como na ordenação da listagem): não traduzem dentro de `GROUP BY`/`AVG`.
- **Ler e agregar em memória**: lento, e sem limite de memória.
- **SQL escrito à mão / view / procedure**: proibido pelo ADR 0011, e duplicaria o escopo de segurança.
- **Colunas computadas ou tabela de fatos**: mudança de schema e sincronização para cada campo novo; desnecessário no volume atual.

## Consequências

- ✅ A agregação custa o mesmo que uma listagem filtrada: no teste com 20 mil registros, de 0,16 s a 0,4 s (sem índices novos).
- ✅ O teste `Indicadores_DuasDimensoes_UmaConsultaComGroupByNoBanco` confere, pelo `ToQueryString()`, que sai um SQL só com `GROUP BY`.
- ⚠️ `MontadorLinhas` usa expressões montadas à mão: quem mexer precisa ler o comentário da classe. Uma métrica ou dimensão nova é um
  método novo em `ConsultaIndicadoresLinq`, sem mexer no montador.
- ⚠️ O tamanho do SQL cresce com o número de dimensões e métricas (um JOIN para cada).
