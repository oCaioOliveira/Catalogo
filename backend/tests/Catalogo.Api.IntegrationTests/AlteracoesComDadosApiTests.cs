using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Catalogo.Api.IntegrationTests.Apoio;
using Catalogo.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Catalogo.Api.IntegrationTests;

// Alterações de metadados em estruturas COM DADOS (RN58 a RN76): para cada linha da tabela, um caso que bloqueia (409 com o
// relatório de impacto) e um que permite. Grupo "Inventário" criado pela API em cada teste.
public class AlteracoesComDadosApiTests(BancoDeTestesFixture banco) : TesteDeMetadados(banco)
{
    private string _notebook = null!;

    [Fact]
    public async Task AdicionarCampoOpcional_ComRegistros_Permitido()
    {
        // RN59
        await PrepararAsync(equipamentos: 2);

        await CriarCampoAdmin("equipamento", Campo("cor", "TEXTO", tamanho: 20));

        Assert.True(CampoNoEstado("equipamento", "cor")["ativo"]!.GetValue<bool>());
    }

    [Fact]
    public async Task AdicionarCampoObrigatorio_SemValorPadrao409_ComValorPadraoPreencheOsRegistrosEApareceNosFiltros()
    {
        // RN60
        var ids = await PrepararAsync(equipamentos: 3);
        JsonObject Patrimonio(JsonNode? valorPadrao) => new()
        {
            ["versao"] = VersaoEntidade("equipamento"), ["chave"] = "patrimonio", ["nome"] = "Patrimônio", ["tipoDado"] = "TEXTO",
            ["tamanhoMaximo"] = 20, ["obrigatorio"] = true, ["valorPadrao"] = valorPadrao,
        };

        var bloqueado = await Tentar(HttpMethod.Post, $"{UrlEntidadeAdmin("equipamento")}/campos", Patrimonio(null));
        var impacto = Impacto(await Bloqueio(bloqueado, "ALTERACAO_INCOMPATIVEL_COM_DADOS"), "CAMPO_OBRIGATORIO_SEM_VALOR");
        Assert.Equal(3, impacto["quantidade"]!.GetValue<int>());
        Assert.Equal(ids.Order(), impacto["exemplos"]!.AsArray().Select(e => e!["id"]!.GetValue<string>()).Order());
        Assert.All(impacto["exemplos"]!.AsArray(), e => Assert.StartsWith("Equipamento ", e!["rotulo"]!.GetValue<string>()));

        var criado = await Escrever(HttpMethod.Post, $"{UrlEntidadeAdmin("equipamento")}/campos", Patrimonio("  SEM-PATRIMONIO "), HttpStatusCode.Created);

        Assert.Equal(3, Impacto(criado["impactos"]!.AsArray(), "VALOR_PADRAO_APLICADO")["quantidade"]!.GetValue<int>());
        Assert.Equal("SEM-PATRIMONIO", (await LerRegistro("equipamento", ids[0]))["dados"]!["patrimonio"]!.GetValue<string>());   // normalizado (RN04)
        var filtrados = await LerJson(await Cliente.GetAsync($"{UrlUso("equipamento")}?patrimonio=SEM-PATRIMONIO"));
        Assert.Equal(3, filtrados["total"]!.GetValue<int>());   // RegistroValor junto com Dados (ADR 0013)
        var semPatrimonio = await Cliente.PostAsJsonAsync(UrlUso("equipamento"), NovoEquipamento("Sem patrimônio"));
        Assert.Equal(HttpStatusCode.BadRequest, semPatrimonio.StatusCode);   // para os registros novos, o campo é obrigatório
    }

