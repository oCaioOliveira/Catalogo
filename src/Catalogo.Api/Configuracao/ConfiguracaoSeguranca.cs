using Catalogo.Api.Autenticacao;
using Catalogo.Application.Abstracoes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Catalogo.Api.Configuracao;

/// <summary>
/// Autenticação: o login é a autenticação integrada do Windows (Negotiate, só no POST /auth/login), que devolve o JWT; o resto da
/// API usa o JWT. Autorização padrão: tudo exige um usuário autenticado pelo JWT. Detalhes em docs/seguranca.md e docs/adr/0054.
/// </summary>
public static class ConfiguracaoSeguranca
{
    /// <summary>
    /// Política do login: exige o usuário autenticado pelo Windows (esquema Negotiate). No IIS, o Negotiate repassa a autenticação
    /// para o IIS (Autenticação do Windows ligada no site). Os testes de integração trocam esta política por um esquema de teste.
    /// </summary>
    public const string PoliticaLoginWindows = "login-windows";

    public static IServiceCollection AdicionarSeguranca(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUsuarioAtual, UsuarioAtualHttp>();
        services.AddScoped<IGeradorToken, GeradorTokenJwt>();
        // Preenchida pelo Program.cs com a chave do banco, antes de a API começar a atender (docs/adr/0053).
        services.AddSingleton<ChaveAssinaturaJwt>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer().AddNegotiate();

        // Lido só quando a primeira requisição chega, depois de toda a configuração carregada
        // (inclusive a dos testes de integração).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration, ChaveAssinaturaJwt>((opcoes, configuracao, chave) =>
            {
                var jwt = OpcoesJwt.Ler(configuracao, chave.Valor);
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

        // Todo endpoint exige um usuário autenticado pelo JWT, a menos que tenha [AllowAnonymous] ou outra política (o login).
        // O que cada perfil pode fazer NÃO fica aqui: está na PoliticaPermissao (Domain).
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(PoliticaLoginWindows, politica => politica
                .AddAuthenticationSchemes(NegotiateDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());

        return services;
    }
}
