/* =====================================================================
   V010 — Ícone dos tipos de item e índice dos itens recentes (etapa de design e usabilidade)
   ---------------------------------------------------------------------
   - Entidade.Icone varchar(50) NULL: o nome de um ícone do PrimeIcons ("pi-chart-line"), mostrado no menu, no título da
     listagem, no detalhe, nas seções de relacionados e nos atalhos da página Início. Nulo = a tela usa um ícone genérico.
     Novas entidades recebem o ícone pela administração (Áreas de gestão → tipo de item → aba Geral) ou pela API.
   - Preenche os ícones dos tipos de item da área testes_desempenho (só onde ainda estão vazios: não sobrescreve um ícone
     escolhido pela API). Exceção consciente à regra "metadados só pela API", como o V007 e o V008: muda só a carga do V001,
     para um ambiente novo nascer igual.
   - IX_HistoricoAlteracao_Usuario: "Continuar de onde parou" (GET /api/v1/grupos/{grupo}/eu/recentes) lê o histórico de UM
     usuário, do mais novo para o mais antigo. O índice de alvo (V003) não serve para essa busca.
   A Versao das entidades alteradas muda (AtualizadoEm). A API guarda os metadados em cache por até 10 minutos: reinicie-a.
   O script pode rodar de novo sem erro.
   ===================================================================== */

IF COL_LENGTH(N'dbo.Entidade', N'Icone') IS NULL
    ALTER TABLE dbo.Entidade ADD Icone varchar(50) NULL;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

UPDATE e SET Icone = v.Icone, AtualizadoEm = SYSUTCDATETIME()
FROM dbo.Entidade e
JOIN dbo.Grupo g ON g.Id = e.GrupoId AND g.Chave = 'testes_desempenho'
JOIN (VALUES
        ('relatorio_teste',  'pi-chart-line'),
        ('projeto',          'pi-briefcase'),
        ('endpoint_servico', 'pi-link'),
        ('rtc',              'pi-ticket'),
        ('comunidade',       'pi-users'),
        ('sigla',            'pi-tag'),
        ('ambiente',         'pi-server'),
        ('status_resultado', 'pi-flag'),
        ('cenario_teste',    'pi-list-check'),
        ('link',             'pi-external-link')) v (Chave, Icone)
     ON v.Chave = e.Chave
WHERE e.Icone IS NULL;

COMMIT TRANSACTION;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_HistoricoAlteracao_Usuario' AND object_id = OBJECT_ID(N'dbo.HistoricoAlteracao'))
    CREATE INDEX IX_HistoricoAlteracao_Usuario
        ON dbo.HistoricoAlteracao (UsuarioId, OcorridoEm DESC)
        INCLUDE (AlvoTipo, AlvoId, EntidadeId, Operacao);
GO
