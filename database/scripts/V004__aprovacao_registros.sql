/* =====================================================================
   V004 — Aprovação de registros, ativável por entidade e por equipe
   ---------------------------------------------------------------------
   - ConfiguracaoEntidadeEquipe: configurações de um tipo de item (Entidade) para
     uma equipe. Hoje só RequerAprovacao; pensada para receber outras (ex.: avaliação).
     AUSÊNCIA DE LINHA = tudo desativado. Só entidades com EscopoEquipe = 1 podem
     ser configuradas (validado na aplicação).
   - Registro.SituacaoAprovacao (PENDENTE, APROVADO, REJEITADO) e a ÚLTIMA decisão
     (DecididoPorId, DecididoEm, JustificativaDecisao), para exibição rápida. Todas as
     decisões ficam no HistoricoAlteracao.
   - HistoricoAlteracao: operações APROVACAO e REJEICAO, alvo CONFIGURACAO e a
     coluna Justificativa.
   O seed NÃO ativa nada. Registros existentes ficam APROVADO (DEFAULT).
   O script pode rodar de novo sem erro.
   ===================================================================== */

IF OBJECT_ID(N'dbo.ConfiguracaoEntidadeEquipe', N'U') IS NULL
CREATE TABLE dbo.ConfiguracaoEntidadeEquipe (
    EntidadeId       uniqueidentifier NOT NULL,
    EquipeId         uniqueidentifier NOT NULL,
    RequerAprovacao  bit              NOT NULL CONSTRAINT DF_ConfiguracaoEntidadeEquipe_RequerAprovacao DEFAULT 0,
    AtualizadoPorId  uniqueidentifier NOT NULL,
    AtualizadoEm     datetime2        NOT NULL CONSTRAINT DF_ConfiguracaoEntidadeEquipe_AtualizadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_ConfiguracaoEntidadeEquipe PRIMARY KEY (EntidadeId, EquipeId),
    CONSTRAINT FK_ConfiguracaoEntidadeEquipe_Entidade FOREIGN KEY (EntidadeId) REFERENCES dbo.Entidade (Id),
    CONSTRAINT FK_ConfiguracaoEntidadeEquipe_Equipe FOREIGN KEY (EquipeId) REFERENCES dbo.Equipe (Id),
    CONSTRAINT FK_ConfiguracaoEntidadeEquipe_AtualizadoPor FOREIGN KEY (AtualizadoPorId) REFERENCES dbo.Usuario (Id)
);
GO

IF COL_LENGTH(N'dbo.Registro', N'SituacaoAprovacao') IS NULL
    ALTER TABLE dbo.Registro ADD
        SituacaoAprovacao    varchar(20)      NOT NULL CONSTRAINT DF_Registro_SituacaoAprovacao DEFAULT 'APROVADO'
                             CONSTRAINT CK_Registro_SituacaoAprovacao CHECK (SituacaoAprovacao IN ('PENDENTE','APROVADO','REJEITADO')),
        DecididoPorId        uniqueidentifier NULL CONSTRAINT FK_Registro_DecididoPor REFERENCES dbo.Usuario (Id),
        DecididoEm           datetime2        NULL,
        JustificativaDecisao nvarchar(1000)   NULL;
GO

-- Fila de aprovação: pendentes por equipe, dos mais antigos para os mais novos.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Registro_Situacao_Equipe' AND object_id = OBJECT_ID(N'dbo.Registro'))
    CREATE INDEX IX_Registro_Situacao_Equipe ON dbo.Registro (SituacaoAprovacao, EquipeId, CriadoEm);
GO

/* HistoricoAlteracao: novos valores e a justificativa das decisões. */
IF COL_LENGTH(N'dbo.HistoricoAlteracao', N'Justificativa') IS NULL
    ALTER TABLE dbo.HistoricoAlteracao ADD Justificativa nvarchar(1000) NULL;
GO

ALTER TABLE dbo.HistoricoAlteracao DROP CONSTRAINT IF EXISTS CK_HistoricoAlteracao_Operacao;
ALTER TABLE dbo.HistoricoAlteracao DROP CONSTRAINT IF EXISTS CK_HistoricoAlteracao_AlvoTipo;
ALTER TABLE dbo.HistoricoAlteracao DROP CONSTRAINT IF EXISTS CK_HistoricoAlteracao_Entidade;
GO

ALTER TABLE dbo.HistoricoAlteracao ADD
    CONSTRAINT CK_HistoricoAlteracao_Operacao
        CHECK (Operacao IN ('CRIACAO','ALTERACAO','EXCLUSAO','APROVACAO','REJEICAO')),
    -- CONFIGURACAO: AlvoId = equipe configurada, EntidadeId = entidade configurada.
    CONSTRAINT CK_HistoricoAlteracao_AlvoTipo
        CHECK (AlvoTipo IN ('REGISTRO','USUARIO','EQUIPE','CONFIGURACAO')),
    CONSTRAINT CK_HistoricoAlteracao_Entidade
        CHECK ((AlvoTipo IN ('REGISTRO','CONFIGURACAO') AND EntidadeId IS NOT NULL)
            OR (AlvoTipo IN ('USUARIO','EQUIPE') AND EntidadeId IS NULL));
GO
