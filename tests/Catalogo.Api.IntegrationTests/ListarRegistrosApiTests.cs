using System.Net;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;

namespace Catalogo.Api.IntegrationTests;

// GET lista — RN17 (paginação), RN18 (filtros), RN19 (ordenação) e RN20 (rótulos).
public class ListarRegistrosApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Fact]
    public async Task Get_Paginado_DevolvePaginaETotaisCorretos()
    {
        for (var i = 1; i <= 5; i++)
            await Criar("comunidade", new JsonObject { ["nome"] = $"Comunidade {i}" });

        var pagina2 = await ListarOk("comunidade", "tamanho=2&pagina=2&ordenacao=nome");
        var pagina3 = await ListarOk("comunidade", "tamanho=2&pagina=3&ordenacao=nome");

        Assert.Equal(5, pagina2["total"]!.GetValue<int>());
        Assert.Equal(3, pagina2["totalPaginas"]!.GetValue<int>());
        Assert.Equal(2, pagina2["pagina"]!.GetValue<int>());
        Assert.Equal(2, pagina2["tamanho"]!.GetValue<int>());
        Assert.Equal(["Comunidade 3", "Comunidade 4"], Nomes(pagina2));
        Assert.Equal(["Comunidade 5"], Nomes(pagina3));
    }

    [Fact]
    public async Task Get_SemOrdenacao_UsaMaisRecentesPrimeiro()
    {
        await Criar("comunidade", new JsonObject { ["nome"] = "Primeira" });
        await Criar("comunidade", new JsonObject { ["nome"] = "Segunda" });

        Assert.Equal(["Segunda", "Primeira"], Nomes(await ListarOk("comunidade")));
    }

    [Fact]
    public async Task Get_OrdenacaoPorDecimal_ComparaComoNumero()
    {
        // RN19: 9.5 antes de 10 (como texto, "10" viria antes de "9.5").
        var (relatorio, endpoint) = await PrepararCenarios();
        await Criar("cenario_teste", DadosDoCenario(relatorio, endpoint, 1, vazao: 10));
        await Criar("cenario_teste", DadosDoCenario(relatorio, endpoint, 2, vazao: 9.5));
        await Criar("cenario_teste", DadosDoCenario(relatorio, endpoint, 3, vazao: 100));

        var crescente = await ListarOk("cenario_teste", "ordenacao=vazao_media_tps");
        var decrescente = await ListarOk("cenario_teste", "ordenacao=-vazao_media_tps");

        Assert.Equal(["9.50", "10.00", "100.00"], Valores(crescente, "vazao_media_tps"));
        Assert.Equal(["100.00", "10.00", "9.50"], Valores(decrescente, "vazao_media_tps"));
    }

    [Fact]
    public async Task Get_IntervaloDeDataExecucao_IncluiAsPontas()
    {
        var projeto = await CriarProjeto("A");
        await CriarRelatorio(projeto, "2026-09-30T23:00:00Z", "Setembro");
        await CriarRelatorio(projeto, "2026-10-01T00:00:00Z", "Primeiro dia");
        await CriarRelatorio(projeto, "2026-10-31T23:59:00Z", "Último dia");
        await CriarRelatorio(projeto, "2026-11-01T00:00:00Z", "Novembro");

        var outubro = await ListarOk("relatorio_teste", "data_execucao.de=2026-10-01&data_execucao.ate=2026-10-31&ordenacao=data_execucao");

        Assert.Equal(["Primeiro dia", "Último dia"], Valores(outubro, "titulo"));
    }

    [Fact]
    public async Task Get_ContemEIgual_NaoDiferenciamMaiusculasNemAcentos()
    {
        await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade Ágil" });
        await Criar("comunidade", new JsonObject { ["nome"] = "Outra" });

        Assert.Equal(["Comunidade Ágil"], Nomes(await ListarOk("comunidade", "nome.contem=AGIL")));
        Assert.Equal(["Comunidade Ágil"], Nomes(await ListarOk("comunidade", "nome=comunidade%20agil")));
    }

    [Fact]
    public async Task Get_FiltroPorReferencia_UsaRegistroReferencia()
    {
        var projetoA = await CriarProjeto("A");
        var projetoB = await CriarProjeto("B");
        await CriarEndpoint(projetoA, "do-a");
        await CriarEndpoint(projetoB, "do-b");

        var lista = await ListarOk("endpoint_servico", $"projeto={projetoA}");

        Assert.Equal(["do-a"], Valores(lista, "nome_servico"));
    }

    [Fact]
    public async Task Get_ContemEmTextoLongoAcimaDe4000Caracteres_Encontra()
    {
        // configuracoes_infraestrutura é TEXTO_LONGO (nvarchar(max)); o trecho buscado está depois do caractere 4000.
        var textoLongo = new string('x', 4500) + "marcador-final";
        await Criar("ambiente", new JsonObject { ["nome"] = "Longo", ["configuracoes_infraestrutura"] = textoLongo });
        await Criar("ambiente", new JsonObject { ["nome"] = "Curto", ["configuracoes_infraestrutura"] = "curto" });

        var lista = await ListarOk("ambiente", "configuracoes_infraestrutura.contem=MARCADOR-FINAL");

        Assert.Equal(["Longo"], Nomes(lista));
    }

    [Fact]
    public async Task Get_ChaveDesconhecida_Devolve400NoMesmoFormatoDeErroPorCampo()
    {
        var resposta = await Listar("comunidade", "nao_existe=1&tamanho=500");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await LerJson(resposta))["errors"]!.AsObject();
        Assert.Equal(["nao_existe", "tamanho"], erros.Select(e => e.Key).Order());
    }

    [Theory]
    [InlineData("nome=' OR 1=1 --")]
    [InlineData("nome.contem=' OR 1=1 --")]
    [InlineData("nome.contem='; DROP TABLE catalogo.Registro; --")]
    public async Task Get_TentativaDeSqlInjection_DevolveListaVaziaSemErro(string filtro)
    {
        await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade Ágil" });

        var lista = await ListarOk("comunidade", Uri.EscapeDataString(filtro.Split('=')[0]) + "=" + Uri.EscapeDataString(filtro[(filtro.IndexOf('=') + 1)..]));

        Assert.Equal(0, lista["total"]!.GetValue<int>());
        Assert.Empty(lista["itens"]!.AsArray());
        // E a tabela continua lá.
        Assert.Equal(1, (await ListarOk("comunidade"))["total"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("100%", "100% certo")]
    [InlineData("a_b", "a_b")]
    [InlineData("[x]", "[x] colchetes")]
    public async Task Get_ContemComCuringasDoLike_BuscaOsCaracteresLiteralmente(string busca, string esperado)
    {
        foreach (var nome in new[] { "100% certo", "100 certo", "a_b", "axb", "[x] colchetes", "x colchetes" })
            await Criar("comunidade", new JsonObject { ["nome"] = nome });

        var lista = await ListarOk("comunidade", "nome.contem=" + Uri.EscapeDataString(busca));

        Assert.Equal([esperado], Nomes(lista));
    }

    [Fact]
    public async Task Get_Lista_TrazReferenciasComIdERotulo()
    {
        // RN20: rótulos buscados em lote para a página toda.
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade Ágil" });
        await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "S1" });
        await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "S2" });

        var itens = (await ListarOk("sigla"))["itens"]!.AsArray();

        Assert.Equal(2, itens.Count);
        Assert.All(itens, item =>
        {
            Assert.Equal(comunidade, item!["dados"]!["comunidade"]!["id"]!.GetValue<string>());
            Assert.Equal("Comunidade Ágil", item["dados"]!["comunidade"]!["rotulo"]!.GetValue<string>());
        });
    }

    private async Task<(string Relatorio, string Endpoint)> PrepararCenarios()
    {
        var projeto = await CriarProjeto("A");
        return (await CriarRelatorio(projeto), await CriarEndpoint(projeto, "consulta"));
    }

    private static List<string> Nomes(JsonNode lista) => Valores(lista, "nome");

    private static List<string> Valores(JsonNode lista, string campo) =>
        lista["itens"]!.AsArray().Select(i => Texto(i!["dados"]![campo]!)).ToList();

    // Texto como está (sem escapar acentos); números no formato do JSON (ex.: 9.50).
    private static string Texto(JsonNode valor) =>
        valor is JsonValue v && v.TryGetValue<string>(out var texto) ? texto : valor.ToJsonString();
}
