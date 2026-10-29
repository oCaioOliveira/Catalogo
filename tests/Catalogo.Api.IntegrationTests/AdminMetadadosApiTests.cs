using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Domain.Acesso;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

// Administração de metadados pela API (RN50 a RN57): criar uma listagem do zero, quem administra, simulação, concorrência,
// histórico e desativação.
public class AdminMetadadosApiTests(BancoDeTestesFixture banco) : TesteDeMetadados(banco)
{
    [Fact]
    public async Task CriarInventarioInteiroPelaApi_ECadastrarRegistrosPelosEndpointsNormais()
    {
        // RN53 a RN55: grupo, entidades (uma global), campos com referência, campo de exibição e restrições.
        await CriarInventarioAsync();

        var notebook = await CriarCategoria("Notebook");
        var caro = await CriarEquipamento("Notebook do Caio", notebook, valor: 7500.5m, data: "2026-03-10");
        await CriarEquipamento("Notebook reserva", notebook, valor: 3200m);

        var registro = await LerRegistro("equipamento", caro);
        Assert.Equal("Notebook", registro["dados"]!["categoria"]!["rotulo"]!.GetValue<string>());   // campo de exibição da categoria
        Assert.Equal(7500.50m, registro["dados"]!["valor"]!.GetValue<decimal>());

        // Filtros da listagem nos campos criados pela API, e a restrição única funcionando.
        var filtrada = await LerJson(await Cliente.GetAsync($"{UrlUso("equipamento")}?valor.de=5000"));
        Assert.Equal([caro], filtrada["itens"]!.AsArray().Select(i => i!["id"]!.GetValue<string>()));
        var duplicado = await Cliente.PostAsJsonAsync(UrlUso("equipamento"), new JsonObject
        {
            ["equipeId"] = EquipePadrao.Id.ToString(), ["nome"] = "notebook do caio", ["categoria"] = notebook,
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
        Assert.Equal("CONFLITO_UNICIDADE", (await LerJson(duplicado))["codigo"]!.GetValue<string>());

        // A listagem aparece no menu e nos metadados de uso, com o escopo certo (a entidade global foi gravada como global).
        Assert.Contains((await LerJson(await Cliente.GetAsync("/api/v1/grupos"))).AsArray(), g => g!["chave"]!.GetValue<string>() == Grupo);
        var entidades = (await LerJson(await Cliente.GetAsync($"/api/v1/grupos/{Grupo}/entidades"))).AsArray();
        Assert.Equal([("categoria", false), ("equipamento", true)],
            entidades.Select(e => (e!["chave"]!.GetValue<string>(), e["escopoEquipe"]!.GetValue<bool>())));
        await Recarregar();
        Assert.Equal((1, 2), (EntidadeNoEstado("categoria")["quantidadeRegistros"]!.GetValue<int>(),
            EntidadeNoEstado("equipamento")["quantidadeRegistros"]!.GetValue<int>()));
    }

    [Fact]
    public async Task NaoAdministrador_Recebe403EmTodaARotaAdmin()
    {
        // RN50: os metadados valem para todas as equipes; só o ADMINISTRADOR os altera.
        await CriarInventarioAsync();
        var (_, gestor) = await CriarUsuarioAsync("gestor", Perfil.Gestor, [EquipePadrao]);
        var (_, interno) = await CriarUsuarioAsync("interno", Perfil.Interno, [EquipePadrao]);
        var versao = VersaoEntidade("equipamento");

        (HttpMethod Metodo, string Url, JsonObject? Corpo)[] requisicoes =
        [
            (HttpMethod.Get, $"{Admin}/grupos", null),
            (HttpMethod.Get, UrlGrupoAdmin, null),
            (HttpMethod.Get, $"{UrlGrupoAdmin}/historico", null),
            (HttpMethod.Post, $"{Admin}/grupos", new JsonObject { ["chave"] = "outro", ["nome"] = "Outro" }),
            (HttpMethod.Put, UrlGrupoAdmin, new JsonObject { ["versao"] = VersaoGrupo, ["nome"] = "X" }),
            (HttpMethod.Patch, $"{UrlGrupoAdmin}/ativacao", new JsonObject { ["versao"] = VersaoGrupo, ["ativo"] = false }),
            (HttpMethod.Post, $"{UrlGrupoAdmin}/entidades", new JsonObject { ["versao"] = VersaoGrupo, ["chave"] = "x", ["nome"] = "X" }),
            (HttpMethod.Put, UrlEntidadeAdmin("equipamento"), new JsonObject { ["versao"] = versao, ["nome"] = "X" }),
            (HttpMethod.Post, $"{UrlEntidadeAdmin("equipamento")}/campos", Campo("cor", "TEXTO", tamanho: 10)),
            (HttpMethod.Put, UrlCampoAdmin("equipamento", "nome"), new JsonObject { ["versao"] = versao, ["nome"] = "X", ["tamanhoMaximo"] = 10 }),
            (HttpMethod.Delete, $"{UrlCampoAdmin("equipamento", "valor")}?versao={Uri.EscapeDataString(versao)}", null),
            (HttpMethod.Post, $"{UrlEntidadeAdmin("equipamento")}/restricoes", new JsonObject { ["versao"] = versao, ["nome"] = "uq", ["campos"] = new JsonArray("valor") }),
        ];

        foreach (var cliente in new[] { gestor, interno })
        {
            foreach (var (metodo, url, corpo) in requisicoes)
            {
                var resposta = await Enviar(cliente, metodo, url, corpo?.DeepClone().AsObject());
                Assert.True(resposta.StatusCode == HttpStatusCode.Forbidden, $"{metodo} {url}: {(int)resposta.StatusCode}");
                Assert.Equal("SEM_PERMISSAO", (await LerJson(resposta))["codigo"]!.GetValue<string>());
            }
        }

        // Nada mudou, e a leitura dos metadados de uso continua como antes.
        await Recarregar();
        Assert.Equal(versao, VersaoEntidade("equipamento"));
        Assert.Equal(HttpStatusCode.OK, (await interno.GetAsync($"/api/v1/grupos/{Grupo}/entidades")).StatusCode);
    }

    [Fact]
    public async Task Simulacao_DevolveOMesmoRelatorioDoBloqueio_ENaoGravaNada()
    {
        // RN56
        await CriarInventarioAsync();
        var notebook = await CriarCategoria("Notebook");
        var longo = await CriarEquipamento(new string('N', 80), notebook);
        var versao = VersaoEntidade("equipamento");
        JsonObject Diminuir() => new() { ["versao"] = versao, ["nome"] = "nome", ["obrigatorio"] = true, ["tamanhoMaximo"] = 20 };

        var simulada = await LerJson(await Tentar(HttpMethod.Put, $"{UrlCampoAdmin("equipamento", "nome")}?simular=true", Diminuir()));
        var bloqueio = await Bloqueio(await Tentar(HttpMethod.Put, UrlCampoAdmin("equipamento", "nome"), Diminuir()), "ALTERACAO_INCOMPATIVEL_COM_DADOS");

        Assert.False(simulada["permitido"]!.GetValue<bool>());
        Assert.True(simulada["simulado"]!.GetValue<bool>());
        Assert.Null(simulada["grupo"]);
        Assert.Equal(bloqueio.ToJsonString(), simulada["impactos"]!.ToJsonString());
        var impacto = Impacto(bloqueio, "TEXTO_ACIMA_DO_TAMANHO");
        Assert.Equal(1, impacto["quantidade"]!.GetValue<int>());
        Assert.Equal((longo, new string('N', 80)), (impacto["exemplos"]![0]!["id"]!.GetValue<string>(), impacto["exemplos"]![0]!["rotulo"]!.GetValue<string>()));

        // Permitida: a simulação diz que passa, mostra o efeito, mas não grava (nem campo, nem valores, nem versão).
        var permitida = await LerJson(await Tentar(HttpMethod.Post, $"{UrlEntidadeAdmin("equipamento")}/campos?simular=true", new JsonObject
        {
            ["versao"] = versao, ["chave"] = "patrimonio", ["nome"] = "Patrimônio", ["tipoDado"] = "TEXTO", ["tamanhoMaximo"] = 20,
            ["obrigatorio"] = true, ["valorPadrao"] = "SEM-PATRIMONIO",
        }));
        Assert.True(permitida["permitido"]!.GetValue<bool>());
        Assert.Equal(1, Impacto(permitida["impactos"]!.AsArray(), "VALOR_PADRAO_APLICADO")["quantidade"]!.GetValue<int>());
        await Recarregar();
        Assert.Equal(versao, VersaoEntidade("equipamento"));
        Assert.DoesNotContain(EntidadeNoEstado("equipamento")["campos"]!.AsArray(), c => c!["chave"]!.GetValue<string>() == "patrimonio");
        Assert.False((await LerRegistro("equipamento", longo))["dados"]!.AsObject().ContainsKey("patrimonio"));
    }

    [Fact]
    public async Task NomePlural_CriarEditarELimpar_ApareceNaAdministracaoENaListaDeEntidades()
    {
        await CriarGrupoAdmin();
        await Escrever(HttpMethod.Post, $"{UrlGrupoAdmin}/entidades", new JsonObject
        {
            ["versao"] = VersaoGrupo, ["chave"] = "local", ["nome"] = "Local", ["nomePlural"] = "  Locais ", ["escopoEquipe"] = false,
        }, HttpStatusCode.Created);
        Assert.Equal("Locais", EntidadeNoEstado("local")["nomePlural"]!.GetValue<string>());

        async Task<JsonNode?> PluralNoUso() => (await LerJson(await Cliente.GetAsync($"/api/v1/grupos/{Grupo}/entidades"))).AsArray()
            .Single(e => e!["chave"]!.GetValue<string>() == "local")!["nomePlural"];

        Assert.Equal("Locais", (await PluralNoUso())!.GetValue<string>());

        await Escrever(HttpMethod.Put, UrlEntidadeAdmin("local"), new JsonObject
        {
            ["versao"] = VersaoEntidade("local"), ["nome"] = "Local", ["nomePlural"] = "Lugares",
        });
        Assert.Equal("Lugares", (await PluralNoUso())!.GetValue<string>());

        // No PUT, ausente = sem plural (as telas usam o nome).
        await Escrever(HttpMethod.Put, UrlEntidadeAdmin("local"), new JsonObject { ["versao"] = VersaoEntidade("local"), ["nome"] = "Local" });
        Assert.Null(EntidadeNoEstado("local")["nomePlural"]);
        Assert.Null(await PluralNoUso());
    }

    [Fact]
    public async Task Icone_CriarEditarELimpar_ApareceNaAdministracaoENaListaDeEntidades()
    {
        // RN95
        await CriarGrupoAdmin();
        await Escrever(HttpMethod.Post, $"{UrlGrupoAdmin}/entidades", new JsonObject
        {
            ["versao"] = VersaoGrupo, ["chave"] = "local", ["nome"] = "Local", ["icone"] = " pi-map-marker ", ["escopoEquipe"] = false,
        }, HttpStatusCode.Created);
        Assert.Equal("pi-map-marker", EntidadeNoEstado("local")["icone"]!.GetValue<string>());

        async Task<JsonNode?> IconeNoUso() => (await LerJson(await Cliente.GetAsync($"/api/v1/grupos/{Grupo}/entidades"))).AsArray()
            .Single(e => e!["chave"]!.GetValue<string>() == "local")!["icone"];

        Assert.Equal("pi-map-marker", (await IconeNoUso())!.GetValue<string>());

        await Escrever(HttpMethod.Put, UrlEntidadeAdmin("local"), new JsonObject
        {
            ["versao"] = VersaoEntidade("local"), ["nome"] = "Local", ["icone"] = "pi-building",
        });
        Assert.Equal("pi-building", (await IconeNoUso())!.GetValue<string>());

        // Fora do formato: 400 no campo "icone", nada gravado.
        var invalido = await Cliente.PutAsJsonAsync(UrlEntidadeAdmin("local"), new JsonObject
        {
            ["versao"] = VersaoEntidade("local"), ["nome"] = "Local", ["icone"] = "fa fa-home",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);
        Assert.NotNull((await LerJson(invalido))["errors"]!["icone"]);

        // No PUT, ausente = sem ícone (as telas usam um genérico).
        await Escrever(HttpMethod.Put, UrlEntidadeAdmin("local"), new JsonObject { ["versao"] = VersaoEntidade("local"), ["nome"] = "Local" });
        Assert.Null(EntidadeNoEstado("local")["icone"]);
        Assert.Null(await IconeNoUso());
    }

    [Fact]
    public async Task ListaDeGrupos_TrazQuantosTiposDeItemEItensCadaGrupoTem()
    {
        await CriarInventarioAsync();
        await CriarCategoria("Notebook");
        await CriarEquipamento("Notebook 1", await CriarCategoria("Monitor"));

        var grupos = (await LerJson(await Cliente.GetAsync($"{Admin}/grupos"))).AsArray();
        var inventario = grupos.Single(g => g!["chave"]!.GetValue<string>() == Grupo)!;

        Assert.Equal(2, inventario["quantidadeEntidades"]!.GetValue<int>());
        Assert.Equal(3, inventario["quantidadeRegistros"]!.GetValue<int>());
    }

    [Fact]
    public async Task PostComCampoDesconhecidoOuInativo_400ComOsCamposDesatualizadosNosDetalhes()
    {
        // RN01 e RN02: o front com metadados antigos recarrega e pede para revisar o formulário.
        await CriarInventarioAsync();
        await Escrever(HttpMethod.Patch, $"{UrlCampoAdmin("equipamento", "data_aquisicao")}/ativacao",
            new JsonObject { ["versao"] = VersaoEntidade("equipamento"), ["ativo"] = false });
        var categoria = await CriarCategoria("Notebook");

        var resposta = await Cliente.PostAsJsonAsync(UrlUso("equipamento"), new JsonObject
        {
            ["equipeId"] = EquipeDosAtalhos.Id.ToString(), ["nome"] = "X", ["categoria"] = categoria,
            ["data_aquisicao"] = "2026-01-01", ["cor"] = "azul",
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var problema = await LerJson(resposta);
        Assert.Equal("VALIDACAO", problema["codigo"]!.GetValue<string>());
        Assert.Equal(["cor", "data_aquisicao"],
            problema["detalhes"]!["camposDesatualizados"]!.AsArray().Select(c => c!.GetValue<string>()).Order());
    }

    [Fact]
    public async Task HistoricoDeUmaEntidade_SoAsLinhasDela_CadaUmaDizendoOQueFoiAlterado()
    {
        await CriarInventarioAsync();

        var daCategoria = await LerJson(await Cliente.GetAsync($"{UrlGrupoAdmin}/historico?entidade=categoria&tamanho=100"));
        var doGrupo = await LerJson(await Cliente.GetAsync($"{UrlGrupoAdmin}/historico?tamanho=100"));
        var alvos = daCategoria["itens"]!.AsArray().Select(i => i!["alvo"]!.GetValue<string>()).ToList();

        // categoria: criação, campo nome, campo de exibição (alteração da entidade) e a restrição.
        Assert.Equal(4, alvos.Count);
        Assert.All(alvos, a => Assert.Contains("'categoria'", a));
        Assert.Contains("Campo 'nome' de 'categoria'", alvos);
        Assert.Contains("Unicidade 'uq_categoria_nome' de 'categoria'", alvos);
        Assert.True(doGrupo["total"]!.GetValue<int>() > 4);
        Assert.Contains(doGrupo["itens"]!.AsArray(), i => i!["alvo"]!.GetValue<string>() == "Listagem");
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente.GetAsync($"{UrlGrupoAdmin}/historico?entidade=nao_existe")).StatusCode);
    }

    [Fact]
    public async Task EditarComVersaoAntiga_409ConflitoDeConcorrencia()
    {
        // RN52: a versão da entidade muda em toda alteração dela e dos seus campos.
        await CriarInventarioAsync();
        var antiga = VersaoEntidade("equipamento");
        await CriarCampoAdmin("equipamento", Campo("cor", "TEXTO", tamanho: 20));

        var resposta = await Tentar(HttpMethod.Put, UrlCampoAdmin("equipamento", "cor"),
            new JsonObject { ["versao"] = antiga, ["nome"] = "Cor", ["tamanhoMaximo"] = 30 });
        var semVersao = await Tentar(HttpMethod.Put, UrlEntidadeAdmin("equipamento"), new JsonObject { ["nome"] = "Equipamento" });

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        Assert.Equal("CONFLITO_CONCORRENCIA", (await LerJson(resposta))["codigo"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, semVersao.StatusCode);
        Assert.True((await LerJson(semVersao))["errors"]!.AsObject().ContainsKey("versao"));
    }

    [Fact]
    public async Task ChaveImutavel_PutComOutraChave400()
    {
        // RN51
        await CriarInventarioAsync();

        var campo = await Tentar(HttpMethod.Put, UrlCampoAdmin("equipamento", "valor"), new JsonObject
        {
            ["versao"] = VersaoEntidade("equipamento"), ["chave"] = "preco", ["nome"] = "Preço", ["precisao"] = 12, ["escala"] = 2,
        });
        var grupo = await Tentar(HttpMethod.Put, UrlGrupoAdmin, new JsonObject { ["versao"] = VersaoGrupo, ["chave"] = "estoque", ["nome"] = "Estoque" });

        Assert.Equal(HttpStatusCode.BadRequest, campo.StatusCode);
        Assert.Contains("não muda", (await LerJson(campo))["errors"]!["chave"]![0]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.BadRequest, grupo.StatusCode);
    }

    [Fact]
    public async Task TodaMudanca_GeraUmaEntradaMetadadoNoHistorico_ComAntesEDepois()
    {
        // RN52: uma linha por mudança, mesmo com registros reescritos (a quantidade vai nos dados).
        await CriarInventarioAsync();
        var notebook = await CriarCategoria("Notebook");
        await CriarEquipamento("A", notebook);
        await CriarEquipamento("B", notebook);
        await Escrever(HttpMethod.Put, UrlCampoAdmin("equipamento", "valor"), new JsonObject
        {
            ["versao"] = VersaoEntidade("equipamento"), ["nome"] = "Valor de compra", ["precisao"] = 14, ["escala"] = 2,
        });
        await CriarCampoAdmin("equipamento", new JsonObject
        {
            ["chave"] = "patrimonio", ["nome"] = "Patrimônio", ["tipoDado"] = "TEXTO", ["tamanhoMaximo"] = 20, ["obrigatorio"] = true,
            ["valorPadrao"] = "SEM-PATRIMONIO",
        });

        var historico = await LerJson(await Cliente.GetAsync($"{UrlGrupoAdmin}/historico?tamanho=100"));
        var itens = historico["itens"]!.AsArray();

        // Inventário: grupo + equipes habilitadas (RN90) + 2 entidades + 5 campos + 2 campos de exibição + 2 restrições = 13;
        // mais a alteração e o campo novo.
        Assert.Equal(15, historico["total"]!.GetValue<int>());
        var equipes = itens.Single(i => i!["alvo"]!.GetValue<string>() == "Equipes habilitadas")!;
        Assert.Contains(equipes["mudancas"]!.AsArray(), m => m!["campo"]!.GetValue<string>() == "equipes"
            && m["de"]!.GetValue<string>() == "" && m["para"]!.GetValue<string>() == "Equipe Padrão");
        Assert.All(itens, i => Assert.Equal("admin.testes", i!["usuario"]!["matricula"]!.GetValue<string>()));
        var alteracao = itens[1]!;
        Assert.Equal("ALTERACAO", alteracao["operacao"]!.GetValue<string>());
        Assert.Contains(alteracao["mudancas"]!.AsArray(), m => m!["campo"]!.GetValue<string>() == "nome"
            && m["de"]!.GetValue<string>() == "Valor" && m["para"]!.GetValue<string>() == "Valor de compra");
        var novoCampo = itens[0]!;
        Assert.Equal("CRIACAO", novoCampo["operacao"]!.GetValue<string>());
        Assert.Contains(novoCampo["mudancas"]!.AsArray(), m => m!["campo"]!.GetValue<string>() == "registrosAfetados" && m["para"]!.GetValue<int>() == 2);

        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        Assert.Equal(15, await contexto.HistoricosAlteracao.CountAsync(h => h.AlvoTipo == Domain.Historico.AlvoHistorico.Metadado));
    }

    [Fact]
    public async Task EntidadeDesativada_404ParaOInterno_SomenteLeituraParaOAdministrador()
    {
        // RN75
        await CriarInventarioAsync();
        var notebook = await CriarCategoria("Notebook");
        var equipamento = await CriarEquipamento("Notebook 1", notebook);
        var (_, interno) = await CriarUsuarioAsync("interno", Perfil.Interno, [EquipePadrao]);
        await Escrever(HttpMethod.Patch, $"{UrlEntidadeAdmin("categoria")}/ativacao",
            new JsonObject { ["versao"] = VersaoEntidade("categoria"), ["ativo"] = false });

        // INTERNO: a entidade não existe.
        var listaDoInterno = await interno.GetAsync(UrlUso("categoria"));
        Assert.Equal(HttpStatusCode.NotFound, listaDoInterno.StatusCode);
        Assert.Equal("ENTIDADE_NAO_ENCONTRADA", (await LerJson(listaDoInterno))["codigo"]!.GetValue<string>());
        Assert.DoesNotContain((await LerJson(await interno.GetAsync($"/api/v1/grupos/{Grupo}/entidades"))).AsArray(),
            e => e!["chave"]!.GetValue<string>() == "categoria");

        // ADMINISTRADOR: lê, mas não cria nem altera.
        Assert.Equal(HttpStatusCode.OK, (await Cliente.GetAsync(UrlUso("categoria"))).StatusCode);
        var criar = await Cliente.PostAsJsonAsync(UrlUso("categoria"), new JsonObject { ["nome"] = "Monitor" });
        Assert.Equal(HttpStatusCode.Conflict, criar.StatusCode);
        Assert.Equal("ENTIDADE_INATIVA", (await LerJson(criar))["codigo"]!.GetValue<string>());

        // Quem já apontava para ela continua funcionando; vínculos novos e opções, não.
        var atual = await LerRegistro("equipamento", equipamento);
        Assert.Equal("Notebook", atual["dados"]!["categoria"]!["rotulo"]!.GetValue<string>());
        var editado = await Cliente.PutAsJsonAsync($"{UrlUso("equipamento")}/{equipamento}", new JsonObject
        {
            ["versao"] = atual["versao"]!.GetValue<string>(), ["nome"] = "Notebook 1 (revisado)", ["categoria"] = notebook,
        });
        Assert.Equal(HttpStatusCode.OK, editado.StatusCode);
        var opcoes = await LerJson(await Cliente.GetAsync(
            $"/api/v1/grupos/{Grupo}/entidades/equipamento/campos/categoria/opcoes?equipeId={EquipePadrao.Id}"));
        Assert.Equal(0, opcoes["total"]!.GetValue<int>());
        var novoVinculo = await Cliente.PostAsJsonAsync(UrlUso("equipamento"), new JsonObject
        {
            ["equipeId"] = EquipePadrao.Id.ToString(), ["nome"] = "Notebook 2", ["categoria"] = notebook,
        });
        Assert.Equal(HttpStatusCode.BadRequest, novoVinculo.StatusCode);
        Assert.Contains("desativada", (await LerJson(novoVinculo))["errors"]!["categoria"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task GrupoDesativado_SomeParaONaoAdministrador()
    {
        // RN77
        await CriarInventarioAsync();
        var (_, interno) = await CriarUsuarioAsync("interno", Perfil.Interno, [EquipePadrao]);
        await Escrever(HttpMethod.Patch, $"{UrlGrupoAdmin}/ativacao", new JsonObject { ["versao"] = VersaoGrupo, ["ativo"] = false });

        Assert.DoesNotContain((await LerJson(await interno.GetAsync("/api/v1/grupos"))).AsArray(), g => g!["chave"]!.GetValue<string>() == Grupo);
        Assert.Equal(HttpStatusCode.NotFound, (await interno.GetAsync($"/api/v1/grupos/{Grupo}/entidades")).StatusCode);
        var doAdministrador = (await LerJson(await Cliente.GetAsync("/api/v1/grupos"))).AsArray().Single(g => g!["chave"]!.GetValue<string>() == Grupo)!;
        Assert.False(doAdministrador["ativo"]!.GetValue<bool>());
        var criar = await Cliente.PostAsJsonAsync(UrlUso("categoria"), new JsonObject { ["nome"] = "Monitor" });
        Assert.Equal("ENTIDADE_INATIVA", (await LerJson(criar))["codigo"]!.GetValue<string>());
    }

    [Fact]
    public async Task ExcluirGrupoComEntidades_409_ESemEntidades_Exclui()
    {
        // RN77
        await CriarInventarioAsync();

        var comEntidades = await Tentar(HttpMethod.Delete, $"{UrlGrupoAdmin}?versao={Uri.EscapeDataString(VersaoGrupo)}", null);
        Assert.Equal(2, Impacto(await Bloqueio(comEntidades, "METADADO_EM_USO"), "GRUPO_COM_ENTIDADES")["usos"]!.AsArray().Count);

        var vazio = "vazio_" + Guid.NewGuid().ToString("N")[..6];
        var criado = await LerJson(await Cliente.PostAsJsonAsync($"{Admin}/grupos", new JsonObject { ["chave"] = vazio, ["nome"] = "Vazio" }));
        var versao = criado["grupo"]!["versao"]!.GetValue<string>();
        var excluido = await Cliente.DeleteAsync($"{Admin}/grupos/{vazio}?versao={Uri.EscapeDataString(versao)}");
        Assert.Equal(HttpStatusCode.OK, excluido.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente.GetAsync($"{Admin}/grupos/{vazio}")).StatusCode);
    }
}
