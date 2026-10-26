/* =====================================================================
   CATÁLOGO — Instalação inicial — Script 02 de 03: estrutura completa
   ---------------------------------------------------------------------
   Cria, no schema catalogo, todas as tabelas do Catálogo, com chaves primárias, chaves únicas, chaves estrangeiras,
   CHECKs, valores padrão e índices. É o resultado CONSOLIDADO dos scripts versionados V001 a V012 do projeto
   (database/scripts/), escrito direto no schema catalogo: os scripts versionados não são executados um a um.

   Também cria catalogo.__VersaoSchema, a tabela de controle de versões do schema, já com as versões 1 a 12 registradas
   como aplicadas (observação: "instalação consolidada"). Os próximos pacotes começam na versão 13.

   Pré-requisito: o script 01_criar_schema_e_permissoes.sql já executado.
   Execução: SSMS (ou sqlcmd), conectado ao banco do Catálogo, com um usuário que possa criar tabelas no schema catalogo.
   Não usa variáveis de SQLCMD. Não cria nem altera nada fora do schema catalogo. Não grava dados de usuários.
   Tempo estimado: menos de 5 segundos (o banco não tem dados do Catálogo).
   Proteção: se o schema catalogo já tiver tabelas, o script para, sem alterar nada.
   Toda a criação roda numa única transação: em caso de erro, nada fica criado.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Exigidos pelo índice filtrado UX_ModeloAvaliacao_Ativo (já são o padrão do SSMS).
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

PRINT '02_estrutura: verificando pré-requisitos...';
IF SCHEMA_ID(N'catalogo') IS NULL
BEGIN
    RAISERROR (N'02_estrutura: o schema catalogo não existe. Execute antes o 01_criar_schema_e_permissoes.sql. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID(N'catalogo'))
BEGIN
    RAISERROR (N'02_estrutura: o Catálogo já está instalado (o schema catalogo já tem tabelas). Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END;
GO

BEGIN TRANSACTION;

/* ---------------------------------------------------------------------
   1. Metadados: áreas de gestão (Grupo), tipos de item (Entidade), campos e regras
   --------------------------------------------------------------------- */
PRINT '02_estrutura: 1/6 metadados (Grupo, Entidade, Campo, CampoDependencia, RestricaoUnica, RestricaoUnicaCampo)';

CREATE TABLE catalogo.Grupo (
    Id           uniqueidentifier NOT NULL CONSTRAINT DF_Grupo_Id DEFAULT NEWSEQUENTIALID(),
    Chave        varchar(100)     NOT NULL,
    Nome         nvarchar(150)    NOT NULL,
    Descricao    nvarchar(500)    NULL,
    CriadoEm     datetime2        NOT NULL CONSTRAINT DF_Grupo_CriadoEm DEFAULT SYSUTCDATETIME(),
    Ativo        bit              NOT NULL CONSTRAINT DF_Grupo_Ativo DEFAULT 1,
    AtualizadoEm datetime2        NULL,
    Versao       rowversion       NOT NULL,
    CONSTRAINT PK_Grupo PRIMARY KEY (Id),
    CONSTRAINT UQ_Grupo_Chave UNIQUE (Chave)
);

CREATE TABLE catalogo.Entidade (
    Id              uniqueidentifier NOT NULL CONSTRAINT DF_Entidade_Id DEFAULT NEWSEQUENTIALID(),
    GrupoId         uniqueidentifier NOT NULL,
    Chave           varchar(100)     NOT NULL,
    Nome            nvarchar(150)    NOT NULL,
    Descricao       nvarchar(500)    NULL,
    CampoExibicaoId uniqueidentifier NULL,
    CriadoEm        datetime2        NOT NULL CONSTRAINT DF_Entidade_CriadoEm DEFAULT SYSUTCDATETIME(),
    EscopoEquipe    bit              NOT NULL CONSTRAINT DF_Entidade_EscopoEquipe DEFAULT 1,
    Ativo           bit              NOT NULL CONSTRAINT DF_Entidade_Ativo DEFAULT 1,
    Ordem           int              NOT NULL CONSTRAINT DF_Entidade_Ordem DEFAULT 0,
    AtualizadoEm    datetime2        NULL,
    Versao          rowversion       NOT NULL,
    NomePlural      nvarchar(150)    NULL,
    Icone           varchar(50)      NULL,
    CONSTRAINT PK_Entidade PRIMARY KEY (Id),
    CONSTRAINT FK_Entidade_Grupo FOREIGN KEY (GrupoId) REFERENCES catalogo.Grupo (Id),
    CONSTRAINT UQ_Entidade_Grupo_Chave UNIQUE (GrupoId, Chave)
);

