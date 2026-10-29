/* =====================================================================
   V014 — Estrutura do Catálogo no padrão da CAIXA, VAZIA, no schema cat
   ---------------------------------------------------------------------
   Por quê: o back-end usa o schema cat com os nomes do padrão da CAIXA (CATTB001_GRUPO a CATTB023_VERSAO_SCHEMA,
   docs/adr/0052). No banco local, as tabelas antigas do schema catalogo (V001 a V012) e os dados delas ficam INTACTOS: o
   V013 (que as move para cat com sp_rename) não roda aqui. Este script cria a estrutura nova ao lado delas, sem dados.

   Como: as mesmas peças do pacote 2026-10-27-padrao-caixa (database/ferramentas/estrutura_padrao_caixa.ps1), para a
   estrutura ser idêntica à do CATDB001:
     1. a estrutura consolidada de entrega/2026-10-26-instalacao-inicial/02_estrutura.sql (V001 a V012), sem ISJSON e com
        COLLATE Latin1_General_CI_AI nas colunas de texto, criada num schema PROVISÓRIO catalogo_v014 (nunca em catalogo);
     2. o V013__padrao_caixa.sql com @Origem = catalogo_v014: move para cat, renomeia tabelas e colunas, dá ao papel
        catalogo_aplicacao SELECT, INSERT, UPDATE, DELETE e EXECUTE no schema cat e exclui o schema provisório;
     3. registra as versões 1 a 13 em cat.CATTB023_VERSAO_SCHEMA como consolidadas por este script (a 14 é registrada por
        quem aplica, como sempre).
   Não cria nenhuma área de gestão (nem testes_desempenho), usuário ou equipe: a estrutura nasce vazia.

   Se cat.CATTB023_VERSAO_SCHEMA já existir (ex.: CATDB001, que recebeu o V013), não faz nada.
   Uma transação só (XACT_ABORT): ou cria tudo, ou nada. Compatível com SQL Server 2014 (nível 120).
   Gerado a partir das peças acima; para mudar a estrutura, crie o V015 (não edite este script).
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Exigidos pelo índice filtrado UX_ModeloAvaliacao_Ativo.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

PRINT 'V014: verificando pré-requisitos...';
IF OBJECT_ID(N'cat.CATTB023_VERSAO_SCHEMA', N'U') IS NOT NULL
BEGIN
    PRINT 'V014: a estrutura do schema cat já existe (cat.CATTB023_VERSAO_SCHEMA). Nada a fazer.';
    SET NOEXEC ON;
END
ELSE IF EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID(N'cat'))
BEGIN
    RAISERROR (N'V014: o schema cat já tem tabelas, mas não a de controle de versões. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID(N'catalogo_v014'))
BEGIN
    RAISERROR (N'V014: o schema provisório catalogo_v014 já tem tabelas. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END;
GO

BEGIN TRANSACTION;
IF SCHEMA_ID(N'catalogo_v014') IS NULL EXEC (N'CREATE SCHEMA catalogo_v014 AUTHORIZATION dbo;');
GO

/* ---------------------------------------------------------------------
   Passo 1 de 3: estrutura consolidada (V001 a V012) no schema provisório catalogo_v014
   --------------------------------------------------------------------- */
PRINT 'V014: 1/3 estrutura consolidada no schema provisório catalogo_v014...';
/* ---------------------------------------------------------------------
   1. Metadados: áreas de gestão (Grupo), tipos de item (Entidade), campos e regras
   --------------------------------------------------------------------- */
PRINT '02_estrutura: 1/6 metadados (Grupo, Entidade, Campo, CampoDependencia, RestricaoUnica, RestricaoUnicaCampo)';

CREATE TABLE catalogo_v014.Grupo (
    Id           uniqueidentifier NOT NULL CONSTRAINT DF_Grupo_Id DEFAULT NEWSEQUENTIALID(),
    Chave        varchar(100) COLLATE Latin1_General_CI_AI     NOT NULL,
    Nome         nvarchar(150) COLLATE Latin1_General_CI_AI    NOT NULL,
    Descricao    nvarchar(500) COLLATE Latin1_General_CI_AI    NULL,
    CriadoEm     datetime2        NOT NULL CONSTRAINT DF_Grupo_CriadoEm DEFAULT SYSUTCDATETIME(),
    Ativo        bit              NOT NULL CONSTRAINT DF_Grupo_Ativo DEFAULT 1,
    AtualizadoEm datetime2        NULL,
    Versao       rowversion       NOT NULL,
    CONSTRAINT PK_Grupo PRIMARY KEY (Id),
    CONSTRAINT UQ_Grupo_Chave UNIQUE (Chave)
);

CREATE TABLE catalogo_v014.Entidade (
    Id              uniqueidentifier NOT NULL CONSTRAINT DF_Entidade_Id DEFAULT NEWSEQUENTIALID(),
    GrupoId         uniqueidentifier NOT NULL,
    Chave           varchar(100) COLLATE Latin1_General_CI_AI     NOT NULL,
    Nome            nvarchar(150) COLLATE Latin1_General_CI_AI    NOT NULL,
    Descricao       nvarchar(500) COLLATE Latin1_General_CI_AI    NULL,
    CampoExibicaoId uniqueidentifier NULL,
    CriadoEm        datetime2        NOT NULL CONSTRAINT DF_Entidade_CriadoEm DEFAULT SYSUTCDATETIME(),
    EscopoEquipe    bit              NOT NULL CONSTRAINT DF_Entidade_EscopoEquipe DEFAULT 1,
    Ativo           bit              NOT NULL CONSTRAINT DF_Entidade_Ativo DEFAULT 1,
    Ordem           int              NOT NULL CONSTRAINT DF_Entidade_Ordem DEFAULT 0,
    AtualizadoEm    datetime2        NULL,
    Versao          rowversion       NOT NULL,
    NomePlural      nvarchar(150) COLLATE Latin1_General_CI_AI    NULL,
    Icone           varchar(50) COLLATE Latin1_General_CI_AI      NULL,
    CONSTRAINT PK_Entidade PRIMARY KEY (Id),
    CONSTRAINT FK_Entidade_Grupo FOREIGN KEY (GrupoId) REFERENCES catalogo_v014.Grupo (Id),
    CONSTRAINT UQ_Entidade_Grupo_Chave UNIQUE (GrupoId, Chave)
);

CREATE TABLE catalogo_v014.Campo (
    Id                      uniqueidentifier NOT NULL CONSTRAINT DF_Campo_Id DEFAULT NEWSEQUENTIALID(),
    EntidadeId              uniqueidentifier NOT NULL,
    Chave                   varchar(100) COLLATE Latin1_General_CI_AI     NOT NULL,
    Nome                    nvarchar(150) COLLATE Latin1_General_CI_AI    NOT NULL,
    TipoDado                varchar(20) COLLATE Latin1_General_CI_AI      NOT NULL,
    Obrigatorio             bit              NOT NULL CONSTRAINT DF_Campo_Obrigatorio DEFAULT 0,
    TamanhoMaximo           int              NULL,
    Precisao                tinyint          NULL,
    Escala                  tinyint          NULL,
    Opcoes                  nvarchar(max) COLLATE Latin1_General_CI_AI    NULL,
    EntidadeReferenciadaId  uniqueidentifier NULL,
    AcaoExclusao            varchar(20) COLLATE Latin1_General_CI_AI      NULL,
    Ordem                   int              NOT NULL CONSTRAINT DF_Campo_Ordem DEFAULT 0,
    Ativo                   bit              NOT NULL CONSTRAINT DF_Campo_Ativo DEFAULT 1,
    CriadoEm                datetime2        NOT NULL CONSTRAINT DF_Campo_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Campo PRIMARY KEY (Id),
    CONSTRAINT FK_Campo_Entidade FOREIGN KEY (EntidadeId) REFERENCES catalogo_v014.Entidade (Id),
    CONSTRAINT FK_Campo_EntidadeReferenciada FOREIGN KEY (EntidadeReferenciadaId) REFERENCES catalogo_v014.Entidade (Id),
    CONSTRAINT UQ_Campo_Entidade_Chave UNIQUE (EntidadeId, Chave),
    CONSTRAINT UQ_Campo_Id_Entidade UNIQUE (Id, EntidadeId),
    CONSTRAINT UQ_Campo_Id_EntidadeReferenciada UNIQUE (Id, EntidadeReferenciadaId),
    CONSTRAINT CK_Campo_TipoDado CHECK (TipoDado IN
        ('TEXTO','TEXTO_LONGO','INTEIRO','DECIMAL','BOOLEANO','DATA','DATA_HORA','LISTA','REFERENCIA','URL')),
    CONSTRAINT CK_Campo_Referencia CHECK (
        (TipoDado = 'REFERENCIA' AND EntidadeReferenciadaId IS NOT NULL
             AND AcaoExclusao IS NOT NULL AND AcaoExclusao IN ('RESTRINGIR','CASCATA'))
     OR (TipoDado <> 'REFERENCIA' AND EntidadeReferenciadaId IS NULL AND AcaoExclusao IS NULL)),
    CONSTRAINT CK_Campo_Texto CHECK (
        TipoDado <> 'TEXTO' OR (TamanhoMaximo IS NOT NULL AND TamanhoMaximo BETWEEN 1 AND 4000)),
    CONSTRAINT CK_Campo_Decimal CHECK (
        (TipoDado = 'DECIMAL' AND Precisao IS NOT NULL AND Escala IS NOT NULL
             AND Precisao BETWEEN 1 AND 38 AND Escala <= Precisao
             AND Escala <= 10 AND Precisao - Escala <= 28)
     OR (TipoDado <> 'DECIMAL' AND Precisao IS NULL AND Escala IS NULL)),
    CONSTRAINT CK_Campo_Lista CHECK (
        (TipoDado = 'LISTA' AND Opcoes IS NOT NULL AND Opcoes IS NOT NULL)
     OR (TipoDado <> 'LISTA' AND Opcoes IS NULL))
);

