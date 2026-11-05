namespace Catalogo.Domain.Acesso;

/// <summary>
/// A matrícula a partir do nome que a autenticação integrada do Windows (AD) entrega (RN23, docs/adr/0054):
/// <c>DOMINIO\c161587</c> → <c>C161587</c>. Também aceita o formato UPN (<c>c161587@dominio</c>) e o nome sem domínio.
/// </summary>
public static class MatriculaWindows
{
    /// <summary>Tamanho máximo da matrícula (coluna CO_MATRICULA).</summary>
    public const int TamanhoMaximo = 20;

    /// <summary>A matrícula em maiúsculas, ou nulo se o nome estiver vazio ou não der uma matrícula válida.</summary>
    public static string? Extrair(string? nomeWindows)
    {
        var nome = nomeWindows?.Trim();
        if (string.IsNullOrEmpty(nome))
            return null;

        var barra = nome.LastIndexOf('\\');
        if (barra >= 0)
            nome = nome[(barra + 1)..];
        else if (nome.IndexOf('@') is var arroba and > 0)
            nome = nome[..arroba];

        var matricula = nome.Trim().ToUpperInvariant();
        return matricula.Length is > 0 and <= TamanhoMaximo && !matricula.Any(char.IsWhiteSpace) && !matricula.Contains('@')
            ? matricula
            : null;
    }
}
