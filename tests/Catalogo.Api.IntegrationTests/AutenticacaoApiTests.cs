using System.Net;
using System.Net.Http.Json;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;
using Microsoft.AspNetCore.Hosting;

namespace Catalogo.Api.IntegrationTests;

// RN23 (login), RN24 (bloqueio) e RN25 (troca obrigatória de senha).
public class AutenticacaoApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private const string UrlLogin = "/api/v1/auth/login";

    [Fact]
    public async Task Login_Certo_DevolveTokenQueAbreAApi()
    {
        await CriarUsuarioAsync("maria", Perfil.Externo, [EquipePadrao]);

        var resposta = await ClienteAnonimo.PostAsJsonAsync(UrlLogin, new { matricula = "MARIA", senha = SenhaPadrao });

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await LerJson(resposta);
        Assert.Equal("EXTERNO", corpo["usuario"]!["perfil"]!.GetValue<string>());
        Assert.Equal(EquipePadrao.Nome, corpo["usuario"]!["equipes"]![0]!["nome"]!.GetValue<string>());
        using var cliente = ClienteComToken(corpo["token"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"{UrlGrupo}/entidades")).StatusCode);
    }

    [Fact]
    public async Task Login_CincoSenhasErradas_BloqueiaInclusiveComASenhaCerta()
    {
        // RN24
        await CriarUsuarioAsync("maria", Perfil.Externo, [EquipePadrao]);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Login("maria", "errada")).StatusCode);

        var comASenhaCerta = await Login("maria", SenhaPadrao);

        Assert.Equal(HttpStatusCode.Unauthorized, comASenhaCerta.StatusCode);
        Assert.Equal("Matrícula ou senha inválidas.", (await LerJson(comASenhaCerta))["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Login_MatriculaInexistenteESenhaErrada_TemAMesmaResposta()
    {
        // RN23: não revela se a matrícula existe.
        await CriarUsuarioAsync("maria", Perfil.Externo, [EquipePadrao]);

        var inexistente = await LerJson(await Login("ninguem", SenhaPadrao));
        var senhaErrada = await LerJson(await Login("maria", "errada"));

        Assert.Equal(inexistente["detail"]!.ToJsonString(), senhaErrada["detail"]!.ToJsonString());
        Assert.Equal(inexistente["title"]!.ToJsonString(), senhaErrada["title"]!.ToJsonString());
    }

    [Fact]
    public async Task Login_UsuarioInativo_NaoEntra()
    {
        var (usuario, _) = await CriarUsuarioAsync("maria", Perfil.Externo, [EquipePadrao]);
        await Cliente.PatchAsJsonAsync($"/api/v1/usuarios/{usuario.Id}/ativacao", new { ativo = false });

        Assert.Equal(HttpStatusCode.Unauthorized, (await Login("maria", SenhaPadrao)).StatusCode);
    }

    [Fact]
    public async Task UsuarioDesativadoComTokenValido_PerdeOAcessoNaHora()
    {
        // RN23: a situação é lida a cada requisição; o administrador descarta o cache ao desativar.
        var (usuario, cliente) = await CriarUsuarioAsync("maria", Perfil.Externo, [EquipePadrao]);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"{UrlGrupo}/entidades")).StatusCode);

        await Cliente.PatchAsJsonAsync($"/api/v1/usuarios/{usuario.Id}/ativacao", new { ativo = false });

        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync($"{UrlGrupo}/entidades")).StatusCode);
    }

    [Fact]
    public async Task SenhaProvisoria_OutrosEndpointsDevolvem403AteATroca()
    {
        // RN25
        var (_, cliente) = await CriarUsuarioAsync("maria", Perfil.Interno, [EquipePadrao], deveTrocarSenha: true);

        var bloqueado = await cliente.GetAsync($"{UrlGrupo}/entidades");
        Assert.Equal(HttpStatusCode.Forbidden, bloqueado.StatusCode);
        Assert.Equal("TROCA_DE_SENHA_OBRIGATORIA", (await LerJson(bloqueado))["codigo"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/v1/auth/eu")).StatusCode);

        var troca = await cliente.PostAsJsonAsync("/api/v1/auth/trocar-senha", new { senhaAtual = SenhaPadrao, novaSenha = "NovaSenha2026" });

        Assert.Equal(HttpStatusCode.NoContent, troca.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"{UrlGrupo}/entidades")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login("maria", "NovaSenha2026")).StatusCode);
    }

    [Fact]
    public async Task TrocarSenha_NovaFracaOuIgual_Devolve400()
    {
        // RN25
        var (_, cliente) = await CriarUsuarioAsync("maria", Perfil.Interno, [EquipePadrao], deveTrocarSenha: true);

        var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/trocar-senha", new { senhaAtual = SenhaPadrao, novaSenha = SenhaPadrao });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.NotNull((await LerJson(resposta))["errors"]!["novaSenha"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("token-invalido")]
    public async Task SemTokenOuComTokenInvalido_Devolve401(string? token)
    {
        using var cliente = token is null ? Banco.Fabrica.CreateClient() : ClienteComToken(token);

        var resposta = await cliente.GetAsync(UrlRegistros("comunidade"));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Login_AcimaDoLimitePorMinuto_Devolve429()
    {
        // RN23: rate limiting do login (limite baixo só neste teste).
        await using var fabrica = Banco.Fabrica.WithWebHostBuilder(b => b.UseSetting("Seguranca:LoginLimitePorMinuto", "2"));
        using var cliente = fabrica.CreateClient();

        var status = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
            status.Add((await cliente.PostAsJsonAsync(UrlLogin, new { matricula = "x", senha = "y" })).StatusCode);

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], status);
    }

    private Task<HttpResponseMessage> Login(string matricula, string senha) =>
        ClienteAnonimo.PostAsJsonAsync(UrlLogin, new { matricula, senha });
}
