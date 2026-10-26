namespace Catalogo.Domain.Excecoes;

/// <summary>
/// Códigos ESTÁVEIS dos erros da API: vão na extensão "codigo" de todo ProblemDetails, e é por eles que o
/// front decide o que fazer (nunca pelo texto da mensagem). Tabela com status e o que o front faz em
/// docs/api/codigos-de-erro.md.
///
/// Renomear ou remover um código QUEBRA o contrato: só numa v2 (docs/adr/0022). Código novo pode entrar na v1,
/// mas precisa de uma linha na tabela do docs/api/codigos-de-erro.md. Nunca escreva o código como string solta.
/// </summary>
public static class CodigosErro
{
    // ---- 400 ----

    /// <summary>Dados ou parâmetros inválidos. Os erros por campo vêm em "errors".</summary>
    public const string Validacao = "VALIDACAO";

    /// <summary>A exportação passaria do limite de linhas: o usuário precisa filtrar mais.</summary>
    public const string ExportacaoMuitoGrande = "EXPORTACAO_MUITO_GRANDE";

    /// <summary>Requisição malformada que nem chegou à validação (ex.: corpo grande demais).</summary>
    public const string RequisicaoInvalida = "REQUISICAO_INVALIDA";

    // ---- 401 ----

    /// <summary>Sem token, token inválido ou expirado, ou usuário desativado depois do login.</summary>
    public const string NaoAutenticado = "NAO_AUTENTICADO";

    /// <summary>
    /// Login recusado. Vale para matrícula inexistente, senha errada, usuário inativo OU bloqueado: o motivo
    /// nunca é revelado (RN23). Por isso não existe um código de "usuário bloqueado".
    /// </summary>
    public const string CredenciaisInvalidas = "CREDENCIAIS_INVALIDAS";

    // ---- 403 ----

    /// <summary>O perfil não permite a operação (RN26).</summary>
    public const string SemPermissao = "SEM_PERMISSAO";

    /// <summary>Senha provisória: só a troca de senha e o "eu" funcionam (RN25).</summary>
    public const string TrocaDeSenhaObrigatoria = "TROCA_DE_SENHA_OBRIGATORIA";

    // ---- 404 ----

    public const string GrupoNaoEncontrado = "GRUPO_NAO_ENCONTRADO";
    public const string EntidadeNaoEncontrada = "ENTIDADE_NAO_ENCONTRADA";
    public const string CampoNaoEncontrado = "CAMPO_NAO_ENCONTRADO";

    /// <summary>O registro não existe ou o usuário não pode vê-lo (RN28: não se revela que ele existe).</summary>
    public const string RegistroNaoEncontrado = "REGISTRO_NAO_ENCONTRADO";

    /// <summary>A equipe não existe ou é de outro gestor (RN33, RN40).</summary>
    public const string EquipeNaoEncontrada = "EQUIPE_NAO_ENCONTRADA";

    public const string UsuarioNaoEncontrado = "USUARIO_NAO_ENCONTRADO";
    public const string ModeloAvaliacaoNaoEncontrado = "MODELO_AVALIACAO_NAO_ENCONTRADO";
    public const string RestricaoNaoEncontrada = "RESTRICAO_NAO_ENCONTRADA";
    public const string DependenciaNaoEncontrada = "DEPENDENCIA_NAO_ENCONTRADA";

    /// <summary>O usuário ainda não avaliou o item (DELETE .../avaliacoes/minha).</summary>
    public const string AvaliacaoNaoEncontrada = "AVALIACAO_NAO_ENCONTRADA";

    /// <summary>Importação inexistente, de outro tipo de item ou de alguém cujas importações o usuário não vê (RN97).</summary>
    public const string ImportacaoNaoEncontrada = "IMPORTACAO_NAO_ENCONTRADA";

    /// <summary>Nenhuma rota da API corresponde à URL (erro de programação do cliente).</summary>
    public const string RotaNaoEncontrada = "ROTA_NAO_ENCONTRADA";

    // ---- 405, 415 ----

    public const string MetodoNaoPermitido = "METODO_NAO_PERMITIDO";
    public const string TipoDeConteudoNaoSuportado = "TIPO_DE_CONTEUDO_NAO_SUPORTADO";

    // ---- 409 ----

    /// <summary>Valor já usado numa restrição única (RN09). "detalhes": { restricao, campos }.</summary>
    public const string ConflitoUnicidade = "CONFLITO_UNICIDADE";

