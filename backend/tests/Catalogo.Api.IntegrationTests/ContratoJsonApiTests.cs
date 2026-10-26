using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;

namespace Catalogo.Api.IntegrationTests;

// Convenções do contrato (docs/api/README.md): grupos para o menu, envelope de paginação e datas em UTC com "Z".
public class ContratoJsonApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    [Fact]
    public async Task Grupos_UsuarioLogado_VeOsGruposComChaveNomeEDescricao()
    {
        var (_, externo) = await CriarUsuarioAsync("externo", Perfil.Externo, [EquipePadrao]);

        var resposta = await externo.GetAsync("/api/v1/grupos");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var grupo = (await LerJson(resposta)).AsArray().Single(g => g!["chave"]!.GetValue<string>() == "testes_desempenho")!;
        Assert.Equal("Testes de Desempenho", grupo["nome"]!.GetValue<string>());
        Assert.True(grupo.AsObject().ContainsKey("descricao"));   // nulo explícito, como no resto da API
        Assert.Equal(HttpStatusCode.Unauthorized, (await ClienteAnonimo.GetAsync("/api/v1/grupos")).StatusCode);
    }

    [Fact]
    public async Task Equipes_UsamOEnvelopeDePaginacao()
    {
        await CriarEquipeAsync("Equipe B");
        await CriarEquipeAsync("Equipe C", ativa: false);

        var pagina = await LerJson(await Cliente.GetAsync("/api/v1/equipes?ativa=true&tamanho=1&pagina=2"));

        Assert.Equal(["itens", "pagina", "tamanho", "total", "totalPaginas"], pagina.AsObject().Select(p => p.Key));
        Assert.Equal("Equipe Padrão", Assert.Single(pagina["itens"]!.AsArray())!["nome"]!.GetValue<string>());
        Assert.Equal((2, 1, 2, 2), (pagina["pagina"]!.GetValue<int>(), pagina["tamanho"]!.GetValue<int>(),
            pagina["total"]!.GetValue<int>(), pagina["totalPaginas"]!.GetValue<int>()));
        Assert.Equal(HttpStatusCode.BadRequest, (await Cliente.GetAsync("/api/v1/equipes?tamanho=101")).StatusCode);
    }

    [Fact]
    public async Task DatasDeSistema_SaemEmUtcComZ()
    {
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "Ágil" });
        await Atualizar("comunidade", comunidade, ComVersao(await Ler("comunidade", comunidade), new JsonObject { ["nome"] = "Ágil 2" }));
        var login = await LerJson(await ClienteAnonimo.PostAsJsonAsync("/api/v1/auth/login", new { matricula = "admin.testes", senha = SenhaPadrao }));
        var registro = await Ler("comunidade", comunidade);
        var equipes = await LerJson(await Cliente.GetAsync("/api/v1/equipes"));
        var usuario = await LerJson(await Cliente.GetAsync($"/api/v1/usuarios/{Administrador.Id}"));

        string[] datas =
        [
            registro["criadoEm"]!.GetValue<string>(),
            registro["atualizadoEm"]!.GetValue<string>(),
            login["expiraEm"]!.GetValue<string>(),
            equipes["itens"]![0]!["criadoEm"]!.GetValue<string>(),
            usuario["criadoEm"]!.GetValue<string>(),
            usuario["ultimoAcessoEm"]!.GetValue<string>(),
        ];
        Assert.All(datas, data => Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$", data));
    }
}
