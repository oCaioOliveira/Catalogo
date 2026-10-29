using Catalogo.Application.CasosDeUso;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.Tests.CasosDeUso;

public class ListarEntidadesTests
{
    private readonly GrupoDeTeste _grupo = new();
    private readonly ListarEntidades _listarEntidades;

    public ListarEntidadesTests()
    {
        _listarEntidades = new ListarEntidades(new UsuarioAtualFalso(), new MetadadosProviderFalso(_grupo.Grupo), new ConfiguracaoRepositoryFalso(),
            new ModeloAvaliacaoRepositoryFalso(new AvaliacoesEmMemoria()));
    }

    [Fact]
    public async Task Executar_GrupoExistente_DevolveEntidadesOrdenadasPorNome()
    {
        var entidades = await _listarEntidades.ExecutarAsync("testes", null, CancellationToken.None);

        Assert.Equal(
            ["Cenário de Teste", "Comunidade", "Endpoint de Serviço", "Projeto", "Relatório de Teste"],
            entidades.Select(e => e.Nome));
    }

    [Fact]
    public async Task Executar_CampoInativo_NaoApareceParaOFront()
    {
        // RN02
        var comunidade = (await _listarEntidades.ExecutarAsync("testes", null, CancellationToken.None))
            .Single(e => e.Chave == "comunidade");

        Assert.Equal(["nome", "descricao"], comunidade.Campos.Select(c => c.Chave));
        Assert.Equal("nome", comunidade.CampoExibicao);
    }

    [Fact]
    public async Task Executar_CampoReferenciaComDependencia_DevolveChavesNoFormatoDoBanco()
    {
        var cenario = (await _listarEntidades.ExecutarAsync("testes", null, CancellationToken.None))
            .Single(e => e.Chave == "cenario_teste");
        var endpoint = cenario.Campos.Single(c => c.Chave == "endpoint_servico");

        Assert.Equal("REFERENCIA", endpoint.TipoDado);
        Assert.Equal("endpoint_servico", endpoint.EntidadeReferenciada);
        Assert.Equal("RESTRINGIR", endpoint.AcaoExclusao);
        Assert.NotNull(endpoint.Dependencia);
        Assert.Equal("relatorio_teste", endpoint.Dependencia.CampoVia);
        Assert.Equal("projeto", endpoint.Dependencia.CampoOrigem);
        Assert.Equal("projeto", endpoint.Dependencia.CampoAlvo);
        // RN21: o front envia em "dependeDe" o valor do campo relatorio_teste do formulário.
        Assert.Equal("relatorio_teste", endpoint.DependeDe);
        Assert.Null(cenario.Campos.Single(c => c.Chave == "relatorio_teste").DependeDe);
        Assert.Equal(["relatorio_teste", "numero_cenario"], Assert.Single(cenario.RestricoesUnicas).Campos);
    }

    [Fact]
    public async Task Executar_GrupoInexistente_LancaRecursoNaoEncontrado()
    {
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            _listarEntidades.ExecutarAsync("nao_existe", null, CancellationToken.None));
    }
}
