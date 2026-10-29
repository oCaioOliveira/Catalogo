using System.Text.Json;
using System.Text.RegularExpressions;
using Catalogo.Migracao.Texto;

namespace Catalogo.Migracao.Conversao;

/// <summary>O que a coluna RTC tem: um work item (com o número) ou um link que não é work item (ex.: commit do Git).</summary>
public sealed record ValorRtc(string? NumeroWorkItem, string Url)
{
    public bool EhWorkItem => NumeroWorkItem is not null;
}

/// <summary>As regras de conversão das colunas simples do CSV (docs/migracao-sharepoint.md, tabela "coluna → destino").</summary>
public static partial class RegrasConversao
{
    /// <summary>Base para completar os links relativos do SharePoint ("/teams/...", "/:li:/t/..."). Configurável no gerador.</summary>
    public const string BaseSharePointPadrao = "https://caixa.sharepoint.com";

    /// <summary>
    /// RTC: o número do work item vem de "id=&lt;n&gt;" (link do RTC/CCM) ou de ".../WorkItem/&lt;n&gt;" (itemName, ex.: com.ibm.team.workitem.WorkItem/21463723). Qualquer outra coisa
    /// (ex.: um link de commit do Git colocado no lugar errado) NÃO é RTC: volta sem número, para virar um Link do relatório.
    /// </summary>
    public static ValorRtc? Rtc(string valor)
    {
        var texto = LimpezaTexto.NormalizarLinha(valor);
        if (texto.Length == 0)
            return null;
        var porId = RtcPorId().Match(texto);
        if (porId.Success)
            return new ValorRtc(porId.Groups[1].Value, texto);
        var porItem = RtcPorWorkItem().Match(texto);
        return porItem.Success ? new ValorRtc(porItem.Groups[1].Value, texto) : new ValorRtc(null, texto);
    }

    /// <summary>
    /// Link absoluto: os relativos do SharePoint ("/teams/...", "/:li:/t/...") são completados com a base. Devolve também se foi
    /// completado (para o relatório de anomalias).
    /// </summary>
    public static (string Url, bool Completado) LinkAbsoluto(string valor, string baseSharePoint)
    {
        var texto = LimpezaTexto.NormalizarLinha(valor);
        if (texto.StartsWith('/'))
            return (baseSharePoint.TrimEnd('/') + texto, true);
        return (texto, false);
    }

    /// <summary>Texto de exibição de um Link, pelo tipo do endereço.</summary>
    public static string TextoDoLink(string url)
    {
        var u = url.ToLowerInvariant();
        if (u.Contains("/_git/", StringComparison.Ordinal))
            return u.Contains("/commit/", StringComparison.Ordinal) ? "Script no Azure DevOps (commit)" : "Script no Azure DevOps";
        if (u.Contains("sharepoint", StringComparison.Ordinal) || u.Contains("/teams/", StringComparison.Ordinal) || u.Contains("/:li:/", StringComparison.Ordinal))
            return u.Contains("dispform.aspx", StringComparison.Ordinal) ? "SharePoint (relatório na lista)" : "SharePoint";
        return "Link";
    }

    /// <summary>Comunidade: trim e espaços colapsados (o nome exibido). Nomes com a mesma <see cref="LimpezaTexto.ChaveComparacao"/> são uma só.</summary>
    public static string NomeComunidade(string valor) => LimpezaTexto.NormalizarLinha(valor);

    /// <summary>Sigla: código em maiúsculas, sem espaços nas pontas.</summary>
    public static string CodigoSigla(string valor) => LimpezaTexto.NormalizarLinha(valor).ToUpperInvariant();

    /// <summary>Ambiente: os nomes do SharePoint, com o erro de digitação corrigido ("Contigência PRD" → "Contingência PRD").</summary>
    public static string NomeAmbiente(string valor)
    {
        var nome = LimpezaTexto.NormalizarLinha(valor);
        return LimpezaTexto.ChaveComparacao(nome) == "CONTIGENCIA PRD" ? "Contingência PRD" : nome;
    }

