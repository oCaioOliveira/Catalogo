using Catalogo.Api.Controllers;
using Catalogo.Api.Erros;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Excecoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Autenticacao;

/// <summary>
/// Marca os endpoints que funcionam mesmo com a senha provisória pendente de troca (RN25):
/// a própria troca de senha e o "eu".
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class PermitirSenhaProvisoriaAttribute : Attribute;

/// <summary>
/// Roda depois da autenticação, em toda requisição autenticada, e confere a situação ATUAL do usuário
/// (com cache curto):
///   - usuário inexistente ou desativado → 401 (o token deixa de valer na hora, RN23);
///   - senha provisória pendente → 403 com codigo TROCA_DE_SENHA_OBRIGATORIA, exceto nos endpoints
///     marcados com <see cref="PermitirSenhaProvisoriaAttribute"/> (RN25).
/// </summary>
internal sealed class VerificacaoUsuarioMiddleware(RequestDelegate proximo)
{
    public async Task InvokeAsync(HttpContext http, ISituacaoUsuarios situacao, IProblemDetailsService problemDetails)
    {
        var endpoint = http.GetEndpoint();
        var anonimo = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;

        if (!anonimo && http.User.Identity?.IsAuthenticated == true)
        {
            var id = UsuarioAtualHttp.ObterId(http);
            var usuario = id is Guid usuarioId ? await situacao.ObterAsync(usuarioId, http.RequestAborted) : null;

            if (usuario is null || !usuario.Ativo)
            {
                await EscreverAsync(http, problemDetails, StatusCodes.Status401Unauthorized, "Não autenticado.",
                    "Sessão inválida: usuário inexistente ou desativado.", CodigosErro.NaoAutenticado);
                return;
            }

            if (usuario.DeveTrocarSenha && endpoint?.Metadata.GetMetadata<PermitirSenhaProvisoriaAttribute>() is null)
            {
                await EscreverAsync(http, problemDetails, StatusCodes.Status403Forbidden, "Troca de senha obrigatória.",
                    $"Troque a senha provisória em POST /{RotasApi.V1}/auth/trocar-senha antes de continuar.", CodigosErro.TrocaDeSenhaObrigatoria);
                return;
            }
        }

        await proximo(http);
    }

    private static async Task EscreverAsync(
        HttpContext http, IProblemDetailsService problemDetails, int status, string titulo, string detalhe, string codigo)
    {
        http.Response.StatusCode = status;
        var problema = new ProblemDetails { Status = status, Title = titulo, Detail = detalhe };
        problema.Extensions[ExtensoesProblema.Codigo] = codigo;

        await problemDetails.WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problema });
    }
}
