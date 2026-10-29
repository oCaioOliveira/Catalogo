using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao.Tipos;

/// <summary>
/// URL: endereço ABSOLUTO com esquema http ou https, até 2000 caracteres, sem espaços (RN04, ADR 0042). Normalização: só o trim
/// (o caminho e a consulta diferenciam maiúsculas, então o texto é gravado como veio). Na chave única, o esquema e o host
/// não diferenciam maiúsculas (CalculadoraChaveUnica.NormalizarUrl).
/// </summary>
public sealed class UrlValidador : ITipoDadoValidador
{
    public const int MaximoCaracteres = 2000;

    public TipoDado Tipo => TipoDado.Url;

    public ResultadoValidacaoValor Validar(Campo campo, JsonElement valor)
    {
        if (valor.ValueKind != JsonValueKind.String)
            return ResultadoValidacaoValor.Falha("Deve ser um texto com a URL.");

        var texto = valor.GetString()!.Trim();
        return Conferir(texto) is string erro
            ? ResultadoValidacaoValor.Falha(erro)
            : ResultadoValidacaoValor.Sucesso(JsonValue.Create(texto));
    }

    /// <summary>
    /// A regra da URL, num lugar só: o validador dos registros e a verificação contra os dados da mudança de tipo para URL (RN66)
    /// usam este método. Recebe o texto já sem espaços nas pontas. Nulo = válida.
    /// </summary>
    public static string? Conferir(string texto)
    {
        if (texto.Length > MaximoCaracteres)
            return $"Deve ter no máximo {MaximoCaracteres} caracteres (foram informados {texto.Length}).";
        if (texto.Any(char.IsWhiteSpace))
            return "A URL não pode ter espaços.";
        if (!Uri.TryCreate(texto, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host)
            || !texto.Contains("://", StringComparison.Ordinal))
        {
            return "Deve ser uma URL completa, começando com http:// ou https:// (ex.: https://exemplo.com.br/pagina).";
        }

        return null;
    }
}
