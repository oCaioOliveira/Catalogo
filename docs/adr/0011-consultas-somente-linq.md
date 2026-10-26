# 0011. Consultas ao banco somente via EF Core em LINQ

- Status: Aceito
- Data: 2026-10-21

## Contexto

A listagem tem filtros e ordenação dinâmicos, montados a partir da URL. Montar SQL à mão para isso
abre espaço para SQL injection e espalha consultas fora do mapeamento do EF. O time é júnior.

## Decisão

- Toda consulta da aplicação é feita pelo **EF Core em LINQ**. Proibidos: `SqlQueryRaw`, `FromSql*`,
  `ExecuteSql*` e Dapper.
- Os valores vindos da URL só chegam ao banco como **parâmetros** gerados pelo EF. Os `CampoId` usados nos
  filtros vêm sempre dos metadados.
- Os scripts de `database/scripts/` continuam em SQL: eles definem o schema, não são consultas da aplicação.
- Garantido pelos testes `SemSqlManualTests` (varre `backend/src`) e `PacotesTests` (Dapper proibido).

## Consequências

- ✅ SQL injection fica fora de alcance por construção.
- ✅ Consultas tipadas, que quebram na compilação quando o modelo muda.
- ⚠️ Filtrar e ordenar dentro do JSON não é possível em LINQ. Por isso existe a `RegistroValor` ([ADR 0013](0013-registro-valor.md)).
