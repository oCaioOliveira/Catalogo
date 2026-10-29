using System.Text.Json.Nodes;
using Catalogo.Application.Dtos;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;
using Catalogo.Domain.Historico;

namespace Catalogo.Application.Tests.CasosDeUso;

// Aprovação de registros nos casos de uso (RN33 a RN38). Aprovação ativa para "projeto" na Equipe A.
public class AprovacaoTests
{
    private const string Justificativa = "Conferido com a área responsável.";

    private readonly AmbienteDeTeste _ambiente = new();
    private readonly UsuarioAtual _externo;
    private readonly UsuarioAtual _outroExterno;
    private readonly UsuarioAtual _interno;
    private readonly UsuarioAtual _gestor;

    public AprovacaoTests()
    {
        var equipeA = _ambiente.EquipeA.Id;
        _externo = UsuarioAtualFalso.Criar(Perfil.Externo, equipeA);
        _outroExterno = UsuarioAtualFalso.Criar(Perfil.Externo, equipeA);
        _interno = UsuarioAtualFalso.Criar(Perfil.Interno, equipeA);
        _gestor = UsuarioAtualFalso.Criar(Perfil.Gestor, equipeA);
        _ambiente.AtivarAprovacao(_ambiente.Grupo.Projeto, equipeA);
    }

    [Fact]
    public async Task Criar_ExternoComAprovacaoAtiva_NascePendente()
    {
        // RN34
        var dto = await Como(_externo, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));

