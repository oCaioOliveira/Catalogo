# 0013. Tabela de valores tipados RegistroValor

- Status: Aceito
- Data: 2026-10-21

## Contexto

Os campos simples ficam em `Registro.Dados` (JSON, [ADR 0001](0001-modelo-generico-eav-json.md)).
Filtrar e ordenar pelo valor tipado de um campo dentro do JSON exigiria `JSON_VALUE` + `CAST` em
SQL escrito à mão, o que é proibido ([ADR 0011](0011-consultas-somente-linq.md)). Ordenar o texto
do JSON daria ordem errada para números ("10" antes de "9.5") e datas.

## Decisão

- Nova tabela `dbo.RegistroValor` (script `V002`): uma linha por campo **simples preenchido**, com o
  valor numa coluna **tipada**:
  - TEXTO, TEXTO_LONGO e LISTA → `ValorTexto`;
  - INTEIRO e DECIMAL → `ValorNumero decimal(38,10)`;
  - DATA e DATA_HORA → `ValorData` (DATA_HORA em UTC);
  - BOOLEANO → `ValorBooleano`.
- REFERENCIA não entra (já está em `RegistroReferencia`). Campo nulo ou ausente não gera linha.
- FKs compostas no estilo de `RegistroReferencia` e CHECK de "exatamente um valor por linha".
- **Redundância controlada**: `Registro.Dados` continua sendo a fonte do que a API devolve. A
  `RegistroValor` é uma **projeção** para busca, ordenação, rótulos e, no futuro, agregação.
- **Regra de ouro**: os dois são gravados **sempre juntos, no mesmo `SaveChanges`**, pelo mesmo código
  (`SincronizadorRegistro`), com os valores já normalizados pelos validadores. A exclusão de um
  registro apaga os valores por CASCADE.
- O DECIMAL passa a ter limites que cabem em `decimal(38,10)` (RN22).
- Sem índices por enquanto.

## Consequências

- ✅ Filtros e ordenação tipados, só com LINQ.
- ⚠️ Os dados ficam duplicados. Qualquer gravação de `Registro.Dados` fora do `SincronizadorRegistro`
  (inclusive um script de correção) precisa atualizar a `RegistroValor` junto. O teste
  `RegistroValorTests` confere a consistência depois de criar, atualizar e excluir.
- ⚠️ Sem índices, filtros em tabelas grandes ficam lentos. Índices (ex.: `CampoId, ValorNumero`) virão num script próprio.
