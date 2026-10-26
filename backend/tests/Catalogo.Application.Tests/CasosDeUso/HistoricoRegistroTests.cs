using System.Text.Json.Nodes;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.Tests.CasosDeUso;

// RN32 — histórico gravado pelos casos de uso de registro.
public class HistoricoRegistroTests
{
    private readonly AmbienteDeTeste _ambiente = new();

    [Fact]
    public async Task CriarAlterarExcluir_GeramUmaLinhaCadaComAntesEDepois()
    {
        var criado = await _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "A" });
        await _ambiente.Atualizar("comunidade", criado.Id, new JsonObject { ["versao"] = criado.Versao, ["nome"] = "B" });
        await _ambiente.Excluir("comunidade", criado.Id);

        var linhas = _ambiente.Historico.Linhas;
        Assert.Equal([OperacaoHistorico.Criacao, OperacaoHistorico.Alteracao, OperacaoHistorico.Exclusao], linhas.Select(l => l.Operacao));
        Assert.All(linhas, l => Assert.Equal(criado.Id, l.AlvoId));
        Assert.All(linhas, l => Assert.Equal(_ambiente.Usuario.Matricula, l.UsuarioMatricula));
        Assert.Null(linhas[0].DadosAntes);
        Assert.Equal("{\"nome\":\"A\"}", linhas[0].DadosDepois);
        Assert.Equal("{\"nome\":\"A\"}", linhas[1].DadosAntes);
        Assert.Equal("{\"nome\":\"B\"}", linhas[1].DadosDepois);
        Assert.Equal("{\"nome\":\"B\"}", linhas[2].DadosAntes);
        Assert.Null(linhas[2].DadosDepois);
    }

    [Fact]
    public async Task Historico_GuardaReferenciasComoGuidEAEquipe()
    {
        var projeto = await _ambiente.CriarProjeto();
        var endpoint = await _ambiente.CriarEndpoint(projeto, "consulta");

        var linha = _ambiente.Historico.Linhas.Single(l => l.AlvoId == endpoint);

        Assert.Equal($"{{\"projeto\":\"{projeto}\",\"nome_servico\":\"consulta\"}}", linha.DadosDepois);
        Assert.Equal(_ambiente.EquipeA.Id, linha.EquipeId);
        Assert.Equal(_ambiente.Grupo.Endpoint, linha.EntidadeId);
    }

    [Fact]
    public async Task ExclusaoEmCascata_UmaLinhaPorRegistroComOMesmoCorrelacaoId()
    {
        var projeto = await _ambiente.CriarProjeto();
        var relatorio = await _ambiente.CriarRelatorio(projeto);
        var endpoint = await _ambiente.CriarEndpoint(projeto);
        await _ambiente.CriarCenario(relatorio, endpoint, 1);
        await _ambiente.CriarCenario(relatorio, endpoint, 2);

        await _ambiente.Excluir("relatorio_teste", relatorio);

        var exclusoes = _ambiente.Historico.Linhas.Where(l => l.Operacao == OperacaoHistorico.Exclusao).ToList();
        Assert.Equal(3, exclusoes.Count);
        Assert.Single(exclusoes.Select(l => l.CorrelacaoId).Distinct());
    }

    [Fact]
    public async Task ListarHistorico_MudancasPorCampoComRotuloAtualNasReferencias()
    {
        var projetoA = await _ambiente.CriarProjeto("Portal A");
        var projetoB = await _ambiente.CriarProjeto("Portal B");
        var endpoint = await _ambiente.Criar("endpoint_servico", new JsonObject { ["projeto"] = projetoA.ToString(), ["nome_servico"] = "s" });
        await _ambiente.Atualizar("endpoint_servico", endpoint.Id,
            new JsonObject { ["versao"] = endpoint.Versao, ["projeto"] = projetoB.ToString(), ["nome_servico"] = "s" });

        var pagina = await _ambiente.ListarHistoricoRegistro.ExecutarAsync("testes", "endpoint_servico", endpoint.Id, [], CancellationToken.None);

        var alteracao = pagina.Itens[0];   // mais recente primeiro
        Assert.Equal("ALTERACAO", alteracao.Operacao);
        var mudanca = Assert.Single(alteracao.Mudancas);
        Assert.Equal("projeto", mudanca.Campo);
        Assert.Equal("Portal A", mudanca.De!["rotulo"]!.GetValue<string>());
        Assert.Equal("Portal B", mudanca.Para!["rotulo"]!.GetValue<string>());
        Assert.Equal("admin", alteracao.Usuario.Matricula);
    }

    [Fact]
    public async Task ListarHistorico_RegistroExcluidoDeOutraEquipe_NaoEncontrado()
    {
        // A equipe gravada no histórico vale depois que o registro some (RN28).
        var projeto = await _ambiente.CriarProjeto();
        await _ambiente.Excluir("projeto", projeto);

        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Externo, _ambiente.EquipeB.Id);
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            _ambiente.ListarHistoricoRegistro.ExecutarAsync("testes", "projeto", projeto, [], CancellationToken.None));

        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Externo, _ambiente.EquipeA.Id);
        var pagina = await _ambiente.ListarHistoricoRegistro.ExecutarAsync("testes", "projeto", projeto, [], CancellationToken.None);
        Assert.Equal(2, pagina.Total);
    }
}
