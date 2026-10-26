using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

// Avaliação de itens (RN40 a RN48), com a Equipe A (a padrão), a Equipe B e a Equipe C (sem modelo).
public class AvaliacaoApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private Equipe EquipeA => EquipePadrao;
    private Equipe _equipeB = null!, _equipeC = null!;
    private HttpClient _gestorA = null!, _internoA = null!, _interno2A = null!, _interno3A = null!, _externoA = null!;
    private string _projetoA = null!;

    // ---- Configuração (RN40 a RN42) ----

    [Fact]
    public async Task Configuracao_GestorCriaEmA_SegundoAtivo409_OutraEquipe404_Interno403_AdministradorCriaOutroEmB()
    {
        await PrepararAsync();

        // RN40: o GESTOR de A cria o modelo de A.
        var criado = await CriarModelo(_gestorA, EquipeA, ModeloA());
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        var modeloA = await LerJson(criado);
        Assert.Equal(["qualidade", "observacoes"], Chaves(modeloA["criterios"]!));
        Assert.Equal("OBRIGATORIO", modeloA["criterios"]![0]!["comentario"]!.GetValue<string>());

        // RN41: um ativo por entidade e equipe.
        var segundo = await CriarModelo(_gestorA, EquipeA, ModeloA());
        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
        Assert.Equal("MODELO_ATIVO_EXISTENTE", (await LerJson(segundo))["codigo"]!.GetValue<string>());

        // RN40: GESTOR de outra equipe 404; INTERNO 403.
        Assert.Equal(HttpStatusCode.NotFound, (await CriarModelo(_gestorA, _equipeB, ModeloA())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CriarModelo(_internoA, EquipeA, ModeloA())).StatusCode);

        // O ADMINISTRADOR cria em B um modelo diferente, com três critérios, sem afetar A.
        var emB = await CriarModelo(Cliente, _equipeB, ModeloB());
        Assert.Equal(HttpStatusCode.Created, emB.StatusCode);
        Assert.Equal(["qualidade", "clareza", "cobertura"], Chaves((await LerJson(emB))["criterios"]!));
        var aDepois = await LerJson(await _gestorA.GetAsync(UrlModelo(EquipeA)));
        Assert.Equal(["qualidade", "observacoes"], Chaves(aDepois["criterios"]!));

        // RN40: o formulário aparece nos metadados da equipe.
        var entidades = (await LerJson(await _internoA.GetAsync($"{UrlGrupo}/entidades?equipeId={EquipeA.Id}"))).AsArray();
        var relatorio = entidades.Single(e => e!["chave"]!.GetValue<string>() == "relatorio_teste")!;
        Assert.Equal(["qualidade", "observacoes"], Chaves(relatorio["modeloAvaliacao"]!["criterios"]!));
        Assert.Null(entidades.Single(e => e!["chave"]!.GetValue<string>() == "projeto")!["modeloAvaliacao"]);
    }

    [Fact]
    public async Task Configuracao_DefinicaoInvalida_400ComTodosOsErros()
    {
        // RN42
        await PrepararAsync();
        var corpo = new JsonObject
        {
            ["nome"] = "Inválido",
            ["criterios"] = new JsonArray(
                new JsonObject { ["chave"] = "Qualidade", ["nome"] = "Q", ["tipo"] = "ESTRELAS", ["tamanhoMaximoTexto"] = 5000 },
                new JsonObject { ["chave"] = "obs", ["nome"] = "O", ["tipo"] = "TEXTO", ["comentario"] = "OBRIGATORIO" }),
        };

        var resposta = await CriarModelo(_gestorA, EquipeA, corpo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await LerJson(resposta))["errors"]!.AsObject();
        Assert.Equal(["criterios[0].chave", "criterios[0].tamanhoMaximoTexto", "criterios[1].comentario"], erros.Select(e => e.Key).Order());
    }

    // ---- Avaliar (RN44 a RN46) ----

    [Fact]
    public async Task Avaliar_InternoAvaliaEEdita_ContinuaUmaSo()
    {
        // RN45
        await PrepararComModeloAsync();
        var relatorio = await CriarRelatorio(_projetoA);

        var criada = await Avaliar(_internoA, relatorio, Nota(3));
        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        var versao = (await LerJson(criada))["versao"]!.GetValue<string>();

        var editada = await Avaliar(_internoA, relatorio, Nota(5, observacoes: "Revisado"), versao);
        Assert.Equal(HttpStatusCode.OK, editada.StatusCode);
        Assert.Equal(5, (await LerJson(editada))["respostas"]!["qualidade"]!["estrelas"]!.GetValue<int>());

        var avaliacoes = await LerJson(await _internoA.GetAsync(UrlAvaliacoes(relatorio)));
        Assert.Equal(1, avaliacoes["avaliacoes"]!["total"]!.GetValue<int>());
        Assert.Equal(5.0m, avaliacoes["resumo"]!["criterios"]![0]!["media"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Avaliar_SemComentarioObrigatorioSeisEstrelasECriterioInexistente_400ComOsTresErros()
    {
        // RN46
        await PrepararComModeloAsync();
        var relatorio = await CriarRelatorio(_projetoA);

        var resposta = await Avaliar(_internoA, relatorio, new JsonObject
        {
            ["qualidade"] = new JsonObject { ["estrelas"] = 6 },
            ["inexistente"] = new JsonObject { ["estrelas"] = 3 },
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erros = (await LerJson(resposta))["errors"]!.AsObject();
        Assert.Equal(
            ["respostas.inexistente", "respostas.qualidade.comentario", "respostas.qualidade.estrelas"],
            erros.Select(e => e.Key).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Avaliar_Externo403_RelatorioDeB404_EquipeSemModelo400()
    {
        // RN44
        await PrepararComModeloAsync();
        var relatorioA = await CriarRelatorio(_projetoA);
        EquipeDosAtalhos = _equipeB;
        var relatorioB = await CriarRelatorio(await CriarProjeto("Projeto B"));
        EquipeDosAtalhos = _equipeC;
        var relatorioC = await CriarRelatorio(await CriarProjeto("Projeto C"));
        var (_, internoC) = await CriarUsuarioAsync("interno.c", Perfil.Interno, [_equipeC]);

        Assert.Equal(HttpStatusCode.Forbidden, (await Avaliar(_externoA, relatorioA, Nota(4))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Avaliar(_internoA, relatorioB, Nota(4))).StatusCode);
        var semModelo = await Avaliar(internoC, relatorioC, Nota(4));
        Assert.Equal(HttpStatusCode.BadRequest, semModelo.StatusCode);
        Assert.Contains("não tem modelo de avaliação ativo", (await LerJson(semModelo))["errors"]!["modelo"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Avaliar_ComAprovacaoAtivaEmA_Pendente409_DepoisDeAprovadoPode()
    {
        // RN44
        await PrepararComModeloAsync();
        var rtc = await CriarRtc();
        var ambiente = await Criar("ambiente", new JsonObject { ["nome"] = "Homologação" });
        var status = await Criar("status_resultado", new JsonObject { ["descricao"] = "Concluído" });
        await _gestorA.PutAsJsonAsync(
            $"/api/v1/equipes/{EquipeA.Id}/configuracoes/grupos/testes_desempenho/entidades/relatorio_teste/aprovacao", new { ativa = true });
        var pendente = await LerJson(await Postar(_externoA, "relatorio_teste", new JsonObject
        {
            ["projeto"] = _projetoA, ["rtc"] = rtc, ["ambiente"] = ambiente, ["status_resultado"] = status,
            ["data_execucao"] = "2026-10-21T14:30:00-03:00", ["bateria_testes"] = "Externa", ["tipo_teste"] = "Carga",
            ["executado_por"] = "Fornecedor",
        }));
        var id = pendente["id"]!.GetValue<string>();
        Assert.Equal("PENDENTE", pendente["situacaoAprovacao"]!.GetValue<string>());

        var antes = await Avaliar(_internoA, id, Nota(4));
        Assert.Equal(HttpStatusCode.Conflict, antes.StatusCode);
        Assert.Equal("REGISTRO_NAO_APROVADO", (await LerJson(antes))["codigo"]!.GetValue<string>());

        await _gestorA.PostAsJsonAsync($"{UrlRegistros("relatorio_teste")}/{id}/aprovacao",
            new { decisao = "APROVAR", justificativa = "Conferido com a equipe responsável." });
        Assert.Equal(HttpStatusCode.Created, (await Avaliar(_internoA, id, Nota(4))).StatusCode);
    }

    // ---- Resumo e visibilidade (RN47, RN48) ----

    [Fact]
    public async Task Resumo_TresAvaliadores_Media4_Distribuicao_Minha_EExternoVeSoOResumo()
    {
        // RN47
        await PrepararComModeloAsync();
        var relatorio = await CriarRelatorio(_projetoA);
        await Avaliar(_internoA, relatorio, Nota(4));
        await Avaliar(_interno2A, relatorio, Nota(5));
        await Avaliar(_interno3A, relatorio, Nota(3));

        var doInterno2 = await LerJson(await _interno2A.GetAsync(UrlAvaliacoes(relatorio)));
        var qualidade = doInterno2["resumo"]!["criterios"]![0]!;
        Assert.Equal(4.0m, qualidade["media"]!.GetValue<decimal>());
        Assert.Equal(3, qualidade["quantidade"]!.GetValue<int>());
        Assert.Equal("""{"1":0,"2":0,"3":1,"4":1,"5":1}""", qualidade["distribuicao"]!.ToJsonString());
        Assert.Equal(3, doInterno2["resumo"]!["quantidadeAvaliadores"]!.GetValue<int>());
        Assert.Equal(5, doInterno2["resumo"]!["minha"]!["respostas"]!["qualidade"]!["estrelas"]!.GetValue<int>());
        Assert.Equal("interno2.a", doInterno2["resumo"]!["minha"]!["usuario"]!["matricula"]!.GetValue<string>());
        Assert.Equal(3, doInterno2["avaliacoes"]!["itens"]!.AsArray().Count);

        // O retorno do registro traz as médias e "minha" do usuário logado (RN48).
        var registro = await LerJson(await _interno3A.GetAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}"));
        Assert.Equal(4.0m, registro["avaliacao"]!["medias"]!["qualidade"]!.GetValue<decimal>());
        Assert.True(registro["avaliacao"]!["minha"]!.GetValue<bool>());

        // O EXTERNO vê o resumo, mas não as avaliações individuais nem o histórico delas.
        var doExterno = await LerJson(await _externoA.GetAsync(UrlAvaliacoes(relatorio)));
        Assert.Equal(4.0m, doExterno["resumo"]!["criterios"]![0]!["media"]!.GetValue<decimal>());
        Assert.Null(doExterno["resumo"]!["minha"]);
        Assert.Null(doExterno["avaliacoes"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await _externoA.GetAsync($"{UrlAvaliacoes(relatorio)}/historico")).StatusCode);
        Assert.False((await LerJson(await _externoA.GetAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}")))["avaliacao"]!["minha"]!.GetValue<bool>());
    }

    // ---- Evolução do modelo (RN43) ----

    [Fact]
    public async Task Evolucao_NovoCriterioObrigatorio_NaoInvalidaAntigas_ExcluirComRespostas400_DesativarTiraDasMedias()
    {
        await PrepararComModeloAsync();
        var relatorio = await CriarRelatorio(_projetoA);
        await Avaliar(_internoA, relatorio, Nota(4, observacoes: "Ok"));

        // Novo critério obrigatório: a avaliação antiga continua válida e no resumo.
        var modelo = await LerJson(await _gestorA.GetAsync(UrlModelo(EquipeA)));
        var comClareza = ModeloA(modelo["versao"]!.GetValue<string>());
        comClareza["criterios"]!.AsArray().Add(new JsonObject { ["chave"] = "clareza", ["nome"] = "Clareza", ["tipo"] = "ESTRELAS", ["obrigatorio"] = true });
        var alterado = await _gestorA.PutAsJsonAsync(UrlModelo(EquipeA), comClareza);
        Assert.Equal(HttpStatusCode.OK, alterado.StatusCode);
        var resumo = (await LerJson(await _internoA.GetAsync(UrlAvaliacoes(relatorio))))["resumo"]!;
        Assert.Equal(1, resumo["quantidadeAvaliadores"]!.GetValue<int>());
        Assert.Equal(4.0m, resumo["criterios"]!.AsArray().Single(c => c!["chave"]!.GetValue<string>() == "qualidade")!["media"]!.GetValue<decimal>());

        // Excluir "qualidade" (com respostas): 400 explicando que é preciso desativar.
        var versao = (await LerJson(alterado))["versao"]!.GetValue<string>();
        var semQualidade = new JsonObject
        {
            ["versao"] = versao,
            ["nome"] = "Avaliação da Equipe A",
            ["criterios"] = new JsonArray(Observacoes(), new JsonObject { ["chave"] = "clareza", ["nome"] = "Clareza", ["tipo"] = "ESTRELAS" }),
        };
        var excluir = await _gestorA.PutAsJsonAsync(UrlModelo(EquipeA), semQualidade);
        Assert.Equal(HttpStatusCode.BadRequest, excluir.StatusCode);
        Assert.Contains("desative-o", (await LerJson(excluir))["errors"]!["criterios"]![0]!.GetValue<string>());

        // Desativar funciona e tira o critério das médias (o registro e o resumo deixam de mostrá-lo).
        var desativando = semQualidade.DeepClone().AsObject();
        desativando["criterios"]!.AsArray().Add(Qualidade(ativo: false));
        var desativado = await _gestorA.PutAsJsonAsync(UrlModelo(EquipeA), desativando);
        Assert.Equal(HttpStatusCode.OK, desativado.StatusCode);
        var criterioQualidade = (await LerJson(desativado))["criterios"]!.AsArray().Single(c => c!["chave"]!.GetValue<string>() == "qualidade")!;
        Assert.False(criterioQualidade["ativo"]!.GetValue<bool>());
        Assert.True(criterioQualidade["possuiRespostas"]!.GetValue<bool>());
        var depois = await LerJson(await _internoA.GetAsync(UrlAvaliacoes(relatorio)));
        Assert.Equal(["clareza"], depois["resumo"]!["criterios"]!.AsArray().Select(c => c!["chave"]!.GetValue<string>()));
        var registro = await LerJson(await _internoA.GetAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}"));
        Assert.False(registro["avaliacao"]!["medias"]!.AsObject().ContainsKey("qualidade"));
    }

    [Fact]
    public async Task Evolucao_DesativarModelo_ImpedeNovasAvaliacoesEMantemAsAntigasVisiveis()
    {
        // RN41
        await PrepararComModeloAsync();
        var relatorio = await CriarRelatorio(_projetoA);
        await Avaliar(_internoA, relatorio, Nota(5));

        var desativado = await _gestorA.PatchAsJsonAsync($"{UrlModelo(EquipeA)}/ativacao", new { ativo = false });
        Assert.Equal(HttpStatusCode.OK, desativado.StatusCode);
        Assert.False((await LerJson(desativado))["ativo"]!.GetValue<bool>());

        Assert.Equal(HttpStatusCode.BadRequest, (await Avaliar(_interno2A, relatorio, Nota(3))).StatusCode);
        var avaliacoes = await LerJson(await _internoA.GetAsync(UrlAvaliacoes(relatorio)));
        Assert.Null(avaliacoes["resumo"]);
        var anterior = avaliacoes["resumosAnteriores"]![0]!;
        Assert.Equal("Avaliação da Equipe A", anterior["modelo"]!["nome"]!.GetValue<string>());
        Assert.False(anterior["modelo"]!["ativo"]!.GetValue<bool>());
        Assert.Equal(5.0m, anterior["criterios"]![0]!["media"]!.GetValue<decimal>());
        Assert.Equal("Avaliação da Equipe A", avaliacoes["avaliacoes"]!["itens"]![0]!["modelo"]!["nome"]!.GetValue<string>());

        // Sem modelo ativo, o registro não traz "avaliacao"; e a equipe pode criar outro modelo.
        Assert.Null((await LerJson(await _internoA.GetAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}")))["avaliacao"]);
        Assert.Equal(HttpStatusCode.Created, (await CriarModelo(_gestorA, EquipeA, ModeloA())).StatusCode);
    }

    // ---- Listagem (RN48) ----

    [Fact]
    public async Task Listagem_OrdenaPelaMediaComNaoAvaliadosPorUltimo_EFiltraPelaMedia()
    {
        await PrepararComModeloAsync();
        var nota3 = await CriarRelatorio(_projetoA, bateria: "Nota 3");
        var semNota = await CriarRelatorio(_projetoA, bateria: "Sem nota");
        var nota45 = await CriarRelatorio(_projetoA, bateria: "Nota 4.5");
        await Avaliar(_internoA, nota3, Nota(3));
        await Avaliar(_internoA, nota45, Nota(4));
        await Avaliar(_interno2A, nota45, Nota(5));

        var decrescente = await LerJson(await _internoA.GetAsync($"{UrlRegistros("relatorio_teste")}?ordenacao=-avaliacao.qualidade"));
        Assert.Equal([nota45, nota3, semNota], Ids(decrescente));
        var crescente = await LerJson(await _internoA.GetAsync($"{UrlRegistros("relatorio_teste")}?ordenacao=avaliacao.qualidade"));
        Assert.Equal([nota3, nota45, semNota], Ids(crescente));   // sem nota por último nos dois sentidos

        var filtrado = await LerJson(await _internoA.GetAsync($"{UrlRegistros("relatorio_teste")}?avaliacao.qualidade.de=4"));
        Assert.Equal([nota45], Ids(filtrado));
        Assert.Equal(4.5m, filtrado["itens"]![0]!["avaliacao"]!["medias"]!["qualidade"]!.GetValue<decimal>());

        var desconhecido = await _internoA.GetAsync($"{UrlRegistros("relatorio_teste")}?ordenacao=avaliacao.observacoes");
        Assert.Equal(HttpStatusCode.BadRequest, desconhecido.StatusCode);   // TEXTO não tem média
    }

    // ---- Histórico (RN32, RN40, RN45) e exclusão ----

    [Fact]
    public async Task Historico_ModeloEAvaliacao_CriacaoAlteracaoERemocao()
    {
        await PrepararComModeloAsync();
        var modelo = await LerJson(await _gestorA.GetAsync(UrlModelo(EquipeA)));
        var renomeado = ModeloA(modelo["versao"]!.GetValue<string>());
        renomeado["nome"] = "Avaliação A (revisada)";
        await _gestorA.PutAsJsonAsync(UrlModelo(EquipeA), renomeado);

        var historicoModelo = await LerJson(await _gestorA.GetAsync($"{UrlModelo(EquipeA)}/historico"));
        var itensModelo = historicoModelo["itens"]!.AsArray();
        Assert.Equal(["ALTERACAO", "CRIACAO"], itensModelo.Select(i => i!["operacao"]!.GetValue<string>()));
        Assert.Contains(itensModelo[0]!["mudancas"]!.AsArray(), m => m!["campo"]!.GetValue<string>() == "nome");

        var relatorio = await CriarRelatorio(_projetoA);
        var criada = await LerJson(await Avaliar(_internoA, relatorio, Nota(3)));
        await Avaliar(_internoA, relatorio, Nota(4), criada["versao"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NoContent, (await _internoA.DeleteAsync($"{UrlAvaliacoes(relatorio)}/minha")).StatusCode);

        var historico = await LerJson(await _gestorA.GetAsync($"{UrlAvaliacoes(relatorio)}/historico"));
        var itens = historico["itens"]!.AsArray().Reverse().ToList();
        Assert.Equal(["CRIACAO", "ALTERACAO", "EXCLUSAO"], itens.Select(i => i!["operacao"]!.GetValue<string>()));
        Assert.All(itens, i => Assert.Equal("interno.a", i!["usuario"]!["matricula"]!.GetValue<string>()));
        var alteracao = itens[1]!["mudancas"]!.AsArray().Single(m => m!["campo"]!.GetValue<string>() == "qualidade.estrelas")!;
        Assert.Equal((3, 4), (alteracao["de"]!.GetValue<int>(), alteracao["para"]!.GetValue<int>()));
    }

    [Fact]
    public async Task Exclusao_RelatorioAvaliado_RemoveAsAvaliacoesEOHistoricoGuardaOQueExistia()
    {
        // RN45
        await PrepararComModeloAsync();
        var relatorio = await CriarRelatorio(_projetoA);
        await Avaliar(_internoA, relatorio, Nota(4));
        await Avaliar(_interno2A, relatorio, Nota(5));

        Assert.Equal(HttpStatusCode.NoContent, (await _gestorA.DeleteAsync($"{UrlRegistros("relatorio_teste")}/{relatorio}")).StatusCode);

        using (var escopo = Banco.Fabrica.Services.CreateScope())
        {
            var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
            Assert.Equal(0, await contexto.Avaliacoes.CountAsync(a => a.RegistroId == Guid.Parse(relatorio)));
            Assert.Equal(0, await contexto.AvaliacoesRespostas.CountAsync());
        }

        var historico = await LerJson(await _gestorA.GetAsync($"{UrlAvaliacoes(relatorio)}/historico"));
        var operacoes = historico["itens"]!.AsArray().Select(i => i!["operacao"]!.GetValue<string>()).ToList();
        Assert.Equal(2, operacoes.Count(o => o == "EXCLUSAO"));
        Assert.Equal(2, operacoes.Count(o => o == "CRIACAO"));
    }

    // ---- Apoio ----

    [Fact]
    public async Task ListarEntidades_TrazOsCriteriosDeEstrelasDosModelosAtivosNasEquipesVisiveis()
    {
        // RN48 e ADR 0045: as colunas de média da listagem; texto não entra, e a chave não se repete entre equipes.
        await PrepararComModeloAsync();
        (_, var gestorB) = await CriarUsuarioAsync("gestor.b", Perfil.Gestor, [_equipeB]);
        Assert.Equal(HttpStatusCode.Created, (await CriarModelo(gestorB, _equipeB, ModeloB())).StatusCode);

        List<string> Criterios(JsonNode entidades) => entidades.AsArray()
            .Single(e => e!["chave"]!.GetValue<string>() == "relatorio_teste")!["criteriosAvaliacao"]!.AsArray()
            .Select(c => c!["chave"]!.GetValue<string>()).ToList();

        Assert.Equal(["qualidade"], Criterios(await LerJson(await _internoA.GetAsync($"{UrlGrupo}/entidades"))));
        Assert.Equal(["qualidade", "clareza", "cobertura"], Criterios(await LerJson(await gestorB.GetAsync($"{UrlGrupo}/entidades"))));
        Assert.Equal(["qualidade", "clareza", "cobertura"], Criterios(await LerJson(await Cliente.GetAsync($"{UrlGrupo}/entidades"))));
    }

    private async Task PrepararAsync()
    {
        _equipeB = await CriarEquipeAsync("Equipe B");
        _equipeC = await CriarEquipeAsync("Equipe C");
        (_, _gestorA) = await CriarUsuarioAsync("gestor.a", Perfil.Gestor, [EquipeA]);
        (_, _internoA) = await CriarUsuarioAsync("interno.a", Perfil.Interno, [EquipeA]);
        (_, _interno2A) = await CriarUsuarioAsync("interno2.a", Perfil.Interno, [EquipeA]);
        (_, _interno3A) = await CriarUsuarioAsync("interno3.a", Perfil.Interno, [EquipeA]);
        (_, _externoA) = await CriarUsuarioAsync("externo.a", Perfil.Externo, [EquipeA]);
        _projetoA = await CriarProjeto("Projeto A");
    }

    private async Task PrepararComModeloAsync()
    {
        await PrepararAsync();
        var criado = await CriarModelo(_gestorA, EquipeA, ModeloA());
        Assert.True(criado.StatusCode == HttpStatusCode.Created, await criado.Content.ReadAsStringAsync());
    }

    private Task<string> CriarRelatorio(string projeto, string bateria = "Bateria 1") => CriarRelatorio(projeto, bateria: bateria, rtc: null);

    private static string UrlModelo(Equipe equipe) =>
        $"/api/v1/equipes/{equipe.Id}/configuracoes/grupos/testes_desempenho/entidades/relatorio_teste/avaliacao";

    private static string UrlAvaliacoes(string relatorio) => $"{UrlRegistros("relatorio_teste")}/{relatorio}/avaliacoes";

    private static Task<HttpResponseMessage> CriarModelo(HttpClient cliente, Equipe equipe, JsonObject corpo) =>
        cliente.PostAsJsonAsync(UrlModelo(equipe), corpo);

    private static JsonObject Qualidade(bool ativo = true) => new()
    {
        ["chave"] = "qualidade", ["nome"] = "Qualidade", ["tipo"] = "ESTRELAS", ["obrigatorio"] = true, ["comentario"] = "OBRIGATORIO",
        ["ativo"] = ativo,
    };

    private static JsonObject Observacoes() => new() { ["chave"] = "observacoes", ["nome"] = "Observações", ["tipo"] = "TEXTO" };

    /// <summary>Equipe A: "qualidade" (estrelas, comentário OBRIGATORIO) e "observacoes" (texto, opcional).</summary>
    private static JsonObject ModeloA(string? versao = null)
    {
        var corpo = new JsonObject { ["nome"] = "Avaliação da Equipe A", ["criterios"] = new JsonArray(Qualidade(), Observacoes()) };
        if (versao is not null)
            corpo["versao"] = versao;
        return corpo;
    }

    /// <summary>Equipe B: três critérios, alguns com comentário.</summary>
    private static JsonObject ModeloB() => new()
    {
        ["nome"] = "Avaliação da Equipe B",
        ["criterios"] = new JsonArray(
            new JsonObject { ["chave"] = "qualidade", ["nome"] = "Qualidade", ["tipo"] = "ESTRELAS", ["comentario"] = "OPCIONAL" },
            new JsonObject { ["chave"] = "clareza", ["nome"] = "Clareza", ["tipo"] = "ESTRELAS", ["comentario"] = "OBRIGATORIO" },
            new JsonObject { ["chave"] = "cobertura", ["nome"] = "Cobertura", ["tipo"] = "ESTRELAS", ["obrigatorio"] = true }),
    };

    private static JsonObject Nota(int estrelas, string comentario = "Resultado consistente", string? observacoes = null)
    {
        var respostas = new JsonObject { ["qualidade"] = new JsonObject { ["estrelas"] = estrelas, ["comentario"] = comentario } };
        if (observacoes is not null)
            respostas["observacoes"] = new JsonObject { ["texto"] = observacoes };
        return respostas;
    }

    private static Task<HttpResponseMessage> Avaliar(HttpClient cliente, string relatorio, JsonObject respostas, string? versao = null) =>
        cliente.PutAsJsonAsync($"{UrlAvaliacoes(relatorio)}/minha", new JsonObject { ["versao"] = versao, ["respostas"] = respostas });

    private static List<string> Chaves(JsonNode criterios) => criterios.AsArray().Select(c => c!["chave"]!.GetValue<string>()).ToList();

    private static List<string> Ids(JsonNode pagina) => pagina["itens"]!.AsArray().Select(i => i!["id"]!.GetValue<string>()).ToList();
}
