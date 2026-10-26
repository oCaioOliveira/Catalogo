namespace Catalogo.Domain.Excecoes;

// Exceções de domínio. A Api converte cada uma num status HTTP e num "codigo" estável
// (tabela em src/Catalogo.Api/AGENTS.md e docs/api/codigos-de-erro.md). Não use Exception genérica para regra de negócio.

/// <summary>Base das exceções de domínio. Toda exceção de domínio tem um <see cref="Codigo"/> de <see cref="CodigosErro"/>.</summary>
public abstract class ExcecaoDominio(string mensagem, string codigo, Exception? excecaoOriginal = null)
    : Exception(mensagem, excecaoOriginal)
{
    /// <summary>Código estável para o front (extensão "codigo" do ProblemDetails). Um valor de <see cref="CodigosErro"/>.</summary>
    public string Codigo { get; } = codigo;
}

/// <summary>
/// Os dados enviados violam regras de validação. Carrega TODOS os erros, por campo (RN05).
/// Vira HTTP 400 (ValidationProblemDetails), com <see cref="CodigosErro.Validacao"/> ou um código mais específico
/// (ex.: <see cref="CodigosErro.ExportacaoMuitoGrande"/>).
/// </summary>
public sealed class ValidacaoRegistroException : ExcecaoDominio
{
    public ValidacaoRegistroException(
        IReadOnlyDictionary<string, string[]> erros, string codigo = CodigosErro.Validacao, IReadOnlyList<string>? camposDesatualizados = null)
        : base("Um ou mais campos são inválidos.", codigo)
    {
        Erros = erros;
        CamposDesatualizados = camposDesatualizados ?? [];
    }

    /// <summary>Chave do campo → mensagens de erro desse campo.</summary>
    public IReadOnlyDictionary<string, string[]> Erros { get; }

    /// <summary>
    /// Chaves recusadas por serem desconhecidas ou inativas (RN01, RN02). Saem em detalhes.camposDesatualizados: o front recarrega os
    /// metadados e pede para revisar o formulário, sem depender do texto da mensagem.
    /// </summary>
    public IReadOnlyList<string> CamposDesatualizados { get; }
}

/// <summary>
/// Grupo, entidade, registro, equipe... não existe (ou o usuário não pode vê-lo, RN28). Vira HTTP 404.
/// O código diz O QUE não foi encontrado (ex.: <see cref="CodigosErro.RegistroNaoEncontrado"/>).
/// </summary>
public sealed class RecursoNaoEncontradoException(string mensagem, string codigo) : ExcecaoDominio(mensagem, codigo);

/// <summary>
/// Já existe registro com os mesmos valores numa restrição única (RN09). Vira HTTP 409.
/// </summary>
public sealed class ConflitoUnicidadeException : ExcecaoDominio
{
    public ConflitoUnicidadeException(string nomeRestricao, IReadOnlyList<string> campos, string mensagem)
        : base(mensagem, CodigosErro.ConflitoUnicidade)
    {
        NomeRestricao = nomeRestricao;
        Campos = campos;
    }

    /// <summary>Nome da restrição violada (ex.: "uq_comunidade_nome").</summary>
    public string NomeRestricao { get; }

    /// <summary>Chaves dos campos que formam a restrição.</summary>
    public IReadOnlyList<string> Campos { get; }
}

/// <summary>
/// O registro foi alterado por outra pessoa depois de ser lido (Versao diferente, RN12). Vira HTTP 409.
/// </summary>
public sealed class ConflitoConcorrenciaException(string mensagem, Exception? excecaoOriginal = null)
    : ExcecaoDominio(mensagem, CodigosErro.ConflitoConcorrencia, excecaoOriginal);

/// <summary>
/// Sem identificação válida: credenciais erradas no login (<see cref="CodigosErro.CredenciaisInvalidas"/>), token
/// ausente/inválido, ou usuário desativado depois de logar. Vira HTTP 401. A mensagem do login é sempre genérica (RN23).
/// </summary>
public sealed class NaoAutenticadoException(string mensagem, string codigo = CodigosErro.NaoAutenticado)
    : ExcecaoDominio(mensagem, codigo);

/// <summary>
/// O usuário está identificado mas não pode fazer a operação (RN26). Vira HTTP 403, com
/// <see cref="CodigosErro.SemPermissao"/> ou um código para casos que o front trata de forma especial
/// (ex.: <see cref="CodigosErro.TrocaDeSenhaObrigatoria"/>).
/// </summary>
public sealed class AcessoNegadoException(string mensagem, string codigo = CodigosErro.SemPermissao)
    : ExcecaoDominio(mensagem, codigo);

/// <summary>
/// A operação violaria uma regra de negócio que não é de validação de campo, como desativar o
/// último administrador (RN30) ou deixar usuários sem equipe ativa (RN31). Vira HTTP 409.
/// </summary>
public sealed class RegraDeNegocioException(
    string mensagem, string codigo, IReadOnlyDictionary<string, object?>? dados = null) : ExcecaoDominio(mensagem, codigo)
{
    /// <summary>Dados extras para o front (ex.: { "quantidade": 3 }). Saem na extensão "detalhes" do ProblemDetails.</summary>
    public IReadOnlyDictionary<string, object?> Dados { get; } = dados ?? new Dictionary<string, object?>();
}
