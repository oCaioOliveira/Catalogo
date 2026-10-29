using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Catalogo.Migracao.Sql;

/// <summary>
/// Literais SQL Server 2014 para os scripts da migração, seguros tanto no SSMS quanto no sqlcmd comum (sem -x):
///   - texto vira N'...' com as aspas dobradas; as quebras de linha ficam dentro do literal;
///   - uma linha do texto que o sqlcmd interpretaria como comando (GO, ":r", ":setvar", "!!"...) ou um "$(" (variável do sqlcmd) é
///     separada por concatenação (N'...' + N'GO...'), de modo que nenhuma linha física do script comece com eles e nenhum "$(" fique
///     inteiro; com concatenação, a primeira parte é CAST(... AS nvarchar(max)), senão o SQL Server cortaria o texto em 4000 caracteres;
///   - datas em ISO 8601 ('2024-03-10T17:30:00.0000000', independente do idioma da sessão), números em cultura invariante,
///     GUIDs entre aspas e binários em 0x....
/// </summary>
public static partial class LiteralSql
{
    public static string Texto(string? valor)
    {
        if (valor is null)
            return "NULL";

        var texto = valor.Replace("\r\n", "\n").Replace('\r', '\n');
        var partes = new List<string>();
        var atual = new StringBuilder();
        var linhas = texto.Split('\n');
        for (var i = 0; i < linhas.Length; i++)
        {
            var linha = linhas[i];
            if (i > 0)
            {
                atual.Append('\n');
                if (LinhaPerigosa().IsMatch(linha))
                {
                    partes.Add(atual.ToString());
                    atual.Clear();
                }
            }

            // "$(" vira "$" + "(" em partes separadas.
            var inicio = 0;
            int posicao;
            while ((posicao = linha.IndexOf("$(", inicio, StringComparison.Ordinal)) >= 0)
            {
                atual.Append(linha, inicio, posicao - inicio + 1);
                partes.Add(atual.ToString());
                atual.Clear();
                inicio = posicao + 1;
            }

            atual.Append(linha, inicio, linha.Length - inicio);
        }

        partes.Add(atual.ToString());
        if (LinhaPerigosa().IsMatch(linhas[0]))
            partes.Insert(0, "");

        if (partes.Count == 1)
            return Simples(partes[0]);
        return "(CAST(" + Simples(partes[0]) + " AS nvarchar(max))" + string.Concat(partes.Skip(1).Select(p => " + " + Simples(p))) + ")";
    }

    private static string Simples(string texto) => "N'" + texto.Replace("'", "''", StringComparison.Ordinal) + "'";

    public static string Guid(Guid valor) => $"'{valor:D}'";

    public static string Guid(Guid? valor) => valor is Guid g ? Guid(g) : "NULL";

    public static string DataHora(DateTime? valor) =>
        valor is DateTime d ? $"'{d.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture)}'" : "NULL";

    public static string Numero(decimal? valor) => valor is decimal d ? d.ToString(CultureInfo.InvariantCulture) : "NULL";

    public static string Bit(bool? valor) => valor is bool b ? (b ? "1" : "0") : "NULL";

    public static string Binario(byte[] valor) => "0x" + Convert.ToHexString(valor);

    /// <summary>Linha que o sqlcmd trataria como comando: "GO" (com repetição opcional), ":comando" ou "!!".</summary>
    [GeneratedRegex(@"^\s*(go(\s+\d+)?\s*$|:|!!)", RegexOptions.IgnoreCase)]
    private static partial Regex LinhaPerigosa();
}
