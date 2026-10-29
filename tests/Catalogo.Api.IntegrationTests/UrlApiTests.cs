using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;

namespace Catalogo.Api.IntegrationTests;

// Tipo URL (RN04, RN09, RN18, RN19; ADR 0042): validação na gravação, unicidade, filtros, ordenação e a conversão do V007.
public class UrlApiTests(BancoDeTestesFixture banco) : TesteDeMetadados(banco)
{
    [Fact]
    public async Task Seed_V007ConverteuOsDoisCamposDeEnderecoParaUrl()
    {
        var entidades = (await LerJson(await Cliente.GetAsync($"{UrlGrupo}/entidades"))).AsArray();

        string Tipo(string entidade, string campo) => entidades.Single(e => e!["chave"]!.GetValue<string>() == entidade)!["campos"]!.AsArray()
            .Single(c => c!["chave"]!.GetValue<string>() == campo)!["tipoDado"]!.GetValue<string>();

        Assert.Equal("URL", Tipo("link", "url"));
        Assert.Equal("URL", Tipo("projeto", "url_projeto"));
    }

    [Theory]
    [InlineData("intranet/painel")]
    [InlineData("ftp://arquivos.exemplo.com")]
    [InlineData("https://exemplo.com/com espaco")]
    public async Task Post_LinkComUrlInvalida_Devolve400NoCampo(string url)
    {
        var relatorio = await CriarRelatorio(await CriarProjeto("Portal"));

        var resposta = await Postar("link", new JsonObject { ["relatorio_teste"] = relatorio, ["texto_exibicao"] = "Painel", ["url"] = url });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("url", (await LerJson(resposta))["errors"]!.AsObject().Select(e => e.Key));
    }

    [Fact]
    public async Task Post_LinkComUrlValida_GravaSemEspacosEFiltraPorIgualEContem()
    {
        var relatorio = await CriarRelatorio(await CriarProjeto("Portal"));
        var id = await Criar("link", new JsonObject
        {
            ["relatorio_teste"] = relatorio, ["texto_exibicao"] = "Grafana", ["url"] = "  https://grafana.exemplo.com/d/Abc  ",
        });
        await Criar("link", new JsonObject { ["relatorio_teste"] = relatorio, ["texto_exibicao"] = "Outro", ["url"] = "https://outro.com" });

        Assert.Equal("https://grafana.exemplo.com/d/Abc", (await Ler("link", id))["dados"]!["url"]!.GetValue<string>());
        var contem = await ListarOk("link", "url.contem=GRAFANA");
        var igual = await ListarOk("link", "url=https://grafana.exemplo.com/d/Abc");
        var ordenado = await ListarOk("link", $"relatorio_teste={relatorio}&ordenacao=-url");

        Assert.Equal([id], Ids(contem));
        Assert.Equal([id], Ids(igual));
        Assert.Equal(["https://outro.com", "https://grafana.exemplo.com/d/Abc"],
            ordenado["itens"]!.AsArray().Select(i => i!["dados"]!["url"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Post_UrlRepetidaNumaRestricao_EsquemaEHostSemDiferenciarMaiusculas_CaminhoDiferencia()
    {
        // RN09: "HTTPS://Exemplo.com/a" = "https://exemplo.com/a"; ".../A" é outra.
        await CriarGrupoAdmin();
        await CriarEntidadeAdmin("site", escopoEquipe: false);
        await CriarCampoAdmin("site", Campo("endereco", "URL", obrigatorio: true));
        await CriarRestricaoAdmin("site", "uq_site_endereco", "endereco");
        await CriarNoGrupo("site", new JsonObject { ["endereco"] = "HTTPS://Exemplo.com/a" });

        var repetida = await Cliente.PostAsJsonAsync(UrlUso("site"), new JsonObject { ["endereco"] = "https://exemplo.com/a" });
        var outroCaminho = await Cliente.PostAsJsonAsync(UrlUso("site"), new JsonObject { ["endereco"] = "https://exemplo.com/A" });

        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Equal("CONFLITO_UNICIDADE", (await LerJson(repetida))["codigo"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Created, outroCaminho.StatusCode);
    }

    [Fact]
    public async Task Admin_CampoUrlNoCampoDeExibicao_Permitido()
    {
        // Diferente do TEXTO_LONGO, a URL pode ser o rótulo (e coluna das tabelas).
        await CriarGrupoAdmin();
        await CriarEntidadeAdmin("site", escopoEquipe: false);
        await CriarCampoAdmin("site", Campo("endereco", "URL", obrigatorio: true));

        await DefinirCampoExibicao("site", "endereco");

        Assert.Equal("endereco", EntidadeNoEstado("site")["campoExibicao"]!.GetValue<string>());
    }

    private static List<string> Ids(JsonNode lista) => lista["itens"]!.AsArray().Select(i => i!["id"]!.GetValue<string>()).ToList();
}
