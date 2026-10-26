using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Tests.Validacao.Tipos;

// RN04 — TEXTO
public class TextoValidadorTests
{
    private readonly TextoValidador _validador = new();
    private readonly Campo _campo = Metadado.Campo("nome", TipoDado.Texto, tamanhoMaximo: 5);

    [Fact]
    public void Validar_TextoDentroDoLimite_DevolveTextoSemEspacosNasPontas()
    {
        var resultado = _validador.Validar(_campo, Metadado.Json("\"  abc  \""));

        Assert.True(resultado.Valido);
        Assert.Equal("abc", resultado.ValorNormalizado!.GetValue<string>());
    }

    [Fact]
    public void Validar_TextoExatamenteNoLimite_EhValido()
    {
        Assert.True(_validador.Validar(_campo, Metadado.Json("\"abcde\"")).Valido);
    }

    [Fact]
    public void Validar_TextoUmCaractereAcimaDoLimite_DevolveErroComTamanho()
    {
        var resultado = _validador.Validar(_campo, Metadado.Json("\"abcdef\""));

        Assert.False(resultado.Valido);
        Assert.Contains("no máximo 5 caracteres", resultado.Erro);
        Assert.Contains("6", resultado.Erro);
    }

    [Fact]
    public void Validar_EspacosNasPontas_NaoContamNoLimite()
    {
        Assert.True(_validador.Validar(_campo, Metadado.Json("\"   abcde   \"")).Valido);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[\"a\"]")]
    public void Validar_ValorQueNaoEhTexto_DevolveErro(string json)
    {
        Assert.False(_validador.Validar(_campo, Metadado.Json(json)).Valido);
    }
}
