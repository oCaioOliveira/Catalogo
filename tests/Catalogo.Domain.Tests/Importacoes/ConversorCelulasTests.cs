using System.Text.Json.Nodes;
using Catalogo.Domain.Importacoes;
using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;

namespace Catalogo.Domain.Tests.Importacoes;

// RN100: cada tipo aceita o que um usuário brasileiro digitaria; o resto continua com o validador do tipo (RN04).
public class ConversorCelulasTests
{
    private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    private static ResultadoConversao Converter(TipoDado tipo, CelulaImportada celula, IReadOnlyList<string>? opcoes = null) =>
        ConversorCelulas.Converter(Metadado.Campo("campo", tipo, opcoes: opcoes, escala: 2, precisao: 18), celula, Brasilia);

    private static string? Texto(ResultadoConversao resultado) => resultado.Valor?.ToJsonString();

    [Fact]
    public void Converter_TodoTipoMenosReferencia_TemConversao() =>
        Assert.Equal(Enum.GetValues<TipoDado>().Where(t => t != TipoDado.Referencia).Order(), ConversorCelulas.TiposConvertidos.Order());

    [Theory]
    [InlineData(TipoDado.Texto)]
    [InlineData(TipoDado.Inteiro)]
    [InlineData(TipoDado.Data)]
    [InlineData(TipoDado.Booleano)]
    public void Converter_CelulaVaziaOuSoEspacos_NaoInformado(TipoDado tipo)
    {
        Assert.Null(Converter(tipo, CelulaImportada.DeTexto("   ")).Valor);
        Assert.True(Converter(tipo, CelulaImportada.Vazia).Valido);
    }

    [Theory]
    [InlineData("1500", "1500")]
    [InlineData("1.200", "1200")]          // no inteiro, o ponto é milhar
    [InlineData("12.345.678", "12345678")]
    [InlineData("-7", "-7")]
    [InlineData(" 42 ", "42")]
    public void Inteiro_FormatosAceitos(string digitado, string json) =>
        Assert.Equal(json, Texto(Converter(TipoDado.Inteiro, CelulaImportada.DeTexto(digitado))));

    [Theory]
    [InlineData("1,5")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("99999999999")]
    public void Inteiro_FormatosRecusados(string digitado) =>
        Assert.False(Converter(TipoDado.Inteiro, CelulaImportada.DeTexto(digitado)).Valido);

    [Fact]
    public void Inteiro_NumeroDoExcel_Aceito() =>
        Assert.Equal("1500", Texto(Converter(TipoDado.Inteiro, new CelulaImportada(null, Numero: 1500m))));

    [Theory]
    [InlineData("1234,56", "1234.56")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("1234.56", "1234.56")]
    [InlineData("1.234.567", "1234567")]
    [InlineData("-0,5", "-0.5")]
    [InlineData("0,25", "0.25")]
    public void Decimal_VirgulaOuPontoDecimal_MilharComPontoQuandoHaVirgula(string digitado, string json) =>
        Assert.Equal(json, Texto(Converter(TipoDado.Decimal, CelulaImportada.DeTexto(digitado))));

    [Theory]
    [InlineData("1,234.56")]   // formato americano: ambíguo, recusado
    [InlineData("12,34,5")]
    [InlineData("R$ 10")]
    [InlineData("1.23,4")]
    public void Decimal_FormatosRecusados(string digitado) =>
        Assert.False(Converter(TipoDado.Decimal, CelulaImportada.DeTexto(digitado)).Valido);

    [Theory]
    [InlineData("21/10/2026", "\"2026-10-21\"")]
    [InlineData("1/2/2026", "\"2026-02-01\"")]
    [InlineData("2026-10-21", "\"2026-10-21\"")]
    public void Data_DiaMesAnoOuIso(string digitado, string json) =>
        Assert.Equal(json, Texto(Converter(TipoDado.Data, CelulaImportada.DeTexto(digitado))));

    [Theory]
    [InlineData("31/02/2026")]
    [InlineData("10/21/2026")]
    [InlineData("21-10-2026")]
    [InlineData("ontem")]
    public void Data_FormatosRecusados(string digitado) =>
        Assert.False(Converter(TipoDado.Data, CelulaImportada.DeTexto(digitado)).Valido);

    [Fact]
    public void Data_ValorNativoDoExcelESerieNumerica_Aceitos()
    {
        Assert.Equal("\"2026-10-21\"", Texto(Converter(TipoDado.Data, new CelulaImportada(null, Data: new DateTime(2026, 10, 21)))));
        Assert.Equal("\"2026-10-21\"", Texto(Converter(TipoDado.Data, new CelulaImportada(null, Numero: 46316m))));
    }

