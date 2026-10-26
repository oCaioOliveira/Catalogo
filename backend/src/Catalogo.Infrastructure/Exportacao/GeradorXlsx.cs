using Catalogo.Application.Abstracoes;
using ClosedXML.Excel;

namespace Catalogo.Infrastructure.Exportacao;

/// <summary>
/// XLSX com o ClosedXML (licença MIT): cabeçalho em negrito e congelado, filtro automático, números como
/// número e datas como data (com formato dd/mm/aaaa), para o usuário ordenar e somar no Excel. URL vira hiperlink (ADR 0042).
/// O ClosedXML monta a pasta de trabalho na memória: até o limite da exportação (10.000 linhas) isso é pouco.
/// </summary>
internal sealed class GeradorXlsx : IGeradorArquivoExportacao
{
    private const string FormatoData = "dd/mm/yyyy";
    private const string FormatoDataHora = "dd/mm/yyyy hh:mm:ss";

    public string Formato => "xlsx";
    public string TipoConteudo => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task EscreverAsync(PlanilhaExportacao planilha, Stream destino, CancellationToken cancellationToken)
    {
        using var pasta = new XLWorkbook();
        var aba = pasta.Worksheets.Add(NomeDaAba(planilha.NomePlanilha));

        for (var coluna = 0; coluna < planilha.Colunas.Count; coluna++)
        {
            aba.Cell(1, coluna + 1).Value = planilha.Colunas[coluna];
            // Largura pelo título (AdjustToContents mede fontes e é lento em 10.000 linhas).
            aba.Column(coluna + 1).Width = Math.Clamp(planilha.Colunas[coluna].Length + 4, 12, 50);
        }

        var linha = 1;
        await foreach (var valores in planilha.Linhas.WithCancellation(cancellationToken))
        {
            linha++;
            for (var coluna = 0; coluna < valores.Count; coluna++)
                Preencher(aba.Cell(linha, coluna + 1), valores[coluna]);
        }

        var cabecalho = aba.Range(1, 1, 1, planilha.Colunas.Count);
        cabecalho.Style.Font.Bold = true;
        aba.Range(1, 1, linha, planilha.Colunas.Count).SetAutoFilter();
        aba.SheetView.FreezeRows(1);

        // SaveAs escreve de forma síncrona: grava na memória e copia para a resposta de forma assíncrona.
        using var memoria = new MemoryStream();
        pasta.SaveAs(memoria);
        memoria.Position = 0;
        await memoria.CopyToAsync(destino, cancellationToken);
    }

    private static void Preencher(IXLCell celula, object? valor)
    {
        switch (valor)
        {
            case null:
                return;
            case decimal numero:
                celula.Value = (double)numero;
                break;
            case DateOnly data:
                celula.Value = data.ToDateTime(TimeOnly.MinValue);
                celula.Style.DateFormat.Format = FormatoData;
                break;
            case DateTime dataHora:
                celula.Value = dataHora;
                celula.Style.DateFormat.Format = FormatoDataHora;
                break;
            case Uri endereco:
                // Só http/https (validado na gravação, RN04): o texto da célula é a URL, e o clique abre o endereço.
                celula.Value = endereco.OriginalString;
                celula.SetHyperlink(new XLHyperlink(endereco));
                break;
            default:
                // Texto é gravado como texto (nunca como fórmula), e já vem com a proteção do apóstrofo. O ClosedXML
                // REMOVE um apóstrofo inicial (vira o "quote prefix" do Excel): dobrado, o apóstrofo continua no valor,
                // igual ao CSV. Vale também para um texto que já começava com apóstrofo.
                var texto = valor.ToString()!;
                celula.Value = texto.StartsWith('\'') ? "'" + texto : texto;
                break;
        }
    }

    /// <summary>O Excel aceita até 31 caracteres no nome da aba, sem : \ / ? * [ ].</summary>
    private static string NomeDaAba(string nome)
    {
        var limpo = new string(nome.Where(c => c is not (':' or '\\' or '/' or '?' or '*' or '[' or ']')).ToArray()).Trim();
        return limpo.Length == 0 ? "Exportação" : limpo[..Math.Min(limpo.Length, 31)];
    }
}
