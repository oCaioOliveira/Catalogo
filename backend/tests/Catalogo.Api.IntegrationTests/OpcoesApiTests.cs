using System.Net;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;

namespace Catalogo.Api.IntegrationTests;

// RN21 — opções de campo REFERENCIA (com e sem dependência).
public class OpcoesApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    // O formulário de cenário é de uma entidade por equipe: o administrador informa a equipe do registro (RN27).
    private string UrlOpcoes(string entidade, string campo, string consulta = "") =>
        $"{UrlGrupo}/entidades/{entidade}/campos/{campo}/opcoes?equipeId={EquipePadrao.Id}&{consulta}";

    [Fact]
    public async Task Get_EndpointDoCenarioComDependeDeORelatorio_TrazSoOsEndpointsDoProjetoDoRelatorio()
    {
        var projetoA = await CriarProjeto("A");
        var projetoB = await CriarProjeto("B");
        var relatorioDoA = await CriarRelatorio(projetoA);
        var endpointA1 = await CriarEndpoint(projetoA, "consulta-a");
        var endpointA2 = await CriarEndpoint(projetoA, "cadastro-a");
        await CriarEndpoint(projetoB, "consulta-b");

        var opcoes = await OpcoesOk("cenario_teste", "endpoint_servico", $"dependeDe={relatorioDoA}");

        // Ordenado por rótulo (nome_servico).
        Assert.Equal(["cadastro-a", "consulta-a"], Rotulos(opcoes));
        Assert.Equal([endpointA2, endpointA1], opcoes["itens"]!.AsArray().Select(i => i!["id"]!.GetValue<string>()));
        Assert.Equal(2, opcoes["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Get_CampoComDependenciaSemDependeDe_Devolve400()
    {
        var resposta = await Cliente.GetAsync(UrlOpcoes("cenario_teste", "endpoint_servico"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erro = (await LerJson(resposta))["errors"]!["dependeDe"]![0]!.GetValue<string>();
        Assert.Contains("relatorio_teste", erro);
    }

    [Fact]
    public async Task Get_ComBusca_FiltraPeloRotuloSemDiferenciarAcentos()
    {
        var projeto = await CriarProjeto("A");
        var relatorio = await CriarRelatorio(projeto);
        await CriarEndpoint(projeto, "emissão-boleto");
        await CriarEndpoint(projeto, "consulta-saldo");

        var opcoes = await OpcoesOk("cenario_teste", "endpoint_servico", $"dependeDe={relatorio}&busca=EMISSAO");

        Assert.Equal(["emissão-boleto"], Rotulos(opcoes));
    }

    [Fact]
    public async Task Get_CampoSemDependencia_TrazTodosOsRegistrosDaEntidadeReferenciada()
    {
        var projetoA = await CriarProjeto("A");
        var projetoB = await CriarProjeto("B");
        await CriarRelatorio(projetoA, titulo: "Bateria B");
        await CriarRelatorio(projetoB, titulo: "Bateria A");

        var opcoes = await OpcoesOk("cenario_teste", "relatorio_teste");

        Assert.Equal(["Bateria A", "Bateria B"], Rotulos(opcoes));
    }

    [Fact]
    public async Task Get_CampoQueNaoEhReferencia_Devolve400()
    {
        var resposta = await Cliente.GetAsync(UrlOpcoes("cenario_teste", "numero_cenario"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task Get_CampoInexistente_Devolve404()
    {
        var resposta = await Cliente.GetAsync(UrlOpcoes("cenario_teste", "nao_existe"));

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task ListarEntidades_CampoEndpointDoCenario_InformaDependeDe()
    {
        var entidades = (await LerJson(await Cliente.GetAsync($"{UrlGrupo}/entidades"))).AsArray();
        var cenario = entidades.Single(e => e!["chave"]!.GetValue<string>() == "cenario_teste")!;
        var campos = cenario["campos"]!.AsArray();

        Assert.Equal("relatorio_teste",
            campos.Single(c => c!["chave"]!.GetValue<string>() == "endpoint_servico")!["dependeDe"]!.GetValue<string>());
        Assert.Null(campos.Single(c => c!["chave"]!.GetValue<string>() == "relatorio_teste")!["dependeDe"]);
    }

    private async Task<JsonNode> OpcoesOk(string entidade, string campo, string consulta = "")
    {
        var resposta = await Cliente.GetAsync(UrlOpcoes(entidade, campo, consulta));
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == HttpStatusCode.OK, $"{(int)resposta.StatusCode} {corpo}");
        return JsonNode.Parse(corpo)!;
    }

    private static List<string> Rotulos(JsonNode opcoes) =>
        opcoes["itens"]!.AsArray().Select(i => i!["rotulo"]!.GetValue<string>()).ToList();
}
