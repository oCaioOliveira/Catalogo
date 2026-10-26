using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Tests.Validacao.Tipos;

// RN04 — DATA_HORA
public class DataHoraValidadorTests
{
    private readonly DataHoraValidador _validador = new();
    private readonly Campo _campo = Metadado.Campo("data_execucao", TipoDado.DataHora);

    [Theory]
    [InlineData("2026-10-21T14:30:00-03:00", "2026-10-21T17:30:00Z")]  // converte para UTC
    [InlineData("2026-10-21T17:30:00Z", "2026-10-21T17:30:00Z")]
    [InlineData("2026-10-21T17:30Z", "2026-10-21T17:30:00Z")]           // segundos opcionais
    [InlineData("2026-10-21T17:30:00.25+00:00", "2026-10-21T17:30:00.25Z")]
    [InlineData("2026-12-31T23:00:00-03:00", "2027-01-01T02:00:00Z")]  // muda o dia e o ano
    public void Validar_Iso8601ComFuso_DevolveEmUtc(string entrada, string esperado)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json($"\"{entrada}\""));

        Assert.True(resultado.Valido);
        Assert.Equal(esperado, resultado.ValorNormalizado!.GetValue<string>());
    }

    [Theory]
    [InlineData("\"2026-10-21T14:30:00\"")]   // sem fuso
    [InlineData("\"2026-10-21\"")]            // só data
    [InlineData("\"21/10/2026 14:30\"")]
    [InlineData("\"2026-10-21T25:00:00Z\"")]  // hora inexistente
    [InlineData("1729521000")]
    public void Validar_SemFusoOuForaDoFormato_DevolveErro(string json)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json(json));

        Assert.False(resultado.Valido);
        Assert.Contains("fuso", resultado.Erro);
    }
}