    [Fact]
    public async Task TornarObrigatorio_ComVazios409_ComValorPadraoPreencheSoOsVazios()
    {
        // RN61
        await PrepararAsync(equipamentos: 0);
        var comData = await CriarEquipamento("Com data", _notebook, data: "2026-01-15");
        var semData = await CriarEquipamento("Sem data", _notebook);
        JsonObject Obrigatorio(JsonNode? valorPadrao) => new()
        {
            ["versao"] = VersaoEntidade("equipamento"), ["nome"] = "Data de aquisição", ["obrigatorio"] = true, ["valorPadrao"] = valorPadrao,
        };

        var bloqueado = await Tentar(HttpMethod.Put, UrlCampoAdmin("equipamento", "data_aquisicao"), Obrigatorio(null));
        var impacto = Impacto(await Bloqueio(bloqueado, "ALTERACAO_INCOMPATIVEL_COM_DADOS"), "CAMPO_OBRIGATORIO_SEM_VALOR");
        Assert.Equal(semData, Assert.Single(impacto["exemplos"]!.AsArray())!["id"]!.GetValue<string>());

        await Escrever(HttpMethod.Put, UrlCampoAdmin("equipamento", "data_aquisicao"), Obrigatorio("2026-01-01"));

        Assert.Equal("2026-01-15", (await LerRegistro("equipamento", comData))["dados"]!["data_aquisicao"]!.GetValue<string>());
        Assert.Equal("2026-01-01", (await LerRegistro("equipamento", semData))["dados"]!["data_aquisicao"]!.GetValue<string>());
        Assert.True(CampoNoEstado("equipamento", "data_aquisicao")["obrigatorio"]!.GetValue<bool>());
    }

    [Fact]
    public async Task TornarOpcional_Permitido()
    {
        // RN62
        await PrepararAsync(equipamentos: 1);

        await Escrever(HttpMethod.Put, UrlCampoAdmin("equipamento", "nome"), new JsonObject
        {
            ["versao"] = VersaoEntidade("equipamento"), ["nome"] = "Nome", ["obrigatorio"] = false, ["tamanhoMaximo"] = 100,
        });

        Assert.False(CampoNoEstado("equipamento", "nome")["obrigatorio"]!.GetValue<bool>());
    }

    [Fact]
    public async Task DiminuirTamanho_ValorMaior409_QueCabePermitido()
    {
        // RN63
        await PrepararAsync(equipamentos: 0);
        var longo = await CriarEquipamento(new string('x', 40), _notebook);
        JsonObject Tamanho(int tamanho) => new()
        {
            ["versao"] = VersaoEntidade("equipamento"), ["nome"] = "Nome", ["obrigatorio"] = true, ["tamanhoMaximo"] = tamanho,
        };

        var impacto = Impacto(await Bloqueio(await Tentar(HttpMethod.Put, UrlCampoAdmin("equipamento", "nome"), Tamanho(39)),
            "ALTERACAO_INCOMPATIVEL_COM_DADOS"), "TEXTO_ACIMA_DO_TAMANHO");
        Assert.Equal((1, longo), (impacto["quantidade"]!.GetValue<int>(), impacto["exemplos"]![0]!["id"]!.GetValue<string>()));

        await Escrever(HttpMethod.Put, UrlCampoAdmin("equipamento", "nome"), Tamanho(40));
        Assert.Equal(40, CampoNoEstado("equipamento", "nome")["tamanhoMaximo"]!.GetValue<int>());
    }

    [Fact]
    public async Task AumentarTamanhoPrecisaoEEscala_Permitido()
    {
        // RN64
        await PrepararAsync(equipamentos: 0);
        await CriarEquipamento("Caro", _notebook, valor: 9_999_999_999.99m);   // 10 dígitos inteiros: o máximo de DECIMAL(12,2)

        await Escrever(HttpMethod.Put, UrlCampoAdmin("equipamento", "nome"), new JsonObject
        {
            ["versao"] = VersaoEntidade("equipamento"), ["nome"] = "Nome", ["obrigatorio"] = true, ["tamanhoMaximo"] = 200,
        });
        await Escrever(HttpMethod.Put, UrlCampoAdmin("equipamento", "valor"), new JsonObject
        {
            ["versao"] = VersaoEntidade("equipamento"), ["nome"] = "Valor", ["precisao"] = 15, ["escala"] = 4,
        });

        Assert.Equal((15, 4), (CampoNoEstado("equipamento", "valor")["precisao"]!.GetValue<int>(), CampoNoEstado("equipamento", "valor")["escala"]!.GetValue<int>()));
    }

