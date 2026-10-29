using System.Globalization;
using System.Text.Json.Nodes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Registros;

/// <summary>
/// Converte um valor de campo simples entre o JSON normalizado (Registro.Dados) e a coluna
/// tipada de <see cref="RegistroValor"/>. Tabela tipo → coluna em docs/modelo-de-dados.md.
/// </summary>
public static class ConversorValorTipado
{
    /// <summary>
    /// Monta o <see cref="RegistroValor"/> de um campo simples já validado (RN04).
    /// Devolve nulo para REFERENCIA, que não entra em RegistroValor.
    /// </summary>
    public static RegistroValor? ParaValor(Campo campo, JsonNode valor)
    {
        var registroValor = new RegistroValor { CampoId = campo.Id, EntidadeId = campo.EntidadeId };

        switch (campo.TipoDado)
        {
            case TipoDado.Texto or TipoDado.TextoLongo or TipoDado.Lista or TipoDado.Url:
                registroValor.ValorTexto = valor.GetValue<string>();
                break;
            case TipoDado.Inteiro:
                registroValor.ValorNumero = valor.GetValue<int>();
                break;
            case TipoDado.Decimal:
                registroValor.ValorNumero = valor.GetValue<decimal>();
                break;
            case TipoDado.Data:
                registroValor.ValorData = DateOnly
                    .ParseExact(valor.GetValue<string>(), DataValidador.Formato, CultureInfo.InvariantCulture)
                    .ToDateTime(TimeOnly.MinValue);
                break;
            case TipoDado.DataHora:
                registroValor.ValorData = DateTimeOffset.Parse(valor.GetValue<string>(), CultureInfo.InvariantCulture).UtcDateTime;
                break;
            case TipoDado.Booleano:
                registroValor.ValorBooleano = valor.GetValue<bool>();
                break;
            case TipoDado.Referencia:
                return null;
            default:
                throw new ArgumentOutOfRangeException(nameof(campo), campo.TipoDado, "TipoDado sem coluna em RegistroValor.");
        }

        return registroValor;
    }

    /// <summary>
    /// Valor de um campo para uma planilha (exportação), já tipado a partir do JSON normalizado de Registro.Dados:
    /// TEXTO, TEXTO_LONGO e LISTA → <see cref="string"/>; INTEIRO e DECIMAL → <see cref="decimal"/>;
    /// DATA → <see cref="DateOnly"/>; DATA_HORA → <see cref="DateTime"/> em UTC; BOOLEANO → <see cref="bool"/>;
    /// REFERENCIA (que sai do registro como { id, rotulo }, RN20) → o rótulo, ou o id se não houver rótulo.
    /// Nulo se o campo está vazio.
    /// </summary>
    public static object? ParaPlanilha(Campo campo, JsonNode? valor)
    {
        if (valor is null)
            return null;

        if (campo.EhReferencia)
            return valor["rotulo"]?.GetValue<string>() ?? valor["id"]?.GetValue<string>();

        var tipado = ParaValor(campo, valor)!;

        // URL: um Uri, para o XLSX gravar um hiperlink clicável (o CSV escreve o texto original). ADR 0042.
        if (campo.TipoDado == TipoDado.Url && Uri.TryCreate(tipado.ValorTexto, UriKind.Absolute, out var endereco))
            return endereco;

        if (tipado.ValorData is DateTime data)
            return campo.TipoDado == TipoDado.Data ? DateOnly.FromDateTime(data) : DateTime.SpecifyKind(data, DateTimeKind.Utc);

        return (object?)tipado.ValorTexto ?? (object?)tipado.ValorNumero ?? tipado.ValorBooleano;
    }

    /// <summary>
    /// Texto de um valor tipado, no mesmo formato do JSON normalizado (sem aspas). Usado no rótulo
    /// das referências (RN20). Ex.: DECIMAL(18,2) 35.5 → "35.50"; DATA_HORA → "2026-10-21T17:30:00Z".
    /// </summary>
    public static string? ParaTexto(TipoDado tipo, byte? escala, RegistroValor valor) => tipo switch
    {
        TipoDado.Texto or TipoDado.TextoLongo or TipoDado.Lista or TipoDado.Url => valor.ValorTexto,
        TipoDado.Inteiro => valor.ValorNumero is decimal inteiro ? decimal.ToInt32(inteiro).ToString(CultureInfo.InvariantCulture) : null,
        TipoDado.Decimal => valor.ValorNumero?.ToString("F" + (escala ?? 0), CultureInfo.InvariantCulture),
        TipoDado.Data => valor.ValorData?.ToString(DataValidador.Formato, CultureInfo.InvariantCulture),
        TipoDado.DataHora => valor.ValorData is DateTime data
            ? DataHoraValidador.ParaTextoUtc(new DateTimeOffset(DateTime.SpecifyKind(data, DateTimeKind.Utc)))
            : null,
        TipoDado.Booleano => valor.ValorBooleano switch { true => "true", false => "false", null => null },
        _ => null,
    };
}
