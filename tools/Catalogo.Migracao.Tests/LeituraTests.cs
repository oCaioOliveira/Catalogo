using Catalogo.Migracao.Entrada;
using Catalogo.Migracao.Sql;
using Catalogo.Migracao.Texto;

namespace Catalogo.Migracao.Tests;

public class LeitorCsvTests
{
    [Fact]
    public void Ler_CampoEntreAspasComQuebraDeLinhaEAspasDobradas_UmRegistroComOTextoIntacto()
    {
        var csv = "﻿SIGLA,Objetivo\r\nSID08,\"Validar a vazão\r\nsob carga \"\"pico\"\"\"\r\nSIAAF,simples\r\n";

        var registros = LeitorCsv.Ler(csv);

        Assert.Equal(3, registros.Count);
        Assert.Equal(["SIGLA", "Objetivo"], registros[0].Valores);           // sem o BOM
        Assert.Equal("Validar a vazão\r\nsob carga \"pico\"", registros[1].Valores[1]);
        Assert.Equal(2, registros[1].LinhaInicial);
        Assert.Equal(4, registros[2].LinhaInicial);                         // a quebra dentro das aspas conta como linha física
    }

    [Fact]
    public void Ler_AspasNaoFechadas_Falha()
    {
        Assert.Throws<FormatException>(() => LeitorCsv.Ler("A,B\n1,\"sem fim\n"));
    }

    [Fact]
    public void DeRegistros_CabecalhoDiferente_Falha()
    {
        Assert.Throws<FormatException>(() => LinhaSharePoint.DeRegistros(LeitorCsv.Ler("SIGLA,Outra\n")));
    }

    [Fact]
    public void TextoOriginal_TodasAsColunasComNomeEValorSemLimpeza()
    {
        var valores = string.Join(",", LinhaSharePoint.Colunas.Select((_, i) => i == 10 ? "\"​Objetivo com invisível\n2ª linha\"" : $"v{i}"));
        var linha = LinhaSharePoint.DeRegistros(LeitorCsv.Ler(string.Join(",", LinhaSharePoint.Colunas) + "\n" + valores + "\n"))[0];

        var texto = linha.TextoOriginal();

        Assert.StartsWith("SIGLA: v0\n\nBateria de testes: v1", texto, StringComparison.Ordinal);
        Assert.Contains("Objetivo: ​Objetivo com invisível\n2ª linha\n\nApontamentos: v11", texto, StringComparison.Ordinal);
        Assert.EndsWith("Link: v19", texto, StringComparison.Ordinal);
    }
}

public class ConversorHtmlTests
{
    [Fact]
    public void ParaTexto_ParagrafosQuebrasEEntidades()
    {
        var html = "<div><p>Linha&#160;1 com &quot;aspas&quot; e dois&#58; pontos</p><p>Linha<br>2</p></div>";

        Assert.Equal("Linha 1 com \"aspas\" e dois: pontos\nLinha\n2", ConversorHtml.ParaTexto(html));
    }

    [Fact]
    public void ParaTexto_ListasNumeradasEComMarcadorAninhadas()
    {
        var html = "<ul><li>Primeiro<ol><li>Um</li><li>Dois</li></ol></li><li>Segundo</li></ul>";

        Assert.Equal("- Primeiro\n  1. Um\n  2. Dois\n- Segundo", ConversorHtml.ParaTexto(html));
    }

    [Fact]
    public void ParaTexto_ItemComParagrafoDentro_TextoNaLinhaDoMarcador()
    {
        // Formato do SharePoint: <li><div>texto</div></li>. O "-" não pode ficar sozinho numa linha.
        var html = "<ul><li><div>Foram realizadas validações</div></li><li><p>Não foram disponibilizadas</p><p>credenciais</p></li><li></li></ul>";

        Assert.Equal("- Foram realizadas validações\n- Não foram disponibilizadas\ncredenciais\n-", ConversorHtml.ParaTexto(html));
    }

    [Fact]
    public void ParaTexto_ItemQueComecaComOutraLista_ListaNaLinhaSeguinte()
    {
        Assert.Equal("-\n  1. Um", ConversorHtml.ParaTexto("<ul><li><ol><li>Um</li></ol></li></ul>"));
    }

    [Fact]
    public void ParaTexto_TabelaViraLinhasComColunasSeparadasPorBarra()
    {
        var html = "<p>Antes</p><table><tr><th>Cenário</th><th>Erros %</th></tr><tr><td>1.</td><td>0,00</td></tr></table><p>Depois</p>";

        Assert.Equal("Antes\nCenário | Erros %\n1. | 0,00\nDepois", ConversorHtml.ParaTexto(html));
    }

