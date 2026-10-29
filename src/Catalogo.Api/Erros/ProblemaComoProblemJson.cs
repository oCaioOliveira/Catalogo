using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Catalogo.Api.Erros;

/// <summary>
/// Todo ProblemDetails devolvido pelo MVC (400 automático do [ApiController] para JSON malformado, 415, 404 de
/// recurso...) sai como application/problem+json. Sem isto, o [Produces("application/json")] dos controllers
/// trocaria o tipo. Roda por último (Order máximo), depois do [Produces]. Os erros do TratadorExcecoesGlobal já
/// saem assim pelo IProblemDetailsService.
/// </summary>
internal sealed class ProblemaComoProblemJson : IAlwaysRunResultFilter, IOrderedFilter
{
    public const string TipoConteudo = "application/problem+json";

    public int Order => int.MaxValue;

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: ProblemDetails } resultado)
        {
            resultado.ContentTypes.Clear();
            resultado.ContentTypes.Add(TipoConteudo);
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
