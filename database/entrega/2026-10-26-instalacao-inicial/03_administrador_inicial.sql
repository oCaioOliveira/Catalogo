/* =====================================================================
   CATÁLOGO — Instalação inicial — Script 03 de 03: administrador inicial
   ---------------------------------------------------------------------
   Cadastra o primeiro administrador do Catálogo:
     - matrícula C161587, nome "Caio", perfil ADMINISTRADOR, ativo, sem equipe;
     - senha PROVISÓRIA, gravada SOMENTE como hash (PBKDF2, o mesmo da aplicação): a senha não está neste script;
     - DeveTrocarSenha = 1: no primeiro acesso a aplicação exige a troca da senha, antes de qualquer outra ação.
   Também grava a linha de criação no histórico de alterações, como a aplicação faz.

   GERADO pelo comando "criar-admin --gerar-sql" da aplicação. Não edite à mão: gere de novo.
   Pré-requisito: os scripts 01 e 02 já executados.
   Execução: SSMS (ou sqlcmd), conectado ao banco do Catálogo. Não usa variáveis de SQLCMD. Tempo: menos de 1 segundo.
   Proteção: se já existir um usuário com a matrícula C161587, o script para, sem alterar nada.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

PRINT '03_administrador_inicial: verificando pré-requisitos...';
IF OBJECT_ID(N'catalogo.Usuario', N'U') IS NULL OR OBJECT_ID(N'catalogo.HistoricoAlteracao', N'U') IS NULL
BEGIN
    RAISERROR (N'03_administrador_inicial: a estrutura do Catálogo não está instalada (schema catalogo). Execute antes o 01 e o 02. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF EXISTS (SELECT 1 FROM catalogo.Usuario WHERE Matricula = N'C161587')
BEGIN
    RAISERROR (N'03_administrador_inicial: já existe um usuário com a matrícula C161587. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END;
GO

BEGIN TRANSACTION;

INSERT catalogo.Usuario (Id, Matricula, Nome, SenhaHash, Perfil, Ativo, DeveTrocarSenha)
VALUES ('fe055a1e-9e6a-4c53-8d25-8cf1c2e0fd8e', N'C161587', N'Caio', N'AQAAAAIAAYagAAAAEOVXAVQVkuO/Ne2Eu9hrU+GjajmJpleMqKLKVb+SIXmMJkREiFZh6SbIVMDcNO669Q==',
        N'ADMINISTRADOR', 1, 1);
PRINT '03_administrador_inicial: 1/2 usuário administrador cadastrado.';

-- Histórico (RN32): a criação, com o próprio administrador como autor, como no comando criar-admin.
INSERT catalogo.HistoricoAlteracao (AlvoTipo, AlvoId, Operacao, UsuarioId, UsuarioMatricula, CorrelacaoId, DadosDepois)
VALUES (N'USUARIO', 'fe055a1e-9e6a-4c53-8d25-8cf1c2e0fd8e', N'CRIACAO',
        'fe055a1e-9e6a-4c53-8d25-8cf1c2e0fd8e', N'C161587', '8d764ae3-b7db-41dc-ad3f-eb00985a8751', N'{"matricula":"C161587","nome":"Caio","perfil":"ADMINISTRADOR","ativo":true,"equipes":[]}');
PRINT '03_administrador_inicial: 2/2 histórico de criação gravado.';

COMMIT TRANSACTION;
PRINT '03_administrador_inicial: concluído. No primeiro acesso, o administrador C161587 precisa trocar a senha provisória.';
GO

SET NOEXEC OFF;
GO
