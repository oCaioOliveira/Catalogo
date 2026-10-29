using System.Globalization;
using System.Text.RegularExpressions;
using Catalogo.Migracao.Texto;

namespace Catalogo.Migracao.Cenarios;

/// <summary>O que o ponto significa numa coluna: separador de milhar (o padrão brasileiro) ou separador decimal.</summary>
public enum EstiloPonto
{
    Milhar,
    Decimal,
}

/// <summary>Resultado da leitura de uma célula numérica.</summary>
public sealed record LeituraNumero(decimal? Valor, bool Vazia, string? Problema, string? Unidade, string? Observacao)
{
    public static LeituraNumero DeVazia() => new(null, true, null, null, null);
    public static LeituraNumero Falha(string problema) => new(null, false, problema, null, null);
    public bool Valida => Valor is not null;
}

/// <summary>
/// Números das tabelas do SharePoint, no formato brasileiro: milhar com ponto, decimal com vírgula ("1.234,56"), com ou sem
/// unidade ("ms", "s", "req/s", "TPS", "%", "VUs"...). O PONTO SOZINHO é ambíguo ("1.178" = mil cento e setenta e oito ou um
/// vírgula cento e setenta e oito?): decide o estilo da COLUNA (<see cref="EstiloDaColuna"/>): se a coluna tem algum valor com ponto
/// seguido de uma quantidade de dígitos diferente de 3 ("119.3", "0.25"), o ponto é decimal nela; senão, é milhar. Faixas e
/// textos ("10 a 100", "75–84", "N/A") não são números: voltam com o problema, para quem chama decidir.
/// </summary>
public static partial class NumeroBrasileiro
{
    private static readonly HashSet<string> Vazios = ["", "-", "--", "—", "–", "n/a", "na", "n/d", "nd", "n.a.", "n.d.", "não se aplica", "nao se aplica"];

    /// <summary>O estilo do ponto numa coluna, pelos valores dela (ver a classe).</summary>
    public static EstiloPonto EstiloDaColuna(IEnumerable<string> valores)
    {
        foreach (var valor in valores)
        {
            var token = Token(valor);
            if (token is null || token.Contains(','))
                continue;
            var pontos = token.Count(c => c == '.');
            if (pontos != 1)
                continue;
            var depois = token[(token.IndexOf('.') + 1)..];
            if (depois.Length != 3 || token.StartsWith("0.", StringComparison.Ordinal) || token.StartsWith("-0.", StringComparison.Ordinal))
                return EstiloPonto.Decimal;
        }

        return EstiloPonto.Milhar;
    }

    /// <summary>Lê o número de uma célula, com o estilo do ponto da coluna. Ver a classe.</summary>
    public static LeituraNumero Ler(string celula, EstiloPonto estilo)
    {
        var texto = LimpezaTexto.NormalizarLinha(celula);
        if (Vazios.Contains(texto.ToLowerInvariant()))
            return LeituraNumero.DeVazia();

        // Nota depois do número ("50 (compartilhado)", "1.700 TPS (Total)", "16% *"): o número vale, a nota vai para a observação.
        string? nota = null;
        var comNota = NotaNoFim().Match(texto);
        if (comNota.Success && comNota.Groups["resto"].Value.Trim().Length > 0)
        {
            nota = comNota.Groups["nota"].Value.Trim();
            texto = comNota.Groups["resto"].Value.Trim();
        }

        var casamento = NumeroComUnidade().Match(texto);
        if (!casamento.Success)
        {
            return MaisDeUmNumero().IsMatch(texto)
                ? LeituraNumero.Falha($"faixa ou mais de um número ('{texto}')")
                : LeituraNumero.Falha($"não é um número ('{texto}')");
        }

        var token = casamento.Groups["numero"].Value.Replace(" ", "");
        var unidade = casamento.Groups["unidade"].Success ? casamento.Groups["unidade"].Value.Trim().ToLowerInvariant() : null;
        if (unidade is not null)
            unidade = Unidades().Replace(unidade, m => m.Value.StartsWith("/s", StringComparison.Ordinal) ? "/s" : "/min");
        var (valor, observacao) = Interpretar(token, estilo);
        if (valor is not null && casamento.Groups["mil"].Success)
        {
            valor *= 1000;
            observacao = observacao is null ? $"'{texto}' lido como {valor} (mil)" : $"{observacao}; mil";
        }

        if (casamento.Groups["depois"].Success && casamento.Groups["depois"].Value.Trim().Length > 0)
            nota = nota is null ? casamento.Groups["depois"].Value.Trim() : $"{casamento.Groups["depois"].Value.Trim()} {nota}";
        if (nota is not null)
            observacao = observacao is null ? $"nota \"{nota}\"" : $"{observacao}; nota \"{nota}\"";
        return valor is null
            ? LeituraNumero.Falha($"número em formato não reconhecido ('{texto}')")
            : new LeituraNumero(valor, false, null, unidade, observacao);
    }