    [Fact]
    public async Task DiminuirEscalaOuParteInteira_ValorQueNaoCabe409_QueCabePermitido()
    {
        // RN65
        await PrepararAsync(equipamentos: 0);
        var quebrado = await CriarEquipamento("Quebrado", _notebook, valor: 10.25m);
        await CriarEquipamento("Grande", _notebook, valor: 123_456.00m);
        JsonObject Decimal(int precisao, int escala) => new()
        {
            ["versao"] = VersaoEntidade("equipamento"), ["nome"] = "Valor", ["precisao"] = precisao, ["escala"] = escala,
        };

        var escala = Impacto(await Bloqueio(await Tentar(HttpMethod.Put, UrlCampoAdmin("equipamento", "valor"), Decimal(12, 1)),
            "ALTERACAO_INCOMPATIVEL_COM_DADOS"), "NUMERO_FORA_DA_PRECISAO");
        Assert.Equal(quebrado, Assert.Single(escala["exemplos"]!.AsArray())!["id"]!.GetValue<string>());

        // Aumentar SÓ a escala reduz a parte inteira: 123456 não cabe em DECIMAL(8,4) (4 dígitos inteiros).
        var parteInteira = await Bloqueio(await Tentar(HttpMethod.Put, UrlCampoAdmin("equipamento", "valor"), Decimal(8, 4)),
            "ALTERACAO_INCOMPATIVEL_COM_DADOS");
        Assert.Equal(1, Impacto(parteInteira, "NUMERO_FORA_DA_PRECISAO")["quantidade"]!.GetValue<int>());

        await Escrever(HttpMethod.Put, UrlCampoAdmin("equipamento", "valor"), Decimal(10, 2));   // 8 dígitos inteiros: cabe
        Assert.Equal(10, CampoNoEstado("equipamento", "valor")["precisao"]!.GetValue<int>());
    }

    [Fact]
    public async Task MudarOTipo_400_TextoParaTextoLongoPermitido()
    {
        // RN66
        await PrepararAsync(equipamentos: 1);
        await CriarCampoAdmin("equipamento", Campo("observacao", "TEXTO", tamanho: 200));

        var paraInteiro = await Tentar(HttpMethod.Put, UrlCampoAdmin("equipamento", "data_aquisicao"),
            new JsonObject { ["versao"] = VersaoEntidade("equipamento"), ["nome"] = "Data", ["tipoDado"] = "INTEIRO" });
        Assert.Equal(HttpStatusCode.BadRequest, paraInteiro.StatusCode);
        var problema = await LerJson(paraInteiro);
        Assert.Equal("VALIDACAO", problema["codigo"]!.GetValue<string>());
        Assert.Contains("crie um campo novo", problema["errors"]!["tipoDado"]![0]!.GetValue<string>());

        await Escrever(HttpMethod.Put, UrlCampoAdmin("equipamento", "observacao"),
            new JsonObject { ["versao"] = VersaoEntidade("equipamento"), ["nome"] = "Observação", ["tipoDado"] = "TEXTO_LONGO" });
        Assert.Equal("TEXTO_LONGO", CampoNoEstado("equipamento", "observacao")["tipoDado"]!.GetValue<string>());
    }

