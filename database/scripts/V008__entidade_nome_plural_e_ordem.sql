/* =====================================================================
   V008 — Nome no plural das entidades e nova ordem do menu de Testes de Desempenho
   ---------------------------------------------------------------------
   - Entidade.NomePlural nvarchar(150) NULL: o nome no plural, para títulos de listagem e menu ("Relatórios de Teste").
     Nulo = as telas usam o Nome. Novas entidades recebem o plural pela administração de metadados (API).
   - Preenche os plurais do grupo testes_desempenho (só onde ainda estão vazios: não sobrescreve um plural alterado pela API).
   - Ordem do menu: o Relatório de Teste (a tela inicial) primeiro. Principais: Relatórios de Teste, Projetos, Endpoints de
     Serviço, RTCs; cadastros de apoio: Comunidades, Siglas, Ambientes, Status de Resultado; depois as filhas (Cenários, Links),
     que não aparecem no menu.
   A Versao das entidades alteradas muda (AtualizadoEm). A API guarda os metadados em cache por até 10 minutos: reinicie-a.
   O script pode rodar de novo sem erro.
   ===================================================================== */

IF COL_LENGTH(N'dbo.Entidade', N'NomePlural') IS NULL
    ALTER TABLE dbo.Entidade ADD NomePlural nvarchar(150) NULL;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE e SET NomePlural = v.NomePlural, Ordem = v.Ordem, AtualizadoEm = SYSUTCDATETIME()
FROM dbo.Entidade e
JOIN dbo.Grupo g ON g.Id = e.GrupoId AND g.Chave = 'testes_desempenho'
JOIN (VALUES
        ('relatorio_teste',  N'Relatórios de Teste',   1),
        ('projeto',          N'Projetos',              2),
        ('endpoint_servico', N'Endpoints de Serviço',  3),
        ('rtc',              N'RTCs',                  4),
        ('comunidade',       N'Comunidades',           5),
        ('sigla',            N'Siglas',                6),
        ('ambiente',         N'Ambientes',             7),
        ('status_resultado', N'Status de Resultado',   8),
        ('cenario_teste',    N'Cenários de Teste',     9),
        ('link',             N'Links',                10)) v (Chave, NomePlural, Ordem)
     ON v.Chave = e.Chave
WHERE e.NomePlural IS NULL;

COMMIT TRANSACTION;
GO
