/* =====================================================================
   V009 — Listagens habilitadas por equipe (RN90 a RN93, ADR 0046)
   ---------------------------------------------------------------------
   - dbo.GrupoEquipe: as equipes que usam cada listagem (grupo). Um usuário não-administrador só vê uma listagem
     se pelo menos uma das suas equipes ativas está aqui; dentro dela, "suas equipes" passa a ser só as habilitadas.
     Quem mantém é o ADMINISTRADOR, pela API (PUT /api/v1/admin/metadados/grupos/{grupo}/equipes), com histórico METADADO.
   - Carga inicial, para nada mudar para quem já usa o sistema:
       * testes_desempenho: habilitada para TODAS as equipes existentes;
       * cada outra listagem: habilitada para as equipes que já têm itens nela.
     A carga só roda com a tabela vazia: rodar o script de novo não devolve equipes que o administrador removeu.
   - Não grava histórico da carga (é a situação de antes, não uma decisão de alguém): CriadoPorId nulo.
   A API guarda os metadados em cache por até 10 minutos: reinicie-a depois de aplicar.
   O script pode rodar de novo sem erro.
   ===================================================================== */

IF OBJECT_ID(N'dbo.GrupoEquipe', N'U') IS NULL
CREATE TABLE dbo.GrupoEquipe (
    GrupoId      uniqueidentifier NOT NULL,
    EquipeId     uniqueidentifier NOT NULL,
    CriadoPorId  uniqueidentifier NULL,
    CriadoEm     datetime2        NOT NULL CONSTRAINT DF_GrupoEquipe_CriadoEm DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_GrupoEquipe PRIMARY KEY (GrupoId, EquipeId),
    CONSTRAINT FK_GrupoEquipe_Grupo     FOREIGN KEY (GrupoId)     REFERENCES dbo.Grupo (Id),
    CONSTRAINT FK_GrupoEquipe_Equipe    FOREIGN KEY (EquipeId)    REFERENCES dbo.Equipe (Id),
    CONSTRAINT FK_GrupoEquipe_CriadoPor FOREIGN KEY (CriadoPorId) REFERENCES dbo.Usuario (Id)
);
GO

/* "Quais listagens esta equipe usa" (detalhe da equipe). */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_GrupoEquipe_EquipeId' AND object_id = OBJECT_ID(N'dbo.GrupoEquipe'))
    CREATE INDEX IX_GrupoEquipe_EquipeId ON dbo.GrupoEquipe (EquipeId);
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.GrupoEquipe)
BEGIN
    -- testes_desempenho: todas as equipes existentes.
    INSERT INTO dbo.GrupoEquipe (GrupoId, EquipeId)
    SELECT g.Id, e.Id
    FROM dbo.Grupo g
    CROSS JOIN dbo.Equipe e
    WHERE g.Chave = 'testes_desempenho';

    -- As outras listagens: as equipes que já têm itens nelas.
    INSERT INTO dbo.GrupoEquipe (GrupoId, EquipeId)
    SELECT DISTINCT en.GrupoId, r.EquipeId
    FROM dbo.Registro r
    JOIN dbo.Entidade en ON en.Id = r.EntidadeId
    JOIN dbo.Grupo g ON g.Id = en.GrupoId
    WHERE r.EquipeId IS NOT NULL
      AND g.Chave <> 'testes_desempenho';
END;

COMMIT TRANSACTION;
GO
