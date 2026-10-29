using Catalogo.Api.Autenticacao;
using Catalogo.Api.Configuracao;
using Catalogo.Application.CasosDeUso.Acesso;
using Catalogo.Application.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Catalogo.Api.Controllers;

/// <summary>Login, troca de senha e dados do usuário logado (RN23 a RN25). Ver docs/seguranca.md.</summary>
[ApiController]
[Route(RotasApi.V1 + "/auth")]
[Produces("application/json")]
public sealed class AuthController(
    Login login, TrocarSenha trocarSenha, ObterUsuarioLogado obterUsuarioLogado, ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// Login com matrícula e senha. Devolve o JWT (Authorization: Bearer ...). A mensagem de erro é
    /// sempre a mesma, sem dizer se a matrícula existe (RN23).
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(ConfiguracaoSeguranca.PoliticaLogin)]
    [Consumes("application/json")]
    [ProducesResponseType<LoginDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginDto>> Login([FromBody] LoginEntrada entrada, CancellationToken cancellationToken)
    {
        var resultado = await login.ExecutarAsync(entrada, cancellationToken);

        // Nunca registre a senha. A matrícula identifica quem entrou.
        logger.LogInformation("Login de {Matricula}", resultado.Usuario.Matricula);
        return Ok(resultado);
    }

    /// <summary>Troca a própria senha (obrigatório depois de uma senha provisória, RN25).</summary>
    [HttpPost("trocar-senha")]
    [PermitirSenhaProvisoria]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> TrocarSenha([FromBody] TrocaSenhaEntrada entrada, CancellationToken cancellationToken)
    {
        await trocarSenha.ExecutarAsync(entrada, cancellationToken);
        return NoContent();
    }

    /// <summary>Dados do usuário logado, com as equipes ativas. Funciona com a troca de senha pendente.</summary>
    [HttpGet("eu")]
    [PermitirSenhaProvisoria]
    [ProducesResponseType<UsuarioLogadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UsuarioLogadoDto>> Eu(CancellationToken cancellationToken)
    {
        return Ok(await obterUsuarioLogado.ExecutarAsync(cancellationToken));
    }
}
