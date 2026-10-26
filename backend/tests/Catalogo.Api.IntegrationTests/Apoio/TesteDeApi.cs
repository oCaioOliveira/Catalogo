using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Infrastructure.Persistencia;
using Catalogo.Infrastructure.Persistencia.Tabelas;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests.Apoio;

/// <summary>
/// Base dos testes de integração: limpa os dados antes de cada teste, cria uma equipe padrão e um
/// ADMINISTRADOR (o <see cref="Cliente"/> padrão) e oferece atalhos para chamar a API do grupo
/// "testes_desempenho" (o seed dos scripts). Para outros perfis, use <see cref="CriarUsuarioAsync"/>.
/// </summary>
[Collection(ColecaoApi.Nome)]
public abstract class TesteDeApi(BancoDeTestesFixture banco) : IAsyncLifetime
{
    protected const string UrlGrupo = "/api/v1/grupos/testes_desempenho";

    /// <summary>Fábricas derivadas criadas pelo teste (descartadas no fim dele).</summary>
    private readonly List<WebApplicationFactory<Program>> _fabricasDerivadas = [];

    /// <summary>Senha de todos os usuários criados pelos atalhos (atende a política, RN25).</summary>
    protected const string SenhaPadrao = "SenhaDeTeste123";

    /// <summary>Entidades POR EQUIPE do seed (RN27). As demais (comunidade, sigla, ambiente, status_resultado) são globais.</summary>
    protected static readonly IReadOnlySet<string> EntidadesPorEquipe =
        new HashSet<string> { "projeto", "endpoint_servico", "rtc", "relatorio_teste", "cenario_teste", "link" };

    // O hash PBKDF2 é lento de propósito: calculado uma vez para a senha padrão.
    private static string? _hashSenhaPadrao;

    protected BancoDeTestesFixture Banco { get; } = banco;

    /// <summary>Cliente autenticado como o administrador padrão.</summary>
    protected HttpClient Cliente { get; private set; } = null!;

    /// <summary>Cliente SEM token.</summary>
    protected HttpClient ClienteAnonimo { get; } = banco.Fabrica.CreateClient();

    protected Usuario Administrador { get; private set; } = null!;

    /// <summary>Equipe criada antes de cada teste.</summary>
    protected Equipe EquipePadrao { get; private set; } = null!;

    /// <summary>
    /// Equipe usada pelos atalhos (<see cref="Postar(string, JsonObject)"/>, <see cref="Criar"/>, CriarProjeto...)
    /// nas entidades por equipe, quando o corpo não traz "equipeId". Troque para criar em outra equipe.
    /// </summary>
    protected Equipe EquipeDosAtalhos { get; set; } = null!;

    public async Task InitializeAsync()
    {
        await Banco.LimparDadosAsync();
        EquipePadrao = await CriarEquipeAsync("Equipe Padrão");
        EquipeDosAtalhos = EquipePadrao;
        (Administrador, Cliente) = await CriarUsuarioAsync("admin.testes", Perfil.Administrador);
    }

    public async Task DisposeAsync()
    {
        Cliente.Dispose();
        ClienteAnonimo.Dispose();
        foreach (var fabrica in _fabricasDerivadas)
            await fabrica.DisposeAsync();
    }

    /// <summary>
    /// Outra instância da API, sobre o MESMO banco e com a mesma chave do JWT, com configurações extras
    /// (ex.: "Cors:OrigensPermitidas:0") e, se quiser, serviços trocados. Use com <see cref="ClienteDe(Usuario, WebApplicationFactory{Program})"/>.
    /// </summary>
    protected WebApplicationFactory<Program> FabricaCom(
        IReadOnlyDictionary<string, string> configuracoes, Action<IServiceCollection>? servicos = null)
    {
        var fabrica = Banco.Fabrica.WithWebHostBuilder(builder =>
        {
            foreach (var (chave, valor) in configuracoes)
                builder.UseSetting(chave, valor);
            if (servicos is not null)
                builder.ConfigureTestServices(servicos);
        });
        _fabricasDerivadas.Add(fabrica);
        return fabrica;
    }

