/* =====================================================================
   V019 — Login pelo Windows (AD): remove as colunas de senha de cat.CATTB008_USUARIO
   ---------------------------------------------------------------------
   Por quê: o login passou a ser a autenticação integrada do Windows (docs/adr/0054). A aplicação não guarda mais senha, não
   obriga troca de senha e não bloqueia por tentativas: as quatro colunas abaixo deixaram de ser usadas.
   O que faz: remove, de cat.CATTB008_USUARIO, as colunas
     CO_SENHA_HASH       (hash da senha)
     IC_TROCAR_SENHA     (troca obrigatória no próximo acesso)   e o padrão DF_Usuario_DeveTrocarSenha
     NU_TENTATIVA_FALHA  (tentativas erradas seguidas)           e o padrão DF_Usuario_TentativasFalhas e o CHECK CK_Usuario_TentativasFalhas
     DH_BLOQUEADO        (bloqueio por tentativas)
   Os padrões (DEFAULT) e o CHECK são achados pelo catálogo do sistema, não pelo nome: o script vale mesmo se o nome for outro.
   Nenhuma linha é excluída (RN31); matrícula, nome, perfil, ativo e último acesso continuam.
   No banco da CAIXA, o mesmo conteúdo vai no pacote entrega/2026-11-05-login-windows.
   Idempotente: coluna que já não existe é ignorada. Compatível com SQL Server 2014 (nível 120).
   No código: Usuario (Domain), UsuarioConfiguracao e a entrada "Usuario" do NomesBanco.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

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
    PRINT 'V019: CHECK ' + @nome + ' removido.';
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
    PRINT 'V019: padrão ' + @nome + ' removido.';
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
    PRINT 'V019: coluna ' + @nome + ' removida.';
    FETCH NEXT FROM cols INTO @nome;
END
CLOSE cols;
DEALLOCATE cols;
GO
