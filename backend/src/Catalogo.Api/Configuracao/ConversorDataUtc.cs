using System.Text.Json;
using System.Text.Json.Serialization;

namespace Catalogo.Api.Configuracao;

/// <summary>
/// Datas de sistema SEMPRE em ISO 8601 UTC, com "Z" (ex.: "2026-10-21T17:30:00.1234567Z"), em toda a API.
/// O banco guarda datetime2 sem fuso, e um DateTime com Kind Unspecified sairia SEM o "Z": este conversor
/// trata todo valor sem fuso como UTC (é assim que a aplicação grava) e converte os locais para UTC.
/// Os campos DATA_HORA dos registros não passam por aqui: já ficam no JSON como texto UTC (RN04).
/// </summary>
internal sealed class ConversorDataUtc : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ParaUtc(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(ParaUtc(value));

    public static DateTime ParaUtc(DateTime valor) => valor.Kind switch
    {
        DateTimeKind.Utc => valor,
        DateTimeKind.Local => valor.ToUniversalTime(),
        _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc),
    };
}