    /// <summary>Cliente HTTP de outra fábrica (<see cref="FabricaCom"/>) com o token de um usuário.</summary>
    protected HttpClient ClienteDe(Usuario usuario, WebApplicationFactory<Program> fabrica)
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenDe(usuario));
        return cliente;
    }

    /// <summary>
    /// Cria uma equipe direto no banco. Por padrão ela é habilitada no grupo do seed (como a carga do V009 fez com todas as
    /// equipes, RN90), para os testes que não são de listagens por equipe continuarem como antes. Passe
    /// <paramref name="habilitarNoSeed"/> false para uma equipe que não usa "testes_desempenho".
    /// </summary>
    protected async Task<Equipe> CriarEquipeAsync(string nome, bool ativa = true, bool habilitarNoSeed = true)
    {
        Equipe equipe;
        using (var escopo = Banco.Fabrica.Services.CreateScope())
        {
            var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            equipe = new Equipe { Nome = nome, Ativa = ativa };
            contexto.Equipes.Add(equipe);
            await contexto.SaveChangesAsync();
        }

        if (habilitarNoSeed)
            await HabilitarEquipesAsync(BancoDeTestesFixture.GrupoDoSeed, equipe);
        return equipe;
    }

    /// <summary>
    /// Habilita equipes num grupo direto no banco (RN90), sem passar pela API nem mudar a versão do grupo, e descarta o cache de
    /// metadados dele. Para testar a própria configuração, use o PUT .../admin/metadados/grupos/{grupo}/equipes.
    /// </summary>
    protected async Task HabilitarEquipesAsync(string grupoChave, params Equipe[] equipes)
    {
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var grupoId = await contexto.Grupos.Where(g => g.Chave == grupoChave).Select(g => g.Id).SingleAsync();
        foreach (var equipe in equipes)
            contexto.GruposEquipes.Add(new GrupoEquipeTabela { GrupoId = grupoId, EquipeId = equipe.Id, CriadoEm = DateTime.UtcNow });
        await contexto.SaveChangesAsync();
        escopo.ServiceProvider.GetRequiredService<IMetadadosProvider>().InvalidarCache(grupoChave);
    }

    /// <summary>
    /// Cria um usuário direto no banco (senha <see cref="SenhaPadrao"/>) e devolve um cliente HTTP já
    /// autenticado como ele. O token é gerado pelo próprio IGeradorToken da API, sem passar pelo login.
    /// </summary>
    protected async Task<(Usuario Usuario, HttpClient Cliente)> CriarUsuarioAsync(
        string matricula, Perfil perfil, IEnumerable<Equipe>? equipes = null, bool deveTrocarSenha = false)
    {
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var servicos = escopo.ServiceProvider;
        _hashSenhaPadrao ??= servicos.GetRequiredService<IHasherSenha>().GerarHash(SenhaPadrao);

        var usuario = new Usuario { Matricula = matricula, Nome = "Usuário " + matricula, Perfil = perfil };
        usuario.DefinirSenha(_hashSenhaPadrao, deveTrocarSenha);
        var contexto = servicos.GetRequiredService<CatalogoDbContext>();
        contexto.Usuarios.Add(usuario);
        foreach (var equipe in equipes ?? [])
            usuario.Equipes.Add(new UsuarioEquipe { UsuarioId = usuario.Id, EquipeId = equipe.Id });
        await contexto.SaveChangesAsync();

        return (usuario, ClienteDe(usuario));
    }

    /// <summary>Cliente HTTP com o token de um usuário.</summary>
    protected HttpClient ClienteDe(Usuario usuario) => ClienteComToken(TokenDe(usuario));

    /// <summary>JWT de um usuário, gerado pelo próprio IGeradorToken da API.</summary>
    protected string TokenDe(Usuario usuario)
    {
        using var escopo = Banco.Fabrica.Services.CreateScope();
        return escopo.ServiceProvider.GetRequiredService<IGeradorToken>().Gerar(usuario).Token;
    }

    protected HttpClient ClienteComToken(string token)
    {
        var cliente = Banco.Fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    protected static string UrlRegistros(string entidade) => $"{UrlGrupo}/entidades/{entidade}/registros";

    /// <summary>
    /// POST como administrador. Nas entidades por equipe, sem "equipeId" no corpo, usa a
    /// <see cref="EquipeDosAtalhos"/> (o administrador sempre precisa informar a equipe, RN27).
    /// </summary>
    protected Task<HttpResponseMessage> Postar(string entidade, JsonObject dados)
    {
        if (EntidadesPorEquipe.Contains(entidade) && !dados.ContainsKey("equipeId"))
            dados["equipeId"] = EquipeDosAtalhos.Id.ToString();

        return Cliente.PostAsJsonAsync(UrlRegistros(entidade), dados);
    }

    /// <summary>POST com outro cliente, exatamente com o corpo informado.</summary>
    protected static Task<HttpResponseMessage> Postar(HttpClient cliente, string entidade, JsonObject dados) =>
        cliente.PostAsJsonAsync(UrlRegistros(entidade), dados);

    protected Task<HttpResponseMessage> Atualizar(string entidade, string id, JsonObject dados) =>
        Cliente.PutAsJsonAsync($"{UrlRegistros(entidade)}/{id}", dados);

    protected Task<HttpResponseMessage> Excluir(string entidade, string id) =>
        Cliente.DeleteAsync($"{UrlRegistros(entidade)}/{id}");

    protected Task<HttpResponseMessage> Buscar(string entidade, string id) =>
        Cliente.GetAsync($"{UrlRegistros(entidade)}/{id}");

    /// <summary>GET da listagem. <paramref name="consulta"/> já no formato da URL (ex.: "nome.contem=a&amp;tamanho=2").</summary>
    protected Task<HttpResponseMessage> Listar(string entidade, string consulta = "") =>
        Cliente.GetAsync($"{UrlRegistros(entidade)}?{consulta}");

    /// <summary>Lista e devolve o corpo JSON. Falha o teste se a API não responder 200.</summary>
    protected async Task<JsonNode> ListarOk(string entidade, string consulta = "")
    {
        var resposta = await Listar(entidade, consulta);
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == System.Net.HttpStatusCode.OK, $"GET {entidade}?{consulta}: {(int)resposta.StatusCode} {corpo}");
        return JsonNode.Parse(corpo)!;
    }

    /// <summary>Lê o registro (GET por id) e devolve o corpo JSON.</summary>
    protected async Task<JsonNode> Ler(string entidade, string id) => await LerJson(await Buscar(entidade, id));

    /// <summary>Corpo de PUT: os dados informados mais a "versao" lida no GET (RN12).</summary>
    protected static JsonObject ComVersao(JsonNode lido, JsonObject dados)
    {
        dados["versao"] = lido["versao"]!.GetValue<string>();
        return dados;
    }

    /// <summary>Cria um registro e devolve o id. Falha o teste se a API não responder 201.</summary>
    protected async Task<string> Criar(string entidade, JsonObject dados)
    {
        var resposta = await Postar(entidade, dados);
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == System.Net.HttpStatusCode.Created, $"POST {entidade}: {(int)resposta.StatusCode} {corpo}");

        return JsonNode.Parse(corpo)!["id"]!.GetValue<string>();
    }

    protected static async Task<JsonNode> LerJson(HttpResponseMessage resposta) =>
        JsonNode.Parse(await resposta.Content.ReadAsStringAsync())!;

    // ---- Montagem dos dados do cenário de Testes de Desempenho ----

    /// <summary>Comunidade e sigla (globais) e o projeto (na <see cref="EquipeDosAtalhos"/>).</summary>
    protected async Task<string> CriarProjeto(string nome)
    {
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = $"Comunidade do {nome}" });
        var sigla = await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = $"SG-{nome}" });
        return await Criar("projeto", new JsonObject { ["sigla"] = sigla, ["nome"] = nome });
    }

    protected Task<string> CriarEndpoint(string projeto, string nome) => Criar("endpoint_servico", new JsonObject
    {
        ["projeto"] = projeto,
        ["nome_servico"] = nome,
        ["metodo"] = "POST",
        ["endpoint_url_fila"] = $"/api/{nome}",
        ["ativo"] = true,
    });

    protected async Task<string> CriarRelatorio(
        string projeto, string dataExecucao = "2026-10-21T14:30:00-03:00", string bateria = "Bateria 1", string? rtc = null)
    {
        rtc ??= await CriarRtc();
        var ambiente = await Criar("ambiente", new JsonObject { ["nome"] = "Homologação " + Guid.NewGuid().ToString("N")[..6] });
        var status = await Criar("status_resultado", new JsonObject { ["descricao"] = "Aprovado " + Guid.NewGuid().ToString("N")[..6] });

        return await Criar("relatorio_teste", new JsonObject
        {
            ["projeto"] = projeto,
            ["rtc"] = rtc,
            ["ambiente"] = ambiente,
            ["status_resultado"] = status,
            ["data_execucao"] = dataExecucao,
            ["bateria_testes"] = bateria,
            ["tipo_teste"] = "Carga",
            ["executado_por"] = "Equipe de Desempenho",
        });
    }

    protected Task<string> CriarRtc() =>
        Criar("rtc", new JsonObject { ["rtc_externo_id"] = "RTC-" + Guid.NewGuid().ToString("N")[..8] });

    protected static JsonObject DadosDoCenario(string relatorio, string endpoint, int numero = 1, double vazao = 35.5) => new()
    {
        ["relatorio_teste"] = relatorio,
        ["endpoint_servico"] = endpoint,
        ["numero_cenario"] = numero,
        ["usuarios_virtuais"] = 100,
        ["tempo_medio_resposta_ms"] = 250,
        ["percentil_95_ms"] = 480,
        ["vazao_media_tps"] = vazao,
        ["taxa_erro_percentual"] = 0.25,
    };
}