-- O campo de exibição precisa pertencer à própria entidade.
ALTER TABLE catalogo_v014.Entidade ADD CONSTRAINT FK_Entidade_CampoExibicao
    FOREIGN KEY (CampoExibicaoId, Id) REFERENCES catalogo_v014.Campo (Id, EntidadeId);

CREATE TABLE catalogo_v014.CampoDependencia (
    CampoId         uniqueidentifier NOT NULL,
    CampoViaId      uniqueidentifier NULL,
    CampoOrigemId   uniqueidentifier NOT NULL,
    CampoAlvoId     uniqueidentifier NOT NULL,
    CONSTRAINT PK_CampoDependencia PRIMARY KEY (CampoId),
    CONSTRAINT FK_CampoDependencia_Campo  FOREIGN KEY (CampoId)       REFERENCES catalogo_v014.Campo (Id),
    CONSTRAINT FK_CampoDependencia_Via    FOREIGN KEY (CampoViaId)    REFERENCES catalogo_v014.Campo (Id),
    CONSTRAINT FK_CampoDependencia_Origem FOREIGN KEY (CampoOrigemId) REFERENCES catalogo_v014.Campo (Id),
    CONSTRAINT FK_CampoDependencia_Alvo   FOREIGN KEY (CampoAlvoId)   REFERENCES catalogo_v014.Campo (Id)
);

CREATE TABLE catalogo_v014.RestricaoUnica (
    Id          uniqueidentifier NOT NULL CONSTRAINT DF_RestricaoUnica_Id DEFAULT NEWSEQUENTIALID(),
    EntidadeId  uniqueidentifier NOT NULL,
    Nome        varchar(100) COLLATE Latin1_General_CI_AI     NOT NULL,
    CONSTRAINT PK_RestricaoUnica PRIMARY KEY (Id),
    CONSTRAINT FK_RestricaoUnica_Entidade FOREIGN KEY (EntidadeId) REFERENCES catalogo_v014.Entidade (Id),
    CONSTRAINT UQ_RestricaoUnica_Entidade_Nome UNIQUE (EntidadeId, Nome),
    CONSTRAINT UQ_RestricaoUnica_Id_Entidade UNIQUE (Id, EntidadeId)
);

CREATE TABLE catalogo_v014.RestricaoUnicaCampo (
    RestricaoUnicaId uniqueidentifier NOT NULL,
    EntidadeId       uniqueidentifier NOT NULL,
    CampoId          uniqueidentifier NOT NULL,
    CONSTRAINT PK_RestricaoUnicaCampo PRIMARY KEY (RestricaoUnicaId, CampoId),
    CONSTRAINT FK_RestricaoUnicaCampo_Restricao FOREIGN KEY (RestricaoUnicaId, EntidadeId)
        REFERENCES catalogo_v014.RestricaoUnica (Id, EntidadeId) ON DELETE CASCADE,
    CONSTRAINT FK_RestricaoUnicaCampo_Campo FOREIGN KEY (CampoId, EntidadeId)
        REFERENCES catalogo_v014.Campo (Id, EntidadeId)
);

/* ---------------------------------------------------------------------
   2. Acesso: equipes e usuários
   --------------------------------------------------------------------- */
PRINT '02_estrutura: 2/6 acesso (Equipe, Usuario, UsuarioEquipe, GrupoEquipe)';

CREATE TABLE catalogo_v014.Equipe (
    Id        uniqueidentifier NOT NULL CONSTRAINT DF_Equipe_Id DEFAULT NEWSEQUENTIALID(),
    Nome      nvarchar(150) COLLATE Latin1_General_CI_AI    NOT NULL,
    Ativa     bit              NOT NULL CONSTRAINT DF_Equipe_Ativa DEFAULT 1,
    CriadoEm  datetime2        NOT NULL CONSTRAINT DF_Equipe_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Equipe PRIMARY KEY (Id),
    CONSTRAINT UQ_Equipe_Nome UNIQUE (Nome)
);

CREATE TABLE catalogo_v014.Usuario (
    Id               uniqueidentifier NOT NULL CONSTRAINT DF_Usuario_Id DEFAULT NEWSEQUENTIALID(),
    Matricula        varchar(20) COLLATE Latin1_General_CI_AI      NOT NULL,
    Nome             nvarchar(150) COLLATE Latin1_General_CI_AI    NOT NULL,
    SenhaHash        nvarchar(500) COLLATE Latin1_General_CI_AI    NOT NULL,
    Perfil           varchar(20) COLLATE Latin1_General_CI_AI      NOT NULL,
    Ativo            bit              NOT NULL CONSTRAINT DF_Usuario_Ativo DEFAULT 1,
    DeveTrocarSenha  bit              NOT NULL CONSTRAINT DF_Usuario_DeveTrocarSenha DEFAULT 1,
    TentativasFalhas int              NOT NULL CONSTRAINT DF_Usuario_TentativasFalhas DEFAULT 0,
    BloqueadoAte     datetime2        NULL,
    UltimoAcessoEm   datetime2        NULL,
    CriadoEm         datetime2        NOT NULL CONSTRAINT DF_Usuario_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Usuario PRIMARY KEY (Id),
    CONSTRAINT UQ_Usuario_Matricula UNIQUE (Matricula),
    CONSTRAINT CK_Usuario_Perfil CHECK (Perfil IN ('EXTERNO','INTERNO','GESTOR','ADMINISTRADOR')),
    CONSTRAINT CK_Usuario_TentativasFalhas CHECK (TentativasFalhas >= 0)
);

CREATE TABLE catalogo_v014.UsuarioEquipe (
    UsuarioId  uniqueidentifier NOT NULL,
    EquipeId   uniqueidentifier NOT NULL,
    CriadoEm   datetime2        NOT NULL CONSTRAINT DF_UsuarioEquipe_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_UsuarioEquipe PRIMARY KEY (UsuarioId, EquipeId),
    CONSTRAINT FK_UsuarioEquipe_Usuario FOREIGN KEY (UsuarioId) REFERENCES catalogo_v014.Usuario (Id) ON DELETE CASCADE,
    CONSTRAINT FK_UsuarioEquipe_Equipe  FOREIGN KEY (EquipeId)  REFERENCES catalogo_v014.Equipe (Id)
);

CREATE TABLE catalogo_v014.GrupoEquipe (
    GrupoId      uniqueidentifier NOT NULL,
    EquipeId     uniqueidentifier NOT NULL,
    CriadoPorId  uniqueidentifier NULL,
    CriadoEm     datetime2        NOT NULL CONSTRAINT DF_GrupoEquipe_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GrupoEquipe PRIMARY KEY (GrupoId, EquipeId),
    CONSTRAINT FK_GrupoEquipe_Grupo     FOREIGN KEY (GrupoId)     REFERENCES catalogo_v014.Grupo (Id),
    CONSTRAINT FK_GrupoEquipe_Equipe    FOREIGN KEY (EquipeId)    REFERENCES catalogo_v014.Equipe (Id),
    CONSTRAINT FK_GrupoEquipe_CriadoPor FOREIGN KEY (CriadoPorId) REFERENCES catalogo_v014.Usuario (Id)
);
CREATE INDEX IX_GrupoEquipe_EquipeId ON catalogo_v014.GrupoEquipe (EquipeId);

/* ---------------------------------------------------------------------
   3. Dados: itens cadastrados (Registro) e as suas tabelas auxiliares
   --------------------------------------------------------------------- */
PRINT '02_estrutura: 3/6 dados (Registro, RegistroReferencia, RegistroChaveUnica, RegistroValor)';

CREATE TABLE catalogo_v014.Registro (
    Id                   uniqueidentifier NOT NULL CONSTRAINT DF_Registro_Id DEFAULT NEWSEQUENTIALID(),
    EntidadeId           uniqueidentifier NOT NULL,
    Dados                nvarchar(max) COLLATE Latin1_General_CI_AI    NOT NULL CONSTRAINT DF_Registro_Dados DEFAULT N'{}',
    CriadoEm             datetime2        NOT NULL CONSTRAINT DF_Registro_CriadoEm DEFAULT SYSUTCDATETIME(),
    AtualizadoEm         datetime2        NULL,
    Versao               rowversion       NOT NULL,
    EquipeId             uniqueidentifier NULL CONSTRAINT FK_Registro_Equipe        REFERENCES catalogo_v014.Equipe (Id),
    CriadoPorId          uniqueidentifier NULL CONSTRAINT FK_Registro_CriadoPor     REFERENCES catalogo_v014.Usuario (Id),
    AtualizadoPorId      uniqueidentifier NULL CONSTRAINT FK_Registro_AtualizadoPor REFERENCES catalogo_v014.Usuario (Id),
    SituacaoAprovacao    varchar(20) COLLATE Latin1_General_CI_AI      NOT NULL CONSTRAINT DF_Registro_SituacaoAprovacao DEFAULT 'APROVADO'
                         CONSTRAINT CK_Registro_SituacaoAprovacao CHECK (SituacaoAprovacao IN ('PENDENTE','APROVADO','REJEITADO')),
    DecididoPorId        uniqueidentifier NULL CONSTRAINT FK_Registro_DecididoPor REFERENCES catalogo_v014.Usuario (Id),
    DecididoEm           datetime2        NULL,
    JustificativaDecisao nvarchar(1000) COLLATE Latin1_General_CI_AI   NULL,
    CONSTRAINT PK_Registro PRIMARY KEY (Id),
    CONSTRAINT FK_Registro_Entidade FOREIGN KEY (EntidadeId) REFERENCES catalogo_v014.Entidade (Id),
    CONSTRAINT UQ_Registro_Id_Entidade UNIQUE (Id, EntidadeId),
    CONSTRAINT CK_Registro_Dados_Json CHECK (Dados IS NOT NULL)
);
CREATE INDEX IX_Registro_Entidade_CriadoEm ON catalogo_v014.Registro (EntidadeId, CriadoEm DESC);
CREATE INDEX IX_Registro_Entidade_Equipe ON catalogo_v014.Registro (EntidadeId, EquipeId);
CREATE INDEX IX_Registro_Situacao_Equipe ON catalogo_v014.Registro (SituacaoAprovacao, EquipeId, CriadoEm);

