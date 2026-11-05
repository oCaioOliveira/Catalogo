using System.Net;
using System.Net.Http.Json;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

// RN23 (login pelo Windows, docs/adr/0054) e RN105 (primeiro acesso). O usuário do AD é simulado por AutenticacaoWindowsDeTeste.
public class AutenticacaoApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private const string UrlLogin = "/api/v1/auth/login";

    [Fact]
    public async Task Login_UsuarioDoWindowsCadastrado_DevolveTokenQueAbreAApi()
    {
        var (maria, _) = await CriarUsuarioAsync("C161587", Perfil.Externo, [EquipePadrao]);

        var resposta = await LoginWindows(@"CORPCAIXA\c161587");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await LerJson(resposta);
        Assert.Equal(maria.Id.ToString(), corpo["usuario"]!["id"]!.GetValue<string>());
        Assert.Equal("EXTERNO", corpo["usuario"]!["perfil"]!.GetValue<string>());
        Assert.Equal(EquipePadrao.Nome, corpo["usuario"]!["equipes"]![0]!["nome"]!.GetValue<string>());
        Assert.Null(corpo["deveTrocarSenha"]);
        using var cliente = ClienteComToken(corpo["token"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"{UrlGrupo}/entidades")).StatusCode);
        Assert.Equal("C161587", (await LerJson(await cliente.GetAsync("/api/v1/auth/eu")))["matricula"]!.GetValue<string>());
    }

    [Fact]
    public async Task Login_RegistraOUltimoAcesso()
    {
        var (maria, _) = await CriarUsuarioAsync("C161587", Perfil.Interno, [EquipePadrao]);
        var antes = DateTime.UtcNow.AddSeconds(-5);

        await LoginWindows(@"CORPCAIXA\C161587");

        var usuario = await LerUsuarioAsync("C161587");
        Assert.Equal(maria.Id, usuario.Id);
        Assert.True(usuario.UltimoAcessoEm >= antes);
    }

    [Fact]
    public async Task Login_MatriculaNova_CriaUsuarioExternoSemEquipe()
    {
        // RN105: perfis e equipes continuam com o administrador.
        var resposta = await LoginWindows(@"CORPCAIXA\c999001");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var corpo = await LerJson(resposta);
        Assert.Equal("C999001", corpo["usuario"]!["matricula"]!.GetValue<string>());
        Assert.Equal("EXTERNO", corpo["usuario"]!["perfil"]!.GetValue<string>());
        Assert.Empty(corpo["usuario"]!["equipes"]!.AsArray());

        var usuario = await LerUsuarioAsync("C999001");
        Assert.Equal(Perfil.Externo, usuario.Perfil);
        Assert.True(usuario.Ativo);
        Assert.Empty(usuario.Equipes);

        // O cadastro fica no histórico do usuário, feito por ele mesmo (RN32).
        var historico = await LerJson(await Cliente.GetAsync($"/api/v1/usuarios/{usuario.Id}/historico"));
        var linha = Assert.Single(historico["itens"]!.AsArray())!;
        Assert.Equal("CRIACAO", linha["operacao"]!.GetValue<string>());
        Assert.Equal("C999001", linha["usuario"]!["matricula"]!.GetValue<string>());
    }

    [Fact]
    public async Task Login_MesmaMatriculaDeNovo_NaoCriaOutroUsuario()
    {
        var primeiro = await LerJson(await LoginWindows(@"CORPCAIXA\c999001"));
        var segundo = await LerJson(await LoginWindows("c999001@corpcaixa.local"));

        Assert.Equal(primeiro["usuario"]!["id"]!.GetValue<string>(), segundo["usuario"]!["id"]!.GetValue<string>());
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        Assert.Equal(1, await contexto.Usuarios.CountAsync(u => u.Matricula == "C999001"));
    }

    [Fact]
    public async Task Login_UsuarioDesativado_Devolve403()
    {
        var (usuario, _) = await CriarUsuarioAsync("C161587", Perfil.Externo, [EquipePadrao]);
        await Cliente.PatchAsJsonAsync($"/api/v1/usuarios/{usuario.Id}/ativacao", new { ativo = false });

        var resposta = await LoginWindows(@"CORPCAIXA\c161587");

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        var problema = await LerJson(resposta);
        Assert.Equal("SEM_PERMISSAO", problema["codigo"]!.GetValue<string>());
        Assert.Null(problema["token"]);
    }

    [Fact]
    public async Task Login_SemCredencialDoWindows_Devolve401ComDesafioNegotiate()
    {
        var resposta = await ClienteAnonimo.PostAsync(UrlLogin, null);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Contains("Negotiate", resposta.Headers.WwwAuthenticate.ToString());
        Assert.Equal("NAO_AUTENTICADO", (await LerJson(resposta))["codigo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Login_ComUmTokenEmVezDoWindows_Devolve401()
    {
        // O JWT abre o resto da API, mas não o login: só a autenticação do Windows emite token.
        var resposta = await Cliente.PostAsync(UrlLogin, null);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_NomeDoWindowsSemMatriculaValida_Devolve401()
    {
        var resposta = await LoginWindows(@"CORPCAIXA\" + new string('x', MatriculaWindows.TamanhoMaximo + 1));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("NAO_AUTENTICADO", (await LerJson(resposta))["codigo"]!.GetValue<string>());
    }

    [Fact]
    public async Task LoginPorSenha_NaoExisteMais()
    {
        // Matrícula e senha no corpo não autenticam: só vale o usuário do Windows. A troca de senha também saiu.
        var resposta = await ClienteAnonimo.PostAsJsonAsync(UrlLogin, new { matricula = "admin.testes", senha = "SenhaDeTeste123" });

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente.PostAsJsonAsync("/api/v1/auth/trocar-senha", new { })).StatusCode);
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

    private async Task<Usuario> LerUsuarioAsync(string matricula)
    {
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        return await contexto.Usuarios.AsNoTracking().Include(u => u.Equipes).SingleAsync(u => u.Matricula == matricula);
    }
}
