using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Domain.Acesso;

namespace Catalogo.Api.IntegrationTests.Apoio;

/// <summary>
/// Base dos testes da administração de metadados (RN50 a RN77). Cada teste cria o SEU grupo (chave única), para nunca
/// alterar o seed; a <see cref="BancoDeTestesFixture"/> apaga esses grupos antes do próximo teste.
/// Os atalhos guardam o estado do grupo devolvido por cada escrita (<see cref="Estado"/>), para enviar sempre a versão atual.
/// </summary>
public abstract class TesteDeMetadados(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    protected const string Admin = "/api/v1/admin/metadados";

    /// <summary>Chave do grupo deste teste.</summary>
    protected string Grupo { get; } = "inv_" + Guid.NewGuid().ToString("N")[..8];

    /// <summary>A estrutura do grupo devolvida pela última escrita (GrupoAdminDto).</summary>
    protected JsonNode Estado { get; private set; } = null!;

    protected string UrlGrupoAdmin => $"{Admin}/grupos/{Grupo}";
    protected string UrlEntidadeAdmin(string entidade) => $"{UrlGrupoAdmin}/entidades/{entidade}";
    protected string UrlCampoAdmin(string entidade, string campo) => $"{UrlEntidadeAdmin(entidade)}/campos/{campo}";

    /// <summary>URL da API de USO para os registros de uma entidade deste grupo.</summary>
    protected string UrlUso(string entidade) => $"/api/v1/grupos/{Grupo}/entidades/{entidade}/registros";

    protected string VersaoGrupo => Estado["versao"]!.GetValue<string>();

    protected string VersaoEntidade(string entidade) => EntidadeNoEstado(entidade)["versao"]!.GetValue<string>();

    protected JsonNode EntidadeNoEstado(string entidade) =>
        Estado["entidades"]!.AsArray().Single(e => e!["chave"]!.GetValue<string>() == entidade)!;

    protected JsonNode CampoNoEstado(string entidade, string campo) =>
        EntidadeNoEstado(entidade)["campos"]!.AsArray().Single(c => c!["chave"]!.GetValue<string>() == campo)!;

    /// <summary>Uma escrita que TEM de passar: confere o status e guarda o grupo devolvido.</summary>
    protected async Task<JsonNode> Escrever(HttpMethod metodo, string url, JsonObject? corpo, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var resposta = await Enviar(Cliente, metodo, url, corpo);
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == esperado, $"{metodo} {url}: esperado {(int)esperado}, veio {(int)resposta.StatusCode} {texto}");

        var json = JsonNode.Parse(texto)!;
        if (json["grupo"] is JsonObject grupo)
            Estado = grupo;
        return json;
    }

    protected static Task<HttpResponseMessage> Enviar(HttpClient cliente, HttpMethod metodo, string url, JsonObject? corpo)
    {
        var requisicao = new HttpRequestMessage(metodo, url);
        if (corpo is not null)
            requisicao.Content = JsonContent.Create(corpo);
        return cliente.SendAsync(requisicao);
    }

    /// <summary>Recarrega o estado do grupo (GET da administração).</summary>
    protected async Task Recarregar()
    {
        var resposta = await Cliente.GetAsync(UrlGrupoAdmin);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Estado = (await LerJson(resposta))!;
    }

    protected Task<JsonNode> CriarGrupoAdmin(string nome = "Inventário") =>
        Escrever(HttpMethod.Post, $"{Admin}/grupos", new JsonObject { ["chave"] = Grupo, ["nome"] = nome }, HttpStatusCode.Created);

    /// <summary>
    /// PUT .../equipes (RN90): a lista COMPLETA das equipes habilitadas no grupo deste teste. Devolve a resposta (com os impactos)
    /// e guarda o grupo como ficou.
    /// </summary>
    protected Task<JsonNode> HabilitarEquipesAdmin(params Equipe[] equipes) =>
        Escrever(HttpMethod.Put, $"{UrlGrupoAdmin}/equipes", new JsonObject
        {
            ["versao"] = VersaoGrupo,
            ["equipes"] = new JsonArray(equipes.Select(e => (JsonNode?)e.Id.ToString()).ToArray()),
        });

    protected Task<JsonNode> CriarEntidadeAdmin(string chave, bool escopoEquipe = true) =>
        Escrever(HttpMethod.Post, $"{UrlGrupoAdmin}/entidades", new JsonObject
        {
            ["versao"] = VersaoGrupo, ["chave"] = chave, ["nome"] = chave, ["escopoEquipe"] = escopoEquipe,
        }, HttpStatusCode.Created);

    /// <summary>POST de campo com a versão atual da entidade (os atributos vêm em <paramref name="campo"/>).</summary>
    protected Task<JsonNode> CriarCampoAdmin(string entidade, JsonObject campo)
    {
        campo["versao"] = VersaoEntidade(entidade);
        return Escrever(HttpMethod.Post, $"{UrlEntidadeAdmin(entidade)}/campos", campo, HttpStatusCode.Created);
    }

    /// <summary>A mesma requisição, sem a garantia de sucesso (para os testes de bloqueio).</summary>
    protected Task<HttpResponseMessage> Tentar(HttpMethod metodo, string url, JsonObject? corpo) => Enviar(Cliente, metodo, url, corpo);

    protected Task<JsonNode> DefinirCampoExibicao(string entidade, string campo) =>
        Escrever(HttpMethod.Put, UrlEntidadeAdmin(entidade), new JsonObject
        {
            ["versao"] = VersaoEntidade(entidade), ["nome"] = EntidadeNoEstado(entidade)["nome"]!.GetValue<string>(),
            ["campoExibicao"] = campo,
        });

    protected Task<JsonNode> CriarRestricaoAdmin(string entidade, string nome, params string[] campos) =>
        Escrever(HttpMethod.Post, $"{UrlEntidadeAdmin(entidade)}/restricoes", new JsonObject
        {
            ["versao"] = VersaoEntidade(entidade), ["nome"] = nome, ["campos"] = new JsonArray(campos.Select(c => (JsonNode?)c).ToArray()),
        }, HttpStatusCode.Created);

    /// <summary>
    /// A listagem "Inventário" (docs/api/administracao-de-metadados.md), toda pela API:
    ///   - categoria (GLOBAL): nome TEXTO(50) obrigatório, único, campo de exibição;
    ///   - equipamento (POR EQUIPE): nome TEXTO(100) obrigatório, único e campo de exibição; categoria → categoria (obrigatória);
    ///     data_aquisicao DATA; valor DECIMAL(12,2).
    /// A <see cref="TesteDeApi.EquipePadrao"/> é habilitada no grupo (RN90): uma listagem nova nasce sem equipes (RN93).
    /// </summary>
    protected async Task CriarInventarioAsync()
    {
        await CriarGrupoAdmin();
        await HabilitarEquipesAdmin(EquipePadrao);
        await CriarEntidadeAdmin("categoria", escopoEquipe: false);
        await CriarEntidadeAdmin("equipamento");

        await CriarCampoAdmin("categoria", Campo("nome", "TEXTO", obrigatorio: true, tamanho: 50));
        await DefinirCampoExibicao("categoria", "nome");
        await CriarRestricaoAdmin("categoria", "uq_categoria_nome", "nome");

        await CriarCampoAdmin("equipamento", Campo("nome", "TEXTO", obrigatorio: true, tamanho: 100));
        await CriarCampoAdmin("equipamento", new JsonObject
        {
            ["chave"] = "categoria", ["nome"] = "Categoria", ["tipoDado"] = "REFERENCIA", ["obrigatorio"] = true,
            ["entidadeReferenciada"] = "categoria", ["acaoExclusao"] = "RESTRINGIR",
        });
        await CriarCampoAdmin("equipamento", Campo("data_aquisicao", "DATA"));
        await CriarCampoAdmin("equipamento", new JsonObject
        {
            ["chave"] = "valor", ["nome"] = "Valor", ["tipoDado"] = "DECIMAL", ["precisao"] = 12, ["escala"] = 2,
        });
        await DefinirCampoExibicao("equipamento", "nome");
        await CriarRestricaoAdmin("equipamento", "uq_equipamento_nome", "nome");
    }

    protected static JsonObject Campo(string chave, string tipo, bool obrigatorio = false, int? tamanho = null)
    {
        var campo = new JsonObject { ["chave"] = chave, ["nome"] = chave, ["tipoDado"] = tipo, ["obrigatorio"] = obrigatorio };
        if (tamanho is not null)
            campo["tamanhoMaximo"] = tamanho;
        return campo;
    }

    /// <summary>Cria uma categoria (global) pela API de uso e devolve o id.</summary>
    protected async Task<string> CriarCategoria(string nome) =>
        await CriarNoGrupo("categoria", new JsonObject { ["nome"] = nome });

    /// <summary>Cria um equipamento (na <see cref="TesteDeApi.EquipeDosAtalhos"/>) pela API de uso e devolve o id.</summary>
    protected async Task<string> CriarEquipamento(string nome, string categoria, decimal? valor = null, string? data = null)
    {
        var dados = new JsonObject { ["equipeId"] = EquipeDosAtalhos.Id.ToString(), ["nome"] = nome, ["categoria"] = categoria };
        if (valor is not null)
            dados["valor"] = valor;
        if (data is not null)
            dados["data_aquisicao"] = data;
        return await CriarNoGrupo("equipamento", dados);
    }

    protected async Task<string> CriarNoGrupo(string entidade, JsonObject dados)
    {
        var resposta = await Cliente.PostAsJsonAsync(UrlUso(entidade), dados);
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == HttpStatusCode.Created, $"POST {entidade}: {(int)resposta.StatusCode} {texto}");
        return JsonNode.Parse(texto)!["id"]!.GetValue<string>();
    }

    /// <summary>O registro pela API de uso.</summary>
    protected async Task<JsonNode> LerRegistro(string entidade, string id) => await LerJson(await Cliente.GetAsync($"{UrlUso(entidade)}/{id}"));

    /// <summary>Confere um 409 de bloqueio e devolve os impactos de "detalhes".</summary>
    protected static async Task<JsonArray> Bloqueio(HttpResponseMessage resposta, string codigo)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == HttpStatusCode.Conflict, $"Esperado 409, veio {(int)resposta.StatusCode} {texto}");
        var problema = JsonNode.Parse(texto)!;
        Assert.Equal(codigo, problema["codigo"]!.GetValue<string>());
        return problema["detalhes"]!["impactos"]!.AsArray();
    }

    protected static JsonNode Impacto(JsonArray impactos, string tipo) =>
        impactos.Single(i => i!["tipo"]!.GetValue<string>() == tipo)!;
}
