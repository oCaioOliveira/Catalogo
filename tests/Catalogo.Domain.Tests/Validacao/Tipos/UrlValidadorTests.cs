using Catalogo.Domain.Metadados;
using Catalogo.Domain.Tests.Apoio;
using Catalogo.Domain.Validacao.Tipos;

namespace Catalogo.Domain.Tests.Validacao.Tipos;

// RN04 — URL (ADR 0042)
public class UrlValidadorTests
{
    private readonly UrlValidador _validador = new();
    private readonly Campo _campo = Metadado.Campo("url", TipoDado.Url);

    [Theory]
    [InlineData("https://exemplo.com.br")]
    [InlineData("http://localhost:8080/painel?aba=1#topo")]
    [InlineData("https://grafana.interno/d/AbC123/Desempenho")]
    [InlineData("HTTPS://Exemplo.COM/a")]
    public void Validar_UrlAbsolutaHttpOuHttps_EhValidaEGravadaComoVeio(string url)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json($"\"{url}\""));

        Assert.True(resultado.Valido, resultado.Erro);
        Assert.Equal(url, resultado.ValorNormalizado!.GetValue<string>());
    }

    [Fact]
    public void Validar_EspacosNasPontas_SaoRemovidos()
    {
        var resultado = _validador.Validar(_campo, Metadado.Json("\"  https://exemplo.com/a  \""));

        Assert.Equal("https://exemplo.com/a", resultado.ValorNormalizado!.GetValue<string>());
    }

    [Theory]
    [InlineData("exemplo.com.br", "http://")]
    [InlineData("www.exemplo.com", "http://")]
    [InlineData("/caminho/relativo", "http://")]
    [InlineData("ftp://arquivos.exemplo.com/a", "http://")]
    [InlineData("mailto:alguem@exemplo.com", "http://")]
    [InlineData("javascript:alert(1)", "http://")]
    [InlineData("https://", "http://")]
    [InlineData("https:exemplo.com", "http://")]
    [InlineData("https://exemplo.com/com espaco", "espaços")]
    [InlineData("https://exemplo.com/\\tcom-tab", "espaços")]
    public void Validar_UrlInvalida_DevolveErro(string url, string trecho)
    {
        var resultado = _validador.Validar(_campo, Metadado.Json($"\"{url}\""));

        Assert.False(resultado.Valido);
        Assert.Contains(trecho, resultado.Erro);
    }

    [Theory]
    [InlineData(UrlValidador.MaximoCaracteres, true)]        // limite
    [InlineData(UrlValidador.MaximoCaracteres + 1, false)]   // um acima
    public void Validar_Tamanho_AteOMaximo(int tamanho, bool valido)
    {
        const string inicio = "https://exemplo.com/";
        var url = inicio + new string('a', tamanho - inicio.Length);

        var resultado = _validador.Validar(_campo, Metadado.Json($"\"{url}\""));

        Assert.Equal(valido, resultado.Valido);
        if (!valido)
            Assert.Contains("no máximo 2000", resultado.Erro);
    }

    [Theory]
    [InlineData("10")]
    [InlineData("true")]
    [InlineData("[\"https://a.com\"]")]
    public void Validar_ValorQueNaoEhTexto_DevolveErro(string json)
    {
        Assert.False(_validador.Validar(_campo, Metadado.Json(json)).Valido);
    }
}
