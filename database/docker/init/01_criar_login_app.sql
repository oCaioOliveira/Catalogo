-- Cria o login/usuário listagem_app, usado pela APLICAÇÃO (nunca use o sa na aplicação).
-- Permissões: só as do papel catalogo_aplicacao (SELECT, INSERT, UPDATE, DELETE e EXECUTE no schema catalogo,
-- criado pelo V012). NÃO pode alterar o schema nem ler nada fora de catalogo.
-- Idempotente: se o login já existe, apenas sincroniza a senha com o .env.
-- Variáveis sqlcmd: $(DB_NAME), $(APP_DB_PASSWORD)

USE [master];
GO

IF SUSER_ID(N'listagem_app') IS NULL
BEGIN
    PRINT 'Criando login listagem_app...';
    CREATE LOGIN [listagem_app] WITH PASSWORD = N'$(APP_DB_PASSWORD)',
        DEFAULT_DATABASE = [$(DB_NAME)], CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
END
ELSE
BEGIN
    PRINT 'Login listagem_app já existe; sincronizando senha.';
    ALTER LOGIN [listagem_app] WITH PASSWORD = N'$(APP_DB_PASSWORD)';
END
GO

USE [$(DB_NAME)];
GO

IF DATABASE_PRINCIPAL_ID(N'listagem_app') IS NULL
BEGIN
    PRINT 'Criando usuário listagem_app no banco $(DB_NAME)...';
    CREATE USER [listagem_app] FOR LOGIN [listagem_app] WITH DEFAULT_SCHEMA = [catalogo];
END
GO

-- Banco novo: o papel ainda não existe; o V012 o cria e põe o listagem_app nele.
-- Banco já no V012 (usuário recriado, por exemplo): só repõe o vínculo. ADD MEMBER é seguro de repetir.
IF DATABASE_PRINCIPAL_ID(N'catalogo_aplicacao') IS NOT NULL
    ALTER ROLE [catalogo_aplicacao] ADD MEMBER [listagem_app];
GO
