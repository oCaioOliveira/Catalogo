using Catalogo.Application.Comum;

namespace Catalogo.Application.Tests.Comum;

// Proteção contra injeção de fórmula nas exportações (CSV e XLSX).
public class ProtecaoFormulaTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+55 11 99999-0000")]
    [InlineData("-2")]
    [InlineData("@SOMA(A1)")]
    [InlineData("\tcmd")]
    [InlineData("\rcmd")]
    public void Neutralizar_ComecoPerigoso_RecebeApostrofo(string texto)
    {
        Assert.Equal("'" + texto, ProtecaoFormula.Neutralizar(texto));
    }

    [Theory]
    [InlineData("Projeto Ágil")]
    [InlineData("1+1=2")]
    [InlineData(" =espaço antes")]
    [InlineData("")]
    [InlineData(null)]
    public void Neutralizar_TextoComum_NaoMuda(string? texto)
    {
        Assert.Equal(texto, ProtecaoFormula.Neutralizar(texto));
    }
}
