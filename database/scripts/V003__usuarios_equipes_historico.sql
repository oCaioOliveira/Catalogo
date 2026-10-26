/* =====================================================================
   V003 — Usuários, equipes, escopo dos registros e histórico de alterações
   ---------------------------------------------------------------------
   - Equipe, Usuario, UsuarioEquipe: quem acessa e a quais equipes pertence.
     Equipes e usuários NUNCA são excluídos, só desativados (preserva o histórico).
   - Entidade.EscopoEquipe: 1 = cada registro pertence a uma equipe;
                            0 = registros GLOBAIS (tabelas de apoio de todas as equipes).
   - Registro.EquipeId / CriadoPorId / AtualizadoPorId.
   - HistoricoAlteracao: uma linha por alvo alterado (registro, usuário, equipe).

   ATENÇÃO (ambiente local): registros que já existirem ficam com EquipeId NULL,
   o que não é válido para entidades por equipe. Como no banco local só há dados
   de teste, faça o reset do banco depois deste script (database/README.md).

   O script pode rodar de novo sem erro.
   ===================================================================== */

IF OBJECT_ID(N'dbo.Equipe', N'U') IS NULL
CREATE TABLE dbo.Equipe (
    Id        uniqueidentifier NOT NULL CONSTRAINT DF_Equipe_Id DEFAULT NEWSEQUENTIALID(),
    Nome      nvarchar(150)    NOT NULL,
    Ativa     bit              NOT NULL CONSTRAINT DF_Equipe_Ativa DEFAULT 1,
    CriadoEm  datetime2        NOT NULL CONSTRAINT DF_Equipe_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Equipe PRIMARY KEY (Id),
    CONSTRAINT UQ_Equipe_Nome UNIQUE (Nome)
);
GO

