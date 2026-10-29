using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

/// <summary>
/// RN96: GET /api/v1/grupos/{grupo}/eu/recentes — os itens que o usuário criou ou alterou por último na área de gestão, do mais
/// recente para o mais antigo (no máximo 8), pelo histórico, só com o que ele ainda vê.
/// </summary>
public class RecentesApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private const string UrlRecentes = UrlGrupo + "/eu/recentes";

    private static async Task<JsonArray> Recentes(HttpClient cliente)
    {
        var resposta = await cliente.GetAsync(UrlRecentes);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await LerJson(resposta)).AsArray();
    }

    private static List<string> Ids(JsonArray recentes) => recentes.Select(r => r!["id"]!.GetValue<string>()).ToList();

    private static async Task<string> CriarComo(HttpClient cliente, string entidade, JsonObject dados)
    {
        var resposta = await Postar(cliente, entidade, dados);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await LerJson(resposta))["id"]!.GetValue<string>();
    }

    private static JsonObject Rtc(string id) => new() { ["rtc_externo_id"] = id };

    [Fact]
    public async Task Get_SoOsItensDoUsuario_DoMaisRecenteParaOMaisAntigo_ComTipoRotuloEData()
    {
        var (_, interno) = await CriarUsuarioAsync("interno.r1", Perfil.Interno, [EquipePadrao]);
        var primeiro = await CriarComo(interno, "rtc", Rtc("RTC-1"));
        var segundo = await CriarComo(interno, "rtc", Rtc("RTC-2"));
        await Criar("rtc", Rtc("RTC-DO-ADMIN")); // de outra pessoa: não aparece

        var recentes = await Recentes(interno);

        Assert.Equal([segundo, primeiro], Ids(recentes));
        Assert.Equal("rtc", recentes[0]!["entidade"]!.GetValue<string>());
        Assert.Equal("RTC-2", recentes[0]!["rotulo"]!.GetValue<string>());
        Assert.True(recentes[0]!["ocorridoEm"]!.GetValue<DateTime>() > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Get_AlterarUmItemAntigo_ElePassaParaOTopo_SemRepetir()
    {
        var (_, interno) = await CriarUsuarioAsync("interno.r2", Perfil.Interno, [EquipePadrao]);
        var antigo = await CriarComo(interno, "rtc", Rtc("RTC-A"));
        var novo = await CriarComo(interno, "rtc", Rtc("RTC-B"));

        var lido = await LerJson(await interno.GetAsync($"{UrlRegistros("rtc")}/{antigo}"));
        var alteracao = await interno.PutAsJsonAsync($"{UrlRegistros("rtc")}/{antigo}", ComVersao(lido, Rtc("RTC-A2")));
        Assert.Equal(HttpStatusCode.OK, alteracao.StatusCode);

        var recentes = await Recentes(interno);

        Assert.Equal([antigo, novo], Ids(recentes));
        Assert.Equal("RTC-A2", recentes[0]!["rotulo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Get_ItemExcluido_NaoAparece()
    {
        var (_, interno) = await CriarUsuarioAsync("interno.r3", Perfil.Interno, [EquipePadrao]);
        var fica = await CriarComo(interno, "rtc", Rtc("RTC-FICA"));
        var sai = await CriarComo(interno, "rtc", Rtc("RTC-SAI"));
        Assert.Equal(HttpStatusCode.NoContent, (await Excluir("rtc", sai)).StatusCode);

        Assert.Equal([fica], Ids(await Recentes(interno)));
    }

    [Fact]
    public async Task Get_ItemDeEquipeQueDeixouDeSerHabilitada_NaoAparece()
    {
        // RN92: a equipe B sai da área; os itens dela ficam ocultos para os membros, também aqui.
        var equipeB = await CriarEquipeAsync("Equipe B");
        var (_, gestor) = await CriarUsuarioAsync("gestor.r4", Perfil.Gestor, [EquipePadrao, equipeB]);
        var deA = await CriarComo(gestor, "rtc", new JsonObject { ["rtc_externo_id"] = "RTC-A", ["equipeId"] = EquipePadrao.Id });
        var deB = await CriarComo(gestor, "rtc", new JsonObject { ["rtc_externo_id"] = "RTC-B", ["equipeId"] = equipeB.Id });
        Assert.Equal([deB, deA], Ids(await Recentes(gestor)));

        using (var escopo = Banco.Fabrica.Services.CreateScope())
        {
            var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            await contexto.GruposEquipes.Where(g => g.EquipeId == equipeB.Id).ExecuteDeleteAsync();
            escopo.ServiceProvider.GetRequiredService<IMetadadosProvider>().InvalidarCache(BancoDeTestesFixture.GrupoDoSeed);
        }

        Assert.Equal([deA], Ids(await Recentes(gestor)));
    }

    [Fact]
    public async Task Get_NoMaximoOitoItens()
    {
        var (_, interno) = await CriarUsuarioAsync("interno.r5", Perfil.Interno, [EquipePadrao]);
        var criados = new List<string>();
        for (var i = 1; i <= 10; i++)
            criados.Add(await CriarComo(interno, "rtc", Rtc($"RTC-{i:00}")));

        var recentes = await Recentes(interno);

        Assert.Equal(8, recentes.Count);
        Assert.Equal(Enumerable.Reverse(criados).Take(8), Ids(recentes));
    }

    [Fact]
    public async Task Get_SemNadaCriado_ListaVazia()
    {
        var (_, interno) = await CriarUsuarioAsync("interno.r6", Perfil.Interno, [EquipePadrao]);

        Assert.Empty(await Recentes(interno));
    }

    [Fact]
    public async Task Get_AreaQueOUsuarioNaoVe_Devolve404()
    {
        // RN90: a equipe do usuário não está habilitada na área do seed.
        var fora = await CriarEquipeAsync("Equipe Fora", habilitarNoSeed: false);
        var (_, interno) = await CriarUsuarioAsync("interno.r7", Perfil.Interno, [fora]);

        var resposta = await interno.GetAsync(UrlRecentes);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("GRUPO_NAO_ENCONTRADO", (await LerJson(resposta))["codigo"]!.GetValue<string>());
    }
}
