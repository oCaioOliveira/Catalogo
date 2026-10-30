/* =====================================================================
   CATÁLOGO — Pacote 2026-10-30-chave-jwt — 01_chave_jwt.sql (versão 18 da estrutura)
   ---------------------------------------------------------------------
   Cria a tabela cat.CATTB025_CHAVE_JWT, onde a aplicação guarda a chave de assinatura dos tokens de acesso (JWT). A tabela nasce
   VAZIA: a própria aplicação gera uma chave aleatória na primeira inicialização e a grava; nas inicializações seguintes, lê a mesma.
   O mesmo conteúdo do script versionado database/scripts/V018__chave_jwt.sql, com as proteções do pacote.

   Pré-requisito: a estrutura do Catálogo no schema cat (cat.CATTB023_VERSAO_SCHEMA na versão 13 ou maior).
   Execução: SSMS (ou sqlcmd), conectado ao banco do Catálogo (CATDB001). Não usa variáveis de SQLCMD nem USE.
   Compatível com SQL Server 2014 (nível 120). Tempo estimado: menos de 1 segundo.
   Permissões: o papel catalogo_aplicacao já tem SELECT, INSERT, UPDATE, DELETE e EXECUTE no schema cat; nenhum GRANT novo.
   Proteção: para, sem alterar nada, se a estrutura não estiver instalada. Se a versão 18 já estiver registrada, não faz nada:
   pode ser executado de novo. Tudo roda numa transação: ou cria a tabela e registra a versão, ou nada.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '01_chave_jwt: verificando pré-requisitos...';
IF OBJECT_ID(N'cat.CATTB023_VERSAO_SCHEMA', N'U') IS NULL OR OBJECT_ID(N'cat.CATTB001_GRUPO', N'U') IS NULL
BEGIN
    RAISERROR (N'01_chave_jwt: a estrutura do Catálogo não está instalada no schema cat. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF (SELECT MAX(NU_NSU_VERSAO) FROM cat.CATTB023_VERSAO_SCHEMA) < 13
BEGIN
    RAISERROR (N'01_chave_jwt: este script exige a estrutura na versão 13 ou maior (consulte cat.CATTB023_VERSAO_SCHEMA). Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF EXISTS (SELECT 1 FROM cat.CATTB023_VERSAO_SCHEMA WHERE NU_NSU_VERSAO = 18)
BEGIN
    PRINT '01_chave_jwt: a versão 18 já está registrada (cat.CATTB025_CHAVE_JWT). Nada a fazer.';
    SET NOEXEC ON;
END;
GO

BEGIN TRANSACTION;

PRINT '01_chave_jwt: 1/2 criando cat.CATTB025_CHAVE_JWT...';
IF OBJECT_ID(N'cat.CATTB025_CHAVE_JWT', N'U') IS NULL
BEGIN
    CREATE TABLE cat.CATTB025_CHAVE_JWT
    (
        NU_NSU_CHAVE_JWT uniqueidentifier NOT NULL,
        CO_CHAVE_JWT     varchar(200)     COLLATE Latin1_General_CI_AI NOT NULL,
        DH_CRIADO_CJ     datetime2        NOT NULL CONSTRAINT DF_ChaveJwt_CriadoEm DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ChaveJwt PRIMARY KEY (NU_NSU_CHAVE_JWT),
        CONSTRAINT CK_ChaveJwt_Tamanho CHECK (LEN(CO_CHAVE_JWT) >= 32)
    );
END
ELSE
    PRINT '01_chave_jwt: a tabela já existia; só a versão será registrada.';

PRINT '01_chave_jwt: 2/2 registrando a versão 18...';
INSERT cat.CATTB023_VERSAO_SCHEMA (NU_NSU_VERSAO, DE_ARQUIVO_VS, DE_OBSERVACAO_VS)
VALUES (18, N'V018__chave_jwt.sql', N'Pacote 2026-10-30-chave-jwt (01_chave_jwt.sql).');

COMMIT TRANSACTION;
PRINT '01_chave_jwt: concluído (cat.CATTB025_CHAVE_JWT criada e versão 18 registrada).';
GO

SET NOEXEC OFF;
GO
