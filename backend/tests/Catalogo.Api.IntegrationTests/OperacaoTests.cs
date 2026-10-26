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

    [Fact]
    public async Task Swagger_ForaDeDevelopment_NaoEhPublicado()
    {
        // Os testes rodam no ambiente "Testes".
        var resposta = await Cliente.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
