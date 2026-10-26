using System.Text.Json.Nodes;
using Catalogo.Application.Dtos;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.Tests.CasosDeUso;

public class AtualizarRegistroTests
{
    private readonly AmbienteDeTeste _ambiente = new();

    [Fact]
    public async Task Executar_VersaoDivergente_LancaConflitoDeConcorrenciaENaoGrava()
    {
        // RN12
        var criado = await CriarComunidade("Original");
        var salvamentosAntes = _ambiente.UnidadeDeTrabalho.VezesQueSalvou;

        var erro = await Assert.ThrowsAsync<ConflitoConcorrenciaException>(() =>
            _ambiente.Atualizar("comunidade", criado.Id, new JsonObject
            {
                ["versao"] = Convert.ToBase64String(new byte[8]),
                ["nome"] = "Alterado",
            }));

        Assert.Contains("alterado por outra pessoa", erro.Message);
        Assert.Equal(salvamentosAntes, _ambiente.UnidadeDeTrabalho.VezesQueSalvou);
        Assert.Contains("Original", Gravado(criado.Id).Dados);
    }

    [Fact]
    public async Task Executar_SemVersao_LancaValidacaoNaChaveVersao()
    {
        // RN12: a versão é obrigatória no corpo do PUT.
        var criado = await CriarComunidade("Original");

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            _ambiente.Atualizar("comunidade", criado.Id, new JsonObject { ["nome"] = "Alterado" }));

        Assert.Equal(["versao"], erro.Erros.Keys);
    }

    [Fact]
    public async Task Executar_VersaoAtual_InformaAVersaoEsperadaParaOUpdateEGrava()
    {
        // RN12: além da comparação, a versão lida vai para o WHERE do UPDATE (DefinirVersaoEsperada).
        var criado = await CriarComunidade("Original");
        _ambiente.Relogio.Agora = new DateTimeOffset(2026, 10, 22, 9, 30, 0, TimeSpan.Zero);

        var atualizado = await _ambiente.Atualizar("comunidade", criado.Id, ComVersao(criado, new JsonObject { ["nome"] = "Alterado" }));

        Assert.Equal("Alterado", atualizado.Dados["nome"]!.GetValue<string>());
        Assert.NotEqual(criado.Versao, atualizado.Versao);
        Assert.Equal(new DateTime(2026, 10, 22, 9, 30, 0, DateTimeKind.Utc), atualizado.AtualizadoEm);
        Assert.Equal(_ambiente.Usuario.Id, atualizado.AtualizadoPor!.Id);
        Assert.Equal("Alterado", Assert.Single(Gravado(criado.Id).Valores).ValorTexto);
    }

    [Fact]
    public async Task Executar_MantendoOProprioValorUnico_NaoConflitaConsigoMesmo()
    {
        // RN15 + RN09: o registro mantém "Comunidade Ágil" e só muda a descrição.
        var criado = await CriarComunidade("Comunidade Ágil");

        var atualizado = await _ambiente.Atualizar("comunidade", criado.Id,
            ComVersao(criado, new JsonObject { ["nome"] = "COMUNIDADE AGIL", ["descricao"] = "nova" }));

        Assert.Equal("nova", atualizado.Dados["descricao"]!.GetValue<string>());
        Assert.Single(Gravado(criado.Id).ChavesUnicas);
    }

    [Fact]
    public async Task Executar_ValorUnicoDeOutroRegistro_LancaConflito()
    {
        // RN09
        await CriarComunidade("Já existe");
        var criado = await CriarComunidade("Outra");

        var erro = await Assert.ThrowsAsync<ConflitoUnicidadeException>(() =>
            _ambiente.Atualizar("comunidade", criado.Id, ComVersao(criado, new JsonObject { ["nome"] = "ja existe" })));

        Assert.Equal("uq_comunidade_nome", erro.NomeRestricao);
    }

    [Fact]
    public async Task Executar_CampoOpcionalAusente_RemoveDoJsonEDosValores()
    {
        // RN15: PUT substitui o registro inteiro.
        var criado = await _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "C", ["descricao"] = "texto" });
        Assert.Equal(2, Gravado(criado.Id).Valores.Count);

        var atualizado = await _ambiente.Atualizar("comunidade", criado.Id, ComVersao(criado, new JsonObject { ["nome"] = "C" }));

        Assert.False(atualizado.Dados.ContainsKey("descricao"));
        Assert.Equal("{\"nome\":\"C\"}", Gravado(criado.Id).Dados);
        Assert.Equal(_ambiente.Grupo.ComunidadeNome.Id, Assert.Single(Gravado(criado.Id).Valores).CampoId);
    }

    [Fact]
    public async Task Executar_TrocaDeReferencia_AlteraNoLugar()
    {
        var projetoA = await _ambiente.CriarProjeto("A");
        var projetoB = await _ambiente.CriarProjeto("B");
        var endpoint = await _ambiente.Criar("endpoint_servico", new JsonObject { ["projeto"] = projetoA.ToString(), ["nome_servico"] = "s" });

        var atualizado = await _ambiente.Atualizar("endpoint_servico", endpoint.Id,
            ComVersao(endpoint, new JsonObject { ["projeto"] = projetoB.ToString(), ["nome_servico"] = "s" }));

        Assert.Equal(projetoB, Assert.Single(Gravado(endpoint.Id).Referencias).RegistroDestinoId);
        Assert.Equal("B", atualizado.Dados["projeto"]!["rotulo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Executar_DependenciaViolada_LancaValidacao()
    {
        // RN08 também vale no PUT.
        var projetoA = await _ambiente.CriarProjeto("A");
        var projetoB = await _ambiente.CriarProjeto("B");
        var relatorio = await _ambiente.CriarRelatorio(projetoA);
        var endpointA = await _ambiente.CriarEndpoint(projetoA, "a");
        var endpointB = await _ambiente.CriarEndpoint(projetoB, "b");
        var cenario = await _ambiente.CriarCenario(relatorio, endpointA, 1);

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => _ambiente.Atualizar("cenario_teste", cenario.Id,
            ComVersao(cenario, AmbienteDeTeste.DadosDoCenario(relatorio, endpointB, 1))));

        Assert.Equal(["endpoint_servico"], erro.Erros.Keys);
    }

    [Fact]
    public async Task Executar_RegistroDeOutraEntidade_LancaNaoEncontrado()
    {
        var projeto = await _ambiente.CriarProjeto();

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            _ambiente.Atualizar("comunidade", projeto, new JsonObject { ["versao"] = "AA==", ["nome"] = "x" }));
    }

    private Task<RegistroDto> CriarComunidade(string nome) => _ambiente.Criar("comunidade", new JsonObject { ["nome"] = nome });

    private Domain.Registros.Registro Gravado(Guid id) => _ambiente.Repositorio.Gravados.Single(r => r.Id == id);

    private static JsonObject ComVersao(RegistroDto lido, JsonObject dados)
    {
        dados["versao"] = lido.Versao;
        return dados;
    }
}
