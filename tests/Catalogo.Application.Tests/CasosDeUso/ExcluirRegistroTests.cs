using System.Text.Json.Nodes;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.Tests.CasosDeUso;

// RN16 — o percurso do grafo é testado no Domain (PlanejadorExclusaoTests). Aqui: a integração do
// caso de uso com o repositório e a descrição dos bloqueios.
public class ExcluirRegistroTests
{
    private readonly AmbienteDeTeste _ambiente = new();

    [Fact]
    public async Task Executar_ProjetoComEndpoints_BloqueiaEDescreveAsDependenciasComExemplos()
    {
        var projeto = await _ambiente.CriarProjeto();
        for (var i = 1; i <= 7; i++)
            await _ambiente.CriarEndpoint(projeto, $"servico-{i}");

        var erro = await Assert.ThrowsAsync<ExclusaoBloqueadaException>(() => _ambiente.Excluir("projeto", projeto));

        var dependencia = Assert.Single(erro.Dependencias);
        Assert.Equal("endpoint_servico", dependencia.Entidade);
        Assert.Equal("projeto", dependencia.Campo);
        Assert.Equal(7, dependencia.Quantidade);
        Assert.Equal(5, dependencia.Exemplos.Count);
        Assert.All(dependencia.Exemplos, e => Assert.StartsWith("servico-", e));
        Assert.Empty(_ambiente.Repositorio.Excluidos);
    }

    [Fact]
    public async Task Executar_RelatorioComCenarios_ExcluiOsCenariosAntesDoRelatorio()
    {
        // CASCATA: cenario_teste.relatorio_teste. O endpoint (RESTRINGIR) é apontado, não aponta: não bloqueia.
        var projeto = await _ambiente.CriarProjeto();
        var relatorio = await _ambiente.CriarRelatorio(projeto);
        var endpoint = await _ambiente.CriarEndpoint(projeto);
        var cenario1 = await _ambiente.CriarCenario(relatorio, endpoint, 1);
        var cenario2 = await _ambiente.CriarCenario(relatorio, endpoint, 2);

        await _ambiente.Excluir("relatorio_teste", relatorio);

        var excluidos = _ambiente.Repositorio.Excluidos;
        Assert.Equal(3, excluidos.Count);
        Assert.Equal(relatorio, excluidos[^1]);
        Assert.Contains(cenario1.Id, excluidos);
        Assert.Contains(cenario2.Id, excluidos);
        Assert.Contains(_ambiente.Repositorio.Gravados, r => r.Id == endpoint);
    }

    [Fact]
    public async Task Executar_RegistroInexistenteOuDeOutraEntidade_LancaNaoEncontrado()
    {
        var comunidade = await _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "C" });

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _ambiente.Excluir("comunidade", Guid.NewGuid()));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _ambiente.Excluir("projeto", comunidade.Id));
    }
}
