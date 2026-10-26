/* ---------------------------------------------------------------------
   Descrição canônica da ESTRUTURA do schema catalogo, uma linha por atributo, para comparar dois bancos (comparar_bancos.sh)
   ---------------------------------------------------------------------
   Só lê os catálogos do sistema: sys.tables, sys.columns, sys.indexes (+ index_columns), sys.check_constraints,
   sys.foreign_keys (+ foreign_key_columns) e sys.default_constraints. Tudo por NOME (nunca por object_id), para que
   dois bancos com a mesma estrutura deem exatamente as mesmas linhas.
   --------------------------------------------------------------------- */
SET NOCOUNT ON;
DECLARE @schema int = SCHEMA_ID(N'catalogo');

SELECT Linha COLLATE Latin1_General_BIN2 AS Linha FROM (
    SELECT CONCAT(N'TABELA|', t.name) AS Linha
    FROM sys.tables t WHERE t.schema_id = @schema

    UNION ALL
    SELECT CONCAT(N'COLUNA|', t.name, N'|', c.column_id, N'|', c.name, N'|', TYPE_NAME(c.user_type_id), N'|max=', c.max_length,
                  N'|prec=', c.precision, N'|esc=', c.scale, N'|nulo=', CAST(c.is_nullable AS int), N'|identity=', CAST(c.is_identity AS int),
                  N'|collation=', COALESCE(c.collation_name, N'~'), N'|computada=', CAST(c.is_computed AS int))
    FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id WHERE t.schema_id = @schema

    UNION ALL
    SELECT CONCAT(N'INDICE|', t.name, N'|', i.name, N'|', i.type_desc, N'|unico=', CAST(i.is_unique AS int), N'|pk=', CAST(i.is_primary_key AS int),
                  N'|uq=', CAST(i.is_unique_constraint AS int), N'|filtro=', COALESCE(i.filter_definition, N'~'),
                  N'|chave=', STUFF((SELECT N',' + COL_NAME(ic.object_id, ic.column_id) + CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N'' END
                                     FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                                     ORDER BY ic.key_ordinal FOR XML PATH('')), 1, 1, N''),
                  N'|inclui=', COALESCE(STUFF((SELECT N',' + COL_NAME(ic.object_id, ic.column_id)
                                     FROM sys.index_columns ic WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
                                     ORDER BY COL_NAME(ic.object_id, ic.column_id) FOR XML PATH('')), 1, 1, N''), N'~'))
    FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id WHERE t.schema_id = @schema AND i.type > 0

    UNION ALL
    SELECT CONCAT(N'CHECK|', t.name, N'|', k.name, N'|coluna=', COALESCE(COL_NAME(k.parent_object_id, NULLIF(k.parent_column_id, 0)), N'~'),
                  N'|desligado=', CAST(k.is_disabled AS int), N'|nao_confiavel=', CAST(k.is_not_trusted AS int), N'|', k.definition)
    FROM sys.check_constraints k JOIN sys.tables t ON t.object_id = k.parent_object_id WHERE t.schema_id = @schema

    UNION ALL
    SELECT CONCAT(N'FK|', t.name, N'|', f.name, N'|-> ', OBJECT_SCHEMA_NAME(f.referenced_object_id), N'.', OBJECT_NAME(f.referenced_object_id),
                  N'|', STUFF((SELECT N',' + COL_NAME(fc.parent_object_id, fc.parent_column_id) + N'=' + COL_NAME(fc.referenced_object_id, fc.referenced_column_id)
                               FROM sys.foreign_key_columns fc WHERE fc.constraint_object_id = f.object_id
                               ORDER BY fc.constraint_column_id FOR XML PATH('')), 1, 1, N''),
                  N'|delete=', f.delete_referential_action_desc, N'|update=', f.update_referential_action_desc,
                  N'|desligada=', CAST(f.is_disabled AS int), N'|nao_confiavel=', CAST(f.is_not_trusted AS int))
    FROM sys.foreign_keys f JOIN sys.tables t ON t.object_id = f.parent_object_id WHERE t.schema_id = @schema

    UNION ALL
    SELECT CONCAT(N'DEFAULT|', t.name, N'|', d.name, N'|', COL_NAME(d.parent_object_id, d.parent_column_id), N'|', d.definition)
    FROM sys.default_constraints d JOIN sys.tables t ON t.object_id = d.parent_object_id WHERE t.schema_id = @schema

    UNION ALL
    SELECT CONCAT(N'OBJETO_FORA_DE_TABELA|', o.type_desc, N'|', o.name)
    FROM sys.objects o
    WHERE o.schema_id = @schema AND o.type NOT IN ('U', 'PK', 'UQ', 'F', 'C', 'D')
) x
ORDER BY Linha COLLATE Latin1_General_BIN2;
