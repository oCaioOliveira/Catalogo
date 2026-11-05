using System.Security.Claims;
using System.Text.Encodings.Web;
using Catalogo.Api.Configuracao;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Catalogo.Api.IntegrationTests.Apoio;

/// <summary>
/// Simula o usuário do AD nos testes de integração (não há Windows nem Kerberos no TestServer): o nome que a autenticação integrada
/// entregaria (<c>DOMINIO\c161587</c>) vem no cabeçalho <see cref="Cabecalho"/>. Sem o cabeçalho, responde como o Negotiate a um
/// navegador sem credencial: 401 com <c>WWW-Authenticate: Negotiate</c>.
/// <see cref="Registrar"/> troca, só nos testes, o handler do esquema Negotiate (o da política
/// <see cref="ConfiguracaoSeguranca.PoliticaLoginWindows"/>) por este: o NegotiateHandler de verdade olha TODA requisição e exige um
/// servidor com IConnectionItemsFeature (Kestrel, IIS), que o TestServer não tem.
/// </summary>
public sealed class AutenticacaoWindowsDeTeste(
    IOptionsMonitor<AuthenticationSchemeOptions> opcoes, ILoggerFactory logger, UrlEncoder codificador)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opcoes, logger, codificador)
{
    public const string Cabecalho = "X-Teste-Usuario-Windows";

    public static void Registrar(IServiceCollection services) =>
        services.Configure<AuthenticationOptions>(opcoes =>
            opcoes.SchemeMap[NegotiateDefaults.AuthenticationScheme].HandlerType = typeof(AutenticacaoWindowsDeTeste));

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var nome = Request.Headers[Cabecalho].ToString();
        if (string.IsNullOrEmpty(nome))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identidade = new ClaimsIdentity([new Claim(ClaimTypes.Name, nome)], "Negotiate");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidade), Scheme.Name)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Negotiate";
        return Task.CompletedTask;
    }
}
