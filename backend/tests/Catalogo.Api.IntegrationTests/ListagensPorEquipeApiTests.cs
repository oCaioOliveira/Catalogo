using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;

namespace Catalogo.Api.IntegrationTests;

/// <summary>
/// RN90 a RN93: listagens habilitadas por equipe. Cenário (o mesmo do E2E): a equipe T usa "testes_desempenho" (o seed); a
/// equipe D usa só a listagem deste teste ("Inventário", criada pela API, que nasce sem equipes). Um INTERNO só de T e um GESTOR
/// de T e D.
/// </summary>
public class ListagensPorEquipeApiTests(BancoDeTestesFixture banco) : TesteDeMetadados(banco)
{
    private Equipe _t = null!;
    private Equipe _d = null!;
    private HttpClient _internoT = null!;
    private HttpClient _gestorTD = null!;

    private string UrlEquipesAdmin => $"{UrlGrupoAdmin}/equipes";

    private async Task PrepararAsync()
    {
        await CriarInventarioAsync(); // habilita a EquipePadrao (o administrador cria os itens nela)
        _t = await CriarEquipeAsync("Equipe T");
        _d = await CriarEquipeAsync("Equipe D", habilitarNoSeed: false);
        (_, _internoT) = await CriarUsuarioAsync("interno.t", Perfil.Interno, [_t]);
        (_, _gestorTD) = await CriarUsuarioAsync("gestor.td", Perfil.Gestor, [_t, _d]);
        await HabilitarEquipesAdmin(EquipePadrao, _d); // a lista é completa: a EquipePadrao continua
    }

    private static async Task<List<JsonNode>> Grupos(HttpClient cliente) =>
        (await LerJson(await cliente.GetAsync("/api/v1/grupos"))).AsArray().Select(g => g!).ToList();

    private static List<string> Chaves(IEnumerable<JsonNode> grupos) => grupos.Select(g => g["chave"]!.GetValue<string>()).ToList();

    private Task<HttpResponseMessage> PutEquipes(HttpClient cliente, string query, params Equipe[] equipes) =>
        Enviar(cliente, HttpMethod.Put, UrlEquipesAdmin + query, new JsonObject
        {
            ["versao"] = VersaoGrupo,
            ["equipes"] = new JsonArray(equipes.Select(e => (JsonNode?)e.Id.ToString()).ToArray()),
        });

