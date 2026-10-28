/* =====================================================================
   V013 — Padrão de nomenclatura da CAIXA: schema cat, tabelas CATTB0xx e colunas com prefixo de tipo
   ---------------------------------------------------------------------
   Por quê: o banco da CAIXA (CATDB001) segue o padrão de nomes da casa: schema cat, tabelas CATTB001_GRUPO a
   CATTB023_VERSAO_SCHEMA e colunas com prefixo de tipo (NU_ número/identificador, CO_ código, NO_ nome, DE_ descrição,
   DH_/DT_ data, IC_ indicador, VR_ valor). Todos os ambientes ficam com os mesmos nomes (docs/adr/0052).

   O que faz, numa transação só (XACT_ABORT): ou converte tudo, ou nada.
   - Move as 23 tabelas do schema catalogo para o schema cat (ALTER SCHEMA ... TRANSFER, sem cópia de dados) e renomeia
     tabelas e colunas (sp_rename). Os TIPOS não mudam: identificadores uniqueidentifier, textos nvarchar, versão rowversion.
     Os nomes das constraints e dos índices (PK_Grupo, UQ_Grupo_Chave, FK_..., CK_..., DF_..., IX_...) também não mudam:
     a aplicação traduz os erros do banco por eles.
   - Remove os CHECKs com ISJSON (o CATDB001 é SQL Server 2014, nível 120, que não tem ISJSON). O formato JSON continua
     garantido pela aplicação, que é a única que grava essas colunas. O CK_Campo_Lista é recriado sem o ISJSON.
   - Recria o índice filtrado UX_ModeloAvaliacao_Ativo com os nomes novos.
   - Permissões: o papel catalogo_aplicacao recebe SELECT, INSERT, UPDATE, DELETE e EXECUTE no schema cat (o schema
     catalogo, vazio, é excluído). No Docker local, o listagem_app passa a ter cat como schema padrão.
   - A tabela de controle de versões passa a ser cat.CATTB023_VERSAO_SCHEMA (NU_NSU_VERSAO, DE_ARQUIVO_VS, DH_APLICADO_VS,
     DE_OBSERVACAO_VS); o aplicar_scripts.sh acha as duas.
   Se já tiver sido aplicado (cat.CATTB001_GRUPO existe e catalogo.Grupo não), não faz nada.
   Compatível com SQL Server 2014 (nível 120). Escrito num lote só, com SQL dinâmico: o schema de destino fica em @Destino.

   A partir daqui, todo script novo usa os nomes do padrão: cat.CATTB0xx_... (database/AGENTS.md). O de-para completo está
   em docs/modelo-de-dados.md e, no código, em NomesBanco (Infrastructure).
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Exigidos pelo índice filtrado UX_ModeloAvaliacao_Ativo.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

DECLARE @Origem sysname = N'catalogo';
DECLARE @Destino sysname = N'cat';

DECLARE @Tabelas TABLE (Antiga sysname NOT NULL PRIMARY KEY, Nova sysname NOT NULL UNIQUE);
INSERT @Tabelas (Antiga, Nova) VALUES
    (N'Grupo',                      N'CATTB001_GRUPO'),
    (N'Entidade',                   N'CATTB002_ENTIDADE'),
    (N'Campo',                      N'CATTB003_CAMPO'),
    (N'CampoDependencia',           N'CATTB004_CAMPO_DEPENDENCIA'),
    (N'RestricaoUnica',             N'CATTB005_RESTRICAO_UNICA'),
    (N'RestricaoUnicaCampo',        N'CATTB006_RESTRICAO_UNICA_CAMPO'),
    (N'Equipe',                     N'CATTB007_EQUIPE'),
    (N'Usuario',                    N'CATTB008_USUARIO'),
    (N'UsuarioEquipe',              N'CATTB009_USUARIO_EQUIPE'),
    (N'GrupoEquipe',                N'CATTB010_GRUPO_EQUIPE'),
    (N'Registro',                   N'CATTB011_REGISTRO'),
    (N'RegistroReferencia',         N'CATTB012_REGISTRO_REFERENCIA'),
    (N'RegistroChaveUnica',         N'CATTB013_REGISTRO_CHAVE_UNICA'),
    (N'RegistroValor',              N'CATTB014_REGISTRO_VALOR'),
    (N'HistoricoAlteracao',         N'CATTB015_HISTORICO_ALTERACAO'),
    (N'ConfiguracaoEntidadeEquipe', N'CATTB016_CONFIG_ENTIDADE_EQUIPE'),
    (N'ModeloAvaliacao',            N'CATTB017_MODELO_AVALIACAO'),
    (N'CriterioAvaliacao',          N'CATTB018_CRITERIO_AVALIACAO'),
    (N'Avaliacao',                  N'CATTB019_AVALIACAO'),
    (N'AvaliacaoResposta',          N'CATTB020_AVALIACAO_RESPOSTA'),
    (N'Importacao',                 N'CATTB021_IMPORTACAO'),
    (N'ImportacaoArquivo',          N'CATTB022_IMPORTACAO_ARQUIVO'),
    (N'__VersaoSchema',             N'CATTB023_VERSAO_SCHEMA');

-- De-para das colunas (tabela antiga, coluna antiga, coluna nova). Espelhado em NomesBanco.cs (Infrastructure).
DECLARE @Colunas TABLE (Tabela sysname NOT NULL, Antiga sysname NOT NULL, Nova sysname NOT NULL, PRIMARY KEY (Tabela, Antiga));
INSERT @Colunas (Tabela, Antiga, Nova) VALUES
    -- CATTB001_GRUPO
    (N'Grupo', N'Id', N'NU_NSU_GRUPO'),
    (N'Grupo', N'Chave', N'CO_CHAVE'),
    (N'Grupo', N'Nome', N'NO_GRUPO'),
    (N'Grupo', N'Descricao', N'DE_GRUPO'),
    (N'Grupo', N'CriadoEm', N'DH_CRIADO_EM'),
    (N'Grupo', N'Ativo', N'IC_ATIVO_GRU'),
    (N'Grupo', N'AtualizadoEm', N'DH_ATUALIZADO_EM'),
    (N'Grupo', N'Versao', N'NU_VERSAO'),
    -- CATTB002_ENTIDADE
    (N'Entidade', N'Id', N'NU_NSU_ENTIDADE'),
    (N'Entidade', N'GrupoId', N'NU_NSU_CE_GRUPO_ENT'),
    (N'Entidade', N'Chave', N'CO_CHAVE_ENT'),
    (N'Entidade', N'Nome', N'NO_ENTIDADE_ENT'),
    (N'Entidade', N'Descricao', N'DE_ENTIDADE_ENT'),
    (N'Entidade', N'CampoExibicaoId', N'NU_CAMPO_EXIBICAO'),
    (N'Entidade', N'CriadoEm', N'DH_CRIADO_ENT'),
    (N'Entidade', N'EscopoEquipe', N'IC_ESCOPO_EQUIPE_ENT'),
    (N'Entidade', N'Ativo', N'IC_ATIVO_ENT'),
    (N'Entidade', N'Ordem', N'NU_ORDEM_ENT'),
    (N'Entidade', N'AtualizadoEm', N'DH_ATUALIZADO_ENT'),
    (N'Entidade', N'Versao', N'NU_VERSAO_ENT'),
    (N'Entidade', N'NomePlural', N'NO_PLURAL_ENT'),
    (N'Entidade', N'Icone', N'DE_ICONE_ENT'),
    -- CATTB003_CAMPO
    (N'Campo', N'Id', N'NU_NSU_CAMPO'),
    (N'Campo', N'EntidadeId', N'NU_NSU_CE_ENTIDADE'),
    (N'Campo', N'Chave', N'CO_CHAVE_CAM'),
    (N'Campo', N'Nome', N'NO_CAMPO'),
    (N'Campo', N'TipoDado', N'NO_TIPO_DADO_CAM'),
    (N'Campo', N'Obrigatorio', N'IC_OBRIGATORIO_CAM'),
    (N'Campo', N'TamanhoMaximo', N'NU_TAMANHO_MAXIMO'),
    (N'Campo', N'Precisao', N'NU_PRECISAO'),
    (N'Campo', N'Escala', N'NU_ESCALA'),
    (N'Campo', N'Opcoes', N'DE_OPCAO'),
    (N'Campo', N'EntidadeReferenciadaId', N'NU_ENTIDADE_REFERENCIADA'),
    (N'Campo', N'AcaoExclusao', N'DE_ACAO_EXCLUSAO'),
    (N'Campo', N'Ordem', N'NU_ORDEM_CAM'),
    (N'Campo', N'Ativo', N'IC_ATIVO_CAM'),
    (N'Campo', N'CriadoEm', N'DH_CRIADO_CAM'),
    -- CATTB004_CAMPO_DEPENDENCIA
    (N'CampoDependencia', N'CampoId', N'NU_NSU_CE_CAMPO_CD'),
    (N'CampoDependencia', N'CampoViaId', N'NU_CAMPO_VIA'),
    (N'CampoDependencia', N'CampoOrigemId', N'NU_CAMPO_ORIGEM'),
    (N'CampoDependencia', N'CampoAlvoId', N'NU_CAMPO_ALVO'),
    -- CATTB005_RESTRICAO_UNICA
    (N'RestricaoUnica', N'Id', N'NU_NSU_RESTRICAO_UNICA'),
    (N'RestricaoUnica', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_RU'),
    (N'RestricaoUnica', N'Nome', N'NO_RESTRICAO_UNICA'),
    -- CATTB006_RESTRICAO_UNICA_CAMPO
    (N'RestricaoUnicaCampo', N'RestricaoUnicaId', N'NU_NSU_CE_RESTRICAO_UNICA_RUC'),
    (N'RestricaoUnicaCampo', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_RUC'),
    (N'RestricaoUnicaCampo', N'CampoId', N'NU_NSU_CE_CAMPO_RUC'),
    -- CATTB007_EQUIPE
    (N'Equipe', N'Id', N'NU_NSU_EQUIPE'),
    (N'Equipe', N'Nome', N'NO_EQUIPE'),
    (N'Equipe', N'Ativa', N'IC_ATIVA_EQP'),
    (N'Equipe', N'CriadoEm', N'DH_CRIADO_EM'),
    -- CATTB008_USUARIO
    (N'Usuario', N'Id', N'NU_NSU_USUARIO'),
    (N'Usuario', N'Matricula', N'CO_MATRICULA'),
    (N'Usuario', N'Nome', N'NO_USUARIO'),
    (N'Usuario', N'SenhaHash', N'CO_SENHA_HASH'),
    (N'Usuario', N'Perfil', N'NO_PERFIL'),
    (N'Usuario', N'Ativo', N'IC_ATIVO_USR'),
    (N'Usuario', N'DeveTrocarSenha', N'IC_TROCAR_SENHA'),
    (N'Usuario', N'TentativasFalhas', N'NU_TENTATIVA_FALHA'),
    (N'Usuario', N'BloqueadoAte', N'DH_BLOQUEADO'),
    (N'Usuario', N'UltimoAcessoEm', N'DH_ULTIMO_ACESSO'),
    (N'Usuario', N'CriadoEm', N'DH_CRIADO'),
    -- CATTB009_USUARIO_EQUIPE
    (N'UsuarioEquipe', N'UsuarioId', N'NU_NSU_CE_USUARIO_UE'),
    (N'UsuarioEquipe', N'EquipeId', N'NU_NSU_CE_EQUIPE_UE'),
    (N'UsuarioEquipe', N'CriadoEm', N'DH_CRIADO_UE'),
    -- CATTB010_GRUPO_EQUIPE
    (N'GrupoEquipe', N'GrupoId', N'NU_NSU_CE_GRUPO_GE'),
    (N'GrupoEquipe', N'EquipeId', N'NU_NSU_CE_EQUIPE_GE'),
    (N'GrupoEquipe', N'CriadoPorId', N'NU_NSU_CE_USUARIO_GE'),
    (N'GrupoEquipe', N'CriadoEm', N'DH_CRIADO'),
    -- CATTB011_REGISTRO
    (N'Registro', N'Id', N'NU_NSU_REGISTRO'),
    (N'Registro', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_REG'),
    (N'Registro', N'Dados', N'DE_REGISTRO_DADO'),
    (N'Registro', N'CriadoEm', N'DH_CRIADO'),
    (N'Registro', N'AtualizadoEm', N'DH_ATUALIZADO'),
    (N'Registro', N'Versao', N'NU_VERSAO_REG'),
    (N'Registro', N'EquipeId', N'NU_NSU_CE_EQUIPE_REG'),
    (N'Registro', N'CriadoPorId', N'NU_USUARIO_CRIADO'),
    (N'Registro', N'AtualizadoPorId', N'NU_USUARIO_ATUALIZADO'),
    (N'Registro', N'SituacaoAprovacao', N'DE_SITUACAO_APROVACAO'),
    (N'Registro', N'DecididoPorId', N'NU_USUARIO_DECISAO'),
    (N'Registro', N'DecididoEm', N'DH_DECIDIDO'),
    (N'Registro', N'JustificativaDecisao', N'DE_JUSTIFICATIVA_DECISAO'),
    -- CATTB012_REGISTRO_REFERENCIA
    (N'RegistroReferencia', N'RegistroOrigemId', N'NU_REGISTRO_ORIGEM'),
    (N'RegistroReferencia', N'EntidadeOrigemId', N'NU_ENTIDADE_ORIGEM'),
    (N'RegistroReferencia', N'CampoId', N'NU_NSU_CE_CAMPO_RR'),
    (N'RegistroReferencia', N'RegistroDestinoId', N'NU_REGISTRO_DESTINO'),
    (N'RegistroReferencia', N'EntidadeDestinoId', N'NU_ENTIDADE_DESTINO'),
    -- CATTB013_REGISTRO_CHAVE_UNICA
    (N'RegistroChaveUnica', N'RestricaoUnicaId', N'NU_NSU_CE_RESTRICAO_UNICA'),
    (N'RegistroChaveUnica', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_RCU'),
    (N'RegistroChaveUnica', N'RegistroId', N'NU_NSU_CE_REGISTRO_RCU'),
    (N'RegistroChaveUnica', N'ValorHash', N'VR_VALOR_HASH'),
    -- CATTB014_REGISTRO_VALOR
    (N'RegistroValor', N'RegistroId', N'NU_NSU_CE_REGISTRO_RV'),
    (N'RegistroValor', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_RV'),
    (N'RegistroValor', N'CampoId', N'NU_NSU_CE_CAMPO_RV'),
    (N'RegistroValor', N'ValorTexto', N'DE_VALOR'),
    (N'RegistroValor', N'ValorNumero', N'NU_VALOR'),
    (N'RegistroValor', N'ValorData', N'DT_VALOR'),
    (N'RegistroValor', N'ValorBooleano', N'IC_VALOR'),
    -- CATTB015_HISTORICO_ALTERACAO
    (N'HistoricoAlteracao', N'Id', N'NU_NSU_HISTORICO_ALTERACAO'),
    (N'HistoricoAlteracao', N'AlvoTipo', N'NO_ALVO_TIPO'),
    (N'HistoricoAlteracao', N'AlvoId', N'NU_ALVO'),
    (N'HistoricoAlteracao', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_HA'),
    (N'HistoricoAlteracao', N'EquipeId', N'NU_NSU_CE_EQUIPE_HA'),
    (N'HistoricoAlteracao', N'Operacao', N'NO_OPERACAO'),
    (N'HistoricoAlteracao', N'UsuarioId', N'NU_NSU_CE_USUARIO_HA'),
    (N'HistoricoAlteracao', N'UsuarioMatricula', N'CO_MATRI_USUARIO'),
    (N'HistoricoAlteracao', N'OcorridoEm', N'DT_OCORRIDO'),
    (N'HistoricoAlteracao', N'CorrelacaoId', N'NU_CORRELACAO'),
    (N'HistoricoAlteracao', N'DadosAntes', N'DE_DADO_ANTES'),
    (N'HistoricoAlteracao', N'DadosDepois', N'DE_DADO_DEPOIS'),
    (N'HistoricoAlteracao', N'Justificativa', N'DE_JUSTIFICATIVA_HA'),
    (N'HistoricoAlteracao', N'Origem', N'DE_ORIGEM_HA'),
    -- CATTB016_CONFIG_ENTIDADE_EQUIPE
    (N'ConfiguracaoEntidadeEquipe', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_CEE'),
    (N'ConfiguracaoEntidadeEquipe', N'EquipeId', N'NU_NSU_EQUIPE_CEE'),
    (N'ConfiguracaoEntidadeEquipe', N'RequerAprovacao', N'IC_APROVACAO'),
    (N'ConfiguracaoEntidadeEquipe', N'AtualizadoPorId', N'NU_USR_ATUALIZADO'),
    (N'ConfiguracaoEntidadeEquipe', N'AtualizadoEm', N'DT_ATUALIZADO'),
    -- CATTB017_MODELO_AVALIACAO
    (N'ModeloAvaliacao', N'Id', N'NU_NSU_MODELO_AVALIACAO'),
    (N'ModeloAvaliacao', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_MA'),
    (N'ModeloAvaliacao', N'EquipeId', N'NU_NSU_CE_EQUIPE_MA'),
    (N'ModeloAvaliacao', N'Nome', N'NO_MODELO_AVALIACAO'),
    (N'ModeloAvaliacao', N'Descricao', N'DE_MODELO_AVALIACAO'),
    (N'ModeloAvaliacao', N'Ativo', N'IC_ATIVO_MA'),
    (N'ModeloAvaliacao', N'CriadoPorId', N'NU_USR_CRIADO_MA'),
    (N'ModeloAvaliacao', N'CriadoEm', N'DH_CRIADO_MA'),
    (N'ModeloAvaliacao', N'AtualizadoPorId', N'NU_USR_ATUALIZADO_MA'),
    (N'ModeloAvaliacao', N'AtualizadoEm', N'DH_ATUALIZADO_MA'),
    (N'ModeloAvaliacao', N'Versao', N'NU_VERSAO_MA'),
    -- CATTB018_CRITERIO_AVALIACAO
    (N'CriterioAvaliacao', N'Id', N'NU_NSU_CRITERIO_AVALIACAO'),
    (N'CriterioAvaliacao', N'ModeloAvaliacaoId', N'NU_NSU_CE_MODELO_AVALIACAO_CA'),
    (N'CriterioAvaliacao', N'Chave', N'CO_CHAVE_CA'),
    (N'CriterioAvaliacao', N'Nome', N'NO_CRITERIO_AVALIACAO'),
    (N'CriterioAvaliacao', N'Descricao', N'DE_CRITERIO_AVALIACAO'),
    (N'CriterioAvaliacao', N'Tipo', N'NO_TIPO_CA'),
    (N'CriterioAvaliacao', N'Obrigatorio', N'IC_OBRIGATORIO_CA'),
    (N'CriterioAvaliacao', N'Comentario', N'DE_COMENTARIO_CA'),
    (N'CriterioAvaliacao', N'TamanhoMaximoTexto', N'NU_TAMANHO_MAX_CA'),
    (N'CriterioAvaliacao', N'Ordem', N'NU_ORDEM_CA'),
    (N'CriterioAvaliacao', N'Ativo', N'IC_ATIVO_CA'),
    (N'CriterioAvaliacao', N'CriadoEm', N'DH_CRIADO_CA'),
    -- CATTB019_AVALIACAO
    (N'Avaliacao', N'Id', N'NU_NSU_AVALIACAO'),
    (N'Avaliacao', N'RegistroId', N'NU_NSU_CE_REGISTRO_AV'),
    (N'Avaliacao', N'ModeloAvaliacaoId', N'NU_NSU_CE_MODELO_AVALIACAO_AV'),
    (N'Avaliacao', N'UsuarioId', N'NU_NSU_CE_USUARIO_AV'),
    (N'Avaliacao', N'CriadoEm', N'DH_CRIADO_AV'),
    (N'Avaliacao', N'AtualizadoEm', N'DH_ATUALIZADO_AV'),
    (N'Avaliacao', N'Versao', N'NU_VERSAO_AV'),
    -- CATTB020_AVALIACAO_RESPOSTA
    (N'AvaliacaoResposta', N'AvaliacaoId', N'NU_NSU_AVALIACAO'),
    (N'AvaliacaoResposta', N'CriterioAvaliacaoId', N'NU_NSU_CE_CRITERIO_AVALIACAO_AR'),
    (N'AvaliacaoResposta', N'Estrelas', N'NU_ESTRELA'),
    (N'AvaliacaoResposta', N'Texto', N'DE_AVALIACAO_RESPOSTA'),
    -- CATTB021_IMPORTACAO
    (N'Importacao', N'Id', N'NU_NSU_IMPORTACAO'),
    (N'Importacao', N'GrupoId', N'NU_NSU_CE_GRUPO_IMP'),
    (N'Importacao', N'EntidadeId', N'NU_NSU_CE_ENTIDADE_IMP'),
    (N'Importacao', N'UsuarioId', N'NU_NSU_CE_USUARIO_IMP'),
    (N'Importacao', N'EquipePadraoId', N'NU_NSU_CE_EQUIPE_IMP'),
    (N'Importacao', N'NomeArquivo', N'NO_ARQUIVO_IMPORTACAO'),
    (N'Importacao', N'Formato', N'NO_FORMATO_IMP'),
    (N'Importacao', N'Situacao', N'DE_SITUACAO_IMP'),
    (N'Importacao', N'Opcoes', N'DE_OPCAO_IMP'),
    (N'Importacao', N'Mapeamento', N'DE_MAPEAMENTO_IMP'),
    (N'Importacao', N'Resumo', N'DE_RESUMO_IMP'),
    (N'Importacao', N'CorrelacaoId', N'NU_CORRELACAO_IMP'),
    (N'Importacao', N'CriadoEm', N'DH_CRIADO_IMP'),
    (N'Importacao', N'ConcluidaEm', N'DH_CONCLUIDA_IMP'),
    (N'Importacao', N'DesfeitaEm', N'DH_DESFEITA_IMP'),
    (N'Importacao', N'Versao', N'NU_VERSAO_IMP'),
    -- CATTB022_IMPORTACAO_ARQUIVO
    (N'ImportacaoArquivo', N'ImportacaoId', N'NU_NSU_CE_IMPORTACAO_IA'),
    (N'ImportacaoArquivo', N'Conteudo', N'DE_CONTEUDO_IA'),
    -- CATTB023_VERSAO_SCHEMA
    (N'__VersaoSchema', N'Versao', N'NU_NSU_VERSAO'),
    (N'__VersaoSchema', N'ArquivoNome', N'DE_ARQUIVO_VS'),
    (N'__VersaoSchema', N'AplicadoEm', N'DH_APLICADO_VS'),
    (N'__VersaoSchema', N'Observacao', N'DE_OBSERVACAO_VS');

DECLARE @Sql nvarchar(max), @Mensagem nvarchar(2048), @Antiga sysname, @Nova sysname, @Tabela sysname, @Quantidade int;

-- Já aplicado?
IF OBJECT_ID(QUOTENAME(@Destino) + N'.CATTB001_GRUPO', N'U') IS NOT NULL AND OBJECT_ID(QUOTENAME(@Origem) + N'.Grupo', N'U') IS NULL
BEGIN
    PRINT CONCAT('V013: as tabelas já estão no padrão (', @Destino, '.CATTB001_GRUPO). Nada a fazer.');
    RETURN;
END;

-- Pré-requisitos: as 23 tabelas no schema de origem e nenhuma tabela do padrão no destino.
SELECT @Quantidade = COUNT(*) FROM @Tabelas WHERE OBJECT_ID(QUOTENAME(@Origem) + N'.' + QUOTENAME(Antiga), N'U') IS NULL;
IF @Quantidade > 0
BEGIN
    SET @Mensagem = CONCAT(N'V013: faltam ', @Quantidade, N' tabela(s) do Catálogo no schema ', @Origem, N'. Nada foi alterado.');
    THROW 50013, @Mensagem, 1;
END;
IF SCHEMA_ID(@Destino) IS NOT NULL AND EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID(@Destino))
BEGIN
    SET @Mensagem = CONCAT(N'V013: o schema ', @Destino, N' já tem tabelas. Nada foi alterado.');
    THROW 50013, @Mensagem, 1;
END;
-- Toda coluna existente precisa estar no de-para (uma coluna nova esquecida aqui quebraria a aplicação).
SELECT TOP (1) @Mensagem = CONCAT(N'V013: a coluna ', t.Antiga, N'.', c.name, N' não está no de-para. Nada foi alterado.')
FROM @Tabelas t
JOIN sys.columns c ON c.object_id = OBJECT_ID(QUOTENAME(@Origem) + N'.' + QUOTENAME(t.Antiga), N'U')
WHERE NOT EXISTS (SELECT 1 FROM @Colunas m WHERE m.Tabela = t.Antiga AND m.Antiga = c.name);
IF @@ROWCOUNT > 0
    THROW 50013, @Mensagem, 1;

BEGIN TRANSACTION;

IF SCHEMA_ID(@Destino) IS NULL
BEGIN
    SET @Sql = N'CREATE SCHEMA ' + QUOTENAME(@Destino) + N' AUTHORIZATION dbo;';
    EXEC (@Sql);
END;

-- 1. CHECKs com ISJSON (não existe no SQL Server 2014) e o índice filtrado (recriado com os nomes novos no passo 4).
DECLARE @Remover TABLE (Tabela sysname NOT NULL, Nome sysname NOT NULL, Indice bit NOT NULL);
INSERT @Remover (Tabela, Nome, Indice) VALUES
    (N'Campo', N'CK_Campo_Lista', 0),
    (N'Registro', N'CK_Registro_Dados_Json', 0),
    (N'HistoricoAlteracao', N'CK_HistoricoAlteracao_DadosAntes_Json', 0),
    (N'HistoricoAlteracao', N'CK_HistoricoAlteracao_DadosDepois_Json', 0),
    (N'Importacao', N'CK_Importacao_Opcoes_Json', 0),
    (N'Importacao', N'CK_Importacao_Mapeamento_Json', 0),
    (N'Importacao', N'CK_Importacao_Resumo_Json', 0),
    (N'ModeloAvaliacao', N'UX_ModeloAvaliacao_Ativo', 1);

DECLARE remover CURSOR LOCAL FAST_FORWARD FOR SELECT Tabela, Nome FROM @Remover WHERE Indice = 0;
OPEN remover;
FETCH NEXT FROM remover INTO @Tabela, @Antiga;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(QUOTENAME(@Origem) + N'.' + QUOTENAME(@Antiga), N'C') IS NOT NULL
    BEGIN
        SET @Sql = N'ALTER TABLE ' + QUOTENAME(@Origem) + N'.' + QUOTENAME(@Tabela) + N' DROP CONSTRAINT ' + QUOTENAME(@Antiga) + N';';
        EXEC (@Sql);
    END;
    FETCH NEXT FROM remover INTO @Tabela, @Antiga;
END;
CLOSE remover;
DEALLOCATE remover;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ModeloAvaliacao_Ativo'
                                       AND object_id = OBJECT_ID(QUOTENAME(@Origem) + N'.ModeloAvaliacao'))
BEGIN
    SET @Sql = N'DROP INDEX UX_ModeloAvaliacao_Ativo ON ' + QUOTENAME(@Origem) + N'.ModeloAvaliacao;';
    EXEC (@Sql);
END;

-- O SQL Server não renomeia coluna usada em CHECK: os CHECKs restantes são guardados, removidos e recriados no passo 4,
-- com os nomes novos das colunas (a definição guardada pelo SQL Server traz cada coluna entre colchetes).
DECLARE @Checks TABLE (Tabela sysname NOT NULL, Nome sysname NOT NULL PRIMARY KEY, Definicao nvarchar(max) NOT NULL);
INSERT @Checks (Tabela, Nome, Definicao)
SELECT t.Antiga, cc.name, cc.definition
FROM sys.check_constraints cc
JOIN @Tabelas t ON cc.parent_object_id = OBJECT_ID(QUOTENAME(@Origem) + N'.' + QUOTENAME(t.Antiga), N'U');

DECLARE checks CURSOR LOCAL FAST_FORWARD FOR SELECT Tabela, Nome FROM @Checks;
OPEN checks;
FETCH NEXT FROM checks INTO @Tabela, @Antiga;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'ALTER TABLE ' + QUOTENAME(@Origem) + N'.' + QUOTENAME(@Tabela) + N' DROP CONSTRAINT ' + QUOTENAME(@Antiga) + N';';
    EXEC (@Sql);
    FETCH NEXT FROM checks INTO @Tabela, @Antiga;
END;
CLOSE checks;
DEALLOCATE checks;
PRINT 'V013: 1/5 CHECKs (os com ISJSON, de vez) e índice filtrado removidos.';

-- 2. Tabelas: muda de schema e de nome.
DECLARE tabelas CURSOR LOCAL FAST_FORWARD FOR SELECT Antiga, Nova FROM @Tabelas ORDER BY Nova;
OPEN tabelas;
FETCH NEXT FROM tabelas INTO @Antiga, @Nova;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = N'ALTER SCHEMA ' + QUOTENAME(@Destino) + N' TRANSFER ' + QUOTENAME(@Origem) + N'.' + QUOTENAME(@Antiga) + N';';
    EXEC (@Sql);
    SET @Tabela = QUOTENAME(@Destino) + N'.' + QUOTENAME(@Antiga);
    EXEC sys.sp_rename @objname = @Tabela, @newname = @Nova, @objtype = N'OBJECT';
    FETCH NEXT FROM tabelas INTO @Antiga, @Nova;
END;
CLOSE tabelas;
DEALLOCATE tabelas;
PRINT 'V013: 2/5 23 tabelas movidas para o schema de destino e renomeadas.';

-- 3. Colunas.
DECLARE colunas CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.Nova, c.Antiga, c.Nova FROM @Colunas c JOIN @Tabelas t ON t.Antiga = c.Tabela ORDER BY t.Nova, c.Antiga;
OPEN colunas;
FETCH NEXT FROM colunas INTO @Tabela, @Antiga, @Nova;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @Sql = QUOTENAME(@Destino) + N'.' + QUOTENAME(@Tabela) + N'.' + QUOTENAME(@Antiga);
    EXEC sys.sp_rename @objname = @Sql, @newname = @Nova, @objtype = N'COLUMN';
    FETCH NEXT FROM colunas INTO @Tabela, @Antiga, @Nova;
END;
CLOSE colunas;
DEALLOCATE colunas;
PRINT 'V013: 3/5 173 colunas renomeadas.';

-- 4. O que foi removido no passo 1, com os nomes novos (sem ISJSON).
DECLARE @Definicao nvarchar(max), @Coluna sysname, @ColunaNova sysname;
DECLARE checks CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.Nova, c.Nome, c.Definicao FROM @Checks c JOIN @Tabelas t ON t.Antiga = c.Tabela ORDER BY c.Nome;
OPEN checks;
FETCH NEXT FROM checks INTO @Tabela, @Antiga, @Definicao;
WHILE @@FETCH_STATUS = 0
BEGIN
    DECLARE nomes CURSOR LOCAL FAST_FORWARD FOR
        SELECT m.Antiga, m.Nova FROM @Colunas m JOIN @Tabelas t ON t.Antiga = m.Tabela WHERE t.Nova = @Tabela;
    OPEN nomes;
    FETCH NEXT FROM nomes INTO @Coluna, @ColunaNova;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Definicao = REPLACE(@Definicao, N'[' + @Coluna + N']', N'[' + @ColunaNova + N']');
        FETCH NEXT FROM nomes INTO @Coluna, @ColunaNova;
    END;
    CLOSE nomes;
    DEALLOCATE nomes;

    SET @Sql = N'ALTER TABLE ' + QUOTENAME(@Destino) + N'.' + QUOTENAME(@Tabela) + N' WITH CHECK ADD CONSTRAINT '
             + QUOTENAME(@Antiga) + N' CHECK ' + @Definicao + N';';
    EXEC (@Sql);
    FETCH NEXT FROM checks INTO @Tabela, @Antiga, @Definicao;
END;
CLOSE checks;
DEALLOCATE checks;

SET @Sql = N'ALTER TABLE ' + QUOTENAME(@Destino) + N'.CATTB003_CAMPO ADD CONSTRAINT CK_Campo_Lista CHECK (
       (NO_TIPO_DADO_CAM = ''LISTA'' AND DE_OPCAO IS NOT NULL)
    OR (NO_TIPO_DADO_CAM <> ''LISTA'' AND DE_OPCAO IS NULL));';
EXEC (@Sql);
SET @Sql = N'CREATE UNIQUE INDEX UX_ModeloAvaliacao_Ativo ON ' + QUOTENAME(@Destino)
         + N'.CATTB017_MODELO_AVALIACAO (NU_NSU_CE_ENTIDADE_MA, NU_NSU_CE_EQUIPE_MA) WHERE IC_ATIVO_MA = 1;';
EXEC (@Sql);
PRINT 'V013: 4/5 CHECKs, CK_Campo_Lista (sem ISJSON) e UX_ModeloAvaliacao_Ativo recriados com os nomes novos.';

-- 5. Permissões e o schema de origem, que ficou vazio.
IF DATABASE_PRINCIPAL_ID(N'catalogo_aplicacao') IS NULL
    EXEC (N'CREATE ROLE catalogo_aplicacao AUTHORIZATION dbo;');
SET @Sql = N'GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::' + QUOTENAME(@Destino) + N' TO catalogo_aplicacao;';
EXEC (@Sql);

-- Só no Docker local (em outros ambientes o login da aplicação é associado ao papel pelo DBA).
IF DATABASE_PRINCIPAL_ID(N'listagem_app') IS NOT NULL
BEGIN
    SET @Sql = N'ALTER USER listagem_app WITH DEFAULT_SCHEMA = ' + QUOTENAME(@Destino) + N';';
    EXEC (@Sql);
END;

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE schema_id = SCHEMA_ID(@Origem))
   AND NOT EXISTS (SELECT 1 FROM sys.types WHERE schema_id = SCHEMA_ID(@Origem))
BEGIN
    SET @Sql = N'DROP SCHEMA ' + QUOTENAME(@Origem) + N';';
    EXEC (@Sql);
END;
PRINT 'V013: 5/5 permissões no schema de destino; schema de origem excluído.';

COMMIT TRANSACTION;
PRINT 'V013: concluído.';
GO
