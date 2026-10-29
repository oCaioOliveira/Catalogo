namespace Catalogo.Application.Abstracoes;

/// <summary>
/// Escreve uma planilha num formato de arquivo (CSV, XLSX). Implementado na Infrastructure (o XLSX usa o
/// ClosedXML, que é pacote e por isso não pode ficar na Application). Um por formato.
/// </summary>
public interface IGeradorArquivoExportacao
{
    /// <summary>Valor do parâmetro "formato" e extensão do arquivo, em minúsculas (ex.: "csv").</summary>
    string Formato { get; }

    /// <summary>Content-Type da resposta.</summary>
    string TipoConteudo { get; }

    /// <summary>
    /// Escreve o cabeçalho e as linhas, conforme chegam (as linhas vêm do banco em lotes). Os textos já chegam
    /// protegidos contra injeção de fórmula (<see cref="Comum.ProtecaoFormula"/>).
    /// </summary>
    Task EscreverAsync(PlanilhaExportacao planilha, Stream destino, CancellationToken cancellationToken);
}

/// <summary>
/// Os dados a exportar: os títulos das colunas e as linhas, com um valor por coluna, na mesma ordem. Cada valor é
/// <see cref="string"/>, <see cref="decimal"/>, <see cref="DateOnly"/> (data), <see cref="DateTime"/> (data e hora,
/// já no fuso da exportação, sem Kind) ou nulo (célula vazia). O gerador formata pelo tipo do valor.
/// </summary>
public sealed record PlanilhaExportacao(
    string NomePlanilha, IReadOnlyList<string> Colunas, IAsyncEnumerable<IReadOnlyList<object?>> Linhas);
