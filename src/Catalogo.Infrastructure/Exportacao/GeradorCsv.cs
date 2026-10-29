using System.Globalization;
using System.Text;
using Catalogo.Application.Abstracoes;

namespace Catalogo.Infrastructure.Exportacao;

/// <summary>
/// CSV para abrir direto no Excel em português: UTF-8 COM BOM (sem ele o Excel lê os acentos errado),
/// separador ";", vírgula decimal, datas dd/MM/aaaa e linhas terminadas em CRLF. Escreve linha a linha,
/// conforme os lotes chegam do banco: o arquivo não fica inteiro na memória.
/// </summary>
internal sealed class GeradorCsv : IGeradorArquivoExportacao
{
    private const char Separador = ';';
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly char[] ExigemAspas = [Separador, '"', '\r', '\n'];

    public string Formato => "csv";
    public string TipoConteudo => "text/csv; charset=utf-8";

    public async Task EscreverAsync(PlanilhaExportacao planilha, Stream destino, CancellationToken cancellationToken)
    {
        // UTF8Encoding(true) escreve o BOM. leaveOpen: o stream é da resposta HTTP.
        await using var escritor = new StreamWriter(destino, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 64 * 1024, leaveOpen: true)
        {
            NewLine = "\r\n",
        };

        await escritor.WriteLineAsync(Linha(planilha.Colunas));
        await foreach (var linha in planilha.Linhas.WithCancellation(cancellationToken))
            await escritor.WriteLineAsync(Linha(linha.Select(Formatar)));

        await escritor.FlushAsync(cancellationToken);
    }

    private static string Linha(IEnumerable<string?> valores) => string.Join(Separador, valores.Select(Escapar));

    private static string? Formatar(object? valor) => valor switch
    {
        null => null,
        string texto => texto,
        decimal numero => numero.ToString(Cultura),
        DateOnly data => data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        DateTime dataHora => dataHora.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
        Uri endereco => endereco.OriginalString,   // URL como foi digitada (Uri.ToString() decodifica os %XX)
        _ => Convert.ToString(valor, Cultura),
    };

    /// <summary>RFC 4180: entre aspas se tiver separador, aspas ou quebra de linha; aspas internas dobradas.</summary>
    private static string Escapar(string? valor) =>
        valor is null ? "" : valor.IndexOfAny(ExigemAspas) >= 0 ? $"\"{valor.Replace("\"", "\"\"")}\"" : valor;
}