CREATE TABLE catalogo.Campo (
    Id                      uniqueidentifier NOT NULL CONSTRAINT DF_Campo_Id DEFAULT NEWSEQUENTIALID(),
    EntidadeId              uniqueidentifier NOT NULL,
    Chave                   varchar(100)     NOT NULL,
    Nome                    nvarchar(150)    NOT NULL,
    TipoDado                varchar(20)      NOT NULL,
    Obrigatorio             bit              NOT NULL CONSTRAINT DF_Campo_Obrigatorio DEFAULT 0,
    TamanhoMaximo           int              NULL,
    Precisao                tinyint          NULL,
    Escala                  tinyint          NULL,
    Opcoes                  nvarchar(max)    NULL,
    EntidadeReferenciadaId  uniqueidentifier NULL,
    AcaoExclusao            varchar(20)      NULL,
    Ordem                   int              NOT NULL CONSTRAINT DF_Campo_Ordem DEFAULT 0,
    Ativo                   bit              NOT NULL CONSTRAINT DF_Campo_Ativo DEFAULT 1,
    CriadoEm                datetime2        NOT NULL CONSTRAINT DF_Campo_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Campo PRIMARY KEY (Id),
    CONSTRAINT FK_Campo_Entidade FOREIGN KEY (EntidadeId) REFERENCES catalogo.Entidade (Id),
    CONSTRAINT FK_Campo_EntidadeReferenciada FOREIGN KEY (EntidadeReferenciadaId) REFERENCES catalogo.Entidade (Id),
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
        (TipoDado = 'LISTA' AND Opcoes IS NOT NULL AND ISJSON(Opcoes) = 1)
     OR (TipoDado <> 'LISTA' AND Opcoes IS NULL))
);

-- O campo de exibição precisa pertencer à própria entidade.
ALTER TABLE catalogo.Entidade ADD CONSTRAINT FK_Entidade_CampoExibicao
    FOREIGN KEY (CampoExibicaoId, Id) REFERENCES catalogo.Campo (Id, EntidadeId);

CREATE TABLE catalogo.CampoDependencia (
    CampoId         uniqueidentifier NOT NULL,
    CampoViaId      uniqueidentifier NULL,
    CampoOrigemId   uniqueidentifier NOT NULL,
    CampoAlvoId     uniqueidentifier NOT NULL,
    CONSTRAINT PK_CampoDependencia PRIMARY KEY (CampoId),
    CONSTRAINT FK_CampoDependencia_Campo  FOREIGN KEY (CampoId)       REFERENCES catalogo.Campo (Id),
    CONSTRAINT FK_CampoDependencia_Via    FOREIGN KEY (CampoViaId)    REFERENCES catalogo.Campo (Id),
    CONSTRAINT FK_CampoDependencia_Origem FOREIGN KEY (CampoOrigemId) REFERENCES catalogo.Campo (Id),
    CONSTRAINT FK_CampoDependencia_Alvo   FOREIGN KEY (CampoAlvoId)   REFERENCES catalogo.Campo (Id)
);

CREATE TABLE catalogo.RestricaoUnica (
    Id          uniqueidentifier NOT NULL CONSTRAINT DF_RestricaoUnica_Id DEFAULT NEWSEQUENTIALID(),
    EntidadeId  uniqueidentifier NOT NULL,
    Nome        varchar(100)     NOT NULL,
    CONSTRAINT PK_RestricaoUnica PRIMARY KEY (Id),
    CONSTRAINT FK_RestricaoUnica_Entidade FOREIGN KEY (EntidadeId) REFERENCES catalogo.Entidade (Id),
    CONSTRAINT UQ_RestricaoUnica_Entidade_Nome UNIQUE (EntidadeId, Nome),
    CONSTRAINT UQ_RestricaoUnica_Id_Entidade UNIQUE (Id, EntidadeId)
);

