namespace Catalogo.Application.Comum;

/// <summary>
/// Proteção contra injeção de fórmula (CSV/formula injection) nas exportações: um texto que começa com
/// "=", "+", "-", "@", tab ou carriage return pode ser executado como fórmula pelo Excel ao abrir o arquivo.
/// Esses textos recebem um apóstrofo no início, no CSV e no XLSX. Números e datas não passam por aqui
/// (um número negativo continua número).
/// </summary>
public static class ProtecaoFormula
{
    private static readonly char[] InicioPerigoso = ['=', '+', '-', '@', '\t', '\r'];

    public static string? Neutralizar(string? texto) =>
        texto is { Length: > 0 } && Array.IndexOf(InicioPerigoso, texto[0]) >= 0 ? "'" + texto : texto;
}