IF OBJECT_ID(N'dbo.Usuario', N'U') IS NULL
CREATE TABLE dbo.Usuario (
    Id               uniqueidentifier NOT NULL CONSTRAINT DF_Usuario_Id DEFAULT NEWSEQUENTIALID(),
    Matricula        varchar(20)      NOT NULL,
    Nome             nvarchar(150)    NOT NULL,
    SenhaHash        nvarchar(500)    NOT NULL,   -- PBKDF2 (PasswordHasher). Nunca a senha.
    Perfil           varchar(20)      NOT NULL,   -- único por usuário, vale em todas as equipes dele
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
GO

-- Um usuário pode pertencer a várias equipes.
IF OBJECT_ID(N'dbo.UsuarioEquipe', N'U') IS NULL
CREATE TABLE dbo.UsuarioEquipe (
    UsuarioId  uniqueidentifier NOT NULL,
    EquipeId   uniqueidentifier NOT NULL,
    CriadoEm   datetime2        NOT NULL CONSTRAINT DF_UsuarioEquipe_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_UsuarioEquipe PRIMARY KEY (UsuarioId, EquipeId),
    CONSTRAINT FK_UsuarioEquipe_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario (Id) ON DELETE CASCADE,
    CONSTRAINT FK_UsuarioEquipe_Equipe  FOREIGN KEY (EquipeId)  REFERENCES dbo.Equipe (Id)
);
GO

/* Escopo dos registros da entidade: 1 = por equipe, 0 = global. */
IF COL_LENGTH(N'dbo.Entidade', N'EscopoEquipe') IS NULL
    ALTER TABLE dbo.Entidade ADD EscopoEquipe bit NOT NULL CONSTRAINT DF_Entidade_EscopoEquipe DEFAULT 1;
GO

IF COL_LENGTH(N'dbo.Registro', N'EquipeId') IS NULL
    ALTER TABLE dbo.Registro ADD
        EquipeId        uniqueidentifier NULL CONSTRAINT FK_Registro_Equipe        REFERENCES dbo.Equipe (Id),
        CriadoPorId     uniqueidentifier NULL CONSTRAINT FK_Registro_CriadoPor     REFERENCES dbo.Usuario (Id),
        AtualizadoPorId uniqueidentifier NULL CONSTRAINT FK_Registro_AtualizadoPor REFERENCES dbo.Usuario (Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Registro_Entidade_Equipe' AND object_id = OBJECT_ID(N'dbo.Registro'))
    CREATE INDEX IX_Registro_Entidade_Equipe ON dbo.Registro (EntidadeId, EquipeId);
GO

/* =====================================================================
   CLASSIFICAÇÃO A CONFIRMAR COM O NEGÓCIO (grupo testes_desempenho):
     GLOBAIS (EscopoEquipe = 0): comunidade, sigla, ambiente, status_resultado
     POR EQUIPE (EscopoEquipe = 1): projeto, endpoint_servico, rtc,
                                    relatorio_teste, cenario_teste, link
   Mudar depois exige um script novo e cuidado com os registros existentes
   (um registro de equipe não pode apontar para outra equipe, RN29).
   ===================================================================== */
UPDATE e SET EscopoEquipe = 0
FROM dbo.Entidade e
JOIN dbo.Grupo g ON g.Id = e.GrupoId
WHERE g.Chave = 'testes_desempenho'
  AND e.Chave IN ('comunidade', 'sigla', 'ambiente', 'status_resultado')
  AND e.EscopoEquipe = 1;
GO

/* Histórico de alterações: uma linha por alvo alterado.
   SEM FK para AlvoId: o histórico precisa sobreviver à exclusão do registro. */
IF OBJECT_ID(N'dbo.HistoricoAlteracao', N'U') IS NULL
CREATE TABLE dbo.HistoricoAlteracao (
    Id               bigint IDENTITY(1,1) NOT NULL,
    AlvoTipo         varchar(20)      NOT NULL,
    AlvoId           uniqueidentifier NOT NULL,
    EntidadeId       uniqueidentifier NULL,       -- só para AlvoTipo = REGISTRO
    EquipeId         uniqueidentifier NULL,       -- equipe do alvo no momento da alteração
    Operacao         varchar(20)      NOT NULL,
    UsuarioId        uniqueidentifier NOT NULL,   -- quem fez
    UsuarioMatricula varchar(20)      NOT NULL,   -- cópia, para leitura fácil
    OcorridoEm       datetime2        NOT NULL CONSTRAINT DF_HistoricoAlteracao_OcorridoEm DEFAULT SYSUTCDATETIME(),
    CorrelacaoId     uniqueidentifier NOT NULL,   -- agrupa as linhas de uma mesma operação (ex.: cascata)
    DadosAntes       nvarchar(max)    NULL,
    DadosDepois      nvarchar(max)    NULL,
    CONSTRAINT PK_HistoricoAlteracao PRIMARY KEY (Id),
    CONSTRAINT FK_HistoricoAlteracao_Usuario  FOREIGN KEY (UsuarioId)  REFERENCES dbo.Usuario (Id),
    CONSTRAINT FK_HistoricoAlteracao_Entidade FOREIGN KEY (EntidadeId) REFERENCES dbo.Entidade (Id),
    CONSTRAINT FK_HistoricoAlteracao_Equipe   FOREIGN KEY (EquipeId)   REFERENCES dbo.Equipe (Id),
    CONSTRAINT CK_HistoricoAlteracao_AlvoTipo CHECK (AlvoTipo IN ('REGISTRO','USUARIO','EQUIPE')),
    CONSTRAINT CK_HistoricoAlteracao_Operacao CHECK (Operacao IN ('CRIACAO','ALTERACAO','EXCLUSAO')),
    CONSTRAINT CK_HistoricoAlteracao_Entidade CHECK ((AlvoTipo = 'REGISTRO' AND EntidadeId IS NOT NULL)
                                                  OR (AlvoTipo <> 'REGISTRO' AND EntidadeId IS NULL)),
    CONSTRAINT CK_HistoricoAlteracao_DadosAntes_Json  CHECK (DadosAntes  IS NULL OR ISJSON(DadosAntes)  = 1),
    CONSTRAINT CK_HistoricoAlteracao_DadosDepois_Json CHECK (DadosDepois IS NULL OR ISJSON(DadosDepois) = 1)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HistoricoAlteracao_Alvo' AND object_id = OBJECT_ID(N'dbo.HistoricoAlteracao'))
    CREATE INDEX IX_HistoricoAlteracao_Alvo ON dbo.HistoricoAlteracao (AlvoTipo, AlvoId, OcorridoEm);
GO