CREATE TABLE catalogo_v014.RegistroReferencia (
    RegistroOrigemId  uniqueidentifier NOT NULL,
    EntidadeOrigemId  uniqueidentifier NOT NULL,
    CampoId           uniqueidentifier NOT NULL,
    RegistroDestinoId uniqueidentifier NOT NULL,
    EntidadeDestinoId uniqueidentifier NOT NULL,
    CONSTRAINT PK_RegistroReferencia PRIMARY KEY (RegistroOrigemId, CampoId),
    CONSTRAINT FK_RegistroReferencia_Origem FOREIGN KEY (RegistroOrigemId, EntidadeOrigemId)
        REFERENCES catalogo_v014.Registro (Id, EntidadeId) ON DELETE CASCADE,
    CONSTRAINT FK_RegistroReferencia_CampoOrigem FOREIGN KEY (CampoId, EntidadeOrigemId)
        REFERENCES catalogo_v014.Campo (Id, EntidadeId),
    CONSTRAINT FK_RegistroReferencia_Destino FOREIGN KEY (RegistroDestinoId, EntidadeDestinoId)
        REFERENCES catalogo_v014.Registro (Id, EntidadeId),
    CONSTRAINT FK_RegistroReferencia_CampoDestino FOREIGN KEY (CampoId, EntidadeDestinoId)
        REFERENCES catalogo_v014.Campo (Id, EntidadeReferenciadaId)
);
CREATE INDEX IX_RegistroReferencia_Destino ON catalogo_v014.RegistroReferencia (RegistroDestinoId, CampoId);

CREATE TABLE catalogo_v014.RegistroChaveUnica (
    RestricaoUnicaId uniqueidentifier NOT NULL,
    EntidadeId       uniqueidentifier NOT NULL,
    RegistroId       uniqueidentifier NOT NULL,
    ValorHash        binary(32)       NOT NULL,
    CONSTRAINT PK_RegistroChaveUnica PRIMARY KEY (RestricaoUnicaId, RegistroId),
    CONSTRAINT UQ_RegistroChaveUnica_Valor UNIQUE (RestricaoUnicaId, ValorHash),
    CONSTRAINT FK_RegistroChaveUnica_Restricao FOREIGN KEY (RestricaoUnicaId, EntidadeId)
        REFERENCES catalogo_v014.RestricaoUnica (Id, EntidadeId),
    CONSTRAINT FK_RegistroChaveUnica_Registro FOREIGN KEY (RegistroId, EntidadeId)
        REFERENCES catalogo_v014.Registro (Id, EntidadeId) ON DELETE CASCADE
);

CREATE TABLE catalogo_v014.RegistroValor (
    RegistroId    uniqueidentifier NOT NULL,
    EntidadeId    uniqueidentifier NOT NULL,
    CampoId       uniqueidentifier NOT NULL,
    ValorTexto    nvarchar(max) COLLATE Latin1_General_CI_AI    NULL,
    ValorNumero   decimal(38,10)   NULL,
    ValorData     datetime2        NULL,
    ValorBooleano bit              NULL,
    CONSTRAINT PK_RegistroValor PRIMARY KEY (RegistroId, CampoId),
    CONSTRAINT FK_RegistroValor_Registro FOREIGN KEY (RegistroId, EntidadeId)
        REFERENCES catalogo_v014.Registro (Id, EntidadeId) ON DELETE CASCADE,
    CONSTRAINT FK_RegistroValor_Campo FOREIGN KEY (CampoId, EntidadeId)
        REFERENCES catalogo_v014.Campo (Id, EntidadeId),
    CONSTRAINT CK_RegistroValor_UmValor CHECK (
          CASE WHEN ValorTexto    IS NULL THEN 0 ELSE 1 END
        + CASE WHEN ValorNumero   IS NULL THEN 0 ELSE 1 END
        + CASE WHEN ValorData     IS NULL THEN 0 ELSE 1 END
        + CASE WHEN ValorBooleano IS NULL THEN 0 ELSE 1 END = 1)
);

/* ---------------------------------------------------------------------
   4. Histórico de alterações e aprovação
   --------------------------------------------------------------------- */
PRINT '02_estrutura: 4/6 histórico e aprovação (HistoricoAlteracao, ConfiguracaoEntidadeEquipe)';

CREATE TABLE catalogo_v014.HistoricoAlteracao (
    Id               bigint IDENTITY(1,1) NOT NULL,
    AlvoTipo         varchar(20) COLLATE Latin1_General_CI_AI      NOT NULL,
    AlvoId           uniqueidentifier NOT NULL,
    EntidadeId       uniqueidentifier NULL,
    EquipeId         uniqueidentifier NULL,
    Operacao         varchar(20) COLLATE Latin1_General_CI_AI      NOT NULL,
    UsuarioId        uniqueidentifier NOT NULL,
    UsuarioMatricula varchar(20) COLLATE Latin1_General_CI_AI      NOT NULL,
    OcorridoEm       datetime2        NOT NULL CONSTRAINT DF_HistoricoAlteracao_OcorridoEm DEFAULT SYSUTCDATETIME(),
    CorrelacaoId     uniqueidentifier NOT NULL,
    DadosAntes       nvarchar(max) COLLATE Latin1_General_CI_AI    NULL,
    DadosDepois      nvarchar(max) COLLATE Latin1_General_CI_AI    NULL,
    Justificativa    nvarchar(1000) COLLATE Latin1_General_CI_AI   NULL,
    Origem           varchar(20) COLLATE Latin1_General_CI_AI      NULL,
    CONSTRAINT PK_HistoricoAlteracao PRIMARY KEY (Id),
    CONSTRAINT FK_HistoricoAlteracao_Usuario  FOREIGN KEY (UsuarioId)  REFERENCES catalogo_v014.Usuario (Id),
    CONSTRAINT FK_HistoricoAlteracao_Entidade FOREIGN KEY (EntidadeId) REFERENCES catalogo_v014.Entidade (Id),
    CONSTRAINT FK_HistoricoAlteracao_Equipe   FOREIGN KEY (EquipeId)   REFERENCES catalogo_v014.Equipe (Id),
    CONSTRAINT CK_HistoricoAlteracao_DadosAntes_Json  CHECK (DadosAntes  IS NULL OR DadosAntes IS NOT NULL),
    CONSTRAINT CK_HistoricoAlteracao_DadosDepois_Json CHECK (DadosDepois IS NULL OR DadosDepois IS NOT NULL),
    CONSTRAINT CK_HistoricoAlteracao_Operacao
        CHECK (Operacao IN ('CRIACAO','ALTERACAO','EXCLUSAO','APROVACAO','REJEICAO')),
    CONSTRAINT CK_HistoricoAlteracao_AlvoTipo
        CHECK (AlvoTipo IN ('REGISTRO','USUARIO','EQUIPE','CONFIGURACAO','MODELO_AVALIACAO','AVALIACAO','METADADO')),
    CONSTRAINT CK_HistoricoAlteracao_Entidade
        CHECK ((AlvoTipo IN ('REGISTRO','CONFIGURACAO','MODELO_AVALIACAO','AVALIACAO') AND EntidadeId IS NOT NULL)
            OR (AlvoTipo IN ('USUARIO','EQUIPE','METADADO') AND EntidadeId IS NULL)),
    CONSTRAINT CK_HistoricoAlteracao_Origem CHECK (Origem IS NULL OR Origem IN ('IMPORTACAO'))
);
CREATE INDEX IX_HistoricoAlteracao_Alvo ON catalogo_v014.HistoricoAlteracao (AlvoTipo, AlvoId, OcorridoEm);
CREATE INDEX IX_HistoricoAlteracao_Usuario
    ON catalogo_v014.HistoricoAlteracao (UsuarioId, OcorridoEm DESC)
    INCLUDE (AlvoTipo, AlvoId, EntidadeId, Operacao);

CREATE TABLE catalogo_v014.ConfiguracaoEntidadeEquipe (
    EntidadeId       uniqueidentifier NOT NULL,
    EquipeId         uniqueidentifier NOT NULL,
    RequerAprovacao  bit              NOT NULL CONSTRAINT DF_ConfiguracaoEntidadeEquipe_RequerAprovacao DEFAULT 0,
    AtualizadoPorId  uniqueidentifier NOT NULL,
    AtualizadoEm     datetime2        NOT NULL CONSTRAINT DF_ConfiguracaoEntidadeEquipe_AtualizadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ConfiguracaoEntidadeEquipe PRIMARY KEY (EntidadeId, EquipeId),
    CONSTRAINT FK_ConfiguracaoEntidadeEquipe_Entidade FOREIGN KEY (EntidadeId) REFERENCES catalogo_v014.Entidade (Id),
    CONSTRAINT FK_ConfiguracaoEntidadeEquipe_Equipe FOREIGN KEY (EquipeId) REFERENCES catalogo_v014.Equipe (Id),
    CONSTRAINT FK_ConfiguracaoEntidadeEquipe_AtualizadoPor FOREIGN KEY (AtualizadoPorId) REFERENCES catalogo_v014.Usuario (Id)
);

/* ---------------------------------------------------------------------
   5. Avaliação de itens
   --------------------------------------------------------------------- */
PRINT '02_estrutura: 5/6 avaliação (ModeloAvaliacao, CriterioAvaliacao, Avaliacao, AvaliacaoResposta)';

