using System.Net;
using Catalogo.Api.Configuracao;
using Catalogo.Api.IntegrationTests.Apoio;

namespace Catalogo.Api.IntegrationTests;

// CORS (docs/seguranca.md#cors): só as origens de CORS_ALLOW (separadas por vírgula) recebem os cabeçalhos; "*" libera todas.
public class CorsApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private const string OrigemDoFront = "http://localhost:5173";

    [Fact]
    public async Task Preflight_OrigemConfigurada_RecebeOsCabecalhos()
    {
        var resposta = await Preflight(OrigemDoFront, "PUT");

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal(OrigemDoFront, Assert.Single(resposta.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("PUT", resposta.Headers.GetValues("Access-Control-Allow-Methods").Single().Split(','));
        var cabecalhos = resposta.Headers.GetValues("Access-Control-Allow-Headers").Single().ToLowerInvariant();
        Assert.Contains("authorization", cabecalhos);
        Assert.Contains("content-type", cabecalhos);
        // Sem cookies: a autenticação é o JWT no cabeçalho.
        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Preflight_OrigemNaoConfigurada_NaoRecebeOsCabecalhos()
    {
        var resposta = await Preflight("https://site-qualquer.example", "PUT");

        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Requisicao_OrigemConfigurada_ExpoeLocationEContentDisposition_InclusiveNosErros()
    {
        var fabrica = FabricaComOrigem();
        var cliente = ClienteDe(Administrador, fabrica);
        cliente.DefaultRequestHeaders.Add("Origin", OrigemDoFront);
        var anonimo = fabrica.CreateClient();
        anonimo.DefaultRequestHeaders.Add("Origin", OrigemDoFront);

        var sucesso = await cliente.GetAsync("/api/v1/grupos");
        var erro = await anonimo.GetAsync("/api/v1/grupos");

        Assert.Equal(OrigemDoFront, sucesso.Headers.GetValues("Access-Control-Allow-Origin").Single());
        var expostos = sucesso.Headers.GetValues("Access-Control-Expose-Headers").Single().ToLowerInvariant();
        Assert.Contains("location", expostos);
        Assert.Contains("content-disposition", expostos);
        // O front precisa ler o corpo do 401 (o "codigo") também.
        Assert.Equal(HttpStatusCode.Unauthorized, erro.StatusCode);
        Assert.Equal(OrigemDoFront, erro.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task Preflight_SegundaOrigemDaLista_RecebeOsCabecalhos()
    {
        var fabrica = FabricaCom(new Dictionary<string, string> { [ConfiguracaoCors.ChaveOrigens] = $"https://outro.example, {OrigemDoFront}/" });

        var resposta = await Preflight(OrigemDoFront, "PUT", fabrica);

        Assert.Equal(OrigemDoFront, Assert.Single(resposta.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Preflight_CorsAllowAsterisco_LiberaQualquerOrigem()
    {
        var fabrica = FabricaCom(new Dictionary<string, string> { [ConfiguracaoCors.ChaveOrigens] = "*" });

        var resposta = await Preflight("https://site-qualquer.example", "DELETE", fabrica);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        Assert.Equal("*", Assert.Single(resposta.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(resposta.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> FabricaComOrigem() =>
        FabricaCom(new Dictionary<string, string> { [ConfiguracaoCors.ChaveOrigens] = OrigemDoFront });

    private async Task<HttpResponseMessage> Preflight(
        string origem, string metodo, Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>? fabrica = null)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Options, $"{UrlRegistros("comunidade")}/{Guid.NewGuid()}");
        requisicao.Headers.Add("Origin", origem);
        requisicao.Headers.Add("Access-Control-Request-Method", metodo);
        requisicao.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        return await (fabrica ?? FabricaComOrigem()).CreateClient().SendAsync(requisicao);
    }
}
