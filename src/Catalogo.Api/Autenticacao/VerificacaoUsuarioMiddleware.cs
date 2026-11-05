using Catalogo.Api.Configuracao;
using Catalogo.Api.Erros;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Excecoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Autenticacao;

/// <summary>
/// Roda depois da autenticação, em toda requisição autenticada pelo JWT, e confere a situação ATUAL do usuário (com cache curto):
/// usuário inexistente ou desativado → 401 (o token deixa de valer na hora, RN23). O login (usuário do Windows, ainda sem
/// token) não passa por aqui: quem confere o usuário nele é o caso de uso Login.
/// </summary>
internal sealed class VerificacaoUsuarioMiddleware(RequestDelegate proximo)
{
    public async Task InvokeAsync(HttpContext http, ISituacaoUsuarios situacao, IProblemDetailsService problemDetails)
    {
        var endpoint = http.GetEndpoint();
        var anonimo = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;

        if (!anonimo && !EhLoginWindows(endpoint) && http.User.Identity?.IsAuthenticated == true)
        {
            var id = UsuarioAtualHttp.ObterId(http);
            var usuario = id is Guid usuarioId ? await situacao.ObterAsync(usuarioId, http.RequestAborted) : null;

            if (usuario is null || !usuario.Ativo)
            {
                await EscreverAsync(http, problemDetails, StatusCodes.Status401Unauthorized, "Não autenticado.",
                    "Sessão inválida: usuário inexistente ou desativado.", CodigosErro.NaoAutenticado);
                return;
            }
        }

        await proximo(http);
    }

    /// <summary>O endpoint do login, autenticado pelo Windows (política <see cref="ConfiguracaoSeguranca.PoliticaLoginWindows"/>).</summary>
    private static bool EhLoginWindows(Endpoint? endpoint) =>
        endpoint?.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy == ConfiguracaoSeguranca.PoliticaLoginWindows) == true;

    private static async Task EscreverAsync(
        HttpContext http, IProblemDetailsService problemDetails, int status, string titulo, string detalhe, string codigo)
    {
        http.Response.StatusCode = status;
        var problema = new ProblemDetails { Status = status, Title = titulo, Detail = detalhe };
        problema.Extensions[ExtensoesProblema.Codigo] = codigo;

        await problemDetails.WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problema });
    }
}
