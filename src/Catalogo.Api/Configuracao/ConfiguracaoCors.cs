using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Net.Http.Headers;

namespace Catalogo.Api.Configuracao;

/// <summary>
/// CORS para o front-end, que roda em outra origem (docs/seguranca.md#cors).
///   - Origens: as de CORS_ALLOW (raiz do appsettings.json), separadas por vírgula. "*" sozinho libera qualquer origem.
///   - Métodos GET, POST, PUT, PATCH e DELETE; cabeçalhos Authorization e Content-Type.
///   - Expõe Location (201) e Content-Disposition (nome do arquivo da exportação).
///   - SEM credenciais (cookies): a autenticação é o JWT no cabeçalho Authorization (por isso "*" é permitido).
/// </summary>
public static class ConfiguracaoCors
{
    public const string ChaveOrigens = ConfiguracaoDeploy.ChaveCors;

    public const string TodasAsOrigens = "*";

    public static IServiceCollection AdicionarCors(this IServiceCollection services)
    {
        services.AddCors();

        // Lido quando a primeira requisição chega, depois de toda a configuração carregada (inclusive a dos testes).
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((opcoes, configuracao) =>
            opcoes.AddDefaultPolicy(politica =>
            {
                var origens = LerOrigens(configuracao);
                if (origens.Todas)
                    politica.AllowAnyOrigin();
                else
                    politica.WithOrigins(origens.Lista);

                politica
                    .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete)
                    .WithHeaders(HeaderNames.Authorization, HeaderNames.ContentType)
                    .WithExposedHeaders(HeaderNames.Location, HeaderNames.ContentDisposition);
            }));

        return services;
    }

    /// <summary>
    /// As origens de CORS_ALLOW, sem espaços e sem barra no fim. Falha se a chave estiver vazia, se "*" vier junto de outras origens,
    /// ou se alguma não for uma origem completa (http ou https, servidor e porta opcional, sem caminho e sem curinga parcial).
    /// </summary>
    public static OrigensCors LerOrigens(IConfiguration configuracao)
    {
        var origens = (configuracao[ChaveOrigens] ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (origens.Length == 0)
            throw new InvalidOperationException($"{ChaveOrigens} vazio. Liste as origens do front separadas por vírgula, ou \"*\" para liberar todas.");

        if (origens.Contains(TodasAsOrigens))
        {
            if (origens.Length > 1)
                throw new InvalidOperationException($"{ChaveOrigens}: \"*\" libera todas as origens e não pode vir junto de outras ('{configuracao[ChaveOrigens]}').");
            return new OrigensCors(true, []);
        }

        foreach (var origem in origens)
        {
            if (origem.Contains('*') || !EhOrigem(origem))
            {
                throw new InvalidOperationException(
                    $"'{origem}' em {ChaveOrigens} não é uma origem válida. Liste cada origem completa (ex.: https://catalogo.empresa.com.br), " +
                    "separadas por vírgula, ou só \"*\" para liberar todas.");
            }
        }

        return new OrigensCors(false, origens.Select(o => o.TrimEnd('/')).ToArray());
    }

    private static bool EhOrigem(string texto) =>
        Uri.TryCreate(texto, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}

/// <summary>As origens aceitas pelo CORS: <see cref="Todas"/> (CORS_ALLOW = "*") ou só as da <see cref="Lista"/>.</summary>
public sealed record OrigensCors(bool Todas, string[] Lista);
