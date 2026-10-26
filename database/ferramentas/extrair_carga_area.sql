/* ---------------------------------------------------------------------
   Extração da configuração de UMA área de gestão como INSERTs (usado por gerar_carga_area.sh; não rode sozinho)
   ---------------------------------------------------------------------
   Variável sqlcmd: $(GRUPO) = chave da área. Só lê: nenhuma alteração no banco.
   Escreve, uma por linha e em ordem, as instruções de carga: Grupo, Entidade (sem o campo de exibição), Campo,
   campo de exibição (UPDATE), RestricaoUnica, RestricaoUnicaCampo e CampoDependencia, com os MESMOS identificadores
   do banco de origem. Não extrai itens, equipes, GrupoEquipe, usuários, histórico, aprovação nem avaliação.
   Para (RAISERROR) se a área não existir ou se um campo dela referenciar um tipo de item de OUTRA área.
   --------------------------------------------------------------------- */
SET NOCOUNT ON;

DECLARE @GrupoChave varchar(100) = '$(GRUPO)';
DECLARE @Grupo uniqueidentifier = (SELECT Id FROM catalogo.Grupo WHERE Chave = @GrupoChave);
IF @Grupo IS NULL
BEGIN
    RAISERROR (N'A área de gestão %s não existe neste banco.', 16, 1, @GrupoChave);
    RETURN;
END;
IF EXISTS (SELECT 1 FROM catalogo.Campo c
           JOIN catalogo.Entidade e ON e.Id = c.EntidadeId
           JOIN catalogo.Entidade alvo ON alvo.Id = c.EntidadeReferenciadaId
           WHERE e.GrupoId = @Grupo AND alvo.GrupoId <> @Grupo)
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
    SELECT g.Id, 'Chave', CAST(g.Chave AS nvarchar(max)) FROM catalogo.Grupo g WHERE g.Id = @Grupo
    UNION ALL SELECT g.Id, 'Nome', g.Nome FROM catalogo.Grupo g WHERE g.Id = @Grupo
    UNION ALL SELECT g.Id, 'Descricao', g.Descricao FROM catalogo.Grupo g WHERE g.Id = @Grupo
    UNION ALL SELECT e.Id, 'Chave', CAST(e.Chave AS nvarchar(max)) FROM catalogo.Entidade e WHERE e.GrupoId = @Grupo
    UNION ALL SELECT e.Id, 'Nome', e.Nome FROM catalogo.Entidade e WHERE e.GrupoId = @Grupo
    UNION ALL SELECT e.Id, 'NomePlural', e.NomePlural FROM catalogo.Entidade e WHERE e.GrupoId = @Grupo
    UNION ALL SELECT e.Id, 'Descricao', e.Descricao FROM catalogo.Entidade e WHERE e.GrupoId = @Grupo
    UNION ALL SELECT e.Id, 'Icone', CAST(e.Icone AS nvarchar(max)) FROM catalogo.Entidade e WHERE e.GrupoId = @Grupo
    UNION ALL SELECT c.Id, 'Chave', CAST(c.Chave AS nvarchar(max)) FROM catalogo.Campo c JOIN catalogo.Entidade e ON e.Id = c.EntidadeId WHERE e.GrupoId = @Grupo
    UNION ALL SELECT c.Id, 'Nome', c.Nome FROM catalogo.Campo c JOIN catalogo.Entidade e ON e.Id = c.EntidadeId WHERE e.GrupoId = @Grupo
    UNION ALL SELECT c.Id, 'TipoDado', CAST(c.TipoDado AS nvarchar(max)) FROM catalogo.Campo c JOIN catalogo.Entidade e ON e.Id = c.EntidadeId WHERE e.GrupoId = @Grupo
    UNION ALL SELECT c.Id, 'Opcoes', c.Opcoes FROM catalogo.Campo c JOIN catalogo.Entidade e ON e.Id = c.EntidadeId WHERE e.GrupoId = @Grupo
    UNION ALL SELECT c.Id, 'AcaoExclusao', CAST(c.AcaoExclusao AS nvarchar(max)) FROM catalogo.Campo c JOIN catalogo.Entidade e ON e.Id = c.EntidadeId WHERE e.GrupoId = @Grupo
    UNION ALL SELECT r.Id, 'Nome', CAST(r.Nome AS nvarchar(max)) FROM catalogo.RestricaoUnica r JOIN catalogo.Entidade e ON e.Id = r.EntidadeId WHERE e.GrupoId = @Grupo
) u (Id, Coluna, Valor);

