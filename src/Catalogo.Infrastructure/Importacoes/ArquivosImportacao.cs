using System.IO.Compression;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.ChavesUnicas;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Validacao;
using ClosedXML.Excel;

namespace Catalogo.Infrastructure.Importacoes;

/// <summary>
/// Lê e escreve as planilhas da importação com o ClosedXML (licença MIT, já usado na exportação). Só arquivo: as regras de conteúdo
/// (conversão, referências, unicidade) ficam no Domain e na Application.
///
/// Segurança (RN98): o tipo é conferido pelo CONTEÚDO (assinatura do ZIP e as partes do Excel; texto UTF-8 sem bytes nulos), e antes de
/// abrir um XLSX a soma dos tamanhos descompactados das partes é limitada (<see cref="LimitesImportacao.TamanhoDescompactadoMaximo"/>),
/// contra arquivos compactados maliciosos ("zip bomb"). As células são lidas pelo VALOR (o último resultado calculado), nunca pela fórmula.
/// </summary>
internal sealed class ArquivosImportacao : IArquivosImportacao
{
    private static readonly System.Globalization.CultureInfo Cultura = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    private const int LinhasComFormato = LimitesImportacao.MaximoLinhas + 1;
    private const int MaximoPartesXlsx = 10_000;
    private const string AbaListas = "Listas";

    public FormatoImportacao DetectarFormato(byte[] conteudo, string nomeArquivo)
    {
        var extensao = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        if (extensao is not (".xlsx" or ".csv"))
            Falhar("Envie uma planilha .xlsx (Excel) ou um arquivo .csv.");

        if (conteudo.Length >= 4 && conteudo[0] == 0xD0 && conteudo[1] == 0xCF && conteudo[2] == 0x11 && conteudo[3] == 0xE0)
            Falhar("O arquivo está no formato antigo do Excel (.xls) ou protegido por senha. No Excel, use \"Salvar como\" → \"Pasta de Trabalho do Excel (.xlsx)\", sem senha.");

        var pareceZip = conteudo.Length >= 4 && conteudo[0] == 'P' && conteudo[1] == 'K' && conteudo[2] == 3 && conteudo[3] == 4;
        if (pareceZip)
        {
            ConferirPacoteXlsx(conteudo);
            if (extensao == ".csv")
                Falhar("O arquivo tem a extensão .csv, mas é uma planilha do Excel. Renomeie para .xlsx ou salve como CSV.");
            return FormatoImportacao.Xlsx;
        }

        if (extensao == ".xlsx")
            Falhar("O arquivo tem a extensão .xlsx, mas não é uma planilha do Excel válida.");
        if (conteudo.AsSpan(0, Math.Min(conteudo.Length, 64 * 1024)).Contains((byte)0))
            Falhar("O arquivo .csv não é um texto: confira se é mesmo um CSV exportado de uma planilha.");
        return FormatoImportacao.Csv;
    }

