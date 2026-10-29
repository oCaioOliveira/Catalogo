using System.Net;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;

namespace Catalogo.Api.IntegrationTests;

// PUT — RN12 (concorrência) e RN15 (substituição do registro inteiro).
public class AtualizarRegistroApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Fact]
    public async Task Put_ComVersaoAtual_Devolve200ComNovaVersaoEAtualizadoEm()
    {
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "C" });
        var sigla = await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "ANTIGO" });
        var lido = await Ler("sigla", sigla);

        var resposta = await Atualizar("sigla", sigla, ComVersao(lido, new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "NOVO" }));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var atualizado = await LerJson(resposta);
        Assert.Equal("NOVO", atualizado["dados"]!["codigo_sigla"]!.GetValue<string>());
        Assert.NotEqual(lido["versao"]!.GetValue<string>(), atualizado["versao"]!.GetValue<string>());
        Assert.NotNull(atualizado["atualizadoEm"]);
        Assert.Equal("C", atualizado["dados"]!["comunidade"]!["rotulo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Put_ComVersaoAntiga_Devolve409()
    {
        // RN12: dois usuários leram a mesma versão; o segundo a gravar recebe 409.
        var id = await Criar("comunidade", new JsonObject { ["nome"] = "Original" });
        var lidoPorAmbos = await Ler("comunidade", id);
        var primeira = await Atualizar("comunidade", id, ComVersao(lidoPorAmbos, new JsonObject { ["nome"] = "Primeiro" }));
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);

        var segunda = await Atualizar("comunidade", id, ComVersao(lidoPorAmbos, new JsonObject { ["nome"] = "Segundo" }));

        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
        Assert.Equal("Conflito de concorrência.", (await LerJson(segunda))["title"]!.GetValue<string>());
        Assert.Equal("Primeiro", (await Ler("comunidade", id))["dados"]!["nome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Put_SemCampoOpcional_RemoveOCampoDoRegistro()
    {
        // RN15: campo opcional ausente no corpo = campo removido.
        var id = await Criar("ambiente", new JsonObject { ["nome"] = "HML", ["servidor_gerenciador_filas"] = "mq01" });
        var lido = await Ler("ambiente", id);

        var resposta = await Atualizar("ambiente", id, ComVersao(lido, new JsonObject { ["nome"] = "HML" }));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var dados = (await Ler("ambiente", id))["dados"]!.AsObject();
        Assert.False(dados.ContainsKey("servidor_gerenciador_filas"));
        // A busca também deixa de encontrá-lo pelo campo removido (RegistroValor sincronizado).
        Assert.Equal(0, (await ListarOk("ambiente", "servidor_gerenciador_filas=mq01"))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Put_CodigoQueJaExisteEmOutraSigla_Devolve409EMantendoOProprio_Devolve200()
    {
        // RN09 + RN15: conflita com outro registro, nunca consigo mesmo.
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "C" });
        await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "OCUPADO" });
        var sigla = await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "MEU" });
        var lido = await Ler("sigla", sigla);

        var conflito = await Atualizar("sigla", sigla, ComVersao(lido, new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "ocupado" }));
        var mantendo = await Atualizar("sigla", sigla, ComVersao(lido, new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "meu" }));

        Assert.Equal(HttpStatusCode.Conflict, conflito.StatusCode);
        Assert.Equal("uq_sigla_codigo", (await LerJson(conflito))["detalhes"]!["restricao"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, mantendo.StatusCode);
    }

    [Fact]
    public async Task Put_CenarioComEndpointDeOutroProjeto_Devolve400NaDependencia()
    {
        // RN08 também vale no PUT.
        var projetoA = await CriarProjeto("A");
        var projetoB = await CriarProjeto("B");
        var relatorio = await CriarRelatorio(projetoA);
        var endpointA = await CriarEndpoint(projetoA, "a");
        var endpointB = await CriarEndpoint(projetoB, "b");
        var cenario = await Criar("cenario_teste", DadosDoCenario(relatorio, endpointA));
        var lido = await Ler("cenario_teste", cenario);

        var resposta = await Atualizar("cenario_teste", cenario, ComVersao(lido, DadosDoCenario(relatorio, endpointB)));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["endpoint_servico"], (await LerJson(resposta))["errors"]!.AsObject().Select(e => e.Key));
    }

    [Fact]
    public async Task Put_SemVersao_Devolve400NaChaveVersao()
    {
        var id = await Criar("comunidade", new JsonObject { ["nome"] = "C" });

        var resposta = await Atualizar("comunidade", id, new JsonObject { ["nome"] = "D" });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.NotNull((await LerJson(resposta))["errors"]!["versao"]);
    }

    [Fact]
    public async Task Put_RegistroInexistente_Devolve404()
    {
        var resposta = await Atualizar("comunidade", Guid.NewGuid().ToString(), new JsonObject { ["versao"] = "AA==", ["nome"] = "C" });

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