CREATE TABLE catalogo.RestricaoUnicaCampo (
    RestricaoUnicaId uniqueidentifier NOT NULL,
    EntidadeId       uniqueidentifier NOT NULL,
    CampoId          uniqueidentifier NOT NULL,
    CONSTRAINT PK_RestricaoUnicaCampo PRIMARY KEY (RestricaoUnicaId, CampoId),
    CONSTRAINT FK_RestricaoUnicaCampo_Restricao FOREIGN KEY (RestricaoUnicaId, EntidadeId)
        REFERENCES catalogo.RestricaoUnica (Id, EntidadeId) ON DELETE CASCADE,
    CONSTRAINT FK_RestricaoUnicaCampo_Campo FOREIGN KEY (CampoId, EntidadeId)
        REFERENCES catalogo.Campo (Id, EntidadeId)
);

/* ---------------------------------------------------------------------
   2. Acesso: equipes e usuários
   --------------------------------------------------------------------- */
PRINT '02_estrutura: 2/6 acesso (Equipe, Usuario, UsuarioEquipe, GrupoEquipe)';

CREATE TABLE catalogo.Equipe (
    Id        uniqueidentifier NOT NULL CONSTRAINT DF_Equipe_Id DEFAULT NEWSEQUENTIALID(),
    Nome      nvarchar(150)    NOT NULL,
    Ativa     bit              NOT NULL CONSTRAINT DF_Equipe_Ativa DEFAULT 1,
    CriadoEm  datetime2        NOT NULL CONSTRAINT DF_Equipe_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Equipe PRIMARY KEY (Id),
    CONSTRAINT UQ_Equipe_Nome UNIQUE (Nome)
);