    [Fact]
    public void ParaTexto_ImagemELinkEEstilosRemovidos()
    {
        // Casos reais do CSV: imagem sem src (perdida na exportação) e estilos com &#58; nos atributos.
        var html = "<span style=\"color&#58;rgb(0, 0, 0);\">Ver <a href=\"https://devops.caixa/x\">script</a> e " +
                   "<a href=\"https://a.b/c\">https://a.b/c</a></span><img alt=\"\" style=\"margin&#58;5px;\" /><img src=\"/fig.png\" alt=\"Gráfico\">" +
                   "<style>p{color:red}</style>";

        Assert.Equal("Ver script (https://devops.caixa/x) e https://a.b/c [imagem: sem endereço no SharePoint] [imagem: /fig.png — Gráfico]",
            ConversorHtml.ParaTexto(html));
    }

    [Fact]
    public void ParaTexto_TextoQueNaoEhHtml_SoLimpa()
    {
        Assert.Equal("Validar a vazão sob carga", ConversorHtml.ParaTexto("​​Validar  a vazão sob carga  "));
    }

    [Fact]
    public void Tabelas_RowspanEColspanSaoPropagados()
    {
        var html = """
            <table>
              <tr><th rowspan="2">Cenário</th><th colspan="2">Vazão média total</th></tr>
              <tr><th>A</th><th>B</th></tr>
              <tr><td>1.</td><td rowspan="2">176,5<br>178</td><td>x</td></tr>
              <tr><td>2.</td><td>y</td></tr>
            </table>
            """;

        var grade = Assert.Single(ConversorHtml.Tabelas(html));

        Assert.Equal(["Cenário", "Vazão média total", "Vazão média total"], grade.Linhas[0]);
        Assert.Equal(["Cenário", "A", "B"], grade.Linhas[1]);
        Assert.Equal(["1.", "176,5\n178", "x"], grade.Linhas[2]);
        Assert.Equal(["2.", "176,5\n178", "y"], grade.Linhas[3]);
    }
}

public class LimpezaTextoTests
{
    [Fact]
    public void NormalizarMultilinha_InvisiveisEspacosELinhasEmBranco()
    {
        Assert.Equal("a b\n\nc", LimpezaTexto.NormalizarMultilinha("​a  b \r\n\r\n\r\n­c\n\n"));
    }

    [Theory]
    [InlineData(" DEPÓSITOS E CAPTAÇÃO", "DEPÓSITOS E CAPTAÇÃO")]
    [InlineData("\t Box Dados, CRM e Marketing Digital", "box dados,  crm e marketing digital")]
    [InlineData("Credito  Comercial", "CRÉDITO COMERCIAL")]
    public void ChaveComparacao_IgnoraMaiusculasAcentosEEspacos(string a, string b)
    {
        Assert.Equal(LimpezaTexto.ChaveComparacao(a), LimpezaTexto.ChaveComparacao(b));
    }
}

public class LiteralSqlTests
{
    [Fact]
    public void Texto_AspasDobradasEQuebrasDentroDoLiteral()
    {
        Assert.Equal("N'd''água\nlinha 2'", LiteralSql.Texto("d'água\r\nlinha 2"));
    }

    [Theory]
    [InlineData("antes\nGO\ndepois")]
    [InlineData("antes\n  go 5\ndepois")]
    [InlineData("antes\n:r arquivo.sql")]
    [InlineData("antes\n!!dir")]
    public void Texto_LinhaQueOSqlcmdInterpretaria_NuncaComecaUmaLinhaFisica(string texto)
    {
        var literal = LiteralSql.Texto(texto);

        Assert.StartsWith("(CAST(N'", literal, StringComparison.Ordinal);       // nvarchar(max): sem corte em 4000
        Assert.DoesNotContain(literal.Split('\n').Skip(1), l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^\s*(go\b|:|!!)", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    [Fact]
    public void Texto_VariavelDoSqlcmd_ESeparada()
    {
        var literal = LiteralSql.Texto("echo $(HOME)");

        Assert.DoesNotContain("$(", literal, StringComparison.Ordinal);
        Assert.Equal("(CAST(N'echo $' AS nvarchar(max)) + N'(HOME)')", literal);
    }

    [Fact]
    public void Valores_DataIsoNumeroInvarianteEBinario()
    {
        Assert.Equal("'2024-03-10T17:30:00.0000000'", LiteralSql.DataHora(new DateTime(2024, 3, 10, 17, 30, 0, DateTimeKind.Utc)));
        Assert.Equal("1234.5", LiteralSql.Numero(1234.5m));
        Assert.Equal("0x0AFF", LiteralSql.Binario([0x0A, 0xFF]));
        Assert.Equal("NULL", LiteralSql.Texto(null));
    }
}
