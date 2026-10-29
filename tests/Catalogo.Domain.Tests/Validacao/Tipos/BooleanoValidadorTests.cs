using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Tests.Validacao.Tipos;

// RN04 — BOOLEANO
public class BooleanoValidadorTests
{
    private readonly BooleanoValidador _validador = new();
    private readonly Campo _campo = Metadado.Campo("ativo", TipoDado.Booleano);

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Validar_TrueOuFalse_EhValido(string json, bool esperado)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json(json));

        Assert.True(resultado.Valido);
        Assert.Equal(esperado, resultado.ValorNormalizado!.GetValue<bool>());
    }

    [Theory]
    [InlineData("\"true\"")]
    [InlineData("1")]
    [InlineData("0")]
    public void Validar_OutrosValores_DevolveErro(string json)
    {
        Assert.False(_validador.Validar(_campo, Metadado.Json(json)).Valido);
    }
}
