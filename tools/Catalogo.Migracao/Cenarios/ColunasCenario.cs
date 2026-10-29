using System.Text.RegularExpressions;
using Catalogo.Migracao.Texto;

namespace Catalogo.Migracao.Cenarios;

/// <summary>O SENTIDO de uma coluna da tabela de cenários, seja qual for o texto do cabeçalho.</summary>
public enum SentidoColuna
{
    Cenario,
    Servico,
    Metodo,
    Endpoint,
    Usuarios,
    Vazao,
    TempoMedio,
    Percentil95,
    Amostras,
    Erros,
}

/// <summary>Uma coluna reconhecida: o sentido e o fator que leva o valor à unidade do campo (segundos → ms = 1000; por minuto → TPS = 1/60).</summary>
public sealed record ColunaReconhecida(SentidoColuna Sentido, decimal Fator, string? Conversao);

/// <summary>
/// Reconhece as colunas da tabela de cenários PELO SENTIDO: o cabeçalho é comparado sem acentos, maiúsculas, caracteres invisíveis
/// e espaços repetidos, e a unidade entre parênteses decide a conversão. Exemplos das 91 variações reais:
/// "Vazão média total (Transações/segundo)" e "Throughput (req/s)" → vazão; "Tempo médio de resposta (milissegundos)", "Média (ms)"
/// e "Average (ms)" → tempo médio; "Percentil 95 (segundos)" → percentil 95, ×1000; "Usuários Virtuais (Threads simultâneas)" e
/// "VUs" → usuários virtuais. Colunas de outro sentido (CPU, memória, p99, vazão máxima...) não são reconhecidas: os valores delas
/// vão para as observações dos cenários.
/// </summary>
public static partial class ColunasCenario
{
    public static ColunaReconhecida? Reconhecer(string cabecalho)
    {
        var h = LimpezaTexto.SemAcentosMinusculo(cabecalho).Trim(' ', ':', '*', '.');
        if (h.Length == 0)
            return null;
        var semUnidade = Parenteses().Replace(h, " ").Trim();
        semUnidade = Espacos().Replace(semUnidade, " ");
        var emSegundos = EmSegundos().IsMatch(h) && !h.Contains("mili", StringComparison.Ordinal) && !h.Contains("(ms", StringComparison.Ordinal);
        var porMinuto = h.Contains("minuto", StringComparison.Ordinal) && !h.Contains("segundo", StringComparison.Ordinal);

        if (semUnidade is "cenario" or "cenarios" or "cenario(s)" or "enario" or "n do cenario" or "no do cenario" or "n cenario" or "#")
            return new(SentidoColuna.Cenario, 1, null);
        if (semUnidade is "nome do servico" or "servico" or "servicos" or "servico(s)" or "nome da chamada" or "chamada" or "chamadas"
            or "funcionalidade" or "nome do servico/fila" or "servico/fila")
            return new(SentidoColuna.Servico, 1, null);
        if (semUnidade is "metodo" or "metodos" or "metodo http" or "verbo")
            return new(SentidoColuna.Metodo, 1, null);
        if (semUnidade.StartsWith("endpoint", StringComparison.Ordinal) || semUnidade is "url" or "urls" or "fila" or "filas" or "fila(s)" or "fila mq"
            or "filas mq" or "recurso" or "uri")
            return new(SentidoColuna.Endpoint, 1, null);
        if (semUnidade.StartsWith("usuarios virtuais", StringComparison.Ordinal) || semUnidade is "vus" or "vu" or "quantidade de usuarios"
            or "qtd usuarios" or "qtde usuarios" or "qtd de usuarios" or "threads simultaneas" or "carga de usuario" or "carga de usuarios"
            or "usuarios" or "usuarios simultaneos")
            return new(SentidoColuna.Usuarios, 1, null);
        if (semUnidade.StartsWith("amostras", StringComparison.Ordinal) || semUnidade.StartsWith("total de amostras", StringComparison.Ordinal)
            || semUnidade is "qtd amostras" or "quantidade de amostras" or "requisicoes" or "total de requisicoes")
            return new(SentidoColuna.Amostras, 1, null);
        if (semUnidade.StartsWith("erro", StringComparison.Ordinal) || semUnidade.StartsWith("taxa de erro", StringComparison.Ordinal)
            || semUnidade is "% erros" or "% erro" or "% de erros")
            return new(SentidoColuna.Erros, 1, null);
        if (EhPercentil95(h, semUnidade))
            return emSegundos ? new(SentidoColuna.Percentil95, 1000, "segundos → ms (×1000)") : new(SentidoColuna.Percentil95, 1, null);
        if (EhTempoMedio(semUnidade))
            return emSegundos ? new(SentidoColuna.TempoMedio, 1000, "segundos → ms (×1000)") : new(SentidoColuna.TempoMedio, 1, null);
        if (EhVazao(semUnidade))
            return porMinuto ? new(SentidoColuna.Vazao, 1m / 60m, "por minuto → por segundo (÷60)") : new(SentidoColuna.Vazao, 1, null);
        return null;
    }

    private static bool EhPercentil95(string h, string semUnidade) =>
        h.Contains("percentil 95", StringComparison.Ordinal) || h.Contains("percentil95", StringComparison.Ordinal)
        || semUnidade is "p95" or "95%" or "%95" or "linha 95%" or "percentil" || h.StartsWith("95% (", StringComparison.Ordinal)
        || h.StartsWith("p95 (", StringComparison.Ordinal);

    private static bool EhTempoMedio(string semUnidade) =>
        semUnidade.StartsWith("tempo medio", StringComparison.Ordinal) || semUnidade.StartsWith("tempo de resposta medio", StringComparison.Ordinal)
        || semUnidade is "media" or "average" or "tempo medio de resposta" or "resposta media";

    private static bool EhVazao(string semUnidade) =>
        (semUnidade.StartsWith("vazao", StringComparison.Ordinal) && !semUnidade.StartsWith("vazao maxima", StringComparison.Ordinal))
        || semUnidade.StartsWith("throughput", StringComparison.Ordinal) || semUnidade.StartsWith("tps", StringComparison.Ordinal)
        || semUnidade.StartsWith("taxa de processamento media", StringComparison.Ordinal)
        || semUnidade.StartsWith("consumo medio", StringComparison.Ordinal);

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex Parenteses();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Espacos();

    [GeneratedRegex(@"\(\s*(s|seg|segundos?)\s*\)|em segundos")]
    private static partial Regex EmSegundos();
}
