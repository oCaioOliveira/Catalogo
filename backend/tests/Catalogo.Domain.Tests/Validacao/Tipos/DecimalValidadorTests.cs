using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Tests.Validacao.Tipos;

// RN04 — DECIMAL. O campo de teste é decimal(5,2), igual a "taxa_erro_percentual" do seed:
// aceita de -999.99 a 999.99.
public class DecimalValidadorTests
{
    private readonly DecimalValidador _validador = new();
    private readonly Campo _campo = Metadado.Campo("taxa", TipoDado.Decimal, precisao: 5, escala: 2);

    [Theory]
    [InlineData("0", "0.00")]
    [InlineData("12.5", "12.50")]
    [InlineData("12.50", "12.50")]
    [InlineData("12.500", "12.50")]   // zeros à direita não contam como casa decimal
    [InlineData("999.99", "999.99")]  // limite superior
    [InlineData("-999.99", "-999.99")]
    [InlineData("7", "7.00")]
    public void Validar_DentroDaPrecisaoEEscala_DevolveComEscalaFixa(string json, string esperadoNoJson)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json(json));

        Assert.True(resultado.Valido);
        Assert.Equal(esperadoNoJson, resultado.ValorNormalizado!.ToJsonString());
    }

    [Theory]
    [InlineData("12.505")]
    [InlineData("0.001")]
    public void Validar_MaisCasasQueAEscala_DevolveErro(string json)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json(json));

        Assert.False(resultado.Valido);
        Assert.Contains("2 casas decimais", resultado.Erro);
    }

    [Theory]
    [InlineData("1000")]      // 4 dígitos antes da vírgula; o limite é 5 - 2 = 3
    [InlineData("-1000.00")]
    public void Validar_MaisDigitosQueAPrecisao_DevolveErro(string json)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json(json));

        Assert.False(resultado.Valido);
        Assert.Contains("3 dígitos antes da vírgula", resultado.Erro);
    }

    [Fact]
    public void Validar_PrecisaoIgualAEscala_SoAceitaMenorQueUm()
    {
        var campo = Metadado.Campo("fracao", TipoDado.Decimal, precisao: 2, escala: 2);

        Assert.True(_validador.Validar(campo, Metadado.Json("0.99")).Valido);
        Assert.False(_validador.Validar(campo, Metadado.Json("1")).Valido);
    }

    [Theory]
    [InlineData("\"12.5\"")]
    [InlineData("false")]
    public void Validar_ValorQueNaoEhNumero_DevolveErro(string json)
    {
        Assert.False(_validador.Validar(_campo, Metadado.Json(json)).Valido);
    }
}
