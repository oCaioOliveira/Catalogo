namespace Catalogo.Domain.Metadados;

/// <summary>
/// Tipos de dado que um <see cref="Campo"/> pode ter.
/// No banco ficam como texto na coluna Campo.TipoDado (CHECK CK_Campo_TipoDado).
/// Ao adicionar um tipo novo, siga o passo a passo de src/Catalogo.Domain/AGENTS.md.
/// </summary>
public enum TipoDado
{
    Texto,
    TextoLongo,
    Inteiro,
    Decimal,
    Booleano,
    Data,
    DataHora,
    Lista,
    Referencia,

    /// <summary>Endereço http(s) absoluto (ADR 0042). O valor vai para RegistroValor.ValorTexto.</summary>
    Url,
}

/// <summary>
/// Converte <see cref="TipoDado"/> de e para o texto gravado no banco ("TEXTO", "DATA_HORA"...).
/// É um switch explícito de propósito: o nome no banco não segue o nome do enum.
/// </summary>
public static class TipoDadoConversor
{
    public static string ParaTexto(TipoDado tipo) => tipo switch
    {
        TipoDado.Texto => "TEXTO",
        TipoDado.TextoLongo => "TEXTO_LONGO",
        TipoDado.Inteiro => "INTEIRO",
        TipoDado.Decimal => "DECIMAL",
        TipoDado.Booleano => "BOOLEANO",
        TipoDado.Data => "DATA",
        TipoDado.DataHora => "DATA_HORA",
        TipoDado.Lista => "LISTA",
        TipoDado.Referencia => "REFERENCIA",
        TipoDado.Url => "URL",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "TipoDado sem texto correspondente."),
    };

    public static TipoDado DeTexto(string texto) => texto switch
    {
        "TEXTO" => TipoDado.Texto,
        "TEXTO_LONGO" => TipoDado.TextoLongo,
        "INTEIRO" => TipoDado.Inteiro,
        "DECIMAL" => TipoDado.Decimal,
        "BOOLEANO" => TipoDado.Booleano,
        "DATA" => TipoDado.Data,
        "DATA_HORA" => TipoDado.DataHora,
        "LISTA" => TipoDado.Lista,
        "REFERENCIA" => TipoDado.Referencia,
        "URL" => TipoDado.Url,
        _ => throw new ArgumentOutOfRangeException(nameof(texto), texto, "Texto de TipoDado desconhecido."),
    };
}
