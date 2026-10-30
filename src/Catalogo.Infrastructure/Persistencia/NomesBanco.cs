namespace Catalogo.Infrastructure.Persistencia;

/// <summary>
/// Nomes FÍSICOS do banco no padrão da CAIXA (V013, docs/adr/0052): tabelas CATTB0xx_... e colunas com prefixo de tipo
/// (NU_ número/identificador, CO_ código, NO_ nome, DE_ descrição, DH_/DT_ data, IC_ indicador, VR_ valor).
/// As configurações do EF (Persistencia/Configuracoes) continuam com os nomes LÓGICOS (Grupo, Registro.Dados...); o
/// <see cref="CatalogoDbContext"/> troca pelos físicos daqui depois de aplicá-las. Uma tabela ou coluna sem nome físico aqui
/// faz o modelo falhar ao ser montado (melhor do que um erro de "coluna inválida" no meio de uma requisição).
/// O de-para é o mesmo de database/scripts/V013__padrao_caixa.sql (o teste NomesBancoTests confere os dois).
/// </summary>
public static class NomesBanco
{
    private static readonly Dictionary<string, (string Tabela, Dictionary<string, string> Colunas)> Mapa = new(StringComparer.Ordinal)
    {
        ["Grupo"] = ("CATTB001_GRUPO", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_GRUPO", ["Chave"] = "CO_CHAVE", ["Nome"] = "NO_GRUPO", ["Descricao"] = "DE_GRUPO",
            ["CriadoEm"] = "DH_CRIADO_EM", ["Ativo"] = "IC_ATIVO_GRU", ["AtualizadoEm"] = "DH_ATUALIZADO_EM", ["Versao"] = "NU_VERSAO",
        }),
        ["Entidade"] = ("CATTB002_ENTIDADE", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_ENTIDADE", ["GrupoId"] = "NU_NSU_CE_GRUPO_ENT", ["Chave"] = "CO_CHAVE_ENT", ["Nome"] = "NO_ENTIDADE_ENT",
            ["Descricao"] = "DE_ENTIDADE_ENT", ["CampoExibicaoId"] = "NU_CAMPO_EXIBICAO", ["CriadoEm"] = "DH_CRIADO_ENT",
            ["EscopoEquipe"] = "IC_ESCOPO_EQUIPE_ENT", ["Ativo"] = "IC_ATIVO_ENT", ["Ordem"] = "NU_ORDEM_ENT",
            ["AtualizadoEm"] = "DH_ATUALIZADO_ENT", ["Versao"] = "NU_VERSAO_ENT", ["NomePlural"] = "NO_PLURAL_ENT", ["Icone"] = "DE_ICONE_ENT",
        }),
        ["Campo"] = ("CATTB003_CAMPO", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_CAMPO", ["EntidadeId"] = "NU_NSU_CE_ENTIDADE", ["Chave"] = "CO_CHAVE_CAM", ["Nome"] = "NO_CAMPO",
            ["TipoDado"] = "NO_TIPO_DADO_CAM", ["Obrigatorio"] = "IC_OBRIGATORIO_CAM", ["TamanhoMaximo"] = "NU_TAMANHO_MAXIMO",
            ["Precisao"] = "NU_PRECISAO", ["Escala"] = "NU_ESCALA", ["Opcoes"] = "DE_OPCAO", ["EntidadeReferenciadaId"] = "NU_ENTIDADE_REFERENCIADA",
            ["AcaoExclusao"] = "DE_ACAO_EXCLUSAO", ["Ordem"] = "NU_ORDEM_CAM", ["Ativo"] = "IC_ATIVO_CAM", ["CriadoEm"] = "DH_CRIADO_CAM",
        }),
        ["CampoDependencia"] = ("CATTB004_CAMPO_DEPENDENCIA", new(StringComparer.Ordinal)
        {
            ["CampoId"] = "NU_NSU_CE_CAMPO_CD", ["CampoViaId"] = "NU_CAMPO_VIA", ["CampoOrigemId"] = "NU_CAMPO_ORIGEM", ["CampoAlvoId"] = "NU_CAMPO_ALVO",
        }),
        ["RestricaoUnica"] = ("CATTB005_RESTRICAO_UNICA", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_RESTRICAO_UNICA", ["EntidadeId"] = "NU_NSU_CE_ENTIDADE_RU", ["Nome"] = "NO_RESTRICAO_UNICA",
        }),
        ["RestricaoUnicaCampo"] = ("CATTB006_RESTRICAO_UNICA_CAMPO", new(StringComparer.Ordinal)
        {
            ["RestricaoUnicaId"] = "NU_NSU_CE_RESTRICAO_UNICA_RUC", ["EntidadeId"] = "NU_NSU_CE_ENTIDADE_RUC", ["CampoId"] = "NU_NSU_CE_CAMPO_RUC",
        }),
        ["Equipe"] = ("CATTB007_EQUIPE", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_EQUIPE", ["Nome"] = "NO_EQUIPE", ["Ativa"] = "IC_ATIVA_EQP", ["CriadoEm"] = "DH_CRIADO_EM",
        }),
        ["Usuario"] = ("CATTB008_USUARIO", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_USUARIO", ["Matricula"] = "CO_MATRICULA", ["Nome"] = "NO_USUARIO", ["SenhaHash"] = "CO_SENHA_HASH",
            ["Perfil"] = "NO_PERFIL", ["Ativo"] = "IC_ATIVO_USR", ["DeveTrocarSenha"] = "IC_TROCAR_SENHA",
            ["TentativasFalhas"] = "NU_TENTATIVA_FALHA", ["BloqueadoAte"] = "DH_BLOQUEADO", ["UltimoAcessoEm"] = "DH_ULTIMO_ACESSO",
            ["CriadoEm"] = "DH_CRIADO",
        }),
        ["UsuarioEquipe"] = ("CATTB009_USUARIO_EQUIPE", new(StringComparer.Ordinal)
        {
            ["UsuarioId"] = "NU_NSU_CE_USUARIO_UE", ["EquipeId"] = "NU_NSU_CE_EQUIPE_UE", ["CriadoEm"] = "DH_CRIADO_UE",
        }),
        ["GrupoEquipe"] = ("CATTB010_GRUPO_EQUIPE", new(StringComparer.Ordinal)
        {
            ["GrupoId"] = "NU_NSU_CE_GRUPO_GE", ["EquipeId"] = "NU_NSU_CE_EQUIPE_GE", ["CriadoPorId"] = "NU_NSU_CE_USUARIO_GE", ["CriadoEm"] = "DH_CRIADO",
        }),
        ["Registro"] = ("CATTB011_REGISTRO", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_REGISTRO", ["EntidadeId"] = "NU_NSU_CE_ENTIDADE_REG", ["Dados"] = "DE_REGISTRO_DADO", ["CriadoEm"] = "DH_CRIADO",
            ["AtualizadoEm"] = "DH_ATUALIZADO", ["Versao"] = "NU_VERSAO_REG", ["EquipeId"] = "NU_NSU_CE_EQUIPE_REG",
            ["CriadoPorId"] = "NU_USUARIO_CRIADO", ["AtualizadoPorId"] = "NU_USUARIO_ATUALIZADO", ["SituacaoAprovacao"] = "DE_SITUACAO_APROVACAO",
            ["DecididoPorId"] = "NU_USUARIO_DECISAO", ["DecididoEm"] = "DH_DECIDIDO", ["JustificativaDecisao"] = "DE_JUSTIFICATIVA_DECISAO",
        }),
        ["RegistroReferencia"] = ("CATTB012_REGISTRO_REFERENCIA", new(StringComparer.Ordinal)
        {
            ["RegistroOrigemId"] = "NU_REGISTRO_ORIGEM", ["EntidadeOrigemId"] = "NU_ENTIDADE_ORIGEM", ["CampoId"] = "NU_NSU_CE_CAMPO_RR",
            ["RegistroDestinoId"] = "NU_REGISTRO_DESTINO", ["EntidadeDestinoId"] = "NU_ENTIDADE_DESTINO",
        }),
        ["RegistroChaveUnica"] = ("CATTB013_REGISTRO_CHAVE_UNICA", new(StringComparer.Ordinal)
        {
            ["RestricaoUnicaId"] = "NU_NSU_CE_RESTRICAO_UNICA", ["EntidadeId"] = "NU_NSU_CE_ENTIDADE_RCU", ["RegistroId"] = "NU_NSU_CE_REGISTRO_RCU",
            ["ValorHash"] = "VR_VALOR_HASH",
        }),
        ["RegistroValor"] = ("CATTB014_REGISTRO_VALOR", new(StringComparer.Ordinal)
        {
            ["RegistroId"] = "NU_NSU_CE_REGISTRO_RV", ["EntidadeId"] = "NU_NSU_CE_ENTIDADE_RV", ["CampoId"] = "NU_NSU_CE_CAMPO_RV",
            ["ValorTexto"] = "DE_VALOR", ["ValorNumero"] = "NU_VALOR", ["ValorData"] = "DT_VALOR", ["ValorBooleano"] = "IC_VALOR",
        }),
        ["HistoricoAlteracao"] = ("CATTB015_HISTORICO_ALTERACAO", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_HISTORICO_ALTERACAO", ["AlvoTipo"] = "NO_ALVO_TIPO", ["AlvoId"] = "NU_ALVO", ["EntidadeId"] = "NU_NSU_CE_ENTIDADE_HA",
            ["EquipeId"] = "NU_NSU_CE_EQUIPE_HA", ["Operacao"] = "NO_OPERACAO", ["UsuarioId"] = "NU_NSU_CE_USUARIO_HA",
            ["UsuarioMatricula"] = "CO_MATRI_USUARIO", ["OcorridoEm"] = "DT_OCORRIDO", ["CorrelacaoId"] = "NU_CORRELACAO",
            ["DadosAntes"] = "DE_DADO_ANTES", ["DadosDepois"] = "DE_DADO_DEPOIS", ["Justificativa"] = "DE_JUSTIFICATIVA_HA", ["Origem"] = "DE_ORIGEM_HA",
        }),
        ["ConfiguracaoEntidadeEquipe"] = ("CATTB016_CONFIG_ENTIDADE_EQUIPE", new(StringComparer.Ordinal)
        {
            ["EntidadeId"] = "NU_NSU_CE_ENTIDADE_CEE", ["EquipeId"] = "NU_NSU_EQUIPE_CEE", ["RequerAprovacao"] = "IC_APROVACAO",
            ["AtualizadoPorId"] = "NU_USR_ATUALIZADO", ["AtualizadoEm"] = "DT_ATUALIZADO",
        }),
        ["ModeloAvaliacao"] = ("CATTB017_MODELO_AVALIACAO", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_MODELO_AVALIACAO", ["EntidadeId"] = "NU_NSU_CE_ENTIDADE_MA", ["EquipeId"] = "NU_NSU_CE_EQUIPE_MA",
            ["Nome"] = "NO_MODELO_AVALIACAO", ["Descricao"] = "DE_MODELO_AVALIACAO", ["Ativo"] = "IC_ATIVO_MA", ["CriadoPorId"] = "NU_USR_CRIADO_MA",
            ["CriadoEm"] = "DH_CRIADO_MA", ["AtualizadoPorId"] = "NU_USR_ATUALIZADO_MA", ["AtualizadoEm"] = "DH_ATUALIZADO_MA", ["Versao"] = "NU_VERSAO_MA",
        }),
        ["CriterioAvaliacao"] = ("CATTB018_CRITERIO_AVALIACAO", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_CRITERIO_AVALIACAO", ["ModeloAvaliacaoId"] = "NU_NSU_CE_MODELO_AVALIACAO_CA", ["Chave"] = "CO_CHAVE_CA",
            ["Nome"] = "NO_CRITERIO_AVALIACAO", ["Descricao"] = "DE_CRITERIO_AVALIACAO", ["Tipo"] = "NO_TIPO_CA", ["Obrigatorio"] = "IC_OBRIGATORIO_CA",
            ["Comentario"] = "DE_COMENTARIO_CA", ["TamanhoMaximoTexto"] = "NU_TAMANHO_MAX_CA", ["Ordem"] = "NU_ORDEM_CA", ["Ativo"] = "IC_ATIVO_CA",
            ["CriadoEm"] = "DH_CRIADO_CA",
        }),
        ["Avaliacao"] = ("CATTB019_AVALIACAO", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_AVALIACAO", ["RegistroId"] = "NU_NSU_CE_REGISTRO_AV", ["ModeloAvaliacaoId"] = "NU_NSU_CE_MODELO_AVALIACAO_AV",
            ["UsuarioId"] = "NU_NSU_CE_USUARIO_AV", ["CriadoEm"] = "DH_CRIADO_AV", ["AtualizadoEm"] = "DH_ATUALIZADO_AV", ["Versao"] = "NU_VERSAO_AV",
        }),
        ["AvaliacaoResposta"] = ("CATTB020_AVALIACAO_RESPOSTA", new(StringComparer.Ordinal)
        {
            ["AvaliacaoId"] = "NU_NSU_AVALIACAO", ["CriterioAvaliacaoId"] = "NU_NSU_CE_CRITERIO_AVALIACAO_AR", ["Estrelas"] = "NU_ESTRELA",
            ["Texto"] = "DE_AVALIACAO_RESPOSTA",
        }),
        ["Importacao"] = ("CATTB021_IMPORTACAO", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_IMPORTACAO", ["GrupoId"] = "NU_NSU_CE_GRUPO_IMP", ["EntidadeId"] = "NU_NSU_CE_ENTIDADE_IMP",
            ["UsuarioId"] = "NU_NSU_CE_USUARIO_IMP", ["EquipePadraoId"] = "NU_NSU_CE_EQUIPE_IMP", ["NomeArquivo"] = "NO_ARQUIVO_IMPORTACAO",
            ["Formato"] = "NO_FORMATO_IMP", ["Situacao"] = "DE_SITUACAO_IMP", ["Opcoes"] = "DE_OPCAO_IMP", ["Mapeamento"] = "DE_MAPEAMENTO_IMP",
            ["Resumo"] = "DE_RESUMO_IMP", ["CorrelacaoId"] = "NU_CORRELACAO_IMP", ["CriadoEm"] = "DH_CRIADO_IMP",
            ["ConcluidaEm"] = "DH_CONCLUIDA_IMP", ["DesfeitaEm"] = "DH_DESFEITA_IMP", ["Versao"] = "NU_VERSAO_IMP",
        }),
        ["ImportacaoArquivo"] = ("CATTB022_IMPORTACAO_ARQUIVO", new(StringComparer.Ordinal)
        {
            ["ImportacaoId"] = "NU_NSU_CE_IMPORTACAO_IA", ["Conteudo"] = "DE_CONTEUDO_IA",
        }),
        ["__VersaoSchema"] = ("CATTB023_VERSAO_SCHEMA", new(StringComparer.Ordinal)
        {
            ["Versao"] = "NU_NSU_VERSAO", ["ArquivoNome"] = "DE_ARQUIVO_VS", ["AplicadoEm"] = "DH_APLICADO_VS", ["Observacao"] = "DE_OBSERVACAO_VS",
        }),
        // Tabelas criadas depois do V013 (já nascem com o nome físico; CATTB024 é a de controle da migração, fora da aplicação).
        ["ChaveJwt"] = ("CATTB025_CHAVE_JWT", new(StringComparer.Ordinal)
        {
            ["Id"] = "NU_NSU_CHAVE_JWT", ["Chave"] = "CO_CHAVE_JWT", ["CriadoEm"] = "DH_CRIADO_CJ",
        }),
    };

    /// <summary>As tabelas lógicas (as do modelo de dados, docs/modelo-de-dados.md).</summary>
    public static IEnumerable<string> TabelasLogicas => Mapa.Keys;

    /// <summary>Nome físico da tabela (ex.: "Registro" → "CATTB011_REGISTRO").</summary>
    public static string Tabela(string tabelaLogica) =>
        Mapa.TryGetValue(tabelaLogica, out var tabela)
            ? tabela.Tabela
            : throw new InvalidOperationException($"A tabela lógica '{tabelaLogica}' não tem nome físico em NomesBanco.");

    /// <summary>Nome físico da coluna (ex.: ("Registro", "Dados") → "DE_REGISTRO_DADO").</summary>
    public static string Coluna(string tabelaLogica, string colunaLogica) =>
        Mapa.TryGetValue(tabelaLogica, out var tabela) && tabela.Colunas.TryGetValue(colunaLogica, out var coluna)
            ? coluna
            : throw new InvalidOperationException($"A coluna lógica '{tabelaLogica}.{colunaLogica}' não tem nome físico em NomesBanco.");

    /// <summary>Todas as colunas de uma tabela lógica: (lógica, física).</summary>
    public static IReadOnlyDictionary<string, string> Colunas(string tabelaLogica) =>
        Mapa.TryGetValue(tabelaLogica, out var tabela)
            ? tabela.Colunas
            : throw new InvalidOperationException($"A tabela lógica '{tabelaLogica}' não tem nome físico em NomesBanco.");
}