CREATE TABLE catalogo.Usuario (
    Id               uniqueidentifier NOT NULL CONSTRAINT DF_Usuario_Id DEFAULT NEWSEQUENTIALID(),
    Matricula        varchar(20)      NOT NULL,
    Nome             nvarchar(150)    NOT NULL,
    SenhaHash        nvarchar(500)    NOT NULL,
    Perfil           varchar(20)      NOT NULL,
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

CREATE TABLE catalogo.UsuarioEquipe (
    UsuarioId  uniqueidentifier NOT NULL,
    EquipeId   uniqueidentifier NOT NULL,
    CriadoEm   datetime2        NOT NULL CONSTRAINT DF_UsuarioEquipe_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_UsuarioEquipe PRIMARY KEY (UsuarioId, EquipeId),
    CONSTRAINT FK_UsuarioEquipe_Usuario FOREIGN KEY (UsuarioId) REFERENCES catalogo.Usuario (Id) ON DELETE CASCADE,
    CONSTRAINT FK_UsuarioEquipe_Equipe  FOREIGN KEY (EquipeId)  REFERENCES catalogo.Equipe (Id)
);

CREATE TABLE catalogo.GrupoEquipe (
    GrupoId      uniqueidentifier NOT NULL,
    EquipeId     uniqueidentifier NOT NULL,
    CriadoPorId  uniqueidentifier NULL,
    CriadoEm     datetime2        NOT NULL CONSTRAINT DF_GrupoEquipe_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GrupoEquipe PRIMARY KEY (GrupoId, EquipeId),
    CONSTRAINT FK_GrupoEquipe_Grupo     FOREIGN KEY (GrupoId)     REFERENCES catalogo.Grupo (Id),
    CONSTRAINT FK_GrupoEquipe_Equipe    FOREIGN KEY (EquipeId)    REFERENCES catalogo.Equipe (Id),
    CONSTRAINT FK_GrupoEquipe_CriadoPor FOREIGN KEY (CriadoPorId) REFERENCES catalogo.Usuario (Id)
);
CREATE INDEX IX_GrupoEquipe_EquipeId ON catalogo.GrupoEquipe (EquipeId);

/* ---------------------------------------------------------------------
   3. Dados: itens cadastrados (Registro) e as suas tabelas auxiliares
   --------------------------------------------------------------------- */
PRINT '02_estrutura: 3/6 dados (Registro, RegistroReferencia, RegistroChaveUnica, RegistroValor)';

CREATE TABLE catalogo.Registro (
    Id                   uniqueidentifier NOT NULL CONSTRAINT DF_Registro_Id DEFAULT NEWSEQUENTIALID(),
    EntidadeId           uniqueidentifier NOT NULL,
    Dados                nvarchar(max)    NOT NULL CONSTRAINT DF_Registro_Dados DEFAULT N'{}',
    CriadoEm             datetime2        NOT NULL CONSTRAINT DF_Registro_CriadoEm DEFAULT SYSUTCDATETIME(),
    AtualizadoEm         datetime2        NULL,
    Versao               rowversion       NOT NULL,
    EquipeId             uniqueidentifier NULL CONSTRAINT FK_Registro_Equipe        REFERENCES catalogo.Equipe (Id),
    CriadoPorId          uniqueidentifier NULL CONSTRAINT FK_Registro_CriadoPor     REFERENCES catalogo.Usuario (Id),
    AtualizadoPorId      uniqueidentifier NULL CONSTRAINT FK_Registro_AtualizadoPor REFERENCES catalogo.Usuario (Id),
    SituacaoAprovacao    varchar(20)      NOT NULL CONSTRAINT DF_Registro_SituacaoAprovacao DEFAULT 'APROVADO'
                         CONSTRAINT CK_Registro_SituacaoAprovacao CHECK (SituacaoAprovacao IN ('PENDENTE','APROVADO','REJEITADO')),
    DecididoPorId        uniqueidentifier NULL CONSTRAINT FK_Registro_DecididoPor REFERENCES catalogo.Usuario (Id),
    DecididoEm           datetime2        NULL,
    JustificativaDecisao nvarchar(1000)   NULL,
    CONSTRAINT PK_Registro PRIMARY KEY (Id),
    CONSTRAINT FK_Registro_Entidade FOREIGN KEY (EntidadeId) REFERENCES catalogo.Entidade (Id),
    CONSTRAINT UQ_Registro_Id_Entidade UNIQUE (Id, EntidadeId),
    CONSTRAINT CK_Registro_Dados_Json CHECK (ISJSON(Dados) = 1)
);
CREATE INDEX IX_Registro_Entidade_CriadoEm ON catalogo.Registro (EntidadeId, CriadoEm DESC);
CREATE INDEX IX_Registro_Entidade_Equipe ON catalogo.Registro (EntidadeId, EquipeId);
CREATE INDEX IX_Registro_Situacao_Equipe ON catalogo.Registro (SituacaoAprovacao, EquipeId, CriadoEm);

CREATE TABLE catalogo.RegistroReferencia (
    RegistroOrigemId  uniqueidentifier NOT NULL,
    EntidadeOrigemId  uniqueidentifier NOT NULL,
    CampoId           uniqueidentifier NOT NULL,
    RegistroDestinoId uniqueidentifier NOT NULL,
    EntidadeDestinoId uniqueidentifier NOT NULL,
    CONSTRAINT PK_RegistroReferencia PRIMARY KEY (RegistroOrigemId, CampoId),
    CONSTRAINT FK_RegistroReferencia_Origem FOREIGN KEY (RegistroOrigemId, EntidadeOrigemId)
        REFERENCES catalogo.Registro (Id, EntidadeId) ON DELETE CASCADE,
    CONSTRAINT FK_RegistroReferencia_CampoOrigem FOREIGN KEY (CampoId, EntidadeOrigemId)
        REFERENCES catalogo.Campo (Id, EntidadeId),
    CONSTRAINT FK_RegistroReferencia_Destino FOREIGN KEY (RegistroDestinoId, EntidadeDestinoId)
        REFERENCES catalogo.Registro (Id, EntidadeId),
    CONSTRAINT FK_RegistroReferencia_CampoDestino FOREIGN KEY (CampoId, EntidadeDestinoId)
        REFERENCES catalogo.Campo (Id, EntidadeReferenciadaId)
);
CREATE INDEX IX_RegistroReferencia_Destino ON catalogo.RegistroReferencia (RegistroDestinoId, CampoId);

CREATE TABLE catalogo.RegistroChaveUnica (
    RestricaoUnicaId uniqueidentifier NOT NULL,
    EntidadeId       uniqueidentifier NOT NULL,
    RegistroId       uniqueidentifier NOT NULL,
    ValorHash        binary(32)       NOT NULL,
    CONSTRAINT PK_RegistroChaveUnica PRIMARY KEY (RestricaoUnicaId, RegistroId),
    CONSTRAINT UQ_RegistroChaveUnica_Valor UNIQUE (RestricaoUnicaId, ValorHash),
    CONSTRAINT FK_RegistroChaveUnica_Restricao FOREIGN KEY (RestricaoUnicaId, EntidadeId)
        REFERENCES catalogo.RestricaoUnica (Id, EntidadeId),
    CONSTRAINT FK_RegistroChaveUnica_Registro FOREIGN KEY (RegistroId, EntidadeId)
        REFERENCES catalogo.Registro (Id, EntidadeId) ON DELETE CASCADE
);

CREATE TABLE catalogo.RegistroValor (
    RegistroId    uniqueidentifier NOT NULL,
    EntidadeId    uniqueidentifier NOT NULL,
    CampoId       uniqueidentifier NOT NULL,
    ValorTexto    nvarchar(max)    NULL,
    ValorNumero   decimal(38,10)   NULL,
    ValorData     datetime2        NULL,
    ValorBooleano bit              NULL,
    CONSTRAINT PK_RegistroValor PRIMARY KEY (RegistroId, CampoId),
    CONSTRAINT FK_RegistroValor_Registro FOREIGN KEY (RegistroId, EntidadeId)
        REFERENCES catalogo.Registro (Id, EntidadeId) ON DELETE CASCADE,
    CONSTRAINT FK_RegistroValor_Campo FOREIGN KEY (CampoId, EntidadeId)
        REFERENCES catalogo.Campo (Id, EntidadeId),
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

CREATE TABLE catalogo.HistoricoAlteracao (
    Id               bigint IDENTITY(1,1) NOT NULL,
    AlvoTipo         varchar(20)      NOT NULL,
    AlvoId           uniqueidentifier NOT NULL,
    EntidadeId       uniqueidentifier NULL,
    EquipeId         uniqueidentifier NULL,
    Operacao         varchar(20)      NOT NULL,
    UsuarioId        uniqueidentifier NOT NULL,
    UsuarioMatricula varchar(20)      NOT NULL,
    OcorridoEm       datetime2        NOT NULL CONSTRAINT DF_HistoricoAlteracao_OcorridoEm DEFAULT SYSUTCDATETIME(),
    CorrelacaoId     uniqueidentifier NOT NULL,
    DadosAntes       nvarchar(max)    NULL,
    DadosDepois      nvarchar(max)    NULL,
    Justificativa    nvarchar(1000)   NULL,
    Origem           varchar(20)      NULL,
    CONSTRAINT PK_HistoricoAlteracao PRIMARY KEY (Id),
    CONSTRAINT FK_HistoricoAlteracao_Usuario  FOREIGN KEY (UsuarioId)  REFERENCES catalogo.Usuario (Id),
    CONSTRAINT FK_HistoricoAlteracao_Entidade FOREIGN KEY (EntidadeId) REFERENCES catalogo.Entidade (Id),
    CONSTRAINT FK_HistoricoAlteracao_Equipe   FOREIGN KEY (EquipeId)   REFERENCES catalogo.Equipe (Id),
    CONSTRAINT CK_HistoricoAlteracao_DadosAntes_Json  CHECK (DadosAntes  IS NULL OR ISJSON(DadosAntes)  = 1),
    CONSTRAINT CK_HistoricoAlteracao_DadosDepois_Json CHECK (DadosDepois IS NULL OR ISJSON(DadosDepois) = 1),
    CONSTRAINT CK_HistoricoAlteracao_Operacao
        CHECK (Operacao IN ('CRIACAO','ALTERACAO','EXCLUSAO','APROVACAO','REJEICAO')),
    CONSTRAINT CK_HistoricoAlteracao_AlvoTipo
        CHECK (AlvoTipo IN ('REGISTRO','USUARIO','EQUIPE','CONFIGURACAO','MODELO_AVALIACAO','AVALIACAO','METADADO')),
    CONSTRAINT CK_HistoricoAlteracao_Entidade
        CHECK ((AlvoTipo IN ('REGISTRO','CONFIGURACAO','MODELO_AVALIACAO','AVALIACAO') AND EntidadeId IS NOT NULL)
            OR (AlvoTipo IN ('USUARIO','EQUIPE','METADADO') AND EntidadeId IS NULL)),
    CONSTRAINT CK_HistoricoAlteracao_Origem CHECK (Origem IS NULL OR Origem IN ('IMPORTACAO'))
);
CREATE INDEX IX_HistoricoAlteracao_Alvo ON catalogo.HistoricoAlteracao (AlvoTipo, AlvoId, OcorridoEm);
CREATE INDEX IX_HistoricoAlteracao_Usuario
    ON catalogo.HistoricoAlteracao (UsuarioId, OcorridoEm DESC)
    INCLUDE (AlvoTipo, AlvoId, EntidadeId, Operacao);

CREATE TABLE catalogo.ConfiguracaoEntidadeEquipe (
    EntidadeId       uniqueidentifier NOT NULL,
    EquipeId         uniqueidentifier NOT NULL,
    RequerAprovacao  bit              NOT NULL CONSTRAINT DF_ConfiguracaoEntidadeEquipe_RequerAprovacao DEFAULT 0,
    AtualizadoPorId  uniqueidentifier NOT NULL,
    AtualizadoEm     datetime2        NOT NULL CONSTRAINT DF_ConfiguracaoEntidadeEquipe_AtualizadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ConfiguracaoEntidadeEquipe PRIMARY KEY (EntidadeId, EquipeId),
    CONSTRAINT FK_ConfiguracaoEntidadeEquipe_Entidade FOREIGN KEY (EntidadeId) REFERENCES catalogo.Entidade (Id),
    CONSTRAINT FK_ConfiguracaoEntidadeEquipe_Equipe FOREIGN KEY (EquipeId) REFERENCES catalogo.Equipe (Id),
    CONSTRAINT FK_ConfiguracaoEntidadeEquipe_AtualizadoPor FOREIGN KEY (AtualizadoPorId) REFERENCES catalogo.Usuario (Id)
);

/* ---------------------------------------------------------------------
   5. Avaliação de itens
   --------------------------------------------------------------------- */
PRINT '02_estrutura: 5/6 avaliação (ModeloAvaliacao, CriterioAvaliacao, Avaliacao, AvaliacaoResposta)';

CREATE TABLE catalogo.ModeloAvaliacao (
    Id               uniqueidentifier NOT NULL CONSTRAINT DF_ModeloAvaliacao_Id DEFAULT NEWSEQUENTIALID(),
    EntidadeId       uniqueidentifier NOT NULL,
    EquipeId         uniqueidentifier NOT NULL,
    Nome             nvarchar(150)    NOT NULL,
    Descricao        nvarchar(500)    NULL,
    Ativo            bit              NOT NULL CONSTRAINT DF_ModeloAvaliacao_Ativo DEFAULT 1,
    CriadoPorId      uniqueidentifier NOT NULL,
    CriadoEm         datetime2        NOT NULL CONSTRAINT DF_ModeloAvaliacao_CriadoEm DEFAULT SYSUTCDATETIME(),
    AtualizadoPorId  uniqueidentifier NULL,
    AtualizadoEm     datetime2        NULL,
    Versao           rowversion       NOT NULL,
    CONSTRAINT PK_ModeloAvaliacao PRIMARY KEY (Id),
    CONSTRAINT FK_ModeloAvaliacao_Entidade FOREIGN KEY (EntidadeId) REFERENCES catalogo.Entidade (Id),
    CONSTRAINT FK_ModeloAvaliacao_Equipe FOREIGN KEY (EquipeId) REFERENCES catalogo.Equipe (Id),
    CONSTRAINT FK_ModeloAvaliacao_CriadoPor FOREIGN KEY (CriadoPorId) REFERENCES catalogo.Usuario (Id),
    CONSTRAINT FK_ModeloAvaliacao_AtualizadoPor FOREIGN KEY (AtualizadoPorId) REFERENCES catalogo.Usuario (Id)
);
-- Um modelo ATIVO por tipo de item e equipe (índice único filtrado).
CREATE UNIQUE INDEX UX_ModeloAvaliacao_Ativo ON catalogo.ModeloAvaliacao (EntidadeId, EquipeId) WHERE Ativo = 1;

CREATE TABLE catalogo.CriterioAvaliacao (
    Id                 uniqueidentifier NOT NULL CONSTRAINT DF_CriterioAvaliacao_Id DEFAULT NEWSEQUENTIALID(),
    ModeloAvaliacaoId  uniqueidentifier NOT NULL,
    Chave              varchar(100)     NOT NULL,
    Nome               nvarchar(150)    NOT NULL,
    Descricao          nvarchar(500)    NULL,
    Tipo               varchar(20)      NOT NULL,
    Obrigatorio        bit              NOT NULL CONSTRAINT DF_CriterioAvaliacao_Obrigatorio DEFAULT 0,
    Comentario         varchar(20)      NOT NULL CONSTRAINT DF_CriterioAvaliacao_Comentario DEFAULT 'NENHUM',
    TamanhoMaximoTexto int              NOT NULL CONSTRAINT DF_CriterioAvaliacao_TamanhoMaximoTexto DEFAULT 2000,
    Ordem              int              NOT NULL,
    Ativo              bit              NOT NULL CONSTRAINT DF_CriterioAvaliacao_Ativo DEFAULT 1,
    CriadoEm           datetime2        NOT NULL CONSTRAINT DF_CriterioAvaliacao_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_CriterioAvaliacao PRIMARY KEY (Id),
    CONSTRAINT FK_CriterioAvaliacao_Modelo FOREIGN KEY (ModeloAvaliacaoId) REFERENCES catalogo.ModeloAvaliacao (Id),
    CONSTRAINT UQ_CriterioAvaliacao_Modelo_Chave UNIQUE (ModeloAvaliacaoId, Chave),
    CONSTRAINT CK_CriterioAvaliacao_Chave
        CHECK (LEN(Chave) > 0 AND Chave NOT LIKE '%[^a-z0-9_]%' COLLATE Latin1_General_BIN),
    CONSTRAINT CK_CriterioAvaliacao_Tipo CHECK (Tipo IN ('ESTRELAS','TEXTO')),
    CONSTRAINT CK_CriterioAvaliacao_Comentario CHECK (Comentario IN ('NENHUM','OPCIONAL','OBRIGATORIO')),
    CONSTRAINT CK_CriterioAvaliacao_ComentarioSoEmEstrelas CHECK (Tipo = 'ESTRELAS' OR Comentario = 'NENHUM'),
    CONSTRAINT CK_CriterioAvaliacao_TamanhoMaximoTexto CHECK (TamanhoMaximoTexto BETWEEN 1 AND 4000)
);

CREATE TABLE catalogo.Avaliacao (
    Id                 uniqueidentifier NOT NULL CONSTRAINT DF_Avaliacao_Id DEFAULT NEWSEQUENTIALID(),
    RegistroId         uniqueidentifier NOT NULL,
    ModeloAvaliacaoId  uniqueidentifier NOT NULL,
    UsuarioId          uniqueidentifier NOT NULL,
    CriadoEm           datetime2        NOT NULL CONSTRAINT DF_Avaliacao_CriadoEm DEFAULT SYSUTCDATETIME(),
    AtualizadoEm       datetime2        NULL,
    Versao             rowversion       NOT NULL,
    CONSTRAINT PK_Avaliacao PRIMARY KEY (Id),
    CONSTRAINT FK_Avaliacao_Registro FOREIGN KEY (RegistroId) REFERENCES catalogo.Registro (Id) ON DELETE CASCADE,
    CONSTRAINT FK_Avaliacao_Modelo FOREIGN KEY (ModeloAvaliacaoId) REFERENCES catalogo.ModeloAvaliacao (Id),
    CONSTRAINT FK_Avaliacao_Usuario FOREIGN KEY (UsuarioId) REFERENCES catalogo.Usuario (Id),
    CONSTRAINT UQ_Avaliacao_Registro_Modelo_Usuario UNIQUE (RegistroId, ModeloAvaliacaoId, UsuarioId)
);
CREATE INDEX IX_Avaliacao_Modelo ON catalogo.Avaliacao (ModeloAvaliacaoId);

CREATE TABLE catalogo.AvaliacaoResposta (
    AvaliacaoId          uniqueidentifier NOT NULL,
    CriterioAvaliacaoId  uniqueidentifier NOT NULL,
    Estrelas             tinyint          NULL,
    Texto                nvarchar(max)    NULL,
    CONSTRAINT PK_AvaliacaoResposta PRIMARY KEY (AvaliacaoId, CriterioAvaliacaoId),
    CONSTRAINT FK_AvaliacaoResposta_Avaliacao FOREIGN KEY (AvaliacaoId) REFERENCES catalogo.Avaliacao (Id) ON DELETE CASCADE,
    CONSTRAINT FK_AvaliacaoResposta_Criterio FOREIGN KEY (CriterioAvaliacaoId) REFERENCES catalogo.CriterioAvaliacao (Id),
    CONSTRAINT CK_AvaliacaoResposta_Estrelas CHECK (Estrelas IS NULL OR Estrelas BETWEEN 1 AND 5)
);
CREATE INDEX IX_AvaliacaoResposta_Criterio ON catalogo.AvaliacaoResposta (CriterioAvaliacaoId) INCLUDE (Estrelas);

/* ---------------------------------------------------------------------
   6. Importação de planilhas e controle de versões do schema
   --------------------------------------------------------------------- */
PRINT '02_estrutura: 6/6 importação (Importacao, ImportacaoArquivo) e controle de versões (__VersaoSchema)';

CREATE TABLE catalogo.Importacao (
    Id              uniqueidentifier NOT NULL CONSTRAINT DF_Importacao_Id DEFAULT NEWSEQUENTIALID(),
    GrupoId         uniqueidentifier NOT NULL,
    EntidadeId      uniqueidentifier NOT NULL,
    UsuarioId       uniqueidentifier NOT NULL,
    EquipePadraoId  uniqueidentifier NULL,
    NomeArquivo     nvarchar(260)    NOT NULL,
    Formato         varchar(4)       NOT NULL,
    Situacao        varchar(10)      NOT NULL CONSTRAINT DF_Importacao_Situacao DEFAULT 'ENVIADA',
    Opcoes          nvarchar(max)    NULL,
    Mapeamento      nvarchar(max)    NULL,
    Resumo          nvarchar(max)    NULL,
    CorrelacaoId    uniqueidentifier NOT NULL,
    CriadoEm        datetime2        NOT NULL CONSTRAINT DF_Importacao_CriadoEm DEFAULT SYSUTCDATETIME(),
    ConcluidaEm     datetime2        NULL,
    DesfeitaEm      datetime2        NULL,
    Versao          rowversion       NOT NULL,
    CONSTRAINT PK_Importacao PRIMARY KEY (Id),
    CONSTRAINT UQ_Importacao_CorrelacaoId UNIQUE (CorrelacaoId),
    CONSTRAINT FK_Importacao_Grupo    FOREIGN KEY (GrupoId)        REFERENCES catalogo.Grupo (Id),
    CONSTRAINT FK_Importacao_Entidade FOREIGN KEY (EntidadeId)     REFERENCES catalogo.Entidade (Id) ON DELETE CASCADE,
    CONSTRAINT FK_Importacao_Usuario  FOREIGN KEY (UsuarioId)      REFERENCES catalogo.Usuario (Id),
    CONSTRAINT FK_Importacao_Equipe   FOREIGN KEY (EquipePadraoId) REFERENCES catalogo.Equipe (Id),
    CONSTRAINT CK_Importacao_Formato  CHECK (Formato IN ('XLSX','CSV')),
    CONSTRAINT CK_Importacao_Situacao CHECK (Situacao IN ('ENVIADA','VALIDADA','CONCLUIDA','DESFEITA','EXPIRADA')),
    CONSTRAINT CK_Importacao_Opcoes_Json     CHECK (Opcoes     IS NULL OR ISJSON(Opcoes)     = 1),
    CONSTRAINT CK_Importacao_Mapeamento_Json CHECK (Mapeamento IS NULL OR ISJSON(Mapeamento) = 1),
    CONSTRAINT CK_Importacao_Resumo_Json     CHECK (Resumo     IS NULL OR ISJSON(Resumo)     = 1)
);
CREATE INDEX IX_Importacao_Entidade_CriadoEm ON catalogo.Importacao (EntidadeId, CriadoEm);

-- Arquivo enviado na importação (até 5 MB), apagado automaticamente pela aplicação depois de 7 dias.
CREATE TABLE catalogo.ImportacaoArquivo (
    ImportacaoId uniqueidentifier NOT NULL,
    Conteudo     varbinary(max)   NOT NULL,
    CONSTRAINT PK_ImportacaoArquivo PRIMARY KEY (ImportacaoId),
    CONSTRAINT FK_ImportacaoArquivo_Importacao FOREIGN KEY (ImportacaoId) REFERENCES catalogo.Importacao (Id) ON DELETE CASCADE
);

CREATE TABLE catalogo.__VersaoSchema (
    Versao      INT           NOT NULL CONSTRAINT PK___VersaoSchema PRIMARY KEY,
    ArquivoNome NVARCHAR(260) NOT NULL,
    AplicadoEm  DATETIME2(0)  NOT NULL CONSTRAINT DF___VersaoSchema_AplicadoEm DEFAULT SYSUTCDATETIME(),
    Observacao  NVARCHAR(500) NULL
);

INSERT catalogo.__VersaoSchema (Versao, ArquivoNome, Observacao)
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

COMMIT TRANSACTION;
PRINT '02_estrutura: estrutura criada com sucesso (23 tabelas no schema catalogo, versões 1 a 12 registradas).';
GO

SET NOEXEC OFF;
GO