    [Fact]
    public async Task MudarOTipo_TextoLongoParaUrl_409ComValorQueNaoEUrl_PermitidoDepoisDeCorrigir_EVoltaParaTextoLongo()
    {
        // RN66 (ADR 0042): TEXTO_LONGO → URL só se todo valor for uma URL válida; URL → TEXTO_LONGO, sempre.
        await PrepararAsync(equipamentos: 1);
        await CriarCampoAdmin("equipamento", Campo("manual", "TEXTO_LONGO"));
        var comUrl = NovoEquipamento("Com URL");
        comUrl["manual"] = "https://manuais.exemplo.com/notebook";
        await CriarNoGrupo("equipamento", comUrl);
        var semUrl = NovoEquipamento("Sem URL");
        semUrl["manual"] = "intranet/manuais/notebook";
        var ruim = await CriarNoGrupo("equipamento", semUrl);
        JsonObject ParaTipo(string tipo) => new() { ["versao"] = VersaoEntidade("equipamento"), ["nome"] = "Manual", ["tipoDado"] = tipo };

        var bloqueado = await Bloqueio(await Tentar(HttpMethod.Put, UrlCampoAdmin("equipamento", "manual"), ParaTipo("URL")),
            "ALTERACAO_INCOMPATIVEL_COM_DADOS");
        var impacto = Impacto(bloqueado, "VALOR_NAO_E_URL");
        Assert.Equal(1, impacto["quantidade"]!.GetValue<int>());
        Assert.Equal(ruim, Assert.Single(impacto["exemplos"]!.AsArray())!["id"]!.GetValue<string>());
        Assert.Equal("TEXTO_LONGO", (await LerJson(await Cliente.GetAsync(UrlGrupoAdmin)))["entidades"]!.AsArray()
            .Single(e => e!["chave"]!.GetValue<string>() == "equipamento")!["campos"]!.AsArray()
            .Single(c => c!["chave"]!.GetValue<string>() == "manual")!["tipoDado"]!.GetValue<string>());   // nada mudou

        Assert.Equal(HttpStatusCode.NoContent, (await Cliente.DeleteAsync($"{UrlUso("equipamento")}/{ruim}")).StatusCode);
        await Escrever(HttpMethod.Put, UrlCampoAdmin("equipamento", "manual"), ParaTipo("URL"));
        Assert.Equal("URL", CampoNoEstado("equipamento", "manual")["tipoDado"]!.GetValue<string>());
        var invalido = await Cliente.PostAsJsonAsync(UrlUso("equipamento"), semUrl);
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);   // agora a gravação valida a URL

