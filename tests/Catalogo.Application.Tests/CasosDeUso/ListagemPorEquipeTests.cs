using System.Text.Json.Nodes;
using Catalogo.Application.CasosDeUso;
using Catalogo.Application.Comum;
using Catalogo.Application.Dtos;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.Tests.CasosDeUso;

// RN90 a RN93: listagens habilitadas por equipe. O grupo de teste começa com a Equipe A e a B habilitadas (AmbienteDeTeste).
public class ListagemPorEquipeTests
{
    private readonly AmbienteDeTeste _ambiente = new();

    private Guid EquipeA => _ambiente.EquipeA.Id;
    private Guid EquipeB => _ambiente.EquipeB.Id;

    private ListarGrupos ListarGrupos => new(_ambiente.UsuarioAtual, new MetadadosProviderFalso(_ambiente.Grupo.Grupo), _ambiente.Equipes);

    private ListarOpcoes ListarOpcoes => new(
        _ambiente.UsuarioAtual, new MetadadosProviderFalso(_ambiente.Grupo.Grupo), _ambiente.Repositorio, new ResolvedorEquipe(_ambiente.Equipes));

    /// <summary>Só a Equipe B fica habilitada (a A "deixou de usar" a listagem, RN92).</summary>
    private void SoEquipeBHabilitada() => _ambiente.Grupo.EquipesHabilitadas.Remove(EquipeA);

    [Fact]
    public async Task ListarGrupos_SemEquipeHabilitada_NaoDevolveAListagem()
    {
        // RN90
        SoEquipeBHabilitada();
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Interno, EquipeA);

        Assert.Empty(await ListarGrupos.ExecutarAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ListarGrupos_GestorDeDuasEquipes_MinhasEquipesSoAsHabilitadas()
    {
        // RN91: "minhasEquipes" é a interseção; "possuiEquipes" diz se há alguma habilitada.
        SoEquipeBHabilitada();
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, EquipeA, EquipeB);

        var grupo = Assert.Single(await ListarGrupos.ExecutarAsync(CancellationToken.None));

        Assert.Equal([EquipeB], grupo.MinhasEquipes.Select(e => e.Id));
        Assert.True(grupo.PossuiEquipes);
    }

    [Fact]
    public async Task ListarGrupos_ListagemNovaSemEquipes_SoParaOAdministrador()
    {
        // RN93
        _ambiente.Grupo.EquipesHabilitadas.Clear();

        var doAdmin = Assert.Single(await ListarGrupos.ExecutarAsync(CancellationToken.None));
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, EquipeA, EquipeB);
        var doGestor = await ListarGrupos.ExecutarAsync(CancellationToken.None);

