using System.Threading.RateLimiting;
using Catalogo.Api.Autenticacao;
using Catalogo.Api.Erros;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Excecoes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Catalogo.Api.Configuracao;

/// <summary>
/// Autenticação JWT, autorização padrão (tudo exige login) e rate limiting do login.
/// Detalhes e configuração por ambiente em docs/seguranca.md.
/// </summary>
public static class ConfiguracaoSeguranca
{
    /// <summary>Nome da política de rate limiting do login.</summary>
    public const string PoliticaLogin = "login";

    public static IServiceCollection AdicionarSeguranca(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUsuarioAtual, UsuarioAtualHttp>();
        services.AddScoped<IGeradorToken, GeradorTokenJwt>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Lido só quando a primeira requisição chega, depois de toda a configuração carregada
        // (inclusive a dos testes de integração).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration>((opcoes, configuracao) =>
            {
                var jwt = OpcoesJwt.Ler(configuracao);
                // Mantém os nomes dos claims como estão no token ("sub", "matricula"...).
                opcoes.MapInboundClaims = false;
                opcoes.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Emissor,
                    ValidAudience = jwt.Audiencia,
                    IssuerSigningKey = jwt.Chave,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = GeradorTokenJwt.ClaimMatricula,
                    RoleClaimType = GeradorTokenJwt.ClaimPerfil,
                };
                opcoes.TokenValidationParameters.ValidTypes = [JwtConstants.HeaderType];
            });

        // Todo endpoint exige um usuário autenticado, a menos que tenha [AllowAnonymous].
        // O que cada perfil pode fazer NÃO fica aqui: está na PoliticaPermissao (Domain).
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        // RN23: no máximo N tentativas de login por minuto por endereço IP ("Seguranca:LoginLimitePorMinuto", padrão 10).
        services.AddRateLimiter(opcoes =>
        {
            opcoes.AddPolicy(PoliticaLogin, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LimiteLoginPorMinuto(http.RequestServices.GetRequiredService<IConfiguration>()),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            opcoes.OnRejected = async (contexto, cancellationToken) =>
            {
                contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await contexto.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = contexto.HttpContext,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Muitas tentativas.",
                        Detail = "Aguarde um minuto e tente de novo.",
                        Extensions = { [ExtensoesProblema.Codigo] = CodigosErro.MuitasTentativas },
                    },
                });
            };
        });

        return services;
    }

    private static int LimiteLoginPorMinuto(IConfiguration configuracao) =>
        int.TryParse(configuracao["Seguranca:LoginLimitePorMinuto"], out var limite) && limite > 0 ? limite : 10;
}
