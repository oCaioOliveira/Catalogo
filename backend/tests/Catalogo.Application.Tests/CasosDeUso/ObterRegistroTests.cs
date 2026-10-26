using System.Text.Json.Nodes;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Registros;

namespace Catalogo.Application.Tests.CasosDeUso;

public class ObterRegistroTests
{
    private readonly AmbienteDeTeste _ambiente = new();

    [Fact]
    public async Task Executar_RegistroComReferencia_DevolveDadosPlanosComIdERotuloDaReferencia()
    {
        // RN07 e RN20
        var projeto = await _ambiente.CriarProjeto("Portal");
        var endpoint = new Registro
        {
            Id = Guid.NewGuid(),
            EntidadeId = _ambiente.Grupo.Endpoint,
            Dados = "{\"nome_servico\":\"consulta\"}",
            CriadoEm = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc),
            Versao = [0, 0, 0, 0, 0, 0, 0, 7],
        };
        endpoint.Referencias.Add(new RegistroReferencia
        {
            RegistroOrigemId = endpoint.Id,
            CampoId = _ambiente.Grupo.EndpointProjeto.Id,
            RegistroDestinoId = projeto,
        });
        _ambiente.Repositorio.Gravados.Add(endpoint);

        var dto = await _ambiente.ObterRegistro.ExecutarAsync("testes", "endpoint_servico", endpoint.Id, CancellationToken.None);

        // Ordem dos campos: projeto (1) e nome_servico (2).
        var esperado = new JsonObject
        {
            ["projeto"] = new JsonObject { ["id"] = projeto.ToString(), ["rotulo"] = "Portal" },
            ["nome_servico"] = "consulta",
        };
        Assert.Equal(esperado.ToJsonString(), dto.Dados.ToJsonString());
        Assert.Equal(endpoint.Id, dto.Id);
        Assert.Equal(endpoint.CriadoEm, dto.CriadoEm);
        Assert.Null(dto.AtualizadoEm);
        Assert.Equal("AAAAAAAAAAc=", dto.Versao);
    }

    [Fact]
    public async Task Executar_ReferenciaParaEntidadeSemRotulo_DevolveRotuloNulo()
    {
        // RN20: o relatório de teste aponta para um projeto; o cenário aponta para o relatório,
        // cujo campo de exibição é uma REFERENCIA (projeto) → rótulo nulo.
        var projeto = await _ambiente.CriarProjeto();
        var relatorio = await _ambiente.CriarRelatorio(projeto);
        var endpoint = await _ambiente.CriarEndpoint(projeto);
        var cenario = await _ambiente.CriarCenario(relatorio, endpoint, 1);

        var dto = await _ambiente.ObterRegistro.ExecutarAsync("testes", "cenario_teste", cenario.Id, CancellationToken.None);

        Assert.Equal(relatorio.ToString(), dto.Dados["relatorio_teste"]!["id"]!.GetValue<string>());
        Assert.Null(dto.Dados["relatorio_teste"]!["rotulo"]);
        Assert.Equal("s", dto.Dados["endpoint_servico"]!["rotulo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Executar_IdInexistente_LancaRecursoNaoEncontrado()
    {
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            _ambiente.ObterRegistro.ExecutarAsync("testes", "comunidade", Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Executar_IdDeOutraEntidade_LancaRecursoNaoEncontrado()
    {
        var projeto = await _ambiente.CriarProjeto();

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            _ambiente.ObterRegistro.ExecutarAsync("testes", "comunidade", projeto, CancellationToken.None));
    }
}