CREATE TABLE catalogo_v014.ModeloAvaliacao (
    Id               uniqueidentifier NOT NULL CONSTRAINT DF_ModeloAvaliacao_Id DEFAULT NEWSEQUENTIALID(),
    EntidadeId       uniqueidentifier NOT NULL,
    EquipeId         uniqueidentifier NOT NULL,
    Nome             nvarchar(150) COLLATE Latin1_General_CI_AI    NOT NULL,
    Descricao        nvarchar(500) COLLATE Latin1_General_CI_AI    NULL,
    Ativo            bit              NOT NULL CONSTRAINT DF_ModeloAvaliacao_Ativo DEFAULT 1,
    CriadoPorId      uniqueidentifier NOT NULL,
    CriadoEm         datetime2        NOT NULL CONSTRAINT DF_ModeloAvaliacao_CriadoEm DEFAULT SYSUTCDATETIME(),
    AtualizadoPorId  uniqueidentifier NULL,
    AtualizadoEm     datetime2        NULL,
    Versao           rowversion       NOT NULL,
    CONSTRAINT PK_ModeloAvaliacao PRIMARY KEY (Id),
    CONSTRAINT FK_ModeloAvaliacao_Entidade FOREIGN KEY (EntidadeId) REFERENCES catalogo_v014.Entidade (Id),
    CONSTRAINT FK_ModeloAvaliacao_Equipe FOREIGN KEY (EquipeId) REFERENCES catalogo_v014.Equipe (Id),
    CONSTRAINT FK_ModeloAvaliacao_CriadoPor FOREIGN KEY (CriadoPorId) REFERENCES catalogo_v014.Usuario (Id),
    CONSTRAINT FK_ModeloAvaliacao_AtualizadoPor FOREIGN KEY (AtualizadoPorId) REFERENCES catalogo_v014.Usuario (Id)
);
-- Um modelo ATIVO por tipo de item e equipe (índice único filtrado).
CREATE UNIQUE INDEX UX_ModeloAvaliacao_Ativo ON catalogo_v014.ModeloAvaliacao (EntidadeId, EquipeId) WHERE Ativo = 1;

CREATE TABLE catalogo_v014.CriterioAvaliacao (
    Id                 uniqueidentifier NOT NULL CONSTRAINT DF_CriterioAvaliacao_Id DEFAULT NEWSEQUENTIALID(),
    ModeloAvaliacaoId  uniqueidentifier NOT NULL,
    Chave              varchar(100) COLLATE Latin1_General_CI_AI     NOT NULL,
    Nome               nvarchar(150) COLLATE Latin1_General_CI_AI    NOT NULL,
    Descricao          nvarchar(500) COLLATE Latin1_General_CI_AI    NULL,
    Tipo               varchar(20) COLLATE Latin1_General_CI_AI      NOT NULL,
    Obrigatorio        bit              NOT NULL CONSTRAINT DF_CriterioAvaliacao_Obrigatorio DEFAULT 0,
    Comentario         varchar(20) COLLATE Latin1_General_CI_AI      NOT NULL CONSTRAINT DF_CriterioAvaliacao_Comentario DEFAULT 'NENHUM',
    TamanhoMaximoTexto int              NOT NULL CONSTRAINT DF_CriterioAvaliacao_TamanhoMaximoTexto DEFAULT 2000,
    Ordem              int              NOT NULL,
    Ativo              bit              NOT NULL CONSTRAINT DF_CriterioAvaliacao_Ativo DEFAULT 1,
    CriadoEm           datetime2        NOT NULL CONSTRAINT DF_CriterioAvaliacao_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_CriterioAvaliacao PRIMARY KEY (Id),
    CONSTRAINT FK_CriterioAvaliacao_Modelo FOREIGN KEY (ModeloAvaliacaoId) REFERENCES catalogo_v014.ModeloAvaliacao (Id),
    CONSTRAINT UQ_CriterioAvaliacao_Modelo_Chave UNIQUE (ModeloAvaliacaoId, Chave),
    CONSTRAINT CK_CriterioAvaliacao_Chave
        CHECK (LEN(Chave) > 0 AND Chave NOT LIKE '%[^a-z0-9_]%' COLLATE Latin1_General_BIN),
    CONSTRAINT CK_CriterioAvaliacao_Tipo CHECK (Tipo IN ('ESTRELAS','TEXTO')),
    CONSTRAINT CK_CriterioAvaliacao_Comentario CHECK (Comentario IN ('NENHUM','OPCIONAL','OBRIGATORIO')),
    CONSTRAINT CK_CriterioAvaliacao_ComentarioSoEmEstrelas CHECK (Tipo = 'ESTRELAS' OR Comentario = 'NENHUM'),
    CONSTRAINT CK_CriterioAvaliacao_TamanhoMaximoTexto CHECK (TamanhoMaximoTexto BETWEEN 1 AND 4000)
);

CREATE TABLE catalogo_v014.Avaliacao (
    Id                 uniqueidentifier NOT NULL CONSTRAINT DF_Avaliacao_Id DEFAULT NEWSEQUENTIALID(),
    RegistroId         uniqueidentifier NOT NULL,
    ModeloAvaliacaoId  uniqueidentifier NOT NULL,
    UsuarioId          uniqueidentifier NOT NULL,
    CriadoEm           datetime2        NOT NULL CONSTRAINT DF_Avaliacao_CriadoEm DEFAULT SYSUTCDATETIME(),
    AtualizadoEm       datetime2        NULL,
    Versao             rowversion       NOT NULL,
    CONSTRAINT PK_Avaliacao PRIMARY KEY (Id),
    CONSTRAINT FK_Avaliacao_Registro FOREIGN KEY (RegistroId) REFERENCES catalogo_v014.Registro (Id) ON DELETE CASCADE,
    CONSTRAINT FK_Avaliacao_Modelo FOREIGN KEY (ModeloAvaliacaoId) REFERENCES catalogo_v014.ModeloAvaliacao (Id),
    CONSTRAINT FK_Avaliacao_Usuario FOREIGN KEY (UsuarioId) REFERENCES catalogo_v014.Usuario (Id),
    CONSTRAINT UQ_Avaliacao_Registro_Modelo_Usuario UNIQUE (RegistroId, ModeloAvaliacaoId, UsuarioId)
);
CREATE INDEX IX_Avaliacao_Modelo ON catalogo_v014.Avaliacao (ModeloAvaliacaoId);

CREATE TABLE catalogo_v014.AvaliacaoResposta (
    AvaliacaoId          uniqueidentifier NOT NULL,
    CriterioAvaliacaoId  uniqueidentifier NOT NULL,
    Estrelas             tinyint          NULL,
    Texto                nvarchar(max) COLLATE Latin1_General_CI_AI    NULL,
    CONSTRAINT PK_AvaliacaoResposta PRIMARY KEY (AvaliacaoId, CriterioAvaliacaoId),
    CONSTRAINT FK_AvaliacaoResposta_Avaliacao FOREIGN KEY (AvaliacaoId) REFERENCES catalogo_v014.Avaliacao (Id) ON DELETE CASCADE,
    CONSTRAINT FK_AvaliacaoResposta_Criterio FOREIGN KEY (CriterioAvaliacaoId) REFERENCES catalogo_v014.CriterioAvaliacao (Id),
    CONSTRAINT CK_AvaliacaoResposta_Estrelas CHECK (Estrelas IS NULL OR Estrelas BETWEEN 1 AND 5)
);
CREATE INDEX IX_AvaliacaoResposta_Criterio ON catalogo_v014.AvaliacaoResposta (CriterioAvaliacaoId) INCLUDE (Estrelas);

/* ---------------------------------------------------------------------
   6. Importação de planilhas e controle de versões do schema
   --------------------------------------------------------------------- */
PRINT '02_estrutura: 6/6 importação (Importacao, ImportacaoArquivo) e controle de versões (__VersaoSchema)';

CREATE TABLE catalogo_v014.Importacao (
    Id              uniqueidentifier NOT NULL CONSTRAINT DF_Importacao_Id DEFAULT NEWSEQUENTIALID(),
    GrupoId         uniqueidentifier NOT NULL,
    EntidadeId      uniqueidentifier NOT NULL,
    UsuarioId       uniqueidentifier NOT NULL,
    EquipePadraoId  uniqueidentifier NULL,
    NomeArquivo     nvarchar(260) COLLATE Latin1_General_CI_AI    NOT NULL,
    Formato         varchar(4) COLLATE Latin1_General_CI_AI       NOT NULL,
    Situacao        varchar(10) COLLATE Latin1_General_CI_AI      NOT NULL CONSTRAINT DF_Importacao_Situacao DEFAULT 'ENVIADA',
    Opcoes          nvarchar(max) COLLATE Latin1_General_CI_AI    NULL,
    Mapeamento      nvarchar(max) COLLATE Latin1_General_CI_AI    NULL,
    Resumo          nvarchar(max) COLLATE Latin1_General_CI_AI    NULL,
    CorrelacaoId    uniqueidentifier NOT NULL,
    CriadoEm        datetime2        NOT NULL CONSTRAINT DF_Importacao_CriadoEm DEFAULT SYSUTCDATETIME(),
    ConcluidaEm     datetime2        NULL,
    DesfeitaEm      datetime2        NULL,
    Versao          rowversion       NOT NULL,
    CONSTRAINT PK_Importacao PRIMARY KEY (Id),
    CONSTRAINT UQ_Importacao_CorrelacaoId UNIQUE (CorrelacaoId),
    CONSTRAINT FK_Importacao_Grupo    FOREIGN KEY (GrupoId)        REFERENCES catalogo_v014.Grupo (Id),
    CONSTRAINT FK_Importacao_Entidade FOREIGN KEY (EntidadeId)     REFERENCES catalogo_v014.Entidade (Id) ON DELETE CASCADE,
    CONSTRAINT FK_Importacao_Usuario  FOREIGN KEY (UsuarioId)      REFERENCES catalogo_v014.Usuario (Id),
    CONSTRAINT FK_Importacao_Equipe   FOREIGN KEY (EquipePadraoId) REFERENCES catalogo_v014.Equipe (Id),
    CONSTRAINT CK_Importacao_Formato  CHECK (Formato IN ('XLSX','CSV')),
    CONSTRAINT CK_Importacao_Situacao CHECK (Situacao IN ('ENVIADA','VALIDADA','CONCLUIDA','DESFEITA','EXPIRADA')),
    CONSTRAINT CK_Importacao_Opcoes_Json     CHECK (Opcoes     IS NULL OR Opcoes IS NOT NULL),
    CONSTRAINT CK_Importacao_Mapeamento_Json CHECK (Mapeamento IS NULL OR Mapeamento IS NOT NULL),
    CONSTRAINT CK_Importacao_Resumo_Json     CHECK (Resumo     IS NULL OR Resumo IS NOT NULL)
);
CREATE INDEX IX_Importacao_Entidade_CriadoEm ON catalogo_v014.Importacao (EntidadeId, CriadoEm);

