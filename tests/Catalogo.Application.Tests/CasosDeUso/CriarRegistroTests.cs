using System.Text.Json.Nodes;
using Catalogo.Application.Abstracoes;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.Tests.CasosDeUso;

public class CriarRegistroTests
{
    private readonly AmbienteDeTeste _ambiente = new();
    private GrupoDeTeste Grupo => _ambiente.Grupo;
    private RegistroRepositoryFalso Repositorio => _ambiente.Repositorio;

    [Fact]
    public async Task Executar_DadosValidos_GravaRegistroComChaveUnicaEValoresNumUnicoSave()
    {
        var dto = await _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "  Comunidade Ágil " });

        var gravado = Assert.Single(Repositorio.Gravados);
        Assert.Equal(1, _ambiente.UnidadeDeTrabalho.VezesQueSalvou);
        Assert.Equal(Grupo.Comunidade, gravado.EntidadeId);
        Assert.Equal("{\"nome\":\"Comunidade Ágil\"}", gravado.Dados);
        var chave = Assert.Single(gravado.ChavesUnicas);
        Assert.Equal(Grupo.UqComunidadeNome.Id, chave.RestricaoUnicaId);
        Assert.Equal(32, chave.ValorHash.Length);
        // docs/adr/0013: o valor tipado é gravado junto com o JSON.
        var valor = Assert.Single(gravado.Valores);
        Assert.Equal("Comunidade Ágil", valor.ValorTexto);
        Assert.Equal(gravado.Id, dto.Id);
        Assert.Equal("Comunidade Ágil", dto.Dados["nome"]!.GetValue<string>());
    }

    [Fact]
    public async Task Executar_ComReferencia_GravaReferenciaEmTabelaPropriaEDevolveIdERotulo()
    {
        // RN07 e RN20
        var projeto = await _ambiente.CriarProjeto("Portal");

        var dto = await _ambiente.Criar("endpoint_servico", new JsonObject
        {
            ["projeto"] = projeto.ToString(),
            ["nome_servico"] = "consulta",
        });

        var endpoint = Repositorio.Gravados.Single(r => r.Id == dto.Id);
        Assert.Equal("{\"nome_servico\":\"consulta\"}", endpoint.Dados);
        var referencia = Assert.Single(endpoint.Referencias);
        Assert.Equal(Grupo.EndpointProjeto.Id, referencia.CampoId);
        Assert.Equal(projeto, referencia.RegistroDestinoId);
        Assert.Equal(Grupo.Projeto, referencia.EntidadeDestinoId);
        Assert.Equal(Grupo.Endpoint, referencia.EntidadeOrigemId);
        // O retorno é plano: a referência aparece junto com os campos simples, como { id, rotulo }.
        Assert.Equal(projeto.ToString(), dto.Dados["projeto"]!["id"]!.GetValue<string>());
        Assert.Equal("Portal", dto.Dados["projeto"]!["rotulo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Executar_GrupoOuEntidadeInexistente_LancaRecursoNaoEncontrado()
    {
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            _ambiente.CriarRegistro.ExecutarAsync("nao_existe", "comunidade", [], CancellationToken.None));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            _ambiente.CriarRegistro.ExecutarAsync("testes", "nao_existe", [], CancellationToken.None));
    }

    [Fact]
    public async Task Executar_DadosInvalidos_LancaValidacaoENaoGrava()
    {
        // RN05
        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            _ambiente.Criar("comunidade", new JsonObject { ["nome"] = new string('x', 101), ["apelido"] = "x", ["outro"] = 1 }));

        Assert.Equal(["apelido", "nome", "outro"], erro.Erros.Keys.Order());
        Assert.Empty(Repositorio.Gravados);
        Assert.Equal(0, _ambiente.UnidadeDeTrabalho.VezesQueSalvou);
    }

    [Fact]
    public async Task Executar_ChavesReservadasNoCorpo_SaoIgnoradas()
    {
        // RN14: o front pode reenviar id, versao, criadoEm e atualizadoEm sem erro.
        var dto = await _ambiente.Criar("comunidade", new JsonObject
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["versao"] = "AAAAAAAAB9I=",
            ["criadoEm"] = "2020-01-01T00:00:00Z",
            ["nome"] = "C",
        });

        Assert.Equal("{\"nome\":\"C\"}", Repositorio.Gravados.Single(r => r.Id == dto.Id).Dados);
    }

    [Fact]
    public async Task Executar_ReferenciaInexistente_LancaValidacaoNoCampo()
    {
        // RN06
        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => _ambiente.Criar("endpoint_servico", new JsonObject
        {
            ["projeto"] = Guid.NewGuid().ToString(),
            ["nome_servico"] = "consulta",
        }));

        Assert.Contains("não encontrado", Assert.Single(erro.Erros["projeto"]));
    }

    [Fact]
    public async Task Executar_ReferenciaParaRegistroDeOutraEntidade_LancaValidacaoNoCampo()
    {
        // RN06: aponta para uma comunidade num campo que espera projeto.
        var comunidade = await _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "c1" });

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => _ambiente.Criar("endpoint_servico", new JsonObject
        {
            ["projeto"] = comunidade.Id.ToString(),
            ["nome_servico"] = "consulta",
        }));

        Assert.Contains("'Projeto'", Assert.Single(erro.Erros["projeto"]));
    }

    [Fact]
    public async Task Executar_DependenciaViolada_LancaValidacaoNoCampoDependente()
    {
        // RN08: endpoint do projeto B num cenário cujo relatório é do projeto A.
        var projetoA = await _ambiente.CriarProjeto("A");
        var projetoB = await _ambiente.CriarProjeto("B");
        var relatorio = await _ambiente.CriarRelatorio(projetoA);
        var endpointDoB = await _ambiente.CriarEndpoint(projetoB);

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => _ambiente.CriarCenario(relatorio, endpointDoB, 1));

        Assert.Equal(["endpoint_servico"], erro.Erros.Keys);
        Assert.Contains("'Projeto'", erro.Erros["endpoint_servico"][0]);
        Assert.DoesNotContain(Repositorio.Gravados, r => r.EntidadeId == Grupo.Cenario);
    }

    [Fact]
    public async Task Executar_DependenciaRespeitada_Grava()
    {
        // RN08
        var projeto = await _ambiente.CriarProjeto();
        var relatorio = await _ambiente.CriarRelatorio(projeto);
        var endpoint = await _ambiente.CriarEndpoint(projeto);

        await _ambiente.CriarCenario(relatorio, endpoint, 1);

        Assert.Single(Repositorio.Gravados, r => r.EntidadeId == Grupo.Cenario);
    }

    [Fact]
    public async Task Executar_ChaveUnicaJaExistente_LancaConflitoComNomeDaRestricao()
    {
        // RN09: mesmo nome com acento e caixa diferentes.
        await _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "Comunidade Ágil" });

        var erro = await Assert.ThrowsAsync<ConflitoUnicidadeException>(() =>
            _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "COMUNIDADE AGIL" }));

        Assert.Equal("uq_comunidade_nome", erro.NomeRestricao);
        Assert.Equal(["nome"], erro.Campos);
        Assert.Single(Repositorio.Gravados);
    }

    [Fact]
    public async Task Executar_ChaveUnicaComposta_SoConflitaComTodosOsValoresIguais()
    {
        // RN09: (relatorio_teste, numero_cenario)
        var projeto = await _ambiente.CriarProjeto();
        var relatorio1 = await _ambiente.CriarRelatorio(projeto);
        var relatorio2 = await _ambiente.CriarRelatorio(projeto);
        var endpoint = await _ambiente.CriarEndpoint(projeto);

        await _ambiente.CriarCenario(relatorio1, endpoint, 1);
        await _ambiente.CriarCenario(relatorio1, endpoint, 2);
        await _ambiente.CriarCenario(relatorio2, endpoint, 1);

        var erro = await Assert.ThrowsAsync<ConflitoUnicidadeException>(() => _ambiente.CriarCenario(relatorio1, endpoint, 1));
        Assert.Equal(["relatorio_teste", "numero_cenario"], erro.Campos);
    }

    [Fact]
    public async Task Executar_BancoAcusaViolacaoDeUnicidade_ConverteEmConflitoComNomeDaRestricao()
    {
        // Passo 7: outra requisição gravou o mesmo valor entre a verificação e o SaveChanges.
        _ambiente.UnidadeDeTrabalho.ErroAoSalvar = new ViolacaoUnicidadeBancoException(Grupo.UqComunidadeNome.Id, new Exception("2627"));

        var erro = await Assert.ThrowsAsync<ConflitoUnicidadeException>(() =>
            _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "Nova" }));

        Assert.Equal("uq_comunidade_nome", erro.NomeRestricao);
        Assert.Contains("'Comunidade'", erro.Message);
    }

    [Fact]
    public async Task Executar_BancoAcusaViolacaoSemRestricaoIdentificada_AindaAssimLancaConflito()
    {
        _ambiente.UnidadeDeTrabalho.ErroAoSalvar = new ViolacaoUnicidadeBancoException(null, new Exception("2601"));

        await Assert.ThrowsAsync<ConflitoUnicidadeException>(() => _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "Nova" }));
    }
}
