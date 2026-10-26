using System.Globalization;
using System.Text;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Validacao;

namespace Catalogo.Domain.Importacoes;

/// <summary>
/// Uma célula lida do arquivo. O CSV só tem <see cref="Texto"/>; o XLSX traz o VALOR da célula com o tipo dela (número, data nativa
/// do Excel ou verdadeiro/falso), nunca a fórmula. Uma célula sem nada (ou só com espaços) é "não informado".
/// </summary>
public readonly record struct CelulaImportada(string? Texto, decimal? Numero = null, DateTime? Data = null, bool? Booleano = null)
{
    public static CelulaImportada Vazia { get; } = new(null);

    public static CelulaImportada DeTexto(string? texto) => new(texto);

    public bool EhVazia => string.IsNullOrWhiteSpace(Texto) && Numero is null && Data is null && Booleano is null;

    /// <summary>O valor como o usuário vê, para a prévia, os exemplos da etapa "Colunas" e o relatório.</summary>
    public string ParaTexto()
    {
        if (Data is DateTime data)
        {
            return data.TimeOfDay == TimeSpan.Zero
                ? data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : data.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        if (Numero is decimal numero)
            return numero.ToString("0.############################", CultureInfo.GetCultureInfo("pt-BR"));
        if (Booleano is bool booleano)
            return booleano ? "Sim" : "Não";
        return Texto?.Trim() ?? "";
    }
}

/// <param name="Numero">Número da linha na planilha: o cabeçalho é a 1, a primeira linha de dados é a 2.</param>
public sealed record LinhaImportada(int Numero, IReadOnlyList<CelulaImportada> Celulas)
{
    public CelulaImportada Celula(int indice) => indice < Celulas.Count ? Celulas[indice] : CelulaImportada.Vazia;
}

/// <summary>O arquivo lido: os cabeçalhos (primeira linha) e as linhas de dados não vazias.</summary>
public sealed record PlanilhaImportada(IReadOnlyList<string> Cabecalhos, IReadOnlyList<LinhaImportada> Linhas)
{
    private static readonly System.Globalization.CultureInfo Cultura = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Confere os limites de linhas e colunas e se há cabeçalho (RN98). As linhas totalmente vazias já ficaram de fora.
    /// Lança <see cref="ValidacaoRegistroException"/> com a chave "arquivo".
    /// </summary>
    public static PlanilhaImportada Criar(IReadOnlyList<string> cabecalhos, IReadOnlyList<LinhaImportada> linhas)
    {
        var erros = new ErrosPorCampo();
        if (cabecalhos.Count == 0 || cabecalhos.All(string.IsNullOrWhiteSpace))
            erros.Adicionar(ChaveErro, "O arquivo não tem cabeçalho: a primeira linha precisa ter o nome das colunas.");
        else if (cabecalhos.Count > LimitesImportacao.MaximoColunas)
            erros.Adicionar(ChaveErro, $"O arquivo tem {cabecalhos.Count} colunas; o máximo é {LimitesImportacao.MaximoColunas}.");
        else if (linhas.Count == 0)
            erros.Adicionar(ChaveErro, "O arquivo não tem nenhuma linha de dados abaixo do cabeçalho.");
        else if (linhas.Count > LimitesImportacao.MaximoLinhas)
            erros.Adicionar(ChaveErro,
                $"O arquivo tem {linhas.Count.ToString("N0", Cultura)} linhas de dados; o máximo é {LimitesImportacao.MaximoLinhas.ToString("N0", Cultura)} por importação. Divida o arquivo.");

        erros.LancarSeHouverErros();
        return new PlanilhaImportada(cabecalhos.Select(c => c?.Trim() ?? "").ToList(), linhas);
    }

    /// <summary>Chave dos erros do arquivo (limites, formato, tipo real).</summary>
    public const string ChaveErro = "arquivo";

    /// <summary>Até <see cref="LimitesImportacao.ExemplosPorColuna"/> valores não vazios de uma coluna, para a etapa "Colunas".</summary>
    public IReadOnlyList<string> Exemplos(int coluna) => Linhas
        .Select(l => l.Celula(coluna))
        .Where(c => !c.EhVazia)
        .Take(LimitesImportacao.ExemplosPorColuna)
        .Select(c => c.ParaTexto())
        .ToList();
}

/// <summary>
/// Lê um CSV em UTF-8 (com ou sem BOM), com o separador ";" ou "," detectado pela linha de cabeçalho (RN98). Aspas como no Excel:
/// um campo entre aspas pode ter o separador, quebras de linha e aspas dobradas ("").
/// </summary>
public static class LeitorCsv
{
    public static PlanilhaImportada Ler(byte[] conteudo)
    {
        string texto;
        try
        {
            texto = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(conteudo);
        }
        catch (DecoderFallbackException)
        {
            var erros = new ErrosPorCampo();
            erros.Adicionar(PlanilhaImportada.ChaveErro,
                "O CSV não está em UTF-8. No Excel, use \"Salvar como\" → \"CSV UTF-8 (separado por vírgulas)\", ou envie o .xlsx.");
            erros.LancarSeHouverErros();
            throw;
        }

        if (texto.Length > 0 && texto[0] == '﻿')
            texto = texto[1..];

        var separador = DetectarSeparador(texto);
        var registros = Separar(texto, separador);
        if (registros.Count == 0)
            return PlanilhaImportada.Criar([], []);

        var linhas = new List<LinhaImportada>();
        for (var i = 1; i < registros.Count; i++)
        {
            var celulas = registros[i].Select(CelulaImportada.DeTexto).ToList();
            if (celulas.All(c => c.EhVazia))
                continue;
            linhas.Add(new LinhaImportada(i + 1, celulas));
        }

        return PlanilhaImportada.Criar(registros[0], linhas);
    }

    /// <summary>
    /// O separador da primeira linha (o cabeçalho), contando fora das aspas: ";" (o padrão do Excel em português) se aparecer pelo
    /// menos tantas vezes quanto ",". Um cabeçalho de uma coluna só fica com ";".
    /// </summary>
    public static char DetectarSeparador(string texto)
    {
        int pontoEVirgula = 0, virgula = 0;
        var entreAspas = false;
        foreach (var caractere in texto)
        {
            if (caractere == '"')
                entreAspas = !entreAspas;
            else if (!entreAspas && caractere is '\n' or '\r')
                break;
            else if (!entreAspas && caractere == ';')
                pontoEVirgula++;
            else if (!entreAspas && caractere == ',')
                virgula++;
        }

        return virgula > pontoEVirgula ? ',' : ';';
    }

    /// <summary>Os registros do CSV (cada um, a lista de campos), respeitando as aspas.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Separar(string texto, char separador)
    {
        var registros = new List<IReadOnlyList<string>>();
        var atual = new List<string>();
        var campo = new StringBuilder();
        var entreAspas = false;

        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (entreAspas)
            {
                if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"')
                {
                    campo.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    entreAspas = false;
                }
                else
                {
                    campo.Append(c);
                }
            }
            else if (c == '"' && campo.Length == 0)
            {
                entreAspas = true;
            }
            else if (c == separador)
            {
                atual.Add(campo.ToString());
                campo.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n')
                    i++;
                atual.Add(campo.ToString());
                campo.Clear();
                registros.Add(atual);
                atual = [];
            }
            else
            {
                campo.Append(c);
            }
        }

        if (campo.Length > 0 || atual.Count > 0)
        {
            atual.Add(campo.ToString());
            registros.Add(atual);
        }

        return registros;
    }
}
