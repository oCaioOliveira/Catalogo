using Catalogo.Api.Configuracao;
using Catalogo.Application.CasosDeUso.Acesso;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalogo.Api.Controllers;

/// <summary>Login pelo Windows e dados do usuário logado (RN23, RN105). Ver docs/seguranca.md e docs/adr/0054.</summary>
[ApiController]
[Route(RotasApi.V1 + "/auth")]
[Produces("application/json")]
public sealed class AuthController(Login login, ObterUsuarioLogado obterUsuarioLogado, ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// Login pela autenticação integrada do Windows (AD, Negotiate): sem corpo, o navegador manda as credenciais do Windows
    /// (chame com credenciais, ex.: withCredentials). Devolve o JWT (Authorization: Bearer ...) usado no resto da API. A matrícula
    /// sai do login do Windows (DOMINIO\c161587 → C161587); matrícula nova vira um usuário EXTERNO sem equipe (RN105).
    /// </summary>
    [HttpPost("login")]
    [Authorize(Policy = ConfiguracaoSeguranca.PoliticaLoginWindows)]
    [ProducesResponseType<LoginDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginDto>> Login(CancellationToken cancellationToken)
    {
        var resultado = await login.ExecutarAsync(User.Identity?.Name, cancellationToken);

        // A matrícula identifica quem entrou.
        logger.LogInformation("Login de {Matricula}", resultado.Usuario.Matricula);
        return Ok(resultado);
    }

    /// <summary>Dados do usuário logado, com as equipes ativas.</summary>
    [HttpGet("eu")]
    [ProducesResponseType<UsuarioLogadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UsuarioLogadoDto>> Eu(CancellationToken cancellationToken)
    {
        return Ok(await obterUsuarioLogado.ExecutarAsync(cancellationToken));
    }
}
