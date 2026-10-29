using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;

namespace Catalogo.Domain.Validacao.Tipos;

/// <summary>DATA: data sem hora no formato ISO 8601 "aaaa-mm-dd" (ex.: "2026-10-21"). RN04.</summary>
public sealed class DataValidador : ITipoDadoValidador
{
    public const string Formato = "yyyy-MM-dd";

    public TipoDado Tipo => TipoDado.Data;

    public ResultadoValidacaoValor Validar(Campo campo, JsonElement valor)
    {
        if (valor.ValueKind != JsonValueKind.String || !TentarConverter(valor.GetString()!, out var data))
            return ResultadoValidacaoValor.Falha("Deve ser uma data no formato aaaa-mm-dd (ex.: 2026-10-21).");

        return ResultadoValidacaoValor.Sucesso(JsonValue.Create(data.ToString(Formato, CultureInfo.InvariantCulture)));
    }

    /// <summary>Converte texto "aaaa-mm-dd". Também usado pelos filtros da listagem.</summary>
    public static bool TentarConverter(string texto, out DateOnly data) =>
        DateOnly.TryParseExact(texto.Trim(), Formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out data);
}
