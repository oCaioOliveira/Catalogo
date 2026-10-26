using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao.Tipos;

/// <summary>
/// DATA_HORA: data e hora ISO 8601 COM fuso horário, ex.: "2026-10-21T14:30:00-03:00" ou "...Z".
/// Sem o fuso não dá para saber o instante exato, então o valor é recusado.
/// O valor é gravado convertido para UTC, ex.: "2026-10-21T17:30:00Z". RN04.
/// </summary>
public sealed partial class DataHoraValidador : ITipoDadoValidador
{
    /// <summary>Formato gravado. Os "F" omitem frações de segundo zeradas.</summary>
    public const string FormatoUtc = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    public TipoDado Tipo => TipoDado.DataHora;

    public ResultadoValidacaoValor Validar(Campo campo, JsonElement valor)
    {
        if (valor.ValueKind != JsonValueKind.String)
            return Falha();

        if (!TentarConverter(valor.GetString()!, out var dataHora))
            return Falha();

        return ResultadoValidacaoValor.Sucesso(JsonValue.Create(ParaTextoUtc(dataHora)));
    }

    /// <summary>Converte texto ISO 8601 com fuso. Também usado pelos filtros da listagem.</summary>
    public static bool TentarConverter(string texto, out DateTimeOffset dataHora)
    {
        dataHora = default;
        texto = texto.Trim();

        // O DateTimeOffset.TryParse sozinho aceita formatos que não são ISO (ex.: "10/21/2026"),
        // por isso o formato é conferido antes com uma expressão regular.
        return FormatoIso8601().IsMatch(texto)
            && DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out dataHora);
    }

    public static string ParaTextoUtc(DateTimeOffset dataHora) =>
        dataHora.UtcDateTime.ToString(FormatoUtc, CultureInfo.InvariantCulture);

    private static ResultadoValidacaoValor Falha() => ResultadoValidacaoValor.Falha(
        "Deve ser data e hora ISO 8601 com fuso horário (ex.: 2026-10-21T14:30:00-03:00 ou 2026-10-21T17:30:00Z).");

    // aaaa-mm-ddThh:mm, segundos e frações opcionais, e fuso obrigatório (Z ou ±hh:mm).
    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$")]
    private static partial Regex FormatoIso8601();
}
