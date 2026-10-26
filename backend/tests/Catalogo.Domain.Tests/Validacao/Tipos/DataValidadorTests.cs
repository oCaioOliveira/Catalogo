using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Tests.Validacao.Tipos;

// RN04 — DATA
public class DataValidadorTests
{
    private readonly DataValidador _validador = new();
    private readonly Campo _campo = Metadado.Campo("data", TipoDado.Data);

    [Theory]
    [InlineData("\"2026-10-21\"")]
    [InlineData("\"2024-02-29\"")]  // ano bissexto
    [InlineData("\"0001-01-01\"")]
    public void Validar_DataIso8601_EhValida(string json)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json(json));

        Assert.True(resultado.Valido);
        Assert.Equal(json.Trim('"'), resultado.ValorNormalizado!.GetValue<string>());
    }

    [Theory]
    [InlineData("\"21/10/2026\"")]
    [InlineData("\"2026-13-01\"")]
    [InlineData("\"2025-02-29\"")]           // 2025 não é bissexto
    [InlineData("\"2026-10-21T10:00:00Z\"")] // data com hora não é DATA
    [InlineData("20261021")]
    public void Validar_ForaDoFormatoOuInexistente_DevolveErro(string json)
    {
        Assert.False(_validador.Validar(_campo, Metadado.Json(json)).Valido);
    }
}
