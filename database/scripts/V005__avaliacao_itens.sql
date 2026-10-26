/* =====================================================================
   V005 — Avaliação de itens, configurável por entidade e por equipe
   ---------------------------------------------------------------------
   - ModeloAvaliacao: o formulário de avaliação de uma entidade numa equipe.
     No máximo UM modelo ativo por (EntidadeId, EquipeId): índice único filtrado.
     Só entidades com EscopoEquipe = 1 têm modelo (validado na aplicação).
   - CriterioAvaliacao: as perguntas do modelo (ESTRELAS de 1 a 5, ou TEXTO livre).
     Critério com respostas não é excluído: é desativado (Ativo = 0).
   - Avaliacao: a avaliação de UM usuário sobre UM registro, num modelo.
     Excluir o registro exclui as avaliações (ON DELETE CASCADE).
   - AvaliacaoResposta: a resposta a um critério (estrelas e/ou texto).
   - HistoricoAlteracao: alvos MODELO_AVALIACAO e AVALIACAO.
   Tabelas relacionais tipadas (sem JSON), para médias e contagens em LINQ (docs/adr/0021).
   O seed NÃO cria nenhum modelo. O script pode rodar de novo sem erro.
   ===================================================================== */

-- Índice filtrado (UX_ModeloAvaliacao_Ativo) exige QUOTED_IDENTIFIER ON. O sqlcmd usa OFF por padrão.
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.ModeloAvaliacao', N'U') IS NULL
CREATE TABLE dbo.ModeloAvaliacao (
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
    CONSTRAINT FK_ModeloAvaliacao_Entidade FOREIGN KEY (EntidadeId) REFERENCES dbo.Entidade (Id),
    CONSTRAINT FK_ModeloAvaliacao_Equipe FOREIGN KEY (EquipeId) REFERENCES dbo.Equipe (Id),
    CONSTRAINT FK_ModeloAvaliacao_CriadoPor FOREIGN KEY (CriadoPorId) REFERENCES dbo.Usuario (Id),
    CONSTRAINT FK_ModeloAvaliacao_AtualizadoPor FOREIGN KEY (AtualizadoPorId) REFERENCES dbo.Usuario (Id)
);
GO

-- Um modelo ATIVO por entidade e equipe. Os inativos ficam (as avaliações deles continuam visíveis).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ModeloAvaliacao_Ativo' AND object_id = OBJECT_ID(N'dbo.ModeloAvaliacao'))
    CREATE UNIQUE INDEX UX_ModeloAvaliacao_Ativo ON dbo.ModeloAvaliacao (EntidadeId, EquipeId) WHERE Ativo = 1;
GO

IF OBJECT_ID(N'dbo.CriterioAvaliacao', N'U') IS NULL
CREATE TABLE dbo.CriterioAvaliacao (
    Id                 uniqueidentifier NOT NULL CONSTRAINT DF_CriterioAvaliacao_Id DEFAULT NEWSEQUENTIALID(),
    ModeloAvaliacaoId  uniqueidentifier NOT NULL,
    Chave              varchar(100)     NOT NULL,
    Nome               nvarchar(150)    NOT NULL,
    Descricao          nvarchar(500)    NULL,
    Tipo               varchar(20)      NOT NULL,
    Obrigatorio        bit              NOT NULL CONSTRAINT DF_CriterioAvaliacao_Obrigatorio DEFAULT 0,
    -- Só ESTRELAS: o avaliador justifica a nota em texto? NENHUM, OPCIONAL ou OBRIGATORIO.
    Comentario         varchar(20)      NOT NULL CONSTRAINT DF_CriterioAvaliacao_Comentario DEFAULT 'NENHUM',
    -- Vale para o texto livre (TEXTO) e para o comentário (ESTRELAS).
    TamanhoMaximoTexto int              NOT NULL CONSTRAINT DF_CriterioAvaliacao_TamanhoMaximoTexto DEFAULT 2000,
    Ordem              int              NOT NULL,
    Ativo              bit              NOT NULL CONSTRAINT DF_CriterioAvaliacao_Ativo DEFAULT 1,
    CriadoEm           datetime2        NOT NULL CONSTRAINT DF_CriterioAvaliacao_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_CriterioAvaliacao PRIMARY KEY (Id),
    CONSTRAINT FK_CriterioAvaliacao_Modelo FOREIGN KEY (ModeloAvaliacaoId) REFERENCES dbo.ModeloAvaliacao (Id),
    CONSTRAINT UQ_CriterioAvaliacao_Modelo_Chave UNIQUE (ModeloAvaliacaoId, Chave),
    -- ^[a-z0-9_]+$ : nenhum caractere fora de a-z, 0-9 e "_" (collation binária: maiúsculas não passam).
    CONSTRAINT CK_CriterioAvaliacao_Chave
        CHECK (LEN(Chave) > 0 AND Chave NOT LIKE '%[^a-z0-9_]%' COLLATE Latin1_General_BIN),
    CONSTRAINT CK_CriterioAvaliacao_Tipo CHECK (Tipo IN ('ESTRELAS','TEXTO')),
    CONSTRAINT CK_CriterioAvaliacao_Comentario CHECK (Comentario IN ('NENHUM','OPCIONAL','OBRIGATORIO')),
    CONSTRAINT CK_CriterioAvaliacao_ComentarioSoEmEstrelas CHECK (Tipo = 'ESTRELAS' OR Comentario = 'NENHUM'),
    CONSTRAINT CK_CriterioAvaliacao_TamanhoMaximoTexto CHECK (TamanhoMaximoTexto BETWEEN 1 AND 4000)
);
GO

