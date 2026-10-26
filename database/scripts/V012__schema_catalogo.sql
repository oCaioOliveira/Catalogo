/* =====================================================================
   V012 — Schema próprio: todos os objetos do Catálogo passam de dbo para catalogo
   ---------------------------------------------------------------------
   Por quê: o banco de produção da CAIXA é compartilhado com outros sistemas, que têm tabelas de nomes genéricos em dbo
   (Usuario, Equipe, Registro...). O Catálogo fica inteiro no schema catalogo, em todos os ambientes, para não divergirem.

   - Cria o schema catalogo, se não existir.
   - Move as 23 tabelas do Catálogo (inclusive a __VersaoSchema) com ALTER SCHEMA ... TRANSFER: os dados, as chaves, os
     índices, os CHECKs e os DEFAULTs vão junto, sem cópia. Uma tabela que já está em catalogo é pulada; se ela existir nos
     DOIS schemas, o script para (THROW) e nada muda.
   - __VersaoSchema ganha a coluna Observacao (NULL): o pacote de instalação dos DBAs (database/entrega/) registra ali que as
     versões vieram da instalação consolidada.
   - Permissões: o papel catalogo_aplicacao recebe SELECT, INSERT, UPDATE, DELETE e EXECUTE no schema catalogo, e só isso
     (nenhum DDL). O usuário listagem_app (o login da aplicação no Docker local) entra no papel, sai de db_datareader e
     db_datawriter, perde o EXECUTE no banco inteiro e passa a ter catalogo como schema padrão. Em produção não existe
     listagem_app: o DBA associa o login da aplicação ao papel (database/entrega/).
   Tudo numa transação só (XACT_ABORT): ou move tudo, ou nada. O script pode rodar de novo sem erro.

   A partir daqui, todo script novo escreve catalogo. em todos os objetos (database/AGENTS.md).
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF SCHEMA_ID(N'catalogo') IS NULL
BEGIN
    EXEC (N'CREATE SCHEMA catalogo AUTHORIZATION dbo;');
    PRINT 'V012: schema catalogo criado.';
END;

DECLARE @Tabelas TABLE (Nome sysname NOT NULL PRIMARY KEY);
INSERT @Tabelas (Nome) VALUES
    (N'__VersaoSchema'),
    (N'Grupo'), (N'Entidade'), (N'Campo'), (N'CampoDependencia'), (N'RestricaoUnica'), (N'RestricaoUnicaCampo'),
    (N'Registro'), (N'RegistroReferencia'), (N'RegistroChaveUnica'), (N'RegistroValor'),
    (N'Equipe'), (N'Usuario'), (N'UsuarioEquipe'), (N'HistoricoAlteracao'),
    (N'ConfiguracaoEntidadeEquipe'),
    (N'ModeloAvaliacao'), (N'CriterioAvaliacao'), (N'Avaliacao'), (N'AvaliacaoResposta'),
    (N'GrupoEquipe'),
    (N'Importacao'), (N'ImportacaoArquivo');

DECLARE @Duplicada sysname = (
    SELECT TOP (1) Nome FROM @Tabelas
    WHERE OBJECT_ID(N'dbo.' + QUOTENAME(Nome), N'U') IS NOT NULL
      AND OBJECT_ID(N'catalogo.' + QUOTENAME(Nome), N'U') IS NOT NULL
    ORDER BY Nome);
IF @Duplicada IS NOT NULL
BEGIN
    DECLARE @Mensagem nvarchar(2048) = CONCAT(N'V012: a tabela ', @Duplicada,
        N' existe em dbo e em catalogo. Nada foi movido. Confira qual das duas tem os dados do Catálogo antes de continuar.');
    THROW 50012, @Mensagem, 1;
END;

DECLARE @Nome sysname, @Sql nvarchar(400), @Movidas int = 0;
DECLARE tabelas CURSOR LOCAL FAST_FORWARD FOR
    SELECT Nome FROM @Tabelas WHERE OBJECT_ID(N'dbo.' + QUOTENAME(Nome), N'U') IS NOT NULL ORDER BY Nome;
OPEN tabelas;
FETCH NEXT FROM tabelas INTO @Nome;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'ALTER SCHEMA catalogo TRANSFER dbo.' + QUOTENAME(@Nome) + N';';
    EXEC (@Sql);
    SET @Movidas += 1;
    FETCH NEXT FROM tabelas INTO @Nome;
END;
CLOSE tabelas;
DEALLOCATE tabelas;
PRINT CONCAT('V012: tabelas movidas de dbo para catalogo: ', @Movidas, '.');

IF COL_LENGTH(N'catalogo.__VersaoSchema', N'Observacao') IS NULL
    EXEC (N'ALTER TABLE catalogo.__VersaoSchema ADD Observacao nvarchar(500) NULL;');

IF DATABASE_PRINCIPAL_ID(N'catalogo_aplicacao') IS NULL
    EXEC (N'CREATE ROLE catalogo_aplicacao AUTHORIZATION dbo;');
GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::catalogo TO catalogo_aplicacao;

-- Só no Docker local (em produção o login da aplicação tem outro nome e é associado pelo DBA).
IF DATABASE_PRINCIPAL_ID(N'listagem_app') IS NOT NULL
BEGIN
    ALTER ROLE catalogo_aplicacao ADD MEMBER listagem_app;
    IF IS_ROLEMEMBER(N'db_datareader', N'listagem_app') = 1 ALTER ROLE db_datareader DROP MEMBER listagem_app;
    IF IS_ROLEMEMBER(N'db_datawriter', N'listagem_app') = 1 ALTER ROLE db_datawriter DROP MEMBER listagem_app;
    REVOKE EXECUTE FROM listagem_app;
    ALTER USER listagem_app WITH DEFAULT_SCHEMA = catalogo;
    PRINT 'V012: listagem_app agora só tem o papel catalogo_aplicacao.';
END;

COMMIT TRANSACTION;
GO