    [Theory]
    [InlineData("21/10/2026 14:30", "\"2026-10-21T14:30:00-03:00\"")]
    [InlineData("21/10/2026 14:30:15", "\"2026-10-21T14:30:15-03:00\"")]
    [InlineData("2026-10-21T14:30", "\"2026-10-21T14:30:00-03:00\"")]
    [InlineData("2026-10-21T17:30:00Z", "\"2026-10-21T17:30:00Z\"")]
    [InlineData("2026-10-21T14:30:00-03:00", "\"2026-10-21T14:30:00-03:00\"")]
    public void DataHora_DiaMesAnoHoraOuIso_SemFusoEOFusoDeNegocio(string digitado, string json) =>
        Assert.Equal(json, Texto(Converter(TipoDado.DataHora, CelulaImportada.DeTexto(digitado))));

    [Theory]
    [InlineData("21/10/2026")]
    [InlineData("21/10/2026 25:00")]
    [InlineData("amanhã 10h")]
    public void DataHora_FormatosRecusados(string digitado) =>
        Assert.False(Converter(TipoDado.DataHora, CelulaImportada.DeTexto(digitado)).Valido);

    [Fact]
    public void DataHora_ValorNativoDoExcel_HoraLocalDoNegocio() =>
        Assert.Equal("\"2026-10-21T14:30:00-03:00\"",
            Texto(Converter(TipoDado.DataHora, new CelulaImportada(null, Data: new DateTime(2026, 10, 21, 14, 30, 0)))));

    [Theory]
    [InlineData("Sim", true)]
    [InlineData("não", false)]
    [InlineData("NAO", false)]
    [InlineData("Verdadeiro", true)]
    [InlineData("falso", false)]
    [InlineData("true", true)]
    [InlineData("FALSE", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void Booleano_FormatosAceitos(string digitado, bool esperado) =>
        Assert.Equal(esperado, Converter(TipoDado.Booleano, CelulaImportada.DeTexto(digitado)).Valor!.GetValue<bool>());

    [Theory]
    [InlineData("talvez")]
    [InlineData("S")]
    [InlineData("2")]
    public void Booleano_FormatosRecusados(string digitado) =>
        Assert.False(Converter(TipoDado.Booleano, CelulaImportada.DeTexto(digitado)).Valido);

    [Fact]
    public void Booleano_VerdadeiroDoExcelENumero1_Aceitos()
    {
        Assert.True(Converter(TipoDado.Booleano, new CelulaImportada(null, Booleano: true)).Valor!.GetValue<bool>());
        Assert.False(Converter(TipoDado.Booleano, new CelulaImportada(null, Numero: 0m)).Valor!.GetValue<bool>());
    }

    [Theory]
    [InlineData("carga", "Carga")]
    [InlineData("ESTRESSE", "Estresse")]
    [InlineData("resistencia", "Resistência")]
    public void Lista_SemDiferenciarMaiusculasEAcentos_GravaAOpcaoOficial(string digitado, string oficial) =>
        Assert.Equal(oficial, Converter(TipoDado.Lista, CelulaImportada.DeTexto(digitado), ["Carga", "Estresse", "Resistência"]).Valor!.GetValue<string>());

    [Fact]
    public void Lista_ForaDasOpcoes_ErroComAsOpcoes()
    {
        var resultado = Converter(TipoDado.Lista, CelulaImportada.DeTexto("Pico"), ["Carga", "Estresse"]);

        Assert.False(resultado.Valido);
        Assert.Contains("Carga, Estresse", resultado.Erro);
    }

    [Fact]
    public void TextoEUrl_VemComoTexto_NumeroDoExcelViraOTextoQueOUsuarioVe()
    {
        Assert.Equal("\"https://grafana/d/1\"", Texto(Converter(TipoDado.Url, CelulaImportada.DeTexto(" https://grafana/d/1 "))));
        Assert.Equal("\"1234,5\"", Texto(Converter(TipoDado.Texto, new CelulaImportada(null, Numero: 1234.5m))));
        Assert.IsType<JsonValue>(Converter(TipoDado.TextoLongo, CelulaImportada.DeTexto("abc")).Valor, exactMatch: false);
    }

    [Fact]
    public void Referencia_NaoEConvertidaAqui() =>
        Assert.Throws<InvalidOperationException>(() => Converter(TipoDado.Referencia, CelulaImportada.DeTexto("Projeto")));
}
