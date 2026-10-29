/* =====================================================================
   CATÁLOGO — Migração dos relatórios de testes de desempenho (SharePoint) — 03: conferência
   ---------------------------------------------------------------------
   SÓ CONSULTAS: não altera nada. Compara o banco com o CSV do SharePoint (687 relatórios):
     1. as 9 partes registradas em cat.CATTB024_MIGRACAO_CONTROLE;
     2. a quantidade de itens por tipo de item da área testes_desempenho;
     3. os relatórios por resultado, por ambiente e por ano de execução.
   Cada linha mostra "OK" ou "DIVERGÊNCIA". A última consulta resume: a quantidade de divergências (o esperado é 0).
   GERADO por backend/tools/Catalogo.Migracao em 2026-10-28 23:34. Não edite à mão.
   ===================================================================== */

SET NOCOUNT ON;
DECLARE @Conferencia TABLE (Ordem int IDENTITY(1,1), Grupo nvarchar(60), Item nvarchar(200), Esperado int, Encontrado int);
DECLARE @Grupo uniqueidentifier = '57a926cd-8e83-4fff-add0-6a9c5411162b';

INSERT @Conferencia (Grupo, Item, Esperado, Encontrado)
SELECT N'Partes aplicadas', N'partes de 01 a 09', 9,
       CASE WHEN OBJECT_ID(N'cat.CATTB024_MIGRACAO_CONTROLE', N'U') IS NULL THEN 0
            ELSE (SELECT COUNT(*) FROM cat.CATTB024_MIGRACAO_CONTROLE WHERE CO_MIGRACAO = 'sharepoint-testes-desempenho' AND NU_PARTE BETWEEN 1 AND 9) END;

INSERT @Conferencia (Grupo, Item, Esperado, Encontrado)
SELECT N'Itens por tipo', e.Nome, e.Esperado,
       (SELECT COUNT(*) FROM cat.CATTB011_REGISTRO r WHERE r.NU_NSU_CE_ENTIDADE_REG = e.Id)
FROM (VALUES
    ('63c4ba1a-27f8-4459-8d6a-c6fb527d9780', N'Relatórios de Teste', 687),
    ('e7ca0ca6-24c8-47cf-bf57-1adde3c481aa', N'Projetos', 559),
    ('895bcf5c-66c7-4f8a-a7bd-78dcc373a351', N'Endpoints de Serviço', 1242),
    ('8189f1da-3f69-4a2d-b081-c857571e725e', N'RTCs', 662),
    ('d0c964da-6229-486f-ac19-d6548d548383', N'Comunidades', 32),
    ('332b6ecb-f049-495f-b85e-d98da46b94bc', N'Siglas', 129),
    ('209e0e8b-511e-4c4f-951e-b78cbd87de52', N'Ambientes', 9),
    ('fe97f789-793f-497c-beef-65e9b04d4b63', N'Status de Resultado', 2),
    ('4bb8007f-a74c-4f02-907a-83b1136e711c', N'Cenários de Teste', 1586),
    ('f5a7946b-6614-4b2e-966f-2fdf68e92e24', N'Links', 14)
) e (Id, Nome, Esperado);

INSERT @Conferencia (Grupo, Item, Esperado, Encontrado)
SELECT N'Relatórios por resultado', v.Valor, v.Esperado, ISNULL(b.Encontrado, 0)
FROM (VALUES
    (N'Insatisfatório', 227),
    (N'Satisfatório', 460)
) v (Valor, Esperado)
LEFT JOIN (
    SELECT x.Valor, COUNT(*) AS Encontrado
    FROM (SELECT v.DE_VALOR AS Valor FROM cat.CATTB011_REGISTRO r JOIN cat.CATTB012_REGISTRO_REFERENCIA rr ON rr.NU_REGISTRO_ORIGEM = r.NU_NSU_REGISTRO AND rr.NU_NSU_CE_CAMPO_RR = '05eb423e-f36b-1410-86bb-000558e25cb6' JOIN cat.CATTB014_REGISTRO_VALOR v ON v.NU_NSU_CE_REGISTRO_RV = rr.NU_REGISTRO_DESTINO AND v.NU_NSU_CE_CAMPO_RV = 'e9ea423e-f36b-1410-86bb-000558e25cb6' WHERE r.NU_NSU_CE_ENTIDADE_REG = '63c4ba1a-27f8-4459-8d6a-c6fb527d9780') x
    GROUP BY x.Valor
) b ON b.Valor = v.Valor;