    /// <summary>Alguém alterou o dado depois que o cliente o leu (RN12): recarregar e tentar de novo.</summary>
    public const string ConflitoConcorrencia = "CONFLITO_CONCORRENCIA";

    /// <summary>Exclusão impedida por RESTRINGIR (RN16). "detalhes": { dependencias }.</summary>
    public const string ExclusaoBloqueada = "EXCLUSAO_BLOQUEADA";

    /// <summary>Desativar ou rebaixar o último administrador ativo (RN30).</summary>
    public const string UltimoAdministrador = "ULTIMO_ADMINISTRADOR";

    /// <summary>O comando criar-admin só funciona sem administrador ativo (RN30).</summary>
    public const string AdministradorJaExiste = "ADMINISTRADOR_JA_EXISTE";

    /// <summary>Desativar a equipe deixaria usuários sem equipe ativa (RN31). "detalhes": { quantidade }.</summary>
    public const string UsuariosSemEquipe = "USUARIOS_SEM_EQUIPE";

    /// <summary>Desativar a aprovação com registros pendentes (RN33). "detalhes": { quantidade }.</summary>
    public const string AprovacoesPendentes = "APROVACOES_PENDENTES";

    /// <summary>Decidir um registro que não está PENDENTE (RN36).</summary>
    public const string RegistroNaoPendente = "REGISTRO_NAO_PENDENTE";

    /// <summary>Já há modelo de avaliação ativo para a entidade na equipe (RN41). "detalhes": { modeloId }.</summary>
    public const string ModeloAtivoExistente = "MODELO_ATIVO_EXISTENTE";

    /// <summary>Avaliar um item que não está APROVADO (RN44).</summary>
    public const string RegistroNaoAprovado = "REGISTRO_NAO_APROVADO";

    /// <summary>
    /// Alteração de metadados que deixaria registros inválidos (RN58). "detalhes": { impactos: [...] }, o mesmo relatório
    /// da simulação (RN56).
    /// </summary>
    public const string AlteracaoIncompativelComDados = "ALTERACAO_INCOMPATIVEL_COM_DADOS";

    /// <summary>O campo ou a entidade está em uso (exibição, restrição, dependência, referência) (RN69, RN70, RN76). "detalhes": { impactos }.</summary>
    public const string MetadadoEmUso = "METADADO_EM_USO";

    /// <summary>
    /// A operação não vale na situação atual da importação (ex.: executar sem prévia, executar duas vezes, desfazer o que não foi
    /// concluído) ou a prévia não tem nada a gravar (RN97, RN101). "detalhes": { situacao }.
    /// </summary>
    public const string ImportacaoSituacaoInvalida = "IMPORTACAO_SITUACAO_INVALIDA";

    /// <summary>
    /// Os dados mudaram entre a prévia e a execução (ex.: um item referenciado foi excluído): nada foi gravado, e a prévia nova já
    /// está salva (RN101). O front recarrega a prévia e pede outra confirmação.
    /// </summary>
    public const string ImportacaoDadosMudaram = "IMPORTACAO_DADOS_MUDARAM";

    /// <summary>
    /// A importação não pode ser desfeita (RN103): itens criados por ela foram alterados ou são referenciados por outros.
    /// "detalhes": { impedimentos: [{ motivo, itens: [{ id, rotulo }] }] }.
    /// </summary>
    public const string ImportacaoNaoPodeSerDesfeita = "IMPORTACAO_NAO_PODE_SER_DESFEITA";

    /// <summary>O arquivo da importação já foi apagado pela limpeza automática (7 dias, RN104): sem prévia nem relatório.</summary>
    public const string ImportacaoArquivoExpirado = "IMPORTACAO_ARQUIVO_EXPIRADO";

    /// <summary>Escrita num registro de entidade (ou grupo) desativada: só leitura, mesmo para o administrador (RN75, RN77).</summary>
    public const string EntidadeInativa = "ENTIDADE_INATIVA";

    // ---- 429, 500 ----

    /// <summary>Limite de tentativas de login por minuto (RN23).</summary>
    public const string MuitasTentativas = "MUITAS_TENTATIVAS";

    /// <summary>Erro inesperado. Fora de Development, a resposta só traz o código e o traceId.</summary>
    public const string ErroInterno = "ERRO_INTERNO";
}
