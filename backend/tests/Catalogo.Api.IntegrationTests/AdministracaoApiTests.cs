using System.Net;
using System.Net.Http.Json;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

// Administração de equipes e usuários (só ADMINISTRADOR): RN25, RN30, RN31 e RN32.
public class AdministracaoApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Theory]
    [InlineData(Perfil.Externo)]
    [InlineData(Perfil.Interno)]
    [InlineData(Perfil.Gestor)]
    public async Task NaoAdministrador_Recebe403EmUsuariosEEquipes(Perfil perfil)
    {
        var (_, cliente) = await CriarUsuarioAsync("usuario", perfil, [EquipePadrao]);

        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/v1/usuarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/v1/equipes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync("/api/v1/equipes", new { nome = "X" })).StatusCode);
    }

    [Fact]
    public async Task Administrador_CriaEquipesEUsuariosDeCadaPerfilEEmDuasEquipes()
    {
        var equipeA = await LerJson(await Cliente.PostAsJsonAsync("/api/v1/equipes", new { nome = "Equipe Alfa" }));
        var equipeB = await LerJson(await Cliente.PostAsJsonAsync("/api/v1/equipes", new { nome = "Equipe Beta" }));
        var idA = equipeA["id"]!.GetValue<string>();
        var idB = equipeB["id"]!.GetValue<string>();

        foreach (var perfil in new[] { "EXTERNO", "INTERNO", "GESTOR" })
        {
            var resposta = await Cliente.PostAsJsonAsync("/api/v1/usuarios",
                new { matricula = perfil.ToLowerInvariant(), nome = perfil, perfil, equipes = new[] { idA } });
            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        }

        var duasEquipes = await Cliente.PostAsJsonAsync("/api/v1/usuarios",
            new { matricula = "duas", nome = "Duas equipes", perfil = "INTERNO", equipes = new[] { idA, idB } });
        var semEquipe = await Cliente.PostAsJsonAsync("/api/v1/usuarios",
            new { matricula = "sem", nome = "Sem equipe", perfil = "INTERNO", equipes = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Created, duasEquipes.StatusCode);
        Assert.Equal(2, (await LerJson(duasEquipes))["usuario"]!["equipes"]!.AsArray().Count);
        Assert.Equal(HttpStatusCode.BadRequest, semEquipe.StatusCode);   // RN31
        var lista = await LerJson(await Cliente.GetAsync($"/api/v1/usuarios?equipeId={idB}"));
        Assert.Equal(1, lista["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task SenhaProvisoria_FuncionaUmaVezEForcaATroca()
    {
        // RN25
        var criado = await LerJson(await Cliente.PostAsJsonAsync("/api/v1/usuarios",
            new { matricula = "novo", nome = "Novo", perfil = "INTERNO", equipes = new[] { EquipePadrao.Id } }));
        var provisoria = criado["senhaProvisoria"]!.GetValue<string>();

        var login = await LerJson(await ClienteAnonimo.PostAsJsonAsync("/api/v1/auth/login", new { matricula = "novo", senha = provisoria }));
        Assert.True(login["deveTrocarSenha"]!.GetValue<bool>());
        using var cliente = ClienteComToken(login["token"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync(UrlRegistros("comunidade"))).StatusCode);

        await cliente.PostAsJsonAsync("/api/v1/auth/trocar-senha", new { senhaAtual = provisoria, novaSenha = "MinhaSenha2026" });

        var deNovoComAProvisoria = await ClienteAnonimo.PostAsJsonAsync("/api/v1/auth/login", new { matricula = "novo", senha = provisoria });
        Assert.Equal(HttpStatusCode.Unauthorized, deNovoComAProvisoria.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(UrlRegistros("comunidade"))).StatusCode);
    }

    [Fact]
    public async Task UltimoAdministrador_NaoPodeSerDesativadoNemRebaixado()
    {
        // RN30
        var desativar = await Cliente.PatchAsJsonAsync($"/api/v1/usuarios/{Administrador.Id}/ativacao", new { ativo = false });
        var rebaixar = await Cliente.PutAsJsonAsync($"/api/v1/usuarios/{Administrador.Id}",
            new { nome = "Admin", perfil = "GESTOR", equipes = new[] { EquipePadrao.Id } });

        Assert.Equal(HttpStatusCode.Conflict, desativar.StatusCode);
        Assert.Equal("ULTIMO_ADMINISTRADOR", (await LerJson(desativar))["codigo"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Conflict, rebaixar.StatusCode);
    }

    [Fact]
    public async Task UltimaEquipeAtivaDeUmUsuario_NaoPodeSerDesativada()
    {
        // RN31
        await CriarUsuarioAsync("interno", Perfil.Interno, [EquipePadrao]);

        var resposta = await Cliente.PatchAsJsonAsync($"/api/v1/equipes/{EquipePadrao.Id}/ativacao", new { ativa = false });

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task RespostasEHistoricoDeUsuarios_NuncaTrazemSenhaNemHash()
    {
        // RN32: confere no JSON BRUTO.
        var criado = await Cliente.PostAsJsonAsync("/api/v1/usuarios",
            new { matricula = "novo", nome = "Novo", perfil = "INTERNO", equipes = new[] { EquipePadrao.Id } });
        var corpoCriacao = await criado.Content.ReadAsStringAsync();
        var id = (await LerJson(criado))["usuario"]!["id"]!.GetValue<string>();
        var redefinicao = await (await Cliente.PostAsync($"/api/v1/usuarios/{id}/redefinir-senha", null)).Content.ReadAsStringAsync();

        string hash;
        using (var escopo = Banco.Fabrica.Services.CreateScope())
            hash = await escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>().Usuarios
                .Where(u => u.Id == Guid.Parse(id)).Select(u => u.SenhaHash).SingleAsync();

        var respostas = new[]
        {
            corpoCriacao,
            await Cliente.GetStringAsync($"/api/v1/usuarios/{id}"),
            await Cliente.GetStringAsync("/api/v1/usuarios"),
            await Cliente.GetStringAsync($"/api/v1/usuarios/{id}/historico"),
        };

        foreach (var corpo in respostas)
        {
            Assert.DoesNotContain(hash, corpo);
            Assert.DoesNotContain("senhaHash", corpo, StringComparison.OrdinalIgnoreCase);
        }

        // A senha provisória aparece SÓ na resposta que a gerou.
        var provisoriaRedefinida = System.Text.Json.Nodes.JsonNode.Parse(redefinicao)!["senhaProvisoria"]!.GetValue<string>();
        Assert.All(respostas, corpo => Assert.DoesNotContain(provisoriaRedefinida, corpo));
        Assert.Contains("\"para\":\"redefinida\"", respostas[3]);
    }

    [Fact]
    public async Task HistoricoDoUsuario_RegistraMudancaDeEquipes()
    {
        var equipeB = await CriarEquipeAsync("Equipe B");
        var (usuario, _) = await CriarUsuarioAsync("interno", Perfil.Interno, [EquipePadrao]);

        await Cliente.PutAsJsonAsync($"/api/v1/usuarios/{usuario.Id}",
            new { nome = usuario.Nome, perfil = "INTERNO", equipes = new[] { EquipePadrao.Id, equipeB.Id } });

        var historico = await LerJson(await Cliente.GetAsync($"/api/v1/usuarios/{usuario.Id}/historico"));
        var mudanca = historico["itens"]![0]!["mudancas"]!.AsArray().Single(m => m!["campo"]!.GetValue<string>() == "equipes")!;
        Assert.Single(mudanca["de"]!.AsArray());
        Assert.Equal(2, mudanca["para"]!.AsArray().Count);
    }
}
