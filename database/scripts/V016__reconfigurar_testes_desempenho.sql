/* =====================================================================
   CATÁLOGO — Reconfiguração da área de gestão "Testes de Desempenho" (testes_desempenho), padrão CAIXA (schema cat)
   ---------------------------------------------------------------------
   GERADO por database/ferramentas/gerar_carga_area.sh (MODO=reconfigurar) em 2026-10-28 17:20, a partir do banco ListagemGenerica
   (schema de origem: cat). Não edite à mão: gere de novo.

   Leva a área (que já existe, com o mesmo GUID) à configuração da origem: atualiza a área e os tipos de item pelo GUID,
   exclui os campos, as unicidades e as dependências atuais e grava os da origem, com os mesmos identificadores (GUIDs) e chaves:
     - 1 área de gestão, 10 tipos de item, 52 campos (tipos, tamanhos, obrigatoriedade, opções, ordem, referências
       e ações de exclusão), o campo de exibição de cada tipo de item, 8 unicidades (11 campos) e 1 dependência(s).
   NÃO cria itens, equipes, usuários, histórico, aprovação nem avaliação. As equipes habilitadas na área não mudam.

   Pré-requisito: a estrutura do Catálogo no schema cat (cat.CATTB023_VERSAO_SCHEMA na versão 13 ou maior).
   Execução: SSMS (ou sqlcmd), conectado ao banco do Catálogo. Não usa variáveis de SQLCMD. Compatível com SQL Server 2014
   (nível 120). Tempo estimado: menos de 1 segundo.
   Proteção: para, sem alterar nada, se a estrutura não estiver instalada, se a área não existir ou se ela tiver itens
   (ou histórico, configuração por equipe, modelo de avaliação ou importação nos tipos de item). Se a área já estiver na
   configuração nova (mesma impressão digital), não faz nada: pode ser executado de novo.
   Tudo roda numa transação (um identificador repetido também desfaz tudo): antes do COMMIT, o script confere as quantidades e
   a impressão digital da configuração (0x55818132BC259070B41B24F6A76835156AC71928647B347C4CB44A4B60B66249); se algo divergir, desfaz tudo.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT 'reconfigurar_testes_desempenho: verificando pré-requisitos...';
IF OBJECT_ID(N'cat.CATTB023_VERSAO_SCHEMA', N'U') IS NULL OR OBJECT_ID(N'cat.CATTB001_GRUPO', N'U') IS NULL
BEGIN
    RAISERROR (N'reconfigurar_testes_desempenho: a estrutura do Catálogo não está instalada no schema cat. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF (SELECT MAX(NU_NSU_VERSAO) FROM cat.CATTB023_VERSAO_SCHEMA) < 13
BEGIN
    RAISERROR (N'reconfigurar_testes_desempenho: este script exige a estrutura na versão 13 ou maior (consulte cat.CATTB023_VERSAO_SCHEMA). Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF NOT EXISTS (SELECT 1 FROM cat.CATTB001_GRUPO WHERE CO_CHAVE = 'testes_desempenho')
BEGIN
    RAISERROR (N'reconfigurar_testes_desempenho: a área de gestão testes_desempenho não existe neste banco. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END;
GO

PRINT 'reconfigurar_testes_desempenho: conferindo se a área já está na configuração nova e se não tem dados...';
DECLARE @GrupoChave varchar(100) = 'testes_desempenho';
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
/* ---------------------------------------------------------------------
   Impressão digital da configuração de UMA área de gestão (trecho reaproveitado, não é um script completo)
   ---------------------------------------------------------------------
   Pré-requisito: as tabelas temporárias #AreaGrupo, #AreaEntidade, #AreaCampo, #AreaRestricao, #AreaRestricaoCampo e
   #AreaDependencia já preenchidas (ler_area_cat.sql ou ler_area_catalogo.sql), com os nomes LÓGICOS das colunas.
   Resultado:
     - @Canonico: uma linha de texto por objeto da configuração (área, tipos de item, campos, unicidades e seus campos,
       dependências), com TODOS os atributos de configuração, na ordem dos identificadores;
     - @Impressao: o SHA-256 dessas linhas. Duas áreas com a mesma impressão têm exatamente a mesma configuração, seja qual
       for o schema de origem (catalogo ou cat).
   Ficam de fora o que não é configuração: datas de criação e alteração e a versão (rowversion), que mudam a cada instalação.
   Exige SET QUOTED_IDENTIFIER ON (padrão do SSMS; no sqlcmd, use -I), por causa do .value() do XML.
   Compatível com SQL Server 2014: o HASHBYTES aceita no máximo 8000 bytes, então o texto é resumido em partes de 3500
   caracteres, encadeadas (hash = SHA-256(hash anterior + parte)).
   --------------------------------------------------------------------- */
DECLARE @Canonico TABLE (Secao tinyint NOT NULL, Chave1 uniqueidentifier NOT NULL, Chave2 uniqueidentifier NULL, Linha nvarchar(max) NOT NULL);

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 1, g.Id, NULL, CONCAT(N'Grupo|', g.Id, N'|', g.Chave, N'|', g.Nome, N'|', COALESCE(N'"' + g.Descricao + N'"', N'~'),
                             N'|Ativo=', CAST(g.Ativo AS int))
FROM #AreaGrupo g;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 2, e.Id, NULL, CONCAT(N'Entidade|', e.Id, N'|', e.Chave, N'|', e.Nome, N'|', COALESCE(N'"' + e.NomePlural + N'"', N'~'),
                             N'|', COALESCE(N'"' + e.Descricao + N'"', N'~'), N'|', COALESCE(e.Icone, N'~'),
                             N'|EscopoEquipe=', CAST(e.EscopoEquipe AS int), N'|Ativo=', CAST(e.Ativo AS int), N'|Ordem=', e.Ordem,
                             N'|CampoExibicao=', COALESCE(CAST(e.CampoExibicaoId AS nvarchar(36)), N'~'))
