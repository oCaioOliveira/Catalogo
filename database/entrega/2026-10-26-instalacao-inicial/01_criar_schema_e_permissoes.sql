/* =====================================================================
   CATÁLOGO — Instalação inicial — Script 01 de 03: schema e permissões
   ---------------------------------------------------------------------
   - Cria o schema catalogo (proprietário: dbo), onde ficarão TODOS os objetos do Catálogo.
   - Cria o papel de banco catalogo_aplicacao, com SELECT, INSERT, UPDATE, DELETE e EXECUTE no schema catalogo, e só isso:
     nenhuma permissão de DDL (CREATE, ALTER, DROP) e nenhum acesso a objetos fora do schema catalogo.
   - NÃO cria login, usuário nem senha. A associação do login da aplicação ao papel fica no trecho comentado do final, com
     marcador, para o DBA ajustar ao padrão da casa.

   Execução: SSMS (ou sqlcmd), conectado ao banco do Catálogo, com um usuário db_owner (ou com CREATE SCHEMA e CREATE ROLE).
   Não usa variáveis de SQLCMD. Tempo estimado: menos de 1 segundo.
   Proteção: se o schema catalogo já existir e tiver qualquer objeto, o script para, sem alterar nada (pode ser de outro
   sistema). Se o schema existir vazio, ele é reaproveitado. Se o papel já existir, só as permissões são reaplicadas.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

PRINT '01_criar_schema_e_permissoes: verificando pré-requisitos...';
IF SCHEMA_ID(N'catalogo') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(N'catalogo'))
BEGIN
    RAISERROR (N'01_criar_schema_e_permissoes: o schema catalogo já existe e tem objetos. Nada foi alterado. Se for uma instalação anterior do Catálogo, não é preciso executar de novo; se for de outro sistema, contate a equipe do Catálogo antes de continuar.', 16, 1);
    SET NOEXEC ON;
END;
GO

BEGIN TRANSACTION;

IF SCHEMA_ID(N'catalogo') IS NULL
BEGIN
    EXEC (N'CREATE SCHEMA catalogo AUTHORIZATION dbo;');
    PRINT '01_criar_schema_e_permissoes: 1/2 schema catalogo criado.';
END
ELSE
    PRINT '01_criar_schema_e_permissoes: 1/2 schema catalogo já existia, vazio: reaproveitado.';

IF DATABASE_PRINCIPAL_ID(N'catalogo_aplicacao') IS NULL
BEGIN
    EXEC (N'CREATE ROLE catalogo_aplicacao AUTHORIZATION dbo;');
    PRINT '01_criar_schema_e_permissoes: 2/2 papel catalogo_aplicacao criado.';
END
ELSE
    PRINT '01_criar_schema_e_permissoes: 2/2 papel catalogo_aplicacao já existia: permissões reaplicadas.';

GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::catalogo TO catalogo_aplicacao;

COMMIT TRANSACTION;
PRINT '01_criar_schema_e_permissoes: concluído. Permissões do papel catalogo_aplicacao: SELECT, INSERT, UPDATE, DELETE e EXECUTE no schema catalogo.';
GO

SET NOEXEC OFF;
GO

/* ---------------------------------------------------------------------
   Associação do login da aplicação ao papel (executar à parte, depois de ajustar)
   ---------------------------------------------------------------------
   Substitua <LOGIN_DA_APLICACAO> pelo login definido pelos DBAs, conforme o padrão da casa:
     - login SQL:      [usuario_catalogo]
     - login de domínio: [DOMINIO\conta_servico_catalogo]
   O login em si (e a sua senha, se for SQL) é criado pelos DBAs, fora deste pacote. A senha NUNCA vai em script.
   Se o usuário já existir no banco, pule o CREATE USER.

   CREATE USER [<LOGIN_DA_APLICACAO>] FOR LOGIN [<LOGIN_DA_APLICACAO>] WITH DEFAULT_SCHEMA = catalogo;
   ALTER ROLE catalogo_aplicacao ADD MEMBER [<LOGIN_DA_APLICACAO>];

   Conferência (deve listar só as cinco permissões no schema catalogo, pelo papel):
   SELECT r.name AS papel, p.permission_name, p.class_desc, SCHEMA_NAME(p.major_id) AS schema_alvo
   FROM sys.database_role_members m
   JOIN sys.database_principals r ON r.principal_id = m.role_principal_id
   JOIN sys.database_permissions p ON p.grantee_principal_id = r.principal_id
   WHERE m.member_principal_id = DATABASE_PRINCIPAL_ID(N'<LOGIN_DA_APLICACAO>');
   --------------------------------------------------------------------- */