DECLARE @Saida TABLE (Ordem int IDENTITY(1,1) PRIMARY KEY, Linha nvarchar(max) NOT NULL);

-- Grupo
INSERT @Saida (Linha) VALUES (N'PRINT ''03_carga: 1/7 área de gestão'';');
INSERT @Saida (Linha)
SELECT CONCAT(N'INSERT catalogo.Grupo (Id, Chave, Nome, Descricao, Ativo) VALUES (''', g.Id, N''', ',
              (SELECT Literal FROM @Texto WHERE Id = g.Id AND Coluna = 'Chave'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = g.Id AND Coluna = 'Nome'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = g.Id AND Coluna = 'Descricao'), N', ', CAST(g.Ativo AS int), N');')
FROM catalogo.Grupo g WHERE g.Id = @Grupo;

-- Entidades (o campo de exibição entra depois dos campos, por causa da FK composta)
INSERT @Saida (Linha) VALUES (N''), (N'PRINT ''03_carga: 2/7 tipos de item'';'),
    (N'INSERT catalogo.Entidade (Id, GrupoId, Chave, Nome, NomePlural, Descricao, Icone, EscopoEquipe, Ativo, Ordem) VALUES');
INSERT @Saida (Linha)
SELECT CONCAT(N'    (''', e.Id, N''', ''', e.GrupoId, N''', ',
              (SELECT Literal FROM @Texto WHERE Id = e.Id AND Coluna = 'Chave'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = e.Id AND Coluna = 'Nome'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = e.Id AND Coluna = 'NomePlural'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = e.Id AND Coluna = 'Descricao'), N', ',
              (SELECT Literal FROM @Texto WHERE Id = e.Id AND Coluna = 'Icone'), N', ',
              CAST(e.EscopoEquipe AS int), N', ', CAST(e.Ativo AS int), N', ', e.Ordem, N')',
              CASE WHEN ROW_NUMBER() OVER (ORDER BY e.Ordem DESC, e.Chave DESC) = 1 THEN N';' ELSE N',' END)
FROM catalogo.Entidade e WHERE e.GrupoId = @Grupo
ORDER BY e.Ordem, e.Chave;

-- Campos
INSERT @Saida (Linha) VALUES (N''), (N'PRINT ''03_carga: 3/7 campos'';'),
    (N'INSERT catalogo.Campo (Id, EntidadeId, Chave, Nome, TipoDado, Obrigatorio, TamanhoMaximo, Precisao, Escala, Opcoes, EntidadeReferenciadaId, AcaoExclusao, Ordem, Ativo) VALUES');
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
FROM catalogo.Campo c JOIN catalogo.Entidade e ON e.Id = c.EntidadeId
WHERE e.GrupoId = @Grupo
ORDER BY e.Ordem, e.Chave, c.Ordem, c.Chave;

-- Campo de exibição
INSERT @Saida (Linha) VALUES (N''), (N'PRINT ''03_carga: 4/7 campo de exibição de cada tipo de item'';'),
    (N'UPDATE e SET CampoExibicaoId = v.CampoExibicaoId'),
    (N'FROM catalogo.Entidade e'),
    (N'JOIN (VALUES');
INSERT @Saida (Linha)
SELECT CONCAT(N'    (''', e.Id, N''', ''', e.CampoExibicaoId, N''')',
              CASE WHEN ROW_NUMBER() OVER (ORDER BY e.Ordem DESC, e.Chave DESC) = 1 THEN N'' ELSE N',' END,
              N'  -- ', e.Chave, N'.', c.Chave)
FROM catalogo.Entidade e JOIN catalogo.Campo c ON c.Id = e.CampoExibicaoId
WHERE e.GrupoId = @Grupo
ORDER BY e.Ordem, e.Chave;
INSERT @Saida (Linha) VALUES (N') v (EntidadeId, CampoExibicaoId) ON v.EntidadeId = e.Id;');

-- Unicidades
INSERT @Saida (Linha) VALUES (N''), (N'PRINT ''03_carga: 5/7 unicidades'';');
IF EXISTS (SELECT 1 FROM catalogo.RestricaoUnica r JOIN catalogo.Entidade e ON e.Id = r.EntidadeId WHERE e.GrupoId = @Grupo)
BEGIN
    INSERT @Saida (Linha) VALUES (N'INSERT catalogo.RestricaoUnica (Id, EntidadeId, Nome) VALUES');
    INSERT @Saida (Linha)
    SELECT CONCAT(N'    (''', r.Id, N''', ''', r.EntidadeId, N''', ', (SELECT Literal FROM @Texto WHERE Id = r.Id AND Coluna = 'Nome'), N')',
                  CASE WHEN ROW_NUMBER() OVER (ORDER BY e.Ordem DESC, e.Chave DESC, r.Nome DESC) = 1 THEN N';' ELSE N',' END)
    FROM catalogo.RestricaoUnica r JOIN catalogo.Entidade e ON e.Id = r.EntidadeId
    WHERE e.GrupoId = @Grupo
    ORDER BY e.Ordem, e.Chave, r.Nome;
END;

-- Campos das unicidades
INSERT @Saida (Linha) VALUES (N''), (N'PRINT ''03_carga: 6/7 campos das unicidades'';');
IF EXISTS (SELECT 1 FROM catalogo.RestricaoUnicaCampo rc JOIN catalogo.Entidade e ON e.Id = rc.EntidadeId WHERE e.GrupoId = @Grupo)
BEGIN
    INSERT @Saida (Linha) VALUES (N'INSERT catalogo.RestricaoUnicaCampo (RestricaoUnicaId, EntidadeId, CampoId) VALUES');
    INSERT @Saida (Linha)
    SELECT CONCAT(N'    (''', rc.RestricaoUnicaId, N''', ''', rc.EntidadeId, N''', ''', rc.CampoId, N''')',
                  CASE WHEN ROW_NUMBER() OVER (ORDER BY e.Ordem DESC, e.Chave DESC, r.Nome DESC, c.Ordem DESC, c.Chave DESC) = 1 THEN N';' ELSE N',' END,
                  N'  -- ', r.Nome, N': ', c.Chave)
    FROM catalogo.RestricaoUnicaCampo rc
    JOIN catalogo.RestricaoUnica r ON r.Id = rc.RestricaoUnicaId
    JOIN catalogo.Entidade e ON e.Id = rc.EntidadeId
    JOIN catalogo.Campo c ON c.Id = rc.CampoId
    WHERE e.GrupoId = @Grupo
    ORDER BY e.Ordem, e.Chave, r.Nome, c.Ordem, c.Chave;
END;

-- Dependências
INSERT @Saida (Linha) VALUES (N''), (N'PRINT ''03_carga: 7/7 dependências entre campos'';');
IF EXISTS (SELECT 1 FROM catalogo.CampoDependencia d JOIN catalogo.Campo c ON c.Id = d.CampoId JOIN catalogo.Entidade e ON e.Id = c.EntidadeId WHERE e.GrupoId = @Grupo)
BEGIN
    INSERT @Saida (Linha) VALUES (N'INSERT catalogo.CampoDependencia (CampoId, CampoViaId, CampoOrigemId, CampoAlvoId) VALUES');
    INSERT @Saida (Linha)
    SELECT CONCAT(N'    (''', d.CampoId, N''', ', COALESCE(N'''' + CAST(d.CampoViaId AS nvarchar(36)) + N'''', N'NULL'), N', ''',
                  d.CampoOrigemId, N''', ''', d.CampoAlvoId, N''')',
                  CASE WHEN ROW_NUMBER() OVER (ORDER BY e.Ordem DESC, e.Chave DESC, c.Ordem DESC, c.Chave DESC) = 1 THEN N';' ELSE N',' END,
                  N'  -- ', e.Chave, N'.', c.Chave)
    FROM catalogo.CampoDependencia d
    JOIN catalogo.Campo c ON c.Id = d.CampoId
    JOIN catalogo.Entidade e ON e.Id = c.EntidadeId
    WHERE e.GrupoId = @Grupo
    ORDER BY e.Ordem, e.Chave, c.Ordem, c.Chave;
END;

SELECT Linha FROM @Saida ORDER BY Ordem;
