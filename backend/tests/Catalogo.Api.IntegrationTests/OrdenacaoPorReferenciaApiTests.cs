using System.Net;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;

namespace Catalogo.Api.IntegrationTests;

// RN19: ordenar por um campo REFERENCIA = pelo valor do campo de exibição do registro apontado, com o tipo dele; sem referência
// por último nos dois sentidos. Sem campo de exibição na apontada, 400.
public class OrdenacaoPorReferenciaApiTests(BancoDeTestesFixture banco) : TesteDeMetadados(banco)
{
    [Fact]
    public async Task Get_RelatoriosOrdenadosPorProjeto_PeloNomeDoProjetoNosDoisSentidos()
    {
        foreach (var nome in new[] { "Gama", "Alfa", "Beta" })
            await CriarRelatorio(await CriarProjeto(nome), bateria: $"Bateria {nome}");

        var crescente = await ListarOk("relatorio_teste", "ordenacao=projeto");
        var decrescente = await ListarOk("relatorio_teste", "ordenacao=-projeto");

        Assert.Equal(["Alfa", "Beta", "Gama"], Rotulos(crescente, "projeto"));
        Assert.Equal(["Gama", "Beta", "Alfa"], Rotulos(decrescente, "projeto"));
    }

    [Fact]
    public async Task Get_LinksOrdenadosPorRtcOpcional_SemReferenciaSempreNoFim()
    {
        var relatorio = await CriarRelatorio(await CriarProjeto("Portal"));
        var rtcB = await Criar("rtc", new JsonObject { ["rtc_externo_id"] = "RTC-B" });
        var rtcA = await Criar("rtc", new JsonObject { ["rtc_externo_id"] = "RTC-A" });
        await CriarLink(relatorio, "sem rtc", null);
        await CriarLink(relatorio, "com B", rtcB);
        await CriarLink(relatorio, "com A", rtcA);

        var crescente = await ListarOk("link", $"relatorio_teste={relatorio}&ordenacao=rtc");
        var decrescente = await ListarOk("link", $"relatorio_teste={relatorio}&ordenacao=-rtc");

        Assert.Equal(["com A", "com B", "sem rtc"], Textos(crescente));
        Assert.Equal(["com B", "com A", "sem rtc"], Textos(decrescente));
    }

    [Fact]
    public async Task Get_OrdenadoPorReferenciaComExibicaoNumerica_NumeroComoNumero()
    {
        // lote (exibição: numero INTEIRO) ← item.lote. Como texto, "10" viria antes de "2" e de "9".
        await CriarGrupoAdmin();
        await CriarEntidadeAdmin("lote", escopoEquipe: false);
        await CriarCampoAdmin("lote", Campo("numero", "INTEIRO", obrigatorio: true));
        await DefinirCampoExibicao("lote", "numero");
        await CriarEntidadeAdmin("item", escopoEquipe: false);
        await CriarCampoAdmin("item", Campo("nome", "TEXTO", obrigatorio: true, tamanho: 50));
        await CriarCampoAdmin("item", Referencia("lote", "lote"));
        foreach (var numero in new[] { 10, 2, 9 })
        {
            var lote = await CriarNoGrupo("lote", new JsonObject { ["numero"] = numero });
            await CriarNoGrupo("item", new JsonObject { ["nome"] = $"item do {numero}", ["lote"] = lote });
        }
        await CriarNoGrupo("item", new JsonObject { ["nome"] = "sem lote" });

        var crescente = await LerJson(await Cliente.GetAsync($"{UrlUso("item")}?ordenacao=lote"));
        var decrescente = await LerJson(await Cliente.GetAsync($"{UrlUso("item")}?ordenacao=-lote"));

        Assert.Equal(["item do 2", "item do 9", "item do 10", "sem lote"], Valores(crescente, "nome"));
        Assert.Equal(["item do 10", "item do 9", "item do 2", "sem lote"], Valores(decrescente, "nome"));
    }

    [Fact]
    public async Task Get_OrdenadoPorReferenciaParaEntidadeSemCampoDeExibicao_Devolve400()
    {
        await CriarGrupoAdmin();
        await CriarEntidadeAdmin("lote", escopoEquipe: false);
        await CriarCampoAdmin("lote", Campo("numero", "INTEIRO"));
        await CriarEntidadeAdmin("item", escopoEquipe: false);
        await CriarCampoAdmin("item", Referencia("lote", "lote"));

        var resposta = await Cliente.GetAsync($"{UrlUso("item")}?ordenacao=lote");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("não tem campo de exibição", (await LerJson(resposta))["errors"]!["ordenacao"]![0]!.GetValue<string>());
    }

    private Task<string> CriarLink(string relatorio, string texto, string? rtc)
    {
        var dados = new JsonObject { ["relatorio_teste"] = relatorio, ["texto_exibicao"] = texto, ["url"] = "https://exemplo.com.br/painel" };
        if (rtc is not null)
            dados["rtc"] = rtc;
        return Criar("link", dados);
    }

    private static JsonObject Referencia(string chave, string entidade) => new()
    {
        ["chave"] = chave, ["nome"] = chave, ["tipoDado"] = "REFERENCIA", ["entidadeReferenciada"] = entidade, ["acaoExclusao"] = "RESTRINGIR",
    };

    private static List<string> Rotulos(JsonNode lista, string campo) =>
        lista["itens"]!.AsArray().Select(i => i!["dados"]![campo]!["rotulo"]!.GetValue<string>()).ToList();

    private static List<string> Textos(JsonNode lista) => Valores(lista, "texto_exibicao");

    private static List<string> Valores(JsonNode lista, string campo) =>
        lista["itens"]!.AsArray().Select(i => i!["dados"]![campo]!.GetValue<string>()).ToList();
}
