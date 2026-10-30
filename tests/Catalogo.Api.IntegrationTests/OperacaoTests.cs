using System.Net;
using Catalogo.Api.IntegrationTests.Apoio;

namespace Catalogo.Api.IntegrationTests;

public class OperacaoTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Fact]
    public async Task Health_ComBancoAcessivel_Devolve200Healthy()
    {
        var resposta = await Cliente.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("TQS")]
    [InlineData("HMP")]
    [InlineData("PRD")]
    public async Task Swagger_ForaDeLocalEDes_NaoEhPublicado(string ambiente)
    {
        var fabrica = FabricaCom(new Dictionary<string, string> { ["ENVIRONMENT"] = ambiente });

        var resposta = await fabrica.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Theory]
    [InlineData("LOCAL")]
    [InlineData("DES")]
    public async Task Swagger_EmLocalEDes_EhPublicado(string ambiente)
    {
        var fabrica = FabricaCom(new Dictionary<string, string> { ["ENVIRONMENT"] = ambiente });

        var resposta = await fabrica.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }
}
