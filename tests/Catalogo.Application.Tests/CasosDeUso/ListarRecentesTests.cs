using Catalogo.Application.Abstracoes;
using Catalogo.Application.CasosDeUso;
using Catalogo.Application.Tests.Dubles;
using Catalogo.Domain.Acesso;
using Catalogo.Domain.Excecoes;

namespace Catalogo.Application.Tests.CasosDeUso;

// RN96: os itens recentes do usuário numa área de gestão (página Início). A consulta de verdade (histórico ∩ visíveis, no banco)
// é testada na integração (RecentesApiTests); aqui, o escopo que o caso de uso monta e a resposta.
public class ListarRecentesTests
{
    private readonly AmbienteDeTeste _ambiente = new();

    private ListarRecentes ListarRecentes => new(_ambiente.UsuarioAtual, new MetadadosProviderFalso(_ambiente.Grupo.Grupo), _ambiente.Repositorio);

    [Fact]
    public async Task Executar_AreaQueOUsuarioNaoVe_Devolve404()
    {
        // RN90: área sem nenhuma equipe do usuário habilitada.
        _ambiente.Grupo.EquipesHabilitadas.Clear();
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Interno, _ambiente.EquipeA.Id);

        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => ListarRecentes.ExecutarAsync("testes", CancellationToken.None));
    }

    [Fact]
    public async Task Executar_Interno_PedeOEscopoDaListagemDeCadaTipoDeItem()
    {
        // O mesmo escopo da listagem (RN91, RN37): equipes do usuário NESTA área; globais sem filtro de equipe.
        _ambiente.Usuario = UsuarioAtualFalso.Criar(Perfil.Externo, _ambiente.EquipeA.Id);

        await ListarRecentes.ExecutarAsync("testes", CancellationToken.None);

        var escopos = _ambiente.Repositorio.EscoposRecentesPedidos;
        Assert.Equal(_ambiente.Grupo.Grupo.Entidades.Count, escopos.Count);
        var doProjeto = Assert.Single(escopos, e => e.EntidadeId == _ambiente.Grupo.Projeto).Escopo;
        Assert.Equal([_ambiente.EquipeA.Id], doProjeto.EquipesPermitidas!);
        Assert.Equal(_ambiente.Usuario.Id, doProjeto.NaoAprovadosSomenteDoAutor);
        var daComunidade = Assert.Single(escopos, e => e.EntidadeId == _ambiente.Grupo.Comunidade).Escopo;
        Assert.Null(daComunidade.EquipesPermitidas);
    }

    [Fact]
    public async Task Executar_DevolveChaveDoTipoRotuloEDataNaOrdemDoBanco()
    {
        var antigo = new ItemRecente(Guid.NewGuid(), _ambiente.Grupo.Projeto, new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));
        var novo = new ItemRecente(Guid.NewGuid(), _ambiente.Grupo.Relatorio, new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc));
        _ambiente.Repositorio.Recentes.AddRange([antigo, novo]);

        var recentes = await ListarRecentes.ExecutarAsync("testes", CancellationToken.None);

        Assert.Equal([novo.RegistroId, antigo.RegistroId], recentes.Select(r => r.Id));
        Assert.Equal(["relatorio_teste", "projeto"], recentes.Select(r => r.Entidade));
        Assert.Equal(novo.OcorridoEm, recentes[0].OcorridoEm);
    }

    [Fact]
    public async Task Executar_LimitaAOitoItens()
    {
        for (var i = 0; i < 12; i++)
            _ambiente.Repositorio.Recentes.Add(new ItemRecente(Guid.NewGuid(), _ambiente.Grupo.Projeto, DateTime.UtcNow.AddMinutes(-i)));

        var recentes = await ListarRecentes.ExecutarAsync("testes", CancellationToken.None);

        Assert.Equal(ListarRecentes.Limite, recentes.Count);
        Assert.Equal(8, ListarRecentes.Limite);
    }
}