    /// <summary>O número de um token só com algarismos, sinal, pontos e vírgulas. A observação diz quando o ponto foi decidido pela coluna.</summary>
    private static (decimal? Valor, string? Observacao) Interpretar(string token, EstiloPonto estilo)
    {
        var negativo = token.StartsWith('-');
        var digitos = token.TrimStart('+', '-');
        var virgulas = digitos.Count(c => c == ',');
        var pontos = digitos.Count(c => c == '.');
        string normalizado;
        string? observacao = null;

        if (virgulas > 0 && pontos > 0)
        {
            // Os dois: o último é o decimal ("1.234,56" brasileiro; "1,234.56" americano).
            normalizado = digitos.LastIndexOf(',') > digitos.LastIndexOf('.')
                ? digitos.Replace(".", "").Replace(',', '.')
                : digitos.Replace(",", "");
        }
        else if (virgulas > 1)
        {
            normalizado = MilharComVirgula().IsMatch(digitos) ? digitos.Replace(",", "") : "";
        }
        else if (virgulas == 1)
        {
            normalizado = digitos.Replace(',', '.');
        }
        else if (pontos > 1)
        {
            normalizado = MilharComPonto().IsMatch(digitos) ? digitos.Replace(".", "") : "";
        }
        else if (pontos == 1)
        {
            var depois = digitos[(digitos.IndexOf('.') + 1)..];
            if (depois.Length == 3 && !digitos.StartsWith("0.", StringComparison.Ordinal))
            {
                if (estilo == EstiloPonto.Decimal)
                {
                    normalizado = digitos;
                    observacao = $"'{token}' lido como decimal (a coluna usa ponto decimal)";
                }
                else
                {
                    normalizado = digitos.Replace(".", "");
                }
            }
            else
            {
                normalizado = digitos;
            }
        }
        else
        {
            normalizado = digitos;
        }

        if (!decimal.TryParse(normalizado, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor))
            return (null, null);
        return (negativo ? -valor : valor, observacao);
    }

    /// <summary>O token numérico de uma célula (sem a unidade), ou nulo se ela não for "número + unidade".</summary>
    private static string? Token(string celula)
    {
        var casamento = NumeroComUnidade().Match(LimpezaTexto.NormalizarLinha(celula));
        return casamento.Success ? casamento.Groups["numero"].Value.Replace(" ", "") : null;
    }

    /// <summary>
    /// Um número, com ou sem sinal, e uma unidade opcional no fim. Aceita espaço como separador de milhar só entre grupos de 3.
    /// </summary>
    [GeneratedRegex(@"^(?<numero>[+-]?\d{1,3}(?:[ .,]\d{3})*(?:[.,]\d+)?|[+-]?\d+(?:[.,]\d+)?|[+-]?[.,]\d+)\s*(?<mil>mil\b)?\s*(?<unidade>%|ms|s|seg|segs|segundos?|milissegundos?|min|minutos?|req/?min|reqs?/min|tpm|/min|/sec|/seg|/s|req/?s|reqs/s|rps|tps|tx/s|transa[cç](?:ões|oes)/s(?:egundo)?|msg/s|mensagens/s(?:egundo)?|vus?|uvs?|usu[aá]rios|threads)?(?<depois>\s+[A-Za-z]{2,10})?\.?$", RegexOptions.IgnoreCase)]
    private static partial Regex NumeroComUnidade();

    [GeneratedRegex(@"^/(sec|seg|s)$|^/min$")]
    private static partial Regex Unidades();

    /// <summary>Um número seguido de uma nota: parênteses (fechados ou não) e/ou asteriscos no fim.</summary>
    [GeneratedRegex(@"^(?<resto>.*?\d[^()]*?)\s*(?<nota>\(.*|\*+)$")]
    private static partial Regex NotaNoFim();

    [GeneratedRegex(@"\d[\d.,]*\s*(a|até|ate|-|–|—|/|e|~)\s*\d")]
    private static partial Regex MaisDeUmNumero();

    [GeneratedRegex(@"^\d{1,3}(,\d{3})+$")]
    private static partial Regex MilharComVirgula();

    [GeneratedRegex(@"^\d{1,3}(\.\d{3})+$")]
    private static partial Regex MilharComPonto();
}
