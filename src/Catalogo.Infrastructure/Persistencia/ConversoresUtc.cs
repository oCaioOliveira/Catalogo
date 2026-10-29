using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Catalogo.Infrastructure.Persistencia;

/// <summary>
/// datetime2 não guarda o fuso. Todas as datas do sistema são gravadas em UTC, então marcamos como UTC
/// ao ler (assim a API devolve "...Z").
/// </summary>
internal static class ConversoresUtc
{
    public static readonly ValueConverter<DateTime, DateTime> Data =
        new(valor => valor, valor => DateTime.SpecifyKind(valor, DateTimeKind.Utc));

    public static readonly ValueConverter<DateTime?, DateTime?> DataOpcional =
        new(valor => valor, valor => valor == null ? null : DateTime.SpecifyKind(valor.Value, DateTimeKind.Utc));
}