    public PlanilhaImportada Ler(byte[] conteudo, FormatoImportacao formato)
    {
        if (formato == FormatoImportacao.Csv)
            return LeitorCsv.Ler(conteudo);

        ConferirPacoteXlsx(conteudo);
        XLWorkbook pasta;
        try
        {
            pasta = new XLWorkbook(new MemoryStream(conteudo, writable: false));
        }
        catch (Exception erro) when (erro is not ValidacaoRegistroException)
        {
            Falhar("Não foi possível ler a planilha: o arquivo está corrompido ou protegido por senha.");
            throw;
        }

        using (pasta)
        {
            var aba = pasta.Worksheets.FirstOrDefault(a => a.Visibility == XLWorksheetVisibility.Visible
                                                           && CalculadoraChaveUnica.NormalizarTexto(a.Name) != CalculadoraChaveUnica.NormalizarTexto(LimitesImportacao.AbaInstrucoes));
            if (aba is null)
                Falhar("A planilha não tem uma aba com dados (a aba \"Instruções\" é ignorada).");

            var ultimaColuna = aba!.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
            var cabecalhos = Enumerable.Range(1, ultimaColuna).Select(c => aba.Cell(1, c).GetFormattedString().Trim()).ToList();
            if (cabecalhos.Count > LimitesImportacao.MaximoColunas)
                return PlanilhaImportada.Criar(cabecalhos, []);

            var linhas = new List<LinhaImportada>();
            var ultimaLinha = aba.LastRowUsed()?.RowNumber() ?? 1;
            for (var numero = 2; numero <= ultimaLinha; numero++)
            {
                var celulas = Enumerable.Range(1, ultimaColuna).Select(c => Celula(aba.Cell(numero, c))).ToList();
                if (celulas.All(c => c.EhVazia))
                    continue;
                linhas.Add(new LinhaImportada(numero, celulas));
                if (linhas.Count > LimitesImportacao.MaximoLinhas)
                {
                    // Não precisa ler o resto para dizer que passou do limite.
                    Falhar($"O arquivo tem mais de {LimitesImportacao.MaximoLinhas.ToString("N0", Cultura)} linhas de dados; o máximo é {LimitesImportacao.MaximoLinhas.ToString("N0", Cultura)} por importação. Divida o arquivo.");
                }
            }

            return PlanilhaImportada.Criar(cabecalhos, linhas);
        }
    }

    public byte[] GerarModelo(ModeloPlanilha modelo)
    {
        using var pasta = new XLWorkbook();
        var dados = pasta.Worksheets.Add(NomeDaAba(modelo.NomeEntidade, "Dados"));
        var instrucoes = pasta.Worksheets.Add(LimitesImportacao.AbaInstrucoes);
        IXLWorksheet? listas = null;

        for (var i = 0; i < modelo.Colunas.Count; i++)
        {
            var coluna = modelo.Colunas[i];
            var numero = i + 1;
            var cabecalho = dados.Cell(1, numero);
            cabecalho.Value = coluna.Cabecalho;
            cabecalho.Style.NumberFormat.Format = "@";
            cabecalho.Style.Font.Bold = true;
            if (coluna.Obrigatorio)
                cabecalho.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF2CC");
            dados.Column(numero).Width = Math.Clamp(coluna.Cabecalho.Length + 6, 14, 45);

            // O formato vai no ESTILO DA COLUNA (e não em 5.000 células): o arquivo fica pequeno e vale para qualquer linha.
            FormatarColuna(dados.Column(numero).Style, coluna);
            var celulas = dados.Range(2, numero, LinhasComFormato, numero);

            if (coluna.Lista is { Count: > 0 } valores)
            {
                listas ??= pasta.Worksheets.Add(AbaListas);
                var colunaLista = listas.LastColumnUsed()?.ColumnNumber() + 1 ?? 1;
                for (var v = 0; v < valores.Count; v++)
                    listas.Cell(v + 1, colunaLista).Value = valores[v];

                var validacao = celulas.CreateDataValidation();
                validacao.List(listas.Range(1, colunaLista, valores.Count, colunaLista), true);
                validacao.IgnoreBlanks = true;
                validacao.ErrorStyle = coluna.ListaRestrita ? XLErrorStyle.Stop : XLErrorStyle.Warning;
                validacao.ErrorTitle = coluna.Cabecalho;
                validacao.ErrorMessage = coluna.ListaRestrita
                    ? "Escolha um valor da lista."
                    : "Este nome não está na lista de itens que você vê. Se ele foi criado depois de baixar o modelo, pode manter.";
            }
        }

        dados.SheetView.FreezeRows(1);
        listas?.Hide();
        EscreverInstrucoes(instrucoes, modelo);
        return Salvar(pasta);
    }

