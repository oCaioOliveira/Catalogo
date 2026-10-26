using System.Diagnostics;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Api.Erros;

/// <summary>
/// Extensões que TODO ProblemDetails da API tem (docs/adr/0023):
///   - "codigo": código estável de <see cref="CodigosErro"/>, para o front decidir o que fazer;
///   - "traceId": identificador da requisição. O mesmo valor sai no log (TraceId), para o suporte achar o erro;
///   - "detalhes" (só em alguns erros): dados para o front, com formato documentado em docs/api/codigos-de-erro.md.
/// </summary>
public static class ExtensoesProblema
{
    public const string Codigo = "codigo";
    public const string TraceId = "traceId";
    public const string Detalhes = "detalhes";

    /// <summary>
    /// Ligado em AddProblemDetails (Program.cs): roda para TODO ProblemDetails escrito, inclusive os do próprio
    /// ASP.NET (rota inexistente, 405, 415, JSON malformado, 401 sem token). Quem lançou a exceção já definiu o
    /// código; aqui só se completa o que faltar, para que nenhum erro saia sem "codigo" e "traceId".
    /// </summary>
    public static void Completar(ProblemDetailsContext contexto)
    {
        var problema = contexto.ProblemDetails;
        problema.Extensions.TryAdd(TraceId, Activity.Current?.Id ?? contexto.HttpContext.TraceIdentifier);
        problema.Extensions.TryAdd(Codigo, CodigoPadrao(problema.Status ?? contexto.HttpContext.Response.StatusCode));
    }

    /// <summary>Código dos erros gerados pelo framework, sem exceção de domínio por trás.</summary>
    public static string CodigoPadrao(int status) => status switch
    {
        // [ApiController]: corpo que não é JSON válido ou não tem o formato esperado.
        StatusCodes.Status400BadRequest => CodigosErro.Validacao,
        // Sem token, token inválido ou expirado (o JwtBearer responde 401 sem corpo).
        StatusCodes.Status401Unauthorized => CodigosErro.NaoAutenticado,
        StatusCodes.Status403Forbidden => CodigosErro.SemPermissao,
        // Só chega aqui quando nenhuma rota corresponde à URL: os 404 de recurso têm código próprio.
        StatusCodes.Status404NotFound => CodigosErro.RotaNaoEncontrada,
        StatusCodes.Status405MethodNotAllowed => CodigosErro.MetodoNaoPermitido,
        StatusCodes.Status415UnsupportedMediaType => CodigosErro.TipoDeConteudoNaoSuportado,
        StatusCodes.Status429TooManyRequests => CodigosErro.MuitasTentativas,
        >= 500 => CodigosErro.ErroInterno,
        _ => CodigosErro.RequisicaoInvalida,
    };
}