    [Fact]
    public async Task Grupos_InternoSoDeT_NaoVeAListagemDeD_E404NasRotasDela()
    {
        // RN90
        await PrepararAsync();

        var grupos = await Grupos(_internoT);

        Assert.Equal([BancoDeTestesFixture.GrupoDoSeed], Chaves(grupos));
        Assert.Equal(HttpStatusCode.NotFound, (await _internoT.GetAsync($"/api/v1/grupos/{Grupo}/entidades")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _internoT.GetAsync(UrlUso("equipamento"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _internoT.GetAsync(UrlUso("equipamento") + "/exportar?formato=csv")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _internoT.GetAsync($"/api/v1/grupos/{Grupo}/entidades/equipamento/campos/categoria/opcoes")).StatusCode);
    }

    [Fact]
    public async Task Grupos_GestorDeTeD_VeAsDuas_ComMinhasEquipesDeCadaUma()
    {
        // RN91: "minhasEquipes" de cada listagem é a interseção; "possuiEquipes" diz se há alguma habilitada.
        await PrepararAsync();

        var grupos = (await Grupos(_gestorTD)).ToDictionary(g => g["chave"]!.GetValue<string>());

        Assert.Equal(["Equipe D"], grupos[Grupo]["minhasEquipes"]!.AsArray().Select(e => e!["nome"]!.GetValue<string>()));
        Assert.Equal(["Equipe T"], grupos[BancoDeTestesFixture.GrupoDoSeed]["minhasEquipes"]!.AsArray().Select(e => e!["nome"]!.GetValue<string>()));
        Assert.All(grupos.Values, g => Assert.True(g["possuiEquipes"]!.GetValue<bool>()));
    }

    [Fact]
    public async Task Criar_GestorNaListagemDeD_SemEquipeId_UsaD_EmTRecebe403()
    {
        // RN91 + RN27: com uma só equipe NESTA listagem, o equipeId é opcional.
        await PrepararAsync();
        var categoria = await CriarCategoria("Notebook");

        var semEquipe = await _gestorTD.PostAsJsonAsync(UrlUso("equipamento"), new JsonObject { ["nome"] = "N1", ["categoria"] = categoria });
        var emT = await _gestorTD.PostAsJsonAsync(UrlUso("equipamento"),
            new JsonObject { ["nome"] = "N2", ["categoria"] = categoria, ["equipeId"] = _t.Id.ToString() });

        Assert.Equal(HttpStatusCode.Created, semEquipe.StatusCode);
        Assert.Equal(_d.Id.ToString(), (await LerJson(semEquipe))["equipeId"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Forbidden, emT.StatusCode);
    }

    [Fact]
    public async Task GetEquipes_TodasAsEquipes_ComHabilitadaEQuantidadeDeItens()
    {
        // RN90: a aba "Equipes" (ativas e inativas).
        await PrepararAsync();
        EquipeDosAtalhos = _d;
        await CriarEquipamento("N1", await CriarCategoria("Notebook"));
        await CriarEquipeAsync("Equipe Inativa", ativa: false, habilitarNoSeed: false);

        var json = await LerJson(await Cliente.GetAsync(UrlEquipesAdmin));
        var equipes = json["equipes"]!.AsArray().ToDictionary(e => e!["nome"]!.GetValue<string>());

        Assert.Equal(VersaoGrupo, json["versao"]!.GetValue<string>());
        Assert.True(equipes["Equipe D"]!["habilitada"]!.GetValue<bool>());
        Assert.Equal(1, equipes["Equipe D"]!["quantidadeItens"]!.GetValue<int>());
        Assert.False(equipes["Equipe T"]!["habilitada"]!.GetValue<bool>());
        Assert.False(equipes["Equipe Inativa"]!["ativa"]!.GetValue<bool>());
    }

    [Fact]
    public async Task RemoverEquipeComItens_SimulacaoInforma_ItensOcultos_ReabilitarDevolveTudo()
    {
        // RN92: permitido, com aviso; nada é apagado.
        await PrepararAsync();
        EquipeDosAtalhos = _d;
        var categoria = await CriarCategoria("Notebook");
        await CriarEquipamento("N1", categoria);
        await CriarEquipamento("N2", categoria);

        var simulacao = await LerJson(await PutEquipes(Cliente, "?simular=true", EquipePadrao));
        var impacto = Assert.Single(simulacao["impactos"]!.AsArray())!;
        Assert.True(simulacao["permitido"]!.GetValue<bool>());
        Assert.True(simulacao["simulado"]!.GetValue<bool>());
        Assert.Equal("ITENS_OCULTOS", impacto["tipo"]!.GetValue<string>());
        Assert.False(impacto["bloqueia"]!.GetValue<bool>());
        Assert.Equal(2, impacto["quantidade"]!.GetValue<int>());
        Assert.Contains(Grupo, Chaves(await Grupos(_gestorTD))); // a simulação não gravou nada

        await HabilitarEquipesAdmin(EquipePadrao);
        Assert.DoesNotContain(Grupo, Chaves(await Grupos(_gestorTD)));
        Assert.Equal(HttpStatusCode.NotFound, (await _gestorTD.GetAsync(UrlUso("equipamento"))).StatusCode);
        Assert.Equal(2, (await LerJson(await Cliente.GetAsync(UrlUso("equipamento"))))["total"]!.GetValue<int>()); // o admin vê

        await HabilitarEquipesAdmin(EquipePadrao, _d);
        var deVolta = await LerJson(await _gestorTD.GetAsync(UrlUso("equipamento")));
        Assert.Equal(2, deVolta["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task PutEquipes_GravaHistoricoMetadado_EMudaAVersao()
    {
        // RN52 + RN90
        await PrepararAsync();
        var versaoAntes = VersaoGrupo;

        await HabilitarEquipesAdmin(EquipePadrao, _t, _d);

        var historico = await LerJson(await Cliente.GetAsync($"{UrlGrupoAdmin}/historico"));
        var ultima = historico["itens"]![0]!;
        Assert.NotEqual(versaoAntes, VersaoGrupo);
        Assert.Equal("Equipes habilitadas", ultima["alvo"]!.GetValue<string>());
        Assert.Contains(ultima["mudancas"]!.AsArray(), m => m!["campo"]!.GetValue<string>() == "equipes"
            && m["de"]!.GetValue<string>() == "Equipe D, Equipe Padrão"
            && m["para"]!.GetValue<string>() == "Equipe D, Equipe Padrão, Equipe T");
    }

    [Fact]
    public async Task PutEquipes_Validacoes()
    {
        // RN50 (403), RN52 (409) e a lista (400, todos os erros juntos, RN05).
        await PrepararAsync();

        var doGestor = await PutEquipes(_gestorTD, "", _d);
        var versaoAntiga = await Enviar(Cliente, HttpMethod.Put, UrlEquipesAdmin, new JsonObject
        {
            ["versao"] = "AAAAAAAAAAA=", ["equipes"] = new JsonArray(),
        });
        var invalida = await Enviar(Cliente, HttpMethod.Put, UrlEquipesAdmin, new JsonObject
        {
            ["versao"] = VersaoGrupo,
            ["equipes"] = new JsonArray("nao-e-guid", Guid.NewGuid().ToString(), _d.Id.ToString(), _d.Id.ToString()),
        });
        var semLista = await Enviar(Cliente, HttpMethod.Put, UrlEquipesAdmin, new JsonObject { ["versao"] = VersaoGrupo });

        Assert.Equal(HttpStatusCode.Forbidden, doGestor.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, versaoAntiga.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        var erros = (await LerJson(invalida))["errors"]!.AsObject();
        Assert.Equal(["equipes[0]", "equipes[1]", "equipes[3]"], erros.Select(e => e.Key).Order());
        Assert.Equal(HttpStatusCode.BadRequest, semLista.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _gestorTD.GetAsync(UrlEquipesAdmin)).StatusCode);
    }

    [Fact]
    public async Task ListagemNova_SemEquipes_SoOAdministradorVe()
    {
        // RN93
        await CriarGrupoAdmin("Recém-criada");
        var equipe = await CriarEquipeAsync("Equipe X");
        var (_, gestor) = await CriarUsuarioAsync("gestor.x", Perfil.Gestor, [equipe]);

        var doAdmin = (await Grupos(Cliente)).Single(g => g["chave"]!.GetValue<string>() == Grupo);

        Assert.False(doAdmin["possuiEquipes"]!.GetValue<bool>());
        Assert.Equal(0, Estado["quantidadeEquipes"]!.GetValue<int>());
        Assert.DoesNotContain(Grupo, Chaves(await Grupos(gestor)));
        Assert.Equal(HttpStatusCode.NotFound, (await gestor.GetAsync($"/api/v1/grupos/{Grupo}/entidades")).StatusCode);
    }

    [Fact]
    public async Task EquipeNova_NaoEhHabilitadaEmNenhumaListagem_EODetalheListaAsHabilitadas()
    {
        // RN93 (equipe nova) e o detalhe da equipe com as listagens (RN90).
        await PrepararAsync();
        var criada = await LerJson(await Cliente.PostAsJsonAsync("/api/v1/equipes", new { nome = "Equipe Nova" }));

        var detalheD = await LerJson(await Cliente.GetAsync($"/api/v1/equipes/{_d.Id}"));

        Assert.Empty(criada["listagens"]!.AsArray());
        Assert.Equal([Grupo], detalheD["listagens"]!.AsArray().Select(l => l!["chave"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Painel_SoListagensVisiveis_EEquipesDoConjunto()
    {
        // RN90, RN91: o painel segue a visibilidade da listagem e o conjunto de equipes dela.
        await PrepararAsync();
        var (_, gestorT) = await CriarUsuarioAsync("gestor.t", Perfil.Gestor, [_t]);

        var semAcesso = await gestorT.GetAsync($"/api/v1/painel/grupos/{Grupo}/visao-geral");
        var equipeForaDoConjunto = await _gestorTD.GetAsync($"/api/v1/painel/grupos/{Grupo}/visao-geral?equipeId={_t.Id}");
        var equipeDoConjunto = await _gestorTD.GetAsync($"/api/v1/painel/grupos/{Grupo}/visao-geral?equipeId={_d.Id}");

        Assert.Equal(HttpStatusCode.NotFound, semAcesso.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, equipeForaDoConjunto.StatusCode);
        Assert.Equal(HttpStatusCode.OK, equipeDoConjunto.StatusCode);
    }

    [Fact]
    public async Task Configuracoes_SoEquipesHabilitadasNaListagem()
    {
        // RN91: aprovação e avaliação de uma equipe só numa listagem em que ela está habilitada (404 nas outras).
        await PrepararAsync();
        string Url(Equipe equipe, string grupo) =>
            $"/api/v1/equipes/{equipe.Id}/configuracoes/grupos/{grupo}/entidades/" +
            (grupo == Grupo ? "equipamento" : "projeto") + "/aprovacao";

        Assert.Equal(HttpStatusCode.OK, (await _gestorTD.GetAsync(Url(_d, Grupo))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _gestorTD.GetAsync(Url(_t, Grupo))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _gestorTD.GetAsync(Url(_d, BancoDeTestesFixture.GrupoDoSeed))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente.GetAsync(Url(_t, Grupo))).StatusCode); // o admin também
        Assert.Equal(HttpStatusCode.NotFound,
            (await _gestorTD.GetAsync($"/api/v1/equipes/{_t.Id}/configuracoes/grupos/{Grupo}/entidades/equipamento/avaliacao/modelos")).StatusCode);
    }

    [Fact]
    public async Task Fila_SoListagensVisiveisEEquipesDoConjunto()
    {
        // RN90, RN91: o pendente de D na listagem de D aparece; filtrar por uma listagem que o gestor não vê é 404.
        await PrepararAsync();
        var (_, gestorT) = await CriarUsuarioAsync("gestor.t", Perfil.Gestor, [_t]);

        Assert.Equal(HttpStatusCode.NotFound, (await gestorT.GetAsync($"/api/v1/aprovacoes/pendentes?grupo={Grupo}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _gestorTD.GetAsync($"/api/v1/aprovacoes/pendentes?grupo={Grupo}")).StatusCode);
    }

    [Fact]
    public async Task CargaInicialDoV009_SeedParaTodas_OutrasParaQuemTemItens()
    {
        // V009: "testes_desempenho" para TODAS as equipes existentes; cada outra listagem, para as equipes que já têm itens nela.
        // O script só carrega com a tabela vazia: aqui ele roda de novo, depois de esvaziá-la, sobre o banco de testes.
        // O V009 (imutável) foi escrito para dbo; desde o V012 as tabelas ficam no schema do Catálogo, então ele roda com o schema trocado.
        await PrepararAsync();
        EquipeDosAtalhos = _d;
        await CriarEquipamento("N1", await CriarCategoria("Notebook"));
        var semItens = await CriarEquipeAsync("Equipe Sem Itens", habilitarNoSeed: false);

        await using var conexao = new SqlConnection(Banco.ConnectionString);
        await conexao.OpenAsync();
        await Executar(conexao, $"DELETE FROM {SchemaBanco.Nome}.GrupoEquipe;");
        var v009 = (await File.ReadAllTextAsync(CaminhoDoV009())).Replace("dbo.", SchemaBanco.Nome + ".", StringComparison.Ordinal);
        foreach (var lote in Regex.Split(v009, @"^\s*GO\s*$", RegexOptions.Multiline))
        {
            if (!string.IsNullOrWhiteSpace(lote))
                await Executar(conexao, lote);
        }

        var vinculos = await LerVinculos(conexao);
        Assert.Equal(
            new[] { "Equipe D", "Equipe Padrão", "Equipe Sem Itens", "Equipe T" },
            vinculos.Where(v => v.Grupo == BancoDeTestesFixture.GrupoDoSeed).Select(v => v.Equipe).Order());
        Assert.Equal(["Equipe D"], vinculos.Where(v => v.Grupo == Grupo).Select(v => v.Equipe));
        Assert.DoesNotContain(vinculos, v => v.Grupo == Grupo && v.Equipe == semItens.Nome);
    }

    private static async Task Executar(SqlConnection conexao, string sql)
    {
        await using var comando = new SqlCommand(sql, conexao);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<List<(string Grupo, string Equipe)>> LerVinculos(SqlConnection conexao)
    {
        await using var comando = new SqlCommand(
            $"SELECT g.Chave, e.Nome FROM {SchemaBanco.Nome}.GrupoEquipe v JOIN {SchemaBanco.Nome}.Grupo g ON g.Id = v.GrupoId " +
            $"JOIN {SchemaBanco.Nome}.Equipe e ON e.Id = v.EquipeId", conexao);
        await using var leitor = await comando.ExecuteReaderAsync();
        var vinculos = new List<(string, string)>();
        while (await leitor.ReadAsync())
            vinculos.Add((leitor.GetString(0), leitor.GetString(1)));
        return vinculos;
    }

    private static string CaminhoDoV009()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            var caminho = Path.Combine(pasta.FullName, "database", "scripts", "V009__grupo_equipe.sql");
            if (File.Exists(caminho))
                return caminho;
        }

        throw new FileNotFoundException("database/scripts/V009__grupo_equipe.sql não encontrado acima da pasta dos testes.");
    }
}