IF OBJECT_ID(N'dbo.Avaliacao', N'U') IS NULL
CREATE TABLE dbo.Avaliacao (
    Id                 uniqueidentifier NOT NULL CONSTRAINT DF_Avaliacao_Id DEFAULT NEWSEQUENTIALID(),
    RegistroId         uniqueidentifier NOT NULL,
    ModeloAvaliacaoId  uniqueidentifier NOT NULL,
    UsuarioId          uniqueidentifier NOT NULL,
    CriadoEm           datetime2        NOT NULL CONSTRAINT DF_Avaliacao_CriadoEm DEFAULT SYSUTCDATETIME(),
    AtualizadoEm       datetime2        NULL,
    Versao             rowversion       NOT NULL,
    CONSTRAINT PK_Avaliacao PRIMARY KEY (Id),
    CONSTRAINT FK_Avaliacao_Registro FOREIGN KEY (RegistroId) REFERENCES dbo.Registro (Id) ON DELETE CASCADE,
    CONSTRAINT FK_Avaliacao_Modelo FOREIGN KEY (ModeloAvaliacaoId) REFERENCES dbo.ModeloAvaliacao (Id),
    CONSTRAINT FK_Avaliacao_Usuario FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuario (Id),
    -- Uma avaliação por usuário, por item e por modelo.
    CONSTRAINT UQ_Avaliacao_Registro_Modelo_Usuario UNIQUE (RegistroId, ModeloAvaliacaoId, UsuarioId)
);
GO

IF OBJECT_ID(N'dbo.AvaliacaoResposta', N'U') IS NULL
CREATE TABLE dbo.AvaliacaoResposta (
    AvaliacaoId          uniqueidentifier NOT NULL,
    CriterioAvaliacaoId  uniqueidentifier NOT NULL,
    Estrelas             tinyint          NULL,
    -- Resposta de um critério TEXTO ou comentário de um critério ESTRELAS.
    Texto                nvarchar(max)    NULL,
    CONSTRAINT PK_AvaliacaoResposta PRIMARY KEY (AvaliacaoId, CriterioAvaliacaoId),
    CONSTRAINT FK_AvaliacaoResposta_Avaliacao FOREIGN KEY (AvaliacaoId) REFERENCES dbo.Avaliacao (Id) ON DELETE CASCADE,
    CONSTRAINT FK_AvaliacaoResposta_Criterio FOREIGN KEY (CriterioAvaliacaoId) REFERENCES dbo.CriterioAvaliacao (Id),
    CONSTRAINT CK_AvaliacaoResposta_Estrelas CHECK (Estrelas IS NULL OR Estrelas BETWEEN 1 AND 5)
);
GO

-- Médias e "o critério já tem respostas?" (alteração de modelo) partem do critério.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AvaliacaoResposta_Criterio' AND object_id = OBJECT_ID(N'dbo.AvaliacaoResposta'))
    CREATE INDEX IX_AvaliacaoResposta_Criterio ON dbo.AvaliacaoResposta (CriterioAvaliacaoId) INCLUDE (Estrelas);
GO

-- Resumo de um modelo: avaliações de um modelo (a UQ acima já atende a busca por registro).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Avaliacao_Modelo' AND object_id = OBJECT_ID(N'dbo.Avaliacao'))
    CREATE INDEX IX_Avaliacao_Modelo ON dbo.Avaliacao (ModeloAvaliacaoId);
GO

/* HistoricoAlteracao: alvos MODELO_AVALIACAO e AVALIACAO.
   MODELO_AVALIACAO: AlvoId = equipe, EntidadeId = entidade (como CONFIGURACAO; o id do modelo vai nos dados).
   AVALIACAO: AlvoId = registro avaliado, EntidadeId/EquipeId = os do registro (o histórico sobrevive à exclusão). */
ALTER TABLE dbo.HistoricoAlteracao DROP CONSTRAINT IF EXISTS CK_HistoricoAlteracao_AlvoTipo;
ALTER TABLE dbo.HistoricoAlteracao DROP CONSTRAINT IF EXISTS CK_HistoricoAlteracao_Entidade;
GO

ALTER TABLE dbo.HistoricoAlteracao ADD
    CONSTRAINT CK_HistoricoAlteracao_AlvoTipo
        CHECK (AlvoTipo IN ('REGISTRO','USUARIO','EQUIPE','CONFIGURACAO','MODELO_AVALIACAO','AVALIACAO')),
    CONSTRAINT CK_HistoricoAlteracao_Entidade
        CHECK ((AlvoTipo IN ('REGISTRO','CONFIGURACAO','MODELO_AVALIACAO','AVALIACAO') AND EntidadeId IS NOT NULL)
            OR (AlvoTipo IN ('USUARIO','EQUIPE') AND EntidadeId IS NULL));
GO
