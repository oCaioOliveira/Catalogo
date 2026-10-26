/* =====================================================================
   V006 — Administração de metadados pela API
   ---------------------------------------------------------------------
   A partir daqui, listagens novas (grupos, entidades, campos, restrições e
   dependências) são criadas e alteradas PELA API, só pelo ADMINISTRADOR
   (docs/api/administracao-de-metadados.md). Scripts V00x ficam só para schema.

   - Grupo.Ativo, Entidade.Ativo: desativar em vez de excluir (docs/adr/0028).
   - Entidade.Ordem: ordem da entidade no menu.
   - Grupo.Versao, Entidade.Versao (rowversion): concorrência otimista nas edições
     de metadados. Toda alteração de campo, restrição ou dependência atualiza
     Entidade.AtualizadoEm, o que também muda a Versao da entidade dona.
   - HistoricoAlteracao: alvo METADADO. AlvoId = grupo e EntidadeId SEMPRE nulo: a
     FK_HistoricoAlteracao_Entidade impediria excluir uma entidade, e o histórico
     nunca é apagado (RN32). A entidade alterada vai nos dados (DadosAntes/DadosDepois).
   O script pode rodar de novo sem erro.
   ===================================================================== */

IF COL_LENGTH(N'dbo.Grupo', N'Ativo') IS NULL
    ALTER TABLE dbo.Grupo ADD
        Ativo        bit       NOT NULL CONSTRAINT DF_Grupo_Ativo DEFAULT 1,
        AtualizadoEm datetime2 NULL,
        Versao       rowversion NOT NULL;
GO

IF COL_LENGTH(N'dbo.Entidade', N'Ativo') IS NULL
    ALTER TABLE dbo.Entidade ADD
        Ativo        bit       NOT NULL CONSTRAINT DF_Entidade_Ativo DEFAULT 1,
        Ordem        int       NOT NULL CONSTRAINT DF_Entidade_Ordem DEFAULT 0,
        AtualizadoEm datetime2 NULL,
        Versao       rowversion NOT NULL;
GO

/* Ordem do menu da listagem de Testes de Desempenho: a mesma ordem do seed (V001). */
UPDATE e SET Ordem = v.Ordem
FROM dbo.Entidade e
JOIN dbo.Grupo g ON g.Id = e.GrupoId AND g.Chave = 'testes_desempenho'
JOIN (VALUES ('comunidade', 1), ('sigla', 2), ('projeto', 3), ('endpoint_servico', 4), ('rtc', 5),
             ('ambiente', 6), ('status_resultado', 7), ('relatorio_teste', 8), ('cenario_teste', 9), ('link', 10)) v (Chave, Ordem)
     ON v.Chave = e.Chave
WHERE e.Ordem = 0;
GO

/* HistoricoAlteracao: alvo METADADO, com AlvoId = grupo e EntidadeId nulo (sobrevive à exclusão da entidade). */
ALTER TABLE dbo.HistoricoAlteracao DROP CONSTRAINT IF EXISTS CK_HistoricoAlteracao_AlvoTipo;
ALTER TABLE dbo.HistoricoAlteracao DROP CONSTRAINT IF EXISTS CK_HistoricoAlteracao_Entidade;
GO

ALTER TABLE dbo.HistoricoAlteracao ADD
    CONSTRAINT CK_HistoricoAlteracao_AlvoTipo
        CHECK (AlvoTipo IN ('REGISTRO','USUARIO','EQUIPE','CONFIGURACAO','MODELO_AVALIACAO','AVALIACAO','METADADO')),
    CONSTRAINT CK_HistoricoAlteracao_Entidade
        CHECK ((AlvoTipo IN ('REGISTRO','CONFIGURACAO','MODELO_AVALIACAO','AVALIACAO') AND EntidadeId IS NOT NULL)
            OR (AlvoTipo IN ('USUARIO','EQUIPE','METADADO') AND EntidadeId IS NULL));
GO
