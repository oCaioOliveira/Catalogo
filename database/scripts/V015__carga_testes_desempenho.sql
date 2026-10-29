/* =====================================================================
   CATÁLOGO — Carga da área de gestão "Testes de Desempenho" (testes_desempenho), padrão CAIXA (schema cat)
   ---------------------------------------------------------------------
   GERADO por database/ferramentas/gerar_carga_area.sh em 2026-10-28 16:36, a partir do banco ListagemGenerica
   (schema de origem: catalogo). Não edite à mão: gere de novo.

   Cria a configuração da área (metadados) no schema cat, com os mesmos identificadores (GUIDs) e as mesmas chaves da origem:
     - 1 área de gestão, 10 tipos de item, 40 campos (tipos, tamanhos, obrigatoriedade, opções, ordem, referências
       e ações de exclusão), o campo de exibição de cada tipo de item, 8 unicidades (10 campos) e 1 dependência(s).
   NÃO cria itens, equipes, usuários, histórico, aprovação nem avaliação. A área nasce SEM equipes habilitadas: só o
   administrador a vê, até habilitar as equipes pela interface (Configurações → Áreas de gestão).

   Pré-requisito: a estrutura do Catálogo no schema cat (cat.CATTB023_VERSAO_SCHEMA na versão 13 ou maior).
   Execução: SSMS (ou sqlcmd), conectado ao banco do Catálogo. Não usa variáveis de SQLCMD. Compatível com SQL Server 2014
   (nível 120). Tempo estimado: menos de 1 segundo.
   Proteção: para, sem alterar nada, se a estrutura não estiver instalada ou se a área já existir. Tudo roda numa
   transação (um identificador repetido também desfaz tudo): antes do COMMIT, o script confere as quantidades e a impressão
   digital da configuração (0xA9134AD6BFC533E0021C39D9721BD8A005FFADD0EF688B1975781C4C3AAB0D89); se algo divergir, desfaz tudo.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT 'carga_testes_desempenho: verificando pré-requisitos...';
IF OBJECT_ID(N'cat.CATTB023_VERSAO_SCHEMA', N'U') IS NULL OR OBJECT_ID(N'cat.CATTB001_GRUPO', N'U') IS NULL
BEGIN
    RAISERROR (N'carga_testes_desempenho: a estrutura do Catálogo não está instalada no schema cat. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF (SELECT MAX(NU_NSU_VERSAO) FROM cat.CATTB023_VERSAO_SCHEMA) < 13
BEGIN
    RAISERROR (N'carga_testes_desempenho: esta carga exige a estrutura na versão 13 ou maior (consulte cat.CATTB023_VERSAO_SCHEMA). Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END
ELSE IF EXISTS (SELECT 1 FROM cat.CATTB001_GRUPO WHERE CO_CHAVE = 'testes_desempenho')
BEGIN
    RAISERROR (N'carga_testes_desempenho: a área de gestão testes_desempenho já existe neste banco. Nada foi alterado.', 16, 1);
    SET NOEXEC ON;
END;
GO

BEGIN TRANSACTION;

PRINT 'carga_testes_desempenho: 1/7 área de gestão';
INSERT cat.CATTB001_GRUPO (NU_NSU_GRUPO, CO_CHAVE, NO_GRUPO, DE_GRUPO, IC_ATIVO_GRU) VALUES ('57A926CD-8E83-4FFF-ADD0-6A9C5411162B', N'testes_desempenho', N'Testes de Desempenho', NULL, 1);

PRINT 'carga_testes_desempenho: 2/7 tipos de item';
INSERT cat.CATTB002_ENTIDADE (NU_NSU_ENTIDADE, NU_NSU_CE_GRUPO_ENT, CO_CHAVE_ENT, NO_ENTIDADE_ENT, NO_PLURAL_ENT, DE_ENTIDADE_ENT, DE_ICONE_ENT, IC_ESCOPO_EQUIPE_ENT, IC_ATIVO_ENT, NU_ORDEM_ENT) VALUES
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

PRINT 'carga_testes_desempenho: 3/7 campos';
INSERT cat.CATTB003_CAMPO (NU_NSU_CAMPO, NU_NSU_CE_ENTIDADE, CO_CHAVE_CAM, NO_CAMPO, NO_TIPO_DADO_CAM, IC_OBRIGATORIO_CAM, NU_TAMANHO_MAXIMO, NU_PRECISAO, NU_ESCALA, DE_OPCAO, NU_ENTIDADE_REFERENCIADA, DE_ACAO_EXCLUSAO, NU_ORDEM_CAM, IC_ATIVO_CAM) VALUES
    ('F0EA423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'projeto', N'Projeto', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', N'RESTRINGIR', 1, 1),  -- relatorio_teste.projeto
    ('F7EA423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'rtc', N'RTC', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, '8189F1DA-3F69-4A2D-B081-C857571E725E', N'RESTRINGIR', 2, 1),  -- relatorio_teste.rtc
    ('FEEA423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'ambiente', N'Ambiente', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, '209E0E8B-511E-4C4F-951E-B78CBD87DE52', N'RESTRINGIR', 3, 1),  -- relatorio_teste.ambiente
    ('05EB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'status_resultado', N'Status', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, 'FE97F789-793F-497C-BEEF-65E9B04D4B63', N'RESTRINGIR', 4, 1),  -- relatorio_teste.status_resultado
    ('0CEB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'data_execucao', N'Data de execução', N'DATA_HORA', 1, NULL, NULL, NULL, NULL, NULL, NULL, 5, 1),  -- relatorio_teste.data_execucao
    ('13EB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'bateria_testes', N'Bateria de testes', N'TEXTO', 1, 50, NULL, NULL, NULL, NULL, NULL, 6, 1),  -- relatorio_teste.bateria_testes
    ('1AEB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'tipo_teste', N'Tipo de teste', N'TEXTO', 1, 50, NULL, NULL, NULL, NULL, NULL, 7, 1),  -- relatorio_teste.tipo_teste
    ('21EB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'executado_por', N'Executado por', N'TEXTO', 1, 150, NULL, NULL, NULL, NULL, NULL, 8, 1),  -- relatorio_teste.executado_por
    ('28EB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'contexto', N'Contexto', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 9, 1),  -- relatorio_teste.contexto
    ('2FEB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'objetivo', N'Objetivo', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 10, 1),  -- relatorio_teste.objetivo
    ('36EB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'apontamentos', N'Apontamentos', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 11, 1),  -- relatorio_teste.apontamentos
    ('3DEB423E-F36B-1410-86BB-000558E25CB6', '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'conclusao', N'Conclusão', N'TEXTO_LONGO', 0, NULL, NULL, NULL, NULL, NULL, NULL, 12, 1),  -- relatorio_teste.conclusao
    ('95EA423E-F36B-1410-86BB-000558E25CB6', 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', N'sigla', N'Sigla', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, '332B6ECB-F049-495F-B85E-D98DA46B94BC', N'RESTRINGIR', 1, 1),  -- projeto.sigla
    ('9CEA423E-F36B-1410-86BB-000558E25CB6', 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', N'nome', N'Nome', N'TEXTO', 1, 150, NULL, NULL, NULL, NULL, NULL, 2, 1),  -- projeto.nome
    ('A3EA423E-F36B-1410-86BB-000558E25CB6', 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', N'url_projeto', N'URL do projeto', N'URL', 0, NULL, NULL, NULL, NULL, NULL, NULL, 3, 1),  -- projeto.url_projeto
    ('AAEA423E-F36B-1410-86BB-000558E25CB6', '895BCF5C-66C7-4F8A-A7BD-78DCC373A351', N'projeto', N'Projeto', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', N'RESTRINGIR', 1, 1),  -- endpoint_servico.projeto
    ('B1EA423E-F36B-1410-86BB-000558E25CB6', '895BCF5C-66C7-4F8A-A7BD-78DCC373A351', N'nome_servico', N'Nome do serviço', N'TEXTO', 1, 150, NULL, NULL, NULL, NULL, NULL, 2, 1),  -- endpoint_servico.nome_servico
    ('B8EA423E-F36B-1410-86BB-000558E25CB6', '895BCF5C-66C7-4F8A-A7BD-78DCC373A351', N'metodo', N'Método', N'TEXTO', 1, 50, NULL, NULL, NULL, NULL, NULL, 3, 1),  -- endpoint_servico.metodo
    ('BFEA423E-F36B-1410-86BB-000558E25CB6', '895BCF5C-66C7-4F8A-A7BD-78DCC373A351', N'endpoint_url_fila', N'Endpoint / URL da fila', N'TEXTO', 1, 255, NULL, NULL, NULL, NULL, NULL, 4, 1),  -- endpoint_servico.endpoint_url_fila
    ('C6EA423E-F36B-1410-86BB-000558E25CB6', '895BCF5C-66C7-4F8A-A7BD-78DCC373A351', N'ativo', N'Ativo', N'BOOLEANO', 1, NULL, NULL, NULL, NULL, NULL, NULL, 5, 1),  -- endpoint_servico.ativo
    ('CDEA423E-F36B-1410-86BB-000558E25CB6', '8189F1DA-3F69-4A2D-B081-C857571E725E', N'rtc_externo_id', N'ID externo do RTC', N'TEXTO', 1, 20, NULL, NULL, NULL, NULL, NULL, 1, 1),  -- rtc.rtc_externo_id
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
    ('59EB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'usuarios_virtuais', N'Usuários virtuais', N'INTEIRO', 1, NULL, NULL, NULL, NULL, NULL, NULL, 4, 1),  -- cenario_teste.usuarios_virtuais
    ('60EB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'tempo_medio_resposta_ms', N'Tempo médio (ms)', N'INTEIRO', 1, NULL, NULL, NULL, NULL, NULL, NULL, 5, 1),  -- cenario_teste.tempo_medio_resposta_ms
    ('67EB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'percentil_95_ms', N'Percentil 95 (ms)', N'INTEIRO', 1, NULL, NULL, NULL, NULL, NULL, NULL, 6, 1),  -- cenario_teste.percentil_95_ms
    ('6EEB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'vazao_media_tps', N'Vazão média (TPS)', N'DECIMAL', 1, NULL, 18, 2, NULL, NULL, NULL, 7, 1),  -- cenario_teste.vazao_media_tps
    ('75EB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'taxa_erro_percentual', N'Taxa de erro (%)', N'DECIMAL', 1, NULL, 5, 2, NULL, NULL, NULL, 8, 1),  -- cenario_teste.taxa_erro_percentual
    ('7CEB423E-F36B-1410-86BB-000558E25CB6', 'F5A7946B-6614-4B2E-966F-2FDF68E92E24', N'relatorio_teste', N'Relatório', N'REFERENCIA', 1, NULL, NULL, NULL, NULL, '63C4BA1A-27F8-4459-8D6A-C6FB527D9780', N'CASCATA', 1, 1),  -- link.relatorio_teste
    ('83EB423E-F36B-1410-86BB-000558E25CB6', 'F5A7946B-6614-4B2E-966F-2FDF68E92E24', N'rtc', N'RTC', N'REFERENCIA', 0, NULL, NULL, NULL, NULL, '8189F1DA-3F69-4A2D-B081-C857571E725E', N'RESTRINGIR', 2, 1),  -- link.rtc
    ('8AEB423E-F36B-1410-86BB-000558E25CB6', 'F5A7946B-6614-4B2E-966F-2FDF68E92E24', N'texto_exibicao', N'Texto de exibição', N'TEXTO', 1, 100, NULL, NULL, NULL, NULL, NULL, 3, 1),  -- link.texto_exibicao
    ('91EB423E-F36B-1410-86BB-000558E25CB6', 'F5A7946B-6614-4B2E-966F-2FDF68E92E24', N'url', N'URL', N'URL', 1, NULL, NULL, NULL, NULL, NULL, NULL, 4, 1);  -- link.url

PRINT 'carga_testes_desempenho: 4/7 campo de exibição de cada tipo de item';
UPDATE e SET NU_CAMPO_EXIBICAO = v.CampoExibicaoId
FROM cat.CATTB002_ENTIDADE e
JOIN (VALUES
    ('63C4BA1A-27F8-4459-8D6A-C6FB527D9780', '13EB423E-F36B-1410-86BB-000558E25CB6'),  -- relatorio_teste.bateria_testes
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

PRINT 'carga_testes_desempenho: 5/7 unicidades';
INSERT cat.CATTB005_RESTRICAO_UNICA (NU_NSU_RESTRICAO_UNICA, NU_NSU_CE_ENTIDADE_RU, NO_RESTRICAO_UNICA) VALUES
    ('A6EB423E-F36B-1410-86BB-000558E25CB6', 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', N'uq_projeto_sigla_nome'),
    ('ADEB423E-F36B-1410-86BB-000558E25CB6', '8189F1DA-3F69-4A2D-B081-C857571E725E', N'uq_rtc_externo_id'),
    ('98EB423E-F36B-1410-86BB-000558E25CB6', 'D0C964DA-6229-486F-AC19-D6548D548383', N'uq_comunidade_nome'),
    ('9FEB423E-F36B-1410-86BB-000558E25CB6', '332B6ECB-F049-495F-B85E-D98DA46B94BC', N'uq_sigla_codigo'),
    ('B4EB423E-F36B-1410-86BB-000558E25CB6', '209E0E8B-511E-4C4F-951E-B78CBD87DE52', N'uq_ambiente_nome'),
    ('BBEB423E-F36B-1410-86BB-000558E25CB6', 'FE97F789-793F-497C-BEEF-65E9B04D4B63', N'uq_status_descricao'),
    ('C2EB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', N'uq_cenario_relatorio_numero'),
    ('C9EB423E-F36B-1410-86BB-000558E25CB6', 'F5A7946B-6614-4B2E-966F-2FDF68E92E24', N'uq_link_rtc');

PRINT 'carga_testes_desempenho: 6/7 campos das unicidades';
INSERT cat.CATTB006_RESTRICAO_UNICA_CAMPO (NU_NSU_CE_RESTRICAO_UNICA_RUC, NU_NSU_CE_ENTIDADE_RUC, NU_NSU_CE_CAMPO_RUC) VALUES
    ('A6EB423E-F36B-1410-86BB-000558E25CB6', 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', '95EA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_projeto_sigla_nome: sigla
    ('A6EB423E-F36B-1410-86BB-000558E25CB6', 'E7CA0CA6-24C8-47CF-BF57-1ADDE3C481AA', '9CEA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_projeto_sigla_nome: nome
    ('ADEB423E-F36B-1410-86BB-000558E25CB6', '8189F1DA-3F69-4A2D-B081-C857571E725E', 'CDEA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_rtc_externo_id: rtc_externo_id
    ('98EB423E-F36B-1410-86BB-000558E25CB6', 'D0C964DA-6229-486F-AC19-D6548D548383', '80EA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_comunidade_nome: nome
    ('9FEB423E-F36B-1410-86BB-000558E25CB6', '332B6ECB-F049-495F-B85E-D98DA46B94BC', '8EEA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_sigla_codigo: codigo_sigla
    ('B4EB423E-F36B-1410-86BB-000558E25CB6', '209E0E8B-511E-4C4F-951E-B78CBD87DE52', 'D4EA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_ambiente_nome: nome
    ('BBEB423E-F36B-1410-86BB-000558E25CB6', 'FE97F789-793F-497C-BEEF-65E9B04D4B63', 'E9EA423E-F36B-1410-86BB-000558E25CB6'),  -- uq_status_descricao: descricao
    ('C2EB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', '44EB423E-F36B-1410-86BB-000558E25CB6'),  -- uq_cenario_relatorio_numero: relatorio_teste
    ('C2EB423E-F36B-1410-86BB-000558E25CB6', '4BB8007F-A74C-4F02-907A-83B1136E711C', '52EB423E-F36B-1410-86BB-000558E25CB6'),  -- uq_cenario_relatorio_numero: numero_cenario
    ('C9EB423E-F36B-1410-86BB-000558E25CB6', 'F5A7946B-6614-4B2E-966F-2FDF68E92E24', '83EB423E-F36B-1410-86BB-000558E25CB6');  -- uq_link_rtc: rtc

PRINT 'carga_testes_desempenho: 7/7 dependências entre campos';
INSERT cat.CATTB004_CAMPO_DEPENDENCIA (NU_NSU_CE_CAMPO_CD, NU_CAMPO_VIA, NU_CAMPO_ORIGEM, NU_CAMPO_ALVO) VALUES
    ('4BEB423E-F36B-1410-86BB-000558E25CB6', '44EB423E-F36B-1410-86BB-000558E25CB6', 'F0EA423E-F36B-1410-86BB-000558E25CB6', 'AAEA423E-F36B-1410-86BB-000558E25CB6');  -- cenario_teste.endpoint_servico

PRINT 'carga_testes_desempenho: conferindo a carga antes de gravar...';
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
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 3) <> 40
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 4) <> 8
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 5) <> 10
   OR (SELECT COUNT(*) FROM @Canonico WHERE Secao = 6) <> 1
   OR @Impressao <> 0xA9134AD6BFC533E0021C39D9721BD8A005FFADD0EF688B1975781C4C3AAB0D89
    THROW 50003, N'carga_testes_desempenho: a configuração gravada difere da esperada (quantidades ou impressão digital). Nada foi gravado.', 1;

COMMIT TRANSACTION;
PRINT 'carga_testes_desempenho: área testes_desempenho carregada com sucesso (10 tipos de item, 40 campos, 8 unicidades, 1 dependência(s); sem equipes habilitadas).';
GO

SET NOEXEC OFF;
GO