        await Escrever(HttpMethod.Put, UrlCampoAdmin("equipamento", "manual"), ParaTipo("TEXTO_LONGO"));
        Assert.Equal("TEXTO_LONGO", CampoNoEstado("equipamento", "manual")["tipoDado"]!.GetValue<string>());
    }

    [Fact]
    public async Task Lista_AdicionarOpcaoPermitido_RemoverOpcaoEmUso409_RemoverNaoUsadaPermitido()
    {
        // RN67 e RN68
        await PrepararAsync(equipamentos: 0);
        await CriarCampoAdmin("equipamento", new JsonObject
        {
            ["chave"] = "situacao", ["nome"] = "Situação", ["tipoDado"] = "LISTA", ["opcoes"] = new JsonArray("EM_USO", "BAIXADO"),
        });
        var baixado = await CriarNoGrupo("equipamento", NovoEquipamento("Baixado", situacao: "BAIXADO"));
        JsonObject Opcoes(params string[] opcoes) => new()
        {
            ["versao"] = VersaoEntidade("equipamento"), ["nome"] = "Situação", ["opcoes"] = new JsonArray(opcoes.Select(o => (JsonNode?)o).ToArray()),
        };

        await Escrever(HttpMethod.Put, UrlCampoAdmin("equipamento", "situacao"), Opcoes("EM_USO", "BAIXADO", "EMPRESTADO"));

        var impacto = Impacto(await Bloqueio(await Tentar(HttpMethod.Put, UrlCampoAdmin("equipamento", "situacao"), Opcoes("EM_USO", "EMPRESTADO")),
            "ALTERACAO_INCOMPATIVEL_COM_DADOS"), "OPCAO_EM_USO");
        Assert.Equal(baixado, Assert.Single(impacto["exemplos"]!.AsArray())!["id"]!.GetValue<string>());

        await Escrever(HttpMethod.Put, UrlCampoAdmin("equipamento", "situacao"), Opcoes("EM_USO", "BAIXADO"));   // EMPRESTADO não era usada
        Assert.Equal(["EM_USO", "BAIXADO"], CampoNoEstado("equipamento", "situacao")["opcoes"]!.AsArray().Select(o => o!.GetValue<string>()));
    }

    [Fact]
    public async Task DesativarCampo_EmUso409_LivrePreservaOValorNoPutEReativarDevolveTudo()
    {
        // RN69
        await PrepararAsync(equipamentos: 0);
        var id = await CriarEquipamento("Notebook 1", _notebook, valor: 4200m);
        JsonObject Ativacao(bool ativo) => new() { ["versao"] = VersaoEntidade("equipamento"), ["ativo"] = ativo };

        // "nome" é campo de exibição e está na restrição única.
        var emUso = Impacto(await Bloqueio(await Tentar(HttpMethod.Patch, $"{UrlCampoAdmin("equipamento", "nome")}/ativacao", Ativacao(false)),
            "METADADO_EM_USO"), "CAMPO_EM_USO");
        Assert.Equal(2, emUso["usos"]!.AsArray().Count);

        await Escrever(HttpMethod.Patch, $"{UrlCampoAdmin("equipamento", "valor")}/ativacao", Ativacao(false));
        var semValor = await LerRegistro("equipamento", id);
        Assert.False(semValor["dados"]!.AsObject().ContainsKey("valor"));   // some da resposta
        var editado = await Cliente.PutAsJsonAsync($"{UrlUso("equipamento")}/{id}", new JsonObject
        {
            ["versao"] = semValor["versao"]!.GetValue<string>(), ["nome"] = "Notebook 1 (revisado)", ["categoria"] = _notebook,
        });
        Assert.Equal(HttpStatusCode.OK, editado.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Cliente.GetAsync($"{UrlUso("equipamento")}?valor.de=1")).StatusCode);   // nem nos filtros

        await Escrever(HttpMethod.Patch, $"{UrlCampoAdmin("equipamento", "valor")}/ativacao", Ativacao(true));
        var reativado = await LerRegistro("equipamento", id);
        Assert.Equal(("Notebook 1 (revisado)", 4200.00m),
            (reativado["dados"]!["nome"]!.GetValue<string>(), reativado["dados"]!["valor"]!.GetValue<decimal>()));
        Assert.Equal(1, (await LerJson(await Cliente.GetAsync($"{UrlUso("equipamento")}?valor.de=4000")))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task ExcluirCampo_ComValor409_SemValorExclui()
    {
        // RN70
        await PrepararAsync(equipamentos: 0);
        await CriarEquipamento("Com valor", _notebook, valor: 10m);
        string Url(string campo) => $"{UrlCampoAdmin("equipamento", campo)}?versao={Uri.EscapeDataString(VersaoEntidade("equipamento"))}";

        var comValor = Impacto(await Bloqueio(await Tentar(HttpMethod.Delete, Url("valor"), null), "ALTERACAO_INCOMPATIVEL_COM_DADOS"), "CAMPO_COM_VALORES");
        Assert.Contains("Desative-o", comValor["descricao"]!.GetValue<string>());

        await Escrever(HttpMethod.Delete, Url("data_aquisicao"), null);
        Assert.DoesNotContain(EntidadeNoEstado("equipamento")["campos"]!.AsArray(), c => c!["chave"]!.GetValue<string>() == "data_aquisicao");
    }

    [Fact]
    public async Task AdicionarRestricao_ComDuplicatas409ListandoOsGrupos_SemDuplicatasGeraAsChavesEBarraNovas()
    {
        // RN71
        await PrepararAsync(equipamentos: 0);
        var a = await CriarEquipamento("A", _notebook, valor: 100m);
        var b = await CriarEquipamento("B", _notebook, valor: 100m);
        await CriarEquipamento("C", _notebook, valor: 200m);
        await CriarEquipamento("Sem valor", _notebook);   // RN10: vazio não conflita
        JsonObject Restricao() => new() { ["versao"] = VersaoEntidade("equipamento"), ["nome"] = "uq_valor", ["campos"] = new JsonArray("valor") };

        var impacto = Impacto(await Bloqueio(await Tentar(HttpMethod.Post, $"{UrlEntidadeAdmin("equipamento")}/restricoes", Restricao()),
            "ALTERACAO_INCOMPATIVEL_COM_DADOS"), "DUPLICATAS_NA_RESTRICAO");
        Assert.Equal(2, impacto["quantidade"]!.GetValue<int>());
        var grupo = Assert.Single(impacto["grupos"]!.AsArray())!.AsArray();
        Assert.Equal(new[] { a, b }.Order(), grupo.Select(r => r!["id"]!.GetValue<string>()).Order());
        Assert.Equal(["A", "B"], grupo.Select(r => r!["rotulo"]!.GetValue<string>()).Order());

        // Corrige a duplicata e cria a restrição: as chaves dos registros existentes são geradas.
        var lido = await LerRegistro("equipamento", b);
        await Cliente.PutAsJsonAsync($"{UrlUso("equipamento")}/{b}", new JsonObject
        {
            ["versao"] = lido["versao"]!.GetValue<string>(), ["nome"] = "B", ["categoria"] = _notebook, ["valor"] = 150m,
        });
        var criada = await Escrever(HttpMethod.Post, $"{UrlEntidadeAdmin("equipamento")}/restricoes", Restricao(), HttpStatusCode.Created);
        Assert.Equal(4, Impacto(criada["impactos"]!.AsArray(), "CHAVES_UNICAS_GERADAS")["quantidade"]!.GetValue<int>());
        Assert.Equal(3, await ContarChavesAsync("uq_valor"));   // o sem valor não tem chave

        var novaDuplicata = await Cliente.PostAsJsonAsync(UrlUso("equipamento"), NovoEquipamento("D", valor: 200m));
        Assert.Equal(HttpStatusCode.Conflict, novaDuplicata.StatusCode);
        Assert.Equal("uq_valor", (await LerJson(novaDuplicata))["detalhes"]!["restricao"]!.GetValue<string>());
    }

    [Fact]
    public async Task RemoverRestricao_RemoveAsChavesEAceitaDuplicatas()
    {
        // RN72
        await PrepararAsync(equipamentos: 1);

        await Escrever(HttpMethod.Delete,
            $"{UrlEntidadeAdmin("equipamento")}/restricoes/uq_equipamento_nome?versao={Uri.EscapeDataString(VersaoEntidade("equipamento"))}", null);

        Assert.Equal(0, await ContarChavesAsync("uq_equipamento_nome"));
        Assert.Equal(HttpStatusCode.Created, (await Cliente.PostAsJsonAsync(UrlUso("equipamento"), NovoEquipamento("Equipamento 1"))).StatusCode);
    }

    [Fact]
    public async Task ReordenarCampos_RestricaoComposta_RecalculaAsChaves()
    {
        // RN09 e RN53: a ordem dos campos é a ordem do hash das restrições compostas.
        await PrepararAsync(equipamentos: 0);
        await CriarEquipamento("A", _notebook, valor: 100m);
        await CriarRestricaoAdmin("equipamento", "uq_categoria_valor", "categoria", "valor");
        var ordem = new JsonArray("valor", "nome", "data_aquisicao", "categoria");

        await Escrever(HttpMethod.Put, $"{UrlEntidadeAdmin("equipamento")}/campos/ordem",
            new JsonObject { ["versao"] = VersaoEntidade("equipamento"), ["campos"] = ordem });

        Assert.Equal(["valor", "nome", "data_aquisicao", "categoria"],
            EntidadeNoEstado("equipamento")["campos"]!.AsArray().OrderBy(c => c!["ordem"]!.GetValue<int>()).Select(c => c!["chave"]!.GetValue<string>()));
        // Com as chaves recalculadas, a duplicata continua barrada (sem recalcular, o hash na ordem nova seria outro).
        var duplicata = await Cliente.PostAsJsonAsync(UrlUso("equipamento"), NovoEquipamento("B", valor: 100m));
        Assert.Equal(HttpStatusCode.Conflict, duplicata.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Cliente.PostAsJsonAsync(UrlUso("equipamento"), NovoEquipamento("C", valor: 101m))).StatusCode);
    }

    [Fact]
    public async Task Dependencia_RegistroQueViola409_ConsistentePermitida()
    {
        // RN73, com a semântica da RN08: o modelo escolhido precisa ser do fabricante do equipamento.
        await PrepararAsync(equipamentos: 0);
        await CriarEntidadeAdmin("fabricante", escopoEquipe: false);
        await CriarCampoAdmin("fabricante", Campo("nome", "TEXTO", obrigatorio: true, tamanho: 50));
        await DefinirCampoExibicao("fabricante", "nome");
        await CriarEntidadeAdmin("modelo", escopoEquipe: false);
        await CriarCampoAdmin("modelo", Campo("nome", "TEXTO", obrigatorio: true, tamanho: 50));
        await CriarCampoAdmin("modelo", Referencia("fabricante", "fabricante"));
        await DefinirCampoExibicao("modelo", "nome");
        await CriarCampoAdmin("equipamento", Referencia("fabricante", "fabricante"));
        await CriarCampoAdmin("equipamento", Referencia("modelo", "modelo"));

        var dell = await CriarNoGrupo("fabricante", new JsonObject { ["nome"] = "Dell" });
        var hp = await CriarNoGrupo("fabricante", new JsonObject { ["nome"] = "HP" });
        var latitude = await CriarNoGrupo("modelo", new JsonObject { ["nome"] = "Latitude", ["fabricante"] = dell });
        await CriarNoGrupo("equipamento", NovoEquipamento("Certo", fabricante: dell, modelo: latitude));
        var errado = await CriarNoGrupo("equipamento", NovoEquipamento("Errado", fabricante: hp, modelo: latitude));
        JsonObject Dependencia() => new()
        {
            ["versao"] = VersaoEntidade("equipamento"), ["campoOrigem"] = "fabricante", ["campoAlvo"] = "fabricante",
        };

        var impacto = Impacto(await Bloqueio(await Tentar(HttpMethod.Put, $"{UrlCampoAdmin("equipamento", "modelo")}/dependencia", Dependencia()),
            "ALTERACAO_INCOMPATIVEL_COM_DADOS"), "DEPENDENCIA_VIOLADA");
        Assert.Equal(errado, Assert.Single(impacto["exemplos"]!.AsArray())!["id"]!.GetValue<string>());

        var lido = await LerRegistro("equipamento", errado);
        await Cliente.PutAsJsonAsync($"{UrlUso("equipamento")}/{errado}", new JsonObject
        {
            ["versao"] = lido["versao"]!.GetValue<string>(), ["nome"] = "Errado", ["categoria"] = _notebook, ["fabricante"] = hp,
        });
        await Escrever(HttpMethod.Put, $"{UrlCampoAdmin("equipamento", "modelo")}/dependencia", Dependencia());

        Assert.Equal("fabricante", CampoNoEstado("equipamento", "modelo")["dependencia"]!["campoOrigem"]!.GetValue<string>());
        var violando = await Cliente.PostAsJsonAsync(UrlUso("equipamento"), NovoEquipamento("Novo errado", fabricante: hp, modelo: latitude));
        Assert.Equal(HttpStatusCode.BadRequest, violando.StatusCode);   // a regra nova vale para os registros novos (RN08)
    }

    [Fact]
    public async Task MudarEscopo_ComRegistros409_SemRegistrosPermitido()
    {
        // RN74
        await PrepararAsync(equipamentos: 0);
        await CriarEntidadeAdmin("local");
        await CriarCampoAdmin("local", Campo("nome", "TEXTO", tamanho: 50));
        JsonObject Escopo(string entidade, bool escopoEquipe) => new()
        {
            ["versao"] = VersaoEntidade(entidade), ["nome"] = entidade, ["escopoEquipe"] = escopoEquipe,
        };

        // categoria tem registros (a "Notebook" do preparo).
        var impacto = Impacto(await Bloqueio(await Tentar(HttpMethod.Put, UrlEntidadeAdmin("categoria"), Escopo("categoria", true)),
            "ALTERACAO_INCOMPATIVEL_COM_DADOS"), "ENTIDADE_COM_REGISTROS");
        Assert.Equal(1, impacto["quantidade"]!.GetValue<int>());

        await Escrever(HttpMethod.Put, UrlEntidadeAdmin("local"), Escopo("local", false));
        Assert.False(EntidadeNoEstado("local")["escopoEquipe"]!.GetValue<bool>());
    }

    [Fact]
    public async Task ExcluirEntidade_ComRegistrosOuJaUsada409_VaziaExclui()
    {
        // RN76
        await PrepararAsync(equipamentos: 0);
        await CriarEntidadeAdmin("local");
        await CriarCampoAdmin("local", Campo("nome", "TEXTO", tamanho: 50));
        await CriarEntidadeAdmin("rascunho");
        var temporario = await CriarNoGrupo("local", new JsonObject { ["equipeId"] = EquipePadrao.Id.ToString(), ["nome"] = "Sala 1" });
        string Url(string entidade) => $"{UrlEntidadeAdmin(entidade)}?versao={Uri.EscapeDataString(VersaoEntidade(entidade))}";

        // Referenciada por equipamento.categoria.
        Assert.Contains("equipamento.categoria",
            Impacto(await Bloqueio(await Tentar(HttpMethod.Delete, Url("categoria"), null), "ALTERACAO_INCOMPATIVEL_COM_DADOS"), "ENTIDADE_REFERENCIADA")
                ["usos"]![0]!.GetValue<string>());

        // Com registro; e, depois de excluir o registro, o histórico dele ainda aponta para a entidade.
        Impacto(await Bloqueio(await Tentar(HttpMethod.Delete, Url("local"), null), "ALTERACAO_INCOMPATIVEL_COM_DADOS"), "ENTIDADE_COM_REGISTROS");
        Assert.Equal(HttpStatusCode.NoContent, (await Cliente.DeleteAsync($"{UrlUso("local")}/{temporario}")).StatusCode);
        Impacto(await Bloqueio(await Tentar(HttpMethod.Delete, Url("local"), null), "ALTERACAO_INCOMPATIVEL_COM_DADOS"), "ENTIDADE_COM_HISTORICO");

        await Escrever(HttpMethod.Delete, Url("rascunho"), null);
        Assert.DoesNotContain(Estado["entidades"]!.AsArray(), e => e!["chave"]!.GetValue<string>() == "rascunho");
    }

    /// <summary>O Inventário, uma categoria "Notebook" e <paramref name="equipamentos"/> equipamentos ("Equipamento 1"...).</summary>
    private async Task<List<string>> PrepararAsync(int equipamentos)
    {
        await CriarInventarioAsync();
        _notebook = await CriarCategoria("Notebook");
        var ids = new List<string>();
        for (var i = 1; i <= equipamentos; i++)
            ids.Add(await CriarEquipamento($"Equipamento {i}", _notebook));
        return ids;
    }

    private JsonObject NovoEquipamento(string nome, decimal? valor = null, string? situacao = null, string? fabricante = null, string? modelo = null)
    {
        var dados = new JsonObject { ["equipeId"] = EquipePadrao.Id.ToString(), ["nome"] = nome, ["categoria"] = _notebook };
        if (valor is not null)
            dados["valor"] = valor;
        if (situacao is not null)
            dados["situacao"] = situacao;
        if (fabricante is not null)
            dados["fabricante"] = fabricante;
        if (modelo is not null)
            dados["modelo"] = modelo;
        return dados;
    }

    private static JsonObject Referencia(string chave, string entidade) => new()
    {
        ["chave"] = chave, ["nome"] = chave, ["tipoDado"] = "REFERENCIA", ["entidadeReferenciada"] = entidade, ["acaoExclusao"] = "RESTRINGIR",
    };

    private async Task<int> ContarChavesAsync(string restricao)
    {
        using var escopo = Banco.Fabrica.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<CatalogoDbContext>();
        var ids = contexto.RestricoesUnicas.Where(r => r.Nome == restricao).Select(r => r.Id);
        return await contexto.RegistrosChavesUnicas.CountAsync(c => ids.Contains(c.RestricaoUnicaId));
    }
}