        Assert.False(doAdmin.PossuiEquipes);
        Assert.Empty(doAdmin.MinhasEquipes);
        Assert.Empty(doGestor);
    }

    [Fact]
    public async Task ListarGrupos_Administrador_MinhasEquipesSaoAsHabilitadasEAtivas()
    {
        // RN91: para o administrador, todas as habilitadas; uma equipe inativa não é oferecida em lugar nenhum.
        _ambiente.EquipeB.Ativa = false;

        var grupo = Assert.Single(await ListarGrupos.ExecutarAsync(CancellationToken.None));

        Assert.Equal([EquipeA], grupo.MinhasEquipes.Select(e => e.Id));
    }

    [Fact]
    public async Task ListagemNaoVisivel_404EmTodosOsEndpoints()
    {
        // RN90: metadados, registros, exportação e fila filtrada pela listagem respondem 404.
        SoEquipeBHabilitada();
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, EquipeA);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _ambiente.ListarEntidades.ExecutarAsync("testes", null, CancellationToken.None));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            _ambiente.ListarRegistros.ExecutarAsync("testes", "projeto", [], CancellationToken.None));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _ambiente.Exportar("projeto"));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" }));
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            _ambiente.ListarPendentes.ExecutarAsync([KeyValuePair.Create("grupo", "testes")], CancellationToken.None));
    }

    [Fact]
    public async Task Itens_DeEquipeRemovida_OcultosParaOMembro_VisiveisParaOAdministrador_EVoltamAoReabilitar()
    {
        // RN92: nada é apagado.
        var doA = (await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P-A", ["equipeId"] = EquipeA.ToString() })).Id;
        await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P-B", ["equipeId"] = EquipeB.ToString() });
        var gestor = UsuarioAtualFalso.Criar(Perfil.Gestor, EquipeA, EquipeB);

        SoEquipeBHabilitada();
        _ambiente.Usuario = gestor;
        var oculto = await _ambiente.ListarRegistros.ExecutarAsync("testes", "projeto", [], CancellationToken.None);
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _ambiente.Obter("projeto", doA));

        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Administrador);
        var doAdmin = await _ambiente.ListarRegistros.ExecutarAsync("testes", "projeto", [], CancellationToken.None);

        _ambiente.Grupo.EquipesHabilitadas.Add(EquipeA);
        _ambiente.Usuario = gestor;
        var reabilitado = await _ambiente.ListarRegistros.ExecutarAsync("testes", "projeto", [], CancellationToken.None);

        Assert.Equal(["P-B"], oculto.Itens.Select(r => r.Dados["nome"]!.GetValue<string>()));
        Assert.Equal(2, doAdmin.Total);
        Assert.Equal(2, reabilitado.Total);
    }

    [Fact]
    public async Task Criar_GestorDeDuasEquipesComUmaSoNaListagem_UsaElaSemInformarEquipeId()
    {
        // RN91 + RN27: a regra do equipeId opcional olha só as equipes do usuário NESTA listagem.
        SoEquipeBHabilitada();
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, EquipeA, EquipeB);

        var criado = await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P" });

        Assert.Equal(EquipeB, criado.EquipeId);
    }

    [Fact]
    public async Task Criar_EmEquipeDoUsuarioNaoHabilitada_403()
    {
        // RN91
        SoEquipeBHabilitada();
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, EquipeA, EquipeB);

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P", ["equipeId"] = EquipeA.ToString() }));
    }

    [Fact]
    public async Task Criar_AdministradorEmEquipeNaoHabilitada_400NoEquipeId()
    {
        // RN91: o administrador escolhe entre as equipes habilitadas.
        SoEquipeBHabilitada();

        var erro = await Assert.ThrowsAsync<ValidacaoRegistroException>(() =>
            _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P", ["equipeId"] = EquipeA.ToString() }));

        Assert.Contains("não habilitada", erro.Erros["equipeId"][0]);
    }

    [Fact]
    public async Task Opcoes_DoFormulario_SoComEquipeDaListagem()
    {
        // RN91 + RN21: as opções de um campo seguem a equipe do formulário, que precisa estar no conjunto.
        var projeto = (await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "P-B", ["equipeId"] = EquipeB.ToString() })).Id;
        SoEquipeBHabilitada();
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, EquipeA, EquipeB);

        var opcoes = await ListarOpcoes.ExecutarAsync("testes", "endpoint_servico", "projeto", [], CancellationToken.None);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => ListarOpcoes.ExecutarAsync(
            "testes", "endpoint_servico", "projeto", [KeyValuePair.Create("equipeId", EquipeA.ToString())], CancellationToken.None));

        Assert.Equal([projeto], opcoes.Itens.Select(o => o.Id));
    }

    [Fact]
    public async Task Fila_SoPendentesDeEquipesHabilitadas()
    {
        // RN91, RN92: o pendente de uma equipe removida sai da fila (para o gestor e para o administrador).
        var entidade = _ambiente.Grupo.Grupo.ObterEntidade("projeto")!.Id;
        _ambiente.AtivarAprovacao(entidade, EquipeA);
        _ambiente.AtivarAprovacao(entidade, EquipeB);
        var externoA = UsuarioAtualFalso.Criar(Perfil.Externo, EquipeA);
        var externoB = UsuarioAtualFalso.Criar(Perfil.Externo, EquipeB);
        _ambiente.Usuario = externoA;
        await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "Pendente A" });
        _ambiente.Usuario = externoB;
        await _ambiente.Criar("projeto", new JsonObject { ["nome"] = "Pendente B" });

        SoEquipeBHabilitada();
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Gestor, EquipeA, EquipeB);
        var doGestor = await _ambiente.ListarPendentes.ExecutarAsync([], CancellationToken.None);
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Administrador);
        var doAdmin = await _ambiente.ListarPendentes.ExecutarAsync([], CancellationToken.None);

        Assert.Equal([EquipeB], doGestor.Itens.Select(p => p.Equipe!.Id));
        Assert.Equal([EquipeB], doAdmin.Itens.Select(p => p.Equipe!.Id));
    }

    [Theory]
    [InlineData(Perfil.Gestor)]
    [InlineData(Perfil.Administrador)]
    public async Task ConfiguracaoDaEquipe_EquipeNaoHabilitada_404(Perfil perfil)
    {
        // RN91: aprovação (e avaliação, pelo mesmo ConfiguracaoDaEquipe) só para equipes habilitadas na listagem.
        SoEquipeBHabilitada();
        _ambiente.Usuario = UsuarioAtualFalso.Criar(perfil, EquipeA, EquipeB);

        var erro = await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => _ambiente.DefinirConfiguracaoAprovacao.ExecutarAsync(
            EquipeA, "testes", "projeto", new ConfiguracaoAprovacaoEntrada(true), CancellationToken.None));
        var naHabilitada = await _ambiente.DefinirConfiguracaoAprovacao.ExecutarAsync(
            EquipeB, "testes", "projeto", new ConfiguracaoAprovacaoEntrada(true), CancellationToken.None);

        Assert.Equal(CodigosErro.EquipeNaoEncontrada, erro.Codigo);
        Assert.True(naHabilitada.Ativa);
    }
}