    /// <summary>Nº da bateria: "1ª" → 1, "23ª" → 23. Nulo se não houver número.</summary>
    public static int? NumeroBateria(string valor)
    {
        var casamento = PrimeiroNumero().Match(valor);
        return casamento.Success && int.TryParse(casamento.Value, out var n) ? n : null;
    }

    /// <summary>Título do relatório: "&lt;Contexto&gt; — &lt;N&gt;ª bateria" (até 200 caracteres).</summary>
    public static string Titulo(string contexto, int bateria)
    {
        var sufixo = $" — {bateria}ª bateria";
        var limpo = LimpezaTexto.NormalizarLinha(contexto);
        if (limpo.Length + sufixo.Length > 200)
            limpo = limpo[..(200 - sufixo.Length - 1)] + "…";
        return limpo + sufixo;
    }

    /// <summary>Executado por: os nomes separados por "; " ("A;B" → "A; B").</summary>
    public static string ExecutadoPor(string valor) =>
        string.Join("; ", LimpezaTexto.NormalizarLinha(valor).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>Os itens de uma coluna em array JSON do SharePoint (["API"], ["Java","Quarkus"]). Texto que não é JSON vira um item só.</summary>
    public static IReadOnlyList<string> ItensJson(string valor)
    {
        var texto = valor.Trim();
        if (texto.Length == 0)
            return [];
        if (texto.StartsWith('['))
        {
            try
            {
                return (JsonSerializer.Deserialize<List<string?>>(texto) ?? [])
                    .Where(i => !string.IsNullOrWhiteSpace(i))
                    .Select(i => LimpezaTexto.NormalizarLinha(i!))
                    .ToList();
            }
            catch (JsonException)
            {
                // Não é JSON: segue como texto.
            }
        }

        return [LimpezaTexto.NormalizarLinha(texto)];
    }

    /// <summary>
    /// Grafias canônicas das tecnologias de infraestrutura que só diferem por maiúsculas. Para cada grupo, vale a grafia mais
    /// frequente nos dados; nos empates, esta lista decide (siglas em maiúsculas).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> GrafiasPreferidas = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["BROKER"] = "Broker",
        ["COBOL"] = "COBOL",
        ["COSMOSDB"] = "CosmosDB",
        ["ORACLE"] = "Oracle",
        ["POSTGRES"] = "Postgres",
    };

    /// <summary>
    /// A grafia canônica de cada tecnologia, a partir de TODAS as ocorrências dos dados: as que só diferem por maiúsculas, acentos ou
    /// espaços viram a mais frequente (empates pela lista de preferidas, senão pela ordem alfabética).
    /// </summary>
    public static IReadOnlyDictionary<string, string> GrafiasInfraestrutura(IEnumerable<string> ocorrencias) =>
        ocorrencias
            .GroupBy(LimpezaTexto.ChaveComparacao)
            .ToDictionary(
                g => g.Key,
                g => GrafiasPreferidas.TryGetValue(g.Key, out var preferida) && g.Contains(preferida)
                    ? preferida
                    : g.GroupBy(x => x, StringComparer.Ordinal).OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.Ordinal).First().Key);

    /// <summary>Infraestrutura: as tecnologias do array JSON, com a grafia canônica, separadas por ", " (sem repetir).</summary>
    public static string Infraestrutura(string valor, IReadOnlyDictionary<string, string> grafias) =>
        string.Join(", ", ItensJson(valor).Select(i => grafias.GetValueOrDefault(LimpezaTexto.ChaveComparacao(i), i)).Distinct(StringComparer.Ordinal));

    [GeneratedRegex(@"[?&#]id=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex RtcPorId();

    [GeneratedRegex(@"[./]WorkItem/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex RtcPorWorkItem();

    [GeneratedRegex(@"\d+")]
    private static partial Regex PrimeiroNumero();
}
