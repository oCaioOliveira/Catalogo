/* =====================================================================
   V011 — Importação de itens por planilha (Excel e CSV)
   ---------------------------------------------------------------------
   - dbo.Importacao: uma importação de UM tipo de item (entidade), do envio do arquivo até a conclusão (RN97 a RN104).
       Situacao: ENVIADA (arquivo guardado) → VALIDADA (prévia feita) → CONCLUIDA (itens gravados) → DESFEITA (itens criados
       removidos). EXPIRADA: nunca executada e com mais de 7 dias (a limpeza automática marca).
       Opcoes, Mapeamento e Resumo são JSON (o Resumo guarda o resultado de cada linha: a prévia paginada, o relatório e o desfazer
       leem dele). CorrelacaoId é o mesmo das linhas de histórico dos itens gravados pela importação.
       FK para a entidade com ON DELETE CASCADE: excluir um tipo de item (sem itens) leva junto as importações dele.
   - dbo.ImportacaoArquivo: o arquivo enviado (varbinary), no banco e não em disco, para funcionar igual em qualquer servidor.
       Fica separado para a listagem das importações não ler os arquivos. A limpeza apaga o arquivo depois de 7 dias e mantém a
       Importacao (ON DELETE CASCADE se a importação sair).
   - HistoricoAlteracao.Origem: NULL = cadastro manual (pela tela ou pela API); 'IMPORTACAO' = gravado por uma importação.
   O script pode rodar de novo sem erro.
   ===================================================================== */

IF OBJECT_ID(N'dbo.Importacao', N'U') IS NULL
CREATE TABLE dbo.Importacao (
    Id              uniqueidentifier NOT NULL CONSTRAINT DF_Importacao_Id DEFAULT NEWSEQUENTIALID(),
    GrupoId         uniqueidentifier NOT NULL,
    EntidadeId      uniqueidentifier NOT NULL,
    UsuarioId       uniqueidentifier NOT NULL,                 -- quem importou
    EquipePadraoId  uniqueidentifier NULL,                     -- equipe das linhas sem a coluna Equipe
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
    CONSTRAINT FK_Importacao_Grupo    FOREIGN KEY (GrupoId)        REFERENCES dbo.Grupo (Id),
    CONSTRAINT FK_Importacao_Entidade FOREIGN KEY (EntidadeId)     REFERENCES dbo.Entidade (Id) ON DELETE CASCADE,
    CONSTRAINT FK_Importacao_Usuario  FOREIGN KEY (UsuarioId)      REFERENCES dbo.Usuario (Id),
    CONSTRAINT FK_Importacao_Equipe   FOREIGN KEY (EquipePadraoId) REFERENCES dbo.Equipe (Id),
    CONSTRAINT CK_Importacao_Formato  CHECK (Formato IN ('XLSX','CSV')),
    CONSTRAINT CK_Importacao_Situacao CHECK (Situacao IN ('ENVIADA','VALIDADA','CONCLUIDA','DESFEITA','EXPIRADA')),
    CONSTRAINT CK_Importacao_Opcoes_Json     CHECK (Opcoes     IS NULL OR ISJSON(Opcoes)     = 1),
    CONSTRAINT CK_Importacao_Mapeamento_Json CHECK (Mapeamento IS NULL OR ISJSON(Mapeamento) = 1),
    CONSTRAINT CK_Importacao_Resumo_Json     CHECK (Resumo     IS NULL OR ISJSON(Resumo)     = 1)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Importacao_Entidade_CriadoEm' AND object_id = OBJECT_ID(N'dbo.Importacao'))
    CREATE INDEX IX_Importacao_Entidade_CriadoEm ON dbo.Importacao (EntidadeId, CriadoEm);
GO

IF OBJECT_ID(N'dbo.ImportacaoArquivo', N'U') IS NULL
CREATE TABLE dbo.ImportacaoArquivo (
    ImportacaoId uniqueidentifier NOT NULL,
    Conteudo     varbinary(max)   NOT NULL,
    CONSTRAINT PK_ImportacaoArquivo PRIMARY KEY (ImportacaoId),
    CONSTRAINT FK_ImportacaoArquivo_Importacao FOREIGN KEY (ImportacaoId) REFERENCES dbo.Importacao (Id) ON DELETE CASCADE
);
GO

IF COL_LENGTH(N'dbo.HistoricoAlteracao', N'Origem') IS NULL
    ALTER TABLE dbo.HistoricoAlteracao ADD Origem varchar(20) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_HistoricoAlteracao_Origem')
    ALTER TABLE dbo.HistoricoAlteracao ADD CONSTRAINT CK_HistoricoAlteracao_Origem CHECK (Origem IS NULL OR Origem IN ('IMPORTACAO'));
GO