-- Arquivo enviado na importação (até 5 MB), apagado automaticamente pela aplicação depois de 7 dias.
CREATE TABLE catalogo_v014.ImportacaoArquivo (
    ImportacaoId uniqueidentifier NOT NULL,
    Conteudo     varbinary(max)   NOT NULL,
    CONSTRAINT PK_ImportacaoArquivo PRIMARY KEY (ImportacaoId),
    CONSTRAINT FK_ImportacaoArquivo_Importacao FOREIGN KEY (ImportacaoId) REFERENCES catalogo_v014.Importacao (Id) ON DELETE CASCADE
);

CREATE TABLE catalogo_v014.__VersaoSchema (
    Versao      INT           NOT NULL CONSTRAINT PK___VersaoSchema PRIMARY KEY,
    ArquivoNome NVARCHAR(260) COLLATE Latin1_General_CI_AI NOT NULL,
    AplicadoEm  DATETIME2(0)  NOT NULL CONSTRAINT DF___VersaoSchema_AplicadoEm DEFAULT SYSUTCDATETIME(),
    Observacao  NVARCHAR(500) COLLATE Latin1_General_CI_AI NULL
);

INSERT catalogo_v014.__VersaoSchema (Versao, ArquivoNome, Observacao)
SELECT v.Versao, v.ArquivoNome, N'Instalação consolidada (pacote 2026-10-26-instalacao-inicial, 02_estrutura.sql): o script versionado não foi executado individualmente.'
FROM (VALUES
    ( 1, N'V001__listagem_generica.sql'),
    ( 2, N'V002__registro_valor.sql'),
    ( 3, N'V003__usuarios_equipes_historico.sql'),
    ( 4, N'V004__aprovacao_registros.sql'),
    ( 5, N'V005__avaliacao_itens.sql'),
    ( 6, N'V006__administracao_metadados.sql'),
    ( 7, N'V007__tipo_url.sql'),
    ( 8, N'V008__entidade_nome_plural_e_ordem.sql'),
    ( 9, N'V009__grupo_equipe.sql'),
    (10, N'V010__entidade_icone_e_recentes.sql'),
    (11, N'V011__importacao.sql'),
    (12, N'V012__schema_catalogo.sql')) v (Versao, ArquivoNome);
GO

