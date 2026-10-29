/* ---------------------------------------------------------------------
   Leitura da configuração de UMA área de gestão no schema cat (padrão da CAIXA) para tabelas temporárias com os nomes
   LÓGICOS (trecho reaproveitado, não é um script completo)
   ---------------------------------------------------------------------
   Pré-requisito: a variável @GrupoChave varchar(100) já declarada com a chave da área (ex.: 'testes_desempenho').
   Resultado: #AreaGrupo, #AreaEntidade, #AreaCampo, #AreaRestricao, #AreaRestricaoCampo e #AreaDependencia, só com a área
   pedida. São lidas por extrair_carga_area.sql e impressao_digital_area.sql. O de-para dos nomes é o do NomesBanco.
   Usado pelo gerador (gerar_carga_area.sh, origem cat) e pela própria carga gerada (autoconferência antes do COMMIT).
   Compatível com SQL Server 2014 (nível 120).
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'tempdb..#AreaDependencia') IS NOT NULL DROP TABLE #AreaDependencia;
IF OBJECT_ID(N'tempdb..#AreaRestricaoCampo') IS NOT NULL DROP TABLE #AreaRestricaoCampo;
IF OBJECT_ID(N'tempdb..#AreaRestricao') IS NOT NULL DROP TABLE #AreaRestricao;
IF OBJECT_ID(N'tempdb..#AreaCampo') IS NOT NULL DROP TABLE #AreaCampo;
IF OBJECT_ID(N'tempdb..#AreaEntidade') IS NOT NULL DROP TABLE #AreaEntidade;
IF OBJECT_ID(N'tempdb..#AreaGrupo') IS NOT NULL DROP TABLE #AreaGrupo;

SELECT g.NU_NSU_GRUPO AS Id, g.CO_CHAVE AS Chave, g.NO_GRUPO AS Nome, g.DE_GRUPO AS Descricao, g.IC_ATIVO_GRU AS Ativo
INTO #AreaGrupo
FROM cat.CATTB001_GRUPO g WHERE g.CO_CHAVE = @GrupoChave;

SELECT e.NU_NSU_ENTIDADE AS Id, e.NU_NSU_CE_GRUPO_ENT AS GrupoId, e.CO_CHAVE_ENT AS Chave, e.NO_ENTIDADE_ENT AS Nome,
       e.NO_PLURAL_ENT AS NomePlural, e.DE_ENTIDADE_ENT AS Descricao, e.DE_ICONE_ENT AS Icone, e.IC_ESCOPO_EQUIPE_ENT AS EscopoEquipe,
       e.IC_ATIVO_ENT AS Ativo, e.NU_ORDEM_ENT AS Ordem, e.NU_CAMPO_EXIBICAO AS CampoExibicaoId
INTO #AreaEntidade
FROM cat.CATTB002_ENTIDADE e JOIN #AreaGrupo g ON g.Id = e.NU_NSU_CE_GRUPO_ENT;

SELECT c.NU_NSU_CAMPO AS Id, c.NU_NSU_CE_ENTIDADE AS EntidadeId, c.CO_CHAVE_CAM AS Chave, c.NO_CAMPO AS Nome,
       c.NO_TIPO_DADO_CAM AS TipoDado, c.IC_OBRIGATORIO_CAM AS Obrigatorio, c.NU_TAMANHO_MAXIMO AS TamanhoMaximo,
       c.NU_PRECISAO AS Precisao, c.NU_ESCALA AS Escala, c.DE_OPCAO AS Opcoes, c.NU_ENTIDADE_REFERENCIADA AS EntidadeReferenciadaId,
       c.DE_ACAO_EXCLUSAO AS AcaoExclusao, c.NU_ORDEM_CAM AS Ordem, c.IC_ATIVO_CAM AS Ativo
INTO #AreaCampo
FROM cat.CATTB003_CAMPO c JOIN #AreaEntidade e ON e.Id = c.NU_NSU_CE_ENTIDADE;

SELECT r.NU_NSU_RESTRICAO_UNICA AS Id, r.NU_NSU_CE_ENTIDADE_RU AS EntidadeId, r.NO_RESTRICAO_UNICA AS Nome
INTO #AreaRestricao
FROM cat.CATTB005_RESTRICAO_UNICA r JOIN #AreaEntidade e ON e.Id = r.NU_NSU_CE_ENTIDADE_RU;

SELECT rc.NU_NSU_CE_RESTRICAO_UNICA_RUC AS RestricaoUnicaId, rc.NU_NSU_CE_ENTIDADE_RUC AS EntidadeId, rc.NU_NSU_CE_CAMPO_RUC AS CampoId
INTO #AreaRestricaoCampo
FROM cat.CATTB006_RESTRICAO_UNICA_CAMPO rc JOIN #AreaEntidade e ON e.Id = rc.NU_NSU_CE_ENTIDADE_RUC;

SELECT d.NU_NSU_CE_CAMPO_CD AS CampoId, d.NU_CAMPO_VIA AS CampoViaId, d.NU_CAMPO_ORIGEM AS CampoOrigemId, d.NU_CAMPO_ALVO AS CampoAlvoId
INTO #AreaDependencia
FROM cat.CATTB004_CAMPO_DEPENDENCIA d JOIN #AreaCampo c ON c.Id = d.NU_NSU_CE_CAMPO_CD;
-- Fim da leitura (schema cat).
