using System.Net;
using Catalogo.Api.IntegrationTests.Apoio;

namespace Catalogo.Api.IntegrationTests;

public class EntidadesApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Fact]
    public async Task Get_GrupoTestesDesempenho_DevolveAs10EntidadesEOs51CamposAtivosDoSeed()
    {
        var resposta = await Cliente.GetAsync($"{UrlGrupo}/entidades");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var entidades = (await LerJson(resposta)).AsArray();
        Assert.Equal(10, entidades.Count);
        // V015 + V016: 52 campos, 51 ativos (dados_originais, do relatório, fica inativo: oculto nas telas).
        Assert.Equal(51, entidades.Sum(e => e!["campos"]!.AsArray().Count));
    }

    [Fact]
    public async Task Get_CenarioTeste_TrazADependenciaDoEndpointEAUnicidadeComposta()
    {
        var entidades = (await LerJson(await Cliente.GetAsync($"{UrlGrupo}/entidades"))).AsArray();
        var cenario = entidades.Single(e => e!["chave"]!.GetValue<string>() == "cenario_teste")!;
        var endpoint = cenario["campos"]!.AsArray().Single(c => c!["chave"]!.GetValue<string>() == "endpoint_servico")!;

        Assert.Equal("REFERENCIA", endpoint["tipoDado"]!.GetValue<string>());
        Assert.Equal("relatorio_teste", endpoint["dependencia"]!["campoVia"]!.GetValue<string>());
        Assert.Equal("projeto", endpoint["dependencia"]!["campoOrigem"]!.GetValue<string>());
        Assert.Equal("projeto", endpoint["dependencia"]!["campoAlvo"]!.GetValue<string>());

        var restricao = Assert.Single(cenario["restricoesUnicas"]!.AsArray())!;
        Assert.Equal("uq_cenario_relatorio_numero_endpoint", restricao["nome"]!.GetValue<string>());
        Assert.Equal(["relatorio_teste", "endpoint_servico", "numero_cenario"], restricao["campos"]!.AsArray().Select(c => c!.GetValue<string>()));
    }

    [Fact]
    public async Task Get_TrazAOrdemDasEntidadesEOAtivoDosCampos()
    {
        // O front monta o menu pela ordem e confere "ativo" sem supor (campos inativos não vêm, RN02).
        var entidades = (await LerJson(await Cliente.GetAsync($"{UrlGrupo}/entidades"))).AsArray();

        var ordens = entidades.Select(e => e!["ordem"]!.GetValue<int>()).ToList();
        Assert.Equal(ordens.Order().ToList(), ordens);
        // V008: o Relatório de Teste (a tela inicial) vem primeiro; depois as principais e os cadastros de apoio.
        Assert.Equal(
            ["relatorio_teste", "projeto", "endpoint_servico", "rtc", "comunidade", "sigla", "ambiente", "status_resultado", "cenario_teste", "link"],
            entidades.Select(e => e!["chave"]!.GetValue<string>()));
        Assert.All(entidades.SelectMany(e => e!["campos"]!.AsArray()), c => Assert.True(c!["ativo"]!.GetValue<bool>()));
    }

    [Fact]
    public async Task Get_TrazONomePluralDasEntidadesDoSeed()
    {
        // V008: o plural vai para os títulos das listagens e o menu; o singular, para "Novo ..." e "Editar ...".
        var entidades = (await LerJson(await Cliente.GetAsync($"{UrlGrupo}/entidades"))).AsArray();

        string? Plural(string chave) => entidades.Single(e => e!["chave"]!.GetValue<string>() == chave)!["nomePlural"]?.GetValue<string>();

        Assert.Equal("Relatórios de Teste", Plural("relatorio_teste"));
        Assert.Equal("Status de Resultado", Plural("status_resultado"));
        Assert.Equal("RTCs", Plural("rtc"));
        Assert.All(entidades, e => Assert.False(string.IsNullOrEmpty(e!["nomePlural"]?.GetValue<string>())));
    }

    [Fact]
    public async Task Get_TrazOIconeDasEntidadesDoSeed()
    {
        // V010 (RN95): o ícone do PrimeIcons, para o menu, os títulos e a página Início.
        var entidades = (await LerJson(await Cliente.GetAsync($"{UrlGrupo}/entidades"))).AsArray();

        string? Icone(string chave) => entidades.Single(e => e!["chave"]!.GetValue<string>() == chave)!["icone"]?.GetValue<string>();

        Assert.Equal("pi-chart-line", Icone("relatorio_teste"));
        Assert.Equal("pi-briefcase", Icone("projeto"));
        Assert.Equal("pi-ticket", Icone("rtc"));
        Assert.Equal("pi-flag", Icone("status_resultado"));
        Assert.All(entidades, e => Assert.StartsWith("pi-", e!["icone"]?.GetValue<string>()));
    }

    [Fact]
    public async Task Get_ChaveDoGrupoEmMaiusculas_EncontraOGrupo()
    {
        // A collation do banco não diferencia maiúsculas.
        var resposta = await Cliente.GetAsync("/api/v1/grupos/TESTES_DESEMPENHO/entidades");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task Get_GrupoInexistente_Devolve404()
    {
        var resposta = await Cliente.GetAsync("/api/v1/grupos/nao_existe/entidades");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
