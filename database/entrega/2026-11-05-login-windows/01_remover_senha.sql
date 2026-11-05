/* =====================================================================
   CATÁLOGO — Pacote 2026-11-05-login-windows — 01_remover_senha.sql (versão 19 da estrutura)
   ---------------------------------------------------------------------
   O login do Catálogo passa a ser a autenticação integrada do Windows (AD): a aplicação não guarda mais senha. Este script
   remove de cat.CATTB008_USUARIO as quatro colunas de senha, com os padrões (DEFAULT) e o CHECK delas:
     CO_SENHA_HASH, IC_TROCAR_SENHA, NU_TENTATIVA_FALHA e DH_BLOQUEADO.
   Nenhuma linha é excluída: matrícula, nome, perfil, situação (ativo) e último acesso dos usuários continuam como estão.
   O mesmo conteúdo do script versionado database/scripts/V019__remover_senha.sql, com as proteções do pacote.

   Pré-requisito: a estrutura do Catálogo no schema cat na versão 18 (pacote 2026-10-30-chave-jwt) ou maior.
   Execução: SSMS (ou sqlcmd), conectado ao banco do Catálogo (CATDB001). Não usa variáveis de SQLCMD nem USE.
   Compatível com SQL Server 2014 (nível 120). Tempo estimado: menos de 1 segundo.
   Permissões: quem executa precisa de ALTER na tabela cat.CATTB008_USUARIO. Nenhum GRANT novo.
   Proteção: para, sem alterar nada, se a estrutura não estiver na versão 18 ou maior. Se a versão 19 já estiver registrada, não
   faz nada: pode ser executado de novo. Tudo roda numa transação: ou remove as colunas e registra a versão, ou nada.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '01_remover_senha: verificando pré-requisitos...';
IF OBJECT_ID(N'cat.CATTB023_VERSAO_SCHEMA', N'U') IS NULL OR OBJECT_ID(N'cat.CATTB008_USUARIO', N'U') IS NULL
BEGIN
    RAISERROR (N'01_remover_senha: a estrutura do Catálogo não está instalada no schema cat. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF EXISTS (SELECT 1 FROM cat.CATTB023_VERSAO_SCHEMA WHERE NU_NSU_VERSAO = 19)
BEGIN
    PRINT '01_remover_senha: a versão 19 já está registrada (colunas de senha removidas). Nada a fazer.';
    SET NOEXEC ON;
END
ELSE IF (SELECT MAX(NU_NSU_VERSAO) FROM cat.CATTB023_VERSAO_SCHEMA) < 18
BEGIN
    RAISERROR (N'01_remover_senha: este script exige a estrutura na versão 18 ou maior (pacote 2026-10-30-chave-jwt; consulte cat.CATTB023_VERSAO_SCHEMA). Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END;
GO

BEGIN TRANSACTION;

PRINT '01_remover_senha: 1/2 removendo as colunas de senha de cat.CATTB008_USUARIO...';
DECLARE @tabela  int = OBJECT_ID(N'cat.CATTB008_USUARIO', N'U');
DECLARE @colunas TABLE (Nome sysname NOT NULL);
INSERT @colunas (Nome) VALUES (N'CO_SENHA_HASH'), (N'IC_TROCAR_SENHA'), (N'NU_TENTATIVA_FALHA'), (N'DH_BLOQUEADO');
DECLARE @sql nvarchar(max), @nome sysname;

-- 1. Os CHECK que citam alguma das colunas (CK_Usuario_TentativasFalhas).
DECLARE cks CURSOR LOCAL FAST_FORWARD FOR
    SELECT DISTINCT ck.name
    FROM sys.check_constraints ck
    JOIN sys.columns c ON c.object_id = ck.parent_object_id
    JOIN @colunas alvo ON alvo.Nome = c.name
    WHERE ck.parent_object_id = @tabela AND CHARINDEX(QUOTENAME(c.name), ck.definition) > 0;
OPEN cks;
FETCH NEXT FROM cks INTO @nome;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE cat.CATTB008_USUARIO DROP CONSTRAINT ' + QUOTENAME(@nome) + N';';
    EXEC sys.sp_executesql @sql;
    PRINT '01_remover_senha:    CHECK ' + @nome + ' removido.';
    FETCH NEXT FROM cks INTO @nome;
END
CLOSE cks;
DEALLOCATE cks;

-- 2. Os padrões (DEFAULT) das colunas (DF_Usuario_DeveTrocarSenha, DF_Usuario_TentativasFalhas).
DECLARE dfs CURSOR LOCAL FAST_FORWARD FOR
    SELECT dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    JOIN @colunas alvo ON alvo.Nome = c.name
    WHERE dc.parent_object_id = @tabela;
OPEN dfs;
FETCH NEXT FROM dfs INTO @nome;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE cat.CATTB008_USUARIO DROP CONSTRAINT ' + QUOTENAME(@nome) + N';';
    EXEC sys.sp_executesql @sql;
    PRINT '01_remover_senha:    padrão ' + @nome + ' removido.';
    FETCH NEXT FROM dfs INTO @nome;
END
CLOSE dfs;
DEALLOCATE dfs;

-- 3. As colunas.
DECLARE cols CURSOR LOCAL FAST_FORWARD FOR
    SELECT alvo.Nome FROM @colunas alvo WHERE COL_LENGTH(N'cat.CATTB008_USUARIO', alvo.Nome) IS NOT NULL;
OPEN cols;
FETCH NEXT FROM cols INTO @nome;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'ALTER TABLE cat.CATTB008_USUARIO DROP COLUMN ' + QUOTENAME(@nome) + N';';
    EXEC sys.sp_executesql @sql;
    PRINT '01_remover_senha:    coluna ' + @nome + ' removida.';
    FETCH NEXT FROM cols INTO @nome;
END
CLOSE cols;
DEALLOCATE cols;

PRINT '01_remover_senha: 2/2 registrando a versão 19...';
INSERT cat.CATTB023_VERSAO_SCHEMA (NU_NSU_VERSAO, DE_ARQUIVO_VS, DE_OBSERVACAO_VS)
VALUES (19, N'V019__remover_senha.sql', N'Pacote 2026-11-05-login-windows (01_remover_senha.sql).');

COMMIT TRANSACTION;
PRINT '01_remover_senha: concluído (colunas de senha removidas e versão 19 registrada).';
GO

SET NOEXEC OFF;
GO
