# 0001. Modelo genérico EAV com JSON e referências em tabela própria

- Status: Aceito
- Data: 2026-10-21

## Contexto

O Catalogo precisa cadastrar tipos de item que ainda não conhecemos (Testes de Desempenho é só o
primeiro). Criar uma tabela e um CRUD por tipo exigiria código e deploy a cada listagem nova. Um
EAV clássico (uma linha por valor) deixa as consultas lentas e difíceis. Guardar tudo num JSON
perde a integridade dos relacionamentos.

## Decisão

- Os tipos de item são **metadados** (`Grupo`, `Entidade`, `Campo`...), e o motor genérico valida pelos metadados.
- Os **campos simples** de um registro ficam num único JSON (`Registro.Dados`, `nvarchar(max)` com `CHECK ISJSON`).
- Os **relacionamentos** (campos `REFERENCIA`) ficam em `RegistroReferencia`, com **FKs compostas
  reais**, que garantem no banco a entidade de origem e a de destino.

## Consequências

- ✅ Uma listagem nova é só um script de metadados (`V00x`), sem código novo.
- ✅ A integridade referencial continua garantida pelo banco.
- ⚠️ Filtrar por campos simples exige `JSON_VALUE`. Se for preciso, use índices em colunas computadas (futuro).
- ⚠️ Toda validação de tipo passa a ser responsabilidade da aplicação (RN04).
- ⚠️ A unicidade não pode ser um UNIQUE direto sobre colunas (ver [0005](0005-unicidade-por-hash.md)).
