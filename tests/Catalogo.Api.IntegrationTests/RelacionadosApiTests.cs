using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Comum;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Metadados;
using Catalogo.Infrastructure.Persistencia;
using Catalogo.Infrastructure.Repositorios;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

/// <summary>
/// RN94: GET .../registros/{id}/relacionados. Uma entrada por campo REFERENCIA ativo de entidade ativa do grupo que aponta para a
/// entidade do item, com a quantidade que o usuário VÊ (a mesma regra da listagem). No seed (Testes de Desempenho): relatório e
/// endpoint apontam para projeto; cenário e link apontam para relatório (CASCATA: filhas); relatório aponta para ambiente (global).
/// </summary>
public class RelacionadosApiTests(BancoDeTestesFixture banco) : TesteDeApi(banco)
{
    private static string UrlRelacionados(string entidade, string id) => $"{UrlRegistros(entidade)}/{id}/relacionados";

    private static async Task<Dictionary<string, JsonNode>> Relacionados(HttpClient cliente, string entidade, string id)
    {
        var resposta = await cliente.GetAsync(UrlRelacionados(entidade, id));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return (await LerJson(resposta)).AsArray()
            .ToDictionary(r => $"{r!["entidade"]!["chave"]!.GetValue<string>()}.{r["campo"]!["chave"]!.GetValue<string>()}", r => r!);
    }

    private static int Quantidade(Dictionary<string, JsonNode> relacionados, string chave) =>
        relacionados[chave]["quantidade"]!.GetValue<int>();

