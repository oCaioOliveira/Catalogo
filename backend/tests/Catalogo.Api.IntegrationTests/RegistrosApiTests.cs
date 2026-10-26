using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;

namespace Catalogo.Api.IntegrationTests;

public class RegistrosApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Fact]
    public async Task Post_SiglaApontandoParaComunidade_Devolve201ComLocationEGetDevolveDadosPlanos()
    {
        // RN07: a referência é gravada em RegistroReferencia, mas volta junto dos campos simples.
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade Ágil" });

        var resposta = await Postar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "CAG" });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var criado = await LerJson(resposta);
        var id = criado["id"]!.GetValue<string>();
        Assert.Equal($"{UrlRegistros("sigla")}/{id}", resposta.Headers.Location!.AbsolutePath);

        var buscado = await LerJson(await Cliente.GetAsync(resposta.Headers.Location));

        Assert.Equal(id, buscado["id"]!.GetValue<string>());
        // RN20: a referência vem como { id, rotulo }; o rótulo é o nome da comunidade.
        Assert.Equal(comunidade, buscado["dados"]!["comunidade"]!["id"]!.GetValue<string>());
        Assert.Equal("Comunidade Ágil", buscado["dados"]!["comunidade"]!["rotulo"]!.GetValue<string>());
        Assert.Equal("CAG", buscado["dados"]!["codigo_sigla"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(buscado["versao"]!.GetValue<string>()));
        Assert.EndsWith("Z", buscado["criadoEm"]!.GetValue<string>());
    }

    [Fact]
    public async Task Post_ComunidadeComMesmoNomeComAcentoECaixaDiferentes_Devolve409()
    {
        // RN09
        await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade Ágil" });

        var resposta = await Postar("comunidade", new JsonObject { ["nome"] = "  COMUNIDADE AGIL " });

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        var problema = await LerJson(resposta);
        Assert.Equal("uq_comunidade_nome", problema["detalhes"]!["restricao"]!.GetValue<string>());
        Assert.Equal("nome", problema["detalhes"]!["campos"]![0]!.GetValue<string>());
        Assert.NotNull(problema["traceId"]);
    }

    [Fact]
    public async Task Post_SiglaComTresProblemas_Devolve400ComOsTresErrosJuntos()
    {
        // RN01 + RN03 + RN04, devolvidos juntos (RN05).
        var resposta = await Postar("sigla", new JsonObject
        {
            ["codigo_sigla"] = new string('S', 30),
            ["campo_inexistente"] = "x",
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await LerJson(resposta))["errors"]!.AsObject();
        Assert.Equal(["campo_inexistente", "codigo_sigla", "comunidade"], erros.Select(e => e.Key).Order());
        Assert.Contains("20 caracteres", erros["codigo_sigla"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Post_CenarioComEndpointDeOutroProjeto_Devolve400NoCampoDependente()
    {
        // RN08
        var projetoA = await CriarProjeto("A");
        var projetoB = await CriarProjeto("B");
        var relatorioDoA = await CriarRelatorio(projetoA);
        var endpointDoB = await CriarEndpoint(projetoB, "consulta");

        var resposta = await Postar("cenario_teste", DadosDoCenario(relatorioDoA, endpointDoB));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await LerJson(resposta))["errors"]!.AsObject();
        var erro = Assert.Single(erros);
        Assert.Equal("endpoint_servico", erro.Key);
        Assert.Contains("'Projeto'", erro.Value![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Post_CenarioComEndpointDoMesmoProjeto_Devolve201()
    {
        // RN08
        var projeto = await CriarProjeto("A");
        var relatorio = await CriarRelatorio(projeto);
        var endpoint = await CriarEndpoint(projeto, "consulta");

        var resposta = await Postar("cenario_teste", DadosDoCenario(relatorio, endpoint));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var dados = (await LerJson(resposta))["dados"]!;
        Assert.Equal("35.50", dados["vazao_media_tps"]!.ToJsonString());   // DECIMAL com a escala do campo
        Assert.Equal(relatorio, dados["relatorio_teste"]!["id"]!.GetValue<string>());
        Assert.Equal("Bateria 1", dados["relatorio_teste"]!["rotulo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Post_ReferenciaParaRegistroDeOutraEntidade_Devolve400()
    {
        // RN06: o banco também barraria (FK composta), mas a mensagem amigável vem da aplicação.
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "C" });

        var resposta = await Postar("projeto", new JsonObject { ["sigla"] = comunidade, ["nome"] = "P" });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.NotNull((await LerJson(resposta))["errors"]!["sigla"]);
    }

    [Fact]
    public async Task Post_GravacoesSimultaneasDoMesmoValor_SoUmaPassaEAsDemaisDevolvem409()
    {
        // RN09 com concorrência: ou a verificação prévia ou o UNIQUE do banco barra. Nunca 500.
        var tarefas = Enumerable.Range(0, 8)
            .Select(_ => Postar("comunidade", new JsonObject { ["nome"] = "Comunidade Concorrida" }))
            .ToList();
        var respostas = await Task.WhenAll(tarefas);

        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(respostas.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Post_TextoComAcento_FicaLegivelNoBanco()
    {
        var id = await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade Ágil" });

        await using var conexao = new SqlConnection(Banco.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = new SqlCommand($"SELECT Dados FROM {SchemaBanco.Nome}.Registro WHERE Id = @id", conexao);
        comando.Parameters.AddWithValue("@id", Guid.Parse(id));

        Assert.Equal("{\"nome\":\"Comunidade Ágil\"}", (string)(await comando.ExecuteScalarAsync())!);
    }

    [Theory]
    [InlineData("/api/v1/grupos/nao_existe/entidades/comunidade/registros/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [InlineData("/api/v1/grupos/testes_desempenho/entidades/nao_existe/registros/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    [InlineData("/api/v1/grupos/testes_desempenho/entidades/comunidade/registros/3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    public async Task Get_GrupoEntidadeOuRegistroInexistente_Devolve404ComProblemDetails(string url)
    {
        var resposta = await Cliente.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Get_RegistroBuscadoPelaEntidadeErrada_Devolve404()
    {
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "C" });

        var resposta = await Cliente.GetAsync($"{UrlRegistros("rtc")}/{comunidade}");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Post_CorpoQueNaoEhObjetoJson_Devolve400()
    {
        var resposta = await Cliente.PostAsync(UrlRegistros("comunidade"),
            new StringContent("[1, 2]", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
