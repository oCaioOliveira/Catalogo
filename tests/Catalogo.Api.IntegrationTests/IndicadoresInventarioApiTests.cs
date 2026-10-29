using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;

namespace Catalogo.Api.IntegrationTests;

/// <summary>
/// Agregação configurável (RN85 a RN87) sobre a listagem Inventário, criada pela administração de metadados: tem o que o seed
/// não tem (um DECIMAL opcional e um TEXTO com muitos valores distintos).
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class IndicadoresInventarioApiTests(BancoDeTestesFixture banco) : TesteDeMetadados(banco)
{
    private string UrlIndicadores => $"/api/v1/painel/grupos/{Grupo}/entidades/equipamento/indicadores";

    [Fact]
    public async Task Media_IgnoraRegistrosSemValor_ContagemContaTodos()
    {
        await CriarInventarioAsync();
        var notebook = await CriarCategoria("Notebook");
        await CriarEquipamento("N1", notebook, valor: 10);
        await CriarEquipamento("N2", notebook, valor: 30);
        await CriarEquipamento("N3", notebook);   // sem valor

        var linha = Assert.Single((await Indicadores("agruparPor=categoria&metricas=contagem,media:valor,soma:valor,minimo:valor,maximo:valor"))["linhas"]!.AsArray())!;

        // RN86: média de 10 e 30 = 20 (N3 não entra); a contagem conta os 3 registros.
        Assert.Equal("Notebook", linha["categoria"]!["rotulo"]!.GetValue<string>());
        Assert.Equal(3, linha["contagem"]!.GetValue<int>());
        Assert.Equal(20m, linha["media:valor"]!.GetValue<decimal>());
        Assert.Equal(40m, linha["soma:valor"]!.GetValue<decimal>());
        Assert.Equal(10m, linha["minimo:valor"]!.GetValue<decimal>());
        Assert.Equal(30m, linha["maximo:valor"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Media_SemNenhumValor_Nula_ERegistrosSemValorNaDimensaoNumGrupoNulo()
    {
        await CriarInventarioAsync();
        var notebook = await CriarCategoria("Notebook");
        await CriarEquipamento("N1", notebook, data: "2026-03-10");
        await CriarEquipamento("N2", notebook);

        var linhas = (await Indicadores("agruparPor=data_aquisicao:mes&metricas=media:valor"))["linhas"]!.AsArray();

        // Sem data: a dimensão vem nula (e primeiro, na ordem crescente). Sem nenhum valor: a média é nula.
        Assert.Equal("""[{"data_aquisicao:mes":null,"contagem":1,"media:valor":null},{"data_aquisicao:mes":"2026-03","contagem":1,"media:valor":null}]""",
            linhas.ToJsonString());
    }

    [Fact]
    public async Task Texto_MaisDe50Valores_OResto_VaiParaOutros()
    {
        await CriarInventarioAsync();
        var notebook = await CriarCategoria("Notebook");
        for (var i = 1; i <= 53; i++)
            await CriarEquipamento($"Equipamento {i:D2}", notebook);

        var resultado = await Indicadores("agruparPor=nome&limite=500");

        // RN85: os 50 mais frequentes (empatados em 1: pela ordem alfabética) e "Outros" com os 3 restantes, por último.
        var linhas = resultado["linhas"]!.AsArray();
        Assert.Equal(51, linhas.Count);
        Assert.Equal("Equipamento 01", linhas[0]!["nome"]!.GetValue<string>());
        Assert.Equal("Equipamento 50", linhas[49]!["nome"]!.GetValue<string>());
        Assert.Equal("Outros", linhas[50]!["nome"]!.GetValue<string>());
        Assert.Equal(3, linhas[50]!["contagem"]!.GetValue<int>());
        Assert.False(resultado["truncado"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Limite_TruncaEAvisa()
    {
        await CriarInventarioAsync();
        var notebook = await CriarCategoria("Notebook");
        for (var i = 1; i <= 4; i++)
            await CriarEquipamento($"E{i}", notebook, valor: i * 10);

        var resultado = await Indicadores("agruparPor=nome&metricas=soma:valor&ordenarPor=-soma:valor&limite=2");

        Assert.True(resultado["truncado"]!.GetValue<bool>());
        Assert.Equal(["E4", "E3"], resultado["linhas"]!.AsArray().Select(l => l!["nome"]!.GetValue<string>()).ToList());
    }

    [Fact]
    public async Task Metrica_PorCaminho_UsaONumeroDoRegistroApontado_EFiltraPorEle()
    {
        // RN89: a vida útil é da CATEGORIA (a entidade apontada); a média é por equipamento.
        await CriarInventarioAsync();
        await CriarCampoAdmin("categoria", Campo("vida_util", "INTEIRO"));
        var notebook = await CriarNoGrupo("categoria", new JsonObject { ["nome"] = "Notebook", ["vida_util"] = 3 });
        var servidor = await CriarNoGrupo("categoria", new JsonObject { ["nome"] = "Servidor", ["vida_util"] = 6 });
        await CriarEquipamento("N1", notebook);
        await CriarEquipamento("N2", notebook);
        await CriarEquipamento("S1", servidor);

        var geral = Assert.Single((await Indicadores("metricas=media:categoria.vida_util,maximo:categoria.vida_util"))["linhas"]!.AsArray())!;
        Assert.Equal(4m, geral["media:categoria.vida_util"]!.GetValue<decimal>());   // (3 + 3 + 6) / 3 equipamentos
        Assert.Equal(6m, geral["maximo:categoria.vida_util"]!.GetValue<decimal>());

        var resultado = await Indicadores("agruparPor=categoria&metricas=media:categoria.vida_util&categoria.vida_util.de=4");
        var linha = Assert.Single(resultado["linhas"]!.AsArray())!;
        Assert.Equal("Servidor", linha["categoria"]!["rotulo"]!.GetValue<string>());
        Assert.Equal("Média de vida_util (Categoria)", resultado["metricas"]![0]!["nome"]!.GetValue<string>());

        var listagem = await Cliente.GetAsync($"{UrlUso("equipamento")}?categoria.vida_util.ate=3");
        Assert.Equal(2, JsonNode.Parse(await listagem.Content.ReadAsStringAsync())!["total"]!.GetValue<int>());
    }

    private async Task<JsonNode> Indicadores(string consulta)
    {
        var resposta = await Cliente.GetAsync($"{UrlIndicadores}?{consulta}");
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.IsSuccessStatusCode, corpo);
        return JsonNode.Parse(corpo)!;
    }
}
