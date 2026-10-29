/* ---------------------------------------------------------------------
   Extração da configuração de UMA área de gestão como INSERTs no schema cat, com os nomes físicos do padrão da CAIXA
   (usado por gerar_carga_area.sh; não rode sozinho)
   ---------------------------------------------------------------------
   @Modo = 'carga': a área é nova (INSERT de tudo). @Modo = 'reconfigurar': a área JÁ EXISTE no destino, sem itens: o grupo e os
   tipos de item são ATUALIZADOS pelo GUID (os novos são inseridos, os que sobram excluídos), e campos, unicidades e dependências
   são excluídos e gravados de novo, com os GUIDs da origem.
   Pré-requisitos: @GrupoChave varchar(100), @Prefixo nvarchar(60) (prefixo das mensagens PRINT) e @Modo varchar(20) declaradas, e as tabelas
   temporárias #Area* preenchidas (ler_area_cat.sql ou ler_area_catalogo.sql). Só lê: nenhuma alteração no banco.
   Escreve, uma por linha e em ordem, as instruções de carga: grupo (CATTB001), entidades (CATTB002, sem o campo de exibição),
   campos (CATTB003), campo de exibição (UPDATE), unicidades (CATTB005), campos das unicidades (CATTB006) e dependências
   (CATTB004), com os MESMOS identificadores da origem. Os valores (códigos de tipo, de escopo, de ação de exclusão) são os
   mesmos nos dois schemas: o V013 só renomeou tabelas e colunas. Não extrai itens, equipes, CATTB010 (equipes habilitadas),
   usuários, histórico, aprovação nem avaliação.
   Para (RAISERROR) se a área não existir ou se um campo dela referenciar um tipo de item de OUTRA área.
   --------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM #AreaGrupo)
BEGIN
    RAISERROR (N'A área de gestão %s não existe no schema de origem.', 16, 1, @GrupoChave);
    RETURN;
END;
IF EXISTS (SELECT 1 FROM #AreaCampo c WHERE c.EntidadeReferenciadaId IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM #AreaEntidade e WHERE e.Id = c.EntidadeReferenciadaId))
BEGIN
    RAISERROR (N'A área %s tem campos que referenciam tipos de item de outra área: a carga não seria autossuficiente.', 16, 1, @GrupoChave);
    RETURN;
END;

-- Literais SQL de cada texto, calculados num lugar só: N'...' com aspas dobradas; quebras de linha e tabulações
-- viram NCHAR(), para o script gerado não depender de espaços no fim das linhas.
DECLARE @Texto TABLE (Id uniqueidentifier NOT NULL, Coluna varchar(30) NOT NULL, Literal nvarchar(max) NOT NULL, PRIMARY KEY (Id, Coluna));
INSERT @Texto (Id, Coluna, Literal)
SELECT u.Id, u.Coluna,
       CASE WHEN u.Valor IS NULL THEN N'NULL'
            ELSE N'N''' + REPLACE(REPLACE(REPLACE(REPLACE(u.Valor, N'''', N''''''),
                                  NCHAR(13), N''' + NCHAR(13) + N'''), NCHAR(10), N''' + NCHAR(10) + N'''), NCHAR(9), N''' + NCHAR(9) + N''')
                 + N'''' END
FROM (
    SELECT g.Id, 'Chave', CAST(g.Chave AS nvarchar(max)) FROM #AreaGrupo g
    UNION ALL SELECT g.Id, 'Nome', g.Nome FROM #AreaGrupo g
    UNION ALL SELECT g.Id, 'Descricao', g.Descricao FROM #AreaGrupo g
    UNION ALL SELECT e.Id, 'Chave', CAST(e.Chave AS nvarchar(max)) FROM #AreaEntidade e
    UNION ALL SELECT e.Id, 'Nome', e.Nome FROM #AreaEntidade e
    UNION ALL SELECT e.Id, 'NomePlural', e.NomePlural FROM #AreaEntidade e
    UNION ALL SELECT e.Id, 'Descricao', e.Descricao FROM #AreaEntidade e
    UNION ALL SELECT e.Id, 'Icone', CAST(e.Icone AS nvarchar(max)) FROM #AreaEntidade e
    UNION ALL SELECT c.Id, 'Chave', CAST(c.Chave AS nvarchar(max)) FROM #AreaCampo c
    UNION ALL SELECT c.Id, 'Nome', c.Nome FROM #AreaCampo c
    UNION ALL SELECT c.Id, 'TipoDado', CAST(c.TipoDado AS nvarchar(max)) FROM #AreaCampo c
    UNION ALL SELECT c.Id, 'Opcoes', c.Opcoes FROM #AreaCampo c
    UNION ALL SELECT c.Id, 'AcaoExclusao', CAST(c.AcaoExclusao AS nvarchar(max)) FROM #AreaCampo c
    UNION ALL SELECT r.Id, 'Nome', CAST(r.Nome AS nvarchar(max)) FROM #AreaRestricao r
) u (Id, Coluna, Valor);

DECLARE @Saida TABLE (Ordem int IDENTITY(1,1) PRIMARY KEY, Linha nvarchar(max) NOT NULL);

-- Grupo
INSERT @Saida (Linha) VALUES (CONCAT(N'PRINT ''', @Prefixo, N': 1/7 área de gestão'';'));
IF @Modo = 'reconfigurar'
BEGIN
    -- A área já existe (mesmo GUID): é atualizada, e a configuração atual dela sai. Os tipos de item ficam (são atualizados
    -- abaixo, pelo GUID): o histórico tem FK para eles.
    INSERT @Saida (Linha) SELECT CONCAT(N'DECLARE @GrupoId uniqueidentifier = ''', g.Id, N''';') FROM #AreaGrupo g;
    INSERT @Saida (Linha)
    SELECT CONCAT(N'UPDATE cat.CATTB001_GRUPO SET NO_GRUPO = ', (SELECT Literal FROM @Texto WHERE Id = g.Id AND Coluna = 'Nome'),
                  N', DE_GRUPO = ', (SELECT Literal FROM @Texto WHERE Id = g.Id AND Coluna = 'Descricao'), N', IC_ATIVO_GRU = ', CAST(g.Ativo AS int),
                  N' WHERE NU_NSU_GRUPO = @GrupoId AND CO_CHAVE = ', (SELECT Literal FROM @Texto WHERE Id = g.Id AND Coluna = 'Chave'), N';')
    FROM #AreaGrupo g;
    INSERT @Saida (Linha) VALUES
        (CONCAT(N'IF @@ROWCOUNT <> 1 THROW 50004, N''', @Prefixo, N': a área de gestão do banco não tem o mesmo identificador (GUID) da origem. Nada foi gravado.'', 1;')),
        (N''),
        (CONCAT(N'PRINT ''', @Prefixo, N': removendo a configuração atual (campos, unicidades e dependências)...'';')),
        (N'UPDATE cat.CATTB002_ENTIDADE SET NU_CAMPO_EXIBICAO = NULL WHERE NU_NSU_CE_GRUPO_ENT = @GrupoId;'),
        (N'DELETE d FROM cat.CATTB004_CAMPO_DEPENDENCIA d'),
        (N'JOIN cat.CATTB003_CAMPO c ON c.NU_NSU_CAMPO = d.NU_NSU_CE_CAMPO_CD'),
        (N'JOIN cat.CATTB002_ENTIDADE e ON e.NU_NSU_ENTIDADE = c.NU_NSU_CE_ENTIDADE WHERE e.NU_NSU_CE_GRUPO_ENT = @GrupoId;'),
        (N'DELETE rc FROM cat.CATTB006_RESTRICAO_UNICA_CAMPO rc'),
        (N'JOIN cat.CATTB002_ENTIDADE e ON e.NU_NSU_ENTIDADE = rc.NU_NSU_CE_ENTIDADE_RUC WHERE e.NU_NSU_CE_GRUPO_ENT = @GrupoId;'),
        (N'DELETE r FROM cat.CATTB005_RESTRICAO_UNICA r'),
        (N'JOIN cat.CATTB002_ENTIDADE e ON e.NU_NSU_ENTIDADE = r.NU_NSU_CE_ENTIDADE_RU WHERE e.NU_NSU_CE_GRUPO_ENT = @GrupoId;'),
        (N'DELETE c FROM cat.CATTB003_CAMPO c'),
        (N'JOIN cat.CATTB002_ENTIDADE e ON e.NU_NSU_ENTIDADE = c.NU_NSU_CE_ENTIDADE WHERE e.NU_NSU_CE_GRUPO_ENT = @GrupoId;');
END
ELSE
BEGIN
    INSERT @Saida (Linha)
    SELECT CONCAT(N'INSERT cat.CATTB001_GRUPO (NU_NSU_GRUPO, CO_CHAVE, NO_GRUPO, DE_GRUPO, IC_ATIVO_GRU) VALUES (''', g.Id, N''', ',
                  (SELECT Literal FROM @Texto WHERE Id = g.Id AND Coluna = 'Chave'), N', ',
                  (SELECT Literal FROM @Texto WHERE Id = g.Id AND Coluna = 'Nome'), N', ',
                  (SELECT Literal FROM @Texto WHERE Id = g.Id AND Coluna = 'Descricao'), N', ', CAST(g.Ativo AS int), N');')
    FROM #AreaGrupo g;
END;

-- Entidades (o campo de exibição entra depois dos campos, por causa da FK composta). Na reconfiguração, por uma tabela
-- variável: atualiza as que existem, insere as novas e exclui as que sobram (só sem dados: o pré-requisito conferiu).
INSERT @Saida (Linha) VALUES (N''), (CONCAT(N'PRINT ''', @Prefixo, N': 2/7 tipos de item'';'));
IF @Modo = 'reconfigurar'
    INSERT @Saida (Linha) VALUES
        (N'DECLARE @Entidades TABLE (Id uniqueidentifier NOT NULL PRIMARY KEY, GrupoId uniqueidentifier NOT NULL, Chave nvarchar(200) NOT NULL,'),
        (N'    Nome nvarchar(400) NOT NULL, NomePlural nvarchar(400) NULL, Descricao nvarchar(max) NULL, Icone nvarchar(100) NULL,'),
        (N'    EscopoEquipe bit NOT NULL, Ativo bit NOT NULL, Ordem int NOT NULL);'),
        (N'INSERT @Entidades (Id, GrupoId, Chave, Nome, NomePlural, Descricao, Icone, EscopoEquipe, Ativo, Ordem) VALUES');
ELSE
    INSERT @Saida (Linha) VALUES
        (N'INSERT cat.CATTB002_ENTIDADE (NU_NSU_ENTIDADE, NU_NSU_CE_GRUPO_ENT, CO_CHAVE_ENT, NO_ENTIDADE_ENT, NO_PLURAL_ENT, DE_ENTIDADE_ENT, DE_ICONE_ENT, IC_ESCOPO_EQUIPE_ENT, IC_ATIVO_ENT, NU_ORDEM_ENT) VALUES');
INSERT @Saida (Linha)
SELECT CONCAT(N'    (''', e.Id, N''', ''', e.GrupoId, N''', ',
              (SELECT Literal FROM @Texto WHERE Id = e.Id AND Coluna = 'Chave'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = e.Id AND Coluna = 'Nome'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = e.Id AND Coluna = 'NomePlural'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = e.Id AND Coluna = 'Descricao'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = e.Id AND Coluna = 'Icone'), N', ',
              CAST(e.EscopoEquipe AS int), N', ', CAST(e.Ativo AS int), N', ', e.Ordem, N')',
              CASE WHEN ROW_NUMBER() OVER (ORDER BY e.Ordem DESC, e.Chave DESC) = 1 THEN N';' ELSE N',' END)
FROM #AreaEntidade e
ORDER BY e.Ordem, e.Chave;
IF @Modo = 'reconfigurar'
    INSERT @Saida (Linha) VALUES
        (N'DELETE e FROM cat.CATTB002_ENTIDADE e'),
        (N'WHERE e.NU_NSU_CE_GRUPO_ENT = @GrupoId AND NOT EXISTS (SELECT 1 FROM @Entidades n WHERE n.Id = e.NU_NSU_ENTIDADE);'),
        (N'UPDATE e SET CO_CHAVE_ENT = n.Chave, NO_ENTIDADE_ENT = n.Nome, NO_PLURAL_ENT = n.NomePlural, DE_ENTIDADE_ENT = n.Descricao,'),
        (N'    DE_ICONE_ENT = n.Icone, IC_ESCOPO_EQUIPE_ENT = n.EscopoEquipe, IC_ATIVO_ENT = n.Ativo, NU_ORDEM_ENT = n.Ordem'),
        (N'FROM cat.CATTB002_ENTIDADE e JOIN @Entidades n ON n.Id = e.NU_NSU_ENTIDADE AND n.GrupoId = e.NU_NSU_CE_GRUPO_ENT;'),
        (N'INSERT cat.CATTB002_ENTIDADE (NU_NSU_ENTIDADE, NU_NSU_CE_GRUPO_ENT, CO_CHAVE_ENT, NO_ENTIDADE_ENT, NO_PLURAL_ENT, DE_ENTIDADE_ENT, DE_ICONE_ENT, IC_ESCOPO_EQUIPE_ENT, IC_ATIVO_ENT, NU_ORDEM_ENT)'),
        (N'SELECT n.Id, n.GrupoId, n.Chave, n.Nome, n.NomePlural, n.Descricao, n.Icone, n.EscopoEquipe, n.Ativo, n.Ordem FROM @Entidades n'),
        (N'WHERE NOT EXISTS (SELECT 1 FROM cat.CATTB002_ENTIDADE e WHERE e.NU_NSU_ENTIDADE = n.Id);');

-- Campos
INSERT @Saida (Linha) VALUES (N''), (CONCAT(N'PRINT ''', @Prefixo, N': 3/7 campos'';')),
    (N'INSERT cat.CATTB003_CAMPO (NU_NSU_CAMPO, NU_NSU_CE_ENTIDADE, CO_CHAVE_CAM, NO_CAMPO, NO_TIPO_DADO_CAM, IC_OBRIGATORIO_CAM, NU_TAMANHO_MAXIMO, NU_PRECISAO, NU_ESCALA, DE_OPCAO, NU_ENTIDADE_REFERENCIADA, DE_ACAO_EXCLUSAO, NU_ORDEM_CAM, IC_ATIVO_CAM) VALUES');
INSERT @Saida (Linha)
SELECT CONCAT(N'    (''', c.Id, N''', ''', c.EntidadeId, N''', ',
              (SELECT Literal FROM @Texto WHERE Id = c.Id AND Coluna = 'Chave'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = c.Id AND Coluna = 'Nome'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = c.Id AND Coluna = 'TipoDado'), N', ',
              CAST(c.Obrigatorio AS int), N', ',
              COALESCE(CAST(c.TamanhoMaximo AS nvarchar(11)), N'NULL'), N', ',
              COALESCE(CAST(c.Precisao AS nvarchar(3)), N'NULL'), N', ',
              COALESCE(CAST(c.Escala AS nvarchar(3)), N'NULL'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = c.Id AND Coluna = 'Opcoes'), N', ',
              COALESCE(N'''' + CAST(c.EntidadeReferenciadaId AS nvarchar(36)) + N'''', N'NULL'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = c.Id AND Coluna = 'AcaoExclusao'), N', ',
              c.Ordem, N', ', CAST(c.Ativo AS int), N')',
              CASE WHEN ROW_NUMBER() OVER (ORDER BY e.Ordem DESC, e.Chave DESC, c.Ordem DESC, c.Chave DESC) = 1 THEN N';' ELSE N',' END,
              N'  -- ', e.Chave, N'.', c.Chave)
FROM #AreaCampo c JOIN #AreaEntidade e ON e.Id = c.EntidadeId
ORDER BY e.Ordem, e.Chave, c.Ordem, c.Chave;

-- Campo de exibição
INSERT @Saida (Linha) VALUES (N''), (CONCAT(N'PRINT ''', @Prefixo, N': 4/7 campo de exibição de cada tipo de item'';'));
IF EXISTS (SELECT 1 FROM #AreaEntidade WHERE CampoExibicaoId IS NOT NULL)
BEGIN
    INSERT @Saida (Linha) VALUES (N'UPDATE e SET NU_CAMPO_EXIBICAO = v.CampoExibicaoId'), (N'FROM cat.CATTB002_ENTIDADE e'), (N'JOIN (VALUES');
    INSERT @Saida (Linha)
    SELECT CONCAT(N'    (''', e.Id, N''', ''', e.CampoExibicaoId, N''')',
                  CASE WHEN ROW_NUMBER() OVER (ORDER BY e.Ordem DESC, e.Chave DESC) = 1 THEN N'' ELSE N',' END,
                  N'  -- ', e.Chave, N'.', c.Chave)
    FROM #AreaEntidade e JOIN #AreaCampo c ON c.Id = e.CampoExibicaoId
    ORDER BY e.Ordem, e.Chave;
    INSERT @Saida (Linha) VALUES (N') v (EntidadeId, CampoExibicaoId) ON v.EntidadeId = e.NU_NSU_ENTIDADE;');
END;

-- Unicidades
INSERT @Saida (Linha) VALUES (N''), (CONCAT(N'PRINT ''', @Prefixo, N': 5/7 unicidades'';'));
IF EXISTS (SELECT 1 FROM #AreaRestricao)
BEGIN
    INSERT @Saida (Linha) VALUES (N'INSERT cat.CATTB005_RESTRICAO_UNICA (NU_NSU_RESTRICAO_UNICA, NU_NSU_CE_ENTIDADE_RU, NO_RESTRICAO_UNICA) VALUES');
    INSERT @Saida (Linha)
    SELECT CONCAT(N'    (''', r.Id, N''', ''', r.EntidadeId, N''', ', (SELECT Literal FROM @Texto WHERE Id = r.Id AND Coluna = 'Nome'), N')',
                  CASE WHEN ROW_NUMBER() OVER (ORDER BY e.Ordem DESC, e.Chave DESC, r.Nome DESC) = 1 THEN N';' ELSE N',' END)
    FROM #AreaRestricao r JOIN #AreaEntidade e ON e.Id = r.EntidadeId
    ORDER BY e.Ordem, e.Chave, r.Nome;
END;

-- Campos das unicidades
INSERT @Saida (Linha) VALUES (N''), (CONCAT(N'PRINT ''', @Prefixo, N': 6/7 campos das unicidades'';'));
IF EXISTS (SELECT 1 FROM #AreaRestricaoCampo)
BEGIN
    INSERT @Saida (Linha) VALUES (N'INSERT cat.CATTB006_RESTRICAO_UNICA_CAMPO (NU_NSU_CE_RESTRICAO_UNICA_RUC, NU_NSU_CE_ENTIDADE_RUC, NU_NSU_CE_CAMPO_RUC) VALUES');
    INSERT @Saida (Linha)
    SELECT CONCAT(N'    (''', rc.RestricaoUnicaId, N''', ''', rc.EntidadeId, N''', ''', rc.CampoId, N''')',
                  CASE WHEN ROW_NUMBER() OVER (ORDER BY e.Ordem DESC, e.Chave DESC, r.Nome DESC, c.Ordem DESC, c.Chave DESC) = 1 THEN N';' ELSE N',' END,
                  N'  -- ', r.Nome, N': ', c.Chave)
    FROM #AreaRestricaoCampo rc
    JOIN #AreaRestricao r ON r.Id = rc.RestricaoUnicaId
    JOIN #AreaEntidade e ON e.Id = rc.EntidadeId
    JOIN #AreaCampo c ON c.Id = rc.CampoId
    ORDER BY e.Ordem, e.Chave, r.Nome, c.Ordem, c.Chave;
END;

-- Dependências
INSERT @Saida (Linha) VALUES (N''), (CONCAT(N'PRINT ''', @Prefixo, N': 7/7 dependências entre campos'';'));
IF EXISTS (SELECT 1 FROM #AreaDependencia)
BEGIN
    INSERT @Saida (Linha) VALUES (N'INSERT cat.CATTB004_CAMPO_DEPENDENCIA (NU_NSU_CE_CAMPO_CD, NU_CAMPO_VIA, NU_CAMPO_ORIGEM, NU_CAMPO_ALVO) VALUES');
    INSERT @Saida (Linha)
    SELECT CONCAT(N'    (''', d.CampoId, N''', ', COALESCE(N'''' + CAST(d.CampoViaId AS nvarchar(36)) + N'''', N'NULL'), N', ''',
                  d.CampoOrigemId, N''', ''', d.CampoAlvoId, N''')',
                  CASE WHEN ROW_NUMBER() OVER (ORDER BY e.Ordem DESC, e.Chave DESC, c.Ordem DESC, c.Chave DESC) = 1 THEN N';' ELSE N',' END,
                  N'  -- ', e.Chave, N'.', c.Chave)
    FROM #AreaDependencia d
    JOIN #AreaCampo c ON c.Id = d.CampoId
    JOIN #AreaEntidade e ON e.Id = c.EntidadeId
    ORDER BY e.Ordem, e.Chave, c.Ordem, c.Chave;
END;

SELECT Linha FROM @Saida ORDER BY Ordem;
