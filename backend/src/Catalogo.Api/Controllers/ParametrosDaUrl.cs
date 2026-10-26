namespace Catalogo.Api.Controllers;

internal static class ParametrosDaUrl
{
    /// <summary>
    /// Todos os parâmetros da URL como pares (chave, valor), um par por valor: "?a=1&amp;a=2" vira dois pares.
    /// Os filtros da listagem são dinâmicos (dependem dos campos da entidade), por isso são lidos daqui
    /// e interpretados no Domain (InterpretadorConsulta).
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Ler(HttpRequest request) =>
        request.Query
            .SelectMany(parametro => parametro.Value.Select(valor => KeyValuePair.Create(parametro.Key, valor ?? "")))
            .ToList();
}
