using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;

namespace Catalogo.Api.IntegrationTests;

// Aprovação de registros (RN33 a RN38), com a Equipe A (a padrão) e a Equipe B.
public class AprovacaoApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private const string Justificativa = "Os números conferem com o relatório da ferramenta.";

    private Equipe EquipeA => EquipePadrao;
    private Equipe _equipeB = null!;
    private HttpClient _gestorA = null!, _internoA = null!, _externoA = null!, _outroExternoA = null!, _externoB = null!, _gestorB = null!;

    // Registros de apoio, criados pelo administrador: projeto e RTC da Equipe A, ambiente e status globais.
    private string _projetoA = null!, _rtcA = null!, _ambiente = null!, _status = null!, _endpointA = null!;

    [Fact]
    public async Task Configuracao_GestorDeAAtivaParaA_EquipeBContinuaSemAprovacao()
    {
        // RN33
        await PrepararAsync();

        var ativacao = await Configurar(_gestorA, EquipeA, "relatorio_teste", true);

        Assert.Equal(HttpStatusCode.OK, ativacao.StatusCode);
        Assert.True((await LerJson(ativacao))["ativa"]!.GetValue<bool>());
        EquipeDosAtalhos = _equipeB;
        var projetoB = await CriarProjeto("Projeto B");
        var rtcB = await CriarRtc();
        Assert.Equal(HttpStatusCode.Forbidden, (await Postar(_externoB, "relatorio_teste", Relatorio(projetoB, rtcB, "B1"))).StatusCode);
    }

    [Fact]
    public async Task Configuracao_GestorDeOutraEquipe404_Interno403_AdministradorQualquer()
    {
        // RN33
        await PrepararAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await Configurar(_gestorA, _equipeB, "relatorio_teste", true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Configurar(_internoA, EquipeA, "relatorio_teste", true)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Configurar(Cliente, _equipeB, "relatorio_teste", true)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Configurar(Cliente, EquipeA, "comunidade", true)).StatusCode);   // global
    }

    [Fact]
    public async Task Configuracao_DesativarComPendente409_DepoisDeDecidirFunciona_ECadaMudancaVaiProHistorico()
    {
        // RN33 e RN32
        await PrepararAsync();
        await Configurar(_gestorA, EquipeA, "relatorio_teste", true);
        var relatorio = await CriarRelatorioPendenteAsync("R1");

        var recusada = await Configurar(_gestorA, EquipeA, "relatorio_teste", false);
        Assert.Equal(HttpStatusCode.Conflict, recusada.StatusCode);
        var problema = await LerJson(recusada);
        Assert.Equal(1, problema["detalhes"]!["quantidade"]!.GetValue<int>());
        Assert.Equal("APROVACOES_PENDENTES", problema["codigo"]!.GetValue<string>());

        await Decidir(_gestorA, relatorio, "APROVAR", Justificativa);
        Assert.Equal(HttpStatusCode.OK, (await Configurar(_gestorA, EquipeA, "relatorio_teste", false)).StatusCode);

        var historico = await LerJson(await _gestorA.GetAsync($"{UrlConfiguracao(EquipeA, "relatorio_teste")}/historico"));
        var itens = historico["itens"]!.AsArray();
        Assert.Equal(["ALTERACAO", "CRIACAO"], itens.Select(i => i!["operacao"]!.GetValue<string>()));
        Assert.Equal("gestor.a", itens[0]!["usuario"]!["matricula"]!.GetValue<string>());
        Assert.False(itens[0]!["mudancas"]![0]!["para"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Externo_CriaRelatorioPendente_E403AoCriarProjetoSemAprovacao()
    {
        // RN34
        await PrepararAsync();
        await Configurar(_gestorA, EquipeA, "relatorio_teste", true);

        var relatorio = await Postar(_externoA, "relatorio_teste", Relatorio(_projetoA, _rtcA, "R1"));
        var projeto = await Postar(_externoA, "projeto", new JsonObject { ["sigla"] = await SiglaAsync(), ["nome"] = "Novo" });

        Assert.Equal(HttpStatusCode.Created, relatorio.StatusCode);
        Assert.Equal("PENDENTE", (await LerJson(relatorio))["situacaoAprovacao"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.Forbidden, projeto.StatusCode);
    }

    [Fact]
    public async Task Pendente_OutroExternoNaoVe_InternoVe()
    {
        // RN37
        await PrepararAsync();
        await Configurar(_gestorA, EquipeA, "relatorio_teste", true);
        var relatorio = await CriarRelatorioPendenteAsync("R1");

        Assert.Equal(HttpStatusCode.NotFound, (await _outroExternoA.GetAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}")).StatusCode);
        Assert.Equal(0, (await LerJson(await _outroExternoA.GetAsync(UrlRegistros("relatorio_teste"))))["total"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.OK, (await _internoA.GetAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}")).StatusCode);
        Assert.Equal(1, (await LerJson(await _internoA.GetAsync($"{UrlRegistros("relatorio_teste")}?situacaoAprovacao=PENDENTE")))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Referencias_AutorApontaParaOProprioPendente_TerceirosNaoApontamNemVeemNasOpcoes()
    {
        // RN38
        await PrepararAsync();
        await Configurar(_gestorA, EquipeA, "relatorio_teste", true);
        await Configurar(_gestorA, EquipeA, "cenario_teste", true);
        var relatorio = await CriarRelatorioPendenteAsync("R1");

        var doAutor = await Postar(_externoA, "cenario_teste", Cenario(relatorio, 1));
        var doInterno = await Postar(_internoA, "cenario_teste", Cenario(relatorio, 2));
        var opcoesAutor = await LerJson(await _externoA.GetAsync($"{UrlGrupo}/entidades/cenario_teste/campos/relatorio_teste/opcoes"));
        var opcoesInterno = await LerJson(await _internoA.GetAsync($"{UrlGrupo}/entidades/cenario_teste/campos/relatorio_teste/opcoes"));

        Assert.Equal(HttpStatusCode.Created, doAutor.StatusCode);
        Assert.Equal("PENDENTE", (await LerJson(doAutor))["situacaoAprovacao"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, doInterno.StatusCode);
        Assert.Contains("ainda não foi aprovado", (await LerJson(doInterno))["errors"]!["relatorio_teste"]![0]!.GetValue<string>());
        Assert.Contains(relatorio, opcoesAutor["itens"]!.AsArray().Select(i => i!["id"]!.GetValue<string>()));
        Assert.DoesNotContain(relatorio, opcoesInterno["itens"]!.AsArray().Select(i => i!["id"]!.GetValue<string>()));
        // A opção diz a situação: o front marca o pendente do próprio autor como "(pendente)".
        var opcao = opcoesAutor["itens"]!.AsArray().Single(i => i!["id"]!.GetValue<string>() == relatorio)!;
        Assert.Equal("PENDENTE", opcao["situacaoAprovacao"]!.GetValue<string>());
        Assert.All(opcoesInterno["itens"]!.AsArray(), i => Assert.Equal("APROVADO", i!["situacaoAprovacao"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Decisao_FluxoCompletoComRejeicaoReenvioEAprovacao_EHistorico()
    {
        // RN35 e RN36
        await PrepararAsync();
        await Configurar(_gestorA, EquipeA, "relatorio_teste", true);
        var relatorio = await CriarRelatorioPendenteAsync("R1");

        Assert.Equal(HttpStatusCode.Forbidden, (await Decidir(_internoA, relatorio, "APROVAR", Justificativa)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Decidir(_gestorB, relatorio, "APROVAR", Justificativa)).StatusCode);

        var semJustificativa = await Decidir(_gestorA, relatorio, "REJEITAR", null);
        Assert.Equal(HttpStatusCode.BadRequest, semJustificativa.StatusCode);
        Assert.NotNull((await LerJson(semJustificativa))["errors"]!["justificativa"]);

        var rejeicao = await LerJson(await Decidir(_gestorA, relatorio, "REJEITAR", "Faltou informar a bateria correta."));
        Assert.Equal("REJEITADO", rejeicao["situacaoAprovacao"]!.GetValue<string>());
        Assert.Equal("gestor.a", rejeicao["ultimaDecisao"]!["decididoPor"]!["matricula"]!.GetValue<string>());

        // Reenvio: o autor corrige e o registro volta para PENDENTE.
        var lido = await LerJson(await _externoA.GetAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}"));
        var reenvio = await _externoA.PutAsJsonAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}", ComVersao(lido, Relatorio(_projetoA, _rtcA, "R1 corrigido")));
        Assert.Equal("PENDENTE", (await LerJson(reenvio))["situacaoAprovacao"]!.GetValue<string>());

        var aprovacao = await LerJson(await Decidir(_gestorA, relatorio, "APROVAR", Justificativa));
        Assert.Equal("APROVADO", aprovacao["situacaoAprovacao"]!.GetValue<string>());
        Assert.Equal("APROVAR", aprovacao["ultimaDecisao"]!["decisao"]!.GetValue<string>());

        // Depois de aprovado: o autor não altera mais e não se decide de novo.
        var alterarAprovado = await _externoA.PutAsJsonAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}",
            ComVersao(aprovacao, Relatorio(_projetoA, _rtcA, "R1 de novo")));
        Assert.Equal(HttpStatusCode.Forbidden, alterarAprovado.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Decidir(_gestorA, relatorio, "REJEITAR", Justificativa)).StatusCode);

        // Histórico: criação, rejeição, alteração (reenvio) e aprovação, nessa ordem.
        var historico = await LerJson(await _gestorA.GetAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}/historico"));
        var itens = historico["itens"]!.AsArray().Reverse().ToList();
        Assert.Equal(["CRIACAO", "REJEICAO", "ALTERACAO", "APROVACAO"], itens.Select(i => i!["operacao"]!.GetValue<string>()));
        Assert.Equal("Faltou informar a bateria correta.", itens[1]!["justificativa"]!.GetValue<string>());
        Assert.Equal(Justificativa, itens[3]!["justificativa"]!.GetValue<string>());
        Assert.Contains(itens[2]!["mudancas"]!.AsArray(), m => m!["campo"]!.GetValue<string>() == "situacaoAprovacao");
    }

    [Fact]
    public async Task Interno_CriaRelatorioJaAprovado()
    {
        // RN34
        await PrepararAsync();
        await Configurar(_gestorA, EquipeA, "relatorio_teste", true);

        var resposta = await Postar(_internoA, "relatorio_teste", Relatorio(_projetoA, _rtcA, "R1"));

        Assert.Equal("APROVADO", (await LerJson(resposta))["situacaoAprovacao"]!.GetValue<string>());
    }

    [Fact]
    public async Task FilaDePendentes_GestorDeASoVeASuaEquipe_AdministradorVeTodas()
    {
        // RN36
        await PrepararAsync();
        await Configurar(_gestorA, EquipeA, "relatorio_teste", true);
        await Configurar(Cliente, _equipeB, "relatorio_teste", true);
        var relatorioA = await CriarRelatorioPendenteAsync("R-A");
        EquipeDosAtalhos = _equipeB;
        var projetoB = await CriarProjeto("Projeto B");
        var rtcB = await CriarRtc();
        await Postar(_externoB, "relatorio_teste", Relatorio(projetoB, rtcB, "R-B"));

        var filaGestor = await LerJson(await _gestorA.GetAsync("/api/v1/aprovacoes/pendentes"));
        var filaAdmin = await LerJson(await Cliente.GetAsync("/api/v1/aprovacoes/pendentes?entidade=relatorio_teste"));

        var item = Assert.Single(filaGestor["itens"]!.AsArray())!;
        Assert.Equal(relatorioA, item["id"]!.GetValue<string>());
        Assert.Equal("relatorio_teste", item["entidade"]!["chave"]!.GetValue<string>());
        Assert.Equal("testes_desempenho", item["grupo"]!.GetValue<string>());
        Assert.Equal("R-A", item["rotulo"]!.GetValue<string>());
        Assert.Equal("externo.a", item["criadoPor"]!["matricula"]!.GetValue<string>());
        Assert.Equal(2, filaAdmin["total"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.Forbidden, (await _internoA.GetAsync("/api/v1/aprovacoes/pendentes")).StatusCode);
    }

    [Fact]
    public async Task ListarEntidades_ComEquipeIdInformaRequerAprovacao()
    {
        await PrepararAsync();
        await Configurar(_gestorA, EquipeA, "relatorio_teste", true);

        var entidades = (await LerJson(await _externoA.GetAsync($"{UrlGrupo}/entidades?equipeId={EquipeA.Id}"))).AsArray();

        Assert.True(entidades.Single(e => e!["chave"]!.GetValue<string>() == "relatorio_teste")!["requerAprovacao"]!.GetValue<bool>());
        Assert.False(entidades.Single(e => e!["chave"]!.GetValue<string>() == "projeto")!["requerAprovacao"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await _externoA.GetAsync($"{UrlGrupo}/entidades?equipeId={_equipeB.Id}")).StatusCode);
    }

    [Fact]
    public async Task ListarEntidades_SemEquipeId_TrazAsEquipesVisiveisComAprovacao()
    {
        // ADR 0045: o front decide onde mostrar a aprovação numa chamada só, sem uma por equipe.
        await PrepararAsync();
        await Configurar(_gestorA, EquipeA, "relatorio_teste", true);

        List<string> Equipes(JsonNode entidades, string chave) => entidades.AsArray()
            .Single(e => e!["chave"]!.GetValue<string>() == chave)!["equipesComAprovacao"]!.AsArray()
            .Select(i => i!.GetValue<string>()).ToList();
        var doExternoA = await LerJson(await _externoA.GetAsync($"{UrlGrupo}/entidades"));
        var doExternoB = await LerJson(await _externoB.GetAsync($"{UrlGrupo}/entidades"));
        var doAdmin = await LerJson(await Cliente.GetAsync($"{UrlGrupo}/entidades"));

        Assert.Equal([EquipeA.Id.ToString()], Equipes(doExternoA, "relatorio_teste"));
        Assert.Empty(Equipes(doExternoA, "projeto"));
        Assert.Empty(Equipes(doExternoA, "comunidade"));   // global: nunca tem aprovação
        Assert.Empty(Equipes(doExternoB, "relatorio_teste"));   // só as equipes que ele vê
        Assert.Equal([EquipeA.Id.ToString()], Equipes(doAdmin, "relatorio_teste"));
    }

    private async Task PrepararAsync()
    {
        _equipeB = await CriarEquipeAsync("Equipe B");
        (_, _gestorA) = await CriarUsuarioAsync("gestor.a", Perfil.Gestor, [EquipeA]);
        (_, _internoA) = await CriarUsuarioAsync("interno.a", Perfil.Interno, [EquipeA]);
        (_, _externoA) = await CriarUsuarioAsync("externo.a", Perfil.Externo, [EquipeA]);
        (_, _outroExternoA) = await CriarUsuarioAsync("externo2.a", Perfil.Externo, [EquipeA]);
        (_, _externoB) = await CriarUsuarioAsync("externo.b", Perfil.Externo, [_equipeB]);
        (_, _gestorB) = await CriarUsuarioAsync("gestor.b", Perfil.Gestor, [_equipeB]);

        _projetoA = await CriarProjeto("Projeto A");
        _rtcA = await CriarRtc();
        _endpointA = await CriarEndpoint(_projetoA, "consulta");
        _ambiente = await Criar("ambiente", new JsonObject { ["nome"] = "Homologação" });
        _status = await Criar("status_resultado", new JsonObject { ["descricao"] = "Aprovado" });
    }

    private async Task<string> SiglaAsync()
    {
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade " + Guid.NewGuid().ToString("N")[..6] });
        return await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "S" + Guid.NewGuid().ToString("N")[..6] });
    }

    private async Task<string> CriarRelatorioPendenteAsync(string bateria)
    {
        var resposta = await Postar(_externoA, "relatorio_teste", Relatorio(_projetoA, _rtcA, bateria));
        var corpo = await LerJson(resposta);
        Assert.True(resposta.StatusCode == HttpStatusCode.Created, corpo.ToJsonString());
        Assert.Equal("PENDENTE", corpo["situacaoAprovacao"]!.GetValue<string>());
        return corpo["id"]!.GetValue<string>();
    }

    private JsonObject Relatorio(string projeto, string rtc, string titulo) => new()
    {
        ["projeto"] = projeto,
        ["rtc"] = rtc,
        ["ambiente"] = _ambiente,
        ["status_resultado"] = _status,
        ["titulo"] = titulo,
        ["data_execucao"] = "2026-10-21T14:30:00-03:00",
        ["bateria_testes"] = 1,
        ["tipo_teste"] = "API",
        ["executado_por"] = "Equipe externa",
    };

    private JsonObject Cenario(string relatorio, int numero) => DadosDoCenario(relatorio, _endpointA, numero);

    private string UrlConfiguracao(Equipe equipe, string entidade) =>
        $"/api/v1/equipes/{equipe.Id}/configuracoes/grupos/testes_desempenho/entidades/{entidade}/aprovacao";

    private Task<HttpResponseMessage> Configurar(HttpClient cliente, Equipe equipe, string entidade, bool ativa) =>
        cliente.PutAsJsonAsync(UrlConfiguracao(equipe, entidade), new { ativa });

    private static Task<HttpResponseMessage> Decidir(HttpClient cliente, string relatorio, string decisao, string? justificativa) =>
        cliente.PostAsJsonAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}/aprovacao", new { decisao, justificativa });
}
