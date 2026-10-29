using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;

namespace Catalogo.Api.IntegrationTests;

// RN26 (matriz), RN27 (globais), RN28 (404 para outra equipe) e RN29 (referências entre equipes),
// com duas equipes: A (a equipe padrão) e B.
public class PermissoesApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private Equipe EquipeA => EquipePadrao;

    [Fact]
    public async Task Externo_VeASuaEquipeEOsGlobais_404NaOutraE403AoEscrever()
    {
        var (projetoA, projetoB, comunidade) = await PrepararAsync();
        var (_, externo) = await CriarUsuarioAsync("externo", Perfil.Externo, [EquipeA]);

        Assert.Equal(HttpStatusCode.OK, (await externo.GetAsync($"{UrlRegistros("projeto")}/{projetoA}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await externo.GetAsync($"{UrlRegistros("comunidade")}/{comunidade}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await externo.GetAsync($"{UrlRegistros("projeto")}/{projetoB}")).StatusCode);

        var lido = await LerJson(await externo.GetAsync($"{UrlRegistros("projeto")}/{projetoA}"));
        Assert.Equal(HttpStatusCode.Forbidden, (await Postar(externo, "rtc", new JsonObject { ["rtc_externo_id"] = "R1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await externo.PutAsJsonAsync($"{UrlRegistros("projeto")}/{projetoA}",
            new JsonObject { ["versao"] = lido["versao"]!.GetValue<string>(), ["nome"] = "X", ["sigla"] = lido["dados"]!["sigla"]!["id"]!.GetValue<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await externo.DeleteAsync($"{UrlRegistros("projeto")}/{projetoA}")).StatusCode);
    }

    [Fact]
    public async Task Interno_CriaEAlteraNaSuaEquipe_403AoExcluirEAoCriarGlobal()
    {
        var (_, interno) = await CriarUsuarioAsync("interno", Perfil.Interno, [EquipeA]);

        var criado = await Postar(interno, "rtc", new JsonObject { ["rtc_externo_id"] = "R1" });
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        var rtc = await LerJson(criado);
        Assert.Equal(EquipeA.Id.ToString(), rtc["equipeId"]!.GetValue<string>());

        var alterado = await interno.PutAsJsonAsync($"{UrlRegistros("rtc")}/{rtc["id"]}",
            new JsonObject { ["versao"] = rtc["versao"]!.GetValue<string>(), ["rtc_externo_id"] = "R2" });
        Assert.Equal(HttpStatusCode.OK, alterado.StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await interno.DeleteAsync($"{UrlRegistros("rtc")}/{rtc["id"]}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Postar(interno, "comunidade", new JsonObject { ["nome"] = "Global" })).StatusCode);
    }

    [Fact]
    public async Task Gestor_CriaAlteraEExcluiNaSuaEquipe_404NaOutra()
    {
        var (_, projetoB, _) = await PrepararAsync();
        var (_, gestor) = await CriarUsuarioAsync("gestor", Perfil.Gestor, [EquipeA]);

        var rtc = await LerJson(await Postar(gestor, "rtc", new JsonObject { ["rtc_externo_id"] = "R1" }));
        var alterado = await gestor.PutAsJsonAsync($"{UrlRegistros("rtc")}/{rtc["id"]}",
            new JsonObject { ["versao"] = rtc["versao"]!.GetValue<string>(), ["rtc_externo_id"] = "R2" });
        Assert.Equal(HttpStatusCode.OK, alterado.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await gestor.DeleteAsync($"{UrlRegistros("rtc")}/{rtc["id"]}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await gestor.GetAsync($"{UrlRegistros("projeto")}/{projetoB}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await gestor.DeleteAsync($"{UrlRegistros("projeto")}/{projetoB}")).StatusCode);
    }

    [Fact]
    public async Task Administrador_SemEquipe_CriaEmQualquerEquipeCriaGlobaisEExcluiEmQualquerEquipe()
    {
        var equipeB = await CriarEquipeAsync("Equipe B");

        var emA = await Postar(Cliente, "rtc", new JsonObject { ["rtc_externo_id"] = "R1", ["equipeId"] = EquipeA.Id.ToString() });
        var emB = await Postar(Cliente, "rtc", new JsonObject { ["rtc_externo_id"] = "R1", ["equipeId"] = equipeB.Id.ToString() });
        var global = await Postar(Cliente, "comunidade", new JsonObject { ["nome"] = "Global" });
        var semEquipe = await Postar(Cliente, "rtc", new JsonObject { ["rtc_externo_id"] = "R2" });

        Assert.Equal(HttpStatusCode.Created, emA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, emB.StatusCode);   // mesmo valor único em outra equipe (RN09)
        Assert.Equal(HttpStatusCode.Created, global.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, semEquipe.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Cliente.DeleteAsync($"{UrlRegistros("rtc")}/{(await LerJson(emB))["id"]}")).StatusCode);
    }

    [Fact]
    public async Task ListagemEOpcoes_DeUsuarioSoDaEquipeA_NuncaTrazemRegistrosDaB()
    {
        var (projetoA, projetoB, _) = await PrepararAsync();
        var relatorioA = await CriarRelatorio(projetoA);
        await CriarEndpoint(projetoA, "endpoint-a");
        EquipeDosAtalhos = await EquipeB();
        await CriarEndpoint(projetoB, "endpoint-b");
        var (_, externo) = await CriarUsuarioAsync("externo", Perfil.Externo, [EquipeA]);

        var lista = await LerJson(await externo.GetAsync(UrlRegistros("endpoint_servico")));
        var opcoesEndpoint = await LerJson(await externo.GetAsync(
            $"{UrlGrupo}/entidades/cenario_teste/campos/endpoint_servico/opcoes?dependeDe={relatorioA}"));
        var opcoesProjeto = await LerJson(await externo.GetAsync($"{UrlGrupo}/entidades/endpoint_servico/campos/projeto/opcoes"));

        Assert.Equal(["endpoint-a"], lista["itens"]!.AsArray().Select(i => i!["dados"]!["nome_servico"]!.GetValue<string>()));
        Assert.Equal(["endpoint-a"], opcoesEndpoint["itens"]!.AsArray().Select(i => i!["rotulo"]!.GetValue<string>()));
        Assert.Equal([projetoA], opcoesProjeto["itens"]!.AsArray().Select(i => i!["id"]!.GetValue<string>()));
        Assert.Equal(HttpStatusCode.Forbidden, (await externo.GetAsync($"{UrlRegistros("projeto")}?equipeId={(await EquipeB()).Id}")).StatusCode);
    }

    private Equipe? _equipeB;

    private async Task<Equipe> EquipeB() => _equipeB ??= await CriarEquipeAsync("Equipe B");

    /// <summary>Um projeto na A, um na B e uma comunidade global (criados pelo administrador).</summary>
    private async Task<(string ProjetoA, string ProjetoB, string Comunidade)> PrepararAsync()
    {
        var projetoA = await CriarProjeto("Projeto A");
        EquipeDosAtalhos = await EquipeB();
        var projetoB = await CriarProjeto("Projeto B");
        EquipeDosAtalhos = EquipePadrao;
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade Global" });
        return (projetoA, projetoB, comunidade);
    }
}