INSERT @Conferencia (Grupo, Item, Esperado, Encontrado)
SELECT N'Relatórios por ambiente', v.Valor, v.Esperado, ISNULL(b.Encontrado, 0)
FROM (VALUES
    (N'Contingência PRD', 2),
    (N'DES', 502),
    (N'HMP', 21),
    (N'PLT', 4),
    (N'PRD', 49),
    (N'Piloto PRD', 12),
    (N'Pré-PRD', 1),
    (N'Sandbox', 12),
    (N'TQS', 84)
) v (Valor, Esperado)
LEFT JOIN (
    SELECT x.Valor, COUNT(*) AS Encontrado
    FROM (SELECT v.DE_VALOR AS Valor FROM cat.CATTB011_REGISTRO r JOIN cat.CATTB012_REGISTRO_REFERENCIA rr ON rr.NU_REGISTRO_ORIGEM = r.NU_NSU_REGISTRO AND rr.NU_NSU_CE_CAMPO_RR = 'feea423e-f36b-1410-86bb-000558e25cb6' JOIN cat.CATTB014_REGISTRO_VALOR v ON v.NU_NSU_CE_REGISTRO_RV = rr.NU_REGISTRO_DESTINO AND v.NU_NSU_CE_CAMPO_RV = 'd4ea423e-f36b-1410-86bb-000558e25cb6' WHERE r.NU_NSU_CE_ENTIDADE_REG = '63c4ba1a-27f8-4459-8d6a-c6fb527d9780') x
    GROUP BY x.Valor
) b ON b.Valor = v.Valor;

INSERT @Conferencia (Grupo, Item, Esperado, Encontrado)
SELECT N'Relatórios por ano de execução', v.Valor, v.Esperado, ISNULL(b.Encontrado, 0)
FROM (VALUES
    (N'2022', 10),
    (N'2023', 88),
    (N'2024', 221),
    (N'2025', 195),
    (N'2026', 173)
) v (Valor, Esperado)
LEFT JOIN (
    SELECT x.Valor, COUNT(*) AS Encontrado
    FROM (SELECT CONVERT(nvarchar(4), YEAR(DATEADD(HOUR, -3, v.DT_VALOR))) AS Valor FROM cat.CATTB011_REGISTRO r JOIN cat.CATTB014_REGISTRO_VALOR v ON v.NU_NSU_CE_REGISTRO_RV = r.NU_NSU_REGISTRO AND v.NU_NSU_CE_CAMPO_RV = '0ceb423e-f36b-1410-86bb-000558e25cb6' WHERE r.NU_NSU_CE_ENTIDADE_REG = '63c4ba1a-27f8-4459-8d6a-c6fb527d9780') x
    GROUP BY x.Valor
) b ON b.Valor = v.Valor;

SELECT Grupo, Item, Esperado, Encontrado, CASE WHEN Esperado = Encontrado THEN 'OK' ELSE 'DIVERGÊNCIA' END AS Situacao
FROM @Conferencia ORDER BY Ordem;

SELECT COUNT(*) AS Verificacoes, SUM(CASE WHEN Esperado = Encontrado THEN 0 ELSE 1 END) AS Divergencias,
       CASE WHEN SUM(CASE WHEN Esperado = Encontrado THEN 0 ELSE 1 END) = 0 THEN 'OK: o banco confere com o CSV.' ELSE 'DIVERGÊNCIA: veja as linhas acima.' END AS Resultado
FROM @Conferencia;
