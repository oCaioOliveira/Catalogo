using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Tests.Validacao.Tipos;

// RN04 — INTEIRO
public class InteiroValidadorTests
{
    private readonly InteiroValidador _validador = new();
    private readonly Campo _campo = Metadado.Campo("quantidade", TipoDado.Inteiro);

    [Theory]
    [InlineData("0", 0)]
    [InlineData("42", 42)]
    [InlineData("-7", -7)]
    [InlineData("2147483647", int.MaxValue)]
    [InlineData("-2147483648", int.MinValue)]
    public void Validar_InteiroDe32Bits_EhValido(string json, int esperado)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json(json));

        Assert.True(resultado.Valido);
        Assert.Equal(esperado, resultado.ValorNormalizado!.GetValue<int>());
    }

    [Theory]
    [InlineData("2147483648")]   // int.MaxValue + 1
    [InlineData("-2147483649")]  // int.MinValue - 1
    [InlineData("1.5")]
    [InlineData("\"42\"")]       // número em texto não é aceito
    [InlineData("true")]
    public void Validar_ValorForaDoInteiro32Bits_DevolveErro(string json)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json(json));

        Assert.False(resultado.Valido);
        Assert.Contains("inteiro", resultado.Erro);
    }
}
