using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

// Padrão de erros (docs/api/codigos-de-erro.md, docs/adr/0023): todo ProblemDetails tem "codigo" e "traceId".
public class ErrosApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private static readonly IReadOnlySet<string> CodigosConhecidos = typeof(CodigosErro)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(c => (string)c.GetRawConstantValue()!)
        .ToHashSet();

    [Fact]
    public async Task PrincipaisCenariosDeErro_TodosTrazemCodigoETraceId()
    {
        var (_, interno) = await CriarUsuarioAsync("interno", Perfil.Interno, [EquipePadrao]);
        var (_, senhaProvisoria) = await CriarUsuarioAsync("provisoria", Perfil.Interno, [EquipePadrao], deveTrocarSenha: true);
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "Ágil" });
        await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "AG" });
        var lido = await Ler("comunidade", comunidade);
        await Atualizar("comunidade", comunidade, ComVersao(lido, new JsonObject { ["nome"] = "Ágil 2" }));
        var naoJson = new StringContent("{ isto não é json", Encoding.UTF8, "application/json");

        (string Cenario, HttpResponseMessage Resposta, HttpStatusCode Status, string Codigo)[] cenarios =
        [
            ("sem token", await ClienteAnonimo.GetAsync("/api/v1/grupos"), HttpStatusCode.Unauthorized, CodigosErro.NaoAutenticado),
            ("token inválido", await ClienteComToken("abc.def.ghi").GetAsync("/api/v1/grupos"), HttpStatusCode.Unauthorized, CodigosErro.NaoAutenticado),
            ("login errado", await ClienteAnonimo.PostAsJsonAsync("/api/v1/auth/login", new { matricula = "interno", senha = "errada123456" }),
                HttpStatusCode.Unauthorized, CodigosErro.CredenciaisInvalidas),
            ("troca de senha", await senhaProvisoria.GetAsync("/api/v1/grupos"), HttpStatusCode.Forbidden, CodigosErro.TrocaDeSenhaObrigatoria),
            ("perfil", await interno.DeleteAsync($"{UrlRegistros("comunidade")}/{comunidade}"), HttpStatusCode.Forbidden, CodigosErro.SemPermissao),
            ("registro", await Buscar("comunidade", Guid.NewGuid().ToString()), HttpStatusCode.NotFound, CodigosErro.RegistroNaoEncontrado),
            ("entidade", await Cliente.GetAsync($"{UrlGrupo}/entidades/nao_existe/registros"), HttpStatusCode.NotFound, CodigosErro.EntidadeNaoEncontrada),
            ("grupo", await Cliente.GetAsync("/api/v1/grupos/nao_existe/entidades"), HttpStatusCode.NotFound, CodigosErro.GrupoNaoEncontrado),
            ("rota", await Cliente.GetAsync("/api/v1/nao-existe"), HttpStatusCode.NotFound, CodigosErro.RotaNaoEncontrada),
            ("rota sem versão", await Cliente.GetAsync("/api/grupos"), HttpStatusCode.NotFound, CodigosErro.RotaNaoEncontrada),
            ("validação", await Postar("comunidade", new JsonObject { ["nome"] = "" }), HttpStatusCode.BadRequest, CodigosErro.Validacao),
            ("JSON malformado", await Cliente.PostAsync(UrlRegistros("comunidade"), naoJson), HttpStatusCode.BadRequest, CodigosErro.Validacao),
            ("parâmetro", await Listar("comunidade", "tamanho=500"), HttpStatusCode.BadRequest, CodigosErro.Validacao),
            ("unicidade", await Postar("comunidade", new JsonObject { ["nome"] = "ágil 2" }), HttpStatusCode.Conflict, CodigosErro.ConflitoUnicidade),
            ("concorrência", await Atualizar("comunidade", comunidade, ComVersao(lido, new JsonObject { ["nome"] = "Velha" })),
                HttpStatusCode.Conflict, CodigosErro.ConflitoConcorrencia),
            ("exclusão bloqueada", await Excluir("comunidade", comunidade), HttpStatusCode.Conflict, CodigosErro.ExclusaoBloqueada),
            ("método", await Cliente.PatchAsync($"{UrlRegistros("comunidade")}/{comunidade}", null), HttpStatusCode.MethodNotAllowed,
                CodigosErro.MetodoNaoPermitido),
            ("tipo de conteúdo", await Cliente.PostAsync(UrlRegistros("comunidade"), new StringContent("nome=x", Encoding.UTF8, "text/plain")),
                HttpStatusCode.UnsupportedMediaType, CodigosErro.TipoDeConteudoNaoSuportado),
            ("importação inexistente", await Cliente.GetAsync($"{UrlGrupo}/entidades/comunidade/importacoes/{Guid.NewGuid()}"),
                HttpStatusCode.NotFound, CodigosErro.ImportacaoNaoEncontrada),
            ("arquivo da importação", await Apoio.PlanilhasDeTeste.EnviarAsync(Cliente, $"{UrlGrupo}/entidades/comunidade", Encoding.UTF8.GetBytes("x"), "a.pdf"),
                HttpStatusCode.BadRequest, CodigosErro.Validacao),
            ("importar sem permissão", await Apoio.PlanilhasDeTeste.EnviarAsync(interno, $"{UrlGrupo}/entidades/comunidade", Encoding.UTF8.GetBytes("nome\nA"), "a.csv"),
                HttpStatusCode.Forbidden, CodigosErro.SemPermissao),
        ];

        foreach (var (cenario, resposta, status, codigo) in cenarios)
        {
            var corpo = await resposta.Content.ReadAsStringAsync();
            Assert.True(resposta.StatusCode == status, $"{cenario}: esperado {(int)status}, veio {(int)resposta.StatusCode} {corpo}");
            Assert.True(resposta.Content.Headers.ContentType?.MediaType == "application/problem+json", $"{cenario}: {resposta.Content.Headers.ContentType} {corpo}");

            var problema = JsonNode.Parse(corpo)!;
            Assert.True(problema["codigo"]?.GetValue<string>() == codigo, $"{cenario}: esperado codigo {codigo}, veio {corpo}");
            Assert.Contains(codigo, CodigosConhecidos);
            Assert.False(string.IsNullOrWhiteSpace(problema["traceId"]?.GetValue<string>()), $"{cenario}: sem traceId: {corpo}");
        }
    }

    [Fact]
    public async Task ErrosComDados_TrazemOsDadosEmDetalhes()
    {
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "Ágil" });
        await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "AG" });

        var bloqueio = await LerJson(await Excluir("comunidade", comunidade));
        var unicidade = await LerJson(await Postar("comunidade", new JsonObject { ["nome"] = "agil" }));

        Assert.Equal("sigla", bloqueio["detalhes"]!["dependencias"]![0]!["entidade"]!.GetValue<string>());
        Assert.Equal(1, bloqueio["detalhes"]!["dependencias"]![0]!["quantidade"]!.GetValue<int>());
        Assert.Equal("uq_comunidade_nome", unicidade["detalhes"]!["restricao"]!.GetValue<string>());
        Assert.Equal(["nome"], unicidade["detalhes"]!["campos"]!.AsArray().Select(c => c!.GetValue<string>()));
    }

    [Fact]
    public async Task ErroInesperado_ForaDeDevelopment_SoTrazCodigoETraceId()
    {
        // O ambiente dos testes é "Testes" (não é Development). Os metadados falham com uma mensagem "interna".
        var fabrica = FabricaCom(new Dictionary<string, string>(),
            servicos => servicos.AddScoped<IMetadadosProvider, MetadadosQueFalham>());

        var resposta = await ClienteDe(Administrador, fabrica).GetAsync("/api/v1/grupos");

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        var corpo = await resposta.Content.ReadAsStringAsync();
        var problema = JsonNode.Parse(corpo)!.AsObject();
        Assert.Equal(CodigosErro.ErroInterno, problema["codigo"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(problema["traceId"]!.GetValue<string>()));
        Assert.False(problema.ContainsKey("excecao"));
        Assert.DoesNotContain(MetadadosQueFalham.Mensagem, corpo);
        Assert.DoesNotContain(nameof(MetadadosQueFalham), corpo);   // nem stack trace
    }

    private sealed class MetadadosQueFalham : IMetadadosProvider
    {
        public const string Mensagem = "senha do banco: segredo123";

        public Task<Domain.Metadados.Grupo?> ObterGrupoAsync(string grupoChave, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Mensagem);

        public Task<IReadOnlyList<ResumoGrupo>> ListarGruposAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Mensagem);

        public void InvalidarCache(string grupoChave)
        {
        }
    }
}
