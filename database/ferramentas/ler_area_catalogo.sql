/* ---------------------------------------------------------------------
   Leitura da configuração de UMA área de gestão no schema ANTIGO catalogo (V012, nomes lógicos) para as mesmas tabelas
   temporárias de ler_area_cat.sql (trecho reaproveitado, não é um script completo)
   ---------------------------------------------------------------------
   Só para gerar cargas a partir das tabelas antigas, que ficaram intactas no banco local (gerar_carga_area.sh com origem
   catalogo). Pré-requisito: @GrupoChave varchar(100) declarada.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'tempdb..#AreaDependencia') IS NOT NULL DROP TABLE #AreaDependencia;
IF OBJECT_ID(N'tempdb..#AreaRestricaoCampo') IS NOT NULL DROP TABLE #AreaRestricaoCampo;
IF OBJECT_ID(N'tempdb..#AreaRestricao') IS NOT NULL DROP TABLE #AreaRestricao;
IF OBJECT_ID(N'tempdb..#AreaCampo') IS NOT NULL DROP TABLE #AreaCampo;
IF OBJECT_ID(N'tempdb..#AreaEntidade') IS NOT NULL DROP TABLE #AreaEntidade;
IF OBJECT_ID(N'tempdb..#AreaGrupo') IS NOT NULL DROP TABLE #AreaGrupo;

SELECT g.Id, g.Chave, g.Nome, g.Descricao, g.Ativo
INTO #AreaGrupo
FROM catalogo.Grupo g WHERE g.Chave = @GrupoChave;

SELECT e.Id, e.GrupoId, e.Chave, e.Nome, e.NomePlural, e.Descricao, e.Icone, e.EscopoEquipe, e.Ativo, e.Ordem, e.CampoExibicaoId
INTO #AreaEntidade
FROM catalogo.Entidade e JOIN #AreaGrupo g ON g.Id = e.GrupoId;

SELECT c.Id, c.EntidadeId, c.Chave, c.Nome, c.TipoDado, c.Obrigatorio, c.TamanhoMaximo, c.Precisao, c.Escala, c.Opcoes,
       c.EntidadeReferenciadaId, c.AcaoExclusao, c.Ordem, c.Ativo
INTO #AreaCampo
FROM catalogo.Campo c JOIN #AreaEntidade e ON e.Id = c.EntidadeId;

SELECT r.Id, r.EntidadeId, r.Nome
INTO #AreaRestricao
FROM catalogo.RestricaoUnica r JOIN #AreaEntidade e ON e.Id = r.EntidadeId;

SELECT rc.RestricaoUnicaId, rc.EntidadeId, rc.CampoId
INTO #AreaRestricaoCampo
FROM catalogo.RestricaoUnicaCampo rc JOIN #AreaEntidade e ON e.Id = rc.EntidadeId;

SELECT d.CampoId, d.CampoViaId, d.CampoOrigemId, d.CampoAlvoId
INTO #AreaDependencia
FROM catalogo.CampoDependencia d JOIN #AreaCampo c ON c.Id = d.CampoId;
-- Fim da leitura (schema catalogo).