FROM #AreaEntidade e;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 3, c.Id, NULL, CONCAT(N'Campo|', c.Id, N'|', c.EntidadeId, N'|', c.Chave, N'|', c.Nome, N'|', c.TipoDado,
                             N'|Obrigatorio=', CAST(c.Obrigatorio AS int), N'|TamanhoMaximo=', COALESCE(CAST(c.TamanhoMaximo AS nvarchar(11)), N'~'),
                             N'|Precisao=', COALESCE(CAST(c.Precisao AS nvarchar(3)), N'~'), N'|Escala=', COALESCE(CAST(c.Escala AS nvarchar(3)), N'~'),
                             N'|Opcoes=', COALESCE(c.Opcoes, N'~'),
                             N'|Referencia=', COALESCE(CAST(c.EntidadeReferenciadaId AS nvarchar(36)), N'~'), N'|AcaoExclusao=', COALESCE(c.AcaoExclusao, N'~'),
                             N'|Ordem=', c.Ordem, N'|Ativo=', CAST(c.Ativo AS int))
FROM #AreaCampo c;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 4, r.Id, NULL, CONCAT(N'RestricaoUnica|', r.Id, N'|', r.EntidadeId, N'|', r.Nome)
FROM #AreaRestricao r;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 5, rc.RestricaoUnicaId, rc.CampoId, CONCAT(N'RestricaoUnicaCampo|', rc.RestricaoUnicaId, N'|', rc.EntidadeId, N'|', rc.CampoId)
FROM #AreaRestricaoCampo rc;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 6, d.CampoId, NULL, CONCAT(N'CampoDependencia|', d.CampoId, N'|Via=', COALESCE(CAST(d.CampoViaId AS nvarchar(36)), N'~'),
                                  N'|Origem=', d.CampoOrigemId, N'|Alvo=', d.CampoAlvoId)
FROM #AreaDependencia d;

DECLARE @TextoCanonico nvarchar(max) = COALESCE(
    (SELECT k.Linha + NCHAR(10) FROM @Canonico k ORDER BY k.Secao, k.Chave1, k.Chave2 FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), N'');
DECLARE @Impressao varbinary(32) = HASHBYTES('SHA2_256', N''), @Posicao int = 1;
WHILE @Posicao <= DATALENGTH(@TextoCanonico) / 2
BEGIN
    SET @Impressao = HASHBYTES('SHA2_256', @Impressao + CAST(SUBSTRING(@TextoCanonico, @Posicao, 3500) AS varbinary(max)));
    SET @Posicao = @Posicao + 3500;
END;
-- Fim do trecho da impressão digital.
IF (SELECT COUNT(*) FROM @Canonico WHERE Secao = 2) = 10
   AND (SELECT COUNT(*) FROM @Canonico WHERE Secao = 3) = 52
   AND (SELECT COUNT(*) FROM @Canonico WHERE Secao = 4) = 8
   AND (SELECT COUNT(*) FROM @Canonico WHERE Secao = 5) = 11
   AND (SELECT COUNT(*) FROM @Canonico WHERE Secao = 6) = 1
   AND @Impressao = 0x55818132BC259070B41B24F6A76835156AC71928647B347C4CB44A4B60B66249
BEGIN
    PRINT 'reconfigurar_testes_desempenho: a área testes_desempenho já está na configuração nova. Nada a fazer.';
    SET NOEXEC ON;
