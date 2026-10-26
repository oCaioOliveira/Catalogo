using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;

namespace Catalogo.Api.IntegrationTests;

// RN27 (equipe na criação) e RN29 (isolamento entre equipes) para quem está em mais de uma equipe.
public class MultiplasEquipesApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Fact]
    public async Task InternoDeAEB_VeAsDuasEExigeEquipeIdAoCriar()
    {
        var equipeB = await CriarEquipeAsync("Equipe B");
        var equipeC = await CriarEquipeAsync("Equipe C");
        var projetoA = await CriarProjeto("PA");
        EquipeDosAtalhos = equipeB;
        var projetoB = await CriarProjeto("PB");
        var (_, interno) = await CriarUsuarioAsync("interno", Perfil.Interno, [EquipePadrao, equipeB]);

        var lista = await LerJson(await interno.GetAsync(UrlRegistros("projeto")));
        Assert.Equal(2, lista["total"]!.GetValue<int>());

        var semEquipe = await Postar(interno, "rtc", new JsonObject { ["rtc_externo_id"] = "R1" });
        var comA = await Postar(interno, "rtc", new JsonObject { ["rtc_externo_id"] = "R1", ["equipeId"] = EquipePadrao.Id.ToString() });
        var comC = await Postar(interno, "rtc", new JsonObject { ["rtc_externo_id"] = "R2", ["equipeId"] = equipeC.Id.ToString() });

        Assert.Equal(HttpStatusCode.BadRequest, semEquipe.StatusCode);
        Assert.NotNull((await LerJson(semEquipe))["errors"]!["equipeId"]);
        Assert.Equal(HttpStatusCode.Created, comA.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, comC.StatusCode);
        Assert.NotEqual(projetoA, projetoB);
    }

    [Fact]
    public async Task RegistroDeAReferenciandoRegistroDeB_Devolve400MesmoComUsuarioNasDuas()
    {
        // RN29: a regra é sobre os registros, não sobre o usuário.
        var equipeB = await CriarEquipeAsync("Equipe B");
        EquipeDosAtalhos = equipeB;
        var projetoB = await CriarProjeto("PB");
        var (_, interno) = await CriarUsuarioAsync("interno", Perfil.Interno, [EquipePadrao, equipeB]);

        var resposta = await Postar(interno, "endpoint_servico", new JsonObject
        {
            ["equipeId"] = EquipePadrao.Id.ToString(),
            ["projeto"] = projetoB,
            ["nome_servico"] = "s",
            ["metodo"] = "GET",
            ["endpoint_url_fila"] = "/s",
            ["ativo"] = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("outra equipe", (await LerJson(resposta))["errors"]!["projeto"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task RemovidoDaEquipeB_PassaAReceber404NosRegistrosDeB()
    {
        // O administrador descarta o cache do usuário ao alterar: vale na hora (no máximo o tempo do cache).
        var equipeB = await CriarEquipeAsync("Equipe B");
        EquipeDosAtalhos = equipeB;
        var projetoB = await CriarProjeto("PB");
        var (usuario, interno) = await CriarUsuarioAsync("interno", Perfil.Interno, [EquipePadrao, equipeB]);
        Assert.Equal(HttpStatusCode.OK, (await interno.GetAsync($"{UrlRegistros("projeto")}/{projetoB}")).StatusCode);

        var alteracao = await Cliente.PutAsJsonAsync($"/api/v1/usuarios/{usuario.Id}",
            new { nome = usuario.Nome, perfil = "INTERNO", equipes = new[] { EquipePadrao.Id } });

        Assert.Equal(HttpStatusCode.OK, alteracao.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await interno.GetAsync($"{UrlRegistros("projeto")}/{projetoB}")).StatusCode);
    }

    [Fact]
    public async Task EquipeBDesativada_PassaAReceber404NosRegistrosDeB()
    {
        var equipeB = await CriarEquipeAsync("Equipe B");
        EquipeDosAtalhos = equipeB;
        var projetoB = await CriarProjeto("PB");
        var (_, interno) = await CriarUsuarioAsync("interno", Perfil.Interno, [EquipePadrao, equipeB]);
        Assert.Equal(HttpStatusCode.OK, (await interno.GetAsync($"{UrlRegistros("projeto")}/{projetoB}")).StatusCode);

        var desativacao = await Cliente.PatchAsJsonAsync($"/api/v1/equipes/{equipeB.Id}/ativacao", new { ativa = false });

        Assert.Equal(HttpStatusCode.OK, desativacao.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await interno.GetAsync($"{UrlRegistros("projeto")}/{projetoB}")).StatusCode);
    }

    [Fact]
    public async Task UsuarioDeUmaUnicaEquipe_CriaSemEquipeIdENaEquipeDele()
    {
        // RN27
        var (_, gestor) = await CriarUsuarioAsync("gestor", Perfil.Gestor, [EquipePadrao]);

        var resposta = await Postar(gestor, "rtc", new JsonObject { ["rtc_externo_id"] = "R1" });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var criado = await LerJson(resposta);
        Assert.Equal(EquipePadrao.Id.ToString(), criado["equipeId"]!.GetValue<string>());
        Assert.Equal("gestor", criado["criadoPor"]!["matricula"]!.GetValue<string>());
    }
}