    [Fact]
    public async Task Projeto_RelatoriosEEndpoints_ComAQuantidade_EZeroTambemVem()
    {
        var projeto = await CriarProjeto("P1");
        await CriarRelatorio(projeto, titulo: "B1");
        await CriarRelatorio(projeto, titulo: "B2");
        await CriarEndpoint(projeto, "consulta");
        var outro = await CriarProjeto("P2");
        await CriarRelatorio(outro, titulo: "B3");

        var resposta = await LerJson(await Cliente.GetAsync(UrlRelacionados("projeto", projeto)));
        var relacionados = await Relacionados(Cliente, "projeto", projeto);
        var doOutro = await Relacionados(Cliente, "projeto", outro);

        // Ordem dos metadados (ordem das entidades): Relatórios de Teste antes de Endpoints de Serviço.
        Assert.Equal(["relatorio_teste.projeto", "endpoint_servico.projeto"], relacionados.Keys.ToList());
        Assert.Equal(2, Quantidade(relacionados, "relatorio_teste.projeto"));
        Assert.Equal(1, Quantidade(relacionados, "endpoint_servico.projeto"));
        Assert.Equal(0, Quantidade(doOutro, "endpoint_servico.projeto"));
        var relatorios = resposta.AsArray()[0]!;
        Assert.Equal("Relatório de Teste", relatorios["entidade"]!["nome"]!.GetValue<string>());
        Assert.Equal("Relatórios de Teste", relatorios["entidade"]!["nomePlural"]!.GetValue<string>());
        Assert.Equal("Projeto", relatorios["campo"]!["nome"]!.GetValue<string>());
        Assert.False(relatorios["filha"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Relatorio_CenariosELinks_ComoFilhas()
    {
        var projeto = await CriarProjeto("P1");
        var endpoint = await CriarEndpoint(projeto, "consulta");
        var relatorio = await CriarRelatorio(projeto);
        await Criar("cenario_teste", DadosDoCenario(relatorio, endpoint, 1));
        await Criar("cenario_teste", DadosDoCenario(relatorio, endpoint, 2));
        await Criar("link", new JsonObject { ["relatorio_teste"] = relatorio, ["texto_exibicao"] = "Painel", ["url"] = "https://exemplo.com.br" });

        var relacionados = await Relacionados(Cliente, "relatorio_teste", relatorio);

        Assert.Equal(2, Quantidade(relacionados, "cenario_teste.relatorio_teste"));
        Assert.Equal(1, Quantidade(relacionados, "link.relatorio_teste"));
        Assert.True(relacionados["cenario_teste.relatorio_teste"]["filha"]!.GetValue<bool>());
        Assert.True(relacionados["link.relatorio_teste"]["filha"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ItemGlobal_Ambiente_ContaSoOsRelatoriosQueOUsuarioVe()
    {
        // RN26/RN91: o ambiente é global, mas os relatórios são por equipe: um usuário de A não conta os de B.
        var equipeB = await CriarEquipeAsync("Equipe B");
        var ambiente = await Criar("ambiente", new JsonObject { ["nome"] = "Homologação compartilhada" });
        var status = await Criar("status_resultado", new JsonObject { ["descricao"] = "Aprovado X" });
        var projetoA = await CriarProjeto("PA");
        await Criar("relatorio_teste", Relatorio(projetoA, await CriarRtc(), ambiente, status, "A1"));
        await Criar("relatorio_teste", Relatorio(projetoA, await CriarRtc(), ambiente, status, "A2"));
        EquipeDosAtalhos = equipeB;
        var projetoB = await CriarProjeto("PB");
        await Criar("relatorio_teste", Relatorio(projetoB, await CriarRtc(), ambiente, status, "B1"));
        var (_, internoA) = await CriarUsuarioAsync("interno.a", Perfil.Interno, [EquipePadrao]);

        var doInterno = await Relacionados(internoA, "ambiente", ambiente);
        var doAdmin = await Relacionados(Cliente, "ambiente", ambiente);

        Assert.Equal(2, Quantidade(doInterno, "relatorio_teste.ambiente"));
        Assert.Equal(3, Quantidade(doAdmin, "relatorio_teste.ambiente"));
        // A listagem filtrada pelo mesmo campo dá o mesmo total (é ela que o front usa para os itens).
        var lista = await LerJson(await internoA.GetAsync($"{UrlRegistros("relatorio_teste")}?ambiente={ambiente}"));
        Assert.Equal(2, lista["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task OrigemGlobal_SiglasDaComunidade_ContagemIgualAListagemFiltrada()
    {
        // A listagem filtrada pelo campo REFERENCIA (o que o front usa nas seções) dá o mesmo total, também com origem global.
        var comunidade = await Criar("comunidade", new JsonObject { ["nome"] = "Comunidade R" });
        await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "SG-R1" });
        await Criar("sigla", new JsonObject { ["comunidade"] = comunidade, ["codigo_sigla"] = "SG-R2" });
        var (_, interno) = await CriarUsuarioAsync("interno.a", Perfil.Interno, [EquipePadrao]);

        var relacionados = await Relacionados(interno, "comunidade", comunidade);
        var lista = await LerJson(await interno.GetAsync($"{UrlRegistros("sigla")}?comunidade={comunidade}&tamanho=10&ordenacao=-codigo_sigla"));

        Assert.Equal(2, Quantidade(relacionados, "sigla.comunidade"));
        Assert.Equal(2, lista["total"]!.GetValue<int>());
        Assert.Equal("SG-R2", lista["itens"]![0]!["dados"]!["codigo_sigla"]!.GetValue<string>());
    }

    [Fact]
    public async Task Externo_NaoContaPendentesDeTerceiros_ContaOsDele()
    {
        // RN37: com a aprovação ligada, o EXTERNO só vê os não aprovados que ele mesmo criou.
        var configuracao = await Cliente.PutAsJsonAsync(
            $"/api/v1/equipes/{EquipePadrao.Id}/configuracoes/grupos/testes_desempenho/entidades/relatorio_teste/aprovacao",
            new { ativa = true });
        Assert.Equal(HttpStatusCode.OK, configuracao.StatusCode);
        var (_, externo) = await CriarUsuarioAsync("externo.a", Perfil.Externo, [EquipePadrao]);
        var (_, outroExterno) = await CriarUsuarioAsync("externo2.a", Perfil.Externo, [EquipePadrao]);
        var projeto = await CriarProjeto("P1");
        var ambiente = await Criar("ambiente", new JsonObject { ["nome"] = "Homologação E" });
        var status = await Criar("status_resultado", new JsonObject { ["descricao"] = "Aprovado E" });
        await Criar("relatorio_teste", Relatorio(projeto, await CriarRtc(), ambiente, status, "aprovado")); // admin: aprovado
        Assert.Equal(HttpStatusCode.Created,
            (await Postar(externo, "relatorio_teste", Relatorio(projeto, await CriarRtc(), ambiente, status, "meu"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await Postar(outroExterno, "relatorio_teste", Relatorio(projeto, await CriarRtc(), ambiente, status, "dele"))).StatusCode);

        Assert.Equal(2, Quantidade(await Relacionados(externo, "projeto", projeto), "relatorio_teste.projeto"));
        Assert.Equal(3, Quantidade(await Relacionados(Cliente, "projeto", projeto), "relatorio_teste.projeto"));
    }

    [Fact]
    public async Task ItemQueOUsuarioNaoVe_404_ComoODetalhe()
    {
        var equipeB = await CriarEquipeAsync("Equipe B");
        EquipeDosAtalhos = equipeB;
        var projetoB = await CriarProjeto("PB");
        var (_, internoA) = await CriarUsuarioAsync("interno.a", Perfil.Interno, [EquipePadrao]);

        Assert.Equal(HttpStatusCode.NotFound, (await internoA.GetAsync(UrlRelacionados("projeto", projetoB))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente.GetAsync(UrlRelacionados("projeto", Guid.NewGuid().ToString()))).StatusCode);
        // Um id de outra entidade nesta URL também não existe.
        var projetoA = await CriarProjeto("PA");
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente.GetAsync(UrlRelacionados("rtc", projetoA))).StatusCode);
    }

    [Fact]
    public async Task ListagemNaoHabilitadaParaAEquipe_404()
    {
        // RN90: um usuário só de uma equipe que não está habilitada em Testes de Desempenho não vê nada sob a listagem.
        var equipeFora = await CriarEquipeAsync("Equipe Fora", habilitarNoSeed: false);
        var (_, internoFora) = await CriarUsuarioAsync("interno.fora", Perfil.Interno, [equipeFora]);
        var ambiente = await Criar("ambiente", new JsonObject { ["nome"] = "Homologação F" });

        Assert.Equal(HttpStatusCode.NotFound, (await internoFora.GetAsync(UrlRelacionados("ambiente", ambiente))).StatusCode);
    }

    [Fact]
    public async Task ContagemNoBanco_UmSqlSoComGroupBy()
    {
        // ADR 0047: agrupa RegistroReferencia por campo no banco, com o escopo da listagem em subconsulta (nada vai para a memória).
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var grupo = (await escopo.ServiceProvider.GetRequiredService<IMetadadosProvider>().ObterGrupoAsync("testes_desempenho", default))!;
        var projeto = grupo.ObterEntidade("projeto")!;
        var usuario = new UsuarioAtual { Id = Guid.NewGuid(), Matricula = "x", Nome = "x", Perfil = Perfil.Externo, Equipes = new HashSet<Guid> { EquipePadrao.Id } };
        var contagens = Relacoes.QueApontamPara(grupo, projeto)
            .Select(r => new ContagemPorCampo(r.Campo.Id, r.Origem.Id, PreparadorConsultaRegistros.Escopo(usuario, r.Origem, null)))
            .ToList();

        var sql = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToQueryString(
            ConsultaRelacionadosLinq.Montar(contexto, Guid.NewGuid(), contagens));

        Assert.Equal(2, contagens.Count);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(sql, "GROUP BY"));
        Assert.Contains("COUNT(*)", sql);
        Assert.Contains("UNION ALL", sql);
        Assert.Contains($"[{NomesBanco.Tabela("RegistroReferencia")}]", sql);
        // o escopo de aprovação do EXTERNO (RN37) está no WHERE
        Assert.Contains($"[{NomesBanco.Coluna("Registro", "SituacaoAprovacao")}]", sql);
    }

    private static JsonObject Relatorio(string projeto, string rtc, string ambiente, string status, string titulo) => new()
    {
        ["projeto"] = projeto,
        ["rtc"] = rtc,
        ["ambiente"] = ambiente,
        ["status_resultado"] = status,
        ["titulo"] = titulo,
        ["data_execucao"] = "2026-10-21T14:30:00-03:00",
        ["bateria_testes"] = 1,
        ["tipo_teste"] = "API",
        ["executado_por"] = "Equipe",
    };
}

/// <summary>
/// RN94 numa listagem criada pela administração: duas referências da mesma entidade para outra (uma entrada por campo), e campos e
/// entidades inativos fora da resposta.
/// </summary>
public class RelacionadosAdministracaoApiTests(BancoDeTestesFixture banco) : TesteDeMetadados(banco)
{
    private static JsonObject Referencia(string chave, string entidade) => new()
    {
        ["chave"] = chave, ["nome"] = chave, ["tipoDado"] = "REFERENCIA", ["entidadeReferenciada"] = entidade, ["acaoExclusao"] = "RESTRINGIR",
    };

    private async Task<List<string>> Chaves(string categoria) =>
        (await LerJson(await Cliente.GetAsync($"{UrlUso("categoria")}/{categoria}/relacionados"))).AsArray()
        .Select(r => $"{r!["entidade"]!["chave"]!.GetValue<string>()}.{r["campo"]!["chave"]!.GetValue<string>()}={r["quantidade"]!.GetValue<int>()}")
        .ToList();

    [Fact]
    public async Task DoisCamposParaAMesmaEntidade_UmaEntradaPorCampo()
    {
        await CriarInventarioAsync();
        await CriarCampoAdmin("equipamento", Referencia("categoria_secundaria", "categoria"));
        EquipeDosAtalhos = EquipePadrao;
        var notebook = await CriarCategoria("Notebook");
        var monitor = await CriarCategoria("Monitor");
        await CriarNoGrupo("equipamento", new JsonObject
        {
            ["equipeId"] = EquipePadrao.Id.ToString(), ["nome"] = "N1", ["categoria"] = notebook, ["categoria_secundaria"] = monitor,
        });
        await CriarNoGrupo("equipamento", new JsonObject
        {
            ["equipeId"] = EquipePadrao.Id.ToString(), ["nome"] = "N2", ["categoria"] = notebook, ["categoria_secundaria"] = notebook,
        });

        Assert.Equal(["equipamento.categoria=2", "equipamento.categoria_secundaria=1"], await Chaves(notebook));
        Assert.Equal(["equipamento.categoria=0", "equipamento.categoria_secundaria=1"], await Chaves(monitor));
    }

    [Fact]
    public async Task CampoEEntidadeInativos_NaoAparecem()
    {
        await CriarInventarioAsync();
        await CriarCampoAdmin("equipamento", Referencia("categoria_secundaria", "categoria"));
        await CriarEntidadeAdmin("acessorio");
        await CriarCampoAdmin("acessorio", Referencia("categoria", "categoria"));
        var notebook = await CriarCategoria("Notebook");
        Assert.Equal(["equipamento.categoria=0", "equipamento.categoria_secundaria=0", "acessorio.categoria=0"], await Chaves(notebook));

        await Escrever(HttpMethod.Patch, $"{UrlCampoAdmin("equipamento", "categoria_secundaria")}/ativacao",
            new JsonObject { ["versao"] = VersaoEntidade("equipamento"), ["ativo"] = false });
        await Escrever(HttpMethod.Patch, $"{UrlEntidadeAdmin("acessorio")}/ativacao",
            new JsonObject { ["versao"] = VersaoEntidade("acessorio"), ["ativo"] = false });

        Assert.Equal(["equipamento.categoria=0"], await Chaves(notebook));
    }
}
