using System.Text;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Importacoes;

namespace Catalogo.Domain.Tests.Importacoes;

// RN98: CSV em UTF-8 (com ou sem BOM), separador ";" ou "," detectado pelo cabeçalho.
public class LeitorCsvTests
{
    [Theory]
    [InlineData("nome;sigla\nA;B", ';')]
    [InlineData("nome,sigla\nA,B", ',')]
    [InlineData("nome;valor\n\"1,5\";2", ';')]
    [InlineData("\"nome;com ponto e vírgula\",sigla,url\nA,B,C", ',')]   // separador dentro das aspas não conta
    [InlineData("nome\nA", ';')]                                            // uma coluna só: o padrão do Excel em português
    [InlineData("a,b;c\n1", ';')]                                          // empate: ponto e vírgula
    public void DetectarSeparador_PeloCabecalhoForaDasAspas(string texto, char esperado) =>
        Assert.Equal(esperado, LeitorCsv.DetectarSeparador(texto));

    [Fact]
    public void Ler_ComBom_PontoEVirgula_AspasEQuebraDeLinhaNoCampo()
    {
        var texto = "nome;descricao\r\n\"Ação\";\"linha 1\nlinha 2; com \"\"aspas\"\"\"\r\n\r\nB;\r\n";
        byte[] conteudo = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(texto)];

        var planilha = LeitorCsv.Ler(conteudo);

        Assert.Equal(["nome", "descricao"], planilha.Cabecalhos);
        Assert.Equal(2, planilha.Linhas.Count);                       // a linha em branco fica de fora
        Assert.Equal("Ação", planilha.Linhas[0].Celula(0).Texto);
        Assert.Equal("linha 1\nlinha 2; com \"aspas\"", planilha.Linhas[0].Celula(1).Texto);
        Assert.Equal(4, planilha.Linhas[1].Numero);                    // o número da linha no arquivo
        Assert.True(planilha.Linhas[1].Celula(1).EhVazia);
    }

    [Fact]
    public void Ler_SemBomComVirgula_Le()
    {
        var planilha = LeitorCsv.Ler(Encoding.UTF8.GetBytes("nome,sigla\nA,B"));

        Assert.Equal("B", planilha.Linhas[0].Celula(1).Texto);
    }

    [Fact]
    public void Ler_NaoUtf8_ErroExplicandoComoSalvar()
    {
        var latin1 = Encoding.Latin1.GetBytes("nome\nAção");

        var erro = Assert.Throws<ValidacaoRegistroException>(() => LeitorCsv.Ler(latin1));

        Assert.Contains("UTF-8", erro.Erros["arquivo"][0]);
    }

    [Theory]
    [InlineData("", "cabeçalho")]
    [InlineData("nome;sigla", "nenhuma linha")]
    public void Ler_SemCabecalhoOuSemLinhas_Erro(string texto, string trecho)
    {
        var erro = Assert.Throws<ValidacaoRegistroException>(() => LeitorCsv.Ler(Encoding.UTF8.GetBytes(texto)));

        Assert.Contains(trecho, erro.Erros["arquivo"][0]);
    }

    [Fact]
    public void Criar_MaisDe100Colunas_Erro()
    {
        var cabecalho = string.Join(";", Enumerable.Range(1, 101).Select(i => $"c{i}"));

        var erro = Assert.Throws<ValidacaoRegistroException>(() => LeitorCsv.Ler(Encoding.UTF8.GetBytes(cabecalho + "\nA")));

        Assert.Contains("101 colunas", erro.Erros["arquivo"][0]);
    }

    [Fact]
    public void Criar_MaisDe5000Linhas_ErroComONumero()
    {
        var texto = "nome\n" + string.Join("\n", Enumerable.Range(1, 5001).Select(i => $"item {i}"));

        var erro = Assert.Throws<ValidacaoRegistroException>(() => LeitorCsv.Ler(Encoding.UTF8.GetBytes(texto)));

        Assert.Contains("5.001 linhas", erro.Erros["arquivo"][0]);
    }

    [Fact]
    public void Exemplos_AteTresValoresNaoVazios()
    {
        var planilha = LeitorCsv.Ler(Encoding.UTF8.GetBytes("nome\nA\n\nB\n \nC\nD"));

        Assert.Equal(["A", "B", "C"], planilha.Exemplos(0));
    }
}
