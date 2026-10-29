using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Tests.Validacao.Tipos;

// RN04 — LISTA
public class ListaValidadorTests
{
    private readonly ListaValidador _validador = new();
    private readonly Campo _campo = Metadado.Campo("metodo", TipoDado.Lista, opcoes: ["GET", "POST"]);

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public void Validar_ValorEntreAsOpcoes_EhValido(string valor)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json($"\"{valor}\""));

        Assert.True(resultado.Valido);
        Assert.Equal(valor, resultado.ValorNormalizado!.GetValue<string>());
    }

    [Theory]
    [InlineData("\"PUT\"")]
    [InlineData("\"get\"")]   // diferencia maiúsculas
    [InlineData("1")]
    public void Validar_ValorForaDasOpcoes_DevolveErroListandoAsOpcoes(string json)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json(json));

        Assert.False(resultado.Valido);
        Assert.Contains("GET, POST", resultado.Erro);
    }
}
