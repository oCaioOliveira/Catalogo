-- Cria o banco da aplicação, se ainda não existir. Idempotente.
-- Variável sqlcmd: $(DB_NAME)
-- O banco herda a collation do servidor (MSSQL_COLLATION no .env).

IF DB_ID(N'$(DB_NAME)') IS NULL
BEGIN
    PRINT 'Criando banco $(DB_NAME)...';
    CREATE DATABASE [$(DB_NAME)];
END
ELSE
    PRINT 'Banco $(DB_NAME) já existe.';
GO
