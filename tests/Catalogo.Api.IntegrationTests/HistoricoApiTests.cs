using System.Net;
using System.Text.Json.Nodes;
using Catalogo.Api.Configuracao;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Historico;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

// RN32 — histórico de alterações.
public class HistoricoApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Fact]
    public async Task CriarAlterarExcluir_GeramTresEntradasComAntesEDepois()
    {
        var id = await Criar("comunidade", new JsonObject { ["nome"] = "Original" });
        await Atualizar("comunidade", id, ComVersao(await Ler("comunidade", id), new JsonObject { ["nome"] = "Alterado" }));
        await Excluir("comunidade", id);

        var historico = await LerJson(await Cliente.GetAsync($"{UrlRegistros("comunidade")}/{id}/historico"));
        var itens = historico["itens"]!.AsArray();

        Assert.Equal(["EXCLUSAO", "ALTERACAO", "CRIACAO"], itens.Select(i => i!["operacao"]!.GetValue<string>()));
        Assert.All(itens, i => Assert.Equal("admin.testes", i!["usuario"]!["matricula"]!.GetValue<string>()));
        Assert.Equal(Mudanca(null, "Original"), Unica(itens[2]!));
        Assert.Equal(Mudanca("Original", "Alterado"), Unica(itens[1]!));
        Assert.Equal(Mudanca("Alterado", null), Unica(itens[0]!));
    }

    [Fact]
    public async Task ExclusaoEmCascata_GeraUmaEntradaPorRegistroComOMesmoCorrelacaoId()
    {
        var projeto = await CriarProjeto("A");
        var relatorio = await CriarRelatorio(projeto);
        var endpoint = await CriarEndpoint(projeto, "consulta");
        var cenario1 = await Criar("cenario_teste", DadosDoCenario(relatorio, endpoint, 1));
        var cenario2 = await Criar("cenario_teste", DadosDoCenario(relatorio, endpoint, 2));

        await Excluir("relatorio_teste", relatorio);

        using var escopo = Banco.Fabrica.Services.CreateScope();
        var exclusoes = await escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>().HistoricosAlteracao
            .Where(h => h.Operacao == OperacaoHistorico.Exclusao)
            .ToListAsync();

        Assert.Equal(
            new[] { relatorio, cenario1, cenario2 }.Select(Guid.Parse).Order(),
            exclusoes.Select(h => h.AlvoId).Order());
        Assert.Single(exclusoes.Select(h => h.CorrelacaoId).Distinct());
        Assert.All(exclusoes, h => Assert.Null(h.DadosDepois));
    }

    [Fact]
    public async Task HistoricoDeRegistroDeOutraEquipe_NaoEhVisivel()
    {
        // RN28: 404, inclusive depois que o registro foi excluído.
        var equipeB = await CriarEquipeAsync("Equipe B");
        EquipeDosAtalhos = equipeB;
        var rtcB = await Criar("rtc", new JsonObject { ["rtc_externo_id"] = "RB" });
        var (_, externoA) = await CriarUsuarioAsync("externo", Perfil.Externo, [EquipePadrao]);

        Assert.Equal(HttpStatusCode.NotFound, (await externoA.GetAsync($"{UrlRegistros("rtc")}/{rtcB}/historico")).StatusCode);

        await Excluir("rtc", rtcB);
        Assert.Equal(HttpStatusCode.NotFound, (await externoA.GetAsync($"{UrlRegistros("rtc")}/{rtcB}/historico")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Cliente.GetAsync($"{UrlRegistros("rtc")}/{rtcB}/historico")).StatusCode);
    }

    [Fact]
    public async Task Historico_ReferenciaMostraORotuloAtual()
    {
        var comunidadeA = await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade A" });
        var comunidadeB = await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade B" });
        var sigla = await Criar("sigla", new JsonObject { ["comunidade"] = comunidadeA, ["codigo_sigla"] = "S" });
        await Atualizar("sigla", sigla, ComVersao(await Ler("sigla", sigla), new JsonObject { ["comunidade"] = comunidadeB, ["codigo_sigla"] = "S" }));

        var alteracao = (await LerJson(await Cliente.GetAsync($"{UrlRegistros("sigla")}/{sigla}/historico")))["itens"]![0]!;
        var mudanca = Assert.Single(alteracao["mudancas"]!.AsArray())!;

        Assert.Equal("comunidade", mudanca["campo"]!.GetValue<string>());
        Assert.Equal("Comunidade A", mudanca["de"]!["rotulo"]!.GetValue<string>());
        Assert.Equal(comunidadeB, mudanca["para"]!["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Registro_TrazCriadoPorEAtualizadoPor()
    {
        var id = await Criar("comunidade", new JsonObject { ["nome"] = "C" });
        var (_, gestor) = await CriarUsuarioAsync("outro.admin", Perfil.Administrador);
        await gestor.PutAsync($"{UrlRegistros("comunidade")}/{id}",
            System.Net.Http.Json.JsonContent.Create(ComVersao(await Ler("comunidade", id), new JsonObject { ["nome"] = "D" })));

        var lido = await Ler("comunidade", id);

        Assert.Equal("admin.testes", lido["criadoPor"]!["matricula"]!.GetValue<string>());
        Assert.Equal("outro.admin", lido["atualizadoPor"]!["matricula"]!.GetValue<string>());
    }

    [Fact]
    public async Task ComandoCriarAdmin_CriaQuandoNaoHaAdministradorAtivoESeRecusaQuandoJaExiste()
    {
        // Antes de cada teste já existe o "admin.testes": o comando se recusa.
        var saida = new StringWriter();
        var recusado = await ComandoCriarAdmin.ExecutarAsync(
            Banco.Fabrica.Services, ["criar-admin", "--matricula", "chefe", "--nome", "Chefe"], saida);
        Assert.Equal(1, recusado);
        Assert.Contains("Já existe um administrador ativo", saida.ToString());

        // Sem administrador ativo, cria (sem senha: ele entra pelo login do Windows).
        using (var escopo = Banco.Fabrica.Services.CreateScope())
        {
            var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            (await contexto.Usuarios.SingleAsync(u => u.Id == Administrador.Id)).Ativo = false;
            await contexto.SaveChangesAsync();
        }

        var criado = await ComandoCriarAdmin.ExecutarAsync(
            Banco.Fabrica.Services, ["criar-admin", "--matricula", "chefe", "--nome", "Chefe"], new StringWriter());

        Assert.Equal(0, criado);
        using var verificacao = Banco.Fabrica.Services.CreateScope();
        var chefe = await verificacao.ServiceProvider.GetRequiredService<CatalogoDbContext>().Usuarios.SingleAsync(u => u.Matricula == "chefe");
        Assert.Equal(Perfil.Administrador, chefe.Perfil);
        Assert.True(chefe.Ativo);
    }

    [Fact]
    public async Task ComandoCriarAdmin_MatriculaQueJaEntrouPeloWindows_ViraAdministrador()
    {
        // RN105: a pessoa abriu o Catálogo antes do comando e virou EXTERNO sem equipe; o comando a promove, com histórico.
        Assert.Equal(System.Net.HttpStatusCode.OK, (await LoginWindows(@"CORPCAIXA\c161587")).StatusCode);
        using (var escopo = Banco.Fabrica.Services.CreateScope())
        {
            var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            (await contexto.Usuarios.SingleAsync(u => u.Id == Administrador.Id)).Ativo = false;
            await contexto.SaveChangesAsync();
        }

        var resultado = await ComandoCriarAdmin.ExecutarAsync(
            Banco.Fabrica.Services, ["criar-admin", "--matricula", "C161587", "--nome", "Fulana"], new StringWriter());

        Assert.Equal(0, resultado);
        using var verificacao = Banco.Fabrica.Services.CreateScope();
        var contextoVerificacao = verificacao.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var fulana = await contextoVerificacao.Usuarios.SingleAsync(u => u.Matricula == "C161587");
        Assert.Equal(Perfil.Administrador, fulana.Perfil);
        Assert.Equal("Fulana", fulana.Nome);
        Assert.Equal(2, await contextoVerificacao.HistoricosAlteracao.CountAsync(h => h.AlvoId == fulana.Id));
    }

    private static string Unica(JsonNode item)
    {
        var mudanca = Assert.Single(item["mudancas"]!.AsArray())!;
        return Mudanca(mudanca["de"]?.GetValue<string>(), mudanca["para"]?.GetValue<string>());
    }

    private static string Mudanca(string? de, string? para) => $"nome: {de ?? "∅"} → {para ?? "∅"}";
}
