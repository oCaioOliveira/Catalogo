using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;

namespace Catalogo.Api.IntegrationTests;

/// <summary>
/// Listagens da administração usadas pelo front: busca e ordenação de usuários, busca de equipes e a quantidade de membros
/// ativos de cada equipe (mudanças aditivas na v1).
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class ListagensAdministracaoApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private async Task<string> CriarEquipe(string nome) =>
        (await LerJson(await Cliente.PostAsJsonAsync("/api/v1/equipes", new { nome })))["id"]!.GetValue<string>();

    private async Task<string> CriarUsuario(string matricula, string nome, params string[] equipes)
    {
        var resposta = await Cliente.PostAsJsonAsync("/api/v1/usuarios", new { matricula, nome, perfil = "INTERNO", equipes });
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return (await LerJson(resposta))["usuario"]!["id"]!.GetValue<string>();
    }

    private async Task<JsonNode> Listar(string url)
    {
        var resposta = await Cliente.GetAsync(url);
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == HttpStatusCode.OK, corpo);
        return JsonNode.Parse(corpo)!;
    }

    private static List<string> Matriculas(JsonNode pagina) =>
        pagina["itens"]!.AsArray().Select(u => u!["matricula"]!.GetValue<string>()).ToList();

    [Fact]
    public async Task Usuarios_Busca_NaMatriculaOuNoNome_SemDiferenciarMaiusculasNemAcentos()
    {
        var equipe = await CriarEquipe("Busca");
        await CriarUsuario("c111111", "João da Silva", equipe);
        await CriarUsuario("c222222", "Maria Souza", equipe);
        await CriarUsuario("x999joao", "Pedro Lima", equipe);

        // "JOAO" acha o nome "João" (acento e maiúsculas) e a matrícula "x999joao".
        Assert.Equal(["c111111", "x999joao"], Matriculas(await Listar("/api/v1/usuarios?busca=JOAO&ordenacao=matricula")));
        Assert.Equal(["c222222"], Matriculas(await Listar("/api/v1/usuarios?busca=c2222")));
        // Combina com os outros filtros (E).
        Assert.Empty(Matriculas(await Listar("/api/v1/usuarios?busca=souza&perfil=GESTOR")));
    }

    [Fact]
    public async Task Usuarios_Ordenacao_PorColunaEDecrescente_EInvalida400()
    {
        var equipe = await CriarEquipe("Ordem");
        await CriarUsuario("m2", "Bruno", equipe);
        await CriarUsuario("m1", "Carla", equipe);
        await CriarUsuario("m3", "Ana", equipe);

        Assert.Equal(["m3", "m2", "m1"], Matriculas(await Listar($"/api/v1/usuarios?equipeId={equipe}")));   // padrão: nome
        Assert.Equal(["m1", "m2", "m3"], Matriculas(await Listar($"/api/v1/usuarios?equipeId={equipe}&ordenacao=matricula")));
        Assert.Equal(["m1", "m2", "m3"], Matriculas(await Listar($"/api/v1/usuarios?equipeId={equipe}&ordenacao=-nome")));

        var invalida = await Cliente.GetAsync("/api/v1/usuarios?ordenacao=senha&busca=" + new string('x', 101));
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        Assert.Equal(["busca", "ordenacao"], (await LerJson(invalida))["errors"]!.AsObject().Select(e => e.Key).Order().ToList());
    }

    [Fact]
    public async Task Equipes_Busca_EMembrosAtivos_ContaSoUsuariosAtivos()
    {
        var alfa = await CriarEquipe("Squad Pagamentos");
        var beta = await CriarEquipe("Squad Cartões");
        await CriarEquipe("Plataforma");
        await CriarUsuario("a1", "Um", alfa);
        await CriarUsuario("a2", "Dois", alfa, beta);
        var inativo = await CriarUsuario("a3", "Três", alfa, beta);
        Assert.Equal(HttpStatusCode.OK,
            (await Cliente.PatchAsJsonAsync($"/api/v1/usuarios/{inativo}/ativacao", new { ativo = false })).StatusCode);

        var pagina = await Listar("/api/v1/equipes?busca=squad");
        var membros = pagina["itens"]!.AsArray().ToDictionary(e => e!["nome"]!.GetValue<string>(), e => e!["membrosAtivos"]!.GetValue<int>());
        // "squad" encontra as duas (sem diferenciar maiúsculas); o usuário inativo não conta.
        Assert.Equal(new Dictionary<string, int> { ["Squad Cartões"] = 1, ["Squad Pagamentos"] = 2 }, membros);

        // O detalhe, a alteração e a ativação devolvem a mesma contagem; a criação, zero.
        Assert.Equal(2, (await Listar($"/api/v1/equipes/{alfa}"))["membrosAtivos"]!.GetValue<int>());
        var resposta = await Cliente.PutAsJsonAsync($"/api/v1/equipes/{alfa}", new { nome = "Squad Pix" });
        var renomeada = await LerJson(resposta);
        Assert.True(resposta.StatusCode == HttpStatusCode.OK, renomeada.ToJsonString());
        Assert.Equal(2, renomeada["membrosAtivos"]!.GetValue<int>());
        Assert.Equal(0, (await LerJson(await Cliente.PostAsJsonAsync("/api/v1/equipes", new { nome = "Nova" })))["membrosAtivos"]!.GetValue<int>());
    }
}