END
ELSE
BEGIN
    DECLARE @Dados nvarchar(400) = CONCAT(
        (SELECT COUNT(*) FROM cat.CATTB011_REGISTRO WHERE NU_NSU_CE_ENTIDADE_REG IN (SELECT Id FROM #AreaEntidade)), N' item(ns), ',
        (SELECT COUNT(*) FROM cat.CATTB015_HISTORICO_ALTERACAO WHERE NU_NSU_CE_ENTIDADE_HA IN (SELECT Id FROM #AreaEntidade)), N' linha(s) de histórico, ',
        (SELECT COUNT(*) FROM cat.CATTB016_CONFIG_ENTIDADE_EQUIPE WHERE NU_NSU_CE_ENTIDADE_CEE IN (SELECT Id FROM #AreaEntidade)), N' configuração(ões) por equipe, ',
        (SELECT COUNT(*) FROM cat.CATTB017_MODELO_AVALIACAO WHERE NU_NSU_CE_ENTIDADE_MA IN (SELECT Id FROM #AreaEntidade)), N' modelo(s) de avaliação, ',
        (SELECT COUNT(*) FROM cat.CATTB021_IMPORTACAO WHERE NU_NSU_CE_GRUPO_IMP IN (SELECT Id FROM #AreaGrupo)), N' importação(ões)');
    IF @Dados <> N'0 item(ns), 0 linha(s) de histórico, 0 configuração(ões) por equipe, 0 modelo(s) de avaliação, 0 importação(ões)'
    BEGIN
        RAISERROR (N'reconfigurar_testes_desempenho: a área testes_desempenho tem dados (%s). A reconfiguração só roda com a área vazia. Nada foi alterado.', 16, 1, @Dados);
        SET NOEXEC ON;
    END;
END;
GO

BEGIN TRANSACTION;

PRINT 'reconfigurar_testes_desempenho: 1/7 área de gestão';
DECLARE @GrupoId uniqueidentifier = '57A926CD-8E83-4FFF-ADD0-6A9C5411162B';
UPDATE cat.CATTB001_GRUPO SET NO_GRUPO = N'Testes de Desempenho', DE_GRUPO = NULL, IC_ATIVO_GRU = 1 WHERE NU_NSU_GRUPO = @GrupoId AND CO_CHAVE = N'testes_desempenho';
IF @@ROWCOUNT <> 1 THROW 50004, N'reconfigurar_testes_desempenho: a área de gestão do banco não tem o mesmo identificador (GUID) da origem. Nada foi gravado.', 1;

PRINT 'reconfigurar_testes_desempenho: removendo a configuração atual (campos, unicidades e dependências)...';
UPDATE cat.CATTB002_ENTIDADE SET NU_CAMPO_EXIBICAO = NULL WHERE NU_NSU_CE_GRUPO_ENT = @GrupoId;
DELETE d FROM cat.CATTB004_CAMPO_DEPENDENCIA d
JOIN cat.CATTB003_CAMPO c ON c.NU_NSU_CAMPO = d.NU_NSU_CE_CAMPO_CD
JOIN cat.CATTB002_ENTIDADE e ON e.NU_NSU_ENTIDADE = c.NU_NSU_CE_ENTIDADE WHERE e.NU_NSU_CE_GRUPO_ENT = @GrupoId;
DELETE rc FROM cat.CATTB006_RESTRICAO_UNICA_CAMPO rc
JOIN cat.CATTB002_ENTIDADE e ON e.NU_NSU_ENTIDADE = rc.NU_NSU_CE_ENTIDADE_RUC WHERE e.NU_NSU_CE_GRUPO_ENT = @GrupoId;
DELETE r FROM cat.CATTB005_RESTRICAO_UNICA r
JOIN cat.CATTB002_ENTIDADE e ON e.NU_NSU_ENTIDADE = r.NU_NSU_CE_ENTIDADE_RU WHERE e.NU_NSU_CE_GRUPO_ENT = @GrupoId;
DELETE c FROM cat.CATTB003_CAMPO c
JOIN cat.CATTB002_ENTIDADE e ON e.NU_NSU_ENTIDADE = c.NU_NSU_CE_ENTIDADE WHERE e.NU_NSU_CE_GRUPO_ENT = @GrupoId;

PRINT 'reconfigurar_testes_desempenho: 2/7 tipos de item';
DECLARE @Entidades TABLE (Id uniqueidentifier NOT NULL PRIMARY KEY, GrupoId uniqueidentifier NOT NULL, Chave nvarchar(200) NOT NULL,
    Nome nvarchar(400) NOT NULL, NomePlural nvarchar(400) NULL, Descricao nvarchar(max) NULL, Icone nvarchar(100) NULL,
    EscopoEquipe bit NOT NULL, Ativo bit NOT NULL, Ordem int NOT NULL);
INSERT @Entidades (Id, GrupoId, Chave, Nome, NomePlural, Descricao, Icone, EscopoEquipe, Ativo, Ordem) VALUES
    ('63C4BA1A-27F8-4459-8D6A-C6FB527D9780', '57A926CD-8E83-4FFF-ADD0-6A9C5411162B', N'relatorio_teste', N'Relatório de Teste', N'Relatórios de Teste', NULL, N'pi-chart-line', 1, 1, 1),
    ('E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', '57A926CD-8E83-4FFF-ADD0-6A9C5411162B', N'projeto', N'Projeto', N'Projetos', NULL, N'pi-briefcase', 1, 1, 2),
    ('895BCF5C-66C7-4F8A-A7BD-78DCC373A351', '57A926CD-8E83-4FFF-ADD0-6A9C5411162B', N'endpoint_servico', N'Endpoint de Serviço', N'Endpoints de Serviço', NULL, N'pi-link', 1, 1, 3),
    ('8189F1DA-3F69-4A2D-B081-C857571E725E', '57A926CD-8E83-4FFF-ADD0-6A9C5411162B', N'rtc', N'RTC', N'RTCs', NULL, N'pi-ticket', 1, 1, 4),
    ('D0C964DA-6229-486F-AC19-D6548D548383', '57A926CD-8E83-4FFF-ADD0-6A9C5411162B', N'comunidade', N'Comunidade', N'Comunidades', NULL, N'pi-users', 0, 1, 5),
    ('332B6ECB-F049-495F-B85E-D98DA46B94BC', '57A926CD-8E83-4FFF-ADD0-6A9C5411162B', N'sigla', N'Sigla', N'Siglas', NULL, N'pi-tag', 0, 1, 6),
    ('209E0E8B-511E-4C4F-951E-B78CBD87DE52', '57A926CD-8E83-4FFF-ADD0-6A9C5411162B', N'ambiente', N'Ambiente', N'Ambientes', NULL, N'pi-server', 0, 1, 7),
    ('FE97F789-793F-497C-BEEF-65E9B04D4B63', '57A926CD-8E83-4FFF-ADD0-6A9C5411162B', N'status_resultado', N'Status do Resultado', N'Status de Resultado', NULL, N'pi-flag', 0, 1, 8),
    ('4BB8007F-A74C-4F02-907A-83B1136E711C', '57A926CD-8E83-4FFF-ADD0-6A9C5411162B', N'cenario_teste', N'Cenário de Teste', N'Cenários de Teste', NULL, N'pi-list-check', 1, 1, 9),
    ('F5A7946B-6614-4B2E-966F-2FDF68E92E24', '57A926CD-8E83-4FFF-ADD0-6A9C5411162B', N'link', N'Link', N'Links', NULL, N'pi-external-link', 1, 1, 10);
DELETE e FROM cat.CATTB002_ENTIDADE e
WHERE e.NU_NSU_CE_GRUPO_ENT = @GrupoId AND NOT EXISTS (SELECT 1 FROM @Entidades n WHERE n.Id = e.NU_NSU_ENTIDADE);
UPDATE e SET CO_CHAVE_ENT = n.Chave, NO_ENTIDADE_ENT = n.Nome, NO_PLURAL_ENT = n.NomePlural, DE_ENTIDADE_ENT = n.Descricao,
    DE_ICONE_ENT = n.Icone, IC_ESCOPO_EQUIPE_ENT = n.EscopoEquipe, IC_ATIVO_ENT = n.Ativo, NU_ORDEM_ENT = n.Ordem
FROM cat.CATTB002_ENTIDADE e JOIN @Entidades n ON n.Id = e.NU_NSU_ENTIDADE AND n.GrupoId = e.NU_NSU_CE_GRUPO_ENT;
INSERT cat.CATTB002_ENTIDADE (NU_NSU_ENTIDADE, NU_NSU_CE_GRUPO_ENT, CO_CHAVE_ENT, NO_ENTIDADE_ENT, NO_PLURAL_ENT, DE_ENTIDADE_ENT, DE_ICONE_ENT, IC_ESCOPO_EQUIPE_ENT, IC_ATIVO_ENT, NU_ORDEM_ENT)
SELECT n.Id, n.GrupoId, n.Chave, n.Nome, n.NomePlural, n.Descricao, n.Icone, n.EscopoEquipe, n.Ativo, n.Ordem FROM @Entidades n
WHERE NOT EXISTS (SELECT 1 FROM cat.CATTB002_ENTIDADE e WHERE e.NU_NSU_ENTIDADE = n.Id);

PRINT 'reconfigurar_testes_desempenho: 3/7 campos';
INSERT cat.CATTB003_CAMPO (NU_NSU_CAMPO, NU_NSU_CE_ENTIDADE, CO_CHAVE_CAM, NO_CAMPO, NO_TIPO_DADO_CAM, IC_OBRIGATORIO_CAM, NU_TAMANHO_MAXIMO, NU_PRECISAO, NU_ESCALA, DE_OPCAO, NU_ENTIDADE_REFERENCIADA, DE_ACAO_EXCLUSAO, NU_ORDEM_CAM, IC_ATIVO_CAM) VALUES
    ('03144306-C6C6-43A5-A69A-B3F3B0E1BABD', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'titulo', N'Título', N'TEXTO', 1, 200, NULL, NULL, NULL, NULL, NULL, 1, 1),  -- relatorio_teste.titulo
    ('F0EA423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'projeto', N'Projeto', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', N'RESTRINGIR', 2, 1),  -- relatorio_teste.projeto
    ('F7EA423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'rtc', N'RTC', N'REFERENCIA', 0, NULL, NULL, NULL, NULL, '8189F1DA-3F69-4A2D-B081-C857571E725E', N'RESTRINGIR', 3, 1),  -- relatorio_teste.rtc
    ('FEEA423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'ambiente', N'Ambiente', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, '209E0E8B-511E-4C4F-951E-B78CBD87DE52', N'RESTRINGIR', 4, 1),  -- relatorio_teste.ambiente
    ('05EB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'status_resultado', N'Status', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, 'FE97F789-793F-497C-BEEF-65E9B04D4B63', N'RESTRINGIR', 5, 1),  -- relatorio_teste.status_resultado
    ('0CEB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'data_execucao', N'Data de execução', N'DATA_HORA', 1, NULL, NULL, NULL, NULL, NULL, NULL, 6, 1),  -- relatorio_teste.data_execucao
    ('14EE87E2-4619-437E-8E51-97F12FE20302', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'bateria_testes', N'Nº da bateria', N'INTEIRO', 1, NULL, NULL, NULL, NULL, NULL, NULL, 7, 1),  -- relatorio_teste.bateria_testes
    ('9B4FFC4D-4B1E-465A-A0BF-AD309050EA2B', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'tipo_teste', N'Tecnologia', N'LISTA', 1, NULL, NULL, NULL, N'["API","WEB","Fila MQ"]', NULL, NULL, 8, 1),  -- relatorio_teste.tipo_teste
    ('21EB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'executado_por', N'Executado por', N'TEXTO', 1, 150, NULL, NULL, NULL, NULL, NULL, 9, 1),  -- relatorio_teste.executado_por
    ('694C6C99-957A-4FE0-8BA7-7C887DDE08F7', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'comunidade_na_execucao', N'Comunidade na execução', N'TEXTO', 0, 100, NULL, NULL, NULL, NULL, NULL, 10, 1),  -- relatorio_teste.comunidade_na_execucao
    ('360669B2-3C63-4106-AC25-E0DE808A9CEA', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'host_gerenciador_filas', N'Host / gerenciador de filas', N'TEXTO', 0, 255, NULL, NULL, NULL, NULL, NULL, 11, 1),  -- relatorio_teste.host_gerenciador_filas
    ('AA2A0090-14D4-4BBD-9F68-08D482B6477D', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'endpoints_testados', N'Endpoints / filas testados', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 12, 1),  -- relatorio_teste.endpoints_testados
    ('28EB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'contexto', N'Contexto', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 13, 1),  -- relatorio_teste.contexto
    ('2FEB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'objetivo', N'Objetivo', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 14, 1),  -- relatorio_teste.objetivo
    ('36EB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'apontamentos', N'Apontamentos', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 15, 1),  -- relatorio_teste.apontamentos
    ('E43465A2-A623-4822-B00E-EB5945C6F00B', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'observacoes_cenarios', N'Observações dos cenários', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 16, 1),  -- relatorio_teste.observacoes_cenarios
    ('3DEB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'conclusao', N'Conclusão', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 17, 1),  -- relatorio_teste.conclusao
    ('C4B42B3C-1603-4CA4-BA8F-039B52A2C59B', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'script', N'Script', N'URL', 0, NULL, NULL, NULL, NULL, NULL, NULL, 18, 1),  -- relatorio_teste.script
    ('21EE31FB-3598-45BD-8296-4325081E9BA0', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'configuracoes_ambiente', N'Configurações do ambiente', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 19, 1),  -- relatorio_teste.configuracoes_ambiente
    ('A619D677-1684-41A1-9F7E-B6A98E87757E', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'infraestrutura', N'Infraestrutura', N'TEXTO', 0, 500, NULL, NULL, NULL, NULL, NULL, 20, 1),  -- relatorio_teste.infraestrutura
    ('FCB6EB0B-B482-42A6-9E16-E648C536C3B0', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'dados_originais', N'Dados originais (SharePoint)', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 21, 0),  -- relatorio_teste.dados_originais
    ('95EA423E-F36B-1410-86BB-000558E25CB6', 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', N'sigla', N'Sigla', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, '332B6ECB-F049-495F-B85E-D98DA46B94BC', N'RESTRINGIR', 1, 1),  -- projeto.sigla
    ('9CEA423E-F36B-1410-86BB-000558E25CB6', 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', N'nome', N'Nome', N'TEXTO', 1, 150, NULL, NULL, NULL, NULL, NULL, 2, 1),  -- projeto.nome
    ('A3EA423E-F36B-1410-86BB-000558E25CB6', 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', N'url_projeto', N'URL do projeto', N'URL', 0, NULL, NULL, NULL, NULL, NULL, NULL, 3, 1),  -- projeto.url_projeto
    ('AAEA423E-F36B-1410-86BB-000558E25CB6', '895BCF5C-66C7-4F8A-A7BD-78DCC373A351', N'projeto', N'Projeto', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', N'RESTRINGIR', 1, 1),  -- endpoint_servico.projeto
    ('B1EA423E-F36B-1410-86BB-000558E25CB6', '895BCF5C-66C7-4F8A-A7BD-78DCC373A351', N'nome_servico', N'Nome do serviço', N'TEXTO', 1, 500, NULL, NULL, NULL, NULL, NULL, 2, 1),  -- endpoint_servico.nome_servico
    ('B8EA423E-F36B-1410-86BB-000558E25CB6', '895BCF5C-66C7-4F8A-A7BD-78DCC373A351', N'metodo', N'Método', N'TEXTO', 0, 50, NULL, NULL, NULL, NULL, NULL, 3, 1),  -- endpoint_servico.metodo
    ('BFEA423E-F36B-1410-86BB-000558E25CB6', '895BCF5C-66C7-4F8A-A7BD-78DCC373A351', N'endpoint_url_fila', N'Endpoint / URL da fila', N'TEXTO', 1, 500, NULL, NULL, NULL, NULL, NULL, 4, 1),  -- endpoint_servico.endpoint_url_fila
    ('C6EA423E-F36B-1410-86BB-000558E25CB6', '895BCF5C-66C7-4F8A-A7BD-78DCC373A351', N'ativo', N'Ativo', N'BOOLEANO', 1, NULL, NULL, NULL, NULL, NULL, NULL, 5, 1),  -- endpoint_servico.ativo
    ('CDEA423E-F36B-1410-86BB-000558E25CB6', '8189F1DA-3F69-4A2D-B081-C857571E725E', N'rtc_externo_id', N'ID externo do RTC', N'TEXTO', 1, 20, NULL, NULL, NULL, NULL, NULL, 1, 1),  -- rtc.rtc_externo_id
    ('7A0D474B-A247-4C4F-86C0-DC86186F9523', '8189F1DA-3F69-4A2D-B081-C857571E725E', N'url', N'URL', N'URL', 0, NULL, NULL, NULL, NULL, NULL, NULL, 2, 1),  -- rtc.url
    ('80EA423E-F36B-1410-86BB-000558E25CB6', 'D0C964DA-6229-486F-AC19-D6548D548383', N'nome', N'Nome', N'TEXTO', 1, 100, NULL, NULL, NULL, NULL, NULL, 1, 1),  -- comunidade.nome
    ('87EA423E-F36B-1410-86BB-000558E25CB6', '332B6ECB-F049-495F-B85E-D98DA46B94BC', N'comunidade', N'Comunidade', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, 'D0C964DA-6229-486F-AC19-D6548D548383', N'RESTRINGIR', 1, 1),  -- sigla.comunidade
    ('8EEA423E-F36B-1410-86BB-000558E25CB6', '332B6ECB-F049-495F-B85E-D98DA46B94BC', N'codigo_sigla', N'Código da sigla', N'TEXTO', 1, 20, NULL, NULL, NULL, NULL, NULL, 2, 1),  -- sigla.codigo_sigla
    ('D4EA423E-F36B-1410-86BB-000558E25CB6', '209E0E8B-511E-4C4F-951E-B78CBD87DE52', N'nome', N'Nome', N'TEXTO', 1, 50, NULL, NULL, NULL, NULL, NULL, 1, 1),  -- ambiente.nome
    ('DBEA423E-F36B-1410-86BB-000558E25CB6', '209E0E8B-511E-4C4F-951E-B78CBD87DE52', N'servidor_gerenciador_filas', N'Servidor gerenciador de filas', N'TEXTO', 0, 255, NULL, NULL, NULL, NULL, NULL, 2, 1),  -- ambiente.servidor_gerenciador_filas
    ('E2EA423E-F36B-1410-86BB-000558E25CB6', '209E0E8B-511E-4C4F-951E-B78CBD87DE52', N'configuracoes_infraestrutura', N'Configurações de infraestrutura', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 3, 1),  -- ambiente.configuracoes_infraestrutura
    ('E9EA423E-F36B-1410-86BB-000558E25CB6', 'FE97F789-793F-497C-BEEF-65E9B04D4B63', N'descricao', N'Descrição', N'TEXTO', 1, 30, NULL, NULL, NULL, NULL, NULL, 1, 1),  -- status_resultado.descricao
    ('44EB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'relatorio_teste', N'Relatório', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'CASCATA', 1, 1),  -- cenario_teste.relatorio_teste
    ('4BEB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'endpoint_servico', N'Endpoint', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, '895BCF5C-66C7-4F8A-A7BD-78DCC373A351', N'RESTRINGIR', 2, 1),  -- cenario_teste.endpoint_servico
    ('52EB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'numero_cenario', N'Nº do cenário', N'INTEIRO', 1, NULL, NULL, NULL, NULL, NULL, NULL, 3, 1),  -- cenario_teste.numero_cenario
    ('C324EA34-7627-4D17-9B57-B7095DFE71AA', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'descricao_cenario', N'Descrição do cenário', N'TEXTO', 0, 150, NULL, NULL, NULL, NULL, NULL, 4, 1),  -- cenario_teste.descricao_cenario
    ('59EB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'usuarios_virtuais', N'Usuários virtuais', N'INTEIRO', 1, NULL, NULL, NULL, NULL, NULL, NULL, 5, 1),  -- cenario_teste.usuarios_virtuais
    ('0D8BC391-D237-4E65-B74D-822D747FABDA', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'tempo_medio_resposta_ms', N'Tempo médio (ms)', N'DECIMAL', 1, NULL, 18, 2, NULL, NULL, NULL, 6, 1),  -- cenario_teste.tempo_medio_resposta_ms
    ('76306AFB-CC69-44C2-A034-7A0DD9A0A75B', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'percentil_95_ms', N'Percentil 95 (ms)', N'DECIMAL', 0, NULL, 18, 2, NULL, NULL, NULL, 7, 1),  -- cenario_teste.percentil_95_ms
    ('6EEB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'vazao_media_tps', N'Vazão média (TPS)', N'DECIMAL', 1, NULL, 18, 2, NULL, NULL, NULL, 8, 1),  -- cenario_teste.vazao_media_tps
    ('75EB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'taxa_erro_percentual', N'Taxa de erro (%)', N'DECIMAL', 1, NULL, 5, 2, NULL, NULL, NULL, 9, 1),  -- cenario_teste.taxa_erro_percentual
    ('4CFAEE65-8757-48E3-BDEC-04D114DF8BD1', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'amostras', N'Amostras', N'INTEIRO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 10, 1),  -- cenario_teste.amostras
    ('7CEB423E-F36B-1410-86BB-000558E25CB6', 'F5A7946B-6614-4B2E-966F-2FDF68E92E24', N'relatorio_teste', N'Relatório', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'CASCATA', 1, 1),  -- link.relatorio_teste
    ('83EB423E-F36B-1410-86BB-000558E25CB6', 'F5A7946B-6614-4B2E-966F-2FDF68E92E24', N'rtc', N'RTC', N'REFERENCIA', 0, NULL, NULL, NULL, NULL, '8189F1DA-3F69-4A2D-B081-C857571E725E', N'RESTRINGIR', 2, 1),  -- link.rtc
    ('8AEB423E-F36B-1410-86BB-000558E25CB6', 'F5A7946B-6614-4B2E-966F-2FDF68E92E24', N'texto_exibicao', N'Texto de exibição', N'TEXTO', 1, 100, NULL, NULL, NULL, NULL, NULL, 3, 1),  -- link.texto_exibicao
    ('91EB423E-F36B-1410-86BB-000558E25CB6', 'F5A7946B-6614-4B2E-966F-2FDF68E92E24', N'url', N'URL', N'URL', 1, NULL, NULL, NULL, NULL, NULL, NULL, 4, 1);  -- link.url

PRINT 'reconfigurar_testes_desempenho: 4/7 campo de exibição de cada tipo de item';
UPDATE e SET NU_CAMPO_EXIBICAO = v.CampoExibicaoId
FROM cat.CATTB002_ENTIDADE e
JOIN (VALUES
    ('63C4BA1A-27F8-4459-8D6A-C6FB527D9780', '03144306-C6C6-43A5-A69A-B3F3B0E1BABD'),  -- relatorio_teste.titulo
    ('E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', '9CEA423E-F36B-1410-86BB-000558E25CB6'),  -- projeto.nome
    ('895BCF5C-66C7-4F8A-A7BD-78DCC373A351', 'B1EA423E-F36B-1410-86BB-000558E25CB6'),  -- endpoint_servico.nome_servico
    ('8189F1DA-3F69-4A2D-B081-C857571E725E', 'CDEA423E-F36B-1410-86BB-000558E25CB6'),  -- rtc.rtc_externo_id
    ('D0C964DA-6229-486F-AC19-D6548D548383', '80EA423E-F36B-1410-86BB-000558E25CB6'),  -- comunidade.nome
    ('332B6ECB-F049-495F-B85E-D98DA46B94BC', '8EEA423E-F36B-1410-86BB-000558E25CB6'),  -- sigla.codigo_sigla
    ('209E0E8B-511E-4C4F-951E-B78CBD87DE52', 'D4EA423E-F36B-1410-86BB-000558E25CB6'),  -- ambiente.nome
    ('FE97F789-793F-497C-BEEF-65E9B04D4B63', 'E9EA423E-F36B-1410-86BB-000558E25CB6'),  -- status_resultado.descricao
    ('4BB8007F-A74C-4F02-907A-83B1136E711C', '52EB423E-F36B-1410-86BB-000558E25CB6'),  -- cenario_teste.numero_cenario
    ('F5A7946B-6614-4B2E-966F-2FDF68E92E24', '8AEB423E-F36B-1410-86BB-000558E25CB6')  -- link.texto_exibicao
) v (EntidadeId, CampoExibicaoId) ON v.EntidadeId = e.NU_NSU_ENTIDADE;

PRINT 'reconfigurar_testes_desempenho: 5/7 unicidades';
INSERT cat.CATTB005_RESTRICAO_UNICA (NU_NSU_RESTRICAO_UNICA, NU_NSU_CE_ENTIDADE_RU, NO_RESTRICAO_UNICA) VALUES
    ('A6EB423E-F36B-1410-86BB-000558E25CB6', 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', N'uq_projeto_sigla_nome'),
    ('ADEB423E-F36B-1410-86BB-000558E25CB6', '8189F1DA-3F69-4A2D-B081-C857571E725E', N'uq_rtc_externo_id'),
    ('98EB423E-F36B-1410-86BB-000558E25CB6', 'D0C964DA-6229-486F-AC19-D6548D548383', N'uq_comunidade_nome'),
    ('9FEB423E-F36B-1410-86BB-000558E25CB6', '332B6ECB-F049-495F-B85E-D98DA46B94BC', N'uq_sigla_codigo'),
    ('B4EB423E-F36B-1410-86BB-000558E25CB6', '209E0E8B-511E-4C4F-951E-B78CBD87DE52', N'uq_ambiente_nome'),
    ('BBEB423E-F36B-1410-86BB-000558E25CB6', 'FE97F789-793F-497C-BEEF-65E9B04D4B63', N'uq_status_descricao'),
    ('6F4D9EDB-CF00-4E86-AFDF-CBE668D91896', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'uq_cenario_relatorio_numero_endpoint'),
    ('C9EB423E-F36B-1410-86BB-000558E25CB6', 'F5A7946B-6614-4B2E-966F-2FDF68E92E24', N'uq_link_rtc');

PRINT 'reconfigurar_testes_desempenho: 6/7 campos das unicidades';
INSERT cat.CATTB006_RESTRICAO_UNICA_CAMPO (NU_NSU_CE_RESTRICAO_UNICA_RUC, NU_NSU_CE_ENTIDADE_RUC, NU_NSU_CE_CAMPO_RUC) VALUES
    ('A6EB423E-F36B-1410-86BB-000558E25CB6', 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', '95EA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_projeto_sigla_nome: sigla
    ('A6EB423E-F36B-1410-86BB-000558E25CB6', 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', '9CEA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_projeto_sigla_nome: nome
    ('ADEB423E-F36B-1410-86BB-000558E25CB6', '8189F1DA-3F69-4A2D-B081-C857571E725E', 'CDEA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_rtc_externo_id: rtc_externo_id
    ('98EB423E-F36B-1410-86BB-000558E25CB6', 'D0C964DA-6229-486F-AC19-D6548D548383', '80EA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_comunidade_nome: nome
    ('9FEB423E-F36B-1410-86BB-000558E25CB6', '332B6ECB-F049-495F-B85E-D98DA46B94BC', '8EEA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_sigla_codigo: codigo_sigla
    ('B4EB423E-F36B-1410-86BB-000558E25CB6', '209E0E8B-511E-4C4F-951E-B78CBD87DE52', 'D4EA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_ambiente_nome: nome
    ('BBEB423E-F36B-1410-86BB-000558E25CB6', 'FE97F789-793F-497C-BEEF-65E9B04D4B63', 'E9EA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_status_descricao: descricao
    ('6F4D9EDB-CF00-4E86-AFDF-CBE668D91896', '4BB8007F-A74C-4F02-907A-83B1136E711C', '44EB423E-F36B-1410-86BB-000558E25CB6'),  -- uq_cenario_relatorio_numero_endpoint: relatorio_teste
    ('6F4D9EDB-CF00-4E86-AFDF-CBE668D91896', '4BB8007F-A74C-4F02-907A-83B1136E711C', '4BEB423E-F36B-1410-86BB-000558E25CB6'),  -- uq_cenario_relatorio_numero_endpoint: endpoint_servico
    ('6F4D9EDB-CF00-4E86-AFDF-CBE668D91896', '4BB8007F-A74C-4F02-907A-83B1136E711C', '52EB423E-F36B-1410-86BB-000558E25CB6'),  -- uq_cenario_relatorio_numero_endpoint: numero_cenario
    ('C9EB423E-F36B-1410-86BB-000558E25CB6', 'F5A7946B-6614-4B2E-966F-2FDF68E92E24', '83EB423E-F36B-1410-86BB-000558E25CB6');  -- uq_link_rtc: rtc

PRINT 'reconfigurar_testes_desempenho: 7/7 dependências entre campos';
INSERT cat.CATTB004_CAMPO_DEPENDENCIA (NU_NSU_CE_CAMPO_CD, NU_CAMPO_VIA, NU_CAMPO_ORIGEM, NU_CAMPO_ALVO) VALUES
    ('4BEB423E-F36B-1410-86BB-000558E25CB6', '44EB423E-F36B-1410-86BB-000558E25CB6', 'F0EA423E-F36B-1410-86BB-000558E25CB6', 'AAEA423E-F36B-1410-86BB-000558E25CB6');  -- cenario_teste.endpoint_servico

PRINT 'reconfigurar_testes_desempenho: conferindo a configuração antes de gravar...';
DECLARE @GrupoChave varchar(100) = 'testes_desempenho';
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
/* ---------------------------------------------------------------------
   Impressão digital da configuração de UMA área de gestão (trecho reaproveitado, não é um script completo)
   ---------------------------------------------------------------------
   Pré-requisito: as tabelas temporárias #AreaGrupo, #AreaEntidade, #AreaCampo, #AreaRestricao, #AreaRestricaoCampo e
   #AreaDependencia já preenchidas (ler_area_cat.sql ou ler_area_catalogo.sql), com os nomes LÓGICOS das colunas.
   Resultado:
     - @Canonico: uma linha de texto por objeto da configuração (área, tipos de item, campos, unicidades e seus campos,
       dependências), com TODOS os atributos de configuração, na ordem dos identificadores;
     - @Impressao: o SHA-256 dessas linhas. Duas áreas com a mesma impressão têm exatamente a mesma configuração, seja qual
       for o schema de origem (catalogo ou cat).
   Ficam de fora o que não é configuração: datas de criação e alteração e a versão (rowversion), que mudam a cada instalação.
   Exige SET QUOTED_IDENTIFIER ON (padrão do SSMS; no sqlcmd, use -I), por causa do .value() do XML.
   Compatível com SQL Server 2014: o HASHBYTES aceita no máximo 8000 bytes, então o texto é resumido em partes de 3500
   caracteres, encadeadas (hash = SHA-256(hash anterior + parte)).
   --------------------------------------------------------------------- */
DECLARE @Canonico TABLE (Secao tinyint NOT NULL, Chave1 uniqueidentifier NOT NULL, Chave2 uniqueidentifier NULL, Linha nvarchar(max) NOT NULL);

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 1, g.Id, NULL, CONCAT(N'Grupo|', g.Id, N'|', g.Chave, N'|', g.Nome, N'|', COALESCE(N'"' + g.Descricao + N'"', N'~'),
                             N'|Ativo=', CAST(g.Ativo AS int))
FROM #AreaGrupo g;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 2, e.Id, NULL, CONCAT(N'Entidade|', e.Id, N'|', e.Chave, N'|', e.Nome, N'|', COALESCE(N'"' + e.NomePlural + N'"', N'~'),
                             N'|', COALESCE(N'"' + e.Descricao + N'"', N'~'), N'|', COALESCE(e.Icone, N'~'),
                             N'|EscopoEquipe=', CAST(e.EscopoEquipe AS int), N'|Ativo=', CAST(e.Ativo AS int), N'|Ordem=', e.Ordem,
                             N'|CampoExibicao=', COALESCE(CAST(e.CampoExibicaoId AS nvarchar(36)), N'~'))
FROM #AreaEntidade e;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 3, c.Id, NULL, CONCAT(N'Campo|', c.Id, N'|', c.EntidadeId, N'|', c.Chave, N'|', c.Nome, N'|', c.TipoDado,
                             N'|Obrigatorio=', CAST(c.Obrigatorio AS int), N'|TamanhoMaximo=', COALESCE(CAST(c.TamanhoMaximo AS nvarchar(11)), N'~'),
                             N'|Precisao=', COALESCE(CAST(c.Precisao AS nvarchar(3)), N'~'), N'|Escala=', COALESCE(CAST(c.Escala AS nvarchar(3)), N'~'),
                             N'|Opcoes=', COALESCE(c.Opcoes, N'~'),
                             N'|Referencia=', COALESCE(CAST(c.EntidadeReferenciadaId AS nvarchar(36)), N'~'), N'|AcaoExclusao=', COALESCE(c.AcaoExclusao, N'~'),
                             N'|Ordem=', c.Ordem, N'|Ativo=', CAST(c.Ativo AS int))
FROM #AreaCampo c;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 4, r.Id, NULL, CONCAT(N'RestricaoUnica|', r.Id, N'|', r.EntidadeId, N'|', r.Nome)
FROM #AreaRestricao r;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 5, rc.RestricaoUnicaId, rc.CampoId, CONCAT(N'RestricaoUnicaCampo|', rc.RestricaoUnicaId, N'|', rc.EntidadeId, N'|', rc.CampoId)
FROM #AreaRestricaoCampo rc;

INSERT @Canonico (Secao, Chave1, Chave2, Linha)
SELECT 6, d.CampoId, NULL, CONCAT(N'CampoDependencia|', d.CampoId, N'|Via=', COALESCE(CAST(d.CampoViaId AS nvarchar(36)), N'~'),
                                  N'|Origem=', d.CampoOrigemId, N'|Alvo=', d.CampoAlvoId)
FROM #AreaDependencia d;

DECLARE @TextoCanonico nvarchar(max) = COALESCE(
    (SELECT k.Linha + NCHAR(10) FROM @Canonico k ORDER BY k.Secao, k.Chave1, k.Chave2 FOR XML PATH(''), TYPE).value('.', 'nvarchar(max)'), N'');
DECLARE @Impressao varbinary(32) = HASHBYTES('SHA2_256', N''), @Posicao int = 1;
WHILE @Posicao <= DATALENGTH(@TextoCanonico) / 2
BEGIN
    SET @Impressao = HASHBYTES('SHA2_256', @Impressao + CAST(SUBSTRING(@TextoCanonico, @Posicao, 3500) AS varbinary(max)));
    SET @Posicao = @Posicao + 3500;
END;
-- Fim do trecho da impressão digital.
IF (SELECT COUNT(*) FROM @Canonico WHERE Secao = 2) <> 10
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 3) <> 52
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 4) <> 8
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 5) <> 11
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 6) <> 1
   OR @Impressao <> 0x55818132BC259070B41B24F6A76835156AC71928647B347C4CB44A4B60B66249
    THROW 50003, N'reconfigurar_testes_desempenho: a configuração gravada difere da esperada (quantidades ou impressão digital). Nada foi gravado.', 1;

COMMIT TRANSACTION;
PRINT 'reconfigurar_testes_desempenho: área testes_desempenho reconfigurada com sucesso (10 tipos de item, 52 campos, 8 unicidades, 1 dependência(s)).';
GO

SET NOEXEC OFF;
GO
