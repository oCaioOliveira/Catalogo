using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Net.Http.Headers;

namespace Catalogo.Api.Configuracao;

/// <summary>
/// CORS para o front-end, que roda em outra origem (docs/seguranca.md#cors).
///   - Origens: só as de "Cors:OrigensPermitidas". Lista vazia = nenhuma origem de outro site é aceita.
///   - Métodos GET, POST, PUT, PATCH e DELETE; cabeçalhos Authorization e Content-Type.
///   - Expõe Location (201) e Content-Disposition (nome do arquivo da exportação).
///   - SEM credenciais (cookies): a autenticação é o JWT no cabeçalho Authorization.
/// Nunca há AllowAnyOrigin: "*" na configuração é recusado na inicialização.
/// </summary>
public static class ConfiguracaoCors
{
    public const string ChaveOrigens = "Cors:OrigensPermitidas";

    public static IServiceCollection AdicionarCors(this IServiceCollection services)
    {
        services.AddCors();

        // Lido quando a primeira requisição chega, depois de toda a configuração carregada (inclusive a dos testes).
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((opcoes, configuracao) =>
            opcoes.AddDefaultPolicy(politica => politica
                .WithOrigins(LerOrigens(configuracao))
                .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete)
                .WithHeaders(HeaderNames.Authorization, HeaderNames.ContentType)
                .WithExposedHeaders(HeaderNames.Location, HeaderNames.ContentDisposition)));

        return services;
    }

    /// <summary>
    /// As origens configuradas, sem barra no fim. Falha se alguma tiver "*" ou não for uma origem completa
    /// (http ou https, servidor e porta opcional, sem caminho).
    /// </summary>
    public static string[] LerOrigens(IConfiguration configuracao)
    {
        var origens = configuracao.GetSection(ChaveOrigens).Get<string[]>() ?? [];
        foreach (var origem in origens)
        {
            if (origem.Contains('*') || !EhOrigem(origem))
            {
                throw new InvalidOperationException(
                    $"'{origem}' em {ChaveOrigens} não é uma origem válida. Liste cada origem completa (ex.: https://catalogo.empresa.com.br); \"*\" não é aceito.");
            }
        }

        return origens.Select(o => o.TrimEnd('/')).ToArray();
    }

    private static bool EhOrigem(string texto) =>
        Uri.TryCreate(texto, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}