IF XACT_STATE() <> 1
BEGIN
    RAISERROR (N'V014: um passo anterior falhou e a transação foi desfeita. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END;
GO

/* ---------------------------------------------------------------------
   Passo 2 de 3: V013 (scripts/V013__padrao_caixa.sql) com @Origem = catalogo_v014: schema cat e nomes do padrão
   --------------------------------------------------------------------- */
PRINT 'V014: 2/3 convertendo para o padrão de nomes (V013)...';
GO
/* =====================================================================
   V013 — Padrão de nomenclatura da CAIXA: schema cat, tabelas CATTB0xx e colunas com prefixo de tipo
   ---------------------------------------------------------------------
   Por quê: o banco da CAIXA (CATDB001) segue o padrão de nomes da casa: schema cat, tabelas CATTB001_GRUPO a
   CATTB023_VERSAO_SCHEMA e colunas com prefixo de tipo (NU_ número/identificador, CO_ código, NO_ nome, DE_ descrição,
   DH_/DT_ data, IC_ indicador, VR_ valor). Todos os ambientes ficam com os mesmos nomes (docs/adr/0052).

   O que faz, numa transação só (XACT_ABORT): ou converte tudo, ou nada.
   - Move as 23 tabelas do schema catalogo para o schema cat (ALTER SCHEMA ... TRANSFER, sem cópia de dados) e renomeia
     tabelas e colunas (sp_rename). Os TIPOS não mudam: identificadores uniqueidentifier, textos nvarchar, versão rowversion.
     Os nomes das constraints e dos índices (PK_Grupo, UQ_Grupo_Chave, FK_..., CK_..., DF_..., IX_...) também não mudam:
     a aplicação traduz os erros do banco por eles.
   - Remove os CHECKs com ISJSON (o CATDB001 é SQL Server 2014, nível 120, que não tem ISJSON). O formato JSON continua
     garantido pela aplicação, que é a única que grava essas colunas. O CK_Campo_Lista é recriado sem o ISJSON.
   - Recria o índice filtrado UX_ModeloAvaliacao_Ativo com os nomes novos.
   - Permissões: o papel catalogo_aplicacao recebe SELECT, INSERT, UPDATE, DELETE e EXECUTE no schema cat (o schema
     catalogo, vazio, é excluído). No Docker local, o listagem_app passa a ter cat como schema padrão.
   - A tabela de controle de versões passa a ser cat.CATTB023_VERSAO_SCHEMA (NU_NSU_VERSAO, DE_ARQUIVO_VS, DH_APLICADO_VS,
     DE_OBSERVACAO_VS); o aplicar_scripts.sh acha as duas.
   Se já tiver sido aplicado (cat.CATTB001_GRUPO existe e catalogo.Grupo não), não faz nada.
   Compatível com SQL Server 2014 (nível 120). Escrito num lote só, com SQL dinâmico: o schema de destino fica em @Destino.

   A partir daqui, todo script novo usa os nomes do padrão: cat.CATTB0xx_... (database/AGENTS.md). O de-para completo está
   em docs/modelo-de-dados.md e, no código, em NomesBanco (Infrastructure).
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Exigidos pelo índice filtrado UX_ModeloAvaliacao_Ativo.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

DECLARE @Origem sysname = N'catalogo_v014';
DECLARE @Destino sysname = N'cat';

DECLARE @Tabelas TABLE (Antiga sysname NOT NULL PRIMARY KEY, Nova sysname NOT NULL UNIQUE);
INSERT @Tabelas (Antiga, Nova) VALUES
    (N'Grupo',                      N'CATTB001_GRUPO'),
    (N'Entidade',                   N'CATTB002_ENTIDADE'),
    (N'Campo',                      N'CATTB003_CAMPO'),
    (N'CampoDependencia',           N'CATTB004_CAMPO_DEPENDENCIA'),
    (N'RestricaoUnica',             N'CATTB005_RESTRICAO_UNICA'),
    (N'RestricaoUnicaCampo',        N'CATTB006_RESTRICAO_UNICA_CAMPO'),
    (N'Equipe',                     N'CATTB007_EQUIPE'),
    (N'Usuario',                    N'CATTB008_USUARIO'),
    (N'UsuarioEquipe',              N'CATTB009_USUARIO_EQUIPE'),
    (N'GrupoEquipe',                N'CATTB010_GRUPO_EQUIPE'),
    (N'Registro',                   N'CATTB011_REGISTRO'),
    (N'RegistroReferencia',         N'CATTB012_REGISTRO_REFERENCIA'),
    (N'RegistroChaveUnica',         N'CATTB013_REGISTRO_CHAVE_UNICA'),
    (N'RegistroValor',              N'CATTB014_REGISTRO_VALOR'),
    (N'HistoricoAlteracao',         N'CATTB015_HISTORICO_ALTERACAO'),
    (N'ConfiguracaoEntidadeEquipe', N'CATTB016_CONFIG_ENTIDADE_EQUIPE'),
    (N'ModeloAvaliacao',            N'CATTB017_MODELO_AVALIACAO'),
    (N'CriterioAvaliacao',          N'CATTB018_CRITERIO_AVALIACAO'),
    (N'Avaliacao',                  N'CATTB019_AVALIACAO'),
    (N'AvaliacaoResposta',          N'CATTB020_AVALIACAO_RESPOSTA'),
    (N'Importacao',                 N'CATTB021_IMPORTACAO'),
    (N'ImportacaoArquivo',          N'CATTB022_IMPORTACAO_ARQUIVO'),
    (N'__VersaoSchema',             N'CATTB023_VERSAO_SCHEMA');

-- De-para das colunas (tabela antiga, coluna antiga, coluna nova). Espelhado em NomesBanco.cs (Infrastructure).
DECLARE @Colunas TABLE (Tabela sysname NOT NULL, Antiga sysname NOT NULL, Nova sysname NOT NULL, PRIMARY KEY (Tabela, Antiga));
INSERT @Colunas (Tabela, Antiga, Nova) VALUES
    -- CATTB001_GRUPO
    (N'Grupo', N'Id', N'NU_NSU_GRUPO'),
    (N'Grupo', N'Chave', N'CO_CHAVE'),
    (N'Grupo', N'Nome', N'NO_GRUPO'),
    (N'Grupo', N'Descricao', N'DE_GRUPO'),
    (N'Grupo', N'CriadoEm', N'DH_CRIADO_EM'),
    (N'Grupo', N'Ativo', N'IC_ATIVO_GRU'),
    (N'Grupo', N'AtualizadoEm', N'DH_ATUALIZADO_EM'),
    (N'Grupo', N'Versao', N'NU_VERSAO'),
    -- CATTB002_ENTIDADE
    (N'Entidade', N'Id', N'NU_NSU_ENTIDADE'),
    (N'Entidade', N'GrupoId', N'NU_NSU_CE_GRUPO_ENT'),
    (N'Entidade', N'Chave', N'CO_CHAVE_ENT'),
    (N'Entidade', N'Nome', N'NO_ENTIDADE_ENT'),
    (N'Entidade', N'Descricao', N'DE_ENTIDADE_ENT'),
    (N'Entidade', N'CampoExibicaoId', N'NU_CAMPO_EXIBICAO'),
    (N'Entidade', N'CriadoEm', N'DH_CRIADO_ENT'),
    (N'Entidade', N'EscopoEquipe', N'IC_ESCOPO_EQUIPE_ENT'),
    (N'Entidade', N'Ativo', N'IC_ATIVO_ENT'),
    (N'Entidade', N'Ordem', N'NU_ORDEM_ENT'),
    (N'Entidade', N'AtualizadoEm', N'DH_ATUALIZADO_ENT'),
    (N'Entidade', N'Versao', N'NU_VERSAO_ENT'),
    (N'Entidade', N'NomePlural', N'NO_PLURAL_ENT'),
    (N'Entidade', N'Icone', N'DE_ICONE_ENT'),
    -- CATTB003_CAMPO
    (N'Campo', N'Id', N'NU_NSU_CAMPO'),
    (N'Campo', N'EntidadeId', N'NU_NSU_CE_ENTIDADE'),
    (N'Campo', N'Chave', N'CO_CHAVE_CAM'),
    (N'Campo', N'Nome', N'NO_CAMPO'),
    (N'Campo', N'TipoDado', N'NO_TIPO_DADO_CAM'),
    (N'Campo', N'Obrigatorio', N'IC_OBRIGATORIO_CAM'),
    (N'Campo', N'TamanhoMaximo', N'NU_TAMANHO_MAXIMO'),
    (N'Campo', N'Precisao', N'NU_PRECISAO'),
    (N'Campo', N'Escala', N'NU_ESCALA'),
    (N'Campo', N'Opcoes', N'DE_OPCAO'),
    (N'Campo', N'EntidadeReferenciadaId', N'NU_ENTIDADE_REFERENCIADA'),
    (N'Campo', N'AcaoExclusao', N'DE_ACAO_EXCLUSAO'),
    (N'Campo', N'Ordem', N'NU_ORDEM_CAM'),
    (N'Campo', N'Ativo', N'IC_ATIVO_CAM'),
    (N'Campo', N'CriadoEm', N'DH_CRIADO_CAM'),
    -- CATTB004_CAMPO_DEPENDENCIA
    (N'CampoDependencia', N'CampoId', N'NU_NSU_CE_CAMPO_CD'),
    (N'CampoDependencia', N'CampoViaId', N'NU_CAMPO_VIA'),
    (N'CampoDependencia', N'CampoOrigemId', N'NU_CAMPO_ORIGEM'),
    (N'CampoDependencia', N'CampoAlvoId', N'NU_CAMPO_ALVO'),
    -- CATTB005_RESTRICAO_UNICA
    (N'RestricaoUnica', N'Id', N'NU_NSU_RESTRICAO_UNICA'),
    (N'RestricaoUnica', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_RU'),
    (N'RestricaoUnica', N'Nome', N'NO_RESTRICAO_UNICA'),
    -- CATTB006_RESTRICAO_UNICA_CAMPO
    (N'RestricaoUnicaCampo', N'RestricaoUnicaId', N'NU_NSU_CE_RESTRICAO_UNICA_RUC'),
    (N'RestricaoUnicaCampo', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_RUC'),
    (N'RestricaoUnicaCampo', N'CampoId', N'NU_NSU_CE_CAMPO_RUC'),
    -- CATTB007_EQUIPE
    (N'Equipe', N'Id', N'NU_NSU_EQUIPE'),
    (N'Equipe', N'Nome', N'NO_EQUIPE'),
    (N'Equipe', N'Ativa', N'IC_ATIVA_EQP'),
    (N'Equipe', N'CriadoEm', N'DH_CRIADO_EM'),
    -- CATTB008_USUARIO
    (N'Usuario', N'Id', N'NU_NSU_USUARIO'),
    (N'Usuario', N'Matricula', N'CO_MATRICULA'),
    (N'Usuario', N'Nome', N'NO_USUARIO'),
    (N'Usuario', N'SenhaHash', N'CO_SENHA_HASH'),
    (N'Usuario', N'Perfil', N'NO_PERFIL'),
    (N'Usuario', N'Ativo', N'IC_ATIVO_USR'),
    (N'Usuario', N'DeveTrocarSenha', N'IC_TROCAR_SENHA'),
    (N'Usuario', N'TentativasFalhas', N'NU_TENTATIVA_FALHA'),
    (N'Usuario', N'BloqueadoAte', N'DH_BLOQUEADO'),
    (N'Usuario', N'UltimoAcessoEm', N'DH_ULTIMO_ACESSO'),
    (N'Usuario', N'CriadoEm', N'DH_CRIADO'),
    -- CATTB009_USUARIO_EQUIPE
    (N'UsuarioEquipe', N'UsuarioId', N'NU_NSU_CE_USUARIO_UE'),
    (N'UsuarioEquipe', N'EquipeId', N'NU_NSU_CE_EQUIPE_UE'),
    (N'UsuarioEquipe', N'CriadoEm', N'DH_CRIADO_UE'),
    -- CATTB010_GRUPO_EQUIPE
    (N'GrupoEquipe', N'GrupoId', N'NU_NSU_CE_GRUPO_GE'),
    (N'GrupoEquipe', N'EquipeId', N'NU_NSU_CE_EQUIPE_GE'),
    (N'GrupoEquipe', N'CriadoPorId', N'NU_NSU_CE_USUARIO_GE'),
    (N'GrupoEquipe', N'CriadoEm', N'DH_CRIADO'),
    -- CATTB011_REGISTRO
    (N'Registro', N'Id', N'NU_NSU_REGISTRO'),
    (N'Registro', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_REG'),
    (N'Registro', N'Dados', N'DE_REGISTRO_DADO'),
    (N'Registro', N'CriadoEm', N'DH_CRIADO'),
    (N'Registro', N'AtualizadoEm', N'DH_ATUALIZADO'),
    (N'Registro', N'Versao', N'NU_VERSAO_REG'),
    (N'Registro', N'EquipeId', N'NU_NSU_CE_EQUIPE_REG'),
    (N'Registro', N'CriadoPorId', N'NU_USUARIO_CRIADO'),
    (N'Registro', N'AtualizadoPorId', N'NU_USUARIO_ATUALIZADO'),
    (N'Registro', N'SituacaoAprovacao', N'DE_SITUACAO_APROVACAO'),
    (N'Registro', N'DecididoPorId', N'NU_USUARIO_DECISAO'),
    (N'Registro', N'DecididoEm', N'DH_DECIDIDO'),
    (N'Registro', N'JustificativaDecisao', N'DE_JUSTIFICATIVA_DECISAO'),
    -- CATTB012_REGISTRO_REFERENCIA
    (N'RegistroReferencia', N'RegistroOrigemId', N'NU_REGISTRO_ORIGEM'),
    (N'RegistroReferencia', N'EntidadeOrigemId', N'NU_ENTIDADE_ORIGEM'),
    (N'RegistroReferencia', N'CampoId', N'NU_NSU_CE_CAMPO_RR'),
    (N'RegistroReferencia', N'RegistroDestinoId', N'NU_REGISTRO_DESTINO'),
    (N'RegistroReferencia', N'EntidadeDestinoId', N'NU_ENTIDADE_DESTINO'),
    -- CATTB013_REGISTRO_CHAVE_UNICA
    (N'RegistroChaveUnica', N'RestricaoUnicaId', N'NU_NSU_CE_RESTRICAO_UNICA'),
    (N'RegistroChaveUnica', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_RCU'),
    (N'RegistroChaveUnica', N'RegistroId', N'NU_NSU_CE_REGISTRO_RCU'),
    (N'RegistroChaveUnica', N'ValorHash', N'VR_VALOR_HASH'),
    -- CATTB014_REGISTRO_VALOR
    (N'RegistroValor', N'RegistroId', N'NU_NSU_CE_REGISTRO_RV'),
    (N'RegistroValor', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_RV'),
    (N'RegistroValor', N'CampoId', N'NU_NSU_CE_CAMPO_RV'),
    (N'RegistroValor', N'ValorTexto', N'DE_VALOR'),
    (N'RegistroValor', N'ValorNumero', N'NU_VALOR'),
    (N'RegistroValor', N'ValorData', N'DT_VALOR'),
    (N'RegistroValor', N'ValorBooleano', N'IC_VALOR'),
    -- CATTB015_HISTORICO_ALTERACAO
    (N'HistoricoAlteracao', N'Id', N'NU_NSU_HISTORICO_ALTERACAO'),
    (N'HistoricoAlteracao', N'AlvoTipo', N'NO_ALVO_TIPO'),
    (N'HistoricoAlteracao', N'AlvoId', N'NU_ALVO'),
    (N'HistoricoAlteracao', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_HA'),
    (N'HistoricoAlteracao', N'EquipeId', N'NU_NSU_CE_EQUIPE_HA'),
    (N'HistoricoAlteracao', N'Operacao', N'NO_OPERACAO'),
    (N'HistoricoAlteracao', N'UsuarioId', N'NU_NSU_CE_USUARIO_HA'),
    (N'HistoricoAlteracao', N'UsuarioMatricula', N'CO_MATRI_USUARIO'),
    (N'HistoricoAlteracao', N'OcorridoEm', N'DT_OCORRIDO'),
    (N'HistoricoAlteracao', N'CorrelacaoId', N'NU_CORRELACAO'),
    (N'HistoricoAlteracao', N'DadosAntes', N'DE_DADO_ANTES'),
    (N'HistoricoAlteracao', N'DadosDepois', N'DE_DADO_DEPOIS'),
    (N'HistoricoAlteracao', N'Justificativa', N'DE_JUSTIFICATIVA_HA'),
    (N'HistoricoAlteracao', N'Origem', N'DE_ORIGEM_HA'),
    -- CATTB016_CONFIG_ENTIDADE_EQUIPE
    (N'ConfiguracaoEntidadeEquipe', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_CEE'),
    (N'ConfiguracaoEntidadeEquipe', N'EquipeId', N'NU_NSU_EQUIPE_CEE'),
    (N'ConfiguracaoEntidadeEquipe', N'RequerAprovacao', N'IC_APROVACAO'),
    (N'ConfiguracaoEntidadeEquipe', N'AtualizadoPorId', N'NU_USR_ATUALIZADO'),
    (N'ConfiguracaoEntidadeEquipe', N'AtualizadoEm', N'DT_ATUALIZADO'),
    -- CATTB017_MODELO_AVALIACAO
    (N'ModeloAvaliacao', N'Id', N'NU_NSU_MODELO_AVALIACAO'),
    (N'ModeloAvaliacao', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_MA'),
    (N'ModeloAvaliacao', N'EquipeId', N'NU_NSU_CE_EQUIPE_MA'),
    (N'ModeloAvaliacao', N'Nome', N'NO_MODELO_AVALIACAO'),
    (N'ModeloAvaliacao', N'Descricao', N'DE_MODELO_AVALIACAO'),
    (N'ModeloAvaliacao', N'Ativo', N'IC_ATIVO_MA'),
    (N'ModeloAvaliacao', N'CriadoPorId', N'NU_USR_CRIADO_MA'),
    (N'ModeloAvaliacao', N'CriadoEm', N'DH_CRIADO_MA'),
    (N'ModeloAvaliacao', N'AtualizadoPorId', N'NU_USR_ATUALIZADO_MA'),
    (N'ModeloAvaliacao', N'AtualizadoEm', N'DH_ATUALIZADO_MA'),
    (N'ModeloAvaliacao', N'Versao', N'NU_VERSAO_MA'),
    -- CATTB018_CRITERIO_AVALIACAO
    (N'CriterioAvaliacao', N'Id', N'NU_NSU_CRITERIO_AVALIACAO'),
    (N'CriterioAvaliacao', N'ModeloAvaliacaoId', N'NU_NSU_CE_MODELO_AVALIACAO_CA'),
    (N'CriterioAvaliacao', N'Chave', N'CO_CHAVE_CA'),
    (N'CriterioAvaliacao', N'Nome', N'NO_CRITERIO_AVALIACAO'),
    (N'CriterioAvaliacao', N'Descricao', N'DE_CRITERIO_AVALIACAO'),
    (N'CriterioAvaliacao', N'Tipo', N'NO_TIPO_CA'),
    (N'CriterioAvaliacao', N'Obrigatorio', N'IC_OBRIGATORIO_CA'),
    (N'CriterioAvaliacao', N'Comentario', N'DE_COMENTARIO_CA'),
    (N'CriterioAvaliacao', N'TamanhoMaximoTexto', N'NU_TAMANHO_MAX_CA'),
    (N'CriterioAvaliacao', N'Ordem', N'NU_ORDEM_CA'),
    (N'CriterioAvaliacao', N'Ativo', N'IC_ATIVO_CA'),
    (N'CriterioAvaliacao', N'CriadoEm', N'DH_CRIADO_CA'),
    -- CATTB019_AVALIACAO
    (N'Avaliacao', N'Id', N'NU_NSU_AVALIACAO'),
    (N'Avaliacao', N'RegistroId', N'NU_NSU_CE_REGISTRO_AV'),
    (N'Avaliacao', N'ModeloAvaliacaoId', N'NU_NSU_CE_MODELO_AVALIACAO_AV'),
    (N'Avaliacao', N'UsuarioId', N'NU_NSU_CE_USUARIO_AV'),
    (N'Avaliacao', N'CriadoEm', N'DH_CRIADO_AV'),
    (N'Avaliacao', N'AtualizadoEm', N'DH_ATUALIZADO_AV'),
    (N'Avaliacao', N'Versao', N'NU_VERSAO_AV'),
    -- CATTB020_AVALIACAO_RESPOSTA
    (N'AvaliacaoResposta', N'AvaliacaoId', N'NU_NSU_AVALIACAO'),
    (N'AvaliacaoResposta', N'CriterioAvaliacaoId', N'NU_NSU_CE_CRITERIO_AVALIACAO_AR'),
    (N'AvaliacaoResposta', N'Estrelas', N'NU_ESTRELA'),
    (N'AvaliacaoResposta', N'Texto', N'DE_AVALIACAO_RESPOSTA'),
    -- CATTB021_IMPORTACAO
    (N'Importacao', N'Id', N'NU_NSU_IMPORTACAO'),
    (N'Importacao', N'GrupoId', N'NU_NSU_CE_GRUPO_IMP'),
    (N'Importacao', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_IMP'),
    (N'Importacao', N'UsuarioId', N'NU_NSU_CE_USUARIO_IMP'),
    (N'Importacao', N'EquipePadraoId', N'NU_NSU_CE_EQUIPE_IMP'),
    (N'Importacao', N'NomeArquivo', N'NO_ARQUIVO_IMPORTACAO'),
    (N'Importacao', N'Formato', N'NO_FORMATO_IMP'),
    (N'Importacao', N'Situacao', N'DE_SITUACAO_IMP'),
    (N'Importacao', N'Opcoes', N'DE_OPCAO_IMP'),
    (N'Importacao', N'Mapeamento', N'DE_MAPEAMENTO_IMP'),
    (N'Importacao', N'Resumo', N'DE_RESUMO_IMP'),
    (N'Importacao', N'CorrelacaoId', N'NU_CORRELACAO_IMP'),
    (N'Importacao', N'CriadoEm', N'DH_CRIADO_IMP'),
    (N'Importacao', N'ConcluidaEm', N'DH_CONCLUIDA_IMP'),
    (N'Importacao', N'DesfeitaEm', N'DH_DESFEITA_IMP'),
    (N'Importacao', N'Versao', N'NU_VERSAO_IMP'),
    -- CATTB022_IMPORTACAO_ARQUIVO
    (N'ImportacaoArquivo', N'ImportacaoId', N'NU_NSU_CE_IMPORTACAO_IA'),
    (N'ImportacaoArquivo', N'Conteudo', N'DE_CONTEUDO_IA'),
    -- CATTB023_VERSAO_SCHEMA
    (N'__VersaoSchema', N'Versao', N'NU_NSU_VERSAO'),
    (N'__VersaoSchema', N'ArquivoNome', N'DE_ARQUIVO_VS'),
    (N'__VersaoSchema', N'AplicadoEm', N'DH_APLICADO_VS'),
    (N'__VersaoSchema', N'Observacao', N'DE_OBSERVACAO_VS');

DECLARE @Sql nvarchar(max), @Mensagem nvarchar(2048), @Antiga sysname, @Nova sysname, @Tabela sysname, @Quantidade int;

-- Já aplicado?
IF OBJECT_ID(QUOTENAME(@Destino) + N'.CATTB001_GRUPO', N'U') IS NOT NULL AND OBJECT_ID(QUOTENAME(@Origem) + N'.Grupo', N'U') IS NULL
BEGIN
    PRINT CONCAT('V013: as tabelas já estão no padrão (', @Destino, '.CATTB001_GRUPO). Nada a fazer.');
    RETURN;
END;

-- Pré-requisitos: as 23 tabelas no schema de origem e nenhuma tabela do padrão no destino.
SELECT @Quantidade = COUNT(*) FROM @Tabelas WHERE OBJECT_ID(QUOTENAME(@Origem) + N'.' + QUOTENAME(Antiga), N'U') IS NULL;
IF @Quantidade > 0
BEGIN
    SET @Mensagem = CONCAT(N'V013: faltam ', @Quantidade, N' tabela(s) do Catálogo no schema ', @Origem, N'. Nada foi alterado.');
    THROW 50013, @Mensagem, 1;
END;
IF SCHEMA_ID(@Destino) IS NOT NULL AND EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID(@Destino))
BEGIN
    SET @Mensagem = CONCAT(N'V013: o schema ', @Destino, N' já tem tabelas. Nada foi alterado.');
    THROW 50013, @Mensagem, 1;
END;
-- Toda coluna existente precisa estar no de-para (uma coluna nova esquecida aqui quebraria a aplicação).
SELECT TOP (1) @Mensagem = CONCAT(N'V013: a coluna ', t.Antiga, N'.', c.name, N' não está no de-para. Nada foi alterado.')
FROM @Tabelas t
JOIN sys.columns c ON c.object_id = OBJECT_ID(QUOTENAME(@Origem) + N'.' + QUOTENAME(t.Antiga), N'U')
WHERE NOT EXISTS (SELECT 1 FROM @Colunas m WHERE m.Tabela = t.Antiga AND m.Antiga = c.name);
IF @@ROWCOUNT > 0
    THROW 50013, @Mensagem, 1;

BEGIN TRANSACTION;

IF SCHEMA_ID(@Destino) IS NULL
BEGIN
    SET @Sql = N'CREATE SCHEMA ' + QUOTENAME(@Destino) + N' AUTHORIZATION dbo;';
    EXEC (@Sql);
END;

-- 1. CHECKs com ISJSON (não existe no SQL Server 2014) e o índice filtrado (recriado com os nomes novos no passo 4).
DECLARE @Remover TABLE (Tabela sysname NOT NULL, Nome sysname NOT NULL, Indice bit NOT NULL);
INSERT @Remover (Tabela, Nome, Indice) VALUES
    (N'Campo', N'CK_Campo_Lista', 0),
    (N'Registro', N'CK_Registro_Dados_Json', 0),
    (N'HistoricoAlteracao', N'CK_HistoricoAlteracao_DadosAntes_Json', 0),
    (N'HistoricoAlteracao', N'CK_HistoricoAlteracao_DadosDepois_Json', 0),
    (N'Importacao', N'CK_Importacao_Opcoes_Json', 0),
    (N'Importacao', N'CK_Importacao_Mapeamento_Json', 0),
    (N'Importacao', N'CK_Importacao_Resumo_Json', 0),
    (N'ModeloAvaliacao', N'UX_ModeloAvaliacao_Ativo', 1);

DECLARE remover CURSOR LOCAL FAST_FORWARD FOR SELECT Tabela, Nome FROM @Remover WHERE Indice = 0;
OPEN remover;
FETCH NEXT FROM remover INTO @Tabela, @Antiga;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(QUOTENAME(@Origem) + N'.' + QUOTENAME(@Antiga), N'C') IS NOT NULL
    BEGIN
        SET @Sql = N'ALTER TABLE ' + QUOTENAME(@Origem) + N'.' + QUOTENAME(@Tabela) + N' DROP CONSTRAINT ' + QUOTENAME(@Antiga) + N';';
        EXEC (@Sql);
    END;
    FETCH NEXT FROM remover INTO @Tabela, @Antiga;
END;
CLOSE remover;
DEALLOCATE remover;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ModeloAvaliacao_Ativo'
                                       AND object_id = OBJECT_ID(QUOTENAME(@Origem) + N'.ModeloAvaliacao'))
BEGIN
    SET @Sql = N'DROP INDEX UX_ModeloAvaliacao_Ativo ON ' + QUOTENAME(@Origem) + N'.ModeloAvaliacao;';
    EXEC (@Sql);
END;

-- O SQL Server não renomeia coluna usada em CHECK: os CHECKs restantes são guardados, removidos e recriados no passo 4,
-- com os nomes novos das colunas (a definição guardada pelo SQL Server traz cada coluna entre colchetes).
DECLARE @Checks TABLE (Tabela sysname NOT NULL, Nome sysname NOT NULL PRIMARY KEY, Definicao nvarchar(max) NOT NULL);
INSERT @Checks (Tabela, Nome, Definicao)
SELECT t.Antiga, cc.name, cc.definition
FROM sys.check_constraints cc
JOIN @Tabelas t ON cc.parent_object_id = OBJECT_ID(QUOTENAME(@Origem) + N'.' + QUOTENAME(t.Antiga), N'U');

DECLARE checks CURSOR LOCAL FAST_FORWARD FOR SELECT Tabela, Nome FROM @Checks;
OPEN checks;
FETCH NEXT FROM checks INTO @Tabela, @Antiga;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'ALTER TABLE ' + QUOTENAME(@Origem) + N'.' + QUOTENAME(@Tabela) + N' DROP CONSTRAINT ' + QUOTENAME(@Antiga) + N';';
    EXEC (@Sql);
    FETCH NEXT FROM checks INTO @Tabela, @Antiga;
END;
CLOSE checks;
DEALLOCATE checks;
PRINT 'V013: 1/5 CHECKs (os com ISJSON, de vez) e índice filtrado removidos.';

-- 2. Tabelas: muda de schema e de nome.
DECLARE tabelas CURSOR LOCAL FAST_FORWARD FOR SELECT Antiga, Nova FROM @Tabelas ORDER BY Nova;
OPEN tabelas;
FETCH NEXT FROM tabelas INTO @Antiga, @Nova;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'ALTER SCHEMA ' + QUOTENAME(@Destino) + N' TRANSFER ' + QUOTENAME(@Origem) + N'.' + QUOTENAME(@Antiga) + N';';
    EXEC (@Sql);
    SET @Tabela = QUOTENAME(@Destino) + N'.' + QUOTENAME(@Antiga);
    EXEC sys.sp_rename @objname = @Tabela, @newname = @Nova, @objtype = N'OBJECT';
    FETCH NEXT FROM tabelas INTO @Antiga, @Nova;
END;
CLOSE tabelas;
DEALLOCATE tabelas;
PRINT 'V013: 2/5 23 tabelas movidas para o schema de destino e renomeadas.';

-- 3. Colunas.
DECLARE colunas CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.Nova, c.Antiga, c.Nova FROM @Colunas c JOIN @Tabelas t ON t.Antiga = c.Tabela ORDER BY t.Nova, c.Antiga;
OPEN colunas;
FETCH NEXT FROM colunas INTO @Tabela, @Antiga, @Nova;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = QUOTENAME(@Destino) + N'.' + QUOTENAME(@Tabela) + N'.' + QUOTENAME(@Antiga);
    EXEC sys.sp_rename @objname = @Sql, @newname = @Nova, @objtype = N'COLUMN';
    FETCH NEXT FROM colunas INTO @Tabela, @Antiga, @Nova;
END;
CLOSE colunas;
DEALLOCATE colunas;
PRINT 'V013: 3/5 173 colunas renomeadas.';

-- 4. O que foi removido no passo 1, com os nomes novos (sem ISJSON).
DECLARE @Definicao nvarchar(max), @Coluna sysname, @ColunaNova sysname;
DECLARE checks CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.Nova, c.Nome, c.Definicao FROM @Checks c JOIN @Tabelas t ON t.Antiga = c.Tabela ORDER BY c.Nome;
OPEN checks;
FETCH NEXT FROM checks INTO @Tabela, @Antiga, @Definicao;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE nomes CURSOR LOCAL FAST_FORWARD FOR
        SELECT m.Antiga, m.Nova FROM @Colunas m JOIN @Tabelas t ON t.Antiga = m.Tabela WHERE t.Nova = @Tabela;
    OPEN nomes;
    FETCH NEXT FROM nomes INTO @Coluna, @ColunaNova;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Definicao = REPLACE(@Definicao, N'[' + @Coluna + N']', N'[' + @ColunaNova + N']');
        FETCH NEXT FROM nomes INTO @Coluna, @ColunaNova;
    END;
    CLOSE nomes;
    DEALLOCATE nomes;

    SET @Sql = N'ALTER TABLE ' + QUOTENAME(@Destino) + N'.' + QUOTENAME(@Tabela) + N' WITH CHECK ADD CONSTRAINT '
             + QUOTENAME(@Antiga) + N' CHECK ' + @Definicao + N';';
    EXEC (@Sql);
    FETCH NEXT FROM checks INTO @Tabela, @Antiga, @Definicao;
END;
CLOSE checks;
DEALLOCATE checks;

SET @Sql = N'ALTER TABLE ' + QUOTENAME(@Destino) + N'.CATTB003_CAMPO ADD CONSTRAINT CK_Campo_Lista CHECK (
       (NO_TIPO_DADO_CAM = ''LISTA'' AND DE_OPCAO IS NOT NULL)
    OR (NO_TIPO_DADO_CAM <> ''LISTA'' AND DE_OPCAO IS NULL));';
EXEC (@Sql);
SET @Sql = N'CREATE UNIQUE INDEX UX_ModeloAvaliacao_Ativo ON ' + QUOTENAME(@Destino)
         + N'.CATTB017_MODELO_AVALIACAO (NU_NSU_CE_ENTIDADE_MA, NU_NSU_CE_EQUIPE_MA) WHERE IC_ATIVO_MA = 1;';
EXEC (@Sql);
PRINT 'V013: 4/5 CHECKs, CK_Campo_Lista (sem ISJSON) e UX_ModeloAvaliacao_Ativo recriados com os nomes novos.';

-- 5. Permissões e o schema de origem, que ficou vazio.
IF DATABASE_PRINCIPAL_ID(N'catalogo_aplicacao') IS NULL
    EXEC (N'CREATE ROLE catalogo_aplicacao AUTHORIZATION dbo;');
SET @Sql = N'GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::' + QUOTENAME(@Destino) + N' TO catalogo_aplicacao;';
EXEC (@Sql);

-- Só no Docker local (em outros ambientes o login da aplicação é associado ao papel pelo DBA).
IF DATABASE_PRINCIPAL_ID(N'listagem_app') IS NOT NULL
BEGIN
    SET @Sql = N'ALTER USER listagem_app WITH DEFAULT_SCHEMA = ' + QUOTENAME(@Destino) + N';';
    EXEC (@Sql);
END;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(@Origem))
   AND NOT EXISTS (SELECT 1 FROM sys.types WHERE schema_id = SCHEMA_ID(@Origem))
BEGIN
    SET @Sql = N'DROP SCHEMA ' + QUOTENAME(@Origem) + N';';
    EXEC (@Sql);
END;
PRINT 'V013: 5/5 permissões no schema de destino; schema de origem excluído.';

COMMIT TRANSACTION;
PRINT 'V013: concluído.';
GO

IF XACT_STATE() <> 1
BEGIN
    RAISERROR (N'V014: um passo anterior falhou e a transação foi desfeita. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END;
GO

/* ---------------------------------------------------------------------
   Passo 3 de 3: versões 1 a 13 e conferência
   --------------------------------------------------------------------- */
PRINT 'V014: 3/3 registrando as versões 1 a 13 e conferindo...';

UPDATE cat.CATTB023_VERSAO_SCHEMA
SET DE_OBSERVACAO_VS = N'Consolidada pelo V014__estrutura_padrao_caixa.sql (estrutura vazia no schema cat): o script versionado não foi executado individualmente.'
WHERE NU_NSU_VERSAO BETWEEN 1 AND 12;

INSERT cat.CATTB023_VERSAO_SCHEMA (NU_NSU_VERSAO, DE_ARQUIVO_VS, DE_OBSERVACAO_VS)
VALUES (13, N'V013__padrao_caixa.sql', N'Consolidada pelo V014__estrutura_padrao_caixa.sql (estrutura vazia no schema cat): o script versionado não foi executado individualmente.');

DECLARE @Tabelas int = (SELECT COUNT(*) FROM sys.tables WHERE schema_id = SCHEMA_ID(N'cat') AND name LIKE N'CATTB0[0-2][0-9][_]%');
DECLARE @Versoes int = (SELECT COUNT(*) FROM cat.CATTB023_VERSAO_SCHEMA);
DECLARE @Grupos int = (SELECT COUNT(*) FROM cat.CATTB001_GRUPO);
IF @Tabelas <> 23 OR @Versoes <> 13 OR @Grupos <> 0 OR SCHEMA_ID(N'catalogo_v014') IS NOT NULL
   OR TYPE_NAME((SELECT system_type_id FROM sys.columns WHERE object_id = OBJECT_ID(N'cat.CATTB001_GRUPO') AND name = N'NU_NSU_GRUPO')) <> N'uniqueidentifier'
BEGIN
    DECLARE @Mensagem nvarchar(400) = CONCAT(N'V014: conferência falhou (tabelas=', @Tabelas, N' de 23, versões=', @Versoes,
        N' de 13, grupos=', @Grupos, N'). Nada foi alterado.');
    THROW 50014, @Mensagem, 1;
END;

COMMIT TRANSACTION;
PRINT 'V014: concluído (23 tabelas vazias no schema cat, versões 1 a 13 registradas).';
GO

SET NOEXEC OFF;
GO
