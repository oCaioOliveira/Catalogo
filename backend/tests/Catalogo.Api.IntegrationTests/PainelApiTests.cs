using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Application.Abstracoes;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Consultas;
using Catalogo.Domain.Historico;
using Catalogo.Domain.Painel;
using Catalogo.Infrastructure.Painel;
using Catalogo.Infrastructure.Persistencia;
using Catalogo.Infrastructure.Repositorios;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

/// <summary>
/// Painel gerencial (RN78 a RN88) com dados CONTROLADOS em duas equipes; na Equipe A, aprovação e avaliação de relatorio_teste
/// estão ativas. Os números esperados estão calculados à mão nos comentários de <see cref="PrepararAsync"/>.
/// </summary>
[Collection(ColecaoApi.Nome)]
public sealed class PainelApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private const string UrlPainel = "/api/v1/painel/grupos/testes_desempenho";

    private Equipe _equipeA = null!, _equipeB = null!;
    private HttpClient _gestorA = null!, _gestorB = null!, _internoA = null!, _interno2A = null!, _interno3A = null!, _externoA = null!;
    private string _projetoA = "", _rtcA = "", _endpointA = "", _projetoB = "", _rtcB = "";
    private string _homologacao = "", _producao = "", _sucesso = "", _falha = "";
    private string _r1 = "", _r2 = "", _r3 = "", _r4 = "", _r5 = "", _r6 = "";

    /// <summary>
    /// Equipe A (aprovação e modelo "qualidade" ativos em relatorio_teste):
    ///   r1 Homologação, Sucesso, 2026-03 (admin; aprovado)  notas 5, 4, 3 → média 4,0
    ///   r2 Homologação, Falha,   2026-04 (admin; aprovado)  notas 2, 2, 2 → média 2,0
    ///   r3 Produção,    Sucesso, 2026-03 (admin; aprovado)  notas 5, 5    → média 5,0 (só 2 notas: fora do ranking); tipo Estresse
    ///      (os demais são do tipo Carga)
    ///   r4 Produção,    Falha,   2026-04 (EXTERNO; aprovado 2 h depois de criado)
    ///   r5 Produção,    Falha,   2026-04 (EXTERNO; rejeitado 4 h depois de criado)
    ///   r6 Produção,    Falha,   2026-04 (EXTERNO; pendente, criado há 10 dias)
    ///   cenários: r1 → vazão 10 e 20; r2 → 30; r3 → 100.
    /// Equipe B: um relatório (Homologação, Sucesso), sem aprovação nem avaliação.
    /// </summary>
    private async Task PrepararAsync()
    {
        _equipeA = await CriarEquipeAsync("Equipe A");
        _equipeB = await CriarEquipeAsync("Equipe B");
        (_, _gestorA) = await CriarUsuarioAsync("gestor.a", Perfil.Gestor, [_equipeA]);
        (_, _gestorB) = await CriarUsuarioAsync("gestor.b", Perfil.Gestor, [_equipeB]);
        (_, _internoA) = await CriarUsuarioAsync("interno.a", Perfil.Interno, [_equipeA]);
        (_, _interno2A) = await CriarUsuarioAsync("interno2.a", Perfil.Interno, [_equipeA]);
        (_, _interno3A) = await CriarUsuarioAsync("interno3.a", Perfil.Interno, [_equipeA]);
        (_, _externoA) = await CriarUsuarioAsync("externo.a", Perfil.Externo, [_equipeA]);

        _homologacao = await Criar("ambiente", new JsonObject { ["nome"] = "Homologação" });
        _producao = await Criar("ambiente", new JsonObject { ["nome"] = "Produção" });
        _sucesso = await Criar("status_resultado", new JsonObject { ["descricao"] = "Sucesso" });
        _falha = await Criar("status_resultado", new JsonObject { ["descricao"] = "Falha" });

        EquipeDosAtalhos = _equipeB;
        _projetoB = await CriarProjeto("Projeto B");
        _rtcB = await CriarRtc();
        await Criar("relatorio_teste", Relatorio(_projetoB, _rtcB, _homologacao, _sucesso, "2026-03-15T10:00:00-03:00", "Bateria B"));

        EquipeDosAtalhos = _equipeA;
        _projetoA = await CriarProjeto("Projeto A");
        _rtcA = await CriarRtc();
        _endpointA = await CriarEndpoint(_projetoA, "pagamento");
        Ok(await _gestorA.PutAsJsonAsync(UrlConfiguracao("aprovacao"), new { ativa = true }));
        Ok(await _gestorA.PostAsJsonAsync(UrlConfiguracao("avaliacao"), new JsonObject
        {
            ["nome"] = "Avaliação da Equipe A",
            ["criterios"] = new JsonArray(new JsonObject { ["chave"] = "qualidade", ["nome"] = "Qualidade", ["tipo"] = "ESTRELAS" }),
        }));

        _r1 = await Criar("relatorio_teste", Relatorio(_projetoA, _rtcA, _homologacao, _sucesso, "2026-03-10T10:00:00-03:00", "Bateria 1"));
        _r2 = await Criar("relatorio_teste", Relatorio(_projetoA, _rtcA, _homologacao, _falha, "2026-04-05T10:00:00-03:00", "Bateria 2"));
        _r3 = await Criar("relatorio_teste", Relatorio(_projetoA, _rtcA, _producao, _sucesso, "2026-03-20T10:00:00-03:00", "Bateria 3", "Estresse"));
        _r4 = await CriarComo(_externoA, Relatorio(_projetoA, _rtcA, _producao, _falha, "2026-04-10T10:00:00-03:00", "Bateria 4"));
        _r5 = await CriarComo(_externoA, Relatorio(_projetoA, _rtcA, _producao, _falha, "2026-04-11T10:00:00-03:00", "Bateria 5"));
        _r6 = await CriarComo(_externoA, Relatorio(_projetoA, _rtcA, _producao, _falha, "2026-04-12T10:00:00-03:00", "Bateria 6"));

        Ok(await _gestorA.PostAsJsonAsync($"{UrlRegistros("relatorio_teste")}/{_r4}/aprovacao", new { decisao = "APROVAR", justificativa = "Conferido e completo" }));
        Ok(await _gestorA.PostAsJsonAsync($"{UrlRegistros("relatorio_teste")}/{_r5}/aprovacao", new { decisao = "REJEITAR", justificativa = "Faltam os cenários de pico" }));

        // Tempos controlados: a criação vai para 2 h e 4 h antes da decisão; o pendente, para 10 dias atrás.
        await AntecederCriacaoDaDecisaoAsync(_r4, TimeSpan.FromHours(2));
        await AntecederCriacaoDaDecisaoAsync(_r5, TimeSpan.FromHours(4));
        await DefinirCriadoEmAsync(_r6, DateTime.UtcNow.AddDays(-10));

        foreach (var (relatorio, vazao, numero) in new[] { (_r1, 10.0, 1), (_r1, 20.0, 2), (_r2, 30.0, 1), (_r3, 100.0, 1) })
            await Criar("cenario_teste", DadosDoCenario(relatorio, _endpointA, numero, vazao));

        foreach (var (cliente, relatorio, estrelas) in new[]
                 {
                     (_internoA, _r1, 5), (_interno2A, _r1, 4), (_interno3A, _r1, 3),
                     (_internoA, _r2, 2), (_interno2A, _r2, 2), (_interno3A, _r2, 2),
                     (_internoA, _r3, 5), (_interno2A, _r3, 5),
                 })
        {
            Ok(await cliente.PutAsJsonAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}/avaliacoes/minha", new JsonObject
            {
                ["respostas"] = new JsonObject { ["qualidade"] = new JsonObject { ["estrelas"] = estrelas } },
            }));
        }
    }

    // ---- Acesso (RN78, RN79) ----

    [Theory]
    [InlineData(Perfil.Interno)]
    [InlineData(Perfil.Externo)]
    public async Task InternoEExterno_403NosTresEndpoints(Perfil perfil)
    {
        var (_, cliente) = await CriarUsuarioAsync("sem.painel", perfil, [EquipePadrao]);

        foreach (var url in new[]
                 {
                     $"{UrlPainel}/visao-geral",
                     $"{UrlPainel}/entidades/relatorio_teste/indicadores",
                     $"{UrlPainel}/entidades/relatorio_teste/indicadores/exportar?formato=csv",
                 })
        {
            var resposta = await cliente.GetAsync(url);
            Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
            Assert.Equal("SEM_PERMISSAO", (await LerJson(resposta))["codigo"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task Gestor_FiltrandoOutraEquipe_403()
    {
        await PrepararAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await _gestorA.GetAsync($"{UrlPainel}/visao-geral?equipeId={_equipeB.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _gestorA.GetAsync($"{UrlPainel}/entidades/relatorio_teste/indicadores?equipeId={_equipeB.Id}")).StatusCode);
    }

    [Fact]
    public async Task GestorA_RegistroNovoDaEquipeB_NaoMudaNenhumNumero()
    {
        await PrepararAsync();
        var antes = (await VisaoGeral(_gestorA)).ToJsonString();
        var indicadoresAntes = (await Indicadores(_gestorA, "relatorio_teste", "agruparPor=status_resultado")).ToJsonString();

        // Na Equipe B: um relatório, um cenário e um pendente de outro gestor. Nada global (que o gestor A veria).
        EquipeDosAtalhos = _equipeB;
        var endpointB = await CriarEndpoint(_projetoB, "consulta");
        var rb = await Criar("relatorio_teste", Relatorio(_projetoB, _rtcB, _producao, _falha, "2026-04-01T10:00:00-03:00", "Bateria B2"));
        await Criar("cenario_teste", DadosDoCenario(rb, endpointB, 1, 999));

        Assert.Equal(antes, (await VisaoGeral(_gestorA)).ToJsonString());
        Assert.Equal(indicadoresAntes, (await Indicadores(_gestorA, "relatorio_teste", "agruparPor=status_resultado")).ToJsonString());
        // O gestor B vê o dele.
        Assert.Equal(2, Volume(await VisaoGeral(_gestorB), "relatorio_teste")["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Administrador_VeTudo_EFiltraPorEquipe()
    {
        await PrepararAsync();

        Assert.Equal(6, Volume(await VisaoGeral(_gestorA), "relatorio_teste")["total"]!.GetValue<int>());
        Assert.Equal(7, Volume(await VisaoGeral(Cliente), "relatorio_teste")["total"]!.GetValue<int>());

        var soB = await VisaoGeral(Cliente, $"equipeId={_equipeB.Id}");
        Assert.Equal(1, Volume(soB, "relatorio_teste")["total"]!.GetValue<int>());
        // B não tem aprovação nem avaliação: os blocos não aparecem. As entidades globais continuam (os 2 ambientes).
        Assert.Null(soB["aprovacao"]);
        Assert.Null(soB["avaliacao"]);
        Assert.Equal(2, Volume(soB, "ambiente")["total"]!.GetValue<int>());
    }

    // ---- Visão geral (RN81 a RN84) ----

    [Fact]
    public async Task VisaoGeral_Aprovacao_ConfereComOCalculoManual()
    {
        await PrepararAsync();

        var total = (await VisaoGeral(_gestorA))["aprovacao"]!["total"]!;

        // Situações: r1, r2, r3, r4 aprovados; r5 rejeitado; r6 pendente (há 10 dias > 7: antigo).
        Assert.Equal(1, total["pendentes"]!.GetValue<int>());
        Assert.Equal(4, total["aprovados"]!.GetValue<int>());
        Assert.Equal(1, total["rejeitados"]!.GetValue<int>());
        Assert.Equal(1, total["pendentesAntigos"]!.GetValue<int>());
        // Tempo até a primeira decisão: (2 h + 4 h) / 2 = 3 h. Taxa de rejeição: 1 / (1 + 1) = 0,5.
        Assert.Equal(3.0, total["tempoMedioPrimeiraDecisaoHoras"]!.GetValue<double>());
        Assert.Equal(2, total["primeirasDecisoes"]!.GetValue<int>());
        Assert.Equal(1, total["aprovacoes"]!.GetValue<int>());
        Assert.Equal(1, total["rejeicoes"]!.GetValue<int>());
        Assert.Equal(0.5, total["taxaRejeicao"]!.GetValue<double>());
    }

    [Fact]
    public async Task VisaoGeral_Avaliacao_MediaDistribuicaoERankingComMinimoDeNotas()
    {
        await PrepararAsync();

        var avaliacao = (await VisaoGeral(_gestorA))["avaliacao"]!;
        var criterio = avaliacao["porEntidade"]![0]!["criterios"]![0]!;

        Assert.Equal(3, avaliacao["minimoAvaliacoesRanking"]!.GetValue<int>());
        // Todas as notas: (5+4+3 + 2+2+2 + 5+5) / 8 = 3,5.
        Assert.Equal("qualidade", criterio["criterio"]!.GetValue<string>());
        Assert.Equal(3.5, criterio["media"]!.GetValue<double>());
        Assert.Equal(8, criterio["quantidade"]!.GetValue<int>());
        Assert.Equal("""{"1":0,"2":3,"3":1,"4":1,"5":3}""", criterio["distribuicao"]!.ToJsonString());
        // r3 (média 5) tem só 2 notas: fica fora do ranking.
        Assert.Equal([_r1, _r2], criterio["melhores"]!.AsArray().Select(i => i!["id"]!.GetValue<string>()).ToList());
        Assert.Equal([_r2, _r1], criterio["piores"]!.AsArray().Select(i => i!["id"]!.GetValue<string>()).ToList());
        Assert.Equal(4.0, criterio["melhores"]![0]!["media"]!.GetValue<double>());
        Assert.Equal("Bateria 1", criterio["melhores"]![0]!["rotulo"]!.GetValue<string>());
    }

    [Fact]
    public async Task VisaoGeral_Volume_EAtividadeDoExterno()
    {
        await PrepararAsync();

        var visao = await VisaoGeral(_gestorA);
        // O pendente do EXTERNO conta para o gestor (ele o vê na listagem): 6 relatórios. 4 cenários.
        Assert.Equal(6, Volume(visao, "relatorio_teste")["total"]!.GetValue<int>());
        Assert.Equal(4, Volume(visao, "cenario_teste")["total"]!.GetValue<int>());
        var externo = visao["atividade"]!.AsArray().Single(a => a!["matricula"]!.GetValue<string>() == "externo.a")!;
        Assert.Equal(3, externo["criacoes"]!.GetValue<int>());
        Assert.Equal(0, externo["alteracoes"]!.GetValue<int>());

        // A atividade do administrador vista pelo gestor A = a do administrador só na Equipe A (mais as globais).
        var doAdminParaGestor = visao["atividade"]!.AsArray().Single(a => a!["matricula"]!.GetValue<string>() == "admin.testes")!;
        var doAdminNaEquipeA = (await VisaoGeral(Cliente, $"equipeId={_equipeA.Id}"))["atividade"]!.AsArray()
            .Single(a => a!["matricula"]!.GetValue<string>() == "admin.testes")!;
        Assert.Equal(doAdminNaEquipeA.ToJsonString(), doAdminParaGestor.ToJsonString());
    }

    [Fact]
    public async Task VisaoGeral_Fuso_RegistroDas01hUtcDoDia1ContaNoMesAnterior()
    {
        await PrepararAsync();
        var relatorio = await Criar("relatorio_teste", Relatorio(_projetoA, _rtcA, _homologacao, _sucesso, "2026-03-31T10:00:00-03:00", "Fuso"));
        // 2026-04-01 01:00 UTC = 2026-03-31 22:00 em São Paulo.
        await DefinirCriadoEmAsync(relatorio, new DateTime(2026, 4, 1, 1, 0, 0, DateTimeKind.Utc));

        var marco = Volume(await VisaoGeral(_gestorA, "de=2026-03-01&ate=2026-03-31"), "relatorio_teste");
        var abril = Volume(await VisaoGeral(_gestorA, "de=2026-04-01&ate=2026-04-30"), "relatorio_teste");
        var serie = (await VisaoGeral(_gestorA, "de=2026-03-01&ate=2026-03-31"))["volume"]!["criadosPorMes"]!;

        Assert.Equal(1, marco["criadosNoPeriodo"]!.GetValue<int>());
        Assert.Equal(0, abril["criadosNoPeriodo"]!.GetValue<int>());
        Assert.Equal("""[{"mes":"2026-03","quantidade":1}]""", serie.ToJsonString());

        var porMes = await Indicadores(_gestorA, "relatorio_teste", "bateria_testes=Fuso&agruparPor=criadoEm:mes");
        Assert.Equal("2026-03", porMes["linhas"]![0]!["criadoEm:mes"]!.GetValue<string>());
    }

    [Fact]
    public async Task VisaoGeral_ParametroInvalido_400ComTodosOsErros()
    {
        var resposta = await Cliente.GetAsync($"{UrlPainel}/visao-geral?de=2026-02-30&ate=ontem&agruparPor=x");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await LerJson(resposta))["errors"]!.AsObject();
        Assert.Equal(["agruparPor", "ate", "de"], erros.Select(e => e.Key).Order(StringComparer.Ordinal).ToList());
    }

    // ---- Agregação (RN85 a RN87) ----

    [Fact]
    public async Task Indicadores_ContagemPorStatus()
    {
        await PrepararAsync();

        var resultado = await Indicadores(_gestorA, "relatorio_teste", "agruparPor=status_resultado");

        Assert.Equal("""[{"chave":"status_resultado","nome":"Status","tipo":"REFERENCIA","periodo":null}]""", resultado["dimensoes"]!.ToJsonString());
        Assert.Equal("""[{"chave":"contagem","nome":"Contagem"}]""", resultado["metricas"]!.ToJsonString());
        // Pela dimensão (o rótulo), em ordem alfabética: Falha (r2, r4, r5, r6) e Sucesso (r1, r3).
        Assert.Equal(
            $$"""[{"status_resultado":{"id":"{{_falha}}","rotulo":"Falha"},"contagem":4},{"status_resultado":{"id":"{{_sucesso}}","rotulo":"Sucesso"},"contagem":2}]""",
            resultado["linhas"]!.ToJsonString());
        Assert.False(resultado["truncado"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Indicadores_MediaDeVazaoPorAmbiente_PeloRelatorioDoCenario()
    {
        await PrepararAsync();

        var resultado = await Indicadores(_gestorA, "cenario_teste",
            "agruparPor=relatorio_teste.ambiente&metricas=media:vazao_media_tps,contagem&ordenarPor=-media:vazao_media_tps");

        // Produção: r3 → 100. Homologação: r1 (10, 20) e r2 (30) → 20.
        var linhas = resultado["linhas"]!.AsArray();
        Assert.Equal(["Produção", "Homologação"], linhas.Select(l => l!["relatorio_teste.ambiente"]!["rotulo"]!.GetValue<string>()).ToList());
        Assert.Equal([100m, 20m], linhas.Select(l => l!["media:vazao_media_tps"]!.GetValue<decimal>()).ToList());
        Assert.Equal([1, 3], linhas.Select(l => l!["contagem"]!.GetValue<int>()).ToList());
        Assert.Equal("Média de Vazão média (TPS)", resultado["metricas"]![0]!["nome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Indicadores_DuasDimensoes_AmbientePorMesDeExecucao()
    {
        await PrepararAsync();

        var resultado = await Indicadores(_gestorA, "relatorio_teste", "agruparPor=ambiente,data_execucao:mes");

        var linhas = resultado["linhas"]!.AsArray()
            .Select(l => $"{l!["ambiente"]!["rotulo"]} {l["data_execucao:mes"]} {l["contagem"]}")
            .ToList();
        Assert.Equal(["Homologação 2026-03 1", "Homologação 2026-04 1", "Produção 2026-03 1", "Produção 2026-04 3"], linhas);
    }

    [Fact]
    public async Task Indicadores_MediaDeAvaliacaoPorAmbiente_IgnoraItensSemNota()
    {
        await PrepararAsync();

        var resultado = await Indicadores(_gestorA, "relatorio_teste", "agruparPor=ambiente&metricas=media_avaliacao:qualidade");

        // Homologação: média das médias de r1 (4,0) e r2 (2,0) = 3,0. Produção: só r3 (5,0); r4, r5 e r6 não têm nota.
        var linhas = resultado["linhas"]!.AsArray();
        Assert.Equal([3m, 5m], linhas.Select(l => l!["media_avaliacao:qualidade"]!.GetValue<decimal>()).ToList());
        Assert.Equal([2, 4], linhas.Select(l => l!["contagem"]!.GetValue<int>()).ToList());
    }

    [Fact]
    public async Task Indicadores_FiltrosDaListagemEPeriodo_SaoAplicados()
    {
        await PrepararAsync();

        Assert.Equal(1, Total(await Indicadores(_gestorA, "relatorio_teste", "bateria_testes=Bateria 1")));
        Assert.Equal(1, Total(await Indicadores(_gestorA, "relatorio_teste", "situacaoAprovacao=PENDENTE")));
        Assert.Equal(4, Total(await Indicadores(_gestorA, "relatorio_teste", $"status_resultado={_falha}")));
        Assert.Equal(1, Total(await Indicadores(_gestorA, "relatorio_teste", "avaliacao.qualidade.de=4.5")));
        // O pendente foi criado há 10 dias; os demais, hoje.
        var hoje = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"));
        Assert.Equal(5, Total(await Indicadores(_gestorA, "relatorio_teste", $"de={hoje.AddDays(-1):yyyy-MM-dd}")));
    }

    [Fact]
    public async Task Indicadores_SemDimensaoESemRegistros_UmaLinhaComZero()
    {
        // RN85/RN86: sem agruparPor há sempre uma linha; sem registros, contagem 0, soma 0 e média nula.
        var resultado = await Indicadores(Cliente, "cenario_teste", "metricas=media:vazao_media_tps,soma:vazao_media_tps");

        Assert.Equal("""[{"contagem":0,"media:vazao_media_tps":null,"soma:vazao_media_tps":0}]""", resultado["linhas"]!.ToJsonString());
    }

    [Fact]
    public async Task Indicadores_ParametrosInvalidos_400ComTodosOsErrosJuntos()
    {
        var resposta = await Cliente.GetAsync(
            $"{UrlPainel}/entidades/cenario_teste/indicadores?agruparPor=vazao_media_tps&metricas=media:relatorio_teste&limite=0&numero_cenario=abc&pagina=2");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var corpo = await LerJson(resposta);
        Assert.Equal("VALIDACAO", corpo["codigo"]!.GetValue<string>());
        Assert.Equal(["agruparPor", "limite", "metricas", "numero_cenario", "pagina"],
            corpo["errors"]!.AsObject().Select(e => e.Key).Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    public async Task Indicadores_EntidadeInexistente_404()
    {
        var resposta = await Cliente.GetAsync($"{UrlPainel}/entidades/nao_existe/indicadores");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("ENTIDADE_NAO_ENCONTRADA", (await LerJson(resposta))["codigo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Indicadores_DuasDimensoes_UmaConsultaComGroupByNoBanco()
    {
        // docs/adr/0031: a agregação é um SQL só, com GROUP BY e as agregações no banco (nada é lido para a memória antes).
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var grupo = (await escopo.ServiceProvider.GetRequiredService<IMetadadosProvider>().ObterGrupoAsync("testes_desempenho", default))!;
        var cenario = grupo.ObterEntidade("cenario_teste")!;
        var indicadores = InterpretadorIndicadores.Interpretar(grupo, cenario,
            [new("agruparPor", "relatorio_teste.ambiente,relatorio_teste.data_execucao:mes"), new("metricas", "media:vazao_media_tps,maximo:percentil_95_ms")]);
        var registros = ConsultaRegistrosLinq.Filtrar(contexto, cenario.Id, new ConsultaRegistros([], Ordenacao.Padrao, Paginacao.Padrao));

        var sql = ConsultaIndicadoresLinq.Montar(contexto, registros, new PeriodoPainel(null, null, TimeSpan.FromHours(-3)), indicadores).ToQueryString();

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(sql, "GROUP BY"));
        Assert.Contains("AVG(", sql);
        Assert.Contains("MAX(", sql);
        Assert.Contains("COUNT(*)", sql);
        Assert.Contains("DATEPART(month", sql);
        Assert.Contains("LEFT JOIN", sql);
    }

    // ---- Caminhos por referência (RN89) ----

    [Fact]
    public async Task Indicadores_PorCaminho_AgrupaPeloAmbienteEFiltraPeloTipoDoRelatorio()
    {
        await PrepararAsync();

        var resultado = await Indicadores(_gestorA, "cenario_teste",
            "agruparPor=relatorio_teste.ambiente&metricas=media:vazao_media_tps&relatorio_teste.tipo_teste=Carga");

        // Cenários de relatórios do tipo Carga: r1 (10, 20) e r2 (30), ambos em Homologação. O de r3 (Estresse, 100) fica fora.
        var linha = Assert.Single(resultado["linhas"]!.AsArray())!;
        Assert.Equal("Homologação", linha["relatorio_teste.ambiente"]!["rotulo"]!.GetValue<string>());
        Assert.Equal(3, linha["contagem"]!.GetValue<int>());
        Assert.Equal(20m, linha["media:vazao_media_tps"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Indicadores_DoisSaltos_AgrupaPeloProjetoDoRelatorio_EFiltraPelaDataDeExecucao()
    {
        await PrepararAsync();

        // cenário → relatório → projeto (nome). Execução a partir de abril: só r2 (um cenário, vazão 30).
        var resultado = await Indicadores(_gestorA, "cenario_teste",
            "agruparPor=relatorio_teste.projeto.nome&metricas=soma:vazao_media_tps&relatorio_teste.data_execucao.de=2026-04-01");

        var linha = Assert.Single(resultado["linhas"]!.AsArray())!;
        Assert.Equal("Projeto A", linha["relatorio_teste.projeto.nome"]!.GetValue<string>());
        Assert.Equal(30m, linha["soma:vazao_media_tps"]!.GetValue<decimal>());
        Assert.Contains(" › ", resultado["dimensoes"]![0]!["nome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Listagem_FiltroPorCaminho_UmEDoisSaltos()
    {
        await PrepararAsync();

        // 1 salto: cenários de relatórios do tipo Estresse (só o de r3).
        var porTipo = await ListarOk("cenario_teste", "relatorio_teste.tipo_teste=Estresse");
        Assert.Equal(100m, Assert.Single(porTipo["itens"]!.AsArray())!["dados"]!["vazao_media_tps"]!.GetValue<decimal>());

        // 2 saltos: cenários cujo relatório está num ambiente de nome "Produção" (o mesmo cenário).
        Assert.Equal(1, (await ListarOk("cenario_teste", "relatorio_teste.ambiente.nome=Produção"))["total"]!.GetValue<int>());

        // Com operador: relatórios de projetos cujo nome contém "B" (só o da Equipe B, que o administrador vê).
        Assert.Equal(1, (await ListarOk("relatorio_teste", "projeto.nome.contem=B"))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Caminho_NuncaExpoeDadosDeOutraEquipe()
    {
        await PrepararAsync();
        // Equipe B: um relatório em Produção, do tipo Carga, com um cenário de vazão 999.
        EquipeDosAtalhos = _equipeB;
        var endpointB = await CriarEndpoint(_projetoB, "consulta");
        var relatorioB = await Criar("relatorio_teste", Relatorio(_projetoB, _rtcB, _producao, _sucesso, "2026-03-01T10:00:00-03:00", "Bateria B2"));
        await Criar("cenario_teste", DadosDoCenario(relatorioB, endpointB, 1, 999));

        // O gestor da Equipe A: nem o agrupamento nem o filtro por caminho chegam ao cenário (nem ao relatório) da Equipe B.
        var porAmbiente = await Indicadores(_gestorA, "cenario_teste", "agruparPor=relatorio_teste.ambiente&metricas=maximo:vazao_media_tps");
        Assert.Equal(4, porAmbiente["linhas"]!.AsArray().Sum(l => l!["contagem"]!.GetValue<int>()));
        Assert.DoesNotContain(porAmbiente["linhas"]!.AsArray(), l => l!["maximo:vazao_media_tps"]!.GetValue<decimal>() == 999m);
        Assert.Equal(0, Total(await Indicadores(_gestorA, "cenario_teste", $"relatorio_teste.projeto={_projetoB}")));
        Assert.Equal(0, Total(await Indicadores(_gestorA, "cenario_teste", "relatorio_teste.bateria_testes=Bateria B2")));
        Assert.Equal(0, (await Ler(await _gestorA.GetAsync($"{UrlRegistros("cenario_teste")}?relatorio_teste.projeto={_projetoB}")))["total"]!.GetValue<int>());

        // E o gestor da Equipe B vê só o dele, pelos mesmos caminhos.
        var daEquipeB = await Indicadores(_gestorB, "cenario_teste", "agruparPor=relatorio_teste.ambiente&metricas=maximo:vazao_media_tps");
        var linhaB = Assert.Single(daEquipeB["linhas"]!.AsArray())!;
        Assert.Equal(999m, linhaB["maximo:vazao_media_tps"]!.GetValue<decimal>());
        Assert.Equal(1, Total(await Indicadores(_gestorB, "cenario_teste", $"relatorio_teste.projeto={_projetoB}")));
    }

    [Fact]
    public async Task Indicadores_CaminhosInvalidos_400ComTodosOsErrosJuntos()
    {
        var resposta = await Cliente.GetAsync(
            $"{UrlPainel}/entidades/cenario_teste/indicadores?agruparPor=relatorio_teste.nao_existe&metricas=media:relatorio_teste.ambiente"
            + "&relatorio_teste.projeto.sigla.codigo_sigla=X&relatorio_teste.data_execucao.de=ontem&numero_cenario.ambiente=1");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await LerJson(resposta))["errors"]!.AsObject();
        Assert.Equal(
            ["agruparPor", "metricas", "numero_cenario.ambiente", "relatorio_teste.data_execucao.de", "relatorio_teste.projeto.sigla.codigo_sigla"],
            erros.Select(e => e.Key).Order(StringComparer.Ordinal).ToList());
        Assert.Contains("no máximo 2 saltos", erros["relatorio_teste.projeto.sigla.codigo_sigla"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Indicadores_CaminhosDeDoisSaltos_UmaConsultaComGroupByEUmJoinPorSalto()
    {
        // ADR 0038: cada salto é um JOIN com RegistroReferencia, e a agregação continua sendo um SQL só, com GROUP BY no banco.
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var grupo = (await escopo.ServiceProvider.GetRequiredService<IMetadadosProvider>().ObterGrupoAsync("testes_desempenho", default))!;
        var cenario = grupo.ObterEntidade("cenario_teste")!;
        var indicadores = InterpretadorIndicadores.Interpretar(grupo, cenario,
            [new("agruparPor", "relatorio_teste.projeto.sigla,relatorio_teste.data_execucao:mes"), new("metricas", "media:vazao_media_tps")]);
        var filtros = InterpretadorConsulta.Interpretar(cenario, [new("relatorio_teste.ambiente.nome", "Produção")], grupo: grupo);
        var registros = ConsultaRegistrosLinq.Filtrar(contexto, cenario.Id, filtros);

        var sql = ConsultaIndicadoresLinq.Montar(contexto, registros, new PeriodoPainel(null, null, TimeSpan.FromHours(-3)), indicadores).ToQueryString();

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(sql, "GROUP BY"));
        Assert.Contains("AVG(", sql);
        // Saltos: 2 (projeto.sigla) + 1 (data_execucao) nas dimensões e 2 no filtro (ambiente.nome); a sigla, REFERENCIA, mais um
        // JOIN com RegistroReferencia para o valor.
        Assert.True(System.Text.RegularExpressions.Regex.Matches(sql, @"\[RegistroReferencia\]").Count >= 5, sql);
    }

    // ---- Exportação (RN88) ----

    [Fact]
    public async Task Exportar_CsvEXlsx_ComAsMesmasLinhas()
    {
        await PrepararAsync();
        const string Consulta = "agruparPor=relatorio_teste.ambiente&metricas=media:vazao_media_tps";

        var csv = await _gestorA.GetAsync($"{UrlPainel}/entidades/cenario_teste/indicadores/exportar?formato=csv&{Consulta}");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("cenario_teste_indicadores_", csv.Content.Headers.ContentDisposition!.FileNameStar ?? csv.Content.Headers.ContentDisposition.FileName);
        var linhas = Encoding.UTF8.GetString((await csv.Content.ReadAsByteArrayAsync())[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Ambiente (Relatório);Contagem;Média de Vazão média (TPS)", "Homologação;3;20", "Produção;1;100"], linhas);

        var xlsx = await _gestorA.GetAsync($"{UrlPainel}/entidades/cenario_teste/indicadores/exportar?formato=xlsx&{Consulta}");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        using var planilha = new XLWorkbook(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync()));
        var aba = planilha.Worksheets.First();
        Assert.Equal("Ambiente (Relatório)", aba.Cell(1, 1).GetString());
        Assert.Equal("Homologação", aba.Cell(2, 1).GetString());
        Assert.Equal(20d, aba.Cell(2, 3).GetDouble());
        Assert.Equal(100d, aba.Cell(3, 3).GetDouble());
    }

    [Fact]
    public async Task Exportar_SemFormato_400()
    {
        var resposta = await Cliente.GetAsync($"{UrlPainel}/entidades/relatorio_teste/indicadores/exportar?agruparPor=nao_existe");

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal(["agruparPor", "formato"], (await LerJson(resposta))["errors"]!.AsObject().Select(e => e.Key).Order(StringComparer.Ordinal).ToList());
    }

    // ---- Apoio ----

    private static async Task<JsonNode> Ler(HttpResponseMessage resposta)
    {
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == HttpStatusCode.OK, $"{resposta.RequestMessage?.RequestUri}: {(int)resposta.StatusCode} {corpo}");
        return JsonNode.Parse(corpo)!;
    }

    private static async Task<JsonNode> VisaoGeral(HttpClient cliente, string consulta = "") =>
        await Ler(await cliente.GetAsync($"{UrlPainel}/visao-geral?{consulta}"));

    private static async Task<JsonNode> Indicadores(HttpClient cliente, string entidade, string consulta) =>
        await Ler(await cliente.GetAsync($"{UrlPainel}/entidades/{entidade}/indicadores?{consulta}"));

    private static JsonNode Volume(JsonNode visao, string entidade) =>
        visao["volume"]!["porEntidade"]!.AsArray().Single(e => e!["entidade"]!.GetValue<string>() == entidade)!;

    private static int Total(JsonNode indicadores) => Assert.Single(indicadores["linhas"]!.AsArray())!["contagem"]!.GetValue<int>();

    private static void Ok(HttpResponseMessage resposta) =>
        Assert.True(resposta.IsSuccessStatusCode, $"{resposta.RequestMessage?.RequestUri}: {(int)resposta.StatusCode} {resposta.Content.ReadAsStringAsync().Result}");

    private string UrlConfiguracao(string recurso) =>
        $"/api/v1/equipes/{_equipeA.Id}/configuracoes/grupos/testes_desempenho/entidades/relatorio_teste/{recurso}";

    private static JsonObject Relatorio(
        string projeto, string rtc, string ambiente, string status, string dataExecucao, string bateria, string tipo = "Carga") => new()
    {
        ["projeto"] = projeto,
        ["rtc"] = rtc,
        ["ambiente"] = ambiente,
        ["status_resultado"] = status,
        ["data_execucao"] = dataExecucao,
        ["bateria_testes"] = bateria,
        ["tipo_teste"] = tipo,
        ["executado_por"] = "Equipe de Desempenho",
    };

    private static async Task<string> CriarComo(HttpClient cliente, JsonObject dados)
    {
        var resposta = await Postar(cliente, "relatorio_teste", dados);
        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == HttpStatusCode.Created, corpo);
        return JsonNode.Parse(corpo)!["id"]!.GetValue<string>();
    }

    /// <summary>Muda a data de criação direto no banco (os testes precisam de tempos conhecidos).</summary>
    private async Task DefinirCriadoEmAsync(string registro, DateTime criadoEmUtc)
    {
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var id = Guid.Parse(registro);
        await contexto.Registros.Where(r => r.Id == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.CriadoEm, criadoEmUtc));
    }

    /// <summary>A criação passa a ser exatamente <paramref name="antes"/> da decisão (APROVACAO ou REJEICAO) do histórico.</summary>
    private async Task AntecederCriacaoDaDecisaoAsync(string registro, TimeSpan antes)
    {
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var id = Guid.Parse(registro);
        var decididoEm = await contexto.HistoricosAlteracao
            .Where(h => h.AlvoId == id && (h.Operacao == OperacaoHistorico.Aprovacao || h.Operacao == OperacaoHistorico.Rejeicao))
            .Select(h => h.OcorridoEm)
            .SingleAsync();
        await DefinirCriadoEmAsync(registro, decididoEm - antes);
    }
}