    public byte[] GerarRelatorio(PlanilhaImportada planilha, IReadOnlyDictionary<int, (string Resultado, string Mensagens)> resultados)
    {
        using var pasta = new XLWorkbook();
        var aba = pasta.Worksheets.Add("Relatório");
        var colunas = planilha.Cabecalhos.Count;

        for (var c = 0; c < colunas; c++)
            aba.Cell(1, c + 1).Value = planilha.Cabecalhos[c];
        aba.Cell(1, colunas + 1).Value = "Resultado";
        aba.Cell(1, colunas + 2).Value = "Mensagens";

        var linha = 1;
        foreach (var original in planilha.Linhas)
        {
            linha++;
            for (var c = 0; c < colunas; c++)
                Escrever(aba.Cell(linha, c + 1), original.Celula(c));

            var (resultado, mensagens) = resultados.GetValueOrDefault(original.Numero, ("", ""));
            aba.Cell(linha, colunas + 1).Value = resultado;
            aba.Cell(linha, colunas + 2).Value = mensagens;
            aba.Cell(linha, colunas + 2).Style.Alignment.WrapText = true;
            if (mensagens.Length > 0)
                aba.Cell(linha, colunas + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#FDE2E1");
        }

        var cabecalho = aba.Range(1, 1, 1, colunas + 2);
        cabecalho.Style.Font.Bold = true;
        aba.Range(1, 1, linha, colunas + 2).SetAutoFilter();
        aba.SheetView.FreezeRows(1);
        for (var c = 1; c <= colunas; c++)
            aba.Column(c).Width = Math.Clamp(aba.Cell(1, c).GetString().Length + 6, 12, 40);
        aba.Column(colunas + 1).Width = 16;
        aba.Column(colunas + 2).Width = 80;
        return Salvar(pasta);
    }

    private static void FormatarColuna(IXLStyle estilo, ColunaModelo coluna)
    {
        switch (coluna.Tipo)
        {
            case TipoDado.Data:
                estilo.DateFormat.Format = "dd/mm/yyyy";
                break;
            case TipoDado.DataHora:
                estilo.DateFormat.Format = "dd/mm/yyyy hh:mm";
                break;
            case TipoDado.Inteiro:
                estilo.NumberFormat.Format = "0";
                break;
            case TipoDado.Decimal:
                estilo.NumberFormat.Format = coluna.Escala is > 0 ? "0." + new string('0', coluna.Escala.Value) : "0";
                break;
            case TipoDado.Texto or TipoDado.TextoLongo or TipoDado.Url or TipoDado.Referencia:
                // Texto: um código "007" não vira o número 7.
                estilo.NumberFormat.Format = "@";
                break;
        }
    }

    private static void EscreverInstrucoes(IXLWorksheet aba, ModeloPlanilha modelo)
    {
        aba.Cell(1, 1).Value = $"Como preencher a planilha de {modelo.NomeEntidade}";
        aba.Cell(1, 1).Style.Font.Bold = true;
        aba.Cell(1, 1).Style.Font.FontSize = 14;

        var linha = 3;
        foreach (var regra in modelo.Regras)
        {
            aba.Cell(linha, 1).Value = "• " + regra;
            aba.Range(linha, 1, linha, 4).Merge().Style.Alignment.WrapText = true;
            aba.Row(linha).Height = 30;
            linha++;
        }

        linha++;
        var titulos = new[] { "Coluna", "Tipo", "Obrigatório", "Como preencher" };
        for (var t = 0; t < titulos.Length; t++)
        {
            aba.Cell(linha, t + 1).Value = titulos[t];
            aba.Cell(linha, t + 1).Style.Font.Bold = true;
            aba.Cell(linha, t + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DDEBF7");
        }

        foreach (var coluna in modelo.Colunas)
        {
            linha++;
            aba.Cell(linha, 1).Value = coluna.Cabecalho;
            aba.Cell(linha, 2).Value = coluna.DescricaoTipo;
            aba.Cell(linha, 3).Value = coluna.Obrigatorio ? "Sim" : "Não";
            aba.Cell(linha, 4).Value = coluna.ComoPreencher;
            aba.Cell(linha, 4).Style.Alignment.WrapText = true;
        }

        aba.Column(1).Width = 32;
        aba.Column(2).Width = 22;
        aba.Column(3).Width = 13;
        aba.Column(4).Width = 100;
        aba.Range(1, 1, linha, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
    }

    private static CelulaImportada Celula(IXLCell celula)
    {
        var valor = celula.Value;
        return valor.Type switch
        {
            XLDataType.Blank => CelulaImportada.Vazia,
            XLDataType.Boolean => new CelulaImportada(null, Booleano: valor.GetBoolean()),
            XLDataType.Number => ComoNumero(valor.GetNumber()),
            XLDataType.DateTime => new CelulaImportada(null, Data: valor.GetDateTime()),
            XLDataType.TimeSpan => CelulaImportada.DeTexto(celula.GetFormattedString()),
            XLDataType.Error => CelulaImportada.DeTexto(celula.GetFormattedString()),
            _ => CelulaImportada.DeTexto(valor.GetText()),
        };
    }

    private static CelulaImportada ComoNumero(double numero) =>
        numero is > (double)decimal.MaxValue or < (double)decimal.MinValue || double.IsNaN(numero)
            ? CelulaImportada.DeTexto(numero.ToString(System.Globalization.CultureInfo.InvariantCulture))
            : new CelulaImportada(null, Numero: (decimal)numero);

    private static void Escrever(IXLCell destino, CelulaImportada celula)
    {
        if (celula.Data is DateTime data)
        {
            destino.Value = data;
            destino.Style.DateFormat.Format = data.TimeOfDay == TimeSpan.Zero ? "dd/mm/yyyy" : "dd/mm/yyyy hh:mm";
        }
        else if (celula.Numero is decimal numero)
        {
            destino.Value = (double)numero;
        }
        else if (celula.Booleano is bool booleano)
        {
            destino.Value = booleano;
        }
        else if (celula.Texto is string texto)
        {
            // Como na exportação: o ClosedXML remove um apóstrofo inicial, então ele é dobrado para o valor ficar igual.
            destino.Value = texto.StartsWith('\'') ? "'" + texto : texto;
        }
    }

    /// <summary>Confere que é um pacote do Excel e que, descompactado, não passa do limite (proteção contra "zip bomb").</summary>
    private static void ConferirPacoteXlsx(byte[] conteudo)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(conteudo, writable: false), ZipArchiveMode.Read);
            if (zip.Entries.Count > MaximoPartesXlsx)
                Falhar("A planilha tem partes demais para ser lida com segurança.");
            if (zip.GetEntry("[Content_Types].xml") is null || zip.GetEntry("xl/workbook.xml") is null)
                Falhar("O arquivo é um ZIP, mas não é uma planilha do Excel (.xlsx).");

            long total = 0;
            foreach (var parte in zip.Entries)
            {
                total += parte.Length;
                if (total > LimitesImportacao.TamanhoDescompactadoMaximo)
                    Falhar($"A planilha descompactada passa de {LimitesImportacao.TamanhoDescompactadoMaximo / (1024 * 1024)} MB: ela não pode ser lida com segurança.");
            }
        }
        catch (InvalidDataException)
        {
            Falhar("O arquivo .xlsx está corrompido.");
        }
    }

    private static byte[] Salvar(XLWorkbook pasta)
    {
        using var memoria = new MemoryStream();
        pasta.SaveAs(memoria);
        return memoria.ToArray();
    }

    /// <summary>O Excel aceita até 31 caracteres no nome da aba, sem : \ / ? * [ ].</summary>
    private static string NomeDaAba(string nome, string padrao)
    {
        var limpo = new string(nome.Where(c => c is not (':' or '\\' or '/' or '?' or '*' or '[' or ']')).ToArray()).Trim();
        return limpo.Length == 0 ? padrao : limpo[..Math.Min(limpo.Length, 31)];
    }

    private static void Falhar(string mensagem)
    {
        var erros = new ErrosPorCampo();
        erros.Adicionar(PlanilhaImportada.ChaveErro, mensagem);
        erros.LancarSeHouverErros();
    }
}
