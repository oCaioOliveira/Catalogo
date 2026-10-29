/* =====================================================================
   V017 — Origem "MIGRACAO_SHAREPOINT" no histórico
   ---------------------------------------------------------------------
   Por quê: os relatórios de testes de desempenho migrados do SharePoint (docs/migracao-sharepoint.md) entram com uma linha de
   histórico de criação cuja origem diz de onde vieram, como a importação de planilhas faz com "IMPORTACAO" (V011).
   O que faz: recria o CK_HistoricoAlteracao_Origem aceitando também 'MIGRACAO_SHAREPOINT' (a coluna é varchar(20)).
   No banco da CAIXA, o mesmo ajuste vai na parte 01 da migração (entrega/2026-10-28-migracao-sharepoint).
   Idempotente: se o CHECK já aceita o valor novo, não faz nada. Compatível com SQL Server 2014.
   No código: OrigemHistorico.MigracaoSharePoint e o texto do CHECK em HistoricoAlteracaoConfiguracao.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = N'CK_HistoricoAlteracao_Origem'
                 AND parent_object_id = OBJECT_ID(N'cat.CATTB015_HISTORICO_ALTERACAO')
                 AND definition LIKE N'%MIGRACAO_SHAREPOINT%')
BEGIN
    BEGIN TRANSACTION;
    IF EXISTS (SELECT 1 FROM sys.check_constraints
               WHERE name = N'CK_HistoricoAlteracao_Origem' AND parent_object_id = OBJECT_ID(N'cat.CATTB015_HISTORICO_ALTERACAO'))
        ALTER TABLE cat.CATTB015_HISTORICO_ALTERACAO DROP CONSTRAINT CK_HistoricoAlteracao_Origem;
    ALTER TABLE cat.CATTB015_HISTORICO_ALTERACAO WITH CHECK ADD CONSTRAINT CK_HistoricoAlteracao_Origem
        CHECK (DE_ORIGEM_HA IS NULL OR DE_ORIGEM_HA IN ('IMPORTACAO', 'MIGRACAO_SHAREPOINT'));
    COMMIT TRANSACTION;
    PRINT 'V017: CK_HistoricoAlteracao_Origem aceita IMPORTACAO e MIGRACAO_SHAREPOINT.';
END
ELSE
    PRINT 'V017: CK_HistoricoAlteracao_Origem já aceita MIGRACAO_SHAREPOINT. Nada a fazer.';
GO
