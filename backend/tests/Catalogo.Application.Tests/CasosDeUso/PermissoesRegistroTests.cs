using System.Text.Json.Nodes;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.Tests.CasosDeUso;

// RN26 a RN29 aplicadas pelos casos de uso de registro.
public class PermissoesRegistroTests
{
    private readonly AmbienteDeTeste _ambiente = new();
    private Guid EquipeA => _ambiente.EquipeA.Id;
    private Guid EquipeB => _ambiente.EquipeB.Id;

    [Fact]
    public async Task Criar_UsuarioDeUmaEquipeSemEquipeId_FicaNaEquipeDele()
    {
        // RN27
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Interno, EquipeA);

        var dto = await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" });

        Assert.Equal(EquipeA, dto.EquipeId);
        Assert.Equal(_ambiente.Usuario.Id, _ambiente.Repositorio.Gravados.Single().CriadoPorId);
    }

    [Fact]
    public async Task Criar_UsuarioDeDuasEquipesSemEquipeId_ExigeEquipeId()
    {
        // RN27
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Interno, EquipeA, EquipeB);

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));

        Assert.Equal(["equipeId"], erro.Erros.Keys);
    }

    [Fact]
    public async Task Criar_AdministradorComEquipeInexistente_EhErroDeValidacao()
    {
        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P", ["equipeId"] = Guid.NewGuid().ToString() }));

        Assert.Contains("não encontrada", erro.Erros["equipeId"][0]);
    }

    [Fact]
    public async Task Criar_EmEquipeDaQualNaoParticipa_LancaAcessoNegado()
    {
        // RN27
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, EquipeA);

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P", ["equipeId"] = EquipeB.ToString() }));
    }

    [Fact]
    public async Task Criar_InternoEmEntidadeGlobal_LancaAcessoNegado()
    {
        // RN27: globais só o administrador cria.
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Interno, EquipeA);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "C" }));
    }

    [Fact]
    public async Task Criar_EquipeIdEmEntidadeGlobal_EhErroDeValidacao()
    {
        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "C", ["equipeId"] = EquipeA.ToString() }));

        Assert.True(erro.Erros.ContainsKey("equipeId"));
    }

    [Fact]
    public async Task Criar_Externo_LancaAcessoNegado()
    {
        // RN26
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Externo, EquipeA);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));
    }

    [Fact]
    public async Task Criar_ReferenciaParaRegistroDeOutraEquipe_RecusaMesmoComUsuarioNasDuas()
    {
        // RN29: a regra é sobre o registro, não sobre o usuário.
        var projetoB = (await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "PB", ["equipeId"] = EquipeB.ToString() })).Id;
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Interno, EquipeA, EquipeB);

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => _ambiente.Criar("endpoint_servico", new JsonObject
        {
            ["equipeId"] = EquipeA.ToString(),
            ["projeto"] = projetoB.ToString(),
            ["nome_servico"] = "s",
        }));

        Assert.Contains("outra equipe", erro.Erros["projeto"][0]);
    }

    [Fact]
    public async Task Criar_ReferenciaParaRegistroQueOUsuarioNaoVe_DizQueNaoExiste()
    {
        // RN28: não revela que o registro da Equipe B existe.
        var projetoB = await _ambiente.CriarProjeto("PB");   // admin cria na Equipe A por padrão...
        _ambiente.Repositorio.Gravados.Single(r => r.Id == projetoB).EquipeId = EquipeB;   // ...e o movemos para B
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Interno, EquipeA);

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => _ambiente.Criar("endpoint_servico", new JsonObject
        {
            ["projeto"] = projetoB.ToString(),
            ["nome_servico"] = "s",
        }));

        Assert.Contains("não encontrado", erro.Erros["projeto"][0]);
    }

    [Fact]
    public async Task Obter_RegistroDeOutraEquipe_LancaNaoEncontrado()
    {
        // RN28
        var projetoA = await _ambiente.CriarProjeto();
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, EquipeB);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _ambiente.Obter("projeto", projetoA));
    }

    [Fact]
    public async Task Obter_RegistroGlobal_TodosVeem()
    {
        var comunidade = (await _ambiente.Criar("comunidade", new JsonObject { ["nome"] = "C" })).Id;
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Externo, EquipeB);

        Assert.Equal(comunidade, (await _ambiente.Obter("comunidade", comunidade)).Id);
    }

    [Fact]
    public async Task Atualizar_RegistroDeOutraEquipe_LancaNaoEncontrado()
    {
        var projetoA = await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" });
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, EquipeB);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            _ambiente.Atualizar("projeto", projetoA.Id, new JsonObject { ["versao"] = projetoA.Versao, ["nome"] = "X" }));
    }

    [Fact]
    public async Task Atualizar_TrocandoAEquipe_EhErroDeValidacao()
    {
        // RN27: a equipe de um registro não muda.
        var projeto = await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" });

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() => _ambiente.Atualizar("projeto", projeto.Id,
            new JsonObject { ["versao"] = projeto.Versao, ["nome"] = "P", ["equipeId"] = EquipeB.ToString() }));

        Assert.True(erro.Erros.ContainsKey("equipeId"));
    }

    [Fact]
    public async Task Excluir_Interno_LancaAcessoNegado()
    {
        // RN26: INTERNO não exclui.
        var projeto = await _ambiente.CriarProjeto();
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Interno, EquipeA);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => _ambiente.Excluir("projeto", projeto));
        Assert.Empty(_ambiente.Repositorio.Excluidos);
    }

    [Fact]
    public async Task Excluir_CascataAlcancandoRegistroQueOUsuarioNaoPodeExcluir_NegaTudo()
    {
        // RN16: o plano inteiro precisa estar dentro do que o usuário pode excluir.
        var projeto = await _ambiente.CriarProjeto();
        var relatorio = await _ambiente.CriarRelatorio(projeto);
        var endpoint = await _ambiente.CriarEndpoint(projeto);
        var cenario = await _ambiente.CriarCenario(relatorio, endpoint, 1);
        // Situação que as regras impedem (cenário de outra equipe apontando em cascata): simulada direto.
        _ambiente.Repositorio.Gravados.Single(r => r.Id == cenario.Id).EquipeId = EquipeB;
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, EquipeA);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => _ambiente.Excluir("relatorio_teste", relatorio));
        Assert.Empty(_ambiente.Repositorio.Excluidos);
    }

    [Fact]
    public async Task Listar_UsuarioDaEquipeA_NaoVeRegistrosDaEquipeB()
    {
        // RN26: o escopo vai para a consulta.
        var projetoA = await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "PA", ["equipeId"] = EquipeA.ToString() });
        await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "PB", ["equipeId"] = EquipeB.ToString() });
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Externo, EquipeA);

        var pagina = await _ambiente.ListarRegistros.ExecutarAsync("testes", "projeto", [], CancellationToken.None);

        Assert.Equal([projetoA.Id], pagina.Itens.Select(i => i.Id));
    }

    [Fact]
    public async Task Listar_FiltrandoPorEquipeDaQualNaoParticipa_LancaAcessoNegado()
    {
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Externo, EquipeA);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => _ambiente.ListarRegistros.ExecutarAsync(
            "testes", "projeto", [KeyValuePair.Create("equipeId", EquipeB.ToString())], CancellationToken.None));
    }

    [Fact]
    public async Task Unicidade_MesmoNomeEmEquipesDiferentes_EhPermitido()
    {
        // RN09: a unicidade de entidades por equipe vale dentro da equipe.
        await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "Portal", ["equipeId"] = EquipeA.ToString() });
        await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "Portal", ["equipeId"] = EquipeB.ToString() });

        await Assert.ThrowsAsync<ConflitoUnicidadeException>(() =>
            _ambiente.Criar("projeto", new JsonObject { ["nome"] = "PORTAL", ["equipeId"] = EquipeA.ToString() }));
    }
}
