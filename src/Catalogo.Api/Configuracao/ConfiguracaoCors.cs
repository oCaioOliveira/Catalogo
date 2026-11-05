using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Net.Http.Headers;

namespace Catalogo.Api.Configuracao;

/// <summary>
/// CORS para o front-end, que roda em outra origem (docs/seguranca.md#cors).
///   - Origens: as de CORS_ALLOW (raiz do appsettings.json), separadas por vírgula, sempre EXATAS (sem "*"): o login pelo Windows
///     manda credenciais, e o navegador só as aceita de uma origem listada (docs/adr/0054).
///   - COM credenciais (AllowCredentials): o POST /auth/login é chamado com withCredentials (autenticação integrada do Windows).
///   - Métodos GET, POST, PUT, PATCH e DELETE; cabeçalhos Authorization e Content-Type.
///   - Expõe Location (201) e Content-Disposition (nome do arquivo da exportação).
/// </summary>
public static class ConfiguracaoCors
{
    public const string ChaveOrigens = ConfiguracaoDeploy.ChaveCors;

    public static IServiceCollection AdicionarCors(this IServiceCollection services)
    {
        services.AddCors();

        // Lido quando a primeira requisição chega, depois de toda a configuração carregada (inclusive a dos testes).
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((opcoes, configuracao) =>
            opcoes.AddDefaultPolicy(politica => politica
                .WithOrigins(LerOrigens(configuracao))
                .AllowCredentials()
                .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Put, HttpMethods.Patch, HttpMethods.Delete)
                .WithHeaders(HeaderNames.Authorization, HeaderNames.ContentType)
                .WithExposedHeaders(HeaderNames.Location, HeaderNames.ContentDisposition)));

        return services;
    }

    /// <summary>
    /// As origens de CORS_ALLOW, sem espaços e sem barra no fim. Falha se a chave estiver vazia ou se alguma não for uma origem
    /// completa e exata (http ou https, servidor e porta opcional, sem caminho e sem "*", nem sozinho nem parcial).
    /// </summary>
    public static string[] LerOrigens(IConfiguration configuracao)
    {
        var valor = ConfiguracaoDeploy.LerValor(configuracao, ChaveOrigens) ?? "";
        var origens = valor
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (origens.Length == 0)
            throw new InvalidOperationException($"{ChaveOrigens} vazio. Liste as origens do front, completas, separadas por vírgula.");

        foreach (var origem in origens)
        {
            if (origem.Contains('*') || !EhOrigem(origem))
            {
                throw new InvalidOperationException(
                    $"'{origem}' em {ChaveOrigens} não é uma origem válida. Liste cada origem completa e exata (ex.: " +
                    "https://catalogo.empresa.com.br), separadas por vírgula. \"*\" não é aceito: o login pelo Windows usa credenciais.");
            }
        }

        return origens.Select(o => o.TrimEnd('/')).ToArray();
    }

    private static bool EhOrigem(string texto) =>
        Uri.TryCreate(texto, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}