        Assert.Equal("PENDENTE", dto.SituacaoAprovacao);
        Assert.Null(dto.UltimaDecisao);
    }

    [Fact]
    public async Task Criar_ExternoSemAprovacaoAtiva_Continua403()
    {
        // RN34: endpoint_servico não tem aprovação ativa.
        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            Como(_externo, () => _ambiente.Criar("relatorio_teste", new JsonObject())));
    }

    [Fact]
    public async Task Criar_InternoComAprovacaoAtiva_NasceAprovado()
    {
        var dto = await Como(_interno, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));

        Assert.Equal("APROVADO", dto.SituacaoAprovacao);
    }

    [Fact]
    public async Task FluxoCompleto_RejeitaReenviaEAprova_ComHistorico()
    {
        // RN35 e RN36
        var criado = await Como(_externo, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "Primeira versão" }));

        var rejeitado = await Como(_gestor, () => _ambiente.Decidir("projeto", criado.Id, "REJEITAR", Justificativa));
        Assert.Equal("REJEITADO", rejeitado.SituacaoAprovacao);
        Assert.Equal("REJEITAR", rejeitado.UltimaDecisao!.Decisao);

        var reenviado = await Como(_externo, () => _ambiente.Atualizar("projeto", criado.Id,
            new JsonObject { ["versao"] = rejeitado.Versao, ["nome"] = "Segunda versão" }));
        Assert.Equal("PENDENTE", reenviado.SituacaoAprovacao);

        var aprovado = await Como(_gestor, () => _ambiente.Decidir("projeto", criado.Id, "APROVAR", Justificativa));
        Assert.Equal("APROVADO", aprovado.SituacaoAprovacao);
        Assert.Equal(Justificativa, aprovado.UltimaDecisao!.Justificativa);

        var linhas = _ambiente.Historico.Linhas.Where(l => l.AlvoId == criado.Id).ToList();
        Assert.Equal(
            [OperacaoHistorico.Criacao, OperacaoHistorico.Rejeicao, OperacaoHistorico.Alteracao, OperacaoHistorico.Aprovacao],
            linhas.Select(l => l.Operacao));
        Assert.Equal(Justificativa, linhas[1].Justificativa);
        Assert.Contains("\"situacaoAprovacao\":\"PENDENTE\"", linhas[2].DadosDepois);   // o reenvio aparece no histórico
    }

    [Fact]
    public async Task Alterar_ExternoDepoisDeAprovado_Recebe403()
    {
        // RN35
        var criado = await Como(_externo, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));
        var aprovado = await Como(_gestor, () => _ambiente.Decidir("projeto", criado.Id, "APROVAR", Justificativa));

        await Assert.ThrowsAsync<AcessoNegadoException>(() => Como(_externo, () =>
            _ambiente.Atualizar("projeto", criado.Id, new JsonObject { ["versao"] = aprovado.Versao, ["nome"] = "X" })));
    }

    [Fact]
    public async Task Alterar_InternoNumPendente_NaoMudaASituacao()
    {
        // RN35
        var criado = await Como(_externo, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));

        var alterado = await Como(_interno, () =>
            _ambiente.Atualizar("projeto", criado.Id, new JsonObject { ["versao"] = criado.Versao, ["nome"] = "Q" }));

        Assert.Equal("PENDENTE", alterado.SituacaoAprovacao);
    }

    [Fact]
    public async Task Ver_PendenteDeOutroExterno_NaoEncontradoEInternoVe()
    {
        // RN37
        var criado = await Como(_externo, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Como(_outroExterno, () => _ambiente.Obter("projeto", criado.Id)));
        Assert.Equal(criado.Id, (await Como(_interno, () => _ambiente.Obter("projeto", criado.Id))).Id);
    }

    [Fact]
    public async Task Listar_OutroExternoNaoVePendenteMasVeAprovado()
    {
        // RN37: no escopo da consulta.
        var pendente = await Como(_externo, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "Pendente" }));
        var aprovado = await Como(_interno, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "Aprovado" }));

        var doOutro = await Como(_outroExterno, () => _ambiente.ListarRegistros.ExecutarAsync("testes", "projeto", [], CancellationToken.None));
        var doAutor = await Como(_externo, () => _ambiente.ListarRegistros.ExecutarAsync("testes", "projeto", [], CancellationToken.None));

        Assert.Equal([aprovado.Id], doOutro.Itens.Select(i => i.Id));
        Assert.Equal(2, doAutor.Total);
        Assert.Contains(pendente.Id, doAutor.Itens.Select(i => i.Id));
    }

    [Fact]
    public async Task Referenciar_PendenteDeOutroUsuario_EhErroDeValidacao()
    {
        // RN38: o próprio autor pode; o interno, que vê o pendente, não pode apontar para ele.
        var projeto = await Como(_externo, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => Como(_interno, () =>
            _ambiente.Criar("relatorio_teste", new JsonObject { ["projeto"] = projeto.Id.ToString() })));

        Assert.Contains("ainda não foi aprovado", erro.Erros["projeto"][0]);
    }

    [Fact]
    public async Task Decidir_InternoGestorDeOutraEquipeJustificativaEDecidirDeNovo()
    {
        // RN36
        var criado = await Como(_externo, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));
        var gestorB = UsuarioAtualFalso.Criar(Perfil.Gestor, _ambiente.EquipeB.Id);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => Como(_interno, () => _ambiente.Decidir("projeto", criado.Id, "APROVAR", Justificativa)));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Como(gestorB, () => _ambiente.Decidir("projeto", criado.Id, "APROVAR", Justificativa)));
        var semJustificativa = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            Como(_gestor, () => _ambiente.Decidir("projeto", criado.Id, "REJEITAR", null)));
        Assert.True(semJustificativa.Erros.ContainsKey("justificativa"));

        await Como(_gestor, () => _ambiente.Decidir("projeto", criado.Id, "APROVAR", Justificativa));
        var deNovo = await Assert.ThrowsAsync<RegraDeNegocioException>(() =>
            Como(_gestor, () => _ambiente.Decidir("projeto", criado.Id, "REJEITAR", Justificativa)));
        Assert.Equal("REGISTRO_NAO_PENDENTE", deNovo.Codigo);
    }

    [Fact]
    public async Task Configurar_PermissoesEEntidadeGlobal()
    {
        // RN33
        var equipeA = _ambiente.EquipeA.Id;
        var ativar = new ConfiguracaoAprovacaoEntrada(true);

        var configurada = await Como(_gestor, () => Configurar(equipeA, "relatorio_teste", ativar));
        Assert.True(configurada.Ativa);
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => Como(_gestor, () => Configurar(_ambiente.EquipeB.Id, "relatorio_teste", ativar)));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => Como(_interno, () => Configurar(equipeA, "relatorio_teste", ativar)));
        await Assert.ThrowsAsync<ValidacaoRegistroException>(() => Configurar(equipeA, "comunidade", ativar));   // global
    }

    [Fact]
    public async Task Desativar_ComPendentes_EhRecusadoComAQuantidade()
    {
        // RN33
        var criado = await Como(_externo, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(() =>
            Como(_gestor, () => Configurar(_ambiente.EquipeA.Id, "projeto", new ConfiguracaoAprovacaoEntrada(false))));
        Assert.Equal(1, erro.Dados["quantidade"]);

        await Como(_gestor, () => _ambiente.Decidir("projeto", criado.Id, "APROVAR", Justificativa));
        var desativada = await Como(_gestor, () => Configurar(_ambiente.EquipeA.Id, "projeto", new ConfiguracaoAprovacaoEntrada(false)));
        Assert.False(desativada.Ativa);
        Assert.Contains(_ambiente.Historico.Linhas, l => l.AlvoTipo == AlvoHistorico.Configuracao && l.Operacao == OperacaoHistorico.Alteracao);
    }

    [Fact]
    public async Task FilaDePendentes_GestorSoDasSuasEquipesEInterno403()
    {
        // RN36
        var daA = await Como(_externo, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));
        _ambiente.AtivarAprovacao(_ambiente.Grupo.Projeto, _ambiente.EquipeB.Id);
        var externoB = UsuarioAtualFalso.Criar(Perfil.Externo, _ambiente.EquipeB.Id);
        await Como(externoB, () => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));

        var filaGestorA = await Como(_gestor, () => _ambiente.ListarPendentes.ExecutarAsync([], CancellationToken.None));
        var filaAdmin = await _ambiente.ListarPendentes.ExecutarAsync([], CancellationToken.None);

        Assert.Equal([daA.Id], filaGestorA.Itens.Select(i => i.Id));
        Assert.Equal(2, filaAdmin.Total);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => Como(_interno, () => _ambiente.ListarPendentes.ExecutarAsync([], CancellationToken.None)));
    }

    [Fact]
    public async Task ListarEntidades_ComEquipeInformaRequerAprovacao()
    {
        var entidades = await _ambiente.ListarEntidades.ExecutarAsync("testes", _ambiente.EquipeA.Id.ToString(), CancellationToken.None);

        Assert.True(entidades.Single(e => e.Chave == "projeto").RequerAprovacao);
        Assert.False(entidades.Single(e => e.Chave == "endpoint_servico").RequerAprovacao);
        Assert.False(entidades.Single(e => e.Chave == "comunidade").RequerAprovacao);   // global
        Assert.Null((await _ambiente.ListarEntidades.ExecutarAsync("testes", null, CancellationToken.None))[0].RequerAprovacao);
    }

    private Task<ConfiguracaoAprovacaoDto> Configurar(Guid equipeId, string entidade, ConfiguracaoAprovacaoEntrada entrada) =>
        _ambiente.DefinirConfiguracaoAprovacao.ExecutarAsync(equipeId, "testes",
            entidade, entrada, CancellationToken.None);

    /// <summary>Executa a ação como outro usuário e volta para o administrador.</summary>
    private async Task<T> Como<T>(UsuarioAtual usuario, Func<Task<T>> acao)
    {
        var anterior = _ambiente.Usuario;
        _ambiente.Usuario = usuario;
        try
        {
            return await acao();
        }
        finally
        {
            _ambiente.Usuario = anterior;
        }
    }
}
