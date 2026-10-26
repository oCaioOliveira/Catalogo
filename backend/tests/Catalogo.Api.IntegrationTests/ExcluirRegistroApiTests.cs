using System.Net;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;

namespace Catalogo.Api.IntegrationTests;

// DELETE — RN11 e RN16 (plano de exclusão com CASCATA e RESTRINGIR).
public class ExcluirRegistroApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Fact]
    public async Task Delete_ProjetoComEndpoints_Devolve409ListandoAsDependencias()
    {
        var projeto = await CriarProjeto("Portal");
        await CriarEndpoint(projeto, "consulta");
        await CriarEndpoint(projeto, "cadastro");

        var resposta = await Excluir("projeto", projeto);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        var problema = await LerJson(resposta);
        Assert.Equal("Exclusão bloqueada.", problema["title"]!.GetValue<string>());
        var dependencia = Assert.Single(problema["detalhes"]!["dependencias"]!.AsArray())!;
        Assert.Equal("endpoint_servico", dependencia["entidade"]!.GetValue<string>());
        Assert.Equal("projeto", dependencia["campo"]!.GetValue<string>());
        Assert.Equal(2, dependencia["quantidade"]!.GetValue<int>());
        Assert.Equal(["cadastro", "consulta"], dependencia["exemplos"]!.AsArray().Select(e => e!.GetValue<string>()).Order());
        // Nada foi excluído.
        Assert.Equal(HttpStatusCode.OK, (await Buscar("projeto", projeto)).StatusCode);
    }

    [Fact]
    public async Task Delete_RelatorioComCenariosELinks_Devolve204ExcluiOsFilhosEMantemORtc()
    {
        var projeto = await CriarProjeto("A");
        var rtc = await CriarRtc();
        var relatorio = await CriarRelatorio(projeto, rtc: rtc);
        var endpoint = await CriarEndpoint(projeto, "consulta");
        var cenario1 = await Criar("cenario_teste", DadosDoCenario(relatorio, endpoint, 1));
        var cenario2 = await Criar("cenario_teste", DadosDoCenario(relatorio, endpoint, 2));
        var link = await Criar("link", NovoLink(relatorio, rtc));

        var resposta = await Excluir("relatorio_teste", relatorio);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
        foreach (var (entidade, id) in new[] { ("relatorio_teste", relatorio), ("cenario_teste", cenario1), ("cenario_teste", cenario2), ("link", link) })
            Assert.Equal(HttpStatusCode.NotFound, (await Buscar(entidade, id)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Buscar("rtc", rtc)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Buscar("endpoint_servico", endpoint)).StatusCode);

        // A chave única do link (uq_link_rtc) foi liberada: outro link pode usar o mesmo RTC.
        var outroRelatorio = await CriarRelatorio(projeto);
        Assert.Equal(HttpStatusCode.Created, (await Postar("link", NovoLink(outroRelatorio, rtc))).StatusCode);
    }

    [Fact]
    public async Task Delete_RtcUsadoPorLink_Devolve409()
    {
        // link.rtc é RESTRINGIR e o link não seria excluído junto.
        var projeto = await CriarProjeto("A");
        var rtc = await CriarRtc();
        var relatorio = await CriarRelatorio(projeto);
        await Criar("link", NovoLink(relatorio, rtc));

        var resposta = await Excluir("rtc", rtc);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        Assert.Equal("link", (await LerJson(resposta))["detalhes"]!["dependencias"]![0]!["entidade"]!.GetValue<string>());
    }

    [Fact]
    public async Task Delete_RegistroInexistente_Devolve404()
    {
        var resposta = await Excluir("comunidade", Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    private static JsonObject NovoLink(string relatorio, string rtc) => new()
    {
        ["relatorio_teste"] = relatorio,
        ["rtc"] = rtc,
        ["texto_exibicao"] = "Evidências",
        ["url"] = "https://exemplo.local/evidencias",
    };
}
