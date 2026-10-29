using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Tests.Validacao.Tipos;

// RN04 — TEXTO_LONGO
public class TextoLongoValidadorTests
{
    private readonly TextoLongoValidador _validador = new();
    private readonly Campo _campo = Metadado.Campo("descricao", TipoDado.TextoLongo);

    [Fact]
    public void Validar_TextoMuitoGrande_EhValido()
    {
        var texto = new string('x', 100_000);

        var resultado = _validador.Validar(_campo, Metadado.Json($"\"{texto}\""));

        Assert.True(resultado.Valido);
        Assert.Equal(texto, resultado.ValorNormalizado!.GetValue<string>());
    }

    [Fact]
    public void Validar_EspacosNasPontas_SaoRemovidos()
    {
        var resultado = _validador.Validar(_campo, Metadado.Json("\"  linha 1\\nlinha 2  \""));

        Assert.Equal("linha 1\nlinha 2", resultado.ValorNormalizado!.GetValue<string>());
    }

    [Theory]
    [InlineData("10")]
    [InlineData("false")]
    public void Validar_ValorQueNaoEhTexto_DevolveErro(string json)
    {
        Assert.False(_validador.Validar(_campo, Metadado.Json(json)).Valido);
    }
}
